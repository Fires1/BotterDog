namespace BotterDog.Entities
{
    public struct Card
    {
        public Suit Suit { get; set; }
        public CardNumber Number { get; set; }

        public Card(Suit suit, CardNumber number)
        {
            Suit = suit;
            Number = number;
        }

        public override string ToString() => $"{Number} of {Suit}";

        public string ToFileName()
        {
            string num = Number switch
            {
                CardNumber.Ace => "A",
                CardNumber.Jack => "J",
                CardNumber.Queen => "Q",
                CardNumber.King => "K",
                _ => ((int)Number).ToString(),
            };
            return $"{Suit.ToString().ToLower()}_{num}.png";
        } 
    }
}
