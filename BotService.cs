using Discord;
using Discord.WebSocket;
using Discord.Interactions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using System.Reflection;

namespace Scrappy;

public class BotService : IHostedService
{
    public DiscordSocketClient Client => _client;
    public DateTime StartTime { get; private set; }

    private readonly DiscordSocketClient _client;
    private readonly InteractionService _interactionService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<BotService> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly Microsoft.Extensions.DependencyInjection.IServiceScopeFactory _scopeFactory;

    public BotService(
        DiscordSocketClient client,
        InteractionService interactionService,
        IConfiguration configuration,
        ILogger<BotService> logger,
        IServiceProvider serviceProvider,
        Microsoft.Extensions.DependencyInjection.IServiceScopeFactory scopeFactory)
    {
        _client = client;
        _interactionService = interactionService;
        _configuration = configuration;
        _logger = logger;
        _serviceProvider = serviceProvider;
        _scopeFactory = scopeFactory;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        StartTime = DateTime.UtcNow;
        _client.Log += LogAsync;
        _interactionService.Log += LogAsync;

        // Ready event to register commands
        _client.Ready += ReadyAsync;
        
        // Handle interactions
        _client.InteractionCreated += HandleInteractionAsync;
        
        // Handle User Joins (Auto-Role & Welcome)
        _client.UserJoined += OnUserJoined;
        _client.UserLeft += OnUserLeft;

        // Handle Messages (Word Filter & Analytics)
        _client.MessageReceived += OnMessageReceived;

        // Handle Voice State (Analytics)
        _client.UserVoiceStateUpdated += OnUserVoiceStateUpdated;

        // Handle Reactions (Reaction Roles)
        _client.ReactionAdded += OnReactionAdded;
        _client.ReactionRemoved += OnReactionRemoved;

        var token = _configuration["Discord:Token"];

        if (string.IsNullOrEmpty(token))
        {
            _logger.LogCritical("Discord token is missing from configuration!");
            return;
        }

        await _client.LoginAsync(TokenType.Bot, token);
        await _client.StartAsync();
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await _client.StopAsync();
    }

    private async Task ReadyAsync()
    {
        try
        {
            // Discover and register commands from this assembly
            await _interactionService.AddModulesAsync(Assembly.GetEntryAssembly(), _serviceProvider);
            
            // Register commands globally (might take up to an hour to propagate)
            // For testing, registering to a specific guild is faster, but for this "Hello" bot, global is fine.
            await _interactionService.RegisterCommandsGloballyAsync();
            
            _logger.LogInformation("Scrappy is connected and ready!");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while registering commands.");
        }
    }

    private async Task HandleInteractionAsync(SocketInteraction interaction)
    {
        try
        {
            var ctx = new SocketInteractionContext(_client, interaction);
            await _interactionService.ExecuteCommandAsync(ctx, _serviceProvider);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling interaction");
            if (interaction.Type == InteractionType.ApplicationCommand)
            {
                await interaction.GetOriginalResponseAsync().ContinueWith(async (msg) => await msg.Result.DeleteAsync());
            }
        }
    }

    private async Task OnUserJoined(SocketGuildUser user)
    {
        try
        {
            using (var scope = _scopeFactory.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<Scrappy.Data.BotContext>();
                var settings = await db.GuildSettings.FindAsync(user.Guild.Id);

                if (settings == null) return;

                // 1. Auto-Role
                if (settings.AutoRoleEnabled && settings.AutoRoleId.HasValue)
                {
                    var role = user.Guild.GetRole(settings.AutoRoleId.Value);
                    if (role != null)
                    {
                        await user.AddRoleAsync(role);
                    }
                }

                // 2. Welcome Message
                if (settings.WelcomeEnabled && settings.WelcomeChannelId.HasValue)
                {
                    var channel = user.Guild.GetTextChannel(settings.WelcomeChannelId.Value);
                    if (channel != null)
                    {
                        var msg = settings.WelcomeMessage ?? "Welcome {user} to {server}!";
                        msg = msg.Replace("{user}", user.Mention)
                                 .Replace("{server}", user.Guild.Name);
                        
                        await channel.SendMessageAsync(msg);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error handling UserJoined for {user.Username}");
        }
    }

    private async Task OnUserLeft(SocketGuild guild, SocketUser user)
    {
        try
        {
            using (var scope = _scopeFactory.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<Scrappy.Data.BotContext>();
                var settings = await db.GuildSettings.FindAsync(guild.Id);

                if (settings == null || !settings.GoodbyeEnabled || !settings.GoodbyeChannelId.HasValue) return;

                var channel = guild.GetTextChannel(settings.GoodbyeChannelId.Value);
                if (channel != null)
                {
                    var msg = settings.GoodbyeMessage ?? "{user} has left the server.";
                    msg = msg.Replace("{user}", user.Username);
                    await channel.SendMessageAsync(msg);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error handling UserLeft for {user.Username} in {guild.Name}");
        }
    }

    private async Task OnMessageReceived(SocketMessage msg)
    {
        if (msg.Author.IsBot || msg is not SocketUserMessage userMsg) return;
        if (userMsg.Channel is not SocketTextChannel channel) return;

        try
        {
            using (var scope = _scopeFactory.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<Scrappy.Data.BotContext>();
                var settings = await db.GuildSettings.FindAsync(channel.Guild.Id);

                // 1. Analytics & Transcripts
                if (settings != null && settings.AnalyticsEnabled)
                {
                    db.MessageLogs.Add(new Scrappy.Data.MessageLog
                    {
                        GuildId = channel.Guild.Id,
                        UserId = msg.Author.Id,
                        ChannelId = channel.Id,
                        Timestamp = DateTime.UtcNow
                    });

                    if (settings.MessageLoggingEnabled)
                    {
                        db.MessageRecords.Add(new Scrappy.Data.MessageRecord
                        {
                            MessageId = msg.Id,
                            GuildId = channel.Guild.Id,
                            ChannelId = channel.Id,
                            UserId = msg.Author.Id,
                            Username = msg.Author.Username,
                            Content = msg.Content,
                            Timestamp = DateTime.UtcNow,
                            AttachmentUrl = msg.Attachments.FirstOrDefault()?.Url
                        });
                    }
                    await db.SaveChangesAsync();
                }

                // 2. Word Filter
                if (settings == null || !settings.WordFilterEnabled || string.IsNullOrEmpty(settings.BannedWords)) return;

                var bannedWords = settings.BannedWords.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                
                foreach (var word in bannedWords)
                {
                    if (userMsg.Content.Contains(word, StringComparison.OrdinalIgnoreCase))
                    {
                        await userMsg.DeleteAsync();
                        
                        // Log to Mod Logs
                        await LogToModLogAsync(channel.Guild, db, "Auto-Mod: Word Filter", 
                            $"**User:** {msg.Author.Mention}\n**Channel:** {channel.Mention}\n**Word:** {word}\n**Message:** {userMsg.Content}", Color.Orange);

                        var warning = await channel.SendMessageAsync($"{msg.Author.Mention}, your message contained a banned word and was removed.");
                        await Task.Delay(3000);
                        await warning.DeleteAsync();
                        break;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling message received");
        }
    }

    private async Task OnUserVoiceStateUpdated(SocketUser user, SocketVoiceState oldState, SocketVoiceState newState)
    {
        try
        {
            // Case 1: User joined a VC
            if (oldState.VoiceChannel == null && newState.VoiceChannel != null)
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<Scrappy.Data.BotContext>();
                
                db.VoiceLogs.Add(new Scrappy.Data.VoiceLog
                {
                    GuildId = newState.VoiceChannel.Guild.Id,
                    UserId = user.Id,
                    JoinTime = DateTime.UtcNow
                });
                await db.SaveChangesAsync();
            }
            // Case 2: User left a VC
            else if (oldState.VoiceChannel != null && newState.VoiceChannel == null)
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<Scrappy.Data.BotContext>();
                
                var log = await db.VoiceLogs
                    .Where(l => l.UserId == user.Id && l.GuildId == oldState.VoiceChannel.Guild.Id && l.LeaveTime == null)
                    .OrderByDescending(l => l.JoinTime)
                    .FirstOrDefaultAsync();

                if (log != null)
                {
                    log.LeaveTime = DateTime.UtcNow;
                    await db.SaveChangesAsync();
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling voice state update");
        }
    }

    private async Task OnReactionAdded(Cacheable<IUserMessage, ulong> cache, Cacheable<IMessageChannel, ulong> channel, SocketReaction reaction)
    {
        if (!reaction.User.IsSpecified || reaction.User.Value.IsBot) return;
        var user = reaction.User.Value as SocketGuildUser;
        if (user == null) return;

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<Scrappy.Data.BotContext>();
            
            var rr = await db.ReactionRoles.FirstOrDefaultAsync(x => x.MessageId == reaction.MessageId && x.Emoji == reaction.Emote.Name);
            if (rr != null)
            {
                var role = user.Guild.GetRole(rr.RoleId);
                if (role != null) await user.AddRoleAsync(role);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling reaction added");
        }
    }

    private async Task OnReactionRemoved(Cacheable<IUserMessage, ulong> cache, Cacheable<IMessageChannel, ulong> channel, SocketReaction reaction)
    {
        if (!reaction.User.IsSpecified || reaction.User.Value.IsBot) return;
        var user = reaction.User.Value as SocketGuildUser;
        if (user == null) return;

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<Scrappy.Data.BotContext>();
            
            var rr = await db.ReactionRoles.FirstOrDefaultAsync(x => x.MessageId == reaction.MessageId && x.Emoji == reaction.Emote.Name);
            if (rr != null)
            {
                var role = user.Guild.GetRole(rr.RoleId);
                if (role != null) await user.RemoveRoleAsync(role);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling reaction removed");
        }
    }

    private async Task LogToModLogAsync(SocketGuild guild, Scrappy.Data.BotContext db, string title, string description, Color color)
    {
        var settings = await db.GuildSettings.FindAsync(guild.Id);
        if (settings == null || !settings.ModLogEnabled || !settings.ModLogChannelId.HasValue) return;

        var channel = guild.GetTextChannel(settings.ModLogChannelId.Value);
        if (channel != null)
        {
            var embed = new EmbedBuilder()
                .WithTitle(title)
                .WithDescription(description)
                .WithColor(color)
                .WithCurrentTimestamp()
                .Build();

            await channel.SendMessageAsync(embed: embed);
        }
    }

    private Task LogAsync(LogMessage log)
    {
        _logger.LogInformation(log.ToString());
        return Task.CompletedTask;
    }
}
