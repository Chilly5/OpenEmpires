using System;
using System.Collections.Generic;
using System.Text;

namespace OpenEmpires
{
    public static class CommanderIntentCatalog
    {
        public const int VillagerUnitType = 0;
        public const int SpearmanUnitType = 1;
        public const int ArcherUnitType = 2;
        public const int ScoutUnitType = 4;
        public const int KnightUnitType = 7;

        private static readonly Dictionary<string, ResourceType> ResourceAliases =
            CreateResourceAliases();

        public static bool TryResolveUnit(string text, out int unitType)
        {
            var result = CommanderContentNameResolver.ResolveUnit(text,
                GameKnowledgeCatalog.BuildCanonicalIdentityCatalog(), Civilization.English);
            unitType = result.Match?.UnitType ?? -1;
            return result.Status == CommanderContentResolutionStatus.Resolved
                && IsSupportedUnit(unitType);
        }

        public static bool TryResolveStructure(string text, out BuildingType structureType)
        {
            var result = CommanderContentNameResolver.ResolveBuilding(text,
                GameKnowledgeCatalog.BuildCanonicalIdentityCatalog(), Civilization.English);
            structureType = result.Match?.BuildingType ?? default;
            return result.Status == CommanderContentResolutionStatus.Resolved
                && IsSupportedStructure(structureType);
        }

        public static bool TryResolveResource(string text, out ResourceType resourceType)
        {
            return ResourceAliases.TryGetValue(NormalizeName(text), out resourceType);
        }

        public static bool IsSupportedUnit(int unitType)
        {
            return GameKnowledgeCatalog.BuildCanonicalIdentityCatalog().FindUnitByType(unitType) != null
                && GameSimulation.TryGetCanonicalProductionBuildingType(unitType, out _);
        }

        public static bool IsSupportedStructure(BuildingType structureType)
        {
            return Enum.IsDefined(typeof(BuildingType), structureType)
                && structureType != BuildingType.Wall
                && structureType != BuildingType.StoneWall
                && structureType != BuildingType.StoneGate
                && structureType != BuildingType.WoodGate
                && structureType != BuildingType.Landmark
                && structureType != BuildingType.Wonder;
        }

        public static string GetUnitDisplayName(int unitType, bool plural = false)
        {
            string name = plural ? UnitInfoUI.GetUnitTypePluralName(unitType) : UnitInfoUI.GetUnitTypeDisplayName(unitType);
            return string.IsNullOrEmpty(name) || name == "Unit" || name == "Units" ? "unit " + unitType : name;
        }

        public static string GetStructureDisplayName(BuildingType structureType)
        {
            string name = UnitInfoUI.GetBuildingTypeDisplayName(structureType);
            return string.IsNullOrEmpty(name) || name == "Building" ? structureType.ToString() : name;
        }

        private static Dictionary<string, ResourceType> CreateResourceAliases()
        {
            var aliases = new Dictionary<string, ResourceType>(StringComparer.OrdinalIgnoreCase);
            foreach (ResourceType resource in Enum.GetValues(typeof(ResourceType)))
                aliases[NormalizeName(resource.ToString())] = resource;
            return aliases;
        }

        internal static string NormalizeName(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;
            var builder = new StringBuilder(value.Length);
            bool previousWasSpace = false;
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (char.IsLetter(c))
                {
                    builder.Append(char.ToLowerInvariant(c));
                    previousWasSpace = false;
                }
                else if (char.IsWhiteSpace(c) && builder.Length > 0 && !previousWasSpace)
                {
                    builder.Append(' ');
                    previousWasSpace = true;
                }
            }
            return builder.ToString().Trim();
        }
    }
}
