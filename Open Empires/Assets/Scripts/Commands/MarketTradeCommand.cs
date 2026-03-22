namespace OpenEmpires
{
    public struct MarketTradeCommand : ICommand
    {
        public CommandType Type => CommandType.MarketTrade;
        public int PlayerId { get; set; }
        public int TradeResource; // ResourceType cast to int (0=Food, 2=Gold, 3=Stone — Gold excluded as currency)
        public bool IsBuying; // true = buy resource with gold, false = sell resource for gold

        public MarketTradeCommand(int playerId, int tradeResource, bool isBuying)
        {
            PlayerId = playerId;
            TradeResource = tradeResource;
            IsBuying = isBuying;
        }
    }
}
