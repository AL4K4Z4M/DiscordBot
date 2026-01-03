using System.ComponentModel.DataAnnotations;

namespace Scrappy.Data;

public class MessageLog
{
    [Key]
    public int Id { get; set; }
    public ulong GuildId { get; set; }
    public ulong UserId { get; set; }
    public ulong ChannelId { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

public class VoiceLog
{
    [Key]
    public int Id { get; set; }
    public ulong GuildId { get; set; }
    public ulong UserId { get; set; }
    public DateTime JoinTime { get; set; } = DateTime.UtcNow;
    public DateTime? LeaveTime { get; set; }
}

public class MessageRecord
{
    [Key]
    public ulong MessageId { get; set; }
    public ulong GuildId { get; set; }
    public ulong ChannelId { get; set; }
    public ulong UserId { get; set; }
    public string Username { get; set; } = "";
    public string Content { get; set; } = "";
    public string? AttachmentUrl { get; set; }
    public DateTime Timestamp { get; set; }
}
