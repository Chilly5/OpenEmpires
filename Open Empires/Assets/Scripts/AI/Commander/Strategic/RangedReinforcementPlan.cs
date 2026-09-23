namespace OpenEmpires
{
    public sealed class RangedReinforcementPlan : StrategicPlan
    {
        public const int FoodWorkers = 8;
        public const int WoodWorkers = 8;
        public const int ArcherTarget = 10;

        public RangedReinforcementPlan(int ownerPlayerId, int sourceIntentId)
            : base(ownerPlayerId, StrategicPlanType.RangedReinforcement, sourceIntentId,
                "Ranged reinforcement preparation complete.",
                "Ranged reinforcement preparation cancelled.")
        {
            var economy = new StrategicMilestone(1, "Economy", 0);
            economy.AddTacticalGoal(new StrategicResourceAllocationGoalRequest(ResourceType.Food, FoodWorkers));
            economy.AddTacticalGoal(new StrategicResourceAllocationGoalRequest(ResourceType.Wood, WoodWorkers));
            AddMilestone(economy);

            var production = new StrategicMilestone(2, "Production", 1);
            production.AddTacticalGoal(new StrategicBuildStructureGoalRequest(
                BuildingType.ArcheryRange, 1, ensureExisting: true));
            AddMilestone(production);

            var force = new StrategicMilestone(3, "Force", 2);
            force.AddTacticalGoal(new StrategicEnsureUnitCountGoalRequest(
                CommanderIntentCatalog.ArcherUnitType, ArcherTarget));
            AddMilestone(force);

            AddMilestone(new StrategicMilestone(4, "Ready", 3));
        }
    }
}
