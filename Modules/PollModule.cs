using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Scrappy.Data;

namespace Scrappy.Modules;

public class PollModule : ScrappyModuleBase
{
    private static readonly string[] NumEmojis = { "1️⃣", "2️⃣", "3️⃣", "4️⃣", "5️⃣", "6️⃣", "7️⃣", "8️⃣", "9️⃣", "🔟" };

    public PollModule(BotContext db) : base(db) { } 

    [SlashCommand("poll", "Create a reaction-based poll.")]
    public async Task CreatePoll(
        [Summary("question", "The question to ask")] string question,
        [Summary("option1", "First option")] string opt1,
        [Summary("option2", "Second option")] string opt2,
        [Summary("option3", "Third option")] string? opt3 = null,
        [Summary("option4", "Fourth option")] string? opt4 = null,
        [Summary("option5", "Fifth option")] string? opt5 = null)
    {
        if (!await CheckPermissionsAsync("Utility")) return; // Polls are in the Utility group
        if (!await IsFeatureEnabledAsync(s => s.PollsEnabled)) return;

        var options = new List<string> { opt1, opt2 };
        if (!string.IsNullOrEmpty(opt3)) options.Add(opt3);
        if (!string.IsNullOrEmpty(opt4)) options.Add(opt4);
        if (!string.IsNullOrEmpty(opt5)) options.Add(opt5);

        var description = "";
        for (int i = 0; i < options.Count; i++)
        {
            description += $"{NumEmojis[i]} {options[i]}\n\n";
        }

        var embed = new EmbedBuilder()
            .WithTitle($"📊 {question}")
            .WithDescription(description)
            .WithColor(Color.Blue)
            .WithAuthor(Context.User.Username, Context.User.GetAvatarUrl())
            .WithCurrentTimestamp()
            .Build();

        await RespondAsync(embed: embed);
        var msg = await GetOriginalResponseAsync();

        for (int i = 0; i < options.Count; i++)
        {
            await msg.AddReactionAsync(new Emoji(NumEmojis[i]));
        }
    }
}