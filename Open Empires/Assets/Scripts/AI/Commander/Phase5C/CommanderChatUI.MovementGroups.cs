using System;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;

namespace OpenEmpires
{
    public sealed partial class CommanderChatUI
    {
        private string pendingMovementGroupText;
        private int pendingMovementGroupGeneration;

        // Only fills an explicitly missing group. Ordinary complete orders still use
        // the provider and the same game-owned admission/approval pipeline.
        private bool HandleMovementGroupClarification(string message)
        {
            if (pendingMovementGroupGeneration != runtimeGeneration) pendingMovementGroupText = null;
            if (pendingClarification != null || pendingActionPlan != null || retainedLocationResult != null)
            { pendingMovementGroupText = null; return false; }
            string text = message.Trim();
            bool missing = Regex.IsMatch(text,
                @"^(?:move|send)\s+(?:(?:my\s+)?(?:units|troops)\s+)?(?:to\s+)?(?:(?:my|the)\s+)?marker[.!]?$",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            if (!missing && pendingMovementGroupText == null) return false;
            if (semanticGoalManager == null || semanticGoalManager.IsDisposed || semanticSimulation == null) return false;
            if (missing)
            {
                ClearActionPlanPreview();
                pendingMovementGroupText = text;
                pendingMovementGroupGeneration = runtimeGeneration;
                AppendLine("Player", text);
                SetLatestRequestState("Needs clarification", "Which units should move to your marker?");
                AppendLine("Commander", "Which units should move to your marker? Give a unit name, a count, or all except villagers.");
                return true;
            }
            string whole = NormalizeWholeForm(text);
            if (whole == "cancel" || whole == "never mind" || whole == "nevermind")
            {
                pendingMovementGroupText = null;
                AppendLine("Player", text);
                AppendLine("Commander", "Pending movement cancelled.");
                SetLatestRequestState("Cancelled", "Pending movement cancelled.");
                return true;
            }
            if (Regex.IsMatch(text, @"^(?:help|what|why|how|which|show|status)\b", RegexOptions.IgnoreCase)) return false;
            if (Regex.IsMatch(text, @"^(?:move|send|build|train|research|attack|gather|stop|reset)\b", RegexOptions.IgnoreCase))
            { pendingMovementGroupText = null; return false; }
            if (!TryMovementGroupParameters(text, GameKnowledgeCatalog.Build(semanticSimulation),
                semanticSimulation.GetPlayerCivilization(semanticGoalManager.PlayerId), out JObject parameters))
            {
                AppendLine("Player", text);
                AppendLine("Commander", "Which unit group? Give a registered unit name, a count, or all except villagers. Your marker is retained.");
                return true;
            }
            string original = pendingMovementGroupText + " — " + text;
            int generation = runtimeGeneration;
            var manager = semanticGoalManager;
            bool Current() => this != null && runtimeGeneration == generation && ReferenceEquals(semanticGoalManager, manager) && !manager.IsDisposed;
            var ticket = manager.BeginSemanticRequest(original, generation, Current);
            var result = CommanderSemanticJson.Parse(new JObject
            {
                ["outcome"] = "DynamicPlan", ["version"] = 1,
                ["nodes"] = new JArray(
                    new JObject { ["id"] = "units", ["mechanic"] = "select-units", ["parameters"] = parameters, ["inputs"] = new JObject(), ["dependsOn"] = new JArray() },
                    new JObject { ["id"] = "place", ["mechanic"] = "resolve-location", ["parameters"] = new JObject { ["anchor"] = "PlayerMarker", ["relation"] = "Near", ["clearGapTiles"] = 1 }, ["inputs"] = new JObject(), ["dependsOn"] = new JArray() },
                    new JObject { ["id"] = "move", ["mechanic"] = "move-units", ["parameters"] = new JObject(), ["inputs"] = new JObject { ["units"] = "units", ["location"] = "place" }, ["dependsOn"] = new JArray("units", "place") })
            }.ToString());
            AppendLine("Player", text);
            if (!result.IsValid) return true;
            pendingMovementGroupText = null;
            manager.TraceRequest(ticket, "movement-group-completed");
            PrepareMarkerRequest(result, original, generation, Current, ticket);
            return true;
        }

        internal static bool TryMovementGroupParameters(string reply, GameKnowledgeCatalog catalog,
            Civilization civilization, out JObject parameters)
        {
            parameters = null;
            if (string.IsNullOrWhiteSpace(reply) || reply.Length > 160) return false;
            string text = reply.Trim().TrimEnd('.', '!').ToLowerInvariant();
            string[] pieces = Regex.Split(text, @"\s+except\s+");
            if (pieces.Length > 2) return false;
            var result = new JObject();
            string inclusion = Regex.Replace(pieces[0], @"^(?:my|the)\s+", "");
            bool all = inclusion.StartsWith("all ", StringComparison.Ordinal) || inclusion == "all";
            if (inclusion.StartsWith("all ", StringComparison.Ordinal)) inclusion = inclusion.Substring(4);
            inclusion = Regex.Replace(inclusion, @"^(?:my|the)\s+", "");
            Match count = Regex.Match(inclusion, @"^(\d+)\s+(.+)$");
            if (count.Success)
            {
                if (all || !int.TryParse(count.Groups[1].Value, out int n) || n < 1 || n > 50) return false;
                result["count"] = n;
                inclusion = count.Groups[2].Value;
            }
            else
            {
                int space = inclusion.IndexOf(' ');
                if (!all && space > 0 && CommanderClarificationReplies.TryParseCount(inclusion.Substring(0, space), out int namedCount))
                {
                    if (namedCount < 1 || namedCount > 50) return false;
                    result["count"] = namedCount;
                    inclusion = inclusion.Substring(space + 1);
                }
                else result["countMode"] = "AllMatching";
            }
            if (!MovementGroupIdentity(inclusion, catalog, civilization, out string field, out string identity)) return false;
            result[field] = identity;
            if (pieces.Length == 2)
            {
                string excluded = Regex.Replace(pieces[1], @"^(?:my|the)\s+", "");
                if (!MovementGroupIdentity(excluded, catalog, civilization, out field, out identity)) return false;
                result[field == "unit" ? "excludeUnit" : "excludeKind"] = identity;
            }
            parameters = result;
            return true;
        }

        private static bool MovementGroupIdentity(string text, GameKnowledgeCatalog catalog,
            Civilization civilization, out string field, out string identity)
        {
            field = "kind"; identity = null;
            switch (text)
            {
                case "all": case "units": identity = "AllUnits"; return true;
                case "army": case "military": case "troops": identity = "Military"; return true;
                case "villagers": case "workers": identity = "Villagers"; return true;
                case "scouts": identity = "Scout"; return true;
            }
            var resolved = CommanderContentNameResolver.ResolveUnit(text, catalog, civilization);
            if (resolved.Status != CommanderContentResolutionStatus.Resolved || resolved.Match == null) return false;
            field = "unit"; identity = resolved.Match.StableId; return true;
        }
    }
}
