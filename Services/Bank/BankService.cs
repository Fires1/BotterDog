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

                    _accounts.Save();
                    Save();

                    break;
            }

            game.State = GameState.Finished;
            FinishedGames.Add(game);
            Games.Remove(game);
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
        #endregion

    }

    public class GameTimer : Timer
    {
        public Guid GameId;
        public SocketMessageComponent Msg;
    }
}
