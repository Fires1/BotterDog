using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using System;
using System.Threading.Tasks;
using FiresStuff.Services;
using BotterDog.Services;
using BotterDog.Entities;

namespace FiresStuff.Modules
{
    public class GamblingModule : InteractionModuleBase<SocketInteractionContext>
    {
        private readonly BotLogService _botLog;
        private readonly AccountService _accounts;
        private readonly IServiceProvider _services;
        private readonly DiscordSocketClient _client;
        private readonly BankService _bank;



        public GamblingModule(BotLogService botlog, AccountService accnts, DiscordSocketClient client, BankService bank, IServiceProvider services)
        {
            _botLog = botlog;
            _services = services;
            _accounts = accnts;
            _client = client;
            _bank = bank;
        }

        #region BALANCE STUFF

        [SlashCommand("bal", "Check your account balance")]
        public async Task BalanceAsync()
        {
            var accnt = _accounts.FindOrCreate(Context.User.Id);
            await RespondAsync(text: $"{Context.User.Mention}, you have an account balance of **${decimal.Round(accnt.Value.Balance, 2)}** doggy dawg bucks.");
        }

        [SlashCommand("checkbal", "Check someone else's account balance")]
        public async Task CheckBalanceAsync(IGuildUser user)
        {
            var accnt = _accounts.FindOrCreate(user.Id);
            await RespondAsync(text: $"{user.DisplayName} has an account balance of **${decimal.Round(accnt.Value.Balance, 2)}** doggy dawg bucks.");
        }

        [SlashCommand("give", "Give money to someone else")]
        public async Task GiveAsync(IGuildUser user, decimal amount)
        {
            var amt = decimal.Round(amount, 2);

            var caller = _accounts.FindOrCreate(Context.User.Id);
            var target = _accounts.FindOrCreate(user.Id);

            if(amt <= 0.0m)
            {
                await RespondAsync("Entered amount must be more than **$0.0**", ephemeral: true);
                return;
            }

            if(amt > caller.Value.Balance)
            {
                await RespondAsync($"You do not have enough money for this transaction! You have **${caller.Value.Balance}**", ephemeral: true);
                return;
            }
            else
            {
                caller.Value.ModifyBalance(-amt);
                target.Value.ModifyBalance(amt);

                _accounts.Save();
                await RespondAsync($"{Context.User.Mention}, you've given {user.Mention} **${amt}** doggy dawg bucks.");
                await _botLog.BotLogAsync(BotLogSeverity.Good, "Money given", $"{Context.User.Username} gave {user.Username} ${amt} doggie bucks.");
            }
        }

        [SlashCommand("setbalance", "ADMINONLY")]
        [RequireOwner]
        public async Task SetBalance(IGuildUser user, decimal amount)
        {
            var amt = decimal.Round(amount, 2);

            var target = _accounts.FindOrCreate(user.Id);
            target.Value.Balance = amt;

            _accounts.Save();
            await RespondAsync($"Set {user.Mention}'s balance to {amt}.", ephemeral: true);
            await _botLog.BotLogAsync(BotLogSeverity.Bad, "BALANCE SET", $"{Context.User.Username} set {user.Username} balance to ${amt} doggie bucks.");
        }

        #endregion


        #region ROULETTE 

        [SlashCommand("roulette", "Play some roulette!")]
        public async Task Roulette(decimal Bet)
        {
            var accnt = _accounts.FindOrCreate(Context.User.Id);

            if(Bet > accnt.Value.Balance)
            {
                await RespondAsync("You do not have enough money to make the starter bet, therefore you are too broke to start this game.", ephemeral: true);
            }

            var game = new GamblingState(Context.User.Id, GameType.Roulette, Bet);

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

            await RespondAsync("", embed: emb.Build(),  components: builder.Build());

            //Get message really quick and update our Ids
            var response = await GetOriginalResponseAsync();
            game.Guild = Context.Guild.Id;
            game.Channel = Context.Channel.Id;
            game.Message = response.Id;
            _bank.Games.Add(game);
        }

        #endregion
    }
}