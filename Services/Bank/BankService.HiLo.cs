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

            var player = game.Players.FirstOrDefault(x => x.Id == arg.User.Id);

            //Find our "button choice"
            var choice = arg.Data.CustomId.Split(":").First();

            //Check if person has enough money and is not playing
            //This only applies to betting buttons
            if (accnt.Balance <= game.Bet && (choice == "hilo-lo" || choice == "hilo-hi"))
            {
                await arg.RespondAsync("You don't have enough money to place this bet.", ephemeral: true);
                return;
            }

            if(game.CurrentRound > 1 && player == null)
            {
                await arg.RespondAsync("You can't join a game that's already started.", ephemeral: true); return;
            }

            //If we placed a bet, and we aren't a current player, let's pay the buy-in.
            //We've already checked that we can afford this so we aren't going to check again.
            if (choice == "hilo-lo" || choice == "hilo-hi")
            {
                if (player == null)
                {
                    player = new HiLoPlayer(accnt);
                    game.Players.Add(player);
                    accnt.Balance -= game.Bet;
                    Pot += game.Bet;
                    _accounts.Save();
                    Save();
                }
                if (player.Status == HiLoPlayerStatus.Bust)
                {
                    player.Status = HiLoPlayerStatus.Waiting;
                    player.Multiplier = 1.0m;
                    accnt.Balance -= game.Bet;
                    Pot += game.Bet;
                    _accounts.Save();
                    Save();
                }
            }

            //See if our player has quit.
            if (player.Status == HiLoPlayerStatus.Quit) { await arg.RespondAsync($"You can't join a game you've already quit", ephemeral: true); return; }

            switch (choice)
            {
                case "hilo-lo":
                    //Initialize time-out timer
                    if(!game.TimerStarted) { StartHiLoTimer(game.Id, arg); game.TimerStarted = true; }
                    //Add bet
                    game.Bets.Add(new Bet(arg.User.Id, arg.User.Username, game.Bet, CalculateHiLoOdds(game.CurrentCard.Number, false), new int[] { game.CurrentRound, 0 }));
                    ResetHiLoTimer(game.Id); //Reset timer
                    player.Status = HiLoPlayerStatus.PlacedBet;

                    await UpdateHiLo(arg, game);
                    break;
                case "hilo-hi":
                    //Initialize time-out timer
                    if (!game.TimerStarted) { StartHiLoTimer(game.Id, arg); game.TimerStarted = true;  }
                    //Add bet
                    game.Bets.Add(new Bet(arg.User.Id, arg.User.Username, game.Bet, CalculateHiLoOdds(game.CurrentCard.Number, true), new int[] { game.CurrentRound, 1 }));
                    ResetHiLoTimer(game.Id); //Reset timer
                    player.Status = HiLoPlayerStatus.PlacedBet;

                    await UpdateHiLo(arg, game);
                    break;
                case "hilo-next":
                    #region HILONEXT
                    //Check to make sure everyone has placed bets for this round.
                    if (game.Players.Where(x=>x.Status == HiLoPlayerStatus.Waiting).Count() > 0) { await arg.RespondAsync("Not every player has placed a bet yet", ephemeral: true); return; }
                    //Check to make sure the creator is the person pressing next.
                    if(game.Creator != arg.User.Id) { await arg.RespondAsync("You are not the creator of this game, therefore you cannot choose to move on. If 60 seconds passes and the game does not continue, the game will automatically pay out and end.", ephemeral: true); return;  }
                    //Make sure atleast one bet exists.
                    if (!game.Players.Where(x=>x.Status == HiLoPlayerStatus.PlacedBet).Any()) { await arg.RespondAsync("You cannot continue without someone placing a bet.", ephemeral: true); return; }
                   
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
                        foreach (var player in game.Players.OrderBy(x=>x.Status))
                        {
                            var e = new EmbedFieldBuilder()
                                .WithName($"{game.Bets.First(x => x.Better == player.Id).DisplayName}")
                                .WithIsInline(true);

                            switch(player.Status)
                            {
                                case HiLoPlayerStatus.Waiting:
                                    e.WithValue($"${decimal.Round(game.Bet * player.Multiplier, 2)} ({decimal.Round(player.Multiplier, 2)}x)");
                                    break;
                                case HiLoPlayerStatus.PlacedBet:
                                    e.WithValue($"${decimal.Round(game.Bet * player.Multiplier, 2)} ({decimal.Round(player.Multiplier, 2)}x)");
                                    break;
                                case HiLoPlayerStatus.Bust:
                                    e.WithValue("BUST");
                                    break;
                                case HiLoPlayerStatus.Quit:
                                    e.WithValue("QUIT");
                                    break;
                                case HiLoPlayerStatus.Bust | HiLoPlayerStatus.Quit:
                                    e.WithValue("BUST");
                                    break;
                            }

                            emb.Fields.Add(e);
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

                    if(player == null)
                    {
                        await arg.RespondAsync("You are not a member of this game.", ephemeral: true);
                        return;
                    }

                    //We are quitting.
                    if (await QuitHiLo(game, player))
                    {
                        return;
                    }
                    else
                    {
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
                            foreach (var player in game.Players.OrderByDescending(x => x.Status))
                            {
                                var e = new EmbedFieldBuilder()
                                    .WithName($"{game.Bets.First(x => x.Better == player.Id).DisplayName}")
                                    .WithIsInline(true);

                                switch (player.Status)
                                {
                                    case HiLoPlayerStatus.Waiting:
                                        e.WithValue($"${decimal.Round(game.Bet * player.Multiplier, 2)} ({decimal.Round(player.Multiplier, 2)}x)");
                                        break;
                                    case HiLoPlayerStatus.PlacedBet:
                                        e.WithValue($"${decimal.Round(game.Bet * player.Multiplier, 2)} ({decimal.Round(player.Multiplier, 2)}x)");
                                        break;
                                    case HiLoPlayerStatus.Bust:
                                        e.WithValue("BUST");
                                        break;
                                    case HiLoPlayerStatus.Quit:
                                        e.WithValue("QUIT");
                                        break;
                                    case HiLoPlayerStatus.Bust | HiLoPlayerStatus.Quit:
                                        e.WithValue("BUST");
                                        break;
                                }
                                emb.Fields.Add(e);
                            }

                            x.Embed = emb.Build();
                        });

                        //Notify players.
                        if ((player.Status & HiLoPlayerStatus.Bust) != 0)
                        {
                            await arg.Channel.SendMessageAsync($"{arg.User.Mention} has bust, then quit.");
                            return;
                        }
                        else
                        {
                            await arg.Channel.SendMessageAsync($"{arg.User.Mention} has quit for a payout of **${decimal.Round(player.Payout.Value, 2)}** doggy dawg bucks.");
                            return;
                        }
                    }
                #endregion
                case "hilo-tie":
                    //Maybe some day
                    throw new NotImplementedException();
            }
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
                foreach (var player in game.Players.OrderBy(x => x.Status))
                {
                    var bet = game.Bets.First(x => x.Better == player.Id);
                    if(bet == null)
                    {
                        continue;
                    }
                    var e = new EmbedFieldBuilder()
                        .WithName($"{game.Bets.First(x => x.Better == player.Id).DisplayName}")
                        .WithIsInline(true);

                    switch (player.Status)
                    {
                        case HiLoPlayerStatus.Waiting:
                            e.WithValue($"${decimal.Round(game.Bet * player.Multiplier, 2)} ({decimal.Round(player.Multiplier, 2)}x)");
                            break;
                        case HiLoPlayerStatus.PlacedBet:
                            e.WithValue($"${decimal.Round(game.Bet * player.Multiplier, 2)} ({decimal.Round(player.Multiplier, 2)}x)");
                            break;
                        case HiLoPlayerStatus.Bust:
                            e.WithValue("BUST");
                            break;
                        case HiLoPlayerStatus.Bust | HiLoPlayerStatus.Quit:
                            e.WithValue("BUST");
                            break;
                        case HiLoPlayerStatus.Quit:
                            e.WithValue("QUIT");
                            break;
                    }
                    emb.Fields.Add(e);
                }



                x.Embed = emb.Build();
            });
        }
    }
}
