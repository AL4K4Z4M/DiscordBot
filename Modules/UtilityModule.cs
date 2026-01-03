using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Scrappy.Data;

namespace Scrappy.Modules;

public class UtilityModule : ScrappyModuleBase
{
    public UtilityModule(BotContext db) : base(db) { }

    [SlashCommand("serverinfo", "Display information about this server.")]
    public async Task ServerInfo()
    {
        if (!await CheckPermissionsAsync("Utility")) return;
        if (!await IsFeatureEnabledAsync(s => s.ServerInfoEnabled)) return;

        var guild = Context.Guild;
        var embed = new EmbedBuilder()
            .WithTitle(guild.Name)
            .WithThumbnailUrl(guild.IconUrl)
            .WithColor(Color.Blue)
            .WithCurrentTimestamp()
            .AddField("Owner", guild.Owner?.Mention ?? "Unknown", true)
            .AddField("Created On", $"<t:{guild.CreatedAt.ToUnixTimeSeconds()}:d>", true)
            .AddField("Members", $"{guild.MemberCount} Total\n{guild.Users.Count(x => !x.IsBot)} Humans\n{guild.Users.Count(x => x.IsBot)} Bots", true)
            .AddField("Channels", $"{guild.TextChannels.Count} Text\n{guild.VoiceChannels.Count} Voice", true)
            .AddField("Roles", guild.Roles.Count.ToString(), true)
            .AddField("Boosts", $"{guild.PremiumSubscriptionCount} (Level {guild.PremiumTier})", true)
            .WithFooter($"ID: {guild.Id}");

        await RespondAsync(embed: embed.Build());
    }

    [SlashCommand("userinfo", "Get information about a user.")]
    public async Task UserInfo(SocketGuildUser? user = null)
    {
        if (!await CheckPermissionsAsync("Utility")) return;
        if (!await IsFeatureEnabledAsync(s => s.UserInfoEnabled)) return;

        user ??= (SocketGuildUser)Context.User;
        var roles = string.Join(", ", user.Roles.Where(x => !x.IsEveryone).Select(x => x.Mention));
        if (string.IsNullOrEmpty(roles)) roles = "None";
        var topRoleColor = user.Roles.OrderByDescending(r => r.Position).FirstOrDefault()?.Color ?? Color.Purple;

        var embed = new EmbedBuilder()
            .WithAuthor(user.GlobalName ?? user.Username, user.GetAvatarUrl() ?? user.GetDefaultAvatarUrl())
            .WithThumbnailUrl(user.GetAvatarUrl(size: 512) ?? user.GetDefaultAvatarUrl())
            .WithColor(topRoleColor)
            .AddField("Username", user.Username, true)
            .AddField("Nickname", user.Nickname ?? "None", true)
            .AddField("ID", user.Id, true)
            .AddField("Joined Server", user.JoinedAt.HasValue ? $"<t:{user.JoinedAt.Value.ToUnixTimeSeconds()}:R>" : "Unknown", true)
            .AddField("Account Created", $"<t:{user.CreatedAt.ToUnixTimeSeconds()}:D>", true)
            .AddField("Roles", roles)
            .WithFooter($"Requested by {Context.User.Username}")
            .WithCurrentTimestamp();

        await RespondAsync(embed: embed.Build());
    }

    [SlashCommand("avatar", "Get a user's avatar.")]
    public async Task Avatar(SocketGuildUser? user = null)
    {
        if (!await CheckPermissionsAsync("Utility")) return;
        if (!await IsFeatureEnabledAsync(s => s.AvatarEnabled)) return;

        user ??= (SocketGuildUser)Context.User;
        var avatarUrl = user.GetAvatarUrl(size: 1024) ?? user.GetDefaultAvatarUrl();

        var embed = new EmbedBuilder()
            .WithTitle($"{user.GlobalName ?? user.Username}'s Avatar")
            .WithImageUrl(avatarUrl)
            .WithColor(Color.Teal);

        await RespondAsync(embed: embed.Build());
    }

    [SlashCommand("free-games", "Check for current free game giveaways.")]
    public async Task FreeGames()
    {
        if (!await CheckPermissionsAsync("Utility")) return;
        // Not specifically toggled but linked to free games feature
        
        await DeferAsync();
        using var client = new HttpClient();
        var response = await client.GetStringAsync("https://www.gamerpower.com/api/giveaways");
        var giveaways = System.Text.Json.JsonSerializer.Deserialize<List<GamerGiveaway>>(response, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (giveaways == null || !giveaways.Any())
        {
            await FollowupAsync("No active giveaways found.");
            return;
        }

        var embed = new EmbedBuilder()
            .WithTitle("🎮 Current Free Games")
            .WithColor(Color.Green);

        foreach (var g in giveaways.Take(5))
        {
            embed.AddField(g.Title, $"**Platform:** {g.Platforms}\n**Worth:** {g.Worth}\n[Claim Here]({g.OpenGiveawayUrl})");
        }

        await FollowupAsync(embed: embed.Build());
    }

    private class GamerGiveaway
    {
        public string Title { get; set; } = "";
        public string Platforms { get; set; } = "";
        public string Worth { get; set; } = "";
        [System.Text.Json.Serialization.JsonPropertyName("open_giveaway_url")]
        public string OpenGiveawayUrl { get; set; } = "";
    }
}