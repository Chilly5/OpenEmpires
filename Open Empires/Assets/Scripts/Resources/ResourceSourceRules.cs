namespace OpenEmpires
{
    // Projections over current canonical registrations. No costs/content database or execution authority.
    public static class ResourceSourceRules
    {
        public static bool IsDefined(ResourceSourceKind kind) => kind >= ResourceSourceKind.Any && kind <= ResourceSourceKind.StoneMine;

        public static bool MatchesOwnedSource(ResourceNodeData node, ResourceSourceKind kind, int playerId, BuildingRegistry buildings)
        {
            if (!IsDefined(kind) || !Matches(node, kind)) return false;
            if (kind == ResourceSourceKind.Any || !node.IsFarmNode) return true;
            var farm = buildings.GetBuilding(node.LinkedBuildingId);
            return farm != null && farm.PlayerId == playerId && farm.Type == BuildingType.Farm
                && !farm.IsUnderConstruction && !farm.IsDestroyed;
        }

        public static bool Matches(ResourceNodeData node, ResourceSourceKind kind)
        {
            if (node == null) return false;
            switch (kind)
            {
                case ResourceSourceKind.Any: return true;
                case ResourceSourceKind.Sheep: return node.Type == ResourceType.Food && node.IsCarcass;
                case ResourceSourceKind.Berries: return node.Type == ResourceType.Food && !node.IsCarcass && !node.IsFarmNode;
                case ResourceSourceKind.Farm: return node.Type == ResourceType.Food && node.IsFarmNode;
                case ResourceSourceKind.Tree: return node.Type == ResourceType.Wood;
                case ResourceSourceKind.GoldMine: return node.Type == ResourceType.Gold;
                case ResourceSourceKind.StoneMine: return node.Type == ResourceType.Stone;
                default: return false;
            }
        }
        public static bool IsCompatible(ResourceType resource, ResourceSourceKind kind)
        {
            switch (kind)
            {
                case ResourceSourceKind.Any: return true;
                case ResourceSourceKind.Sheep:
                case ResourceSourceKind.Berries:
                case ResourceSourceKind.Farm: return resource == ResourceType.Food;
                case ResourceSourceKind.Tree: return resource == ResourceType.Wood;
                case ResourceSourceKind.GoldMine: return resource == ResourceType.Gold;
                case ResourceSourceKind.StoneMine: return resource == ResourceType.Stone;
                default: return false;
            }
        }
    }
}
