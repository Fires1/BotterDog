namespace BotterDog.Entities
{
    public class Bet
    {
        /// <summary>
        /// Discord ID of person placing the bet
        /// </summary>
        public ulong Better { get; set; }

        /// <summary>
        /// Display name of better for return 
        /// </summary>
        public string DisplayName { get; set; }

        /// <summary>
        /// Amount bet
        /// </summary>
        public decimal Amount { get; set; }

        /// <summary>
        /// Multiplied by amount to determine payout
        /// </summary>
        public decimal Odds { get; set; }

        /// <summary>
        /// 'Hit' numbers that determine if payout is multiplied by odds
        /// </summary>
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
}
