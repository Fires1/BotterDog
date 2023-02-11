using BotterDog.Entities;
using Discord;
using Discord.Rest;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;

namespace BotterDog.Services
{
    public class BankService
    {
        public List<GamblingState> Games { get; set; }
        public List<GameTimer> Timers { get; set; }
        public List<GamblingState> FinishedGames { get; set; }
        private readonly DiscordSocketClient _client;

        private readonly Random _random;


        public BankService(DiscordSocketClient client)
        {
            _client = client;

            _client.ModalSubmitted += RouletteModalSubmitted;
            _client.ButtonExecuted += RouletteButtonExecuted;

            _random = new Random();

            Games = new List<GamblingState>();
            FinishedGames = new List<GamblingState>();           
            Timers = new List<GameTimer>();
        }

        public async void Payout(GamblingState game, SocketMessageComponent msg)
        {
            switch(game.GameType)
            {
                case GameType.Roulette:
                    var result = _fullBoard[_random.Next(0, _fullBoard.Length)];
                    await msg.DeleteOriginalResponseAsync();

                    var winningBets = new List<Bet>();

                    foreach(var bet in game.Bets)
                    {
                        if(bet.Hits.Contains(result))
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

                    if(result == 37)
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

                    if(winningBets.Count > 0)
                    {
                        desc = "Winners:";
                        foreach(var bet in winningBets)
                        {
                            desc += $"\r\n{bet.DisplayName} won ${bet.Amount * bet.Odds}({bet.Odds - 1}x ${bet.Amount})";
                        }
                    }

                    await msg.Channel.SendMessageAsync("", embed: new EmbedBuilder()
                        .WithTitle($"{textcolor} {formattedResult}")
                        .WithDescription(desc)
                        .WithColor(embedColor)
                        .Build()); 
                    break;
            }



            FinishedGames.Add(game);
            Games.Remove(game);
            game.State = GameState.Finished;
        }

        #region TIMERS

        public void StartGameTimer(Guid Id, SocketMessageComponent Msg)
        {
            var game = Games.FirstOrDefault(x=>x.Id == Id);
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


        #region ROULETTE

        private readonly int[] _fullBoard = Enumerable.Range(1, 38).ToArray();
        private readonly int[] _odds = Enumerable.Range(1, 36).Where(x => x % 2 == 1).ToArray();
        private readonly int[] _evens = Enumerable.Range(1, 36).Where(x => x % 2 == 0).ToArray();
        private readonly int[] _first12 = Enumerable.Range(1, 12).ToArray();
        private readonly int[] _second12 = Enumerable.Range(13, 24).ToArray();
        private readonly int[] _third12 = Enumerable.Range(25, 36).ToArray();
        private readonly int[] _firstHalf = Enumerable.Range(1, 18).ToArray();
        private readonly int[] _secondHalf = Enumerable.Range(19, 36).ToArray();
        private readonly int[] _reds = { 1, 3, 5, 7, 9, 12, 14, 16, 18, 19, 21, 23, 25, 27, 30, 32, 34, 36 };
        private readonly int[] _blacks = { 2, 4, 6, 8, 10, 11, 13, 15, 17, 20, 22, 24, 26, 28, 29, 31, 33, 35 };

        private async Task RouletteModalSubmitted(SocketModal arg)
        {
            if (!arg.Data.CustomId.StartsWith("roul")) { return; }

            var gameId = new Guid(arg.Data.CustomId.Split(":").Last());

            var game = Games.FirstOrDefault(x => x.Id == gameId);
            if(game == null) { await arg.Channel.SendMessageAsync("Game doesn't exist.");  return; }

            if (game.State != GameState.Betting) { return; }

            var g = _client.GetGuild(game.Guild);
            var t = g.GetTextChannel(game.Channel);
            var m = await t.GetMessageAsync(game.Message) as IUserMessage;

            switch (arg.Data.CustomId.Split(":").First())
            {
                case "roul-single":
                    var input = arg.Data.Components.ToList().First(x => x.CustomId == "roul-sing-pick").Value;
                    var cleanInput = string.Concat(input.Where(c => !char.IsWhiteSpace(c)));
                    var choices = cleanInput.Split(',');

                    for (int i = 0; i < choices.Length; i++)
                    {
                        if (choices[i] == "0")
                        {
                            game.Bets.Add(new Bet(arg.User.Id, (arg.User as SocketGuildUser).DisplayName, game.Bet, 36, new[] { 37 }));
                        }
                        else if (choices[i] == "00")
                        {
                            game.Bets.Add(new Bet(arg.User.Id, (arg.User as SocketGuildUser).DisplayName, game.Bet, 36, new[] { 38 }));
                        }
                        else
                        {
                            if (int.TryParse(choices[i], out int val))
                            {
                                game.Bets.Add(new Bet(arg.User.Id, (arg.User as SocketGuildUser).DisplayName, game.Bet, 36, new[] { val }));
                            }
                            else
                            {
                                //await arg.RespondAsync("Invalid input (ftPti).");
                                await arg.DeferAsync();
                                return;
                            }
                        }
                    }
                   await _updateEmbed(m, game);

                    break;
                case "roul-split":
                    input = arg.Data.Components.ToList().First(x => x.CustomId == "roul-split-pick").Value;
                    cleanInput = string.Concat(input.Where(c => !char.IsWhiteSpace(c)));
                    choices = cleanInput.Split(',');

                    if (choices.Length == 2)
                    {
                        if ((choices[0] == "0" && choices[1] == "00") || (choices[0] == "00" && choices[1] == "0"))
                        {
                            game.Bets.Add(new Bet(arg.User.Id, (arg.User as SocketGuildUser).DisplayName, game.Bet, 18, new[] { 37, 38 }));
                            await _updateEmbed(m, game);
                        }
                        else
                        {

                            if (int.TryParse(choices[0], out int firstVal) && int.TryParse(choices[1], out int secondVal))
                            {
                                if (firstVal + 1 == secondVal || firstVal - 1 == secondVal || firstVal + 3 == secondVal || firstVal - 3 == secondVal)
                                {
                                    game.Bets.Add(new Bet(arg.User.Id, (arg.User as SocketGuildUser).DisplayName, game.Bet, 18, new[] { firstVal, secondVal }));
                                    await _updateEmbed(m, game);
                                }
                            }
                            else
                            {
                                //await arg.RespondAsync("Invalid input (ViTP).");
                                await arg.DeferAsync();
                                return;
                            }
                        }
                    }
                    else
                    {
                        //await arg.RespondAsync("Not enough choices provided (MoLtT).");
                        await arg.DeferAsync();
                        return;
                    }
                    break;
                case "roul-corner":
                    input = arg.Data.Components.ToList().First(x => x.CustomId == "roul-corner-pick").Value;
                    cleanInput = string.Concat(input.Where(c => !char.IsWhiteSpace(c)));
                    choices = cleanInput.Split(',');

                    if (choices.Length == 4)
                    {
                        if(choices.Contains("0") || choices.Contains("00"))
                        {
                            //await arg.RespondAsync("Cannot use 0 or 00 in corners.");
                            await arg.DeferAsync();
                            return;
                        }

                        if (int.TryParse(choices[0], out int firstVal) &&
                            int.TryParse(choices[1], out int secondVal) &&
                            int.TryParse(choices[2], out int thirdVal) &&
                            int.TryParse(choices[3], out int fourthVal))
                        {
                            if ((firstVal + 1 == secondVal || firstVal - 1 == secondVal) &&
                                (thirdVal + 1 == fourthVal || thirdVal - 1 == fourthVal))
                            {
                                game.Bets.Add(new Bet(arg.User.Id, (arg.User as SocketGuildUser).DisplayName, game.Bet, 9, new[] { firstVal, secondVal, thirdVal, fourthVal }));
                                await _updateEmbed(m, game);
                            }
                            else
                            {
                                //await arg.RespondAsync("Invalid input (ViTP).");
                                await arg.DeferAsync();
                                return;
                            }
                        }
                        else
                        {
                            //await arg.RespondAsync("Invalid input (ViTP).");
                            await arg.DeferAsync();
                            return;
                        }
                    }
                    else
                    {
                        //await arg.RespondAsync("Not enough choices provided (MoLtF).");
                        await arg.DeferAsync();
                        return;
                    }
                    break;
                case "roul-dozen":
                    input = arg.Data.Components.ToList().First(x => x.CustomId == "roul-dozen-pick").Value;
                    cleanInput = string.Concat(input.Where(c => !char.IsWhiteSpace(c))).ToLower();

                    switch (cleanInput)
                    {
                        case "first":
                            game.Bets.Add(new Bet(arg.User.Id, (arg.User as SocketGuildUser).DisplayName, game.Bet, 3, _first12));
                            break;
                        case "second":
                            game.Bets.Add(new Bet(arg.User.Id, (arg.User as SocketGuildUser).DisplayName, game.Bet, 3, _second12));
                            break;
                        case "third":
                            game.Bets.Add(new Bet(arg.User.Id, (arg.User as SocketGuildUser).DisplayName, game.Bet, 3, _third12));
                            break;
                        default:
                            //await arg.RespondAsync($"Invalid input (nAaI).");
                            await arg.DeferAsync();
                            return;
                    }

                    await _updateEmbed(m, game);

                    break;
                case "roul-halves":
                    input = arg.Data.Components.ToList().First(x => x.CustomId == "roul-halves-pick").Value;
                    cleanInput = string.Concat(input.Where(c => !char.IsWhiteSpace(c))).ToLower();

                    switch (cleanInput)
                    {
                        case "first":
                            game.Bets.Add(new Bet(arg.User.Id, (arg.User as SocketGuildUser).DisplayName, game.Bet, 2, _firstHalf));
                            break;
                        case "second":
                            game.Bets.Add(new Bet(arg.User.Id, (arg.User as SocketGuildUser).DisplayName, game.Bet, 2, _secondHalf));
                            break;
                        default:
                            //await arg.RespondAsync($"Invalid input (nAaH).");
                            await arg.DeferAsync();
                            break;
                    }

                    await _updateEmbed(m, game);
                    break;

            }
            await arg.DeferAsync();
        }

        private async Task RouletteButtonExecuted(SocketMessageComponent arg)
        {
            if (!arg.Data.CustomId.StartsWith("roul")) { return; }

            var gameId = new Guid(arg.Data.CustomId.Split(":").Last());

            var game = Games.FirstOrDefault(x => x.Id == gameId);
            if (game == null) { await arg.Channel.SendMessageAsync($"{arg.User.Mention} Game doesn't exist anymore."); return; }

            switch (arg.Data.CustomId.Split(":").First())
            {
                case "roul-spin":
                    if(arg.User.Id != game.Creator) { await arg.User.SendMessageAsync("You cannot spin the wheel as you are not the starter of the game"); return; }

                    StartGameTimer(game.Id, arg);
                    await arg.UpdateAsync(x =>
                    {
                        x.Components = new ComponentBuilder().Build();
                        x.Embeds = new Embed[] { new EmbedBuilder()
                            .WithTitle("Roulette")
                            .WithColor(255, 20, 20)
                            .WithDescription("**Rolling**")
                            .WithImageUrl("https://img1.picmix.com/output/stamp/normal/4/5/9/5/1515954_ea5fd.gif")
                            .Build() };
                    });
                    break;
                case "roul-red":
                    if (game.State != GameState.Betting) { return; }
                    game.Bets.Add(new Bet(arg.User.Id, (arg.User as SocketGuildUser).DisplayName, game.Bet, 2, _reds));
                    await _updateEmbed(arg, game);
                    break;
                case "roul-black":
                    if (game.State != GameState.Betting) { return; }
                    game.Bets.Add(new Bet(arg.User.Id, (arg.User as SocketGuildUser).DisplayName, game.Bet, 2, _blacks));
                    await _updateEmbed(arg, game);
                    break;
                case "roul-odds":
                    if (game.State != GameState.Betting) { return; }
                    game.Bets.Add(new Bet(arg.User.Id, (arg.User as SocketGuildUser).DisplayName, game.Bet, 2, _odds));
                    await _updateEmbed(arg, game);
                    break;
                case "roul-evens":
                    if (game.State != GameState.Betting) { return; }
                    game.Bets.Add(new Bet(arg.User.Id, (arg.User as SocketGuildUser).DisplayName, game.Bet, 2, _evens));
                    await _updateEmbed(arg, game);
                    break;
                case "roul-single":
                    if (game.State != GameState.Betting) { return; }
                    await arg.RespondWithModalAsync(new ModalBuilder("Single bet", $"roul-single:{game.Id}")
                        .AddTextInput(new TextInputBuilder()
                        .WithLabel("Numbers")
                        .WithCustomId("roul-sing-pick")
                        .WithMinLength(1)
                        .WithMaxLength(60)
                        .WithRequired(true)
                        .WithPlaceholder("Input any single numbers separated by commas, ex: 12,14,16"))
                        .Build());
                    break;
                case "roul-split":
                    if (game.State != GameState.Betting) { return; }
                    await arg.RespondWithModalAsync(new ModalBuilder("Split bet", $"roul-split:{game.Id}")
                        .AddTextInput(new TextInputBuilder()
                        .WithLabel("Numbers")
                        .WithCustomId("roul-split-pick")
                        .WithMinLength(1)
                        .WithMaxLength(6)
                        .WithStyle(TextInputStyle.Paragraph)
                        .WithRequired(true)
                        .WithPlaceholder("Input TWO numbers that are next to eachother (Max 1 per submission) ex: `14,17` or `1,2`"))
                        .Build());
                    break;
                case "roul-corner":
                    if (game.State != GameState.Betting) { return; }
                    await arg.RespondWithModalAsync(new ModalBuilder("Corner Bet", $"roul-corner:{game.Id}")
                        .AddTextInput(new TextInputBuilder()
                        .WithLabel("Numbers")
                        .WithCustomId("roul-corner-pick")
                        .WithMinLength(1)
                        .WithMaxLength(12)
                        .WithStyle(TextInputStyle.Paragraph)
                        .WithRequired(true)
                        .WithPlaceholder("Input FOUR numbers that are next to eachother ex: `11,12,14,15` OR `28,29,31,32`"))
                        .Build());
                    break;
                case "roul-dozen":
                    if (game.State != GameState.Betting) { return; }
                    await arg.RespondWithModalAsync(new ModalBuilder("Dozen Bet", $"roul-dozen:{game.Id}")
                        .AddTextInput(new TextInputBuilder()
                        .WithLabel("Dozen range")
                        .WithCustomId("roul-dozen-pick")
                        .WithMinLength(1)
                        .WithMaxLength(6)
                        .WithStyle(TextInputStyle.Paragraph)
                        .WithRequired(true)
                        .WithPlaceholder("Input `first`, `second` or `third` dozen. ex. `first` (1-12), `second` (13-24), `third` (25-36)"))
                        .Build());
                    break;
                case "roul-halves":
                    if (game.State != GameState.Betting) { return; }
                    await arg.RespondWithModalAsync(new ModalBuilder("Half Bet", $"roul-halves:{game.Id}")
                        .AddTextInput(new TextInputBuilder()
                        .WithLabel("Half")
                        .WithCustomId("roul-halves-pick")
                        .WithMinLength(1)
                        .WithStyle(TextInputStyle.Paragraph)
                        .WithMaxLength(6)
                        .WithRequired(true)
                        .WithPlaceholder("Input `first`, `second` half. ex. `first` (1-18), `second` (19-36)"))
                        .Build());
                    break;

            }
        }

        private async Task _updateEmbed(SocketMessageComponent arg, GamblingState game)
        {
            var mainEmbed = arg.Message.Embeds.First();
            await arg.UpdateAsync(x =>
            {
                var betsField = mainEmbed.Fields.FirstOrDefault(x => x.Name == "Bets");
                if (betsField.Name == null)
                {
                    x.Embed = mainEmbed.ToEmbedBuilder().AddField("Bets", _formatBets(game), true).Build();
                }
                else
                {
                    var emb = mainEmbed.ToEmbedBuilder();
                    emb.Fields.FirstOrDefault(x => x.Name == "Bets").Value = _formatBets(game);
                    x.Embed = emb.Build();
                }
            });
        }

        private async Task _updateEmbed(IUserMessage arg, GamblingState game)
        {
            var mainEmbed = arg.Embeds.First();

            await arg.ModifyAsync(x =>
            {
                var betsField = mainEmbed.Fields.FirstOrDefault(x => x.Name == "Bets");
                if (betsField.Name == null)
                {
                    x.Embed = mainEmbed.ToEmbedBuilder().AddField("Bets", _formatBets(game), true).Build();
                }
                else
                {
                    var emb = mainEmbed.ToEmbedBuilder();
                    emb.Fields.FirstOrDefault(x => x.Name == "Bets").Value = _formatBets(game);
                    x.Embed = emb.Build();
                }
            });

        }

        private string _formatBets(GamblingState game)
        {
            var bets = new List<string>();
            foreach (var bet in game.Bets)
            {
                bets.Add($"{bet.DisplayName} on {_formatHits(bet.Hits)} ({bet.Odds + 1}x, payout: ${game.Bet * bet.Odds + 1})");
            }
            var totalBets = bets.GroupBy(x => x)
                .Select(g => new { Value = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count);
            string output = "";
            foreach (var totals in totalBets)
            {
                output += $"{totals.Count} - {totals.Value}\r\n";
            }
            return output;
        }

        private string _formatHits(int[] hits)
        {
            string output = "";

            if (hits == _odds)
            {
                output = "Odds";
            }
            else if (hits == _evens)
            {
                output = "Evens";
            }
            else if (hits == _reds)
            {
                output = "Reds";
            }
            else if (hits == _blacks)
            {
                output = "Black";
            }
            else if (hits == _first12)
            {
                output = "1-12";
            }
            else if (hits == _second12)
            {
                output = "13-24";
            }
            else if (hits == _third12)
            {
                output = "25-36";
            }
            else if (hits == _firstHalf)
            {
                output = "1-18";
            }
            else if (hits == _secondHalf)
            {
                output = "19-36";
            }
            else
            {
                output = string.Join(", ", hits);
            }

            return output;
        }


        #endregion
    }

    public class GameTimer : Timer
    {
        public Guid GameId;
        public SocketMessageComponent Msg;
    }
}
