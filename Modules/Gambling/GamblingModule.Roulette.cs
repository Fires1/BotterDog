using Discord;
using Discord.Interactions;
using System.Threading.Tasks;
using BotterDog.Entities;

namespace BotterDog.Modules
{
    public partial class GamblingModule
    {
        [SlashCommand("roulette", "Play some roulette!")]
        public async Task Roulette(decimal Bet)
        {
            var accnt = _accounts.FindOrCreate(Context.User.Id);

            if (Bet > accnt.Value.Balance)
            {
                await RespondAsync("You do not have enough money to make the starter bet, therefore you are too broke to start this game.", ephemeral: true);
            }

            var game = new RouletteState(Context.User.Id, GameType.Roulette, Bet);

            var ar = new ActionRowBuilder()
                .WithButton("Single number 36x", $"roul-single:{game.Id}", ButtonStyle.Success, emote: Emoji.Parse(":one:"))
                .WithButton("Split 18x", $"roul-split:{game.Id}", ButtonStyle.Success, emote: Emoji.Parse(":two:"))
                .WithButton("Corner 9x", $"roul-corner:{game.Id}", ButtonStyle.Success, emote: Emoji.Parse(":1234:"))
                .WithButton("Dozen 3x", $"roul-dozen:{game.Id}", ButtonStyle.Success, emote: Emoji.Parse(":doughnut:"))
                .WithButton("Half 2x", $"roul-halves:{game.Id}", ButtonStyle.Success, emote: Emoji.Parse(":heavy_division_sign:"));

            var ar2 = new ActionRowBuilder()
                .WithButton("Red 2x", $"roul-red:{game.Id}", ButtonStyle.Danger, emote: Emoji.Parse(":red_square:"))
                .WithButton("Black 2x", $"roul-black:{game.Id}", ButtonStyle.Secondary, emote: Emoji.Parse(":black_large_square:"))
                .WithButton("Odds 2x", $"roul-odds:{game.Id}", emote: Emoji.Parse(":exclamation:"))
                .WithButton("Evens 2x", $"roul-evens:{game.Id}", emote: Emoji.Parse(":bangbang:"));

            var ar3 = new ActionRowBuilder()
                .WithButton("SPIN THE WHEEL", $"roul-spin:{game.Id}", ButtonStyle.Danger, emote: Emoji.Parse(":wheelchair:"));


            var emb = new EmbedBuilder()
                .WithTitle($"{Context.User.Username}'s Roulette")
                .WithDescription("**Place your bets!**")
                .WithColor(255, 20, 20)
                .WithImageUrl("https://i.imgur.com/zXZ2H7h.png")
                .AddField("Bet amount", $"${Bet}", true);

            var builder = new ComponentBuilder().WithRows(new[] { ar, ar2, ar3 });

            await RespondAsync("", embed: emb.Build(), components: builder.Build());

            //Get message really quick and update our Ids
            var response = await GetOriginalResponseAsync();
            game.Guild = Context.Guild.Id;
            game.Channel = Context.Channel.Id;
            game.Message = response.Id;
            _bank.Games.Add(game);
        }
    }
}
