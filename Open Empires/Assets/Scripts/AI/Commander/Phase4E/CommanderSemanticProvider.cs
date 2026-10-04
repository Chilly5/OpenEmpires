using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace OpenEmpires
{
    public interface ICommanderSemanticProvider
    {
        Task<CommanderSemanticResult> TranslateSemanticAsync(
            CommanderSemanticProviderRequest request, CancellationToken token);
    }

    // This request retains no CommanderContext: only an allow-listed, bounded projection.
    public sealed class CommanderSemanticProviderRequest
    {
        public const int MaximumPlayerMessageCharacters = 1024;
        public const int MaximumContextCharacters = 8192;
        public const int MaximumSemanticMemoryCharacters = 4096;

        public string PlayerMessage { get; }
        public string SerializedContext { get; }
        public string SerializedSemanticMemory { get; }
        public bool IsPlayerMessageTooLong { get; }

        public CommanderSemanticProviderRequest(string playerMessage, CommanderContext context,
            IReadOnlyList<CommanderSemanticMemoryEntry> semanticMemory = null)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            string text = playerMessage ?? string.Empty;
            IsPlayerMessageTooLong = text.Length > MaximumPlayerMessageCharacters;
            PlayerMessage = IsPlayerMessageTooLong ? string.Empty : text;
            SerializedContext = SerializeContext(context);
            SerializedSemanticMemory = CommanderSemanticConversationMemory.Serialize(semanticMemory
                ?? Array.Empty<CommanderSemanticMemoryEntry>());
            if (SerializedSemanticMemory.Length > MaximumSemanticMemoryCharacters)
                throw new InvalidOperationException("Semantic conversation memory exceeded its limit.");
        }

        private static string SerializeContext(CommanderContext context)
        {
            var unitCounts = new SortedDictionary<string, int>(StringComparer.Ordinal)
            {
                ["Villager"] = 0, ["Spearman"] = 0, ["Archer"] = 0, ["Knight"] = 0
            };
            foreach (CommanderUnitSnapshot unit in context.Units)
            {
                string name = UnitName(unit.UnitType);
                if (name != null)
                    unitCounts[name] = (int)Math.Min(200L,
                        (long)unitCounts[name] + Math.Max(0, unit.Count));
            }

            var buildingCounts = new SortedDictionary<string, int>(StringComparer.Ordinal)
            {
                ["House"] = 0, ["Barracks"] = 0, ["ArcheryRange"] = 0,
                ["Stables"] = 0, ["Tower"] = 0, ["TownCenter"] = 0
            };
            foreach (CommanderBuildingSnapshot building in context.Buildings)
                if (building.Type != null && buildingCounts.ContainsKey(building.Type))
                    buildingCounts[building.Type] = Math.Min(200, buildingCounts[building.Type] + 1);

            var root = new JObject
            {
                ["age"] = Math.Max(0, context.Age),
                ["population"] = Math.Max(0, context.Population),
                ["populationCap"] = Math.Max(0, context.PopulationCap),
                ["ownedUnitCounts"] = JObject.FromObject(unitCounts),
                ["ownedBuildingCounts"] = JObject.FromObject(buildingCounts),
                ["unitCapabilities"] = new JArray("Villager", "Spearman", "Archer", "Knight"),
                ["structureCapabilities"] = new JArray("House", "Barracks", "ArcheryRange",
                    "Stables", "Tower", "TownCenter"),
                ["resourceCapabilities"] = new JArray("Food", "Wood", "Gold", "Stone"),
                ["strategyCapabilities"] = new JArray("AttackPreparation", "DefensivePreparation",
                    "EconomicExpansion", "MilitaryReinforcement", "RangedReinforcement",
                    "DefensiveTurtle"),
                ["knowledge"] = ParseKnowledge(context.KnowledgeContext)
            };
            string serialized = root.ToString(Formatting.None);
            if (serialized.Length > MaximumContextCharacters)
                throw new InvalidOperationException("Semantic context exceeded its limit.");
            return serialized;
        }

        private static JToken ParseKnowledge(string json)
        {
            if (string.IsNullOrEmpty(json)) return new JObject();
            try { return JToken.Parse(json); }
            catch (JsonException) { return new JObject(); }
        }

        private static string UnitName(int type)
        {
            switch (type)
            {
                case 0: return "Villager";
                case 1:
                case 12: return "Spearman"; // HRE Landsknecht resolves from Spearman.
                case 2:
                case 10: return "Archer"; // English Longbowman resolves from Archer.
                case 7: return "Knight";
                default: return null;
            }
        }
    }

    public sealed partial class CommanderSemanticResult
    {
        internal static CommanderSemanticResult ProviderRejected(string safeExplanation)
        {
            return new CommanderSemanticResult(false, CommanderSemanticOutcome.Unsupported,
                Array.Empty<CommanderSemanticNode>(), safeExplanation);
        }
    }
}
