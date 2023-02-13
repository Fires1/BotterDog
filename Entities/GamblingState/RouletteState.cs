using System;
using System.Collections.Generic;

namespace BotterDog.Entities
{
    public class RouletteState : IGamblingState
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

        public RouletteState(ulong Creator, GameType Type, decimal Bet)
        {
            this.Creator = Creator;
            GameType = Type;
            this.Bet = Bet;
            Pot = 0;
            Id = Guid.NewGuid();
            Started = DateTime.Now; 
            State = GameState.Betting;
            Bets = new List<Bet>();
        }
    }
}
