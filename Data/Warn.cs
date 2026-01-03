using System.ComponentModel.DataAnnotations;

namespace Scrappy.Data;

public class Warn
{
    [Key]
    public int Id { get; set; }
    public ulong GuildId { get; set; }
    public ulong UserId { get; set; }
    public string Reason { get; set; } = "No reason provided";
    public ulong ModeratorId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
