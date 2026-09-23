using System;
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
            foreach (var resource in context.Economy)
                if (resource.ResourceType == ResourceType.Food || resource.ResourceType == ResourceType.Wood
                    || resource.ResourceType == ResourceType.Gold)
                    resources[resource.ResourceType.ToString()] = new JObject
                    {
                        ["current"] = resource.CurrentAmount,
                        ["reserved"] = resource.ReservedAmount,
                        ["available"] = resource.AvailableAmount
                    };
            var plans = new JArray();
            foreach (var plan in context.ActivePlans)
                plans.Add(new JObject
                {
                    ["planType"] = plan.PlanType,
                    ["status"] = plan.Status,
                    ["currentMilestone"] = plan.CurrentMilestone,
                    ["milestoneStatus"] = plan.MilestoneStatus
                });
            return new JObject
            {
                ["resources"] = resources,
                ["population"] = JObject.FromObject(context.Population),
                ["workerAllocation"] = JArray.FromObject(context.WorkerAllocation),
                ["totalWorkers"] = context.TotalWorkers,
                ["ownedMilitary"] = JArray.FromObject(context.Military),
                ["productionCapability"] = JArray.FromObject(context.Production),
                ["ownedDefense"] = JObject.FromObject(context.Defense),
                ["currentStrategicPlans"] = plans,
                ["visibleThreats"] = JObject.FromObject(context.Threat)
            }.ToString(Formatting.None);
        }
    }
}
