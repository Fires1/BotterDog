using System;
using System.Collections.Generic;

namespace BotterDog.Entities
{
    public class CustomState : IGamblingState
    {
        public Guid Id { get; set; }
        public ulong Creator { get; set; }
        public DateTime Started { get; set; }
        public ulong Guild { get; set; }
        public ulong Channel { get; set; }
        public ulong Message { get; set; }
        public GameState State { get; set; }
        public GameType GameType { get; set; }
        public List<Bet> Bets { get; set; }
        public decimal Bet { get; set; }
        public decimal Pot { get; set; }

        public List<string> Options { get; set; }
        public string Title { get; set; }
        public CustomDecider Decider { get; set; }
        public int Decided { get; set; }

        public CustomState(ulong Creator, GameType Type, decimal Bet)
        {
            this.Creator = Creator;
            GameType = Type;
            this.Bet = Bet;
            Pot = 0;
            Id = Guid.NewGuid();
            Options = new List<string>();
            Started = DateTime.Now;
            State = GameState.Betting;
            Bets = new List<Bet>();
        }
    }

    public enum CustomDecider
    {
        Self,
        TopperDog
    }
}
