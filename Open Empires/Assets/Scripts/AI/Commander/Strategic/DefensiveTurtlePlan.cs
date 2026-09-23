namespace OpenEmpires
{
    public sealed class DefensiveTurtlePlan : StrategicPlan
    {
        public const int FoodWorkers = 8;
        public const int WoodWorkers = 8;
        public const int TowerCount = 2;
        public const int SpearmanTarget = 8;
        public const int ArcherTarget = 8;
        internal int TowerTargetTotal { get; set; }

        public DefensiveTurtlePlan(int ownerPlayerId, int sourceIntentId)
            : base(ownerPlayerId, StrategicPlanType.DefensiveTurtle, sourceIntentId,
                "Fortified defense preparation complete.",
                "Fortified defense preparation cancelled.")
        {
            var economy = new StrategicMilestone(1, "Economy", 0);
            economy.AddTacticalGoal(new StrategicResourceAllocationGoalRequest(ResourceType.Food, FoodWorkers));
            economy.AddTacticalGoal(new StrategicResourceAllocationGoalRequest(ResourceType.Wood, WoodWorkers));
            AddMilestone(economy);

            var production = new StrategicMilestone(2, "Production", 1);
            production.AddTacticalGoal(new StrategicBuildStructureGoalRequest(
                BuildingType.Barracks, 1, ensureExisting: true));
            production.AddTacticalGoal(new StrategicBuildStructureGoalRequest(
                BuildingType.ArcheryRange, 1, ensureExisting: true));
            AddMilestone(production);

            var fortifications = new StrategicMilestone(3, "Fortifications", 2);
            fortifications.AddTacticalGoal(new StrategicBuildStructureGoalRequest(
                BuildingType.Tower, TowerCount));
            AddMilestone(fortifications);

            var force = new StrategicMilestone(4, "Force", 3);
            force.AddTacticalGoal(new StrategicEnsureUnitCountGoalRequest(
                CommanderIntentCatalog.SpearmanUnitType, SpearmanTarget));
            force.AddTacticalGoal(new StrategicEnsureUnitCountGoalRequest(
                CommanderIntentCatalog.ArcherUnitType, ArcherTarget));
            AddMilestone(force);

            AddMilestone(new StrategicMilestone(5, "Ready", 4));
        }
    }
}
