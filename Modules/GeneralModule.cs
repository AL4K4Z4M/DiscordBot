using Discord;
using Discord.Interactions;
using Microsoft.EntityFrameworkCore;
using Scrappy.Data;

namespace Scrappy.Modules;

public class GeneralModule : ScrappyModuleBase
{
    public GeneralModule(BotContext db) : base(db) { }

    [SlashCommand("helloscrappy", "Scrappy says hello to you!")]
    public async Task HelloScrappy()
    {
        if (!await IsFeatureEnabledAsync(s => s.SayHelloEnabled)) return;

        await RespondAsync($"Hello {Context.User.Mention}!");
    }
}