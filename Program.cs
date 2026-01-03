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

// Serve Dashboard
app.MapGet("/dashboard", (HttpContext context) => 
{
    if (context.User.Identity?.IsAuthenticated != true) return Results.Redirect("/login");
    return Results.File(Path.Combine(app.Environment.WebRootPath, "index.html"), "text/html");
});

app.MapGet("/", () => Results.Redirect("/dashboard"));

// API: Get all guilds
app.MapGet("/api/guilds", (DiscordSocketClient client) =>
{
    return Results.Ok(client.Guilds.Select(g => new
    {
        g.Id,
        g.Name,
        IconUrl = g.IconUrl ?? "https://cdn.discordapp.com/embed/avatars/0.png",
        MemberCount = g.MemberCount
    }));
});

// API: Get guild details
app.MapGet("/api/guilds/{id}", async (ulong id, DiscordSocketClient client, BotContext db) =>
{
    var guild = client.GetGuild(id);
    if (guild == null) return Results.NotFound();
    var settings = await db.GuildSettings.FindAsync(id) ?? new GuildSettings { GuildId = id };
    return Results.Ok(new { Guild = new { guild.Id, guild.Name, IconUrl = guild.IconUrl ?? "https://cdn.discordapp.com/embed/avatars/0.png", guild.MemberCount, BotCount = guild.Users.Count(u => u.IsBot), OnlineCount = guild.Users.Count(u => u.Status != UserStatus.Offline), guild.PremiumTier, guild.VerificationLevel, OwnerName = guild.Owner?.Username ?? "Unknown" }, Settings = settings });
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