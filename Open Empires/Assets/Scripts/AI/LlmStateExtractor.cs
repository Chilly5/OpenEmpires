using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace OpenEmpires
{
    // Builds a compact natural-language game-state snapshot for the LLM prompt. Reads
    // only — never writes. Runs on the typing client; result is sent in the prompt,
    // never networked. Stays under ~500 tokens.
    public static class LlmStateExtractor
    {
        public static string Build(GameSimulation sim, int localPlayerId, int aiPlayerId)
        {
            if (sim == null) return string.Empty;
            var sb = new StringBuilder(768);

            int tick = sim.CurrentTick;
            int minutes = tick / (30 * 60);
            int seconds = (tick / 30) % 60;
            sb.Append("Time=").Append(minutes).Append(":").Append(seconds.ToString("D2"))
              .Append(" (tick ").Append(tick).Append("). ");

            AppendPlayerLine(sb, sim, localPlayerId, "Human");
            AppendAiLine(sb, sim, aiPlayerId);
            AppendGathererBreakdown(sb, sim, aiPlayerId);
            AppendProductionQueues(sb, sim, aiPlayerId);
            AppendBuildings(sb, sim, aiPlayerId);
            AppendUpgrades(sb, sim, aiPlayerId);

            // Per-unit-class enemy composition from the AI's perspective (what its scouts/units have seen).
            var ai = sim.GetAiPlayer(aiPlayerId);
            if (ai != null)
            {
                sb.Append("Enemy-army-seen: spearmen=").Append(ai.CachedEnemySpearmen)
                  .Append(" archers=").Append(ai.CachedEnemyArchers)
                  .Append(" horsemen=").Append(ai.CachedEnemyHorsemen)
                  .Append(". ");
                AppendBattleStrength(sb, sim, ai, aiPlayerId);
                AppendRecentEvents(sb, ai, tick);
            }

            // Enemy base summary with rough cardinal direction relative to AI's base.
            AppendEnemyBases(sb, sim, localPlayerId, aiPlayerId);

            return sb.ToString();
        }

        // The AI's buildings (with counts), marking idle production buildings — tells the model
        // what it can train/research and what's sitting idle.
        private static void AppendBuildings(StringBuilder sb, GameSimulation sim, int aiPlayerId)
        {
            var counts = new Dictionary<BuildingType, int>();
            var idle = new HashSet<BuildingType>();
            var buildings = sim.BuildingRegistry.GetAllBuildings();
            for (int i = 0; i < buildings.Count; i++)
            {
                var b = buildings[i];
                if (b.PlayerId != aiPlayerId || b.IsDestroyed) continue;
                counts.TryGetValue(b.Type, out int c);
                counts[b.Type] = c + 1;
                if (!b.IsUnderConstruction && IsProductionType(b.Type) && (b.TrainingQueue == null || b.TrainingQueue.Count == 0))
                    idle.Add(b.Type);
            }

            sb.Append("Buildings: ");
            if (counts.Count == 0) { sb.Append("none. "); return; }
            bool first = true;
            foreach (var kv in counts)
            {
                if (!first) sb.Append(", ");
                sb.Append(BuildingShortName(kv.Key));
                if (kv.Value > 1) sb.Append(" x").Append(kv.Value);
                if (idle.Contains(kv.Key)) sb.Append("(idle)");
                first = false;
            }
            sb.Append(". ");
        }

        private static bool IsProductionType(BuildingType t)
            => t == BuildingType.TownCenter || t == BuildingType.Barracks || t == BuildingType.ArcheryRange
            || t == BuildingType.Stables || t == BuildingType.Monastery || t == BuildingType.SiegeWorkshop;

        private static string BuildingShortName(BuildingType t)
        {
            switch (t)
            {
                case BuildingType.TownCenter: return "TC";
                case BuildingType.House: return "house";
                case BuildingType.Barracks: return "barracks";
                case BuildingType.ArcheryRange: return "archery";
                case BuildingType.Stables: return "stables";
                case BuildingType.Mill: return "mill";
                case BuildingType.LumberYard: return "lumber-yard";
                case BuildingType.Mine: return "mine";
                case BuildingType.Farm: return "farm";
                case BuildingType.Blacksmith: return "blacksmith";
                case BuildingType.Market: return "market";
                case BuildingType.Monastery: return "monastery";
                case BuildingType.University: return "university";
                case BuildingType.Tower: return "tower";
                case BuildingType.Keep: return "keep";
                case BuildingType.SiegeWorkshop: return "siege-workshop";
                case BuildingType.Wall: return "wall";
                case BuildingType.Landmark: return "landmark";
                case BuildingType.Wonder: return "wonder";
                default: return t.ToString().ToLowerInvariant();
            }
        }

        // Researched upgrades the AI has (own only — enemy upgrades aren't observable through fog).
        private static void AppendUpgrades(StringBuilder sb, GameSimulation sim, int aiPlayerId)
        {
            bool any = false;
            foreach (TechnologyType tech in System.Enum.GetValues(typeof(TechnologyType)))
            {
                if (!sim.HasTechnology(aiPlayerId, tech)) continue;
                sb.Append(any ? ", " : "Upgrades: ").Append(TechShortName(tech));
                any = true;
            }
            if (any) sb.Append(". ");
        }

        private static string TechShortName(TechnologyType t)
        {
            switch (t)
            {
                case TechnologyType.BlacksmithDamage: return "blacksmith-attack";
                case TechnologyType.BlacksmithDefense: return "blacksmith-armor";
                case TechnologyType.Ballistics: return "ballistics";
                case TechnologyType.SiegeEngineering: return "siege-engineering";
                case TechnologyType.Chemistry: return "chemistry";
                case TechnologyType.MurderHoles: return "murder-holes";
                default: return t.ToString().ToLowerInvariant();
            }
        }

        // Counter-aware read of the AI's army vs the enemy army it has seen, plus battle status.
        private static void AppendBattleStrength(StringBuilder sb, GameSimulation sim, AIPlayerSystem ai, int aiPlayerId)
        {
            int mS = 0, mA = 0, mH = 0;
            var all = sim.UnitRegistry.GetAllUnits();
            for (int i = 0; i < all.Count; i++)
            {
                var u = all[i];
                if (u.PlayerId != aiPlayerId || u.State == UnitState.Dead) continue;
                if (u.UnitType == 1 || u.UnitType == 6 || u.UnitType == 12) mS++;
                else if (u.UnitType == 2 || u.UnitType == 8 || u.UnitType == 10) mA++;
                else if (u.UnitType == 3 || u.UnitType == 7 || u.UnitType == 11) mH++;
            }
            int myCount = mS + mA + mH;
            int eS = ai.CachedEnemySpearmen, eA = ai.CachedEnemyArchers, eH = ai.CachedEnemyHorsemen;
            int eCount = eS + eA + eH;

            string verdict;
            if (eCount == 0) verdict = myCount > 0 ? "no enemy army seen" : "no armies";
            else
            {
                // Same counter-weighting as the reactive combat assessment.
                int d = (eH >= eS && eH >= eA) ? 2 : (eA >= eS ? 1 : 3); // enemy dominant: 1=archer,2=cav,3=spear
                int strong = d == 1 ? mH : d == 2 ? mS : mA;
                int weak = d == 1 ? mS : d == 2 ? mA : mH;
                int myEff = myCount * 10 + strong * 5 - weak * 5;
                int enEff = eCount * 10;
                verdict = myEff >= enEff * 12 / 10 ? "you're ahead"
                        : enEff >= myEff * 12 / 10 ? "outmatched" : "roughly even";
            }
            sb.Append("Army-strength: you=").Append(myCount).Append(" vs enemy-seen=").Append(eCount)
              .Append(" (").Append(verdict).Append("). Battle: ").Append(ai.EngagementStatus).Append(". ");
        }

        private static void AppendRecentEvents(StringBuilder sb, AIPlayerSystem ai, int currentTick)
        {
            var events = ai.RecentEvents;
            if (events == null || events.Count == 0) return;
            sb.Append("Recent: ");
            for (int i = 0; i < events.Count; i++)
            {
                if (i > 0) sb.Append("; ");
                int ago = (currentTick - events[i].Tick) / 30; // seconds
                sb.Append('[').Append(ago).Append("s] ").Append(EventText(events[i].Code, events[i].Mag));
            }
            sb.Append(". ");
        }

        private static string EventText(int code, int mag)
        {
            switch (code)
            {
                case AIPlayerSystem.EvAgedUp: return "aged up to " + mag;
                case AIPlayerSystem.EvLostUnits: return "lost " + mag + " units";
                case AIPlayerSystem.EvLostBuilding: return "lost a building";
                case AIPlayerSystem.EvEnemySpotted: return "spotted enemy base";
                case AIPlayerSystem.EvRaided: return "base under attack";
                case AIPlayerSystem.EvEngaged: return "army engaged enemy";
                case AIPlayerSystem.EvRetreating: return "army retreating (outmatched)";
                default: return "event";
            }
        }

        // What the AI currently has training in its production buildings (so it can answer
        // "what's in queue?" and honor "clear the queue / stop production").
        private static void AppendProductionQueues(StringBuilder sb, GameSimulation sim, int aiPlayerId)
        {
            var counts = new Dictionary<int, int>();
            bool autoVills = false;
            var buildings = sim.BuildingRegistry.GetAllBuildings();
            for (int i = 0; i < buildings.Count; i++)
            {
                var b = buildings[i];
                if (b.PlayerId != aiPlayerId || b.IsDestroyed) continue;
                if (b.Type == BuildingType.TownCenter && !b.IsUnderConstruction && b.AutoProduceVillagers)
                    autoVills = true;
                var q = b.TrainingQueue;
                if (q == null) continue;
                for (int j = 0; j < q.Count; j++)
                {
                    counts.TryGetValue(q[j], out int c);
                    counts[q[j]] = c + 1;
                }
            }

            sb.Append("Queues: ");
            bool any = false;
            var keys = new List<int>(counts.Keys);
            keys.Sort();
            for (int k = 0; k < keys.Count; k++)
            {
                if (any) sb.Append(", ");
                sb.Append(UnitShortName(keys[k])).Append(" x").Append(counts[keys[k]]);
                any = true;
            }
            if (autoVills) { sb.Append(any ? ", " : "").Append("auto-villagers ON"); any = true; }
            if (!any) sb.Append("empty");
            sb.Append(". ");
        }

        private static string UnitShortName(int t)
        {
            switch (t)
            {
                case 0: return "villager";
                case 1: case 12: return "spearman";
                case 2: case 10: return "archer";
                case 3: case 11: return "horseman";
                case 4: return "scout";
                case 6: return "man-at-arms";
                case 7: return "knight";
                case 8: return "crossbowman";
                case 9: return "monk";
                case 13: return "ram";
                case 14: return "mangonel";
                case 15: return "trebuchet";
                default: return "unit";
            }
        }

        // The AI teammate's villager distribution by current activity, so the model can honor
        // orders like "move your berry gatherers". Food is split into berries / farm / hunt.
        private static void AppendGathererBreakdown(StringBuilder sb, GameSimulation sim, int aiPlayerId)
        {
            int food = 0, berries = 0, farm = 0, hunt = 0, wood = 0, gold = 0, stone = 0, idle = 0, other = 0;
            var all = sim.UnitRegistry.GetAllUnits();
            for (int i = 0; i < all.Count; i++)
            {
                var u = all[i];
                if (u.PlayerId != aiPlayerId || u.State == UnitState.Dead) continue;
                if (u.UnitType != 0) continue; // villagers only
                if (u.State == UnitState.Idle) { idle++; continue; }

                var node = u.TargetResourceNodeId >= 0 ? sim.MapData.GetResourceNode(u.TargetResourceNodeId) : null;
                ResourceType rt;
                if (node != null)
                {
                    rt = node.Type;
                    if (rt == ResourceType.Food)
                    {
                        food++;
                        if (node.IsFarmNode) farm++;
                        else if (node.IsCarcass) hunt++;
                        else berries++;
                        continue;
                    }
                }
                else if (u.CarriedResourceAmount > 0)
                {
                    rt = u.CarriedResourceType;
                    if (rt == ResourceType.Food) { food++; berries++; continue; } // carry-only food → assume bush
                }
                else { other++; continue; }

                if (rt == ResourceType.Wood) wood++;
                else if (rt == ResourceType.Gold) gold++;
                else if (rt == ResourceType.Stone) stone++;
                else other++;
            }

            sb.Append("Gatherers: food=").Append(food)
              .Append("(berries=").Append(berries).Append(",farm=").Append(farm).Append(",hunt=").Append(hunt).Append(')')
              .Append(" wood=").Append(wood).Append(" gold=").Append(gold).Append(" stone=").Append(stone)
              .Append(" idle=").Append(idle);
            if (other > 0) sb.Append(" other=").Append(other);
            sb.Append(". ");
        }

        private static void AppendPlayerLine(StringBuilder sb, GameSimulation sim, int playerId, string label)
        {
            var res = sim.ResourceManager.GetPlayerResources(playerId);
            int age = sim.GetPlayerAge(playerId);
            int pop = sim.GetPopulation(playerId);
            int popCap = sim.GetPopulationCap(playerId);

            int villagers = 0, military = 0;
            int archers = 0, horsemen = 0, spearmen = 0;
            var all = sim.UnitRegistry.GetAllUnits();
            for (int i = 0; i < all.Count; i++)
            {
                var u = all[i];
                if (u.PlayerId != playerId) continue;
                if (u.State == UnitState.Dead) continue;
                if (u.UnitType == 0) { villagers++; continue; }
                if (u.UnitType == 4 || u.IsSheep) continue;
                military++;
                if (u.UnitType == 2 || u.UnitType == 10) archers++;
                else if (u.UnitType == 3 || u.UnitType == 11) horsemen++;
                else if (u.UnitType == 1 || u.UnitType == 12) spearmen++;
            }

            sb.Append(label).Append(": age=").Append(age)
              .Append(" pop=").Append(pop).Append('/').Append(popCap)
              .Append(" vills=").Append(villagers)
              .Append(" army=").Append(military)
              .Append("(arch=").Append(archers).Append(",horse=").Append(horsemen).Append(",spear=").Append(spearmen).Append(')')
              .Append(" food=").Append(res.Food)
              .Append(" wood=").Append(res.Wood)
              .Append(" gold=").Append(res.Gold)
              .Append(" stone=").Append(res.Stone)
              .Append(". ");
        }

        private static void AppendAiLine(StringBuilder sb, GameSimulation sim, int aiPlayerId)
        {
            AppendPlayerLine(sb, sim, aiPlayerId, "AI-teammate");
            var ai = sim.GetAiPlayer(aiPlayerId);
            if (ai != null)
            {
                sb.Append("AI-state: ").Append(ai.CombatStateName).Append(". ");
                if (ai.ActiveGroupCount > 0)
                    sb.Append("Detached-groups: ").Append(ai.ActiveGroupCount)
                      .Append(" out acting independently. ");
                if (ai.VillagerOrderCount > 0)
                    sb.Append("Villager-orders: ").Append(ai.VillagerOrderCount).Append(" active. ");
            }
        }

        private static void AppendEnemyBases(StringBuilder sb, GameSimulation sim, int localPlayerId, int aiPlayerId)
        {
            FixedVector3? anchor = null;
            if (sim.FirstTownCenterIds.TryGetValue(aiPlayerId, out int aiTcId))
            {
                var aiTc = sim.BuildingRegistry.GetBuilding(aiTcId);
                if (aiTc != null && !aiTc.IsDestroyed) anchor = aiTc.SimPosition;
            }

            var keys = new List<int>(sim.FirstTownCenterIds.Keys);
            keys.Sort();
            int count = 0;
            for (int i = 0; i < keys.Count; i++)
            {
                int pid = keys[i];
                if (pid == localPlayerId || pid == aiPlayerId) continue;
                if (sim.AreAllies(localPlayerId, pid)) continue;
                var tc = sim.BuildingRegistry.GetBuilding(sim.FirstTownCenterIds[pid]);
                if (tc == null || tc.IsDestroyed) continue;

                if (count == 0) sb.Append("Known-enemy-bases: ");
                else sb.Append(", ");
                sb.Append("p").Append(pid).Append("(");
                if (anchor.HasValue) sb.Append(DirectionFrom(anchor.Value, tc.SimPosition)).Append(", ");
                sb.Append("age").Append(sim.GetPlayerAge(pid)).Append(')');
                count++;
            }
            if (count == 0) sb.Append("Known-enemy-bases: none-spotted.");
            else sb.Append('.');
        }

        // Coarse cardinal direction from `from` to `to`.
        private static string DirectionFrom(FixedVector3 from, FixedVector3 to)
        {
            int dx = to.x.Raw - from.x.Raw;
            int dz = to.z.Raw - from.z.Raw;
            bool eastWest = Mathf.Abs(dx) > Mathf.Abs(dz);
            if (eastWest) return dx > 0 ? "east" : "west";
            return dz > 0 ? "north" : "south";
        }
    }
}
