namespace OpenEmpires
{
    public struct TributeCommand : ICommand
    {
        public CommandType Type => CommandType.Tribute;
        public int PlayerId { get; set; }
        public int RecipientPlayerId;
        public int ResourceType;
        public int Amount;

        public TributeCommand(int playerId, int recipientPlayerId, int resourceType, int amount)
        {
            PlayerId = playerId;
            RecipientPlayerId = recipientPlayerId;
            ResourceType = resourceType;
            Amount = amount;
        }
    }
}
