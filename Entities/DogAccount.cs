namespace BotterDog.Entities
{
    public class DogAccount
    {
        /// <summary>
        /// Discord ID
        /// </summary>
        public ulong Id { get;  set; } 

        /// <summary>
        /// Balance of bank account
        /// </summary>
        public decimal Balance { get; set; } 

        public DogAccount(ulong id, decimal startingBalance)
        {
            Id = id;
            Balance = startingBalance;
        }

        public void ModifyBalance(decimal difference)
        {
            //todo: make sure this doesnt go negative
            Balance += difference;
        }
    }
}
