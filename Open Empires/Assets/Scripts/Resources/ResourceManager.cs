using System.Collections.Generic;

namespace OpenEmpires
{
    public class ResourceManager
    {
        private Dictionary<int, PlayerResources> playerResources = new Dictionary<int, PlayerResources>();

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
