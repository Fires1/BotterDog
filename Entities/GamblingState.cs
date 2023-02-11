using System;
using System.Collections.Generic;
using System.Text;

namespace BotterDog.Entities
{
    public class GamblingState
    {
        /// <summary>
        /// Unique GUID assigned to game upon creation
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// Discord ID of the starting user
        /// </summary>
        public ulong Creator { get; set; }

        /// <summary>
        /// Date & time game started
        /// </summary>
        public DateTime Started { get; set; }

        /// <summary>
        /// Original response guild
        /// </summary>
        public ulong Guild { get; set; }
        /// <summary>
        /// Original response channel
        /// </summary>
        public ulong Channel { get; set; }
        /// <summary>
        /// Original response mssage
        /// </summary>
        public ulong Message { get; set; }

        /// <summary>
        /// Current state of the game
        /// </summary>
        public GameState State { get; set; }

        /// <summary>
        /// Type of game for determing payout
        /// </summary>
        public GameType GameType { get; set; }

        /// <summary>
        /// List of logged bets
        /// </summary>
        public List<Bet> Bets { get; set; }

        /// <summary>
        /// Bet amount for game
        /// </summary>
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
}
