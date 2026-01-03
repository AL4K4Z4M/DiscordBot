using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Scrappy.Data;
using Microsoft.EntityFrameworkCore;

namespace Scrappy.Modules;

[Group("config", "Configure Scrappy's features.")]
[RequireUserPermission(GuildPermission.Administrator)]
public class ConfigModule : InteractionModuleBase<SocketInteractionContext>
{
    private readonly BotContext _db;

    public ConfigModule(BotContext db)
    {
        _db = db;
    }

    [SlashCommand("welcome", "Configure welcome messages.")]
    public async Task ConfigWelcome(bool? enabled = null, ITextChannel? channel = null, string? message = null)
    {
        var s = await GetSettings();
        if (enabled.HasValue) s.WelcomeEnabled = enabled.Value;
        if (channel != null) s.WelcomeChannelId = channel.Id;
        if (!string.IsNullOrEmpty(message)) s.WelcomeMessage = message;
        
        await _db.SaveChangesAsync();
        await RespondAsync("Welcome configuration updated!", ephemeral: true);
    }

    [SlashCommand("goodbye", "Configure goodbye messages.")]
    public async Task ConfigGoodbye(bool? enabled = null, ITextChannel? channel = null, string? message = null)
    {
        var s = await GetSettings();
        if (enabled.HasValue) s.GoodbyeEnabled = enabled.Value;
        if (channel != null) s.GoodbyeChannelId = channel.Id;
        if (!string.IsNullOrEmpty(message)) s.GoodbyeMessage = message;
        
        await _db.SaveChangesAsync();
        await RespondAsync("Goodbye configuration updated!", ephemeral: true);
    }

    [SlashCommand("autorole", "Configure auto-assign role.")]
    public async Task ConfigAutoRole(bool? enabled = null, IRole? role = null)
    {
        var s = await GetSettings();
        if (enabled.HasValue) s.AutoRoleEnabled = enabled.Value;
        if (role != null) s.AutoRoleId = role.Id;
        
        await _db.SaveChangesAsync();
        await RespondAsync("Auto-Role configuration updated!", ephemeral: true);
    }

    [SlashCommand("modlog", "Configure moderation logging.")]
    public async Task ConfigModLog(bool? enabled = null, ITextChannel? channel = null)
    {
        var s = await GetSettings();
        if (enabled.HasValue) s.ModLogEnabled = enabled.Value;
        if (channel != null) s.ModLogChannelId = channel.Id;
        
        await _db.SaveChangesAsync();
        await RespondAsync("Mod Log configuration updated!", ephemeral: true);
    }

    [SlashCommand("wordfilter", "Configure the auto-mod word filter.")]
    public async Task ConfigWordFilter(bool? enabled = null, string? words = null)
    {
        var s = await GetSettings();
        if (enabled.HasValue) s.WordFilterEnabled = enabled.Value;
        if (!string.IsNullOrEmpty(words)) s.BannedWords = words;
        
        await _db.SaveChangesAsync();
        await RespondAsync("Word Filter configuration updated!", ephemeral: true);
    }

    [SlashCommand("autopunish", "Configure automatic punishments for warnings.")]
    public async Task ConfigAutoPunish(int? limit = null, int? durationMinutes = null)
    {
        var s = await GetSettings();
        if (limit.HasValue) s.WarnLimitBeforeTimeout = limit.Value;
        if (durationMinutes.HasValue) s.WarnTimeoutDuration = durationMinutes.Value;
        
        await _db.SaveChangesAsync();
        await RespondAsync("Auto-Punish configuration updated!", ephemeral: true);
    }

    [SlashCommand("analytics", "Toggle server analytics tracking.")]
    public async Task ConfigAnalytics(bool enabled)
    {
        var s = await GetSettings();
        s.AnalyticsEnabled = enabled;
        await _db.SaveChangesAsync();
        await RespondAsync($"Analytics tracking turned {(enabled ? "ON" : "OFF")}.", ephemeral: true);
    }

    [SlashCommand("logging", "Toggle full message content logging (for transcripts).")]
    public async Task ConfigLogging(bool enabled)
    {
        var s = await GetSettings();
        s.MessageLoggingEnabled = enabled;
        await _db.SaveChangesAsync();
        await RespondAsync($"Full message logging turned {(enabled ? "ON" : "OFF")}.", ephemeral: true);
    }

    [SlashCommand("freegames", "Configure the automatic free games tracker.")]
    public async Task ConfigFreeGames(bool? enabled = null, ITextChannel? channel = null)
    {
        var s = await GetSettings();
        if (enabled.HasValue) s.FreeGamesEnabled = enabled.Value;
        if (channel != null) s.FreeGamesChannelId = channel.Id;
        
        await _db.SaveChangesAsync();
        await RespondAsync("Free Games Tracker configuration updated!", ephemeral: true);
    }

    [SlashCommand("view", "View current server configuration.")]
    public async Task ViewConfig()
    {
        var s = await GetSettings();
        var embed = new EmbedBuilder()
            .WithTitle("Server Configuration")
            .WithColor(Color.Blue)
            .AddField("Welcome", s.WelcomeEnabled ? $"ON (<#{s.WelcomeChannelId}>)" : "OFF", true)
            .AddField("Goodbye", s.GoodbyeEnabled ? $"ON (<#{s.GoodbyeChannelId}>)" : "OFF", true)
            .AddField("Auto-Role", s.AutoRoleEnabled ? $"ON (<@&{s.AutoRoleId}>)" : "OFF", true)
            .AddField("Mod Logs", s.ModLogEnabled ? $"ON (<#{s.ModLogChannelId}>)" : "OFF", true)
            .AddField("Word Filter", s.WordFilterEnabled ? "ON" : "OFF", true)
            .AddField("Auto-Punish", $"Limit: {s.WarnLimitBeforeTimeout}, Duration: {s.WarnTimeoutDuration}m", true)
            .AddField("Analytics", s.AnalyticsEnabled ? "ON" : "OFF", true)
            .AddField("Msg Logging", s.MessageLoggingEnabled ? "ON" : "OFF", true)
            .AddField("Free Games", s.FreeGamesEnabled ? $"ON (<#{s.FreeGamesChannelId}>)" : "OFF", true)
            .Build();

        await RespondAsync(embed: embed);
    }

    [SlashCommand("grant", "Grant a role permission to use a command group.")]
    public async Task GrantPermission(
        [Summary("group", "The group to grant access to")] [Choice("Moderation", "Moderation"), Choice("Utility", "Utility"), Choice("Automation", "Automation")] string group,
        [Summary("role", "The role to grant access to")] IRole role)
    {
        var existing = await _db.CommandPermissions.FirstOrDefaultAsync(p => p.GuildId == Context.Guild.Id && p.CommandGroup == group && p.RoleId == role.Id);
        if (existing != null)
        {
            await RespondAsync($"Role {role.Name} already has access to {group} commands.", ephemeral: true);
            return;
        }

        _db.CommandPermissions.Add(new CommandPermission
        {
            GuildId = Context.Guild.Id,
            CommandGroup = group,
            RoleId = role.Id
        });

        await _db.SaveChangesAsync();
        await RespondAsync($"Granted {role.Mention} access to **{group}** commands.", ephemeral: true);
    }

    [SlashCommand("revoke", "Revoke a role's permission to use a command group.")]
    public async Task RevokePermission(
        [Summary("group", "The group to revoke access from")] [Choice("Moderation", "Moderation"), Choice("Utility", "Utility"), Choice("Automation", "Automation")] string group,
        [Summary("role", "The role to revoke access from")] IRole role)
    {
        var p = await _db.CommandPermissions.FirstOrDefaultAsync(p => p.GuildId == Context.Guild.Id && p.CommandGroup == group && p.RoleId == role.Id);
        if (p == null)
        {
            await RespondAsync($"Role {role.Name} does not have access to {group} commands.", ephemeral: true);
            return;
        }

        _db.CommandPermissions.Remove(p);
        await _db.SaveChangesAsync();
        await RespondAsync($"Revoked {role.Mention} access to **{group}** commands.", ephemeral: true);
    }

    [SlashCommand("list-permissions", "List all role-based permissions.")]
    public async Task ListPermissions()
    {
        var perms = await _db.CommandPermissions.Where(p => p.GuildId == Context.Guild.Id).ToListAsync();
        if (!perms.Any())
        {
            await RespondAsync("No role-based permissions configured. (Admins always have access)", ephemeral: true);
            return;
        }

        var embed = new EmbedBuilder()
            .WithTitle("Command Group Permissions")
            .WithColor(Color.Blue);

        foreach (var group in perms.GroupBy(p => p.CommandGroup))
        {
            embed.AddField(group.Key, string.Join("\n", group.Select(p => $"<@&{p.RoleId}>")));
        }

        await RespondAsync(embed: embed.Build(), ephemeral: true);
    }

    private async Task<GuildSettings> GetSettings()
    {
        var settings = await _db.GuildSettings.FindAsync(Context.Guild.Id);
        if (settings == null)
        {
            settings = new GuildSettings { GuildId = Context.Guild.Id };
            _db.GuildSettings.Add(settings);
        }
        return settings;
    }
}
