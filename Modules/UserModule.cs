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

        /*[SlashCommand("roles", "Select Roles")]
        public async Task ChooseAsync()
        {
            await DeferAsync();

            var user = Context.User as IGuildUser;
            if (user.Guild == null)
            {
                await FollowupAsync(text: $"No go, must be used in the server.", ephemeral: true);
            }

            var menuBuilder = new SelectMenuBuilder()
                .WithPlaceholder("Select Roles")
                .WithCustomId("roles-add")
                .WithMinValues(1)
                .WithMaxValues(7)
                .AddOption("Roleplay Stuff (FiveM)", "825449572142809099", "Various bullshit for roleplay related items.")
                .AddOption("Source Engine", "825449640223703070", "Source 2, S&box, Garry's Mod.")
                .AddOption("Fuck Craxy", "825449680514711615", "Fuck Craxy.")
                .AddOption("Slapshot", "811325623103914024", "Slapshot Rebound, free 3v3 hockey game on Steam.")
                .AddOption("Racing Games", "843021320145076224", "Anything racism.")
                .AddOption("Fires' Project Updates", "943834038630764574", "Anything I'm personally working on.");

            var builder = new ComponentBuilder()
                .WithSelectMenu(menuBuilder);

            await FollowupAsync("Use the selector below to pick roles:", components: builder.Build(), ephemeral: true);
        }

        [SlashCommand("removeroles", "Remove Roles")]
        public async Task RemoveAsync()
        {
            await DeferAsync();

            var user = Context.User as IGuildUser;
            if (user.Guild == null)
            {
                await FollowupAsync(text: $"No go, must be used in the server.", ephemeral: true);
            }

            var menuBuilder = new SelectMenuBuilder()
                .WithPlaceholder("Select Roles To Remove")
                .WithCustomId("roles-rem")
                .WithMinValues(1)
                .WithMaxValues(7)
                .AddOption("Roleplay Stuff (FiveM)", "825449572142809099", "Various bullshit for roleplay related items.")
                .AddOption("Source Engine", "825449640223703070", "Source 2, S&box, Garry's Mod.")
                .AddOption("Fuck Craxy", "825449680514711615", "Fuck Craxy.")
                .AddOption("Slapshot", "811325623103914024", "Slapshot Rebound, free 3v3 hockey game on Steam.")
                .AddOption("Racing Games", "843021320145076224", "Anything racism.")
                .AddOption("Fires' Project Updates", "943834038630764574", "Anything I'm personally working on.");

            var builder = new ComponentBuilder()
                .WithSelectMenu(menuBuilder);

            await FollowupAsync("I'm too lazy to make this not show an entire list of all the roles, so figure out which ones you have lol. Use the selector below to pick roles.", components: builder.Build(), ephemeral: true);
        }

        /*[SlashCommand("craxy", "Just fuck him already")]
        public async Task FuckCraxy()
        {
            await DeferAsync();

            if(!_dicklimit.IsValid(Context.User.Id))
            {
                await FollowupAsync("You're dick is still recovering from the last fuckening. Try again later.", ephemeral: true);
                return;
            }

            var builder = new ComponentBuilder()
                 .WithButton("Fuck Craxy", "fuck-craxy", ButtonStyle.Danger, new Emoji("\uD83C\uDF46"));

            await FollowupAsync("Wear a condom...", components: builder.Build());
        }

        [MessageCommand("Fuck this person")]
        public async Task FuckPersonMessage(IMessage message)
        {
            await DeferAsync();

            if (!_dicklimit.IsValid(Context.User.Id))
            {
                await FollowupAsync("You're dick is still recovering from the last fuckening. Try again later.", ephemeral: true);
                return;
            }

            var target = (message.Author as IGuildUser);
            var name = target.Nickname ?? target.Username;

            var builder = new ComponentBuilder()
                 .WithButton($"Fuck {name}", $"fuckp-{message.Author.Id}", ButtonStyle.Danger, new Emoji("\uD83C\uDF46"));

            await FollowupAsync("Wear a condom...", components: builder.Build());
        }


        [UserCommand("Fuck this person")]
        public async Task FuckPersonUser(IUser user)
        {
            await DeferAsync();

            if (!_dicklimit.IsValid(Context.User.Id))
            {
                await FollowupAsync("You're dick is still recovering from the last fuckening. Try again later.", ephemeral: true);
                return;
            }

            var target = (user as IGuildUser);
            var name = target.Nickname ?? target.Username;

            var builder = new ComponentBuilder()
                 .WithButton($"Fuck {name}", $"fuckp-{user.Id}", ButtonStyle.Danger, new Emoji("\uD83C\uDF46"));

            await FollowupAsync("Wear a condom...", components: builder.Build());
        }

        [MessageCommand("Fuck Craxy")]
        public async Task FuckCraxyMessage(IMessage message)
        {
            await DeferAsync();

            if (!_dicklimit.IsValid(Context.User.Id))
            {
                await FollowupAsync("You're dick is still recovering from the last fuckening. Try again later.", ephemeral: true);
                return;
            }

            var builder = new ComponentBuilder()
                 .WithButton("Fuck Craxy", "fuck-craxy", ButtonStyle.Danger, new Emoji("\uD83C\uDF46"));

            await FollowupAsync("Wear a condom...", components: builder.Build());
        }

        [UserCommand("Fuck Craxy")]
        public async Task FuckCraxyUser(IUser user)
        {
            await DeferAsync();

            if (!_dicklimit.IsValid(Context.User.Id))
            {
                await FollowupAsync("You're dick is still recovering from the last fuckening. Try again later.", ephemeral: true);
                return;
            }


            var builder = new ComponentBuilder()
                 .WithButton("Fuck Craxy", "fuck-craxy", ButtonStyle.Danger, new Emoji("\uD83C\uDF46"));

            await FollowupAsync("Wear a condom...", components: builder.Build());
        }*/

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