using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Scrappy;
using Scrappy.Data;
using System.Security.Claims;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseWindowsService();

// 1. Setup Configuration
builder.Configuration.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
builder.Configuration.AddJsonFile("secrets.json", optional: true, reloadOnChange: true);
builder.Configuration.AddEnvironmentVariables();

// 2. Setup Database (SQLite)
builder.Services.AddDbContext<BotContext>(options =>
    options.UseSqlite("Data Source=scrappy.db"));

// 3. Setup Discord Bot Services
var socketConfig = new DiscordSocketConfig
{
    GatewayIntents = GatewayIntents.AllUnprivileged | GatewayIntents.MessageContent | GatewayIntents.GuildMembers | GatewayIntents.GuildPresences,
    AlwaysDownloadUsers = true,
    LogLevel = LogSeverity.Info
};
builder.Services.AddSingleton(socketConfig);
builder.Services.AddSingleton<DiscordSocketClient>();
builder.Services.AddSingleton<Scrappy.Services.SystemMonitorService>();
builder.Services.AddSingleton(x => new InteractionService(x.GetRequiredService<DiscordSocketClient>()));
builder.Services.AddSingleton<BotService>();
builder.Services.AddHostedService<BotService>(x => x.GetRequiredService<BotService>());
builder.Services.AddHostedService<Scrappy.Services.ReminderService>();
builder.Services.AddHostedService<Scrappy.Services.FeedService>();
builder.Services.AddHostedService<Scrappy.Services.FreeGamesService>();

// CRITICAL: Cookie Policy to fix "Correlation Failed" on OAuth redirects
builder.Services.Configure<CookiePolicyOptions>(options =>
{
    options.MinimumSameSitePolicy = SameSiteMode.Lax;
    options.Secure = CookieSecurePolicy.SameAsRequest; 
    options.OnAppendCookie = cookieContext =>
    {
        if (cookieContext.CookieName.Contains("Correlation") || cookieContext.CookieName.Contains("Nonce"))
        {
            cookieContext.CookieOptions.SameSite = SameSiteMode.Lax;
            cookieContext.CookieOptions.Secure = false;
        }
    };
});

// 4. Setup Auth
builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = "Discord";
})
.AddCookie(options => 
{
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
})
.AddDiscord(options =>
{
    options.ClientId = builder.Configuration["Discord:ClientId"] ?? "";
    options.ClientSecret = builder.Configuration["Discord:ClientSecret"] ?? "";
    options.Scope.Add("identify");
    options.Scope.Add("guilds");
    options.SaveTokens = true;

    options.Events.OnCreatingTicket = context =>
    {
        var id = context.User.GetProperty("id").GetString();
        var avatar = context.User.GetProperty("avatar").GetString();
        var discriminator = context.User.GetProperty("discriminator").GetString();
        
        var url = string.IsNullOrEmpty(avatar)
            ? $"https://cdn.discordapp.com/embed/avatars/{(ulong.Parse(discriminator ?? "0") % 5)}.png"
            : $"https://cdn.discordapp.com/avatars/{id}/{avatar}.png";

        context.Identity?.AddClaim(new Claim("urn:discord:avatar", url));
        return Task.CompletedTask;
    };
});

builder.Services.AddAuthorization();

var app = builder.Build();

// 5. Database Migration
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<BotContext>();
    db.Database.EnsureCreated();
}

// 6. Configure Pipeline
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
});

app.UseStaticFiles();
app.UseRouting();
app.UseCookiePolicy();
app.UseAuthentication();
app.UseAuthorization();

// Endpoint for Login
app.MapGet("/login", async (context) =>
{
    await Microsoft.AspNetCore.Authentication.AuthenticationHttpContextExtensions.ChallengeAsync(context, "Discord", new Microsoft.AspNetCore.Authentication.AuthenticationProperties { RedirectUri = "/dashboard" });
});

// Endpoint for Logout
app.MapGet("/logout", async (context) =>
{
    await Microsoft.AspNetCore.Authentication.AuthenticationHttpContextExtensions.SignOutAsync(context, CookieAuthenticationDefaults.AuthenticationScheme);
    context.Response.Redirect("/");
});

// API: Get bot config
app.MapGet("/api/config", (IConfiguration config) => Results.Ok(new { ClientId = config["Discord:ClientId"] }));

// API: Get current user info
app.MapGet("/api/user", (HttpContext context) =>
{
    if (context.User.Identity?.IsAuthenticated != true) return Results.Unauthorized();
    return Results.Ok(new { Name = context.User.Identity.Name, AvatarUrl = context.User.FindFirst("urn:discord:avatar")?.Value });
});

// Serve Dashboard
app.MapGet("/dashboard", (HttpContext context) => 
{
    if (context.User.Identity?.IsAuthenticated != true) return Results.Redirect("/login");
    return Results.File(Path.Combine(app.Environment.WebRootPath, "index.html"), "text/html");
});

app.MapGet("/", (HttpContext context) => 
{
    if (context.User.Identity?.IsAuthenticated == true) return Results.Redirect("/dashboard");
    return Results.File(Path.Combine(app.Environment.WebRootPath, "landing.html"), "text/html");
});

// API: Get all guilds (IDs as Strings!)
app.MapGet("/api/guilds", (DiscordSocketClient client) =>
{
    if (client.ConnectionState != ConnectionState.Connected) return Results.StatusCode(503);
    return Results.Ok(client.Guilds.Select(g => new { Id = g.Id.ToString(), Name = g.Name, IconUrl = g.IconUrl ?? "https://cdn.discordapp.com/embed/avatars/0.png", MemberCount = g.MemberCount }));
});

// API: Get guild details (IDs as Strings!)
app.MapGet("/api/guilds/{id}", async (string id, DiscordSocketClient client, BotContext db, ILogger<Program> logger) =>
{
    if (client.ConnectionState != ConnectionState.Connected) return Results.StatusCode(503);
    if (!ulong.TryParse(id, out var guildId)) return Results.BadRequest();

    var guild = client.GetGuild(guildId) ?? client.Guilds.FirstOrDefault(g => g.Id == guildId);
    
    if (guild == null) {
        logger.LogWarning("Dashboard requested guild {id} but it was not found in cache. Cached count: {count}", id, client.Guilds.Count);
        return Results.NotFound();
    }

    var settings = await db.GuildSettings.FindAsync(guildId) ?? new GuildSettings { GuildId = guildId };

    return Results.Ok(new { 
        Guild = new { 
            Id = guild.Id.ToString(), 
            guild.Name, 
            IconUrl = guild.IconUrl ?? "https://cdn.discordapp.com/embed/avatars/0.png", 
            guild.MemberCount, 
            BotCount = guild.Users.Count(u => u.IsBot), 
            OnlineCount = guild.Users.Count(u => u.Status != UserStatus.Offline), 
            guild.PremiumTier, 
            guild.VerificationLevel, 
            OwnerName = guild.Owner?.Username ?? "Unknown" 
        }, 
        Settings = settings 
    });
});

// API: Update guild settings (Universal)
app.MapPost("/api/guilds/{id}/settings", async (string id, HttpContext context, BotContext db) =>
{
    if (!ulong.TryParse(id, out var guildId)) return Results.BadRequest();
    
    var settings = await db.GuildSettings.FindAsync(guildId);
    if (settings == null) {
        settings = new GuildSettings { GuildId = guildId };
        db.GuildSettings.Add(settings);
    }

    var updates = await context.Request.ReadFromJsonAsync<Dictionary<string, object>>();
    if (updates == null) return Results.BadRequest();

    foreach (var update in updates)
    {
        // Use case-insensitive lookup to match 'wordFilterEnabled' to 'WordFilterEnabled'
        var prop = settings.GetType().GetProperty(update.Key, System.Reflection.BindingFlags.IgnoreCase | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        if (prop != null && prop.CanWrite)
        {
            try {
                var val = update.Value?.ToString();
                if (prop.PropertyType == typeof(bool)) 
                {
                    // Handle JSON boolean types correctly
                    if (update.Value is bool b) prop.SetValue(settings, b);
                    else prop.SetValue(settings, bool.Parse(val ?? "false"));
                }
                else if (prop.PropertyType == typeof(string)) prop.SetValue(settings, val);
                else if (prop.PropertyType == typeof(int)) prop.SetValue(settings, int.Parse(val ?? "0"));
                else if (prop.PropertyType == typeof(ulong?)) prop.SetValue(settings, string.IsNullOrEmpty(val) ? (ulong?)null : ulong.Parse(val));
            } catch { /* Ignore malformed updates */ }
        }
    }

    await db.SaveChangesAsync();
    return Results.Ok(settings);
});

// API: Get guild channels
app.MapGet("/api/guilds/{id}/channels", (string id, DiscordSocketClient client) =>
{
    if (client.ConnectionState != ConnectionState.Connected) return Results.StatusCode(503);
    if (!ulong.TryParse(id, out var guildId)) return Results.BadRequest();
    var guild = client.GetGuild(guildId);
    if (guild == null) return Results.NotFound();

    return Results.Ok(guild.TextChannels
        .OrderBy(c => c.Position)
        .Select(c => new { Id = c.Id.ToString(), Name = c.Name }));
});

// API: Get guild roles
app.MapGet("/api/guilds/{id}/roles", (string id, DiscordSocketClient client) =>
{
    if (client.ConnectionState != ConnectionState.Connected) return Results.StatusCode(503);
    if (!ulong.TryParse(id, out var guildId)) return Results.BadRequest();
    var guild = client.GetGuild(guildId);
    if (guild == null) return Results.NotFound();

    return Results.Ok(guild.Roles
        .Where(r => !r.IsEveryone)
        .OrderByDescending(r => r.Position)
        .Select(r => new { 
            Id = r.Id.ToString(), 
            Name = r.Name, 
            Color = $"#{r.Color.RawValue:X6}" 
        }));
});

// API: Get all social feeds for a guild
app.MapGet("/api/guilds/{id}/feeds", async (string id, BotContext db) =>
{
    if (!ulong.TryParse(id, out var guildId)) return Results.BadRequest();
    var feeds = await db.SocialFeeds.Where(f => f.GuildId == guildId).ToListAsync();
    return Results.Ok(feeds);
});

// API: Add a new social feed
app.MapPost("/api/guilds/{id}/feeds", async (string id, HttpContext context, BotContext db) =>
{
    if (!ulong.TryParse(id, out var guildId)) return Results.BadRequest();
    var feed = await context.Request.ReadFromJsonAsync<SocialFeed>();
    if (feed == null) return Results.BadRequest();

    feed.GuildId = guildId;
    feed.LastChecked = DateTime.UtcNow;
    db.SocialFeeds.Add(feed);
    await db.SaveChangesAsync();
    return Results.Ok(feed);
});

// API: Delete a social feed
app.MapDelete("/api/guilds/{id}/feeds/{feedId}", async (string id, int feedId, BotContext db) =>
{
    if (!ulong.TryParse(id, out var guildId)) return Results.BadRequest();
    var feed = await db.SocialFeeds.FirstOrDefaultAsync(f => f.Id == feedId && f.GuildId == guildId);
    if (feed == null) return Results.NotFound();

    db.SocialFeeds.Remove(feed);
    await db.SaveChangesAsync();
    return Results.NoContent();
});

// API: Get guild analytics (Messages over last 7 days)
app.MapGet("/api/guilds/{id}/analytics", async (string id, BotContext db) =>
{
    if (!ulong.TryParse(id, out var guildId)) return Results.BadRequest();
    
    var last7Days = Enumerable.Range(0, 7)
        .Select(i => DateTime.UtcNow.Date.AddDays(-i))
        .Reverse()
        .ToList();

    var stats = await db.MessageLogs
        .Where(l => l.GuildId == guildId && l.Timestamp >= last7Days[0])
        .GroupBy(l => l.Timestamp.Date)
        .Select(g => new { Date = g.Key, Count = g.Count() })
        .ToListAsync();

    var results = last7Days.Select(date => new {
        label = date.ToString("ddd"),
        count = stats.FirstOrDefault(s => s.Date == date)?.Count ?? 0
    });

    return Results.Ok(results);
});

// API: Get recent bot activity
app.MapGet("/api/guilds/{id}/activity", async (string id, BotContext db) =>
{
    if (!ulong.TryParse(id, out var guildId)) return Results.BadRequest();
    
    // Pull last 5 messages from records as a "Live Feed" proxy
    var activity = await db.MessageRecords
        .Where(m => m.GuildId == guildId)
        .OrderByDescending(m => m.Timestamp)
        .Take(5)
        .Select(m => new { m.Username, m.Content, m.Timestamp })
        .ToListAsync();

    return Results.Ok(activity);
});

app.MapGet("/api/stats", (Scrappy.Services.SystemMonitorService sys, BotService bot) =>
{
    var client = bot.Client;
    return Results.Ok(new
    {
        System = new { CpuUsage = sys.GetCpuUsage(), MemoryUsage = sys.GetMemoryUsage(), Storage = sys.GetStorageInfo(), OS = System.Runtime.InteropServices.RuntimeInformation.OSDescription, ProcessorCount = Environment.ProcessorCount },
        Bot = new { Status = client.ConnectionState.ToString(), Latency = client.Latency, GuildCount = client.Guilds.Count, TotalUsers = client.Guilds.Sum(g => g.MemberCount), Uptime = (DateTime.UtcNow - bot.StartTime).ToString(@"dd\.hh\:mm\:ss") }
    });
});

app.Run();