using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using System;
using System.Threading.Tasks;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using FiresStuff.Services;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace FiresStuff.Modules
{
    // Interation modules must be public and inherit from an IInterationModuleBase
    public class UserModule : InteractionModuleBase<SocketInteractionContext>
    {
        private readonly BotLogService _botLog;
        private readonly IServiceProvider _services;

        public UserModule(BotLogService botlog,  IServiceProvider services)
        {
            _botLog = botlog;
            _services = services;
        }

        [SlashCommand("ping", "Pings the bot and returns its latency.")]
        public async Task GreetUserAsync()
            => await RespondAsync(text: $":ping_pong: It took me {Context.Client.Latency}ms to respond to you!", ephemeral: true);

        [SlashCommand("info", "Bot information dump")]
        public async Task Info()
        {
            await DeferAsync();

            var application = await Context.Client.GetApplicationInfoAsync();
            await FollowupAsync(
                $"{Format.Bold("Info")}\n" +
                $"- Author: {application.Owner.Username} (ID {application.Owner.Id})\n" +
                $"- Library: Discord.Net ({DiscordConfig.Version})\n" +
                $"- Runtime: {RuntimeInformation.FrameworkDescription} {RuntimeInformation.OSArchitecture}\n" +
                $"- Uptime: {GetUptime()}\n\n" +

                $"{Format.Bold("Stats")}\n" +
                $"- Heap Size: {GetHeapSize()} MB\n" +
                $"- Guilds: {Context.Client.Guilds.Count}\n" +
                $"- Channels: {Context.Client.Guilds.Sum(g => g.Channels.Count)}" +
                $"- Users: {Context.Client.Guilds.Sum(g => g.Users.Count)}"
            );
        }


        private static string GetUptime()
                => (DateTime.Now - Process.GetCurrentProcess().StartTime).ToString(@"dd\.hh\:mm\:ss");
        private static string GetHeapSize() => Math.Round(GC.GetTotalMemory(true) / (1024.0 * 1024.0), 2).ToString();
    }
}