using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Scrappy.Data;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Scrappy.Services;

public class FreeGamesService : BackgroundService
{
    private readonly DiscordSocketClient _client;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<FreeGamesService> _logger;
    private readonly HttpClient _httpClient;
    private const string ApiUrl = "https://www.gamerpower.com/api/giveaways";

    public FreeGamesService(DiscordSocketClient client, IServiceScopeFactory scopeFactory, ILogger<FreeGamesService> logger)
    {
        _client = client;
        _scopeFactory = scopeFactory;
        _logger = logger;
        _httpClient = new HttpClient();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Wait for bot to be ready
        while (_client.ConnectionState != ConnectionState.Connected)
        {
            await Task.Delay(5000, stoppingToken);
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckForFreeGamesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in FreeGamesService loop");
            }

            await Task.Delay(TimeSpan.FromHours(4), stoppingToken); // Check every 4 hours
        }
    }

    private async Task CheckForFreeGamesAsync()
    {
        var response = await _httpClient.GetStringAsync(ApiUrl);
        var giveaways = JsonSerializer.Deserialize<List<Giveaway>>(response, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (giveaways == null || !giveaways.Any()) return;

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BotContext>();

        var guilds = await db.GuildSettings.Where(s => s.FreeGamesEnabled && s.FreeGamesChannelId != null).ToListAsync();

        foreach (var guildSettings in guilds)
        {
            var guild = _client.GetGuild(guildSettings.GuildId);
            if (guild == null) continue;

            var channel = guild.GetTextChannel(guildSettings.FreeGamesChannelId!.Value);
            if (channel == null) continue;

            foreach (var giveaway in giveaways.Take(5)) // Just process the latest few
            {
                // Check if already posted in this guild
                bool alreadyPosted = await db.PostedGiveaways.AnyAsync(p => p.GuildId == guild.Id && p.GiveawayId == giveaway.Id);
                if (alreadyPosted) continue;

                var embed = new EmbedBuilder()
                    .WithTitle(giveaway.Title)
                    .WithDescription(giveaway.Description)
                    .WithUrl(giveaway.OpenGiveawayUrl)
                    .WithImageUrl(giveaway.Image)
                    .WithColor(Color.Green)
                    .AddField("Platform", giveaway.Platforms, true)
                    .AddField("Type", giveaway.Type, true)
                    .AddField("Worth", giveaway.Worth, true)
                    .WithFooter("Free Games Tracker • GamerPower")
                    .WithCurrentTimestamp()
                    .Build();

                await channel.SendMessageAsync(embed: embed);

                db.PostedGiveaways.Add(new PostedGiveaway
                {
                    GuildId = guild.Id,
                    GiveawayId = giveaway.Id
                });
            }
        }

        await db.SaveChangesAsync();
    }

    private class Giveaway
    {
        public int Id { get; set; }
        public string Title { get; set; } = "";
        public string Description { get; set; } = "";
        public string Image { get; set; } = "";
        public string Type { get; set; } = "";
        public string Platforms { get; set; } = "";
        public string Worth { get; set; } = "";
        [JsonPropertyName("open_giveaway_url")]
        public string OpenGiveawayUrl { get; set; } = "";
    }
}
