using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Threading;
using System.Threading.Tasks;
using FiresStuff.Services;
using BotterDog.Services;
using System.IO;

namespace FiresStuff
{
    public class Program
    {
        public static void Main()
        => new Program().MainAsync().GetAwaiter().GetResult();

        private DiscordSocketClient _client;
        private InteractionService commands;
        private BotLogService _botLog;
        private IServiceProvider serviceProvider;

        public async Task MainAsync()
        {
            var config = new DiscordSocketConfig()
            {
                GatewayIntents = GatewayIntents.AllUnprivileged &~ (GatewayIntents.GuildScheduledEvents | GatewayIntents.GuildInvites),
            };

            _client = new DiscordSocketClient(config);
            serviceProvider = ConfigureServices();

            commands = serviceProvider.GetRequiredService<InteractionService>();
            _botLog = serviceProvider.GetRequiredService<BotLogService>();
            var accnts = serviceProvider.GetRequiredService<AccountService>();
            accnts.Load();

            _client.Log += LogAsync;
            commands.Log += LogAsync;

            await serviceProvider.GetRequiredService<CommandHandler>().InitializeAsync();

            _client.Ready += async () =>
            {
                await commands.RegisterCommandsToGuildAsync(752755222505586739, true); //Sam's Stuff
                //await commands.RegisterCommandsToGuildAsync(537791310212628501, true); //Doggiedogs
                await _client.Rest.DeleteAllGlobalCommandsAsync();
                await _botLog.BotLogAsync(BotLogSeverity.Good, "Ready!", "Bot has been booted and is ready");
                await _client.SetGameAsync("with butter", type: ActivityType.Playing); 
            };

            var token = File.ReadAllText("token.txt");

            await _client.LoginAsync(TokenType.Bot, token);
            await _client.StartAsync();

            // Block this task until the program is closed.
            await Task.Delay(Timeout.Infinite);
        }

        static Task LogAsync(LogMessage message)
        {
            Console.WriteLine(message.ToString());
            return Task.CompletedTask;
        }

        private IServiceProvider ConfigureServices()
        {
            var provider = new ServiceCollection()
                .AddSingleton(_client)
                .AddSingleton(x => new InteractionService(x.GetRequiredService<DiscordSocketClient>()))
                .AddSingleton<CommandHandler>()
                .AddSingleton<BotLogService>()
                .AddSingleton<AccountService>()
                .AddSingleton<BankService>()
                .BuildServiceProvider();
            return provider;
        }
    }
}
