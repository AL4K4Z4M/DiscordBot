using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Scrappy.Data;
using Microsoft.EntityFrameworkCore;

namespace Scrappy.Services;

public class ReminderService : BackgroundService
{
    private readonly DiscordSocketClient _client;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ReminderService> _logger;

    public ReminderService(DiscordSocketClient client, IServiceScopeFactory scopeFactory, ILogger<ReminderService> logger)
    {
        _client = client;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using (var scope = _scopeFactory.CreateScope())
                {
                    var db = scope.ServiceProvider.GetRequiredService<BotContext>();
                    var now = DateTime.UtcNow;

                    var dueReminders = await db.Reminders
                        .Where(r => !r.IsCompleted && r.TargetTime <= now)
                        .ToListAsync();

                    foreach (var reminder in dueReminders)
                    {
                        await SendReminder(reminder);
                        reminder.IsCompleted = true;
                    }

                    if (dueReminders.Any())
                    {
                        await db.SaveChangesAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing reminders");
            }

            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
    }

    private async Task SendReminder(Reminder reminder)
    {
        try
        {
            var channel = await _client.GetChannelAsync(reminder.ChannelId) as IMessageChannel;
            if (channel != null)
            {
                await channel.SendMessageAsync($"🔔 <@{reminder.UserId}>, here is your reminder: **{reminder.Message}**");
            }
            else
            {
                // Fallback to DM if channel is gone
                var user = await _client.GetUserAsync(reminder.UserId);
                if (user != null)
                {
                    await user.SendMessageAsync($"🔔 Here is your reminder: **{reminder.Message}** (Sent via DM because original channel is unavailable)");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"Failed to send reminder {reminder.Id}: {ex.Message}");
        }
    }
}
