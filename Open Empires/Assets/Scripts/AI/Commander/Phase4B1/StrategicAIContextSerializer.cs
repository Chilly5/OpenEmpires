using System;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace OpenEmpires
{
    public static class StrategicAIContextSerializer
    {
        public static string Serialize(StrategicContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            var resources = new JObject();
            foreach (var resource in context.Economy.OrderBy(value => value.ResourceType))
                if (resource.ResourceType == ResourceType.Food || resource.ResourceType == ResourceType.Wood
                    || resource.ResourceType == ResourceType.Gold)
                    resources[resource.ResourceType.ToString()] = new JObject
                    {
                        ["current"] = resource.CurrentAmount,
                        ["reserved"] = resource.ReservedAmount,
                        ["available"] = resource.AvailableAmount
                    };
            var plans = new JArray();
            foreach (var plan in context.ActivePlans.OrderBy(value => value.StrategicPlanId)
                .ThenBy(value => value.PlanType, StringComparer.Ordinal))
                plans.Add(new JObject
                {
                    ["planType"] = plan.PlanType,
                    ["status"] = plan.Status,
                    ["currentMilestone"] = plan.CurrentMilestone,
                    ["milestoneStatus"] = plan.MilestoneStatus
                });
            var payload = new JObject
            {
                ["resources"] = resources,
                ["population"] = JObject.FromObject(context.Population),
                ["workerAllocation"] = JArray.FromObject(context.WorkerAllocation
                    .OrderBy(value => value.ResourceType).ThenBy(value => value.AssignedWorkers)),
                ["totalWorkers"] = context.TotalWorkers,
                ["ownedMilitary"] = JArray.FromObject(context.Military
                    .OrderBy(value => value.UnitType).ThenBy(value => value.OwnedCount)
                    .ThenBy(value => value.QueuedCount)),
                ["productionCapability"] = JArray.FromObject(context.Production
                    .OrderBy(value => value.BuildingType, StringComparer.Ordinal)
                    .ThenBy(value => value.ProductionBuildingCount)
                    .ThenBy(value => value.UnderConstructionCount)
                    .ThenBy(value => value.ActiveQueueCount)
                    .ThenBy(value => value.QueuedUnitCount)
                    .ThenBy(value => value.AvailableCapacity)),
                ["ownedDefense"] = JObject.FromObject(context.Defense),
                ["currentStrategicPlans"] = plans,
                ["visibleThreats"] = new JObject
                {
                    ["VisibleEnemyMilitary"] = JArray.FromObject(context.Threat.VisibleEnemyMilitary
                        .OrderBy(value => value.UnitType).ThenBy(value => value.VisibleCount)),
                    ["VisibleEnemyMilitaryUnits"] = context.Threat.VisibleEnemyMilitaryUnits,
                    ["VisibleEnemyMilitaryStrength"] = context.Threat.VisibleEnemyMilitaryStrength
                }
            };
            if (context.Insights != null)
            {
                StrategicContextInsights insights = context.Insights;
                var army = new JArray();
                foreach (StrategicArmyCompositionInsight value in insights.ArmyComposition
                    .OrderBy(value => value.UnitType))
                    army.Add(new JObject
                    {
                        ["unitType"] = value.UnitType,
                        ["ownedCount"] = value.OwnedCount,
                        ["queuedCount"] = value.QueuedCount,
                        ["ownedShareBasisPoints"] = value.OwnedShareBasisPoints
                    });
                var production = new JArray();
                foreach (StrategicProductionPressureInsight value in insights.ProductionPressure
                    .OrderBy(value => value.BuildingType, StringComparer.Ordinal))
                    production.Add(new JObject
                    {
                        ["buildingType"] = value.BuildingType,
                        ["completedCount"] = value.CompletedCount,
                        ["underConstructionCount"] = value.UnderConstructionCount,
                        ["activeQueueCount"] = value.ActiveQueueCount,
                        ["queuedUnitCount"] = value.QueuedUnitCount,
                        ["idleCompletedCount"] = value.IdleCompletedCount
                    });
                var progress = new JArray();
                foreach (StrategicPlanProgressInsight value in insights.PlanProgress
                    .OrderBy(value => value.StrategicPlanId))
                    progress.Add(new JObject
                    {
                        ["strategicPlanId"] = value.StrategicPlanId,
                        ["planType"] = value.PlanType,
                        ["status"] = value.Status,
                        ["completedMilestoneCount"] = value.CompletedMilestoneCount,
                        ["totalMilestoneCount"] = value.TotalMilestoneCount,
                        ["currentMilestone"] = value.CurrentMilestone,
                        ["milestoneStatus"] = value.MilestoneStatus
                    });
                payload["insights"] = new JObject
                {
                    ["incomeTrendAvailable"] = insights.IncomeTrendAvailable,
                    ["workerActivity"] = new JObject
                    {
                        ["available"] = insights.WorkerActivityAvailable,
                        ["totalWorkers"] = insights.TotalWorkers,
                        ["gatheringWorkers"] = insights.GatheringWorkers,
                        ["basisPoints"] = insights.WorkerActivityBasisPoints
                    },
                    ["armyComposition"] = army,
                    ["productionPressure"] = production,
                    ["planProgress"] = progress
                };
            }
            return payload.ToString(Formatting.None);
        }
    }
}
