using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using Scrappy.Data;

namespace Scrappy.Modules;

public abstract class ScrappyModuleBase : InteractionModuleBase<SocketInteractionContext>
{
    protected readonly BotContext _db;

    protected ScrappyModuleBase(BotContext db)
    {
        _db = db;
    }

    protected async Task<bool> CheckPermissionsAsync(string group)
    {
        var user = Context.User as SocketGuildUser;
        if (user == null) return false;

        // 1. Administrators always have access
        if (user.GuildPermissions.Administrator) return true;

        // 2. Check if user has a role that was granted permission for this group
        var allowedRoles = await _db.CommandPermissions
            .Where(p => p.GuildId == Context.Guild.Id && p.CommandGroup == group)
            .Select(p => p.RoleId)
            .ToListAsync();

        if (user.Roles.Any(r => allowedRoles.Contains(r.Id))) return true;

        await RespondAsync($"You do not have permission to use {group} commands. An administrator must grant your role access.", ephemeral: true);
        return false;
    }

    protected async Task<bool> IsFeatureEnabledAsync(Func<GuildSettings, bool> predicate)
    {
        var settings = await _db.GuildSettings.FindAsync(Context.Guild.Id) ?? new GuildSettings();
        if (!predicate(settings))
        {
            await RespondAsync("This feature is currently disabled in the server settings.", ephemeral: true);
            return false;
        }
        return true;
    }
}
