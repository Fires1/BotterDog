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

        //Initialize stuff we need immediately
        private DiscordSocketClient _client;
        private InteractionService commands;
        private BotLogService _botLog;
        private IServiceProvider serviceProvider;

        public async Task MainAsync()
        {
            //Configure Discord intents
            var config = new DiscordSocketConfig()
            {
                GatewayIntents = GatewayIntents.AllUnprivileged &~ (GatewayIntents.GuildScheduledEvents | GatewayIntents.GuildInvites),
            };
            _client = new DiscordSocketClient(config);

            //Prepare service provider and pull some of the ones we need.
            serviceProvider = ConfigureServices();
            commands = serviceProvider.GetRequiredService<InteractionService>();
            _botLog = serviceProvider.GetRequiredService<BotLogService>();
            var accnts = serviceProvider.GetRequiredService<AccountService>();

            //Link logging methods
            _client.Log += LogAsync;
            commands.Log += LogAsync;

            //Initialize command handler
            await serviceProvider.GetRequiredService<CommandHandler>().InitializeAsync();

            //When we're connected and ready, log it and set activity.
            _client.Ready += async () =>
            {
                //Load accounts from file
                accnts.Load();
                //Re-register commands if any updates occur
                await commands.RegisterCommandsToGuildAsync(752755222505586739, true); //Sam's Stuff
                //await commands.RegisterCommandsToGuildAsync(537791310212628501, true); //Doggiedogs
                await _client.Rest.DeleteAllGlobalCommandsAsync();
                await _botLog.BotLogAsync(BotLogSeverity.Good, "Ready!", "Bot has been booted and is ready");
                await _client.SetGameAsync("with butter", type: ActivityType.Playing); 
            };

            //Super secure
            var token = File.ReadAllText("token.txt");

            await _client.LoginAsync(TokenType.Bot, token);
            await _client.StartAsync();

            // Block this task until the program is closed.
            await Task.Delay(Timeout.Infinite);
        }

        //Bone basic logging
        static Task LogAsync(LogMessage message)
        {
            switch(message.Severity)
            {
                case LogSeverity.Critical:
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine(message.ToString());
                    Console.ForegroundColor = ConsoleColor.White;
                    break;
                case LogSeverity.Error:
                    Console.ForegroundColor = ConsoleColor.DarkRed;
                    Console.WriteLine(message.ToString());
                    Console.ForegroundColor = ConsoleColor.White;
                    break;
                case LogSeverity.Warning:
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine(message.ToString());
                    Console.ForegroundColor = ConsoleColor.White;
                    break;
                case LogSeverity.Info:
                    Console.ForegroundColor = ConsoleColor.White;
                    Console.WriteLine(message.ToString());
                    break;
                case LogSeverity.Debug:
                    Console.ForegroundColor = ConsoleColor.Gray;
                    Console.WriteLine(message.ToString());
                    break;
            }
            return Task.CompletedTask;
        }

        //Add singletons of our desired services.
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
