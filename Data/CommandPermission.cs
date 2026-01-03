using System.ComponentModel.DataAnnotations;

namespace Scrappy.Data;

public class CommandPermission
{
    [Key]
    public int Id { get; set; }
    public ulong GuildId { get; set; }
    public string CommandGroup { get; set; } = ""; // e.g., "Moderation", "Utility"
    public ulong RoleId { get; set; }
}
