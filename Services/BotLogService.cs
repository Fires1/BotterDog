using System;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;

namespace FiresStuff.Services
{
    public class BotLogService
    {
        private readonly DiscordSocketClient _client;
        private readonly IServiceProvider _services;
        private readonly ulong _logGuild = 752755222505586739;
        private readonly ulong _logChannel = 946249520696664104;

        public BotLogService(DiscordSocketClient client, IServiceProvider services)
        {
            _client = client;
            _services = services;
        }

        /// <summary>
        /// Logs utilizing the bot, inside a specific channel within my server.
        /// </summary>
        /// <param name="message">Content to log</param>
        public async Task BotLogAsync(BotLogSeverity severity, string title, string message, bool pingSam = false,  params string[] other)
        {
            var guild = _client.GetGuild(_logGuild);
            var channel = guild.GetTextChannel(_logChannel);

            Color color = Color.Blue;
            switch(severity)
            {
                case BotLogSeverity.Good:
                    color = Color.Green;
                    break;
                case BotLogSeverity.Meh:
                    color = Color.LightOrange;
                    break;
                case BotLogSeverity.Bad:
                    color = Color.Red;
                    break;
            }

            var embed = new EmbedBuilder()
                .WithTitle(title)
                .WithDescription(message + $"\r\n{string.Join(" ", other)}")
                .WithColor(color)
                .WithCurrentTimestamp();

            if (pingSam)
            {
                await channel.SendMessageAsync("<@131182268021604352>", embed: embed.Build());
            }
            else
            {
                await channel.SendMessageAsync(embed: embed.Build());
            }
        }
    }

    public enum BotLogSeverity
    {
        Good,
        Meh,
        Bad
    }
}