using System;
using System.Collections.Generic;

namespace OpenEmpires
{
    public struct TrainingCompletion
    {
        public int BuildingId;
        public int UnitType;
        public int PlayerId;
    }

    public class BuildingTrainingSystem
    {
        private List<TrainingCompletion> completions = new List<TrainingCompletion>();

        public List<TrainingCompletion> Tick(BuildingRegistry registry, SimulationConfig config,
            Func<int, int, bool> canSpawn, bool productionCheatActive = false)
        {
            completions.Clear();
            Dictionary<int, int> pendingSpawns = null;

            var buildings = registry.GetAllBuildings();
            for (int i = 0; i < buildings.Count; i++)
            {
                var building = buildings[i];
                if (building.IsDestroyed || !building.IsTraining)
                    continue;

                building.TrainingTicksRemaining -= productionCheatActive ? 10 : 1;
                if (building.TrainingTicksRemaining <= 0)
                {
                    int pending = 0;
                    if (pendingSpawns != null)
                        pendingSpawns.TryGetValue(building.PlayerId, out pending);

                    // Pop cap gate: freeze at 1 tick until space opens
                    if (!canSpawn(building.PlayerId, pending))
                    {
                        building.TrainingTicksRemaining = 1;
                        continue;
                    }

                    int unitType = building.DequeueTraining();
                    completions.Add(new TrainingCompletion
                    {
                        BuildingId = building.Id,
                        UnitType = unitType,
                        PlayerId = building.PlayerId
                    });

                    if (pendingSpawns == null)
                        pendingSpawns = new Dictionary<int, int>();
                    pendingSpawns[building.PlayerId] = pending + 1;

                    // Start next item in queue if any
                    if (building.IsTraining)
                    {
                        building.TrainingTicksRemaining = GetTrainTime(config, building.TrainingQueue[0]);
                        building.TrainingTicksTotal = building.TrainingTicksRemaining;
                    }
                }
            }

            return completions;
        }

        public static int GetTrainTime(SimulationConfig config, int unitType)
        {
            switch (unitType)
            {
                case 9: return config.MonkTrainTimeTicks;
                case 8: return config.CrossbowmanTrainTimeTicks;
                case 7: return config.KnightTrainTimeTicks;
                case 6: return config.ManAtArmsTrainTimeTicks;
                case 10: return config.LongbowmanTrainTimeTicks;
                case 11: return config.GendarmeTrainTimeTicks;
                case 12: return config.LandsknechtTrainTimeTicks;
                case 4: return config.ScoutTrainTimeTicks;
                case 3: return config.HorsemanTrainTimeTicks;
                case 2: return config.ArcherTrainTimeTicks;
                case 0: return config.VillagerTrainTimeTicks;
                default: return config.SpearmanTrainTimeTicks;
            }
        }
    }
}
