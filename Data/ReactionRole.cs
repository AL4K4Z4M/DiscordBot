using System.ComponentModel.DataAnnotations;

namespace Scrappy.Data;

public class ReactionRole
{
    [Key]
    public int Id { get; set; }
    public ulong GuildId { get; set; }
    public ulong MessageId { get; set; }
    public string Emoji { get; set; } = "";
    public ulong RoleId { get; set; }
}
