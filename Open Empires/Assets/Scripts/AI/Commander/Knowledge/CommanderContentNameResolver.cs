using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenEmpires
{
    public enum CommanderContentResolutionStatus
    {
        Resolved,
        Ambiguous,
        Unknown
    }

    /// <summary>A detached interpretation result. Resolution is not an execution grant.</summary>
    public sealed class CommanderContentResolution<T> where T : class
    {
        public CommanderContentResolutionStatus Status { get; }
        public T Match { get; }
        public IReadOnlyList<T> Suggestions { get; }
        public bool IsAvailableForCivilization { get; }
        public bool IsExecutable { get; }

        internal CommanderContentResolution(CommanderContentResolutionStatus status, T match,
            IEnumerable<T> suggestions, bool available, bool executable)
        {
            Status = status;
            Match = match;
            Suggestions = Array.AsReadOnly((suggestions ?? Enumerable.Empty<T>()).ToArray());
            IsAvailableForCivilization = available;
            IsExecutable = executable;
        }
    }

    /// <summary>
    /// Resolves user/provider names against the detached game catalog. It never substitutes a
    /// civilization variant or turns discovered custom knowledge into gameplay authority.
    /// </summary>
    public static class CommanderContentNameResolver
    {
        public const int MaximumSuggestions = 5;
        private const int MaximumTypoDistance = 2;

        private static readonly Dictionary<string, Func<UnitKnowledge, bool>> UnitRoles =
            new Dictionary<string, Func<UnitKnowledge, bool>>(StringComparer.Ordinal)
        {
            ["cavalry"] = x => x.ProductionBuildingType == (int)BuildingType.Stables,
            ["mounted"] = x => x.ProductionBuildingType == (int)BuildingType.Stables,
            ["infantry"] = x => x.ProductionBuildingType == (int)BuildingType.Barracks,
            ["ranged"] = x => x.IsRanged,
            ["siege"] = x => x.ProductionBuildingType == (int)BuildingType.SiegeWorkshop,
            ["military"] = x => x.UnitType != 0 && x.UnitType != 5 && x.UnitType != UnitData.KingUnitType
        };

        public static CommanderContentResolution<UnitKnowledge> ResolveUnit(string text,
            GameKnowledgeCatalog catalog, Civilization civilization)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            var available = AvailableUnits(catalog, civilization);
            string key = Normalize(text);
            if (key.Length == 0) return UnitUnknown(Array.Empty<UnitKnowledge>());

            if (UnitRoles.TryGetValue(key, out Func<UnitKnowledge, bool> rolePredicate))
            {
                var roleMatches = catalog.Units.Where(rolePredicate)
                    .OrderBy(x => x.StableId, StringComparer.Ordinal).Take(MaximumSuggestions)
                    .Select(x => x.Copy()).ToArray();
                return new CommanderContentResolution<UnitKnowledge>(CommanderContentResolutionStatus.Ambiguous,
                    null, roleMatches, false, false);
            }

            var matches = catalog.Units.Where(x => Matches(key, x.StableId, x.DisplayName, x.Aliases))
                .GroupBy(x => x.StableId, StringComparer.Ordinal).Select(x => x.First()).ToList();
            if (matches.Count == 1)
            {
                UnitKnowledge unit = matches[0];
                bool isAvailable = available.Contains(unit.StableId);
                return new CommanderContentResolution<UnitKnowledge>(CommanderContentResolutionStatus.Resolved,
                    unit.Copy(), Array.Empty<UnitKnowledge>(), isAvailable,
                    CommanderIntentCatalog.IsSupportedUnit(unit.UnitType));
            }
            if (matches.Count > 1)
                return new CommanderContentResolution<UnitKnowledge>(CommanderContentResolutionStatus.Ambiguous,
                    null, matches.OrderBy(x => x.StableId, StringComparer.Ordinal).Take(MaximumSuggestions).Select(x => x.Copy()), false, false);

            var suggestions = FuzzySuggestions(catalog.Units, key, x => x.StableId, x => x.DisplayName, x => x.Aliases,
                x => x.Copy());
            return UnitUnknown(suggestions);
        }

        public static CommanderContentResolution<BuildingKnowledge> ResolveBuilding(string text,
            GameKnowledgeCatalog catalog, Civilization civilization)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            var available = AvailableBuildings(catalog, civilization);
            string key = Normalize(text);
            if (key.Length == 0) return BuildingUnknown(Array.Empty<BuildingKnowledge>());

            var matches = catalog.Buildings.Where(x => Matches(key, x.StableId, x.DisplayName, x.Aliases))
                .GroupBy(x => x.StableId, StringComparer.Ordinal).Select(x => x.First()).ToList();
            if (matches.Count == 1)
            {
                BuildingKnowledge building = matches[0];
                bool isAvailable = available.Contains(building.StableId);
                return new CommanderContentResolution<BuildingKnowledge>(CommanderContentResolutionStatus.Resolved,
                    building.Copy(), Array.Empty<BuildingKnowledge>(), isAvailable,
                    CommanderIntentCatalog.IsSupportedStructure(building.BuildingType));
            }
            if (matches.Count > 1)
                return new CommanderContentResolution<BuildingKnowledge>(CommanderContentResolutionStatus.Ambiguous,
                    null, matches.OrderBy(x => x.StableId, StringComparer.Ordinal).Take(MaximumSuggestions).Select(x => x.Copy()), false, false);

            var suggestions = FuzzySuggestions(catalog.Buildings, key, x => x.StableId, x => x.DisplayName, x => x.Aliases,
                x => x.Copy());
            return BuildingUnknown(suggestions);
        }

        private static HashSet<string> AvailableUnits(GameKnowledgeCatalog catalog, Civilization civ)
        {
            CivilizationKnowledge entry = catalog.Civilizations.FirstOrDefault(x => x.Civilization == civ);
            var available = new HashSet<string>(entry?.AvailableUnitIds ?? Array.Empty<string>(), StringComparer.Ordinal);
            foreach (UnitKnowledge unit in catalog.Units)
                if (!IsNativeUnit(unit.UnitType)) available.Remove(unit.StableId);
            return available;
        }

        private static HashSet<string> AvailableBuildings(GameKnowledgeCatalog catalog, Civilization civ)
        {
            CivilizationKnowledge entry = catalog.Civilizations.FirstOrDefault(x => x.Civilization == civ);
            var available = new HashSet<string>(entry?.AvailableBuildingIds ?? Array.Empty<string>(), StringComparer.Ordinal);
            foreach (BuildingKnowledge building in catalog.Buildings)
                if (!Enum.IsDefined(typeof(BuildingType), building.BuildingType)) available.Remove(building.StableId);
            return available;
        }

        private static bool IsNativeUnit(int type) => type >= 0 && type <= UnitData.KingUnitType && type != 5;

        private static bool Matches(string key, string id, string name, IEnumerable<string> aliases)
        {
            return key == Normalize(id) || key == Normalize(name) || aliases.Any(x => key == Normalize(x));
        }

        private static string Normalize(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;
            var chars = value.Trim().ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray();
            return new string(chars);
        }

        private static List<T> FuzzySuggestions<T>(IEnumerable<T> source, string key,
            Func<T, string> id, Func<T, string> name, Func<T, IEnumerable<string>> aliases, Func<T, T> copy)
            where T : class
        {
            return source.Select(x => new
                {
                    Item = x,
                    Distance = new[] { id(x), name(x) }.Concat(aliases(x))
                        .Select(candidate => EditDistance(key, Normalize(candidate))).Min()
                })
                .Where(x => x.Distance <= MaximumTypoDistance)
                .GroupBy(x => id(x.Item), StringComparer.Ordinal)
                .Select(x => x.OrderBy(y => y.Distance).First())
                .OrderBy(x => x.Distance).ThenBy(x => id(x.Item), StringComparer.Ordinal)
                .Take(MaximumSuggestions).Select(x => copy(x.Item)).ToList();
        }

        private static int EditDistance(string left, string right)
        {
            if (left.Length == 0) return right.Length;
            if (right.Length == 0) return left.Length;
            var previous = new int[right.Length + 1];
            var current = new int[right.Length + 1];
            for (int j = 0; j <= right.Length; j++) previous[j] = j;
            for (int i = 1; i <= left.Length; i++)
            {
                current[0] = i;
                for (int j = 1; j <= right.Length; j++)
                    current[j] = Math.Min(Math.Min(current[j - 1] + 1, previous[j] + 1),
                        previous[j - 1] + (left[i - 1] == right[j - 1] ? 0 : 1));
                var swap = previous; previous = current; current = swap;
            }
            return previous[right.Length];
        }

        private static CommanderContentResolution<UnitKnowledge> UnitUnknown(IEnumerable<UnitKnowledge> suggestions)
            => new CommanderContentResolution<UnitKnowledge>(CommanderContentResolutionStatus.Unknown, null, suggestions, false, false);
        private static CommanderContentResolution<BuildingKnowledge> BuildingUnknown(IEnumerable<BuildingKnowledge> suggestions)
            => new CommanderContentResolution<BuildingKnowledge>(CommanderContentResolutionStatus.Unknown, null, suggestions, false, false);
    }
}
