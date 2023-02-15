using BotterDog.Entities;
using CSharpFunctionalExtensions;
using Discord;
using Discord.WebSocket;
using FiresStuff.Services;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Timers;

namespace BotterDog.Services
{
    public partial class BankService
    {
        /// <summary>
        /// Active games
        /// </summary>
        public List<IGamblingState> Games { get; set; }

        /// <summary>
        /// Timers, used for delayed payout
        /// </summary>
        public List<GameTimer> Timers { get; set; }

        /// <summary>
        /// Historical log of played games
        /// </summary>
        public List<IGamblingState> FinishedGames { get; set; }

        public decimal Pot { get; set; }

        private readonly DiscordSocketClient _client;
        private readonly AccountService _accounts;
        private readonly BotLogService _botLog;
        private readonly Random _random;


        public BankService(DiscordSocketClient client, AccountService accounts, BotLogService botlog)
        {
            _client = client;
            _accounts = accounts;
            _botLog = botlog;

            //Link Roulette events
            _client.ModalSubmitted += RouletteModalSubmitted;
            _client.ButtonExecuted += RouletteButtonExecuted;
            //Link Custom Bet events
            _client.ModalSubmitted += CustomBetCreated;
            _client.ButtonExecuted += PlaceCustomBet;
            _client.SelectMenuExecuted += CustomSelectMenuExecuted;
            //Link Hi Lo events
            _client.ButtonExecuted += PlaceHiLoBet;

            _random = new Random();

            Games = new List<IGamblingState>();
            FinishedGames = new List<IGamblingState>();           
            Timers = new List<GameTimer>();
        }

        public Result Load()
        {
            try
            {
                Pot = JsonConvert.DeserializeObject<decimal>(File.ReadAllText("pot.json"));
                _botLog.BotLogAsync(BotLogSeverity.Good, "Pot loaded", "Bank loaded successfully.");
                return Result.Success();
            }
            catch (Exception e)
            {
                _botLog.BotLogAsync(BotLogSeverity.Bad, "Pot Load failure", "Failure while loading pot occured:", true, e.Message);
                return Result.Failure(e.Message);
            }
        }

        public Result Save(bool silent = true)
        {
            try
            {
                File.WriteAllText("pot.json", JsonConvert.SerializeObject(Pot));
                if (!silent)
                {
                    _botLog.BotLogAsync(BotLogSeverity.Good, "Pot saved", "Pot saved succesfuly.");
                }
                return Result.Success();
            }
            catch (Exception e)
            {
                _botLog.BotLogAsync(BotLogSeverity.Bad, "Pot Save failure", "Failure while saving Pot occured:", true, e.Message);
                return Result.Failure(e.Message);
            }
        }

        public async void Payout(IGamblingState game, SocketMessageComponent msg)
        {
            switch (game.GameType)
            {
                case GameType.Roulette:
                    var g = game as RouletteState;
                    var result = _fullBoard[_random.Next(0, _fullBoard.Length)];
                    await msg.DeleteOriginalResponseAsync();

                    var winningBets = new List<Bet>();

                    foreach (var bet in g.Bets)
                    {
                        if (bet.Hits.Contains(result))
                        {
                            winningBets.Add(bet);
                        }
                    }


                    var textcolor = "Red";
                    var embedColor = new Color(255, 0, 0);
                    if (_blacks.Contains(result))
                    {
                        textcolor = "Black";
                        embedColor = new Color(0, 0, 0);
                    }

                    var formattedResult = result.ToString();

                    if (result == 37)
                    {
                        formattedResult = "0";
                        embedColor = new Color(0, 255, 0);
                    }
                    if (result == 38)
                    {
                        formattedResult = "00";
                        embedColor = new Color(0, 255, 0);
                    }

                    var desc = "No one won :(";

                    decimal totalWon = 0;

                    if (winningBets.Count > 0)
                    {
                        desc = "Winners:";
                        foreach (var bet in winningBets)
                        {
                            desc += $"\r\n{bet.DisplayName} won ${bet.Amount * bet.Odds}({bet.Odds}x ${bet.Amount})";
                            var account = _accounts.FindOrCreate(bet.Better).Value;
                            totalWon += (bet.Amount * bet.Odds);
                            account.ModifyBalance(bet.Amount * bet.Odds);
                        }
                    }

                    Pot += g.Pot;
                    Pot -= totalWon;

                    await msg.Channel.SendMessageAsync("", embed: new EmbedBuilder()
                        .WithTitle($"{textcolor} {formattedResult}")
                        .WithDescription(desc)
                        .WithColor(embedColor)
                        .Build());
                    await _botLog.BotLogAsync(BotLogSeverity.Meh, "Roulette game payed out", $"Payout completed for game {game.Id}:\r\n{g.Bets.Count} bets totalling ${g.Pot}\r\n{desc}");
                    _accounts.Save();
                    Save();
                    break;
                case GameType.Custom:
                    var finished = game as CustomState;

                    var guild = _client.GetGuild(finished.Guild);
                    var channel = guild.GetTextChannel(finished.Channel);
                    var message = await channel.GetMessageAsync(finished.Message);
                    await message.DeleteAsync();

                     winningBets = new List<Bet>();

                    foreach (var bet in finished.Bets)
                    {
                        if (bet.Hits.Contains(finished.Decided))
                        {
                            winningBets.Add(bet);
                        }
                    }

                    var houseFee = decimal.Round(finished.Pot * 0.05m, 2);
                    var endingPot = finished.Pot - houseFee;
                    Pot += houseFee;

                    var winners = "No one won :(";

                    if (winningBets.Count > 0)
                    {
                        winners = "**Winners:**";
                        foreach (var bet in winningBets)
                        {
                            winners += $"\r\n{bet.DisplayName} won ${decimal.Round(endingPot / winningBets.Count, 2)}";
                            var account = _accounts.FindOrCreate(bet.Better).Value;
                            account.ModifyBalance(decimal.Round(endingPot / winningBets.Count, 2));
                        }
                    }

                    await msg.Channel.SendMessageAsync("", embed: new EmbedBuilder()
                        .WithTitle($"{finished.Options[finished.Decided]} wins!")
                        .WithDescription($"It has been decided that **{finished.Options[finished.Decided]}** wins.\r\nFinal Pot: **${endingPot}**\r\n{winners}")
                        .WithColor(0, 255, 0)
                        .WithFooter("To facilitate wagers, the house takes a 5% fee off the ending pot.")
                        .Build());
                    await _botLog.BotLogAsync(BotLogSeverity.Meh, "Custom wager payed out", $"Payout completed for game {game.Id}:\r\n{finished.Bets.Count} bets totalling ${finished.Pot}\r\n{string.Join("\r\n", finished.Options)}");
                    _accounts.Save();
                    Save();

                    break;
            }

            game.State = GameState.Finished;
            FinishedGames.Add(game);
            Games.Remove(game);
        }

        //Process a player quitting
        public async Task QuitHiLo(HiLoState game, ulong user)
        {
            game.QuitPlayers.Add(user);
            game.CurrentPlayers.Remove(user);

            if (!game.BustedPlayers.Contains(user))
            {
                //If they aren't busted, then let's pay them out.
                var account = _accounts.FindOrCreate(user).Value;
                var totalWon = decimal.Round(game.Bet * game.CurrentMultipliers[user], 2);
                account.ModifyBalance(totalWon);
                game.PaidOut.Add(user, totalWon); //Log payout amount
                game.CurrentMultipliers.Remove(user);
            }

            //If no one is left, end the game.
            if(!game.CurrentPlayers.Any())
            {
                await EndHiLoAsync(game);
            }
        }

        //Handle ending the game
        public async Task EndHiLoAsync(HiLoState game)
        {
            game.State = GameState.Finished; //Flag as finished

            //Find time-out timer and clean up.
            var timer = Timers.FirstOrDefault(x => x.GameId == game.Id);
            if (timer != null)
            {
                timer.Stop();
                Timers.Remove(timer);
            }

            string desc = "";

            foreach (var player in game.CurrentPlayers)
            {
                //Pay out any current players.
                var account = _accounts.FindOrCreate(player).Value;
                var totalWon = decimal.Round(game.Bet * game.CurrentMultipliers[player], 2);
                account.ModifyBalance(totalWon);
                game.PaidOut.Add(player, totalWon);
            }
            foreach (var player in game.PaidOut)
            {
                desc += $"{game.Bets.First(x => x.Better == player.Key).DisplayName} won **${decimal.Round(player.Value, 2)}**.\r\n";
            }
            foreach (var player in game.BustedPlayers)
            {
                desc += $"{game.Bets.First(x => x.Better == player).DisplayName} **bust**.\r\n";
            }

            var emb = new EmbedBuilder()
                .WithTitle("High Low Results")
                .WithColor(new Color(40, 255, 40))
                .WithDescription($"**Rounds Played:** {game.CurrentRound}\r\n**Bet:** ${game.Bet}\r\n**Last Card:** *{game.CurrentCard}*\r\n**Results:**\r\n{desc}");


            var guild = _client.GetGuild(game.Guild);
            var channel = guild.GetTextChannel(game.Channel);

            //Delete game board
            var msg = await channel.GetMessageAsync(game.Message);
            await msg.DeleteAsync();

            //Send results
            await channel.SendMessageAsync(embed: emb.Build());

            await _botLog.BotLogAsync(BotLogSeverity.Good, "High-Low game ended", $"{game.Id}\r\nr:{game.CurrentRound}\r\nbet: {game.Bet}\r\nplays :{game.Bets.Count}\r\nbusts {string.Join(",", game.BustedPlayers)}\r\npayouts: {string.Join(";", game.PaidOut.Select(x=> x.Key + "=" + x.Value).ToArray())}\r\n{game.Started}");

            Games.Remove(game);
            FinishedGames.Add(game);
        }

        public async void CancelGame(IGamblingState game, SocketMessageComponent msg)
        {
            switch (game.GameType)
            {
                case GameType.Roulette:
                     
                    break;

                case GameType.Custom:
                    var finished = game as CustomState;

                    var guild = _client.GetGuild(finished.Guild);
                    var channel = guild.GetTextChannel(finished.Channel);
                    var message = await channel.GetMessageAsync(finished.Message);
                    await message.DeleteAsync();

                    var houseFee = decimal.Round(finished.Pot * 0.05m, 2);
                    var endingPot = finished.Pot - houseFee;
                    Pot += houseFee;

                    var alreadyMessaged = new List<ulong>();

                    foreach (var bet in finished.Bets)
                    {
                        var account = _accounts.FindOrCreate(bet.Better).Value;
                        account.ModifyBalance(decimal.Round(endingPot / finished.Bets.Count, 2));

                        if (!alreadyMessaged.Contains(bet.Better))
                        {
                            alreadyMessaged.Add(bet.Better);
                            try
                            {
                                var user = guild.GetUser(bet.Better);
                                if (user != null)
                                {
                                    await user.SendMessageAsync($"The custom wager \"{finished.Title}\" has been cancelled. You have been refunded.");
                                }
                            }
                            catch (Exception)
                            {
                                //swallow
                            }
                        }
                    }
                    _accounts.Save();
                    Save();

                    break;
            }

            FinishedGames.Add(game);
            Games.Remove(game);
        }

        #region TIMERS

        public void StartGameTimer(Guid Id, SocketMessageComponent Msg)
        {
            var game = Games.FirstOrDefault(x => x.Id == Id);
            game.State = GameState.Playing;

            var t = new GameTimer
            {
                Interval = 5 * 1000,
                GameId = Id,
                AutoReset = false,
                Msg = Msg
            };

            t.Elapsed += GameTimerCompleted;
            t.Start();
            Timers.Add(t);
        }

        private void GameTimerCompleted(object sender, ElapsedEventArgs e)
        {
            var Id = ((GameTimer)sender).GameId;
            var Msg = ((GameTimer)sender).Msg;
            var game = Games.FirstOrDefault(x => x.Id == Id);
            game.State = GameState.PendingPayout;
            Payout(game, Msg);
            Timers.Remove((GameTimer)sender);
        }

        public void StartHiLoTimer(Guid Id, SocketMessageComponent Msg)
        {
            var game = Games.FirstOrDefault(x => x.Id == Id);
            game.State = GameState.Playing;

            var t = new GameTimer
            {
                Interval = 60 * 1000,
                GameId = Id,
                AutoReset = false,
                Msg = Msg
            };

            t.Elapsed += HiLoTimerCompleted;
            t.Start();
            Timers.Add(t);
        }

        private async void HiLoTimerCompleted(object sender, ElapsedEventArgs e)
        {
            var Id = ((GameTimer)sender).GameId;
            var Msg = ((GameTimer)sender).Msg;
            var game = Games.FirstOrDefault(x => x.Id == Id) as HiLoState;
            //If game times out, head to next round and end game.
            game.NextRound();
            await EndHiLoAsync(game);
            Timers.Remove((GameTimer)sender);
            await _botLog.BotLogAsync(BotLogSeverity.Meh, "High-Low game timed out", $"{game.Id}");

        }

        public void ResetHiLoTimer(Guid id)
        {
            //Reset if a bet is placed.
            var timer = Timers.FirstOrDefault(x => x.GameId == id);
            timer.Stop();
            timer.Start();
        }
        #endregion

    }

    public class GameTimer : Timer
    {
        public Guid GameId;
        public SocketMessageComponent Msg;
    }
}
