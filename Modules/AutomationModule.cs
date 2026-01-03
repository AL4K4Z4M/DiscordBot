using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Scrappy.Data;
using Microsoft.EntityFrameworkCore;

namespace Scrappy.Modules;

public class AutomationModule : ScrappyModuleBase
{
    public AutomationModule(BotContext db) : base(db) { }

    [SlashCommand("add-reaction-role", "Add a reaction role to a message.")]
    [RequireUserPermission(GuildPermission.ManageRoles)]
    public async Task AddReactionRole(
        [Summary("message_id", "The ID of the message")] string messageIdStr,
        [Summary("emoji", "The emoji to react with")] string emoji,
        [Summary("role", "The role to grant")] IRole role)
    {
        if (!await CheckPermissionsAsync("Automation")) return;

        if (!ulong.TryParse(messageIdStr, out ulong messageId))
        {
            await RespondAsync("Invalid Message ID.", ephemeral: true);
            return;
        }

        var rr = new ReactionRole
        {
            GuildId = Context.Guild.Id,
            MessageId = messageId,
            Emoji = emoji,
            RoleId = role.Id
        };

        _db.ReactionRoles.Add(rr);
        await _db.SaveChangesAsync();

        var msg = await Context.Channel.GetMessageAsync(messageId) as IUserMessage;
        if (msg != null)
        {
            try
            {
                if (Emoji.TryParse(emoji, out var parsedEmoji)) await msg.AddReactionAsync(parsedEmoji);
                else if (Emote.TryParse(emoji, out var parsedEmote)) await msg.AddReactionAsync(parsedEmote);
            }
            catch { }
        }

        await RespondAsync($"Reaction role added! Users who react with {emoji} will receive the **{role.Name}** role.", ephemeral: true);
    }

    [SlashCommand("add-feed", "Add an RSS feed to monitor (e.g. YouTube).")]
    [RequireUserPermission(GuildPermission.ManageGuild)]
    public async Task AddFeed(
        [Summary("name", "A name for this feed")] string name,
        [Summary("url", "The RSS Feed URL")] string url,
        [Summary("channel", "The channel to post updates in")] ITextChannel channel,
        [Summary("message", "Custom message to post")] string message = "New post found!")
    {
        if (!await CheckPermissionsAsync("Automation")) return;
        if (!await IsFeatureEnabledAsync(s => s.SocialFeedsEnabled)) return;

        var feed = new SocialFeed
        {
            GuildId = Context.Guild.Id,
            ChannelId = channel.Id,
            Name = name,
            Url = url,
            CustomMessage = message
        };

        _db.SocialFeeds.Add(feed);
        await _db.SaveChangesAsync();

        await RespondAsync($"Feed **{name}** added! I will check it every 15 minutes.", ephemeral: true);
    }

    [SlashCommand("list-feeds", "List all active social feeds.")]
    public async Task ListFeeds()
    {
        if (!await CheckPermissionsAsync("Automation")) return;

        var feeds = await _db.SocialFeeds.Where(f => f.GuildId == Context.Guild.Id).ToListAsync();
        if (!feeds.Any())
        {
            await RespondAsync("No feeds configured for this server.", ephemeral: true);
            return;
        }

        var embed = new EmbedBuilder()
            .WithTitle("Social Feeds")
            .WithColor(Color.Green);

        foreach (var f in feeds)
        {
            embed.AddField(f.Name, $"URL: {f.Url}\nChannel: <#{f.ChannelId}>\nLast Check: <t:{new DateTimeOffset(f.LastChecked).ToUnixTimeSeconds()}:R>");
        }

        await RespondAsync(embed: embed.Build(), ephemeral: true);
    }

    [SlashCommand("remove-feed", "Remove a social feed.")]
    public async Task RemoveFeed([Summary("id", "The ID of the feed to remove")] int id)
    {
        if (!await CheckPermissionsAsync("Automation")) return;

        var feed = await _db.SocialFeeds.FirstOrDefaultAsync(f => f.Id == id && f.GuildId == Context.Guild.Id);
        if (feed == null)
        {
            await RespondAsync("Feed not found.", ephemeral: true);
            return;
        }

        _db.SocialFeeds.Remove(feed);
        await _db.SaveChangesAsync();
        await RespondAsync($"Feed **{feed.Name}** removed.", ephemeral: true);
    }
}