using BotterDog.Entities;
using CSharpFunctionalExtensions;
using Discord;
using Discord.Rest;
using Discord.WebSocket;
using FiresStuff.Services;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;

namespace BotterDog.Services
{
    public class BankService
    {
        /// <summary>
        /// Active games
        /// </summary>
        public List<GamblingState> Games { get; set; }

        /// <summary>
        /// Timers, used for delayed payout
        /// </summary>
        public List<GameTimer> Timers { get; set; }

        /// <summary>
        /// Historical log of played games
        /// </summary>
        public List<GamblingState> FinishedGames { get; set; }

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

            //Link events
            _client.ModalSubmitted += RouletteModalSubmitted;
            _client.ButtonExecuted += RouletteButtonExecuted;

            _random = new Random();

            Games = new List<GamblingState>();
            FinishedGames = new List<GamblingState>();           
            Timers = new List<GameTimer>();
        }

        public Result Load()
        {
            try
            {
                Pot = JsonConvert.DeserializeObject<decimal>(File.ReadAllText("pot.json"));
                _botLog.BotLogAsync(BotLogSeverity.Good, "Pot loaded", "Accounts loaded successfully.");
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

        #region ROULETTE

        //Prepare some nasty values for reference.
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
            //client.ModalSubmitted captures all modal submits so this allows us to narrow it down to our game
            if (!arg.Data.CustomId.StartsWith("roul")) { return; }

            var accnt = _accounts.FindOrCreate(arg.User.Id).Value;

            //We also include the game GUID in the id so lets get that
            var gameId = new Guid(arg.Data.CustomId.Split(":").Last());

            //Find game
            var game = Games.FirstOrDefault(x => x.Id == gameId);
            //If for whatever reason our game has vanished (such as being completed, but the embed didn't update) we error out.
            if(game == null) { await arg.Channel.SendMessageAsync("Game doesn't exist.");  return; } 

            //If game is in the play state, and all bets have made it, we silently fail.
            if (game.State != GameState.Betting) { return; }

            //Find our original message for embed updates.
            var guild = _client.GetGuild(game.Guild);
            var channel = guild.GetTextChannel(game.Channel);
            var m = await channel.GetMessageAsync(game.Message) as IUserMessage;

            //Switch depending on our modals 'action'
            switch (arg.Data.CustomId.Split(":").First())
            {
                case "roul-single": //Single choice(s) of numbers
                    //Find our input
                    var input = arg.Data.Components.ToList().First(x => x.CustomId == "roul-sing-pick").Value;
                    //Remove any spaces
                    var cleanInput = string.Concat(input.Where(c => !char.IsWhiteSpace(c)));
                    //Split by comma
                    var choices = cleanInput.Split(',');

                    if(accnt.Balance <= choices.Length * game.Bet)
                    {
                        await arg.User.SendMessageAsync("You don't have enough money to place this bet.");
                        await arg.DeferAsync();
                        return;
                    }   

                    for (int i = 0; i < choices.Length; i++)
                    {
                        if (choices[i] == "0") //If our bet is on the 'zero' tab, we put it in as `37`
                        {
                            game.Bets.Add(new Bet(arg.User.Id, (arg.User as SocketGuildUser).DisplayName, game.Bet, 36, new[] { 37 }));
                            accnt.Balance -= game.Bet;
                            game.Pot += game.Bet;
                        }
                        else if (choices[i] == "00") //If our bet is on the `double zero` tab, we put in as `38`
                        {
                            game.Bets.Add(new Bet(arg.User.Id, (arg.User as SocketGuildUser).DisplayName, game.Bet, 36, new[] { 38 }));
                            accnt.Balance -= game.Bet;
                            game.Pot += game.Bet;
                        }
                        else
                        {
                            if (int.TryParse(choices[i], out int val))
                            {
                                if (val > 0 && val <= 36) //If input is within the board
                                {
                                    game.Bets.Add(new Bet(arg.User.Id, (arg.User as SocketGuildUser).DisplayName, game.Bet, 36, new[] { val }));
                                    accnt.Balance -= game.Bet;
                                    game.Pot += game.Bet;
                                }
                            }
                            else
                            {
                                //await arg.RespondAsync("Invalid input (ftPti).");
                                await arg.DeferAsync(); //'Accept' the modal so it closes, but don't respond.
                                return;
                            }
                        }
                    }
                    //Update our embed with our bet data
                   await UpdateEmbed(m, game);
                    _accounts.Save();

                    break;
                case "roul-split": //For a 'split' between two tiles
                    input = arg.Data.Components.ToList().First(x => x.CustomId == "roul-split-pick").Value;
                    cleanInput = string.Concat(input.Where(c => !char.IsWhiteSpace(c)));
                    choices = cleanInput.Split(',');

                    if (accnt.Balance <= game.Bet)
                    {
                        await arg.User.SendMessageAsync("You don't have enough money to place this bet.");
                        await arg.DeferAsync();
                        return;
                    }

                    if (choices.Length == 2)
                    {
                        //Check our values for zeros so we can put in our custom numbers.
                        if ((choices[0] == "0" && choices[1] == "00") || (choices[0] == "00" && choices[1] == "0"))
                        {
                            game.Bets.Add(new Bet(arg.User.Id, (arg.User as SocketGuildUser).DisplayName, game.Bet, 18, new[] { 37, 38 }));
                            accnt.Balance -= game.Bet;
                            game.Pot += game.Bet;
                            await UpdateEmbed(m, game);
                            _accounts.Save();
                        }
                        else
                        {

                            if (int.TryParse(choices[0], out int firstVal) && int.TryParse(choices[1], out int secondVal))
                            {
                                //Check our values to make sure they are next to eachother or above/below.
                                if (firstVal + 1 == secondVal || firstVal - 1 == secondVal || firstVal + 3 == secondVal || firstVal - 3 == secondVal)
                                {
                                    game.Bets.Add(new Bet(arg.User.Id, (arg.User as SocketGuildUser).DisplayName, game.Bet, 18, new[] { firstVal, secondVal }));
                                    accnt.Balance -= game.Bet;
                                    game.Pot += game.Bet;
                                    await UpdateEmbed(m, game);
                                    _accounts.Save();
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
                case "roul-corner": //For 4 number corner picks
                    input = arg.Data.Components.ToList().First(x => x.CustomId == "roul-corner-pick").Value;
                    cleanInput = string.Concat(input.Where(c => !char.IsWhiteSpace(c)));
                    choices = cleanInput.Split(',');

                    if (accnt.Balance <= game.Bet)
                    {
                        await arg.User.SendMessageAsync("You don't have enough money to place this bet.");
                        await arg.DeferAsync();
                        return;
                    }

                    if (choices.Length == 4)
                    {
                        //We don't accept zeros, so just error out silently.
                        if(choices.Contains("0") || choices.Contains("00"))
                        {
                            //await arg.RespondAsync("Cannot use 0 or 00 in corners.");
                            await arg.DeferAsync();
                            return;
                        }

                        //Nasty parse
                        if (int.TryParse(choices[0], out int firstVal) &&
                            int.TryParse(choices[1], out int secondVal) &&
                            int.TryParse(choices[2], out int thirdVal) &&
                            int.TryParse(choices[3], out int fourthVal))
                        {
                            //Check if values are all near eachother, this one is pretty format specific unlike the split.
                            if ((firstVal + 1 == secondVal || firstVal - 1 == secondVal) &&
                                (thirdVal + 1 == fourthVal || thirdVal - 1 == fourthVal))
                            {
                                game.Bets.Add(new Bet(arg.User.Id, (arg.User as SocketGuildUser).DisplayName, game.Bet, 9, new[] { firstVal, secondVal, thirdVal, fourthVal }));
                                accnt.Balance -= game.Bet;
                                game.Pot += game.Bet;
                                await UpdateEmbed(m, game);
                                _accounts.Save();
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
                case "roul-dozen": //For 1-12, 13-24, 25-36
                    input = arg.Data.Components.ToList().First(x => x.CustomId == "roul-dozen-pick").Value;
                    cleanInput = string.Concat(input.Where(c => !char.IsWhiteSpace(c))).ToLower();

                    if (accnt.Balance <= game.Bet)
                    {
                        await arg.User.SendMessageAsync("You don't have enough money to place this bet.");
                        await arg.DeferAsync();
                        return;
                    }

                    //Since they are large selections of numbers, we take a few options
                    switch (cleanInput)
                    {
                        case "first":
                            game.Bets.Add(new Bet(arg.User.Id, (arg.User as SocketGuildUser).DisplayName, game.Bet, 3, _first12));
                            break;
                        case "1-12":
                            game.Bets.Add(new Bet(arg.User.Id, (arg.User as SocketGuildUser).DisplayName, game.Bet, 3, _first12));
                            break;

                        case "second":
                            game.Bets.Add(new Bet(arg.User.Id, (arg.User as SocketGuildUser).DisplayName, game.Bet, 3, _second12));
                            break;
                        case "13-24":
                            game.Bets.Add(new Bet(arg.User.Id, (arg.User as SocketGuildUser).DisplayName, game.Bet, 3, _second12));
                            break;

                        case "third":
                            game.Bets.Add(new Bet(arg.User.Id, (arg.User as SocketGuildUser).DisplayName, game.Bet, 3, _third12));
                            break;
                        case "25-36":
                            game.Bets.Add(new Bet(arg.User.Id, (arg.User as SocketGuildUser).DisplayName, game.Bet, 3, _third12));
                            break;
                        default:
                            //await arg.RespondAsync($"Invalid input (nAaI).");
                            await arg.DeferAsync();
                            return;
                    }
                    accnt.Balance -= game.Bet;
                    game.Pot += game.Bet;
                    await UpdateEmbed(m, game);
                    _accounts.Save();

                    break;
                case "roul-halves": //For either half of the board
                    input = arg.Data.Components.ToList().First(x => x.CustomId == "roul-halves-pick").Value;
                    cleanInput = string.Concat(input.Where(c => !char.IsWhiteSpace(c))).ToLower();

                    if (accnt.Balance <= game.Bet)
                    {
                        await arg.User.SendMessageAsync("You don't have enough money to place this bet.");
                        await arg.DeferAsync();
                        return;
                    }

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
                    accnt.Balance -= game.Bet;
                    game.Pot += game.Bet;
                    await UpdateEmbed(m, game);
                    _accounts.Save();
                    break;

            }
            await arg.DeferAsync();
        }

        private async Task RouletteButtonExecuted(SocketMessageComponent arg)
        {
            //Since client.ButtonExecuted captures all button clicks, let's sort by ours.
            if (!arg.Data.CustomId.StartsWith("roul")) { return; }

            var accnt = _accounts.FindOrCreate(arg.User.Id).Value;

            //Find game GUID off id
            var gameId = new Guid(arg.Data.CustomId.Split(":").Last());

            //Find game
            var game = Games.FirstOrDefault(x => x.Id == gameId);
            if (game == null) { await arg.Channel.SendMessageAsync($"{arg.User.Mention} Game doesn't exist anymore."); return; }


            switch (arg.Data.CustomId.Split(":").First())
            {
                case "roul-spin": //For final 'spin'
                    //Only allow started to spin
                    if(arg.User.Id != game.Creator) { await arg.User.SendMessageAsync("You cannot spin the wheel as you are not the starter of the game"); return; }

                    //Throw up timer for the 'spin'
                    StartGameTimer(game.Id, arg);
                    //Update message
                    await arg.UpdateAsync(x =>
                    {
                        //Clear all button components
                        x.Components = new ComponentBuilder().Build();
                        //Change embed
                        x.Embeds = new Embed[] { new EmbedBuilder()
                            .WithTitle("Roulette")
                            .WithColor(255, 20, 20)
                            .WithDescription("**Rolling**")
                            .WithImageUrl("https://img1.picmix.com/output/stamp/normal/4/5/9/5/1515954_ea5fd.gif")
                            .Build() };
                    });
                    break;
                case "roul-red": //For reds
                    if (game.State != GameState.Betting) { return; }

                    if (accnt.Balance <= game.Bet)
                    {
                        await arg.User.SendMessageAsync("You don't have enough money to place this bet.");
                        await arg.DeferAsync();
                        return;
                    }

                    game.Bets.Add(new Bet(arg.User.Id, (arg.User as SocketGuildUser).DisplayName, game.Bet, 2, _reds));
                    accnt.Balance -= game.Bet;
                    game.Pot += game.Bet;
                    await _updateEmbed(arg, game);
                    _accounts.Save();
                    break;
                case "roul-black": //For blacks
                    if (game.State != GameState.Betting) { return; }

                    if (accnt.Balance <= game.Bet)
                    {
                        await arg.User.SendMessageAsync("You don't have enough money to place this bet.");
                        await arg.DeferAsync();
                        return;
                    }

                    game.Bets.Add(new Bet(arg.User.Id, (arg.User as SocketGuildUser).DisplayName, game.Bet, 2, _blacks));
                    accnt.Balance -= game.Bet;
                    game.Pot += game.Bet;
                    await _updateEmbed(arg, game);
                    _accounts.Save();
                    break;
                case "roul-odds": //For odds
                    if (game.State != GameState.Betting) { return; }

                    if (accnt.Balance <= game.Bet)
                    {
                        await arg.User.SendMessageAsync("You don't have enough money to place this bet.");
                        await arg.DeferAsync();
                        return;
                    }

                    game.Bets.Add(new Bet(arg.User.Id, (arg.User as SocketGuildUser).DisplayName, game.Bet, 2, _odds));
                    accnt.Balance -= game.Bet;
                    game.Pot += game.Bet;
                    await _updateEmbed(arg, game);
                    _accounts.Save();
                    break;
                case "roul-evens": //For evens
                    if (game.State != GameState.Betting) { return; }

                    if (accnt.Balance <= game.Bet)
                    {
                        await arg.User.SendMessageAsync("You don't have enough money to place this bet.");
                        await arg.DeferAsync();
                        return;
                    }

                    game.Bets.Add(new Bet(arg.User.Id, (arg.User as SocketGuildUser).DisplayName, game.Bet, 2, _evens));
                    accnt.Balance -= game.Bet;
                    game.Pot += game.Bet;
                    await _updateEmbed(arg, game);
                    _accounts.Save();
                    break;
                // --- Modals ---
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

        //Update our original embed with new bet information
        private async Task _updateEmbed(SocketMessageComponent arg, GamblingState game)
        {
            //Find embed on message
            var mainEmbed = arg.Message.Embeds.First();

            await arg.UpdateAsync(x =>
            {
                //Find if `Bets` field already exists
                var betsField = mainEmbed.Fields.FirstOrDefault(x => x.Name == "Bets");
                if (betsField.Name == null)
                {
                    //If not, create it
                    x.Embed = mainEmbed.ToEmbedBuilder().AddField("Bets", FormatBets(game), true).Build();
                }
                else
                {
                    //If yes, build it.
                    var emb = mainEmbed.ToEmbedBuilder();
                    emb.Fields.FirstOrDefault(x => x.Name == "Bets").Value = FormatBets(game);
                    x.Embed = emb.Build();
                }
            });
        }

        //Update our original embed with new bet information
        //This is an override for the modals as they do not have access to the SocketMessageComponent
        private async Task UpdateEmbed(IUserMessage arg, GamblingState game)
        {
            //Find embed on message
            var mainEmbed = arg.Embeds.First();

            await arg.ModifyAsync(x =>
            {
                //Find if `Bets` field already exists
                var betsField = mainEmbed.Fields.FirstOrDefault(x => x.Name == "Bets");
                if (betsField.Name == null)
                {
                    //If not, create it
                    x.Embed = mainEmbed.ToEmbedBuilder().AddField("Bets", FormatBets(game), true).Build();
                }
                else
                {
                    //If yes, build it.
                    var emb = mainEmbed.ToEmbedBuilder();
                    emb.Fields.FirstOrDefault(x => x.Name == "Bets").Value = FormatBets(game);
                    x.Embed = emb.Build();
                }
            });
        }

        //Format bets nicely into a larger string
        private string FormatBets(GamblingState game)
        {
            var bets = new List<string>();
            foreach (var bet in game.Bets) //Move to .Single?
            {
                bets.Add($"{bet.DisplayName} on {FormatHits(bet.Hits)} ({bet.Odds - 1}x, payout: ${game.Bet * bet.Odds})");
            }

            //Find amount of bets, so duplicates are not repeated in field.
            var totalBets = bets.GroupBy(x => x)
                .Select(g => new { Value = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count);
            string output = "";
            foreach (var totals in totalBets)
            {
                output += $"{totals.Count}x {totals.Value}\r\n";
            }
            return output;
        }

        //Format hits if they match
        //Wish this could be a Switch statement but C# wont take it for non 'constant'
        private string FormatHits(int[] hits)
        {
            string output;
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
                output = output.Replace("37", "0").Replace("38", "00");
            }

            return output;
        }

        #endregion

        public async void Payout(GamblingState game, SocketMessageComponent msg)
        {
            switch (game.GameType)
            {
                case GameType.Roulette:
                    var result = _fullBoard[_random.Next(0, _fullBoard.Length)];
                    await msg.DeleteOriginalResponseAsync();

                    var winningBets = new List<Bet>();

                    foreach (var bet in game.Bets)
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
                            desc += $"\r\n{bet.DisplayName} won ${bet.Amount * bet.Odds}({bet.Odds - 1}x ${bet.Amount})";
                            var account = _accounts.FindOrCreate(bet.Better).Value;
                            totalWon += (bet.Amount * bet.Odds);
                            account.ModifyBalance(bet.Amount * bet.Odds);
                        }
                    }

                    Pot += game.Pot;
                    Pot -= totalWon;

                    await msg.Channel.SendMessageAsync("", embed: new EmbedBuilder()
                        .WithTitle($"{textcolor} {formattedResult}")
                        .WithDescription(desc)
                        .WithColor(embedColor)
                        .Build());
                    await _botLog.BotLogAsync(BotLogSeverity.Meh, "Roulette game payed out", $"Payout completed for game {game.Id}:\r\n{game.Bets.Count} bets totalling {game.Pot}\r\n{desc}");
                    _accounts.Save();
                    Save();
                    break;
            }


            FinishedGames.Add(game);
            Games.Remove(game);
            game.State = GameState.Finished;
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
