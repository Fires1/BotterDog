namespace BotterDog.Entities
{
    public class DogAccount
    {
        public ulong Id { get;  set; }
        public decimal Balance { get; set; } 

        public DogAccount(ulong id, decimal startingBalance)
        {
            Id = id;
            Balance = startingBalance;
        }

        public void ModifyBalance(decimal difference)
        {
            Balance = Balance + difference;
        }
    }
}
