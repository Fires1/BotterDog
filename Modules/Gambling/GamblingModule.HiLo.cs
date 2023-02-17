using Discord;
using Discord.Interactions;
using System.Threading.Tasks;
using BotterDog.Entities;
using System.Linq;
using BotterDog.Services;

namespace BotterDog.Modules
{
    public partial class GamblingModule
    {
        [SlashCommand("highlow", "Play a game of High-Low")]
        public async Task HiLo(decimal Bet)
        {
            var accnt = _accounts.FindOrCreate(Context.User.Id);

            if(accnt.Value.Balance <= Bet)
            {
                await RespondAsync("You are too broke to start this game.", ephemeral: true);
                return;
            }

            var game = new HiLoState(Context.User.Id, GameType.HiLo, Bet);
            game.Players.Add(new HiLoPlayer(accnt.Value));

            var ar = new ActionRowBuilder()
                .WithButton($"Lower {BankService.CalculateHiLoOdds(game.CurrentCard.Number, false)}x", $"hilo-lo:{game.Id}", ButtonStyle.Success, emote: Emoji.Parse(":arrow_down:"))
                .WithButton($"Higher {BankService.CalculateHiLoOdds(game.CurrentCard.Number, true)}x", $"hilo-hi:{game.Id}", ButtonStyle.Danger, emote: Emoji.Parse(":arrow_up:"))
            .WithButton($"Next Card", $"hilo-next:{game.Id}", ButtonStyle.Secondary, emote: Emoji.Parse(":arrow_forward:"));

            var emb = new EmbedBuilder()
                .WithTitle($"{Context.User.Username}'s High-Low")
                .WithDescription($"{game.CurrentCard}")
                .WithColor(0, 255, 0)
                .AddField("Buy-in:", $"${Bet}", true)
                .AddField("Low", "No bets", true)
                .AddField("High", "No bets", true);

            await RespondWithFileAsync($"cards/{game.CurrentCard.ToFileName()}", embed: emb.Build(), components: new ComponentBuilder().AddRow(ar).Build());

            var response = await GetOriginalResponseAsync();
            game.CreatorsDisplayName = Context.User.Username;
            game.Guild = Context.Guild.Id;
            game.Channel = Context.Channel.Id;
            game.Message = response.Id;
            _bank.Games.Add(game);
        }
    }
}
