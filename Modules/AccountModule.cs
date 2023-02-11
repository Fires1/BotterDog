using Discord;
using Discord.Interactions;
using System.Threading.Tasks;
using FiresStuff.Services;
using BotterDog.Services;

namespace BotterDog.Modules
{
    public partial class AccountModule : InteractionModuleBase<SocketInteractionContext>
    {
        private readonly BotLogService _botLog;
        private readonly AccountService _accounts;

        public AccountModule(BotLogService botlog, AccountService accnts)
        {
            _botLog = botlog;
            _accounts = accnts;
        }

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
    }
}