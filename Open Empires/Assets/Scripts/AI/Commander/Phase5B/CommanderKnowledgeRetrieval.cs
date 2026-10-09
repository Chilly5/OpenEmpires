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
        internal const int MaximumEntries = 8;
        internal static string Retrieve(GameKnowledgeCatalog catalog, string input, Civilization civilization)
        {
            var unitScores = new Dictionary<string, int>(StringComparer.Ordinal);
            var buildingScores = new Dictionary<string, int>(StringComparer.Ordinal);
            var resolutions = new List<JObject>();
            string text = (input ?? string.Empty);
            if (text.Length > 1024) text = text.Substring(0, 1024);
            var words = Regex.Matches(text.ToLowerInvariant(), @"[\p{L}\p{N}]+")
                .Cast<Match>().Select(x => x.Value).Take(96).ToArray();
            string searchable = " " + string.Join(" ", words) + " ";
            foreach (var unit in catalog.Units)
                ScoreAliases(unitScores, unit.StableId, unit.DisplayName, unit.Aliases, searchable);
            foreach (var building in catalog.Buildings)
                ScoreAliases(buildingScores, building.StableId, building.DisplayName, building.Aliases, searchable);

            var phrases = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < words.Length && phrases.Count < 128; i++)
                for (int length = 1; length <= 4 && i + length <= words.Length && phrases.Count < 128; length++)
                {
                    string phrase = string.Join(" ", words.Skip(i).Take(length));
                    if (phrase.Length > 64 || !phrases.Add(phrase)) continue;
                    var unit = CommanderContentNameResolver.ResolveUnit(phrase, catalog, civilization);
                    var building = CommanderContentNameResolver.ResolveBuilding(phrase, catalog, civilization);
                    if (unit.Match != null) Raise(unitScores, unit.Match.StableId, 20 + phrase.Length);
                    foreach (var suggestion in unit.Suggestions) Raise(unitScores, suggestion.StableId, 3);
                    if (building.Match != null) Raise(buildingScores, building.Match.StableId, 20 + phrase.Length);
                    foreach (var suggestion in building.Suggestions) Raise(buildingScores, suggestion.StableId, 3);
                    if (resolutions.Count < 6 && (unit.Suggestions.Count > 0 || building.Suggestions.Count > 0))
                        resolutions.Add(new JObject { ["phrase"] = phrase,
                            ["interpretationOnly"] = true,
                            ["unitCandidates"] = new JArray(unit.Suggestions.Select(x => x.StableId)),
                            ["buildingCandidates"] = new JArray(building.Suggestions.Select(x => x.StableId)),
                            ["ambiguous"] = unit.Status == CommanderContentResolutionStatus.Ambiguous
                                || building.Status == CommanderContentResolutionStatus.Ambiguous });
                }
            var chosen = unitScores.Select(x => (id: x.Key, score: x.Value, unit: true))
                .Concat(buildingScores.Select(x => (id: x.Key, score: x.Value, unit: false)))
                .OrderByDescending(x => x.score).ThenBy(x => x.id, StringComparer.Ordinal)
                .Take(MaximumEntries).ToArray();
            var units = new JArray(); var buildings = new JArray();
            foreach (var entry in chosen)
            {
                if (entry.unit)
                {
                    var data = catalog.FindUnit(entry.id);
                    var resolution = CommanderContentNameResolver.ResolveUnit(data.StableId, catalog, civilization);
                    units.Add(new JObject { ["id"] = data.StableId, ["name"] = data.DisplayName,
                        ["age"] = data.RequiredAge, ["producer"] = data.ProductionBuildingType < 0
                            ? null : ((BuildingType)data.ProductionBuildingType).ToString(),
                        ["cost"] = Cost(data.Cost), ["availableForCivilization"] = resolution.IsAvailableForCivilization,
                        ["commanderExecutable"] = resolution.IsExecutable });
                }
                else
                {
                    var data = catalog.FindBuilding(entry.id);
                    var resolution = CommanderContentNameResolver.ResolveBuilding(data.StableId, catalog, civilization);
                    buildings.Add(new JObject { ["id"] = data.StableId, ["name"] = data.DisplayName,
                        ["age"] = data.RequiredAge, ["cost"] = Cost(data.Cost),
                        ["availableForCivilization"] = resolution.IsAvailableForCivilization,
                        ["commanderExecutable"] = resolution.IsExecutable,
                        ["dropoffResources"] = new JArray(Enum.GetValues(typeof(ResourceType)).Cast<ResourceType>()
                            .Where(r => GameSimulation.AcceptsResourceType(data.BuildingType, r)).Select(r => r.ToString())) });
                }
            }
            return new JObject { ["units"] = units, ["buildings"] = buildings,
                ["technologies"] = new JArray(), ["nameInterpretations"] = new JArray(resolutions),
                ["policy"] = "Knowledge is not execution authority. Show canonical interpretations; clarify ambiguous names. Availability and support are separate." }
                .ToString(Formatting.None);
        }

        private static JObject Cost(KnowledgeCost value) => new JObject
            { ["food"] = value.Food, ["wood"] = value.Wood, ["gold"] = value.Gold, ["stone"] = value.Stone };
        private static void Raise(Dictionary<string, int> scores, string id, int score)
        { if (!scores.TryGetValue(id, out int old) || old < score) scores[id] = score; }
        private static void ScoreAliases(Dictionary<string, int> scores, string id, string name,
            IEnumerable<string> aliases, string searchable)
        {
            foreach (string label in new[] { name }.Concat(aliases))
            {
                string normalized = Regex.Replace(label.ToLowerInvariant(), @"[^\p{L}\p{N}]+", " ").Trim();
                if (normalized.Length > 0 && searchable.Contains(" " + normalized + " "))
                    Raise(scores, id, 30 + normalized.Length);
            }
        }
    }
}
