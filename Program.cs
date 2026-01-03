using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OAuth;
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

// 4. Setup Web Services (Blazor & Auth)
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
    });
});

builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor(options =>
{
    options.DetailedErrors = true;
});

// CRITICAL: Cookie Policy to fix "Correlation Failed" on mobile/external access
builder.Services.Configure<CookiePolicyOptions>(options =>
{
    // This allows cookies to be set on HTTP (non-secure) connections for development
    options.MinimumSameSitePolicy = SameSiteMode.Lax;
    options.Secure = CookieSecurePolicy.SameAsRequest; 
    
    options.OnAppendCookie = cookieContext =>
    {
        // Force Lax for Auth and Correlation cookies to ensure they survive the OAuth redirect
        if (cookieContext.CookieName.StartsWith(".AspNetCore.Cookies") || 
            cookieContext.CookieName.StartsWith(".AspNetCore.Correlation") ||
            cookieContext.CookieName.Contains("Nonce"))
        {
            cookieContext.CookieOptions.SameSite = SameSiteMode.Lax;
            cookieContext.CookieOptions.Secure = false; // Important since we are on HTTP
        }
    };
});

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

var app = builder.Build();

// 5. Database Migration
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<BotContext>();
    db.Database.EnsureCreated();
}

// 6. Configure Pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
}

// Important for proxy/external IP handling
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
});

app.UseCors("AllowAll");

app.UseStaticFiles();
app.UseRouting();

// Apply the Cookie Policy BEFORE Authentication
app.UseCookiePolicy();
app.UseAuthentication();
app.UseAuthorization();

app.MapBlazorHub(options =>
{
    options.Transports = Microsoft.AspNetCore.Http.Connections.HttpTransportType.WebSockets | 
                         Microsoft.AspNetCore.Http.Connections.HttpTransportType.LongPolling;
});
app.MapFallbackToPage("/_Host");

// Endpoint for Login
app.MapGet("/login", async (context) =>
{
    await Microsoft.AspNetCore.Authentication.AuthenticationHttpContextExtensions.ChallengeAsync(context, "Discord", new Microsoft.AspNetCore.Authentication.AuthenticationProperties { RedirectUri = "/" });
});

// Endpoint for Logout
app.MapGet("/logout", async (context) =>
{
    await Microsoft.AspNetCore.Authentication.AuthenticationHttpContextExtensions.SignOutAsync(context, CookieAuthenticationDefaults.AuthenticationScheme);
    context.Response.Redirect("/");
});

app.MapGet("/api/stats", (Scrappy.Services.SystemMonitorService sys, BotService bot) =>
{
    var client = bot.Client;
    var guilds = client.Guilds.Select(g => new
    {
        g.Id,
        g.Name,
        MemberCount = g.MemberCount,
        IconUrl = g.IconUrl
    }).ToList();

    return Results.Ok(new
    {
        System = new
        {
            CpuUsage = sys.GetCpuUsage(),
            MemoryUsage = sys.GetMemoryUsage(),
            Storage = sys.GetStorageInfo(),
            OS = System.Runtime.InteropServices.RuntimeInformation.OSDescription,
            ProcessorCount = Environment.ProcessorCount
        },
        Bot = new
        {
            Status = client.ConnectionState.ToString(),
            Latency = client.Latency,
            GuildCount = client.Guilds.Count,
            TotalUsers = client.Guilds.Sum(g => g.MemberCount),
            Uptime = (DateTime.UtcNow - bot.StartTime).ToString(@"dd\.hh\:mm\:ss"),
            Guilds = guilds
        }
    });
});

app.Run();




