namespace BotterDog.Entities
{
    public class HiLoPlayer
    {
        public DogAccount Account { get; set; }
        public HiLoPlayerStatus Status { get; set; }
        public decimal Multiplier { get; set; }
        public bool HasPaidOut { get; set; }
        public decimal? Payout { get; set; }

        public ulong Id => Account.Id;

        public HiLoPlayer(DogAccount account)
        {
            Account = account;
            Status = HiLoPlayerStatus.Waiting;
            Multiplier = 1.0m;
            HasPaidOut = false;
            Payout = null;
        }
    }
}
