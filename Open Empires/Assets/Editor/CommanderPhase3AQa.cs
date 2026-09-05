#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace OpenEmpires.EditorTools
{
    // Explicit editor-only fixture setup. Once submitted, each goal runs untouched
    // through GameBootstrapper's live ticks and the normal command pipeline.
    public static class CommanderPhase3AQa
    {
        private static readonly string[] Inputs =
        {
            "make 10 archers", "build barracks", "put 8 villagers on wood",
            "make 10 spearmen don't touch gold"
        };
        private static CommanderGoal goal;
        private static int scenario = -1;
        private static int setupTick;
        private static bool initializing;
        private static readonly List<int> protectedIds = new List<int>();
        private static bool protectionViolated;
        private static int commands;
        private static int humanWorkerId = -1;
        private static bool humanControlViolated;

        [MenuItem("Open Empires/Commander/Phase 3A QA/Start Four Scenarios")]
        public static void Start()
        {
            GameBootstrapper bootstrap = GameBootstrapper.Instance;
            if (!EditorApplication.isPlaying || bootstrap == null || bootstrap.Network == null
                || bootstrap.Simulation != null)
            {
                Debug.LogWarning("[Phase3A QA] Start from fresh Play Mode before starting a match.");
                return;
            }
            bootstrap.SetPlayerCount(2);
            bootstrap.SetAIPlayerIds(Array.Empty<int>());
            bootstrap.SetTeamAssignments(new[] { 0, 1 });
            bootstrap.SetCivilizations(new[] { Civilization.French, Civilization.English });
            typeof(NetworkManager).GetProperty(nameof(NetworkManager.GameStarted))?.SetValue(bootstrap.Network, true);
            Time.timeScale = 10;
            initializing = true;
            scenario = -1;
            goal = null;
            protectedIds.Clear();
            protectionViolated = false;
            humanWorkerId = -1;
            humanControlViolated = false;
            EditorApplication.update -= Monitor;
            EditorApplication.update += Monitor;
            Debug.Log("[Phase3A QA] Live local fixture starting; no enemy AI; normal construction/training times.");
        }

        [MenuItem("Open Empires/Commander/Phase 3A QA/Log Evidence")]
        public static void LogEvidence()
        {
            GameSimulation sim = GameBootstrapper.Instance?.Simulation;
            if (sim == null) return;
            Debug.Log($"[Phase3A QA] scenario={scenario + 1} tick={sim.CurrentTick} status={goal?.Status} "
                + $"reason={goal?.StatusReason} archers={CountUnits(sim, 2)} spearmen={CountUnits(sim, 1)} "
                + $"woodWorkers={CountWorkers(sim, ResourceType.Wood)} goldWorkers={CountWorkers(sim, ResourceType.Gold)} "
                + $"protected={protectedIds.Count} protectionViolated={protectionViolated} "
                + $"humanWorker={humanWorkerId} humanControlViolated={humanControlViolated} commands={commands}");
        }

        private static void Monitor()
        {
            if (!EditorApplication.isPlaying)
            {
                EditorApplication.update -= Monitor;
                return;
            }
            GameBootstrapper bootstrap = GameBootstrapper.Instance;
            GameSimulation sim = bootstrap?.Simulation;
            if (sim == null || bootstrap.Commander == null) return;
            if (initializing)
            {
                initializing = false;
                ((int[])typeof(GameSimulation).GetField("playerAges", BindingFlags.NonPublic | BindingFlags.Instance)
                    .GetValue(sim))[0] = 3;
                PlayerResources resources = sim.ResourceManager.GetPlayerResources(0);
                resources.Food = 4000;
                resources.Wood = 4000;
                resources.Gold = 1000;
                foreach (BuildingData building in sim.BuildingRegistry.GetAllBuildings())
                {
                    if (building.PlayerId != 0 || building.Type != BuildingType.TownCenter) continue;
                    sim.CommandBuffer.EnqueueCommand(new ToggleAutoProduceCommand(0, building.Id, false));
                    for (int i = 0; i < 4; i++)
                        sim.CommandBuffer.EnqueueCommand(new TrainUnitCommand(0, building.Id, 0));
                    break;
                }
                sim.CommandBuffer.CommandEnqueued += ObserveCommand;
                setupTick = sim.CurrentTick;
                Debug.Log("[Phase3A QA] Setup: French Age 3, starting budget F4000/W4000/G1000, four villagers queued normally.");
                SubmitNext();
                return;
            }
            if (scenario == 3 && goal != null)
                foreach (int id in protectedIds)
                {
                    UnitData worker = sim.UnitRegistry.GetUnit(id);
                    ResourceNodeData node = worker == null ? null : sim.MapData.GetResourceNode(worker.TargetResourceNodeId);
                    if (node == null || node.Type != ResourceType.Gold) protectionViolated = true;
                }
            if (goal != null && goal.IsTerminal)
            {
                LogEvidence();
                if (goal.Status != CommanderGoalStatus.Completed || protectionViolated || humanControlViolated)
                {
                    Debug.LogError("[Phase3A QA] FAILED live scenario.");
                    EditorApplication.update -= Monitor;
                    return;
                }
                Debug.Log($"[Phase3A QA] PASS scenario {scenario + 1}: {Inputs[scenario]}");
                goal = null;
                if (scenario == 2)
                {
                    // Set up the protected-resource scenario with normal human gather orders.
                    ResourceNodeData gold = null;
                    foreach (ResourceNodeData node in sim.MapData.GetAllResourceNodes())
                        if (node.Type == ResourceType.Gold && !node.IsDepleted
                            && sim.FogOfWar.GetVisibility(0, node.TileX, node.TileZ) == TileVisibility.Visible)
                        { gold = node; break; }
                    if (gold == null) throw new InvalidOperationException("QA requires visible gold.");
                    foreach (UnitData worker in sim.UnitRegistry.GetAllUnits())
                    {
                        if (worker.PlayerId != 0 || !worker.IsVillager || worker.CurrentHealth <= 0) continue;
                        protectedIds.Add(worker.Id);
                        if (protectedIds.Count == 2) break;
                    }
                    sim.CommandBuffer.EnqueueCommand(new GatherCommand(0, protectedIds.ToArray(), gold.Id));
                    setupTick = sim.CurrentTick;
                    return;
                }
                SubmitNext();
            }
            else if (scenario == 2 && goal == null && sim.CurrentTick - setupTick >= 930)
            {
                // Let the human lease expire so the constraint, not the lease, proves protection.
                sim.ResourceManager.GetPlayerResources(0).Wood = 0;
                SubmitNext();
            }
        }

        private static void SubmitNext()
        {
            scenario++;
            commands = 0;
            if (scenario >= Inputs.Length)
            {
                Debug.Log("[Phase3A QA] ALL FOUR SCENARIOS PASSED.");
                EditorApplication.update -= Monitor;
                GameBootstrapper.Instance.Simulation.CommandBuffer.CommandEnqueued -= ObserveCommand;
                Time.timeScale = 1;
                return;
            }
            if (scenario == 2)
            {
                GameSimulation sim = GameBootstrapper.Instance.Simulation;
                ResourceNodeData gold = null;
                foreach (ResourceNodeData node in sim.MapData.GetAllResourceNodes())
                    if (node.Type == ResourceType.Gold && !node.IsDepleted
                        && sim.FogOfWar.GetVisibility(0, node.TileX, node.TileZ) == TileVisibility.Visible)
                    { gold = node; break; }
                foreach (UnitData worker in sim.UnitRegistry.GetAllUnits())
                    if (worker.PlayerId == 0 && worker.IsVillager && worker.CurrentHealth > 0)
                    { humanWorkerId = worker.Id; break; }
                if (gold == null || humanWorkerId < 0) throw new InvalidOperationException("QA needs a worker and visible gold.");
                sim.CommandBuffer.EnqueueCommand(new GatherCommand(0, new[] { humanWorkerId }, gold.Id));
                Debug.Log($"[Phase3A QA] Human assigned worker #{humanWorkerId} to gold immediately before wood goal.");
            }
            CommanderIntentSubmission submission = CommanderIntentDebugSession.Execute(Inputs[scenario], out string error);
            if (submission == null || !submission.CreatedGoal)
                throw new InvalidOperationException(error + submission?.Response);
            goal = submission.Resolution.Goal;
            Debug.Log($"[Phase3A QA] INPUT {Inputs[scenario]} RESPONSE {submission.Response.Replace('\n', ' ')}");
        }

        private static void ObserveCommand(ICommand command, CommandEnqueueSource source)
        {
            if (source != CommandEnqueueSource.Commander) return;
            commands++;
            int[] ids = command is GatherCommand gather ? gather.UnitIds
                : command is PlaceBuildingCommand place ? place.VillagerUnitIds
                : command is ConstructBuildingCommand construct ? construct.UnitIds : null;
            if (ids == null) return;
            foreach (int id in ids)
            {
                if (scenario == 2 && id == humanWorkerId) humanControlViolated = true;
                if (scenario == 3 && protectedIds.Contains(id)) protectionViolated = true;
            }
        }

        private static int CountUnits(GameSimulation sim, int type)
        {
            int count = 0;
            foreach (UnitData unit in sim.UnitRegistry.GetAllUnits())
                if (unit.PlayerId == 0 && unit.UnitType == type && unit.CurrentHealth > 0 && unit.State != UnitState.Dead) count++;
            return count;
        }

        private static int CountWorkers(GameSimulation sim, ResourceType type)
        {
            int count = 0;
            foreach (UnitData unit in sim.UnitRegistry.GetAllUnits())
            {
                if (unit.PlayerId != 0 || !unit.IsVillager || unit.CurrentHealth <= 0) continue;
                if (unit.State != UnitState.Gathering && unit.State != UnitState.MovingToGather
                    && unit.State != UnitState.MovingToDropoff && unit.State != UnitState.DroppingOff) continue;
                ResourceNodeData node = sim.MapData.GetResourceNode(unit.TargetResourceNodeId);
                if (node != null && !node.IsDepleted && node.Type == type) count++;
            }
            return count;
        }
    }
}
#endif
