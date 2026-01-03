using System.ComponentModel.DataAnnotations;

namespace Scrappy.Data;

public class GuildSettings
{
    [Key]
    public ulong GuildId { get; set; }
    // Automation
    public bool WelcomeEnabled { get; set; } = true;
    public ulong? WelcomeChannelId { get; set; }
    public string? WelcomeMessage { get; set; } = "Welcome {user} to {server}!";
    
    public bool GoodbyeEnabled { get; set; } = true;
    public ulong? GoodbyeChannelId { get; set; }
    public string? GoodbyeMessage { get; set; } = "{user} has left the server.";

    public bool AutoRoleEnabled { get; set; } = true;
    public ulong? AutoRoleId { get; set; }
    public bool SocialFeedsEnabled { get; set; } = true;
    public ulong? FreeGamesChannelId { get; set; }
    public bool FreeGamesEnabled { get; set; } = false;

    // Moderation
    public ulong? ModLogChannelId { get; set; }
    public string? BannedWords { get; set; }
    public bool KickEnabled { get; set; } = true;
    public bool BanEnabled { get; set; } = true;
    public bool MuteEnabled { get; set; } = true;
    public bool PurgeEnabled { get; set; } = true;
    public bool WarnEnabled { get; set; } = true;
    public int WarnLimitBeforeTimeout { get; set; } = 3;
    public int WarnTimeoutDuration { get; set; } = 60; // Minutes
    public bool ModLogEnabled { get; set; } = true;
    public bool WordFilterEnabled { get; set; } = true;

    // Utility
    public bool ServerInfoEnabled { get; set; } = true;
    public bool UserInfoEnabled { get; set; } = true;
    public bool AvatarEnabled { get; set; } = true;
    public bool PollsEnabled { get; set; } = true;
    public bool RemindersEnabled { get; set; } = true;
    
    // Phase 4
    public bool AnalyticsEnabled { get; set; } = true;
    public bool MessageLoggingEnabled { get; set; } = false; // Off by default as it's storage heavy
    
    public bool SayHelloEnabled { get; set; } = true;
}
