using System.Collections.Generic;

namespace OpenEmpires
{
    public struct ResearchCompletion
    {
        public int PlayerId;
        public TechnologyType Tech;
        public int BuildingId;
    }

    public class ResearchSystem
    {
        public List<ResearchCompletion> Tick(BuildingRegistry buildingRegistry, SimulationConfig config)
        {
            List<ResearchCompletion> completions = null;
            var allBuildings = buildingRegistry.GetAllBuildings();

            for (int i = 0; i < allBuildings.Count; i++)
            {
                var building = allBuildings[i];
                if (!building.IsResearching) continue;
                if (building.IsDestroyed) continue;

                building.ResearchTicksRemaining--;

                if (building.ResearchTicksRemaining <= 0)
                {
                    var tech = building.DequeueResearch();

                    if (completions == null)
                        completions = new List<ResearchCompletion>();
                    completions.Add(new ResearchCompletion
                    {
                        PlayerId = building.PlayerId,
                        Tech = tech,
                        BuildingId = building.Id
                    });

                    // Start next research in queue
                    if (building.ResearchQueue.Count > 0)
                    {
                        building.IsResearching = true;
                        building.CurrentResearch = building.ResearchQueue[0];
                        int ticks = GetResearchTicks(config, building.ResearchQueue[0]);
                        building.ResearchTicksRemaining = ticks;
                        building.ResearchTicksTotal = ticks;
                    }
                    else
                    {
                        building.IsResearching = false;
                        building.ResearchTicksRemaining = 0;
                        building.ResearchTicksTotal = 0;
                    }
                }
            }

            return completions;
        }

        public static int GetResearchTicks(SimulationConfig config, TechnologyType tech)
        {
            switch (tech)
            {
                case TechnologyType.BlacksmithDamage:
                case TechnologyType.BlacksmithDefense:
                    return config.ResearchTicks_Age3;       // longer than Age 2; tuned for the consolidated +2 bonus
                case TechnologyType.Ballistics:
                case TechnologyType.SiegeEngineering:
                case TechnologyType.Chemistry:
                case TechnologyType.MurderHoles:
                    return config.ResearchTicks_University;
                default:
                    return config.ResearchTicks_Age2;
            }
        }
    }
}
