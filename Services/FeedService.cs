using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Scrappy.Data;
using Microsoft.EntityFrameworkCore;
using System.ServiceModel.Syndication;
using System.Xml;

namespace Scrappy.Services;

public class FeedService : BackgroundService
{
    private readonly DiscordSocketClient _client;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<FeedService> _logger;
    private readonly HttpClient _httpClient;

    public FeedService(DiscordSocketClient client, IServiceScopeFactory scopeFactory, ILogger<FeedService> logger)
    {
        _client = client;
        _scopeFactory = scopeFactory;
        _logger = logger;
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "ScrappyBot/2.5");
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
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<BotContext>();
                
                var feeds = await db.SocialFeeds.ToListAsync(stoppingToken);
                foreach (var feed in feeds)
                {
                    await CheckFeedAsync(feed, db);
                }
                
                await db.SaveChangesAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in FeedService loop");
            }

            await Task.Delay(TimeSpan.FromMinutes(15), stoppingToken);
        }
    }

    private async Task CheckFeedAsync(SocialFeed feed, BotContext db)
    {
        try
        {
            using var response = await _httpClient.GetStreamAsync(feed.Url);
            using var reader = XmlReader.Create(response);
            var syndicationFeed = SyndicationFeed.Load(reader);
            
            var latestItem = syndicationFeed.Items.FirstOrDefault();
            if (latestItem == null) return;

            string itemId = latestItem.Id ?? latestItem.Links.FirstOrDefault()?.Uri.ToString() ?? "";

            // If this is the first check, just save the ID and move on
            if (string.IsNullOrEmpty(feed.LastItemId))
            {
                feed.LastItemId = itemId;
                feed.LastChecked = DateTime.UtcNow;
                return;
            }

            // If new item found
            if (itemId != feed.LastItemId)
            {
                var channel = _client.GetChannel(feed.ChannelId) as ITextChannel;
                if (channel != null)
                {
                    var link = latestItem.Links.FirstOrDefault()?.Uri.ToString();
                    var title = latestItem.Title.Text;
                    
                    var message = $"{feed.CustomMessage}\n\n**{title}**\n{link}";
                    await channel.SendMessageAsync(message);
                }

                feed.LastItemId = itemId;
                feed.LastChecked = DateTime.UtcNow;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"Failed to check feed {feed.Name} ({feed.Url}): {ex.Message}");
        }
    }
}
