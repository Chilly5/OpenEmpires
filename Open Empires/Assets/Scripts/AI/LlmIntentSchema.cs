using System;
using System.Collections.Generic;
using UnityEngine;

namespace OpenEmpires
{
    // Deterministic world-resolution helpers shared by the tool dispatcher. The LLM
    // never names raw map coordinates — it picks from a small keyword set
    // ("enemy_base", "my_base", "near_my_barracks", ...) which these methods resolve
    // through deterministic sim reads on the owner client. The resolved ints ride
    // inside AiIntentCommand and replicate to every client.
    //
    // (Previously this file also held a JSON-mode intent parser and a keyword-based
    // reply→intent reconciler. Both are obsolete under native function calling: the
    // model now emits explicit tool calls, so there is nothing to guess.)
    public static class LlmIntentSchema
    {
        public const int MaxIntentsPerTurn = 8;
        public const int MinDurationTicks = 150;   // 5s
        public const int MaxDurationTicks = 5400;  // 3min
        public const int MaxReplyChars = 400;

        // Unit-class filter for AttackAt/DefendAt's ParamC.
        public const int UnitClassAll      = 0;
        public const int UnitClassArchers  = 1;
        public const int UnitClassHorsemen = 2;
        public const int UnitClassSpearmen = 3;

        // Trigger types (mirrors AIPlayerSystem.PendingDirective handling).
        public const int TriggerImmediate     = 0;
        public const int TriggerDelay          = 1;
        public const int TriggerOnAgeUp        = 2;
        public const int TriggerOnArmySize     = 3;
        public const int TriggerOnEnemyAttack  = 4;

        public struct ParsedIntent
        {
            public AiIntentKind Kind;
            public int ParamA, ParamB, ParamC, ParamD;
            public int DurationTicks;
            public int TriggerType;
            public int TriggerMagnitude;
        }

        public static int ParseUnitSubset(string s)
        {
            if (string.IsNullOrEmpty(s)) return UnitClassAll;
            switch (s.ToLowerInvariant())
            {
                case "archers":  return UnitClassArchers;
                case "horsemen": return UnitClassHorsemen;
                case "cavalry":  return UnitClassHorsemen;
                case "spearmen": return UnitClassSpearmen;
                case "infantry": return UnitClassSpearmen;
            }
            return UnitClassAll;
        }

        // Villager-activity source filter (matches AIPlayerSystem.VillagerMatchesActivity codes).
        public const int VillagerSourceAny = 0, VillagerSourceIdle = 1, VillagerSourceFood = 2,
            VillagerSourceBerries = 3, VillagerSourceFarm = 4, VillagerSourceHunt = 5,
            VillagerSourceWood = 6, VillagerSourceGold = 7, VillagerSourceStone = 8;

        // Returns the source code; "any" (or unknown) → 0 = nearest-N selection (no filter).
        public static int ParseVillagerSource(string s)
        {
            switch ((s ?? string.Empty).ToLowerInvariant())
            {
                case "idle":                       return VillagerSourceIdle;
                case "food":                       return VillagerSourceFood;
                case "berries": case "berry": case "bush": return VillagerSourceBerries;
                case "farm": case "farms":         return VillagerSourceFarm;
                case "hunt": case "hunters": case "meat": return VillagerSourceHunt;
                case "wood": case "lumber":        return VillagerSourceWood;
                case "gold":                       return VillagerSourceGold;
                case "stone":                      return VillagerSourceStone;
            }
            return VillagerSourceAny;
        }

        // Packs a current-activity source + count into one int for an AiIntentCommand param:
        // source*1000 + count (count 0 = "all of that source").
        public static int PackVillagerSelector(int sourceCode, int count)
        {
            if (count < 0) count = 0;
            if (count > 999) count = 999;
            return sourceCode * 1000 + count;
        }

        // Portion of the available units of a class to detach, as a percent 1..100.
        // "rest"/"all" = 100 (everything still ungrouped), so "half" then "rest" halves cleanly.
        public static int ParsePortion(string s)
        {
            switch ((s ?? string.Empty).ToLowerInvariant())
            {
                case "half":        return 50;
                case "third":       return 33;
                case "quarter":     return 25;
                case "two_thirds":  return 67;
                case "rest":        return 100;
                case "all":         return 100;
            }
            return 100;
        }

        public static bool TryParseResource(string s, out ResourceType resType)
        {
            switch ((s ?? string.Empty).ToLowerInvariant())
            {
                case "food":  resType = ResourceType.Food;  return true;
                case "wood":  resType = ResourceType.Wood;  return true;
                case "gold":  resType = ResourceType.Gold;  return true;
                case "stone": resType = ResourceType.Stone; return true;
            }
            resType = ResourceType.Food;
            return false;
        }

        // Convert a trigger keyword + value into (type, magnitude). Magnitude units:
        //   delay → ticks, on_age_up → target age, on_army_size → unit count.
        public static void ResolveTrigger(string trigger, int triggerValue,
            out int triggerType, out int triggerMagnitude)
        {
            triggerType = TriggerImmediate;
            triggerMagnitude = 0;
            switch ((trigger ?? string.Empty).ToLowerInvariant())
            {
                case "delay":
                    triggerType = TriggerDelay;
                    triggerMagnitude = Mathf.Clamp(triggerValue * 30, 0, 15 * 60 * 30);
                    break;
                case "on_age_up":
                    triggerType = TriggerOnAgeUp;
                    triggerMagnitude = Mathf.Clamp(triggerValue, 2, 3);
                    break;
                case "on_army_size":
                    triggerType = TriggerOnArmySize;
                    triggerMagnitude = Mathf.Clamp(triggerValue, 1, 100);
                    break;
                case "on_enemy_attack":
                    triggerType = TriggerOnEnemyAttack;
                    break;
            }
        }

        // ── Position resolution ─────────────────────────────────────────
        // Resolves a position keyword to raw Fixed32 world coords. Deterministic
        // ordering by playerId / building Id so every client would agree (though this
        // runs only on the owner client during tool dispatch).
        public static bool TryResolvePosition(string target, GameSimulation sim, int aiPlayerId,
            out int rawX, out int rawZ)
        {
            rawX = 0; rawZ = 0;
            if (string.IsNullOrEmpty(target) || sim == null) return false;
            target = target.ToLowerInvariant();

            FixedVector3? enemyTc = FindEnemyTc(sim, aiPlayerId);
            FixedVector3? ownTc = FindOwnTc(sim, aiPlayerId);

            switch (target)
            {
                case "enemy_base":
                    if (!enemyTc.HasValue) return false;
                    rawX = enemyTc.Value.x.Raw; rawZ = enemyTc.Value.z.Raw;
                    return true;
                case "my_base":
                    if (!ownTc.HasValue) return false;
                    rawX = ownTc.Value.x.Raw; rawZ = ownTc.Value.z.Raw;
                    return true;
                case "front_line":
                    if (!enemyTc.HasValue || !ownTc.HasValue) return false;
                    rawX = (enemyTc.Value.x.Raw + ownTc.Value.x.Raw) / 2;
                    rawZ = (enemyTc.Value.z.Raw + ownTc.Value.z.Raw) / 2;
                    return true;
            }

            if (target == "north" || target == "south" || target == "east" || target == "west")
            {
                if (sim.MapData == null) return false;
                float w = sim.MapData.Width;
                float h = sim.MapData.Height;
                int tileX, tileZ;
                switch (target)
                {
                    case "north": tileX = (int)(w * 0.5f); tileZ = (int)(h * 0.8f); break;
                    case "south": tileX = (int)(w * 0.5f); tileZ = (int)(h * 0.2f); break;
                    case "east":  tileX = (int)(w * 0.8f); tileZ = (int)(h * 0.5f); break;
                    default:      tileX = (int)(w * 0.2f); tileZ = (int)(h * 0.5f); break;
                }
                var pos = sim.MapData.TileToWorldFixed(tileX, tileZ);
                rawX = pos.x.Raw; rawZ = pos.z.Raw;
                return true;
            }

            const string myPrefix = "near_my_";
            const string theirPrefix = "near_their_";
            bool mine = target.StartsWith(myPrefix, StringComparison.Ordinal);
            bool theirs = !mine && target.StartsWith(theirPrefix, StringComparison.Ordinal);
            if (mine || theirs)
            {
                string suffix = target.Substring(mine ? myPrefix.Length : theirPrefix.Length);
                if (!TryParseBuildingTypeKeyword(suffix, out var btype)) return false;
                return TryResolveBuildingPosition(sim, aiPlayerId, btype, mine, out rawX, out rawZ);
            }

            // Resource-node keywords: my_<res> / their_<res> → nearest node of that type to
            // our / the enemy town center. Used for villager gather targets.
            if (target.StartsWith("my_", StringComparison.Ordinal) || target.StartsWith("their_", StringComparison.Ordinal))
            {
                bool ours = target.StartsWith("my_", StringComparison.Ordinal);
                string resWord = target.Substring(ours ? 3 : 6);
                if (TryParseResource(resWord, out var resType))
                {
                    FixedVector3? anchor = ours ? ownTc : enemyTc;
                    if (!anchor.HasValue) return false;
                    return TryResolveNearestResourceNode(sim, resType, anchor.Value, out rawX, out rawZ);
                }
            }

            return false;
        }

        public static bool TryParseBuildingTypeKeyword(string s, out BuildingType type)
        {
            switch ((s ?? string.Empty).ToLowerInvariant())
            {
                case "tc":              type = BuildingType.TownCenter;    return true;
                case "town_center":     type = BuildingType.TownCenter;    return true;
                case "house":           type = BuildingType.House;         return true;
                case "barracks":        type = BuildingType.Barracks;      return true;
                case "archery":         type = BuildingType.ArcheryRange;  return true;
                case "archery_range":   type = BuildingType.ArcheryRange;  return true;
                case "stables":         type = BuildingType.Stables;       return true;
                case "landmark":        type = BuildingType.Landmark;      return true;
                case "mill":            type = BuildingType.Mill;          return true;
                case "mine":            type = BuildingType.Mine;          return true;
                case "lumber_yard":     type = BuildingType.LumberYard;    return true;
                case "farm":            type = BuildingType.Farm;          return true;
                case "tower":           type = BuildingType.Tower;         return true;
                case "keep":            type = BuildingType.Keep;          return true;
                case "blacksmith":      type = BuildingType.Blacksmith;    return true;
                case "market":          type = BuildingType.Market;        return true;
                case "monastery":       type = BuildingType.Monastery;     return true;
                case "siege_workshop":  type = BuildingType.SiegeWorkshop; return true;
                case "university":      type = BuildingType.University;    return true;
                case "wonder":          type = BuildingType.Wonder;        return true;
            }
            type = default;
            return false;
        }

        private static bool TryResolveBuildingPosition(GameSimulation sim, int aiPlayerId,
            BuildingType type, bool mine, out int rawX, out int rawZ)
        {
            rawX = 0; rawZ = 0;
            var buildings = sim.BuildingRegistry.GetAllBuildings();
            int bestId = int.MaxValue;
            FixedVector3 bestPos = default;
            bool found = false;
            for (int i = 0; i < buildings.Count; i++)
            {
                var b = buildings[i];
                if (b.IsDestroyed) continue;
                if (b.Type != type) continue;
                bool isMine = b.PlayerId == aiPlayerId;
                bool isAlly = sim.AreAllies(b.PlayerId, aiPlayerId);
                if (mine) { if (!isMine) continue; }
                else      { if (isMine || isAlly) continue; }
                if (b.Id < bestId)
                {
                    bestId = b.Id;
                    bestPos = b.SimPosition;
                    found = true;
                }
            }
            if (!found) return false;
            rawX = bestPos.x.Raw;
            rawZ = bestPos.z.Raw;
            return true;
        }

        private static bool TryResolveNearestResourceNode(GameSimulation sim, ResourceType type,
            FixedVector3 anchor, out int rawX, out int rawZ)
        {
            rawX = 0; rawZ = 0;
            var nodes = sim.MapData.GetAllResourceNodesSorted();
            int bestDistSq = int.MaxValue;
            FixedVector3 bestPos = default;
            bool found = false;
            for (int i = 0; i < nodes.Count; i++)
            {
                var n = nodes[i];
                if (n.Type != type) continue;
                if (n.IsDepleted) continue;
                int dx = n.Position.x.Raw - anchor.x.Raw;
                int dz = n.Position.z.Raw - anchor.z.Raw;
                long distSq = (long)dx * dx + (long)dz * dz;
                if (distSq < bestDistSq)
                {
                    bestDistSq = (int)Mathf.Min(int.MaxValue, distSq);
                    bestPos = n.Position;
                    found = true;
                }
            }
            if (!found) return false;
            rawX = bestPos.x.Raw;
            rawZ = bestPos.z.Raw;
            return true;
        }

        public static FixedVector3? FindOwnTc(GameSimulation sim, int aiPlayerId)
        {
            if (sim.FirstTownCenterIds.TryGetValue(aiPlayerId, out int tcId))
            {
                var tc = sim.BuildingRegistry.GetBuilding(tcId);
                if (tc != null && !tc.IsDestroyed) return tc.SimPosition;
            }
            return null;
        }

        public static FixedVector3? FindEnemyTc(GameSimulation sim, int aiPlayerId)
        {
            var keys = new List<int>(sim.FirstTownCenterIds.Keys);
            keys.Sort();
            for (int i = 0; i < keys.Count; i++)
            {
                int pid = keys[i];
                if (pid == aiPlayerId) continue;
                if (sim.AreAllies(pid, aiPlayerId)) continue;
                var tc = sim.BuildingRegistry.GetBuilding(sim.FirstTownCenterIds[pid]);
                if (tc != null && !tc.IsDestroyed) return tc.SimPosition;
            }
            return null;
        }
    }
}
