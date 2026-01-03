using Microsoft.EntityFrameworkCore;

namespace Scrappy.Data;

public class BotContext : DbContext
{
    public BotContext(DbContextOptions<BotContext> options) : base(options) { }

    public DbSet<GuildSettings> GuildSettings { get; set; }
    public DbSet<Warn> Warns { get; set; }
    public DbSet<Reminder> Reminders { get; set; }
    public DbSet<MessageLog> MessageLogs { get; set; }
    public DbSet<VoiceLog> VoiceLogs { get; set; }
    public DbSet<MessageRecord> MessageRecords { get; set; }
    public DbSet<ReactionRole> ReactionRoles { get; set; }
    public DbSet<SocialFeed> SocialFeeds { get; set; }
    public DbSet<PostedGiveaway> PostedGiveaways { get; set; }
    public DbSet<CommandPermission> CommandPermissions { get; set; }
}
