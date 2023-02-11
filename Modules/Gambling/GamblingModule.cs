using Discord.Interactions;
using FiresStuff.Services;
using System;
using BotterDog.Services;
using Discord.WebSocket;

namespace BotterDog.Modules
{
    public partial class GamblingModule : InteractionModuleBase<SocketInteractionContext>
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
    }
}
