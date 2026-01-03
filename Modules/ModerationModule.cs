using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Scrappy.Data;
using Microsoft.EntityFrameworkCore;

namespace Scrappy.Modules;

public class ModerationModule : ScrappyModuleBase
{
    public ModerationModule(BotContext db) : base(db) { }

    [SlashCommand("kick", "Kick a user from the server.")]
    [RequireBotPermission(GuildPermission.KickMembers)]
    public async Task Kick(SocketGuildUser user, string reason = "No reason provided")
    {
        if (!await CheckPermissionsAsync("Moderation")) return;
        if (!await IsFeatureEnabledAsync(s => s.KickEnabled)) return;

        if (user.Id == Context.User.Id)
        {
            await RespondAsync("You cannot kick yourself.", ephemeral: true);
            return;
        }

        try
        {
            await user.KickAsync(reason);
            await RespondAsync($"**{user.Username}** has been kicked. Reason: {reason}");
            await LogActionAsync("User Kicked", $"**User:** {user.Mention} ({user.Id})\n**Moderator:** {Context.User.Mention}\n**Reason:** {reason}", Color.Orange);
        }
        catch (Exception ex)
        {
            await RespondAsync($"Failed to kick user: {ex.Message}", ephemeral: true);
        }
    }

    [SlashCommand("ban", "Ban a user from the server.")]
    [RequireBotPermission(GuildPermission.BanMembers)]
    public async Task Ban(SocketGuildUser user, string reason = "No reason provided", int daysPrune = 0)
    {
        if (!await CheckPermissionsAsync("Moderation")) return;
        if (!await IsFeatureEnabledAsync(s => s.BanEnabled)) return;

        if (user.Id == Context.User.Id)
        {
            await RespondAsync("You cannot ban yourself.", ephemeral: true);
            return;
        }

        try
        {
            await Context.Guild.AddBanAsync(user, daysPrune, reason);
            await RespondAsync($"**{user.Username}** has been banned. Reason: {reason}");
            await LogActionAsync("User Banned", $"**User:** {user.Mention} ({user.Id})\n**Moderator:** {Context.User.Mention}\n**Reason:** {reason}", Color.Red);
        }
        catch (Exception ex)
        {
            await RespondAsync($"Failed to ban user: {ex.Message}", ephemeral: true);
        }
    }

    [SlashCommand("timeout", "Timeout (mute) a user.")]
    [RequireBotPermission(GuildPermission.ModerateMembers)]
    public async Task Timeout(SocketGuildUser user, string duration, string reason = "No reason provided")
    {
        if (!await CheckPermissionsAsync("Moderation")) return;
        if (!await IsFeatureEnabledAsync(s => s.MuteEnabled)) return;

        TimeSpan span;
        if (duration.EndsWith("m") && int.TryParse(duration.TrimEnd('m'), out int minutes)) span = TimeSpan.FromMinutes(minutes);
        else if (duration.EndsWith("h") && int.TryParse(duration.TrimEnd('h'), out int hours)) span = TimeSpan.FromHours(hours);
        else if (duration.EndsWith("d") && int.TryParse(duration.TrimEnd('d'), out int days)) span = TimeSpan.FromDays(days);
        else 
        {
            await RespondAsync("Invalid duration format. Use 10m, 1h, 1d etc.", ephemeral: true);
            return;
        }

        try
        {
            await user.SetTimeOutAsync(span, new RequestOptions { AuditLogReason = reason });
            await RespondAsync($"**{user.Username}** has been timed out for {duration}. Reason: {reason}");
            await LogActionAsync("User Timed Out", $"**User:** {user.Mention} ({user.Id})\n**Moderator:** {Context.User.Mention}\n**Duration:** {duration}\n**Reason:** {reason}", Color.Gold);
        }
        catch (Exception ex)
        {
            await RespondAsync($"Failed to timeout user: {ex.Message}", ephemeral: true);
        }
    }

    [SlashCommand("purge", "Bulk delete messages.")]
    [RequireBotPermission(GuildPermission.ManageMessages)]
    public async Task Purge(int amount)
    {
        if (!await CheckPermissionsAsync("Moderation")) return;
        if (!await IsFeatureEnabledAsync(s => s.PurgeEnabled)) return;

        if (amount < 1 || amount > 100)
        {
            await RespondAsync("Amount must be between 1 and 100.", ephemeral: true);
            return;
        }

        var channel = (ITextChannel)Context.Channel;
        var messages = await channel.GetMessagesAsync(amount).FlattenAsync();
        
        try
        {
            await channel.DeleteMessagesAsync(messages);
            await RespondAsync($"Deleted {messages.Count()} messages.", ephemeral: true);
            await LogActionAsync("Messages Purged", $"**Channel:** {channel.Mention}\n**Moderator:** {Context.User.Mention}\n**Amount:** {messages.Count()}", Color.Blue);
            await Task.Delay(3000);
            await DeleteOriginalResponseAsync();
        }
        catch (Exception ex)
        {
            await RespondAsync($"Failed to purge messages: {ex.Message}", ephemeral: true);
        }
    }

    [SlashCommand("warn", "Issue a warning to a user.")]
    public async Task Warn(SocketGuildUser user, string reason = "No reason provided")
    {
        if (!await CheckPermissionsAsync("Moderation")) return;
        if (!await IsFeatureEnabledAsync(s => s.WarnEnabled)) return;

        var warn = new Warn
        {
            GuildId = Context.Guild.Id,
            UserId = user.Id,
            ModeratorId = Context.User.Id,
            Reason = reason
        };

        _db.Warns.Add(warn);
        await _db.SaveChangesAsync();

        await RespondAsync($"**{user.Username}** has been warned. Reason: {reason}");
        await LogActionAsync("User Warned", $"**User:** {user.Mention} ({user.Id})\n**Moderator:** {Context.User.Mention}\n**Reason:** {reason}", Color.Orange);

        var settings = await _db.GuildSettings.FindAsync(Context.Guild.Id);
        if (settings != null)
        {
            var warnCount = await _db.Warns.CountAsync(w => w.GuildId == Context.Guild.Id && w.UserId == user.Id);
            if (warnCount >= settings.WarnLimitBeforeTimeout)
            {
                try
                {
                    await user.SetTimeOutAsync(TimeSpan.FromMinutes(settings.WarnTimeoutDuration), new RequestOptions { AuditLogReason = "Auto-punish: Warn limit reached." });
                    await FollowupAsync($"**{user.Username}** has been automatically timed out for {settings.WarnTimeoutDuration} minutes due to reaching {warnCount} warnings.");
                    await LogActionAsync("Auto-Punishment Applied", $"**User:** {user.Mention}\n**Action:** Timeout ({settings.WarnTimeoutDuration}m)\n**Reason:** Reached warning limit ({warnCount})", Color.DarkRed);
                }
                catch (Exception ex)
                {
                    _ = FollowupAsync($"Failed to apply auto-punishment: {ex.Message}", ephemeral: true);
                }
            }
        }
    }

    [SlashCommand("warns", "View warnings for a user.")]
    public async Task ViewWarns(SocketGuildUser user)
    {
        if (!await CheckPermissionsAsync("Moderation")) return;
        if (!await IsFeatureEnabledAsync(s => s.WarnEnabled)) return;

        var warns = await _db.Warns
            .Where(w => w.GuildId == Context.Guild.Id && w.UserId == user.Id)
            .OrderByDescending(w => w.CreatedAt)
            .ToListAsync();

        if (!warns.Any())
        {
            await RespondAsync($"{user.Username} has no warnings.", ephemeral: true);
            return;
        }

        var embed = new EmbedBuilder()
            .WithTitle($"Warnings for {user.Username}")
            .WithColor(Color.Orange)
            .WithThumbnailUrl(user.GetAvatarUrl() ?? user.GetDefaultAvatarUrl());

        foreach (var warn in warns.Take(10))
        {
            embed.AddField($"ID: {warn.Id} | <t:{new DateTimeOffset(warn.CreatedAt).ToUnixTimeSeconds()}:d>", 
                           $"**Reason:** {warn.Reason}\n**Mod:** <@{warn.ModeratorId}>");
        }

        await RespondAsync(embed: embed.Build());
    }

    private async Task LogActionAsync(string title, string description, Color color)
    {
        var settings = await _db.GuildSettings.FindAsync(Context.Guild.Id);
        if (settings == null || !settings.ModLogEnabled || !settings.ModLogChannelId.HasValue) return;

        var channel = Context.Guild.GetTextChannel(settings.ModLogChannelId.Value);
        if (channel == null) return;

        var embed = new EmbedBuilder()
            .WithTitle(title)
            .WithDescription(description)
            .WithColor(color)
            .WithCurrentTimestamp()
            .Build();

        await channel.SendMessageAsync(embed: embed);
    }
}