using System;
using System.Collections.Generic;
using System.Text;

namespace BotterDog.Entities
{
    public class GamblingState
    {
        public Guid Id { get; set; }
        public ulong Creator { get; set; }
        public DateTime Started { get; set; }
        public GameType GameType { get; set; }

        public ulong Guild { get; set; }
        public ulong Channel { get; set; }
        public ulong Message { get; set; }

        public GameState State { get; set; }
        public List<Bet> Bets { get; set; }
        public decimal Bet { get; set; }

        public GamblingState(ulong Creator, GameType Type, decimal Bet)
        {
            this.Creator = Creator;
            GameType = Type;
            this.Bet = Bet;
            Id = Guid.NewGuid();
            Started = DateTime.Now; 
            State = GameState.Betting;
            Bets = new List<Bet>();
        }
    }

    public struct Bet
    {
        public ulong Better { get; set; }
        public string DisplayName { get; set; }
        public decimal Amount { get; set; }
        public decimal Odds { get; set; }
        public int[] Hits { get; set; }

        public Bet(ulong Better, string DisplayName, decimal Amount, decimal Odds, int[] Hits)
        {
            this.Better = Better;
            this.DisplayName = DisplayName;
            this.Odds = Odds;
            this.Amount = Amount;
            this.Hits = Hits;
        }
    }

    public enum GameType
    {
        Roulette
    }

    public enum GameState
    {
        Betting,
        Playing,
        PendingPayout,
        Finished
    }
}
