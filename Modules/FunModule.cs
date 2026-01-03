using Discord;
using Discord.Interactions;
using Scrappy.Data;

namespace Scrappy.Modules;

public class FunModule : ScrappyModuleBase
{
    private readonly Random _random = new();

    public FunModule(BotContext db) : base(db) { }

    [SlashCommand("8ball", "Ask the magic 8-ball a question.")]
    public async Task Magic8Ball(string question)
    {
        string[] responses = {
            "It is certain.", "It is decidedly so.", "Without a doubt.", "Yes definitely.",
            "You may rely on it.", "As I see it, yes.", "Most likely.", "Outlook good.",
            "Yes.", "Signs point to yes.", "Reply hazy, try again.", "Ask again later.",
            "Better not tell you now.", "Cannot predict now.", "Concentrate and ask again.",
            "Don't count on it.", "My reply is no.", "My sources say no.",
            "Outlook not so good.", "Very doubtful."
        };

        var response = responses[_random.Next(responses.Length)];
        
        var embed = new EmbedBuilder()
            .WithTitle("🔮 Magic 8-Ball")
            .AddField("Question", question)
            .AddField("Answer", response)
            .WithColor(response.Contains("no") || response.Contains("not") ? Color.Red : 
                       response.Contains("hazy") || response.Contains("predict") ? Color.Gold : Color.Green)
            .Build();

        await RespondAsync(embed: embed);
    }

    [SlashCommand("coinflip", "Flip a coin.")]
    public async Task CoinFlip()
    {
        var result = _random.Next(2) == 0 ? "Heads" : "Tails";
        
        var embed = new EmbedBuilder()
            .WithTitle("🪙 Coin Flip")
            .WithDescription($"The coin landed on **{result}**!")
            .WithColor(Color.Gold)
            .Build();

        await RespondAsync(embed: embed);
    }

    [SlashCommand("roll", "Roll some dice (e.g., 2d6).")]
    public async Task RollDice(string dice = "1d6")
    {
        var parts = dice.ToLower().Split('d');
        if (parts.Length != 2 || !int.TryParse(parts[0], out int count) || !int.TryParse(parts[1], out int sides))
        {
            await RespondAsync("Invalid format. Use something like `1d6` or `2d20`.", ephemeral: true);
            return;
        }

        if (count < 1 || count > 20 || sides < 2 || sides > 100)
        {
            await RespondAsync("Keep it reasonable! (1-20 dice, 2-100 sides)", ephemeral: true);
            return;
        }

        var results = new List<int>();
        for (int i = 0; i < count; i++) results.Add(_random.Next(1, sides + 1));

        var embed = new EmbedBuilder()
            .WithTitle("🎲 Dice Roll")
            .AddField("Dice", dice, true)
            .AddField("Total", results.Sum().ToString(), true)
            .AddField("Rolls", string.Join(", ", results))
            .WithColor(Color.Blue)
            .Build();

        await RespondAsync(embed: embed);
    }
}
