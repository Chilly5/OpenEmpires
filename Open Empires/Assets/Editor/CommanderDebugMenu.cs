#if UNITY_EDITOR
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace OpenEmpires.EditorTools
{
    public static class CommanderDebugMenu
    {
        private static int humanQaWorkerId = -1;
        private static int humanQaResourceNodeId = -1;

        [MenuItem("Open Empires/Commander/Command Window")]
        public static void OpenCommandWindow()
        {
            CommanderIntentDebugWindow.Open();
        }

        [MenuItem("Open Empires/Commander/Start Local QA Match")]
        public static void StartLocalQaMatch()
        {
            if (!EditorApplication.isPlaying)
            {
                Debug.LogWarning("[Commander] Enter Play Mode before starting the local QA match.");
                return;
            }

            GameBootstrapper bootstrapper = GameBootstrapper.Instance;
            NetworkManager network = bootstrapper != null ? bootstrapper.Network : null;
            if (bootstrapper == null || network == null)
            {
                Debug.LogError("[Commander] GameBootstrapper or NetworkManager is unavailable.");
                return;
            }

            bootstrapper.SetPlayerCount(2);
            bootstrapper.SetAIPlayerIds(new[] { 1 });
            bootstrapper.SetTeamAssignments(new[] { 0, 1 });
            typeof(NetworkManager).GetProperty(nameof(NetworkManager.GameStarted),
                BindingFlags.Instance | BindingFlags.Public)?.SetValue(network, true);
            Time.timeScale = 10f;
            Debug.Log("[Commander] Local 1v1 QA match enabled at 10x time scale.");
        }

        [MenuItem("Open Empires/Commander/Reset Time Scale")]
        public static void ResetTimeScale()
        {
            Time.timeScale = 1f;
            Debug.Log("[Commander] Time scale reset to 1x.");
        }

        [MenuItem("Open Empires/Commander/Ensure 10 Spearmen")]
        public static void EnsureTenSpearmen()
        {
            GameBootstrapper bootstrapper = GameBootstrapper.Instance;
            if (bootstrapper == null || bootstrapper.Commander == null)
            {
                Debug.LogWarning("[Commander] Enter Play Mode and wait for GameBootstrapper initialization first.");
                return;
            }
            bootstrapper.DebugEnsureTenSpearmen();
        }

        [MenuItem("Open Empires/Commander/Cancel Debug Goal")]
        public static void CancelDebugGoal()
        {
            GameBootstrapper bootstrapper = GameBootstrapper.Instance;
            if (bootstrapper == null)
            {
                Debug.LogWarning("[Commander] GameBootstrapper is not active.");
                return;
            }
            bootstrapper.DebugCancelCommanderGoal();
        }

        [MenuItem("Open Empires/Commander/Log Status")]
        public static void LogStatus()
        {
            CommanderGoalManager manager = GameBootstrapper.Instance?.Commander;
            CommanderGoal goal = manager?.ActiveGoal;
            if (goal == null && manager != null && manager.Goals.Count > 0)
                goal = manager.Goals[manager.Goals.Count - 1];
            if (goal == null)
            {
                Debug.Log("[Commander] No active goal.");
                return;
            }
            Debug.Log($"[Commander] Goal #{goal.GoalId}: status={goal.Status}, "
                + $"owned={goal.LastObservedOwnedCount}, queued={goal.LastObservedQueuedCount}, "
                + $"reason={goal.StatusReason}");
        }

        [MenuItem("Open Empires/Commander/QA/Kill Active Construction Builder")]
        public static void KillActiveConstructionBuilder()
        {
            GameSimulation simulation = GameBootstrapper.Instance?.Simulation;
            if (simulation == null)
            {
                Debug.LogWarning("[Commander QA] Simulation is unavailable.");
                return;
            }

            UnitData selected = null;
            foreach (UnitData unit in simulation.UnitRegistry.GetAllUnits())
            {
                if (!unit.IsVillager || unit.State == UnitState.Dead || unit.CurrentHealth <= 0
                    || unit.ConstructionTargetBuildingId < 0) continue;
                BuildingData building = simulation.BuildingRegistry.GetBuilding(unit.ConstructionTargetBuildingId);
                if (building == null || building.IsDestroyed || !building.IsUnderConstruction) continue;
                if (selected == null || unit.Id < selected.Id) selected = unit;
            }

            if (selected == null)
            {
                Debug.LogWarning("[Commander QA] No active construction builder was found.");
                return;
            }

            int targetId = selected.ConstructionTargetBuildingId;
            selected.CurrentHealth = 0;
            selected.State = UnitState.Dead;
            Debug.Log($"[Commander QA] Killed builder #{selected.Id} on foundation #{targetId}.");
        }

        [MenuItem("Open Empires/Commander/QA/Human Assign Lowest Villager To Gold")]
        public static void HumanAssignLowestVillagerToGold()
        {
            GameBootstrapper bootstrapper = GameBootstrapper.Instance;
            GameSimulation simulation = bootstrapper?.Simulation;
            if (simulation == null)
            {
                Debug.LogWarning("[Commander QA] Simulation is unavailable.");
                return;
            }

            int playerId = bootstrapper.Network != null && bootstrapper.Network.IsMultiplayer
                ? bootstrapper.Network.LocalPlayerId : 0;
            UnitData selected = null;
            foreach (UnitData unit in simulation.UnitRegistry.GetAllUnits())
            {
                if (unit.PlayerId != playerId || !unit.IsVillager || unit.State == UnitState.Dead
                    || unit.CurrentHealth <= 0) continue;
                if (selected == null || unit.Id < selected.Id) selected = unit;
            }

            ResourceNodeData gold = null;
            foreach (ResourceNodeData node in simulation.MapData.GetAllResourceNodes())
            {
                if (node.Type != ResourceType.Gold || node.IsDepleted
                    || simulation.FogOfWar.GetVisibility(playerId, node.TileX, node.TileZ)
                        != TileVisibility.Visible) continue;
                if (gold == null || node.Id < gold.Id) gold = node;
            }

            if (selected == null || gold == null)
            {
                Debug.LogWarning("[Commander QA] No owned villager or currently visible Gold node was found.");
                return;
            }

            humanQaWorkerId = selected.Id;
            humanQaResourceNodeId = gold.Id;
            simulation.CommandBuffer.EnqueueCommand(
                new GatherCommand(playerId, new[] { selected.Id }, gold.Id));
            Debug.Log($"[Commander QA] Human command assigned villager #{selected.Id} to Gold node #{gold.Id}.");
        }

        [MenuItem("Open Empires/Commander/QA/Log Human Worker")]
        public static void LogHumanWorker()
        {
            GameSimulation simulation = GameBootstrapper.Instance?.Simulation;
            UnitData worker = simulation?.UnitRegistry.GetUnit(humanQaWorkerId);
            if (worker == null)
            {
                Debug.LogWarning("[Commander QA] Human QA worker is unavailable.");
                return;
            }
            Debug.Log($"[Commander QA] Human worker #{worker.Id}: state={worker.State}, "
                + $"targetResource={worker.TargetResourceNodeId}, expectedGold={humanQaResourceNodeId}.");
        }

        [MenuItem("Open Empires/Commander/QA/Add Two Completed Barracks")]
        public static void AddTwoCompletedBarracks()
        {
            GameBootstrapper bootstrapper = GameBootstrapper.Instance;
            GameSimulation simulation = bootstrapper?.Simulation;
            if (simulation == null)
            {
                Debug.LogWarning("[Commander QA] Simulation is unavailable.");
                return;
            }

            int playerId = bootstrapper.Network != null && bootstrapper.Network.IsMultiplayer
                ? bootstrapper.Network.LocalPlayerId : 0;
            BuildingData anchor = null;
            foreach (BuildingData building in simulation.BuildingRegistry.GetAllBuildings())
            {
                if (building.PlayerId != playerId || building.IsDestroyed || building.IsUnderConstruction
                    || simulation.GetEffectiveBuildingType(building) != BuildingType.TownCenter) continue;
                if (anchor == null || building.Id < anchor.Id) anchor = building;
            }
            if (anchor == null)
            {
                Debug.LogWarning("[Commander QA] No owned Town Center was found.");
                return;
            }

            int created = 0;
            int width = simulation.Config.BarracksFootprintWidth;
            int height = simulation.Config.BarracksFootprintHeight;
            for (int radius = 8; radius <= 24 && created < 2; radius++)
            {
                for (int dx = -radius; dx <= radius && created < 2; dx++)
                {
                    for (int dz = -radius; dz <= radius && created < 2; dz++)
                    {
                        if (Mathf.Abs(dx) != radius && Mathf.Abs(dz) != radius) continue;
                        int x = anchor.OriginTileX + dx;
                        int z = anchor.OriginTileZ + dz;
                        if (!IsBuildable(simulation, x, z, width, height)) continue;
                        BuildingData barracks = simulation.CreateBuilding(playerId, BuildingType.Barracks,
                            x, z, underConstruction: false);
                        Debug.Log($"[Commander QA] Added completed Barracks #{barracks.Id} at ({x},{z}).");
                        created++;
                    }
                }
            }
            if (created < 2)
                Debug.LogWarning($"[Commander QA] Added only {created}/2 requested Barracks.");
        }

        private static bool IsBuildable(GameSimulation simulation, int tileX, int tileZ,
            int width, int height)
        {
            for (int x = tileX; x < tileX + width; x++)
                for (int z = tileZ; z < tileZ + height; z++)
                    if (!simulation.MapData.IsBuildable(x, z)) return false;
            return true;
        }
    }
}
#endif
