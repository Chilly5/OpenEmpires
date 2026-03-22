namespace OpenEmpires
{
    public enum CommandType
    {
        Move,
        Gather,
        Stop,
        AttackBuilding,
        AttackUnit,
        TrainUnit,
        SetRallyPoint,
        PlaceBuilding,
        ConstructBuilding,
        Noop,
        DropOff,
        PlaceWall,
        ConvertToGate,
        CancelTrain,
        UpgradeTower,
        CancelUpgrade,
        Garrison,
        Ungarrison,
        Patrol,
        CheatResource,
        CheatProduction,
        CheatVision,
        DeleteUnits,
        DeleteBuilding,
        Surrender,
        SlaughterSheep,
        FollowUnit,
        HealUnit,
        MeteorStrike,
        HealingRain,
        LightningStorm,
        Tsunami,
        MarketTrade,
        Research
    }

    public interface ICommand
    {
        CommandType Type { get; }
        int PlayerId { get; }
    }
}
