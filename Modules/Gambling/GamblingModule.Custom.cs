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
        [SlashCommand("custom", "Start a custom bet, winner takes the whole pot!")]
        public async Task Custom()
        {
            var accnt = _accounts.FindOrCreate(Context.User.Id);

            var game = new CustomState(Context.User.Id, GameType.Custom, 0);

            var modal = new ModalBuilder()
                .WithTitle("Custom Bet Maker")
                .WithCustomId($"cust-menu:{game.Id}")
                .AddTextInput("Title", "cust-title", TextInputStyle.Short, placeholder: "ex: Will Alex will eat butter tonight?", minLength: 6, required: true)
                .AddTextInput("Bet amount", "cust-amount", TextInputStyle.Short, placeholder: "Enter a number.", minLength: 1, required: true)
                .AddTextInput("Enter options, comma-separated", "cust-options", TextInputStyle.Paragraph, placeholder: "Ex: `Alex won't, Alex will`", minLength: 3, required: true)
                .AddTextInput("Who decides what wins? You or the Topper Dog?", "cust-decider", TextInputStyle.Short, placeholder: "Ex: `Me`, `Topper dog`/`Dog`/`Top`", minLength: 2, required: true);

            await RespondWithModalAsync(modal.Build());

            game.Guild = Context.Guild.Id;
            game.Channel = Context.Channel.Id;
            _bank.Games.Add(game);
        }

        [SlashCommand("decidecustom", "Decide out a custom bet")]
        public async Task Decide()
        {
            var accnt = _accounts.FindOrCreate(Context.User.Id);

            var isTopDog = (Context.User as IGuildUser).RoleIds.Contains(BankService.TopperDogRoleId);

            var game = _bank.Games.FirstOrDefault(
                x => x.GameType == GameType.Custom
                && x is CustomState
                && (x.Creator == Context.User.Id && (x as CustomState).Decider == CustomDecider.Self
                || (isTopDog && (x as CustomState).Decider == CustomDecider.TopperDog)))
                as CustomState;

            if (game == null)
            {
                await RespondAsync("No wagers available for you to decide.", ephemeral: true);
                return;
            }

            var decidingAs = "You are deciding as the creator of this wager.";
            if (game.Decider == CustomDecider.TopperDog)
            {
                decidingAs = "You are deciding as the Topper Dawg. Choose carefully.";
            }

            var embed = new EmbedBuilder()
                .WithTitle($"Deciding: {game.Title}");

            var menu = new SelectMenuBuilder()
                .WithMinValues(1)
                .WithMaxValues(1)
                .WithCustomId($"cust-decide:{game.Id}");


            for (int i = 0; i < game.Options.Count; i++)
            {
                var c = game.Options[i];
                var totalBets = game.Bets.Where(x => x.Hits.Contains(i)).Count();
                embed.AddField($"\"{c}\"", $"{totalBets} Bets", true);
                menu.AddOption($"\"{c}\"", $"cust-{i}-decide:{game.Id}");
            }

            embed.AddField("Total $", $"{game.Bet * game.Bets.Count}");

            var ar = new ActionRowBuilder();

            ar.WithSelectMenu(menu);

            var ar2 = new ActionRowBuilder();

            ar2.WithButton("Cancel wager", customId: $"cust-cancel:{game.Id}", ButtonStyle.Danger, emote: Emoji.Parse(":heavy_multiplication_x:"));

            await RespondAsync($"**{decidingAs}**", embed: embed.Build(), components: new ComponentBuilder().AddRow(ar).AddRow(ar2).Build(), ephemeral: true);
        }
    }
}
