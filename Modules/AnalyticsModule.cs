using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using Scrappy.Data;
using System.Text;

namespace Scrappy.Modules;

public class AnalyticsModule : ScrappyModuleBase
{
    public AnalyticsModule(BotContext db) : base(db) { }

    [SlashCommand("stats", "View server activity statistics.")]
    public async Task ViewStats()
    {
        if (!await CheckPermissionsAsync("Utility")) return; // Analytics usually fall under Utility or Admin
        if (!await IsFeatureEnabledAsync(s => s.AnalyticsEnabled)) return;

        var messageCount = await _db.MessageLogs.CountAsync(l => l.GuildId == Context.Guild.Id);
        var todayCount = await _db.MessageLogs.CountAsync(l => l.GuildId == Context.Guild.Id && l.Timestamp >= DateTime.UtcNow.Date);
        
        var topUser = await _db.MessageLogs
            .Where(l => l.GuildId == Context.Guild.Id)
            .GroupBy(l => l.UserId)
            .Select(g => new { UserId = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .FirstOrDefaultAsync();

        var embed = new EmbedBuilder()
            .WithTitle("📊 Server Analytics")
            .WithColor(Color.Blue)
            .AddField("Total Messages", messageCount, true)
            .AddField("Messages Today", todayCount, true)
            .AddField("Top Contributor", topUser != null ? $"<@{topUser.UserId}> ({topUser.Count} msgs)" : "None", true)
            .WithCurrentTimestamp()
            .Build();

        await RespondAsync(embed: embed);
    }

    [SlashCommand("transcript", "Generate an HTML transcript of this channel.")]
    [RequireUserPermission(GuildPermission.ManageMessages)]
    public async Task GenerateTranscript(int limit = 100)
    {
        if (!await CheckPermissionsAsync("Moderation")) return; // Transcripts usually for mods
        if (!await IsFeatureEnabledAsync(s => s.MessageLoggingEnabled)) return;

        await DeferAsync();

        var messages = await _db.MessageRecords
            .Where(m => m.ChannelId == Context.Channel.Id)
            .OrderByDescending(m => m.Timestamp)
            .Take(limit)
            .Reverse()
            .ToListAsync();

        if (!messages.Any())
        {
            await FollowupAsync("No logged messages found for this channel.");
            return;
        }

        var html = new StringBuilder();
        html.Append("<!DOCTYPE html><html><head><style>");
        html.Append("body { font-family: sans-serif; background: #36393f; color: white; padding: 20px; }");
        html.Append(".msg { margin-bottom: 15px; border-bottom: 1px solid #444; padding-bottom: 10px; }");
        html.Append(".user { font-weight: bold; color: #7289da; }");
        html.Append(".time { font-size: 0.8em; color: #888; margin-left: 10px; }");
        html.Append(".content { margin-top: 5px; }");
        html.Append("</style></head><body>");
        html.Append($"<h1>Transcript for #{Context.Channel.Name}</h1>");

        foreach (var msg in messages)
        {
            html.Append("<div class='msg'>");
            html.Append($"<span class='user'>{msg.Username}</span>");
            html.Append($"<span class='time'>{msg.Timestamp:yyyy-MM-dd HH:mm:ss}</span>");
            html.Append($"<div class='content'>{msg.Content}</div>");
            if (!string.IsNullOrEmpty(msg.AttachmentUrl))
            {
                html.Append($"<div class='attachment'><a href='{msg.AttachmentUrl}' style='color: #00aff4;'>Attachment</a></div>");
            }
            html.Append("</div>");
        }

        html.Append("</body></html>");

        var fileName = $"transcript-{Context.Channel.Name}-{DateTime.Now:yyyyMMddHHmmss}.html";
        using var ms = new MemoryStream(Encoding.UTF8.GetBytes(html.ToString()));
        
        await FollowupWithFileAsync(ms, fileName, "Here is your generated transcript.");
    }
}