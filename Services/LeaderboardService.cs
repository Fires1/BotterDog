using BotterDog.Entities;
using CSharpFunctionalExtensions;
using Discord.WebSocket;
using FiresStuff.Services;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace BotterDog.Services
{
    public class LeaderboardService
    {
        public List<LeaderboardEntry> Records { get; set; }
        private readonly DiscordSocketClient _client;
        private readonly BotLogService _botLog;

        public LeaderboardService(DiscordSocketClient client, AccountService accounts, BotLogService botlog)
        {
            _client = client;
            _botLog = botlog;
            Records = new List<LeaderboardEntry>();
        }

        public void Log(LeaderboardEntry record)
        {
            Records.Add(record);
            Save();
        }

        /*public Dictionary<LeaderboardEntry, decimal> GetTop()
        {
        //    return Records.OrderByDescending(x => x.Amount).Take(10).ToList();
        }*/

        public Result Load()
        {
            try
            {
                Records = JsonConvert.DeserializeObject<List<LeaderboardEntry>>(File.ReadAllText("leaderboard.json"));
                _botLog.BotLogAsync(BotLogSeverity.Good, "Leaderboard loaded", "Leaderboard loaded successfully.");
                return Result.Success();
            }
            catch (Exception e)
            {
                _botLog.BotLogAsync(BotLogSeverity.Bad, "Leaderboard Load failure", "Failure while loading Leaderboard occured:", true, e.Message);
                return Result.Failure(e.Message);
            }
        }

        public Result Save(bool silent = true)
        {
            try
            {
                File.WriteAllText("leaderboard.json", JsonConvert.SerializeObject(Records));
                if (!silent)
                {
                    _botLog.BotLogAsync(BotLogSeverity.Good, "Leaderboard saved", "Pot saved succesfuly.");
                }
                return Result.Success();
            }
            catch (Exception e)
            {
                _botLog.BotLogAsync(BotLogSeverity.Bad, "Leaderboard Save failure", "Failure while saving Leaderboard occured:", true, e.Message);
                return Result.Failure(e.Message);
            }
        }
    }

    public struct LeaderboardEntry
    {
        public ulong Id { get; set; }
        public decimal Amount { get; set; }
        public GameType Game { get; set; }
    }
}
