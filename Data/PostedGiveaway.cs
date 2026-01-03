using System.ComponentModel.DataAnnotations;

namespace Scrappy.Data;

public class PostedGiveaway
{
    [Key]
    public int Id { get; set; }
    public ulong GuildId { get; set; }
    public int GiveawayId { get; set; } // The ID from GamerPower API
    public DateTime PostedAt { get; set; } = DateTime.UtcNow;
}
