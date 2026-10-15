using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace OpenEmpires
{
    // Detached provider proposal. It carries typed, bounded data, never authority or a runtime selector.
    public sealed class CommanderRequestGraphDraft
    {
        public sealed class Slot
        {
            private readonly List<JToken> alternatives;
            public int Node { get; }
            public string Path { get; }
            public string Type { get; }
            public IReadOnlyList<JToken> Alternatives
            {
                get
                {
                    var detached = new List<JToken>();
                    foreach (var value in alternatives) detached.Add(value.DeepClone());
                    return detached.AsReadOnly();
                }
            }
            internal Slot(int node, string path, string type, IList<JToken> alternatives)
            {
                Node = node; Path = path; Type = type; this.alternatives = new List<JToken>();
                foreach (var value in alternatives) this.alternatives.Add(value.DeepClone());
            }
        }

        private readonly JObject graph;
        private readonly List<Slot> slots;
        private readonly List<KeyValuePair<string, string>> nameConfirmations;
        private readonly string unresolvedNameRequirements;
        internal bool IsNameConfirmation => nameConfirmations != null;
        internal string NameConfirmationQuestion => nameConfirmations.Count == 0
            ? "Blocked: the proposed content does not uniquely match the unresolved similar name. Original request retained; no action was submitted."
            : "Did you mean " + nameConfirmations[0].Value + " for that similar name? Confirm the name with yes or its name, or no to dismiss. Gameplay approval is separate.";
        public IReadOnlyList<Slot> MissingSlots => slots.AsReadOnly();
        public int NodeCount => (graph["nodes"] as JArray)?.Count ?? 0;
        private CommanderRequestGraphDraft(JObject graph, List<Slot> slots, string unresolvedNames = null)
        { this.graph = (JObject)graph.DeepClone(); this.slots = slots; unresolvedNameRequirements = unresolvedNames; }

        internal CommanderRequestGraphDraft WithNameRequirements(JArray hints)
            => new CommanderRequestGraphDraft(graph, slots, hints.ToString(Formatting.None));

        private CommanderRequestGraphDraft(JObject proposal, List<KeyValuePair<string, string>> confirmations)
            : this(proposal, new List<Slot>()) { nameConfirmations = confirmations; }

        // Only called after strict provider parsing. Retain the entire proposal, including
        // the DynamicPlan envelope, and re-enter the same parser/admission path locally.
        internal static CommanderRequestGraphDraft ConfirmNames(string strictProposal, JArray hints)
        {
            var root = JObject.Parse(strictProposal);
            var catalog = GameKnowledgeCatalog.BuildCanonicalIdentityCatalog();
            var confirmations = new List<KeyValuePair<string, string>>();
            foreach (JObject hint in hints)
            {
                var matches = new List<KeyValuePair<string, string>>();
                foreach (string id in ((JArray)hint["unitCandidates"]).Concat((JArray)hint["buildingCandidates"]).Values<string>())
                {
                    var unit = catalog.FindUnit(id);
                    var building = catalog.FindBuilding(id);
                    string display = unit?.DisplayName ?? building?.DisplayName;
                    if (display == null) continue;
                    bool named = root.Descendants().OfType<JProperty>().Any(p => p.Value.Type == JTokenType.String &&
                        (unit != null ? p.Name == "unit" : p.Name == "structure" || p.Name == "producer" || p.Name == "building") &&
                        ((string)p.Value == id || (unit != null
                            ? CommanderIntentCatalog.TryResolveUnit((string)p.Value, out int u) && u == unit.UnitType
                            : CommanderIntentCatalog.TryResolveStructure((string)p.Value, out BuildingType b) && b == building.BuildingType)));
                    if (named) matches.Add(new KeyValuePair<string, string>(id, display));
                }
                // An omitted, unrelated, or multiply interpreted suggestion cannot be approved.
                if (matches.Count != 1) return new CommanderRequestGraphDraft(root, new List<KeyValuePair<string, string>>());
                if (!confirmations.Any(c => c.Key == matches[0].Key)) confirmations.Add(matches[0]);
            }
            return new CommanderRequestGraphDraft(root, confirmations);
        }

        internal static CommanderRequestGraphDraft Parse(JObject root)
        {
            if (!(root["pending"] is JObject graph) || !(graph["nodes"] is JArray nodes)
                || nodes.Count < 1 || nodes.Count > CommanderSemanticJson.MaximumNodes
                || !(root["missingSlots"] is JArray sourceSlots) || sourceSlots.Count < 1 || sourceSlots.Count > 4)
                throw new JsonException();
            ExactFields(graph, "executionOrder", "nodes");
            string order = (string)graph["executionOrder"];
            if (order != "Independent" && order != "Sequential" && order != "DependencyGraph") throw new JsonException();
            var slots = new List<Slot>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var entry in sourceSlots)
            {
                if (!(entry is JObject spec)) throw new JsonException();
                ExactFields(spec, "node", "path", "type", "alternatives");
                if (spec["node"]?.Type != JTokenType.Integer || spec["path"]?.Type != JTokenType.String
                    || spec["type"]?.Type != JTokenType.String || !(spec["alternatives"] is JArray alternatives)
                    || alternatives.Count < 1 || alternatives.Count > 8) throw new JsonException();
                int index = (int)spec["node"];
                string path = (string)spec["path"], type = (string)spec["type"];
                if (index < 0 || index >= nodes.Count || !(nodes[index] is JObject node)
                    ) throw new JsonException();
                if (!Allowed(type, path))
                    throw CommanderSemanticResult.InvalidClarificationSlot(spec, supportedSlotPaths.ContainsKey(type));
                if (path == CommanderMovementContract.RequestSelectionSlotPath
                    && (string)node["type"] != "MoveUnits") throw new JsonException();
                if (!seen.Add(index + ":" + path)) throw new JsonException();
                JObject parent = Parent(node, path);
                string leaf = Leaf(path);
                if (parent == null || type == "Confirmation" && parent[leaf]?.Type != JTokenType.String
                    || type != "Confirmation" && parent.Property(leaf) != null) throw new JsonException();
                var values = new List<JToken>();
                foreach (var choice in alternatives)
                {
                    if (choice == null || (choice.Type != JTokenType.String && choice.Type != JTokenType.Integer)
                        || choice.ToString().Length > 64 || type == "Count" && choice.Type != JTokenType.Integer
                        || type != "Count" && choice.Type != JTokenType.String
                        || type == "Confirmation" && (string)choice != (string)parent[leaf]) throw new JsonException();
                    values.Add(choice.DeepClone());
                }
                slots.Add(new Slot(index, path, type, values));
            }
            var draft = new CommanderRequestGraphDraft(graph, slots);
            // Every offered completion, not just the first, must survive the existing strict
            // parser and graph checks. Temporary values never enter the stored graph.
            foreach (var slot in slots)
                foreach (var alternative in slot.Alternatives)
                    if (!draft.Probe(slot, alternative).IsValid) throw new JsonException();
            return draft;
        }

        private CommanderSemanticResult Probe(Slot changed, JToken value)
            => CommanderSemanticJson.ParseProviderResponse(CompletedProposal(changed, value).ToString(Formatting.None));

        private JObject CompletedProposal(Slot changed, JToken value)
        {
            var completed = (JObject)graph.DeepClone();
            foreach (var slot in slots)
            {
                var parent = Parent((JObject)((JArray)completed["nodes"])[slot.Node], slot.Path);
                if (slot.Type != "Confirmation") parent[Leaf(slot.Path)] = (slot == changed ? value : slot.Alternatives[0]).DeepClone();
            }
            return new JObject { ["outcome"] = "Request",
                ["executionOrder"] = completed["executionOrder"].DeepClone(),
                ["nodes"] = completed["nodes"].DeepClone() };
        }

        // Game-owned metadata is never serialized into provider fields. Each local
        // transition retains it; only an explicit typed name choice resolves a hint.
        private CommanderSemanticResult ParseLocal(JObject proposal, string selectedPath, JToken selectedValue)
        {
            string json = proposal.ToString(Formatting.None);
            var parsed = CommanderSemanticJson.ParseProviderResponse(json);
            if (!parsed.IsValid || string.IsNullOrEmpty(unresolvedNameRequirements)) return parsed;
            var hints = JArray.Parse(unresolvedNameRequirements);
            string selectedId = null;
            string candidates = null;
            var catalog = GameKnowledgeCatalog.BuildCanonicalIdentityCatalog();
            if (selectedValue?.Type == JTokenType.String)
            {
                if ((selectedPath == "unit" || selectedPath == CommanderMovementContract.RequestSelectionSlotPath)
                    && CommanderIntentCatalog.TryResolveUnit((string)selectedValue, out int unit))
                { selectedId = catalog.FindUnitByType(unit)?.StableId; candidates = "unitCandidates"; }
                else if ((selectedPath == "structure" || selectedPath == "producer")
                    && CommanderIntentCatalog.TryResolveStructure((string)selectedValue, out BuildingType building))
                { selectedId = catalog.Buildings.FirstOrDefault(b => b.BuildingType == building)?.StableId; candidates = "buildingCandidates"; }
            }
            if (selectedId != null)
                foreach (var hint in hints.OfType<JObject>().Where(h => ((JArray)h[candidates]).Values<string>().Contains(selectedId)).ToList())
                    hint.Remove();
            return parsed.RequireNameConfirmation(json, hints);
        }

        internal CommanderSemanticResult Resolve(string reply)
        {
            if (IsNameConfirmation)
            {
                if (nameConfirmations.Count == 0) return null;
                var proposed = nameConfirmations[0];
                string whole = (reply ?? string.Empty).Trim();
                if (!string.Equals(whole, "yes", StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(whole, "y", StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(whole, proposed.Value, StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(whole, proposed.Key, StringComparison.Ordinal)) return null;
                if (nameConfirmations.Count > 1)
                    return CommanderSemanticResult.NameConfirmation(new CommanderRequestGraphDraft(graph, nameConfirmations.Skip(1).ToList()));
                return CommanderSemanticJson.ParseProviderResponse(graph.ToString(Formatting.None));
            }
            if (IsCorrectionUtterance(reply)) return Correct(reply);
            Slot slot = null;
            JToken chosen = null;
            foreach (var candidate in slots)
            {
                JToken match = Match(candidate, reply);
                if (match == null) continue;
                if (slot != null) return null; // A reply matching two distinct slots is ambiguous.
                slot = candidate; chosen = match;
            }
            if (slot == null) return null;
            return ResolveSlot(slot, chosen);
        }

        internal CommanderSemanticResult ResolveMarkerLocation(int node)
        {
            var eligible = slots.Where(s => s.Node == node && s.Type == "Location"
                && s.Alternatives.Any(a => a.Type == JTokenType.String && (string)a == "PlayerMarker")).ToArray();
            return eligible.Length == 1 ? ResolveSlot(eligible[0], new JValue("PlayerMarker")) : null;
        }

        private CommanderSemanticResult ResolveSlot(Slot slot, JToken chosen)
        {
            if (slots.Count == 1)
            {
                var parsed = ParseLocal(CompletedProposal(slot, chosen), slot.Path, chosen);
                return parsed.IsValid ? parsed : null;
            }
            var next = (JObject)graph.DeepClone();
            if (slot.Type != "Confirmation")
                Parent((JObject)((JArray)next["nodes"])[slot.Node], slot.Path)[Leaf(slot.Path)] = chosen.DeepClone();
            var remaining = new JArray();
            foreach (var other in slots)
                if (!ReferenceEquals(other, slot))
                    remaining.Add(new JObject { ["node"] = other.Node, ["path"] = other.Path,
                        ["type"] = other.Type, ["alternatives"] = new JArray(other.Alternatives) });
            return ParseLocal(new JObject { ["outcome"] = "Clarify",
                ["message"] = "Please specify the remaining detail.", ["pending"] = next,
                ["missingSlots"] = remaining }, slot.Path, chosen);
        }

        internal static bool IsCorrectionUtterance(string reply)
        {
            string[] correctionWords = Words(reply);
            if (correctionWords.Length >= 3 && correctionWords[0] == "make" && correctionWords[1] == "that") return true;
            foreach (var word in correctionWords)
                if (word == "instead" || word == "actually" || word == "change" || word == "rather") return true;
            return false;
        }

        private sealed class Change
        {
            internal int Node;
            internal string Path;
            internal JToken Value;
        }

        private CommanderSemanticResult Correct(string reply)
        {
            if (!TryAffirmativeCorrection(reply, out string replacement, out string prior)) return null;
            string[] words = Words(replacement);
            if (words.Length == 0 || words.Length > 12) return null;
            var changes = new List<Change>();
            var nodes = (JArray)graph["nodes"];
            for (int start = 0; start < words.Length; start++)
                for (int length = 1; length <= 3 && start + length <= words.Length; length++)
                {
                    string phrase = string.Join(" ", words, start, length);
                    if (CommanderIntentCatalog.TryResolveStructure(phrase, out BuildingType building))
                    {
                        for (int i = 0; i < nodes.Count; i++)
                        {
                            AddChange(changes, i, (JObject)nodes[i], "structure", new JValue(building.ToString()));
                            AddChange(changes, i, (JObject)nodes[i], "producer", new JValue(building.ToString()));
                        }
                    }
                    if (CommanderIntentCatalog.TryResolveUnit(phrase, out int unitType))
                    {
                        string canonical = CommanderIntentCatalog.GetUnitDisplayName(unitType);
                        for (int i = 0; i < nodes.Count; i++)
                            AddChange(changes, i, (JObject)nodes[i], "unit", new JValue(canonical));
                    }
                    if (CommanderIntentCatalog.TryResolveResource(phrase, out ResourceType resource))
                    {
                        for (int i = 0; i < nodes.Count; i++)
                        {
                            AddChange(changes, i, (JObject)nodes[i], "resource", new JValue(resource.ToString()));
                            AddChange(changes, i, (JObject)nodes[i], "destination.resource", new JValue(resource.ToString()));
                            AddChange(changes, i, (JObject)nodes[i], "placement.resource", new JValue(resource.ToString()));
                        }
                    }
                    string sourceName = SourceAlias(phrase);
                    if (Enum.TryParse(sourceName, true, out ResourceSourceKind sourceKind)
                        && Enum.IsDefined(typeof(ResourceSourceKind), sourceKind))
                    {
                        for (int i = 0; i < nodes.Count; i++)
                        {
                            AddChange(changes, i, (JObject)nodes[i], "sourceKind", new JValue(sourceKind.ToString()));
                            AddChange(changes, i, (JObject)nodes[i], "destination.sourceKind", new JValue(sourceKind.ToString()));
                            AddChange(changes, i, (JObject)nodes[i], "placement.sourceKind", new JValue(sourceKind.ToString()));
                        }
                    }
                }
            int? number = null;
            foreach (var word in words)
                if (CommanderClarificationReplies.TryParseCount(word, out int parsed))
                {
                    if (number.HasValue && number.Value != parsed) return null;
                    number = parsed;
                }
            if (number.HasValue)
            {
                bool workers = Contains(words, "villager") || Contains(words, "villagers") || Contains(words, "worker") || Contains(words, "workers");
                bool amount = prior == "amount" || Contains(words, "additional") || Contains(words, "stockpile") || Contains(words, "food")
                    || Contains(words, "wood") || Contains(words, "gold") || Contains(words, "stone");
                if (workers && amount) return null;
                int fields = 0;
                for (int i = 0; i < nodes.Count; i++)
                {
                    var node = (JObject)nodes[i];
                    if (node["count"]?.Type == JTokenType.Integer && !amount) fields++;
                    if (node["resourceAmount"]?.Type == JTokenType.Integer && !workers) fields++;
                }
                if (fields != 1) return null;
                for (int i = 0; i < nodes.Count; i++)
                {
                    var node = (JObject)nodes[i];
                    if (node["count"]?.Type == JTokenType.Integer && !amount)
                        AddChange(changes, i, node, "count", new JValue(number.Value));
                    if (node["resourceAmount"]?.Type == JTokenType.Integer && !workers)
                        AddChange(changes, i, node, "resourceAmount", new JValue(number.Value));
                }
            }
            if (changes.Count != 1) return null;
            Change change = changes[0];
            if (prior != null && !(prior == "amount" && change.Path == "resourceAmount")
                && !NamesCurrentValue(prior, change)) return null;
            if (change.Path == "resourceAmount" && prior != "amount"
                && !AmountQualifierMatchesGraph(replacement, change.Node)) return null;
            var next = (JObject)graph.DeepClone();
            Parent((JObject)((JArray)next["nodes"])[change.Node], change.Path)[Leaf(change.Path)] = change.Value.DeepClone();
            var remaining = new JArray();
            foreach (var slot in slots)
            {
                if (slot.Node == change.Node && slot.Path == change.Path) continue;
                remaining.Add(new JObject { ["node"] = slot.Node, ["path"] = slot.Path,
                    ["type"] = slot.Type, ["alternatives"] = new JArray(slot.Alternatives) });
            }
            if (remaining.Count == 0)
                return ParseLocal(new JObject { ["outcome"] = "Request",
                    ["executionOrder"] = next["executionOrder"].DeepClone(),
                    ["nodes"] = next["nodes"].DeepClone() }, change.Path, change.Value);
            return ParseLocal(new JObject { ["outcome"] = "Clarify",
                ["message"] = "Please specify the remaining detail.", ["pending"] = next,
                ["missingSlots"] = remaining }, change.Path, change.Value);
        }

        // Correction-like wording keeps a pending draft local, but only these complete,
        // affirmative shapes are allowed to change one typed leaf. Unknown language fails closed.
        private static bool TryAffirmativeCorrection(string reply, out string replacement, out string prior)
        {
            replacement = null; prior = null;
            string whole = Regex.Replace((reply ?? string.Empty).Trim().TrimEnd('.', '!', '?').Trim().ToLowerInvariant(), @"\s+", " ");
            if (!Regex.IsMatch(whole, @"^[a-z0-9 ]+$")) return false;
            for (int i = 0; i < 2; i++)
            {
                if (whole.StartsWith("please ", StringComparison.Ordinal)) whole = whole.Substring(7);
                else if (whole.StartsWith("actually ", StringComparison.Ordinal)) whole = whole.Substring(9);
                else break;
            }
            if (whole.StartsWith("change ", StringComparison.Ordinal))
            {
                string rest = whole.Substring(7);
                int separator = rest.IndexOf(" to ", StringComparison.Ordinal);
                if (separator <= 0 || rest.IndexOf(" to ", separator + 4, StringComparison.Ordinal) >= 0) return false;
                prior = rest.Substring(0, separator);
                replacement = rest.Substring(separator + 4);
                if (prior.StartsWith("the ", StringComparison.Ordinal)) prior = prior.Substring(4);
                if (prior.StartsWith("a ", StringComparison.Ordinal)) prior = prior.Substring(2);
                if (prior == "amount" && CommanderClarificationReplies.TryParseCount(replacement, out int _)) return true;
                if (!IsTypedCorrectionValue(prior) || !IsTypedCorrectionValue(replacement)) return false;
                return true;
            }
            if (whole.StartsWith("make that ", StringComparison.Ordinal))
            {
                replacement = whole.Substring(10);
                return IsTypedCorrectionValue(replacement);
            }
            if (!whole.EndsWith(" instead", StringComparison.Ordinal)) return false;
            replacement = whole.Substring(0, whole.Length - 8);
            if (replacement.StartsWith("build a ", StringComparison.Ordinal)) replacement = replacement.Substring(8);
            else if (replacement.StartsWith("make a ", StringComparison.Ordinal)) replacement = replacement.Substring(7);
            else if (replacement.StartsWith("a ", StringComparison.Ordinal)) replacement = replacement.Substring(2);
            else if (replacement.StartsWith("an ", StringComparison.Ordinal)) replacement = replacement.Substring(3);
            return IsTypedCorrectionValue(replacement);
        }

        private static bool IsTypedCorrectionValue(string phrase)
        {
            if (string.IsNullOrWhiteSpace(phrase) || Words(phrase).Length > 3) return false;
            if (CommanderIntentCatalog.TryResolveStructure(phrase, out BuildingType _) ||
                CommanderIntentCatalog.TryResolveUnit(phrase, out int _) ||
                CommanderIntentCatalog.TryResolveResource(phrase, out ResourceType _)) return true;
            if (Enum.TryParse(SourceAlias(phrase), true, out ResourceSourceKind source)
                && Enum.IsDefined(typeof(ResourceSourceKind), source)) return true;
            string[] words = Words(phrase);
            if (words.Length < 2 || !CommanderClarificationReplies.TryParseCount(words[0], out int _)) return false;
            if (words.Length == 2 && (words[1] == "villager" || words[1] == "villagers"
                || words[1] == "worker" || words[1] == "workers")) return true;
            int resourceIndex = words[1] == "additional" || words[1] == "stockpile" ? 2 : 1;
            return words.Length == resourceIndex + 1 &&
                (words[resourceIndex] == "food" || words[resourceIndex] == "wood"
                    || words[resourceIndex] == "gold" || words[resourceIndex] == "stone");
        }

        private bool NamesCurrentValue(string prior, Change change)
        {
            JToken current = Parent((JObject)((JArray)graph["nodes"])[change.Node], change.Path)?[Leaf(change.Path)];
            if (current == null) return false;
            string leaf = Leaf(change.Path);
            if (leaf == "structure" || leaf == "producer")
                return CommanderIntentCatalog.TryResolveStructure(prior, out BuildingType building)
                    && string.Equals((string)current, building.ToString(), StringComparison.OrdinalIgnoreCase);
            if (leaf == "unit")
                return CommanderIntentCatalog.TryResolveUnit(prior, out int unit)
                    && string.Equals((string)current, CommanderIntentCatalog.GetUnitDisplayName(unit), StringComparison.OrdinalIgnoreCase);
            if (leaf == "resource")
                return CommanderIntentCatalog.TryResolveResource(prior, out ResourceType resource)
                    && string.Equals((string)current, resource.ToString(), StringComparison.OrdinalIgnoreCase);
            if (leaf == "sourceKind")
                return string.Equals((string)current, SourceAlias(prior), StringComparison.OrdinalIgnoreCase);
            string[] words = Words(prior);
            if (words.Length < 2 || !CommanderClarificationReplies.TryParseCount(words[0], out int number)
                || (int)current != number) return false;
            if (leaf == "count") return words.Length == 2 && (words[1] == "villager" || words[1] == "villagers"
                || words[1] == "worker" || words[1] == "workers");
            return leaf == "resourceAmount" && AmountQualifierMatchesGraph(prior, change.Node);
        }

        private bool AmountQualifierMatchesGraph(string phrase, int nodeIndex)
        {
            string[] words = Words(phrase);
            if (words.Length < 2 || words.Length > 3
                || !CommanderClarificationReplies.TryParseCount(words[0], out int _)
                || !CommanderIntentCatalog.TryResolveResource(words[words.Length - 1], out ResourceType namedResource)) return false;
            var node = (JObject)((JArray)graph["nodes"])[nodeIndex];
            if (!string.Equals((string)node["destination"]?["resource"], namedResource.ToString(),
                StringComparison.OrdinalIgnoreCase)) return false;
            if (words.Length == 2) return true; // An omitted mode cannot rewrite the retained mode.
            string namedMode = words[1] == "additional" ? "AdditionalGathered"
                : words[1] == "stockpile" ? "Stockpile" : null;
            return namedMode != null && string.Equals((string)node["resourceAmountMode"], namedMode,
                StringComparison.OrdinalIgnoreCase);
        }

        private static void AddChange(List<Change> changes, int index, JObject node, string path, JToken replacement)
        {
            JObject parent = Parent(node, path);
            JToken current = parent?[Leaf(path)];
            if (current == null || current.Type != replacement.Type || JToken.DeepEquals(current, replacement)) return;
            foreach (var existing in changes)
                if (existing.Node == index && existing.Path == path && JToken.DeepEquals(existing.Value, replacement)) return;
            changes.Add(new Change { Node = index, Path = path, Value = replacement });
        }

        private static bool Contains(string[] words, string word)
        { foreach (string entry in words) if (entry == word) return true; return false; }

        private static string[] Words(string text)
        { return (text ?? string.Empty).Trim().ToLowerInvariant().Split(
            new[] { ' ', '\t', '\r', '\n', ',', '.', '!', '?', ';', ':' }, StringSplitOptions.RemoveEmptyEntries); }

        private static JToken Match(Slot slot, string reply)
        {
            // Only terminal sentence punctuation is presentation, not a slot value.
            // Preserve the original reply in the retained request and evidence.
            string whole = (reply ?? string.Empty).Trim().TrimEnd('.', '!', '?').Trim().ToLowerInvariant();
            JToken chosen = null;
            if (slot.Type == "Confirmation" && (whole == "yes" || whole == "y")) chosen = slot.Alternatives[0];
            else if (slot.Type == "Count" && CommanderClarificationReplies.TryParseCount(reply, out int count)) chosen = new JValue(count);
            else
            {
                string canonical = slot.Type == "SourceKind" ? SourceAlias(whole) : whole;
                if (slot.Type == "Location")
                    canonical = whole == "my town center" || whole == "near my town center" || whole == "my tc"
                        ? (slot.Path == "location" ? "PlayerBase" : "MyTownCenter")
                        : whole == "my marker" || whole == "at my marker" || whole == "i placed a marker"
                            ? "PlayerMarker" : whole;
                foreach (var offered in slot.Alternatives)
                    if (string.Equals((string)offered, canonical, StringComparison.OrdinalIgnoreCase)) { chosen = offered; break; }
            }
            return chosen;
        }

        private static string SourceAlias(string reply)
        {
            switch (reply)
            {
                case "from trees": case "trees": case "tree": case "from tree": return "Tree";
                case "from berries": case "berries": case "berry": return "Berries";
                case "from sheep": case "sheep": return "Sheep";
                case "from farms": case "farms": case "farm": return "Farm";
                case "from gold mines": case "gold mines": case "gold mine": return "GoldMine";
                case "from stone mines": case "stone mines": case "stone mine": return "StoneMine";
                default: return reply;
            }
        }

        internal JObject ToPendingJson() => (JObject)graph.DeepClone();
        internal JArray ToSlotsJson()
        {
            var result = new JArray();
            foreach (var slot in slots)
                result.Add(new JObject { ["node"] = slot.Node, ["path"] = slot.Path,
                    ["type"] = slot.Type, ["alternatives"] = new JArray(slot.Alternatives) });
            return result;
        }

        // One slot vocabulary serves the strict draft parser and the provider contract.
        // This is the existing accepted set; advertising it grants no admission authority.
        private static readonly Dictionary<string,string[]> supportedSlotPaths = new Dictionary<string,string[]>(StringComparer.Ordinal)
        {
            ["Confirmation"]=new[]{"structure","unit","producer"},
            ["Count"]=new[]{CommanderMovementContract.CountSlotPath},
            ["Location"]=new[]{CommanderMovementContract.MarkerLocationSlotPath,"placement.anchor"},
            ["SourceKind"]=new[]{"sourceKind","destination.sourceKind","placement.sourceKind"},
            ["CanonicalName"]=new[]{"structure","unit",CommanderMovementContract.RequestSelectionSlotPath,"producer","technology","resource","destination.resource","placement.resource"}
        };
        internal static string SupportedSlotPathsJson => new JObject(supportedSlotPaths.Select(pair=>
            new JProperty(pair.Key,new JArray(pair.Value)))).ToString(Formatting.None);
        private static bool Allowed(string type,string path)
            => supportedSlotPaths.TryGetValue(type,out var paths)&&Array.IndexOf(paths,path)>=0;
        private static JObject Parent(JObject node, string path)
        {
            int dot = path.IndexOf('.');
            return dot < 0 ? node : node[path.Substring(0, dot)] as JObject;
        }
        private static string Leaf(string path)
        { int dot = path.IndexOf('.'); return dot < 0 ? path : path.Substring(dot + 1); }
        private static void ExactFields(JObject obj, params string[] fields)
        { foreach (var property in obj.Properties()) if (Array.IndexOf(fields, property.Name) < 0) throw new JsonException(); }
    }
}
