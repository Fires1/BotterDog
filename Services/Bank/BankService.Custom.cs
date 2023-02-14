using BotterDog.Entities;
using CSharpFunctionalExtensions;
using Discord;
using Discord.WebSocket;
using FiresStuff.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace BotterDog.Services
{
    ///CUSTOM BET RELATED ITEMS
    public partial class BankService
    {
        public readonly static ulong TopperDogGuildId = 537791310212628501; //537791310212628501
        public readonly static ulong TopperDogRoleId = 833147085435305984; //833147085435305984

        private async Task CustomBetCreated(SocketModal arg)
        {
            if (!arg.Data.CustomId.StartsWith("cust")) { return; }
            await arg.DeferAsync();

            var accnt = _accounts.FindOrCreate(arg.User.Id).Value;

            var entries = arg.Data.Components.ToList();
            var title = entries.First(x => x.CustomId == "cust-title");
            var amount = entries.First(x => x.CustomId == "cust-amount");
            var options = entries.First(x => x.CustomId == "cust-options");
            var decider = entries.First(x => x.CustomId == "cust-decider");
            var gameId = new Guid(arg.Data.CustomId.Split(":").Last());

            if (!(Games.FirstOrDefault(x => x.Id == gameId) is CustomState game)) { await arg.Channel.SendMessageAsync($"{arg.User.Mention} Game doesn't exist anymore."); return; }

            //If game is in the play state, and all bets have made it, we silently fail.
            if (game.State != GameState.Betting) { return; }

            //Find our original message for embed updates.
            var guild = _client.GetGuild(game.Guild);
            var channel = guild.GetTextChannel(game.Channel);
            var creator = _client.GetUser(game.Creator);

            game.Title = title.Value.Trim();

            if(int.TryParse(amount.Value.Trim(), out int bet))
            {
                game.Bet = bet; 
            }
            else
            {
                Games.Remove(game);
                await creator.SendMessageAsync("Bet not formatted correctly.");
                return;
            }

            var optionsinput = options.Value.Split(',');
            if(optionsinput.Length <= 1)
            {
                Games.Remove(game);
                await creator.SendMessageAsync("Options input not formatted correctly. Needs more than 1.");
                return;
            }

            game.Options = optionsinput.Select(x => x.Trim()).ToList();


            var cleanDecider = decider.Value.ToLower().Trim();
            if (cleanDecider == "me" || cleanDecider == "myself")
            {
                game.Decider = CustomDecider.Self;
            } else if(cleanDecider == "topper dog" || cleanDecider == "top dog" || cleanDecider == "topper" || cleanDecider == "dog")
            {
                game.Decider = CustomDecider.TopperDog;
            }
            else
            {
                Games.Remove(game);
                await creator.SendMessageAsync("Unrecognized input on who decides.");
                return;
            }

            var deciderString = game.Decider == CustomDecider.Self ? $"{(arg.User as IGuildUser).DisplayName}" : "Topper Dog";
            var embed = new EmbedBuilder()
                .WithTitle(game.Title)
                .WithColor(new Color(_random.Next(50, 255), _random.Next(50, 255), _random.Next(50, 255)))
                .AddField("Bet Amount", $"${game.Bet}", false)
                .WithFooter($"Wager started by {(arg.User as IGuildUser).DisplayName}\r\nWinning hit decided by {deciderString}");

            for (int i = 0; i < game.Options.Count; i++)
            {
                embed.AddField(game.Options[i].Trim(), "0 bets", true);
            }
            embed.AddField("Total Pot", "$0", false);

            ActionRowBuilder ar = new ActionRowBuilder();

            if (game.Options.Count <= 5)
            {
                for (int i = 0; i < game.Options.Count; i++)
                {
                    var b = i % 2 == 0 ? ButtonStyle.Success : ButtonStyle.Danger;
                    ar.WithButton($"Bet on '{game.Options[i]}'", $"cust-{i}-choice:{game.Id}", b);
                }
            }
            else
            {
                var menu = new SelectMenuBuilder()
                    .WithMinValues(1)
                    .WithPlaceholder("Place a bet")
                    .WithMaxValues(1)
                    .WithCustomId($"cust-choices:{game.Id}");
                for (int i = 0; i < game.Options.Count; i++)
                {
                    menu.AddOption($"Bet on '{game.Options[i]}'", $"cust-{i}-choice:{game.Id}");
                }
                ar.WithSelectMenu(menu);
            }

            var msg = await arg.Channel.SendMessageAsync(embed: embed.Build(), components: new ComponentBuilder().AddRow(ar).Build());
            if(game.Decider == CustomDecider.TopperDog)
            {
                await NotifyTopperDog(game);
            }
            game.Message = msg.Id;
        }

        private async Task PlaceCustomBet(SocketMessageComponent arg)
        {
            //Since client.ButtonExecuted captures all button clicks, let's sort by ours.
            if (!arg.Data.CustomId.StartsWith("cust")) { return; }

            var accnt = _accounts.FindOrCreate(arg.User.Id).Value;

            //Find game GUID off id
            var gameId = new Guid(arg.Data.CustomId.Split(":").Last());

            //Find game
            if (!(Games.FirstOrDefault(x => x.Id == gameId) is CustomState game)) { await arg.User.SendMessageAsync($"Game doesn't exist anymore."); return; }


            if(arg.Data.CustomId.StartsWith("cust-cancel"))
            {
                await CancelWager(game, arg);
                return;
            }

            int choice;
            if (arg.Data.Values != null) //Since we're using the same method, check values to get the selection off a menu.
            {
                choice = int.Parse(arg.Data.Values.First().Split("-")[1]);
            }
            else
            {
                choice = int.Parse(arg.Data.CustomId.Split("-")[1]);
            }

           
            if (accnt.Balance <= game.Bet)
            {
                await arg.User.SendMessageAsync("You don't have enough money to place this bet.");
                await arg.DeferAsync();
                return;
            }

            game.Bets.Add(new Bet(arg.User.Id, arg.User.Username, game.Bet, 1, new int[] { choice }));
            accnt.Balance -= game.Bet;
            game.Pot += game.Bet;
            await UpdateCustomEmbed(arg, choice, game);
            _accounts.Save();
        }

        private async Task CustomSelectMenuExecuted(SocketMessageComponent arg)
        {
            if(!arg.Data.CustomId.Contains("cust-decide"))
            { 
                //Forward the more-than-five-choices selection menu items here.
                await PlaceCustomBet(arg);
                return;
            }
            //Realistically this method shouldn't ever be called by someone who isn't the creator or Topperdog-
            //since it's sent ephemerally, so we aren't going to check roles again.

            //Find game GUID off id
            var gameId = new Guid(arg.Data.CustomId.Split(":").Last());

            var choice = int.Parse(arg.Data.Values.First().Split("-")[1]);

            if (!(Games.FirstOrDefault(x => x.Id == gameId) is CustomState game)) { await arg.User.SendMessageAsync($"Game doesn't exist anymore."); return; }
            if (game.State != GameState.Betting) { await arg.User.SendMessageAsync($"Game state error."); return; }

            game.Decided = choice;
            game.State = GameState.PendingPayout;
            Payout(game, arg);
            await arg.UpdateAsync(x =>
            {
                x.Content = "*You have decided.*";
                x.Components = new ComponentBuilder().Build();
                x.Embeds = new Embed[] { new EmbedBuilder()
                            .WithTitle("Decision completed!")
                            .WithColor(50, 50, 50)
                            .WithDescription("Thank you.")
                            .Build() };
            });
        }

        private async Task CancelWager(CustomState game, SocketMessageComponent arg)
        {
            if (game.State != GameState.Betting) { await arg.User.SendMessageAsync($"Game state error."); return; }
            game.Decided = -1;
            game.State = GameState.Cancelled;
            CancelGame(game, arg);
            await arg.UpdateAsync(x =>
            {
                x.Content = "*Game cancelled*";
                x.Components = new ComponentBuilder().Build();
                x.Embeds = new Embed[] { new EmbedBuilder()
                            .WithTitle("Game has been cancelled.")
                            .WithColor(255, 50, 50)
                            .WithDescription("All players will be refunded. Thank you.")
                            .Build() };
            });
        }

        private async Task NotifyTopperDog(CustomState game)
        {
            var doggys = _client.GetGuild(TopperDogGuildId);
            var topperdog = doggys.GetRole(TopperDogRoleId);
            var topdog = doggys.Users.FirstOrDefault(x => x.Roles.Any(x => x.Id == TopperDogRoleId));
            if(topdog != null)
            {
                await topdog.SendMessageAsync($"**New wager available for you to decide as Topper Dog:**\r\n*{game.Title}*\r\n{string.Join("\r\n- ", game.Options)}\r\n**${game.Bet} bet**");
            }
        }

        private async Task UpdateCustomEmbed(SocketMessageComponent arg, int choice, CustomState game)
        {
            var mainEmbed = arg.Message.Embeds.First();

            var betsForChoice = game.Bets.Where(x => x.Hits.Contains(choice)).Count();
            var totalPot = game.Bets.Count * game.Bet;

            await arg.UpdateAsync(x =>
            {
                var emb = mainEmbed.ToEmbedBuilder();
                emb.Fields.Last().Value = $"${totalPot}";
                emb.Fields[choice + 1].Value = $"{betsForChoice} bets";
                x.Embed = emb.Build();
            });
        }
    }
}
