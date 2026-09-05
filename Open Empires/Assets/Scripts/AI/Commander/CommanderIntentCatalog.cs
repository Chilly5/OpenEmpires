using System;
using System.Collections.Generic;
using System.Text;

namespace OpenEmpires
{
    public static class CommanderIntentCatalog
    {
        public const int SpearmanUnitType = 1;
        public const int ArcherUnitType = 2;
        public const int KnightUnitType = 7;

        private static readonly Dictionary<string, int> UnitAliases = CreateUnitAliases();

        private static readonly Dictionary<string, BuildingType> StructureAliases =
            CreateStructureAliases();

        private static readonly Dictionary<string, ResourceType> ResourceAliases =
            CreateResourceAliases();

        public static bool TryResolveUnit(string text, out int unitType)
        {
            return UnitAliases.TryGetValue(NormalizeName(text), out unitType);
        }

        public static bool TryResolveStructure(string text, out BuildingType structureType)
        {
            return StructureAliases.TryGetValue(NormalizeName(text), out structureType);
        }

        public static bool TryResolveResource(string text, out ResourceType resourceType)
        {
            return ResourceAliases.TryGetValue(NormalizeName(text), out resourceType);
        }

        public static bool IsSupportedUnit(int unitType)
        {
            return unitType == SpearmanUnitType
                || unitType == ArcherUnitType
                || unitType == KnightUnitType;
        }

        public static bool IsSupportedStructure(BuildingType structureType)
        {
            return structureType == BuildingType.House
                || structureType == BuildingType.Barracks
                || structureType == BuildingType.ArcheryRange
                || structureType == BuildingType.Stables;
        }

        public static string GetUnitDisplayName(int unitType, bool plural = false)
        {
            switch (unitType)
            {
                case SpearmanUnitType: return plural ? "spearmen" : "Spearman";
                case ArcherUnitType: return plural ? "archers" : "Archer";
                case KnightUnitType: return plural ? "knights" : "Knight";
                default: return "unit " + unitType;
            }
        }

        public static string GetStructureDisplayName(BuildingType structureType)
        {
            switch (structureType)
            {
                case BuildingType.House: return "House";
                case BuildingType.Barracks: return "Barracks";
                case BuildingType.Stables: return "Stable";
                default: return structureType.ToString();
            }
        }

        private static Dictionary<string, int> CreateUnitAliases()
        {
            var aliases = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            aliases[NormalizeName(KeybindManager.GetUnitTypeDisplayName(SpearmanUnitType))]
                = SpearmanUnitType;
            aliases[NormalizeName(KeybindManager.GetUnitTypeDisplayName(ArcherUnitType))]
                = ArcherUnitType;
            aliases[NormalizeName(KeybindManager.GetUnitTypeDisplayName(KnightUnitType))]
                = KnightUnitType;
            aliases["spearmen"] = SpearmanUnitType;
            aliases["archers"] = ArcherUnitType;
            aliases["knights"] = KnightUnitType;
            return aliases;
        }

        private static Dictionary<string, BuildingType> CreateStructureAliases()
        {
            var aliases = new Dictionary<string, BuildingType>(StringComparer.OrdinalIgnoreCase);
            aliases[NormalizeName(BuildingType.House.ToString())] = BuildingType.House;
            aliases[NormalizeName(BuildingType.Barracks.ToString())] = BuildingType.Barracks;
            aliases[NormalizeName(BuildingType.Stables.ToString())] = BuildingType.Stables;
            aliases["houses"] = BuildingType.House;
            aliases["barrack"] = BuildingType.Barracks;
            aliases["stable"] = BuildingType.Stables;
            aliases["archery range"] = BuildingType.ArcheryRange;
            aliases["archery ranges"] = BuildingType.ArcheryRange;
            return aliases;
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
