using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Scrappy.Data;
using Microsoft.EntityFrameworkCore;

namespace Scrappy.Modules;

public class ReminderModule : ScrappyModuleBase
{
    public ReminderModule(BotContext db) : base(db) { }

    [SlashCommand("remindme", "Set a reminder for yourself.")]
    public async Task SetReminder(
        [Summary("time", "Time from now (e.g., 10m, 1h, 1d)")] string time,
        [Summary("message", "What should I remind you about?")] string message)
    {
        if (!await IsFeatureEnabledAsync(s => s.RemindersEnabled)) return;

        TimeSpan span;
        if (time.EndsWith("m") && int.TryParse(time.TrimEnd('m'), out int minutes)) span = TimeSpan.FromMinutes(minutes);
        else if (time.EndsWith("h") && int.TryParse(time.TrimEnd('h'), out int hours)) span = TimeSpan.FromHours(hours);
        else if (time.EndsWith("d") && int.TryParse(time.TrimEnd('d'), out int days)) span = TimeSpan.FromDays(days);
        else
        {
            await RespondAsync("Invalid time format. Use 10m, 1h, 1d etc.", ephemeral: true);
            return;
        }

        if (span.TotalMinutes < 1)
        {
            await RespondAsync("Reminder must be at least 1 minute in the future.", ephemeral: true);
            return;
        }

        var reminder = new Reminder
        {
            UserId = Context.User.Id,
            ChannelId = Context.Channel.Id,
            GuildId = Context.Guild.Id,
            Message = message,
            TargetTime = DateTime.UtcNow.Add(span)
        };

        _db.Reminders.Add(reminder);
        await _db.SaveChangesAsync();

        await RespondAsync($"Got it! I'll remind you in {time}: **{message}**", ephemeral: true);
    }

    [SlashCommand("reminders", "View your active reminders.")]
    public async Task ListReminders()
    {
        if (!await IsFeatureEnabledAsync(s => s.RemindersEnabled)) return;

        var reminders = await _db.Reminders
            .Where(r => r.UserId == Context.User.Id && !r.IsCompleted)
            .OrderBy(r => r.TargetTime)
            .ToListAsync();

        if (!reminders.Any())
        {
            await RespondAsync("You have no active reminders.", ephemeral: true);
            return;
        }

        var embed = new EmbedBuilder()
            .WithTitle("Your Active Reminders")
            .WithColor(Color.Blue);

        foreach (var r in reminders.Take(10))
        {
            embed.AddField($"<t:{new DateTimeOffset(r.TargetTime).ToUnixTimeSeconds()}:R>", r.Message);
        }

        await RespondAsync(embed: embed.Build(), ephemeral: true);
    }
}
