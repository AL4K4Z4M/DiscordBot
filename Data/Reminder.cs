using System.ComponentModel.DataAnnotations;

namespace Scrappy.Data;

public class Reminder
{
    [Key]
    public int Id { get; set; }
    public ulong UserId { get; set; }
    public ulong ChannelId { get; set; }
    public ulong GuildId { get; set; }
    public string Message { get; set; } = "";
    public DateTime TargetTime { get; set; }
    public bool IsCompleted { get; set; } = false;
}
