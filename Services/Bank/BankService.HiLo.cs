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
    ///HI LO RELATED ITEMS
    public partial class BankService
    {
        //Main handler
        private async Task PlaceHiLoBet(SocketMessageComponent arg)
        {
            //Since client.ButtonExecuted captures all button clicks, let's sort by ours.
            if (!arg.Data.CustomId.StartsWith("hilo")) { return; }

            var accnt = _accounts.FindOrCreate(arg.User.Id).Value;

            //Find game GUID off id
            var gameId = new Guid(arg.Data.CustomId.Split(":").Last());

            //Find game
            if (!(Games.FirstOrDefault(x => x.Id == gameId) is HiLoState game)) { await arg.User.SendMessageAsync($"Game doesn't exist anymore."); await arg.DeferAsync(); return; }

            //Find our "button choice"
            var choice = arg.Data.CustomId.Split(":").First();

            //Check if person has enough money and is not playing
            //This only applies to betting buttons
            if (accnt.Balance <= game.Bet && !game.CurrentPlayers.Contains(arg.User.Id) && (choice != "hilo-quit" || choice != "hilo-next"))
            {
                await arg.RespondAsync("You don't have enough money to place this bet.", ephemeral: true);
                return;
            }

            //Find existing bet if it exists and cancel out if it does.
            var bet = game.Bets.FirstOrDefault(x => x.Hits[0] == game.CurrentRound && x.Better == arg.User.Id);
            if(bet != null
                && choice != "hilo-next") { await arg.RespondAsync($"You've already placed a bet for this round.", ephemeral:true);  return; }

            //See if our player has quit.
            if(game.QuitPlayers.Contains(arg.User.Id) && choice != "hilo-quit") {await arg.RespondAsync($"You can't join a game you've already quit", ephemeral: true);  return;}

            //See if our player has busted, if they haven't, then they can't join if the game has already gone on for a round.
            if (!game.BustedPlayers.Contains(arg.User.Id))
            {
                if (game.CurrentRound > 1
                    && !game.CurrentPlayers.Contains(arg.User.Id)
                    && choice != "hilo-quit") { await arg.RespondAsync("You can't join a game that's already started.", ephemeral: true); return; }
            }

            switch (choice)
            {
                case "hilo-lo":
                    //Initialize time-out timer
                    if(!game.TimerStarted) { StartHiLoTimer(game.Id, arg); game.TimerStarted = true; }
                    //Add bet
                    game.Bets.Add(new Bet(arg.User.Id, arg.User.Username, game.Bet, CalculateHiLoOdds(game.CurrentCard.Number, false), new int[] { game.CurrentRound, 0 }));
                    ResetHiLoTimer(game.Id); //Reset timer
                    break;
                case "hilo-hi":
                    //Initialize time-out timer
                    if (!game.TimerStarted) { StartHiLoTimer(game.Id, arg); game.TimerStarted = true;  }
                    //Add bet
                    game.Bets.Add(new Bet(arg.User.Id, arg.User.Username, game.Bet, CalculateHiLoOdds(game.CurrentCard.Number, true), new int[] { game.CurrentRound, 1 }));
                    ResetHiLoTimer(game.Id); //Reset timer
                    break;
                case "hilo-next":
                    #region HILONEXT
                    //Check to make sure everyone has placed bets for this round.
                    if (game.Bets.Where(x=>x.Hits[0] == game.CurrentRound).Count() != game.CurrentPlayers.Count) { await arg.RespondAsync("Not every player has placed a bet yet", ephemeral: true); return; }
                    //Check to make sure the creator is the person pressing next.
                    if(game.Creator != arg.User.Id) { await arg.RespondAsync("You are not the creator of this game, therefore you cannot choose to move on. If 60 seconds passes and the game does not continue, the game will automatically pay out and end.", ephemeral: true); }
                    //Make sure atleast one bet exists.
                    if (!game.Bets.Any()) { await arg.RespondAsync("You cannot continue without someone placing a bet.", ephemeral: true); return; }
                   
                    //Cycle next round.
                    game.NextRound();

                    //If we have to 'skip' an obvious bet, let's make note of that.
                    var skips = new List<Card>();
                    while(game.CurrentCard.Number == CardNumber.Ace || game.CurrentCard.Number == CardNumber.King)
                    {
                        skips.Add(game.CurrentCard);
                        game.SkipRound();
                    }
                    var skipped = "";
                    if(skips.Any())
                    {
                        skipped += $"Skipped: {string.Join(", ", skips)}";
                    }

                    //Find our embed to update
                    var mainEmbed = arg.Message.Embeds.First();

                    //Prepare options, this time with a quit option.
                    var ar = new ActionRowBuilder()
                    .WithButton($"Lower {CalculateHiLoOdds(game.CurrentCard.Number, false)}x", $"hilo-lo:{game.Id}", ButtonStyle.Success, emote: Emoji.Parse(":arrow_down:"))
                    .WithButton($"Higher {CalculateHiLoOdds(game.CurrentCard.Number, true)}x", $"hilo-hi:{game.Id}", ButtonStyle.Danger, emote: Emoji.Parse(":arrow_up:"))
                    .WithButton("Next Card", $"hilo-next:{game.Id}", ButtonStyle.Secondary, emote: Emoji.Parse(":arrow_forward:"))
                    .WithButton("Quit", $"hilo-quit:{game.Id}", ButtonStyle.Primary, emote: Emoji.Parse(":x:"));

                    //Update our game board
                    await arg.UpdateAsync(x =>
                    {
                        //Change image to our new card.
                        x.Attachments = new FileAttachment[] { new FileAttachment("cards/"+game.CurrentCard.ToFileName()) };

                        //Break open the embed
                        var emb = mainEmbed.ToEmbedBuilder();
                        //Update to new card name as well as display any skips.
                        emb.Description = $"{game.CurrentCard}\r\n{skipped}";
                        //Clear our betting fields
                        emb.Fields.FirstOrDefault(x => x.Name == "Low").Value = $"No bets";
                        emb.Fields.FirstOrDefault(x => x.Name == "High").Value = $"No bets";
                        
                        //Remove any existing player fields so we can re-add them
                        var playerFields = emb.Fields.TakeLast(emb.Fields.Count - 3);
                        if (playerFields.Any())
                        {
                            emb.Fields.RemoveAll(x => playerFields.Contains(x));
                        }
                        //Still playing
                        foreach (var player in game.CurrentPlayers)
                        {
                            emb.Fields.Add(new EmbedFieldBuilder()
                                .WithName($"{game.Bets.First(x => x.Better == player).DisplayName}")
                                .WithValue($"${decimal.Round(game.Bet * game.CurrentMultipliers[player], 2)} ({decimal.Round(game.CurrentMultipliers[player],2)}x)")
                                .WithIsInline(true)
                                );
                        }
                        //Bust
                        foreach (var player in game.BustedPlayers)
                        {
                            emb.Fields.Add(new EmbedFieldBuilder()
                                .WithName($"{game.Bets.First(x => x.Better == player).DisplayName}")
                                .WithValue($"BUST")
                                .WithIsInline(true)
                                );
                        }
                        //Qiot
                        foreach (var player in game.QuitPlayers)
                        {
                            emb.Fields.Add(new EmbedFieldBuilder()
                                .WithName($"{game.Bets.First(x => x.Better == player).DisplayName}")
                                .WithValue($"QUIT")
                                .WithIsInline(true)
                                );
                        }
                        x.Embed = emb.Build();

                        //Add our buttons
                        x.Components = new ComponentBuilder().AddRow(ar).Build();
                    });
                    //Reset our time-out timer.
                    ResetHiLoTimer(game.Id);
                   break;
                #endregion
                case "hilo-quit":
                    #region HILOQUIT
                    //We are quitting.
                    await QuitHiLo(game, arg.User.Id);
                    //If everyone has quit, end this process.
                    if (game.State == GameState.Finished)
                    {
                        return;
                    }
                    //Update our embed shit same as above.
                    mainEmbed = arg.Message.Embeds.First();
                    await arg.UpdateAsync(x =>
                    {
                        var emb = mainEmbed.ToEmbedBuilder();
                        var playerFields = emb.Fields.TakeLast(emb.Fields.Count - 3);
                        if (playerFields.Any())
                        {
                            emb.Fields.RemoveAll(x => playerFields.Contains(x));
                        }
                        foreach (var player in game.CurrentPlayers)
                        {
                            emb.Fields.Add(new EmbedFieldBuilder()
                                .WithName($"{game.Bets.First(x => x.Better == player).DisplayName}")
                                .WithValue($"${decimal.Round(game.Bet * game.CurrentMultipliers[player], 2)} ({decimal.Round(game.CurrentMultipliers[player], 2)}x)")
                                .WithIsInline(true)
                                );
                        }
                        foreach (var player in game.BustedPlayers)
                        {
                            emb.Fields.Add(new EmbedFieldBuilder()
                                .WithName($"{game.Bets.First(x => x.Better == player).DisplayName}")
                                .WithValue($"BUST")
                                .WithIsInline(true)
                                );
                        }
                        foreach (var player in game.QuitPlayers.Where(x => !game.BustedPlayers.Contains(x)).ToList())
                        {
                            emb.Fields.Add(new EmbedFieldBuilder()
                                .WithName($"{game.Bets.First(x => x.Better == player).DisplayName}")
                                .WithValue($"QUIT")
                                .WithIsInline(true)
                                );
                        }
                        x.Embed = emb.Build();
                    });

                    //Notify players.
                    if (game.BustedPlayers.Contains(arg.User.Id))
                    {
                        await arg.Channel.SendMessageAsync($"{arg.User.Mention} has bust, then quit.");
                        return;
                    }
                    else
                    {
                        await arg.Channel.SendMessageAsync($"{arg.User.Mention} has quit for a payout of **${decimal.Round(game.PaidOut[arg.User.Id], 2)}** doggy dawg bucks.");
                        return;
                    }
                #endregion
                case "hilo-tie":
                    //Maybe some day
                    throw new NotImplementedException();
            }

            //If we placed a bet, and we aren't a current player, let's pay the buy-in.
            //We've already checked that we can afford this so we aren't going to check again.
            if(choice == "hilo-lo" || choice == "hilo-hi" && !game.CurrentPlayers.Contains(arg.User.Id))
            {
                //If we are busted, the player is buying back in so let's remove them off that list.
                if(game.BustedPlayers.Contains(arg.User.Id))
                {
                    game.BustedPlayers.Remove(arg.User.Id);
                }
                accnt.Balance -= game.Bet;
                game.Pot += game.Bet;

                game.CurrentPlayers.Add(arg.User.Id);
                game.CurrentMultipliers.Add(arg.User.Id, 1.0M);
            }

            if (choice != "hilo-next" || choice != "hilo-quit")
            {
                await UpdateHiLo(arg, game);
            }
            _accounts.Save();
        }

        //Hard coded odds are swag.
        public static decimal CalculateHiLoOdds(CardNumber number, bool high)
        {
            if(high)
            {
                switch(number)
                {
                    case CardNumber.Ace:
                        return 1.0M;
                    case CardNumber.Two:
                        return 1.1M;
                    case CardNumber.Three:
                        return 1.2M;
                    case CardNumber.Four:
                        return 1.4M;
                    case CardNumber.Five:
                        return 1.4M;
                    case CardNumber.Six:
                        return 1.5M;
                    case CardNumber.Seven:
                        return 1.8M;
                    case CardNumber.Eight:
                        return 2.0M;
                    case CardNumber.Nine:
                        return 3.0M;
                    case CardNumber.Ten:
                        return 4.0M;
                    case CardNumber.Jack:
                        return 5.0M;
                    case CardNumber.Queen:
                        return 12.0M;
                    case CardNumber.King:
                        return 0.0M;
                }
            }
            else
            {
                switch (number)
                {
                    case CardNumber.Ace:
                        return 0.0M;
                    case CardNumber.Two:
                        return 12.0M;
                    case CardNumber.Three:
                        return 5.0M;
                    case CardNumber.Four:
                        return 3.0M;
                    case CardNumber.Five:
                        return 3.0M;
                    case CardNumber.Six:
                        return 2.0M;
                    case CardNumber.Seven:
                        return 1.8M;
                    case CardNumber.Eight:
                        return 1.5M;
                    case CardNumber.Nine:
                        return 1.4M;
                    case CardNumber.Ten:
                        return 1.3M;
                    case CardNumber.Jack:
                        return 1.2M;
                    case CardNumber.Queen:
                        return 1.1M;
                    case CardNumber.King:
                        return 1.0M;
                }
            }
            return 0.0M;
        }

        private async Task UpdateHiLo(SocketMessageComponent arg, HiLoState game)
        {
            var mainEmbed = arg.Message.Embeds.First();

            var betsForLo = game.Bets.Where(x => x.Hits[0] == game.CurrentRound && x.Hits[1] == 0);
            var betsForHi = game.Bets.Where(x => x.Hits[0] == game.CurrentRound && x.Hits[1] == 1);
            var totalPot = game.Bets.Count * game.Bet;

            await arg.UpdateAsync(x =>
            {
                var emb = mainEmbed.ToEmbedBuilder();
                if (betsForLo.Any())
                {
                    emb.Fields.FirstOrDefault(x => x.Name == "Low").Value = $"{string.Join(",", betsForLo.Select(x=>x.DisplayName).ToList())}";
                }
                if (betsForHi.Any())
                {
                    emb.Fields.FirstOrDefault(x => x.Name == "High").Value = $"{string.Join(",", betsForHi.Select(x => x.DisplayName).ToList())}";
                }
                var playerFields = emb.Fields.TakeLast(emb.Fields.Count - 3);
                if (playerFields.Any())
                {
                    emb.Fields.RemoveAll(x => playerFields.Contains(x));
                }
                foreach (var player in game.CurrentPlayers)
                {
                    emb.Fields.Add(new EmbedFieldBuilder()
                        .WithName($"{game.Bets.First(x => x.Better == player).DisplayName}")
                        .WithValue($"${decimal.Round(game.Bet * game.CurrentMultipliers[player], 2)} ({decimal.Round(game.CurrentMultipliers[player], 2)}x)")
                        .WithIsInline(true)
                        );
                }
                foreach (var player in game.BustedPlayers)
                {
                    emb.Fields.Add(new EmbedFieldBuilder()
                        .WithName($"{game.Bets.First(x => x.Better == player).DisplayName}")
                        .WithValue($"BUST")
                        .WithIsInline(true)
                        );
                }
                foreach (var player in game.QuitPlayers)
                {
                    emb.Fields.Add(new EmbedFieldBuilder()
                        .WithName($"{game.Bets.First(x => x.Better == player).DisplayName}")
                        .WithValue($"QUIT")
                        .WithIsInline(true)
                        );
                }


                x.Embed = emb.Build();
            });
        }
    }
}
