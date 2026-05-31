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
            AppendAllyAis(sb, sim, aiPlayerId);

            // Per-unit-class enemy composition from the AI's perspective (what its scouts/units have seen).
            var ai = sim.GetAiPlayer(aiPlayerId);
            if (ai != null)
            {
                sb.Append("Enemy-army-seen: spearmen=").Append(ai.CachedEnemySpearmen)
                  .Append(" archers=").Append(ai.CachedEnemyArchers)
                  .Append(" horsemen=").Append(ai.CachedEnemyHorsemen)
                  .Append(". ");
            }

            // Enemy base summary with rough cardinal direction relative to AI's base.
            AppendEnemyBases(sb, sim, localPlayerId, aiPlayerId);

            return sb.ToString();
        }

        // Lists the OTHER ally AI teammates and their combat state, so a bot can coordinate /
        // avoid duplicating a teammate when the human broadcasts an order to several bots.
        private static void AppendAllyAis(StringBuilder sb, GameSimulation sim, int aiPlayerId)
        {
            bool any = false;
            foreach (var pid in sim.AiPlayerIds)
            {
                if (pid == aiPlayerId) continue;
                if (!sim.AreAllies(pid, aiPlayerId)) continue;
                var other = sim.GetAiPlayer(pid);
                if (other == null) continue;
                sb.Append(any ? ", " : "Ally-AIs: ").Append("AI Player ").Append(pid)
                  .Append('(').Append(other.CombatStateName).Append(')');
                any = true;
            }
            if (any) sb.Append(". ");
        }

        private static void AppendPlayerLine(StringBuilder sb, GameSimulation sim, int playerId, string label)
        {
            var res = sim.ResourceManager.GetPlayerResources(playerId);
            int age = sim.GetPlayerAge(playerId);
            int pop = sim.GetPopulation(playerId);

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
              .Append(" pop=").Append(pop)
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
                sb.Append("p").Append(pid);
                if (anchor.HasValue)
                {
                    sb.Append('(').Append(DirectionFrom(anchor.Value, tc.SimPosition)).Append(')');
                }
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
