using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace OpenEmpires
{
    // Retrieval only: lexical similarity never chooses an entity or authorizes an effect.
    internal static class CommanderKnowledgeRetrieval
    {
        // Required references are independent of optional lexical suggestions. The finite
        // ceiling is a refusal boundary, never permission to omit the seventeenth reference.
        internal const int MaximumRequiredEntries = 16;
        private const int MaximumOptionalInterpretations = 2;
        private const int MaximumOptionalEntries = 2;
        internal static string Retrieve(GameKnowledgeCatalog catalog, string input, Civilization civilization,
            string boundedPending = null)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            var required = new HashSet<string>(StringComparer.Ordinal);
            var matchedPhrases = new HashSet<string>(StringComparer.Ordinal);
            var resolutions = new List<JObject>();
            var resolvedNames = new List<JObject>();
            var resolvedNameIds = new HashSet<string>(StringComparer.Ordinal);
            var optionalInterpretations = new List<JObject>();
            var optional = new HashSet<string>(StringComparer.Ordinal);
            foreach (string source in new[] { input ?? string.Empty, boundedPending ?? string.Empty })
            {
                // Both sources already have independent caller bounds (1024 player, 4096 pending).
                // Scan the complete bounded strings; prefix/word/phrase cuts lose tail facts.
                string searchable = " " + string.Join(" ", Regex.Matches(source.ToLowerInvariant(), @"[\p{L}\p{N}]+")
                    .Cast<Match>().Select(x => x.Value)) + " ";
                foreach (var unit in catalog.Units)
                    MatchAliases(required, matchedPhrases, unit.StableId, unit.DisplayName, unit.Aliases, searchable);
                foreach (var building in catalog.Buildings)
                    MatchAliases(required, matchedPhrases, building.StableId, building.DisplayName, building.Aliases, searchable);
                // Role words such as "cavalry" are resolver-defined ambiguous names, not
                // a new local name table. Deduplication bounds resolver work for long replies.
                foreach (string word in Regex.Matches(source.ToLowerInvariant(), @"[\p{L}\p{N}]+")
                    .Cast<Match>().Select(x => x.Value).Distinct(StringComparer.Ordinal))
                    if (word.Length >= 3) matchedPhrases.Add(word);
            }
            foreach (string phrase in matchedPhrases.OrderBy(x => x, StringComparer.Ordinal))
            {
                var unit = CommanderContentNameResolver.ResolveUnit(phrase, catalog, civilization);
                var building = CommanderContentNameResolver.ResolveBuilding(phrase, catalog, civilization);
                if (unit.Status == CommanderContentResolutionStatus.Ambiguous
                    || building.Status == CommanderContentResolutionStatus.Ambiguous)
                {
                    foreach (var candidate in unit.Suggestions) required.Add(candidate.StableId);
                    foreach (var candidate in building.Suggestions) required.Add(candidate.StableId);
                    resolutions.Add(new JObject { ["phrase"] = phrase, ["interpretationOnly"] = true,
                        ["unitCandidates"] = new JArray(unit.Suggestions.Select(x => x.StableId)),
                        ["buildingCandidates"] = new JArray(building.Suggestions.Select(x => x.StableId)),
                        ["ambiguous"] = true });
                    continue;
                }
                if (unit.Status == CommanderContentResolutionStatus.Resolved)
                {
                    required.Add(unit.Match.StableId);
                    if (resolvedNameIds.Add(unit.Match.StableId)) resolvedNames.Add(new JObject {
                        ["phrase"] = phrase, ["category"] = "Unit", ["id"] = unit.Match.StableId,
                        ["canonicalToken"] = unit.Match.DisplayName });
                }
                if (building.Status == CommanderContentResolutionStatus.Resolved)
                {
                    required.Add(building.Match.StableId);
                    if (resolvedNameIds.Add(building.Match.StableId)) resolvedNames.Add(new JObject {
                        ["phrase"] = phrase, ["category"] = "Building", ["id"] = building.Match.StableId,
                        ["canonicalToken"] = building.Match.Landmark.HasValue ? building.Match.DisplayName : building.Match.BuildingType.ToString() });
                }
                // Only a narrow, resolver-defined near-name can become an optional
                // interpretation. Common short words never fill the catalog with fuzz.
                if (phrase.Length < 7 || unit.Status != CommanderContentResolutionStatus.Unknown
                    || building.Status != CommanderContentResolutionStatus.Unknown) continue;
                var unitCandidates = unit.Suggestions.Take(MaximumOptionalEntries).Select(x => x.StableId).ToArray();
                var buildingCandidates = building.Suggestions.Take(MaximumOptionalEntries).Select(x => x.StableId).ToArray();
                if (unitCandidates.Length + buildingCandidates.Length == 0
                    || unitCandidates.Length + buildingCandidates.Length > MaximumOptionalEntries) continue;
                optionalInterpretations.Add(new JObject { ["phrase"] = phrase, ["interpretationOnly"] = true,
                    ["unitCandidates"] = new JArray(unitCandidates),
                    ["buildingCandidates"] = new JArray(buildingCandidates), ["ambiguous"] = false });
            }
            // A requested native unit must retain its producer and civilization replacement
            // facts even when neither name occurred literally in this turn.
            foreach (string id in required.ToArray())
            {
                var unit = catalog.FindUnit(id);
                if (unit == null) continue;
                int resolvedType = GameSimulation.ResolveCanonicalCivilizationUnitType(civilization, unit.UnitType);
                var replacement = catalog.FindUnitByType(resolvedType);
                if (replacement != null) required.Add(replacement.StableId);
                if (unit.ProductionBuildingType >= 0)
                    required.Add("building:" + (BuildingType)unit.ProductionBuildingType);
                if (replacement?.ProductionBuildingType >= 0)
                    required.Add("building:" + (BuildingType)replacement.ProductionBuildingType);
                if (unit.UnitType == UnitData.KingUnitType)
                    foreach (var landmark in catalog.Buildings.Where(b => b.Landmark.HasValue
                        && LandmarkDefinitions.Get(b.Landmark.Value).SpawnsKingOnCompletion))
                        required.Add(landmark.StableId);
            }
            bool overflow = required.Count > MaximumRequiredEntries || resolutions.Count > 6;
            int addedHints = 0;
            if (!overflow)
                foreach (JObject hint in optionalInterpretations)
                {
                    if (resolutions.Count >= 6 || addedHints >= MaximumOptionalInterpretations) break;
                    var candidates = ((JArray)hint["unitCandidates"]).Concat((JArray)hint["buildingCandidates"])
                        .Select(x => (string)x).Where(id => !required.Contains(id)).Distinct(StringComparer.Ordinal).ToArray();
                    if (optional.Count + candidates.Count(id => !optional.Contains(id)) > MaximumOptionalEntries) continue;
                    foreach (string id in candidates) optional.Add(id);
                    resolutions.Add(hint);
                    addedHints++;
                }
            var requiredIds = overflow ? Array.Empty<string>() : required.OrderBy(x => x, StringComparer.Ordinal).ToArray();
            var optionalIds = overflow ? Array.Empty<string>() : optional.OrderBy(x => x, StringComparer.Ordinal).ToArray();
            var chosen = requiredIds.Concat(optionalIds).ToArray();
            var units = new JArray(); var buildings = new JArray();
            foreach (string id in chosen)
            {
                if (id.StartsWith("unit:", StringComparison.Ordinal))
                {
                    var data = catalog.FindUnit(id);
                    var resolution = CommanderContentNameResolver.ResolveUnit(data.StableId, catalog, civilization);
                    units.Add(new JObject { ["id"] = data.StableId, ["name"] = data.DisplayName,
                        ["age"] = data.RequiredAge, ["producer"] = data.ProductionBuildingType < 0
                            ? null : ((BuildingType)data.ProductionBuildingType).ToString(),
                        ["cost"] = Cost(data.Cost), ["availableForCivilization"] = resolution.IsAvailableForCivilization,
                        ["commanderExecutable"] = resolution.IsExecutable,
                        ["civilizationResolvedId"] = "unit:" + GameSimulation.ResolveCanonicalCivilizationUnitType(civilization, data.UnitType),
                        ["contentExists"] = true });
                }
                else
                {
                    var data = catalog.FindBuilding(id);
                    var resolution = CommanderContentNameResolver.ResolveBuilding(data.StableId, catalog, civilization);
                    buildings.Add(new JObject { ["id"] = data.StableId, ["name"] = data.DisplayName,
                        ["age"] = data.RequiredAge, ["cost"] = Cost(data.Cost),
                        ["availableForCivilization"] = resolution.IsAvailableForCivilization,
                        ["commanderExecutable"] = resolution.IsExecutable, ["contentExists"] = true,
                        ["dropoffResources"] = new JArray(Enum.GetValues(typeof(ResourceType)).Cast<ResourceType>()
                            .Where(r => GameSimulation.AcceptsResourceType(data.BuildingType, r)).Select(r => r.ToString())) });
                }
            }
            return new JObject { ["units"] = units, ["buildings"] = buildings,
                ["technologies"] = new JArray(), ["nameInterpretations"] = new JArray(overflow ? Enumerable.Empty<JObject>() : resolutions),
                ["resolvedNames"] = new JArray(overflow ? Enumerable.Empty<JObject>() : resolvedNames),
                ["requiredCanonicalIds"] = new JArray(requiredIds), ["optionalCanonicalIds"] = new JArray(optionalIds),
                ["requiredReferenceOverflow"] = overflow,
                ["policy"] = "Complete catalog retrieved locally. Omission is not unavailability. Content existence, civilization availability, mechanic support, and current age/resources/producer/population readiness are separate facts. Missing resources or producers are execution blockers, not unknown content." }
                .ToString(Formatting.None);
        }

        private static JObject Cost(KnowledgeCost value) => new JObject
            { ["food"] = value.Food, ["wood"] = value.Wood, ["gold"] = value.Gold, ["stone"] = value.Stone };
        private static void MatchAliases(HashSet<string> required, HashSet<string> matchedPhrases, string id, string name,
            IEnumerable<string> aliases, string searchable)
        {
            foreach (string label in new[] { name }.Concat(aliases))
            {
                string normalized = Regex.Replace(label.ToLowerInvariant(), @"[^\p{L}\p{N}]+", " ").Trim();
                if (normalized.Length > 0 && searchable.Contains(" " + normalized + " "))
                { required.Add(id); matchedPhrases.Add(normalized); }
            }
        }
    }
}
