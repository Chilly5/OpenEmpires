namespace OpenEmpires.Village
{
    public enum VillageJob
    {
        Forester,    // cuts trees, drops wood at the Lumber Yard
        Miner,       // mines gold at the Mine camp
        Quarryman,   // mines stone at the Mine camp
        Farmer,      // works their own farm by the Mill
        Forager,     // picks berries, drops food at the Mill
        Blacksmith,  // indoors at the Blacksmith
        Student,     // indoors at the University
        Merchant,    // indoors at the Market
        Monk,        // indoors at the Monastery
        Guard,       // patrols between the watchtowers
        Cook         // indoors at the Tavern, where villagers buy their meals
    }

    public enum JobKind
    {
        Gather,   // GatherCommand on a resource node near the workplace
        Indoor,   // garrison inside the workplace for the workday
        Patrol    // PatrolCommand between two posts
    }

    public static class VillageJobInfo
    {
        public static JobKind Kind(VillageJob job)
        {
            switch (job)
            {
                case VillageJob.Forester:
                case VillageJob.Miner:
                case VillageJob.Quarryman:
                case VillageJob.Farmer:
                case VillageJob.Forager:
                    return JobKind.Gather;
                case VillageJob.Guard:
                    return JobKind.Patrol;
                default:
                    return JobKind.Indoor;
            }
        }

        public static BuildingType Workplace(VillageJob job)
        {
            switch (job)
            {
                case VillageJob.Forester: return BuildingType.LumberYard;
                case VillageJob.Miner:
                case VillageJob.Quarryman: return BuildingType.Mine;
                case VillageJob.Farmer: return BuildingType.Farm;
                case VillageJob.Forager: return BuildingType.Mill;
                case VillageJob.Blacksmith: return BuildingType.Blacksmith;
                case VillageJob.Student: return BuildingType.University;
                case VillageJob.Merchant: return BuildingType.Market;
                case VillageJob.Monk: return BuildingType.Monastery;
                case VillageJob.Guard: return BuildingType.Tower;
                case VillageJob.Cook: return BuildingType.Tavern;
                default: return BuildingType.TownCenter;
            }
        }

        /// <summary>Resource gathered by Gather-kind jobs.</summary>
        public static ResourceType Resource(VillageJob job)
        {
            switch (job)
            {
                case VillageJob.Forester: return ResourceType.Wood;
                case VillageJob.Miner: return ResourceType.Gold;
                case VillageJob.Quarryman: return ResourceType.Stone;
                default: return ResourceType.Food;
            }
        }

        public static string DisplayName(VillageJob job)
        {
            switch (job)
            {
                case VillageJob.Quarryman: return "Quarryman";
                default: return job.ToString();
            }
        }

        /// <summary>Coins earned per in-game hour for jobs paid by the hour (indoor / patrol). Gatherers are paid per delivery instead.</summary>
        public static int HourlyWage(VillageJob job)
        {
            switch (job)
            {
                case VillageJob.Blacksmith: return 4;
                case VillageJob.Merchant: return 4;
                case VillageJob.Cook: return 4;
                case VillageJob.Guard: return 4;
                case VillageJob.Monk: return 3;   // alms
                case VillageJob.Student: return 2; // stipend
                default: return 0;
            }
        }
    }
}
