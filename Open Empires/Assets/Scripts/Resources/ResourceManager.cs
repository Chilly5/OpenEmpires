using System.Collections.Generic;

namespace OpenEmpires
{
    public class ResourceManager
    {
        private Dictionary<int, PlayerResources> playerResources = new Dictionary<int, PlayerResources>();
        private readonly Dictionary<int, Dictionary<ResourceType, long>> gatheredIncome =
            new Dictionary<int, Dictionary<ResourceType, long>>();

        // Credits only at actual gathered-resource delivery points. Ordinary stockpile
        // changes (starting resources, costs, refunds, cheats, and transfers) stay separate.
        public long GetGatheredIncome(int playerId, ResourceType type)
        {
            return gatheredIncome.TryGetValue(playerId, out var byType)
                && byType.TryGetValue(type, out long amount) ? amount : 0L;
        }

        internal void CreditGatheredIncome(int playerId, ResourceType type, int amount)
        {
            if (amount <= 0) return;
            if (!gatheredIncome.TryGetValue(playerId, out var byType))
            {
                byType = new Dictionary<ResourceType, long>();
                gatheredIncome.Add(playerId, byType);
            }
            byType.TryGetValue(type, out long current);
            byType[type] = checked(current + amount);
            AddResource(playerId, type, amount);
        }

        public PlayerResources GetPlayerResources(int playerId)
        {
            if (!playerResources.TryGetValue(playerId, out var resources))
            {
                resources = new PlayerResources();
                playerResources[playerId] = resources;
            }
            return resources;
        }

        public void AddResource(int playerId, ResourceType type, int amount)
        {
            var resources = GetPlayerResources(playerId);
            switch (type)
            {
                case ResourceType.Food: resources.Food += amount; break;
                case ResourceType.Wood: resources.Wood += amount; break;
                case ResourceType.Gold: resources.Gold += amount; break;
                case ResourceType.Stone: resources.Stone += amount; break;
            }
        }
    }

    public class PlayerResources
    {
        public int Food;
        public int Wood;
        public int Gold;
        public int Stone;
    }
}
