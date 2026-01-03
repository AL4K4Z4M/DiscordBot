using System.ComponentModel.DataAnnotations;

namespace Scrappy.Data;

public class SocialFeed
{
    [Key]
    public int Id { get; set; }
    public ulong GuildId { get; set; }
    public ulong ChannelId { get; set; }
    public string Name { get; set; } = ""; // e.g. "YouTube: My Channel"
    public string Url { get; set; } = "";  // The RSS Feed URL
    public string LastItemId { get; set; } = ""; // Last seen entry ID to prevent duplicates
    public string CustomMessage { get; set; } = "New post found!";
    public DateTime LastChecked { get; set; } = DateTime.UtcNow;
}