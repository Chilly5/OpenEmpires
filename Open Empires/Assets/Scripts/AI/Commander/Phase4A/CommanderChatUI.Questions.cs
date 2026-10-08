using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace OpenEmpires
{
    public sealed partial class CommanderChatUI
    {
        private bool TryHandleQuestionQuery(string message, string normalized)
        {
            if (!TryObserveQuestionAnswer(normalized, out string answer)) return false;
            AppendLine("Player", (message ?? string.Empty).Trim(), false);
            AppendLine("Commander", answer, false);
            Conversation?.Memory.RecordExplanation(answer);
            return true;
        }

        // Existing question logic, now also available as a detached read-only provider observation.
        private bool TryObserveQuestionAnswer(string normalized, out string answer)
        {
            answer = null;
            GameSimulation sim = semanticSimulation ?? advisorySimulation;
            int playerId = Conversation?.PlayerId ?? 0;
            if (sim == null) return false;

            // 1. Counter questions: "what counters spearmen", "what counters archers", etc.
            Match counterMatch = Regex.Match(normalized, @"^what counters? (.+)$", RegexOptions.CultureInvariant);
            if (counterMatch.Success)
            {
                string target = counterMatch.Groups[1].Value.Trim();
                answer = AnswerCounterQuestion(sim, playerId, target);
            }

            // 2. Age progression questions: "how do i reach castle age", "how do i reach castle", "how do i age up"
            if (answer == null)
            {
                Match ageMatch = Regex.Match(normalized, @"^how do i reach (feudal|castle|imperial)(?: age)?$", RegexOptions.CultureInvariant);
                if (ageMatch.Success)
                {
                    string ageName = ageMatch.Groups[1].Value;
                    answer = AnswerAgeQuestion(sim, playerId, ageName);
                }
                else if (normalized == "how do i age up")
                {
                    int currentAge = sim.GetPlayerAge(playerId);
                    string nextAge = currentAge == 1 ? "feudal" : currentAge == 2 ? "castle" : "imperial";
                    answer = AnswerAgeQuestion(sim, playerId, nextAge);
                }
            }

            // 3. Cost questions: "how much does a barracks cost", "what does a barracks cost", "cost of a barracks", "how much does a knight cost"
            if (answer == null)
            {
                Match costMatch = Regex.Match(normalized, @"^(?:how much does (?:a |an )?(.+?) cost|what does (?:a |an )?(.+?) cost|cost of (?:a |an )?(.+))$", RegexOptions.CultureInvariant);
                if (costMatch.Success)
                {
                    string target = (costMatch.Groups[1].Success ? costMatch.Groups[1].Value
                        : costMatch.Groups[2].Success ? costMatch.Groups[2].Value
                        : costMatch.Groups[3].Value).Trim();
                    answer = AnswerCostQuestion(sim, playerId, target);
                }
            }

            // 4. Training location questions: "where do i train archers", "where do i make knights", "where are archers trained"
            if (answer == null)
            {
                Match trainMatch = Regex.Match(normalized, @"^(?:where do i (?:train|make)|where can i (?:train|make)|where are (.+?) trained)(?: (.+))?$", RegexOptions.CultureInvariant);
                if (trainMatch.Success)
                {
                    string target = (trainMatch.Groups[1].Success && !string.IsNullOrWhiteSpace(trainMatch.Groups[1].Value)
                        ? trainMatch.Groups[1].Value
                        : trainMatch.Groups[2].Value).Trim();
                    answer = AnswerTrainingLocationQuestion(sim, target);
                }
            }

            // 5. Civilization availability questions: "can my civilization make knights", "can we build knights", "can my civilization train knights"
            if (answer == null)
            {
                Match civMatch = Regex.Match(normalized, @"^can (?:my civilization|we|i) (?:make|train|build) (.+)$", RegexOptions.CultureInvariant);
                if (civMatch.Success)
                {
                    string target = civMatch.Groups[1].Value.Trim();
                    answer = AnswerCivAvailabilityQuestion(sim, playerId, target);
                }
            }

            // 6. Live-state activity: "what are you currently doing", "what are you doing"
            if (answer == null)
            {
                if (normalized == "what are you currently doing" || normalized == "what are you doing" || normalized == "what are you doing right now")
                {
                    answer = AnswerLiveActivityQuestion(semanticGoalManager);
                }
            }

            // 7. Live-state count: "how many villagers do i have", "how many spearmen do i have"
            if (answer == null)
            {
                Match countMatch = Regex.Match(normalized, @"^how many (.+?) do i have$", RegexOptions.CultureInvariant);
                if (countMatch.Success)
                {
                    string target = countMatch.Groups[1].Value.Trim();
                    answer = AnswerLiveCountQuestion(sim, playerId, target);
                }
            }

            return answer != null;
        }

        private static string AnswerCounterQuestion(GameSimulation sim, int playerId, string target)
        {
            var catalog = GameKnowledgeCatalog.Build(sim);
            var unit = catalog.FindUnit(target);
            if (unit == null)
                return $"I don't recognize '{target}' as a known unit.";

            switch (unit.UnitType)
            {
                case 1: // Spearman
                case 12: // Landsknecht
                    return "Archers counter Spearmen. Spearmen are effective against cavalry (Horsemen and Knights).";
                case 2: // Archer
                case 10: // Longbowman
                    return "Horsemen and Knights counter Archers. Archers are effective against Spearmen.";
                case 3: // Horseman
                case 7: // Knight
                case 11: // Gendarme
                    return "Spearmen counter cavalry. Cavalry are effective against ranged units like Archers.";
                case 6: // Man-at-Arms
                    return "Crossbowmen and Knights counter Man-at-Arms. Man-at-Arms are effective against Spearmen.";
                case 8: // Crossbowman
                    return "Horsemen and Knights counter Crossbowmen. Crossbowmen are effective against armored units like Man-at-Arms and Knights.";
                case 9: // Monk
                    return "Fast cavalry and ranged units counter Monks.";
                case 13: // Battering Ram
                    return "Melee units and Villagers counter Battering Rams. Rams counter buildings.";
                case 14: // Mangonel
                    return "Cavalry and spread-out units counter Mangonels.";
                case 15: // Trebuchet
                    return "Cavalry counters Trebuchets.";
                case 0: // Villager
                    return "Any military unit counters Villagers.";
                case 4: // Scout
                    return "Spearmen and heavy military counter Scouts.";
                default:
                    return unit.IsRanged
                        ? "Cavalry counters ranged units. Ranged units counter slow infantry."
                        : "Ranged units counter slow melee units.";
            }
        }

        private static string AnswerAgeQuestion(GameSimulation sim, int playerId, string ageName)
        {
            int targetAge = ageName == "feudal" ? 2 : ageName == "castle" ? 3 : ageName == "imperial" ? 4 : 0;
            if (targetAge == 0) return "Choose Feudal, Castle or Imperial Age for landmark advice.";
            int currentAge = sim.GetPlayerAge(playerId);
            string targetName = AgeDisplayName(targetAge);
            if (currentAge >= targetAge) return $"You have already reached {targetName} Age.";
            var civilization = sim.GetPlayerCivilization(playerId);
            var transitions = new List<string>();
            int totalFood = 0, totalGold = 0;
            bool exactTotal = true;
            for (int age = Math.Max(2, currentAge + 1); age <= targetAge; age++)
            {
                // GetChoices has an English fallback: never use it for an unavailable transition.
                if (!LandmarkDefinitions.HasChoices(civilization, age))
                    return $"{AgeDisplayName(age)} Age is not available for {civilization}: no canonical landmark choices are defined.";
                var choices = LandmarkDefinitions.GetChoices(civilization, age);
                var first = LandmarkDefinitions.Get(choices.a);
                var second = LandmarkDefinitions.Get(choices.b);
                string options = first.Name + " (" + LandmarkCost(first) + ") or "
                    + second.Name + " (" + LandmarkCost(second) + ")";
                transitions.Add($"{AgeDisplayName(age)} Age: build {options}");
                totalFood += first.FoodCost;
                totalGold += first.GoldCost;
                exactTotal &= first.FoodCost == second.FoodCost && first.GoldCost == second.GoldCost;
            }
            string answer = $"For {civilization}, advance in order from your current {AgeDisplayName(currentAge)} Age: "
                + string.Join("; ", transitions) + ". These are individual transition costs.";
            if (transitions.Count > 1)
                answer += exactTotal ? $" Reaching {targetName} from your current age requires {totalFood} Food and {totalGold} Gold total."
                    : " The cumulative cost depends on which landmarks you choose.";
            return answer;
        }

        private static string LandmarkCost(LandmarkDefinition definition)
            => $"{definition.FoodCost} Food and {definition.GoldCost} Gold";

        private static string AgeDisplayName(int age)
            => age == 1 ? "Dark" : age == 2 ? "Feudal" : age == 3 ? "Castle" : age == 4 ? "Imperial" : "unknown";

        private static string AnswerCostQuestion(GameSimulation sim, int playerId, string target)
        {
            var catalog = GameKnowledgeCatalog.Build(sim);
            var building = catalog.FindBuilding(target);
            if (building != null)
                return $"A {building.DisplayName} costs {FormatCost(building.Cost)}.";

            var effective = GameKnowledgeCatalog.BuildEffective(sim, playerId);
            var unit = effective.GetUnit(target);
            if (unit != null)
            {
                var cost = effective.GetEffectiveUnitCost(target);
                return $"A {unit.DisplayName} costs {FormatCost(cost)}.";
            }

            var tech = catalog.FindTechnology(target);
            if (tech != null)
                return $"{tech.DisplayName} costs {FormatCost(tech.Cost)}.";

            return $"I don't recognize '{target}' in the game catalog.";
        }

        private static string AnswerTrainingLocationQuestion(GameSimulation sim, string target)
        {
            var catalog = GameKnowledgeCatalog.Build(sim);
            var unit = catalog.FindUnit(target);
            if (unit == null)
                return $"I don't recognize '{target}' as a trainable unit.";

            var buildingType = (BuildingType)unit.ProductionBuildingType;
            string buildingName = buildingType.ToString();
            return $"{unit.DisplayName} are trained at the {buildingName} (requires Age {unit.RequiredAge}).";
        }

        private static string AnswerCivAvailabilityQuestion(GameSimulation sim, int playerId, string target)
        {
            var catalog = GameKnowledgeCatalog.Build(sim);
            var unit = catalog.FindUnit(target);
            var civ = sim.GetPlayerCivilization(playerId);
            if (unit == null)
            {
                var building = catalog.FindBuilding(target);
                if (building != null)
                    return $"Yes, {civ} can construct a {building.DisplayName} starting in Age {building.RequiredAge}.";
                return $"I don't recognize '{target}' in the game catalog.";
            }

            var civKnowledge = catalog.Civilizations.FirstOrDefault(c => c.Civilization == civ);
            if (civKnowledge != null && civKnowledge.AvailableUnitIds.Contains(unit.StableId))
                return $"Yes, {civ} can train {unit.DisplayName} starting in Age {unit.RequiredAge}.";

            int resolved = sim.ResolveCivUnitType(playerId, unit.UnitType);
            if (resolved != unit.UnitType)
            {
                var rep = catalog.FindUnitByType(resolved);
                return $"No, {civ} replaces {unit.DisplayName} with {rep?.DisplayName ?? "a unique unit"}.";
            }
            return $"No, {civ} cannot train {unit.DisplayName}.";
        }

        private static string AnswerLiveActivityQuestion(CommanderGoalManager manager)
        {
            if (manager == null) return "No active Commander goals are currently running.";
            return CommanderTacticalStatusProjection.Observe(manager.Simulation,manager,"what are you doing").Answer();
        }

        private static string AnswerLiveCountQuestion(GameSimulation sim, int playerId, string target)
        {
            string lower = target.ToLowerInvariant();
            if (lower == "villager" || lower == "villagers" || lower == "worker" || lower == "workers")
            {
                int count = 0;
                var units = sim.UnitRegistry.GetAllUnits();
                for (int i = 0; i < units.Count; i++)
                {
                    var u = units[i];
                    if (u != null && u.PlayerId == playerId && u.IsVillager && u.CurrentHealth > 0 && u.State != UnitState.Dead)
                        count++;
                }
                return $"You currently have {count} living Villagers.";
            }

            var catalog = GameKnowledgeCatalog.Build(sim);
            var unit = catalog.FindUnit(target);
            if (unit != null)
            {
                int count = 0;
                int resolved = sim.ResolveCivUnitType(playerId, unit.UnitType);
                var units = sim.UnitRegistry.GetAllUnits();
                for (int i = 0; i < units.Count; i++)
                {
                    var u = units[i];
                    if (u != null && u.PlayerId == playerId && u.UnitType == resolved && u.CurrentHealth > 0 && u.State != UnitState.Dead)
                        count++;
                }
                return $"You currently have {count} living {unit.DisplayName}(s).";
            }

            var building = catalog.FindBuilding(target);
            if (building != null)
            {
                int count = 0;
                var buildings = sim.BuildingRegistry.GetAllBuildings();
                for (int i = 0; i < buildings.Count; i++)
                {
                    var b = buildings[i];
                    if (b != null && b.PlayerId == playerId && !b.IsDestroyed && !b.IsUnderConstruction
                        && sim.GetEffectiveBuildingType(b) == building.BuildingType)
                        count++;
                }
                return $"You currently have {count} completed {building.DisplayName}(s).";
            }

            return $"I don't recognize '{target}' to count.";
        }

        private static string FormatCost(KnowledgeCost cost)
        {
            if (cost == null) return "0 resources";
            var parts = new List<string>(4);
            if (cost.Food > 0) parts.Add($"{cost.Food} Food");
            if (cost.Wood > 0) parts.Add($"{cost.Wood} Wood");
            if (cost.Gold > 0) parts.Add($"{cost.Gold} Gold");
            if (cost.Stone > 0) parts.Add($"{cost.Stone} Stone");
            return parts.Count > 0 ? string.Join(" and ", parts) : "0 resources";
        }
    }
}
