using System;
using System.Linq;
using System.Collections.Generic;

namespace BotterDog.Entities
{
    public class HiLoState : IGamblingState
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

        public string CreatorsDisplayName { get; set; } //Used for updating embed
        public Deck Deck { get; set; } //Our instance of a deck of cards
        public Card CurrentCard { get; set; } //Current card pulled
        public int CurrentRound { get;set; } //Current round we are on
        public bool TimerStarted { get; set; } //Have we started our time-out timer?

        //public Dictionary<ulong, decimal> CurrentMultipliers { get; set; } //Current payout odds
        //public List<ulong> CurrentPlayers { get; set; } //Current players
        //public List<ulong> BustedPlayers { get; set; } //Players who have busted
        //public List<ulong> QuitPlayers { get; set; } //Players who have quit
        //public Dictionary<ulong, decimal> PaidOut { get; set; } //Players who have paid out and their amount paid out

        public List<HiLoPlayer> Players { get; set; }

        public Card NextRound()
        {
            //Cache old card
            var oldCard = CurrentCard;
            CurrentCard = Deck.TakeCard();

            //Find all bets for this round
            var betsThisRound = Bets.Where(x => x.Hits[0] == CurrentRound).ToList();

            foreach(var bet in betsThisRound)
            {
                var player = Players.FirstOrDefault(x => bet.Better == x.Id);
                //If bet is low, then hit
                if (bet.Hits[1] == 0 && CurrentCard.Number < oldCard.Number)
                {
                    player.Multiplier = decimal.Round(player.Multiplier * bet.Odds, 2);
                    player.Status = HiLoPlayerStatus.Waiting;
                    continue;
                }
                //If bet is high, then hit
                if (bet.Hits[1] == 1 && CurrentCard.Number > oldCard.Number)
                {
                    player.Multiplier = decimal.Round(player.Multiplier * bet.Odds, 2);
                    player.Status = HiLoPlayerStatus.Waiting;
                    continue;
                }
                //If no hit, then bust the player.
                player.Multiplier = 0;
                player.Status = HiLoPlayerStatus.Bust;
            }
            //New round
            CurrentRound += 1;
            return CurrentCard;
        }

        //Used to skip whenever 'obvious' bets are found.
        public Card SkipRound()
        {
            CurrentRound += 1;
            CurrentCard = Deck.TakeCard();
            return CurrentCard;
        }

        public void Initialize()
        {
            Deck.Shuffle();
            //Find an acceptable card that doesn't have an 'obvious' bet.
            var okCards = Deck.Cards.Where(x => x.Number != CardNumber.Ace && x.Number != CardNumber.King);
            CurrentCard = okCards.FirstOrDefault();
            Deck.Cards.Remove(CurrentCard);
        }

        public HiLoState(ulong Creator, GameType Type, decimal Bet)
        {
            this.Creator = Creator;
            GameType = Type;
            this.Bet = Bet;
            Pot = 0;
            Id = Guid.NewGuid();
            Started = DateTime.Now;
            State = GameState.Betting;
            Bets = new List<Bet>();

            //CurrentMultipliers = new Dictionary<ulong, decimal>();
            //CurrentPlayers = new List<ulong>();
            //BustedPlayers = new List<ulong>();
            //QuitPlayers = new List<ulong>();
            //PaidOut = new Dictionary<ulong, decimal>();
            Players = new List<HiLoPlayer>();
            TimerStarted = false;
            Deck = new Deck();
            Initialize();
            CurrentRound = 1;
        }
    }
}
