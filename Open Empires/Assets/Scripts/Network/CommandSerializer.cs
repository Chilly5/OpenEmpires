using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace OpenEmpires
{
    public static class CommandSerializer
    {
        // ========== JSON SERIALIZATION ==========

        public static (string commandType, string payload) ToJson(ICommand command)
        {
            string commandType = command.Type.ToString();
            string payload;

            switch (command)
            {
                case MoveCommand move:
                    payload = JsonUtility.ToJson(new MovePayload(move));
                    break;
                case GatherCommand gather:
                    payload = JsonUtility.ToJson(new GatherPayload(gather));
                    break;
                case StopCommand stop:
                    payload = JsonUtility.ToJson(new StopPayload(stop));
                    break;
                case AttackBuildingCommand attack:
                    payload = JsonUtility.ToJson(new AttackBuildingPayload(attack));
                    break;
                case AttackUnitCommand attackUnit:
                    payload = JsonUtility.ToJson(new AttackUnitPayload(attackUnit));
                    break;
                case TrainUnitCommand train:
                    payload = JsonUtility.ToJson(new TrainUnitPayload(train));
                    break;
                case SetRallyPointCommand rally:
                    payload = JsonUtility.ToJson(new SetRallyPointPayload(rally));
                    break;
                case PlaceBuildingCommand place:
                    payload = JsonUtility.ToJson(new PlaceBuildingPayload(place));
                    break;
                case ConstructBuildingCommand construct:
                    payload = JsonUtility.ToJson(new ConstructBuildingPayload(construct));
                    break;
                case DropOffCommand dropOff:
                    payload = JsonUtility.ToJson(new DropOffPayload(dropOff));
                    break;
                case PlaceWallCommand placeWall:
                    payload = JsonUtility.ToJson(new PlaceWallPayload(placeWall));
                    break;
                case ConvertToGateCommand convertToGate:
                    payload = JsonUtility.ToJson(new ConvertToGatePayload(convertToGate));
                    break;
                case CancelTrainCommand cancelTrain:
                    payload = JsonUtility.ToJson(new CancelTrainPayload(cancelTrain));
                    break;
                case GarrisonCommand garrison:
                    payload = JsonUtility.ToJson(new GarrisonPayload(garrison));
                    break;
                case UngarrisonCommand ungarrison:
                    payload = JsonUtility.ToJson(new UngarrisonPayload(ungarrison));
                    break;
                case NoopCommand noop:
                    payload = JsonUtility.ToJson(new NoopPayload(noop));
                    break;
                case UpgradeTowerCommand upgrade:
                    payload = JsonUtility.ToJson(new UpgradeTowerPayload(upgrade));
                    break;
                case CancelUpgradeCommand cancelUpgrade:
                    payload = JsonUtility.ToJson(new CancelUpgradePayload(cancelUpgrade));
                    break;
                case PatrolCommand patrol:
                    payload = JsonUtility.ToJson(new PatrolPayload(patrol));
                    break;
                case DeleteUnitsCommand deleteUnits:
                    payload = JsonUtility.ToJson(new DeleteUnitsPayload(deleteUnits));
                    break;
                case DeleteBuildingCommand deleteBuilding:
                    payload = JsonUtility.ToJson(new DeleteBuildingPayload(deleteBuilding));
                    break;
                case SurrenderVoteCommand surrender:
                    payload = JsonUtility.ToJson(new SurrenderPayload(surrender));
                    break;
                case SlaughterSheepCommand slaughter:
                    payload = JsonUtility.ToJson(new SlaughterSheepPayload(slaughter));
                    break;
                case FollowUnitCommand follow:
                    payload = JsonUtility.ToJson(new FollowUnitPayload(follow));
                    break;
                case HealUnitCommand heal:
                    payload = JsonUtility.ToJson(new HealUnitPayload(heal));
                    break;
                case MeteorStrikeCommand meteor:
                    payload = JsonUtility.ToJson(new MeteorStrikePayload(meteor));
                    break;
                case HealingRainCommand healingRain:
                    payload = JsonUtility.ToJson(new HealingRainPayload(healingRain));
                    break;
                case LightningStormCommand lightningStorm:
                    payload = JsonUtility.ToJson(new LightningStormPayload(lightningStorm));
                    break;
                case TsunamiCommand tsunami:
                    payload = JsonUtility.ToJson(new TsunamiPayload(tsunami));
                    break;
                case CheatResourceCommand:
                case CheatProductionCommand:
                case CheatVisionCommand:
                    payload = "{}";
                    break;
                default:
                    payload = "{}";
                    break;
            }

            return (commandType, payload);
        }

        public static ICommand FromJson(string commandType, string payload, int playerId)
        {
            try
            {
                return commandType switch
                {
                    "Move" => ParseMoveCommand(payload, playerId),
                    "Gather" => ParseGatherCommand(payload, playerId),
                    "Stop" => ParseStopCommand(payload, playerId),
                    "AttackBuilding" => ParseAttackBuildingCommand(payload, playerId),
                    "AttackUnit" => ParseAttackUnitCommand(payload, playerId),
                    "TrainUnit" => ParseTrainUnitCommand(payload, playerId),
                    "SetRallyPoint" => ParseSetRallyPointCommand(payload, playerId),
                    "PlaceBuilding" => ParsePlaceBuildingCommand(payload, playerId),
                    "ConstructBuilding" => ParseConstructBuildingCommand(payload, playerId),
                    "DropOff" => ParseDropOffCommand(payload, playerId),
                    "PlaceWall" => ParsePlaceWallCommand(payload, playerId),
                    "ConvertToGate" => ParseConvertToGateCommand(payload, playerId),
                    "CancelTrain" => ParseCancelTrainCommand(payload, playerId),
                    "Garrison" => ParseGarrisonCommand(payload, playerId),
                    "Ungarrison" => ParseUngarrisonCommand(payload, playerId),
                    "Noop" => ParseNoopCommand(payload, playerId),
                    "UpgradeTower" => ParseUpgradeTowerCommand(payload, playerId),
                    "CancelUpgrade" => ParseCancelUpgradeCommand(payload, playerId),
                    "Patrol" => ParsePatrolCommand(payload, playerId),
                    "DeleteUnits" => ParseDeleteUnitsCommand(payload, playerId),
                    "DeleteBuilding" => ParseDeleteBuildingCommand(payload, playerId),
                    "CheatResource" => new CheatResourceCommand { PlayerId = playerId },
                    "CheatProduction" => new CheatProductionCommand { PlayerId = playerId },
                    "CheatVision" => new CheatVisionCommand { PlayerId = playerId },
                    "Surrender" => ParseSurrenderCommand(payload, playerId),
                    "SlaughterSheep" => ParseSlaughterSheepCommand(payload, playerId),
                    "FollowUnit" => ParseFollowUnitCommand(payload, playerId),
                    "HealUnit" => ParseHealUnitCommand(payload, playerId),
                    "MeteorStrike" => ParseMeteorStrikeCommand(payload, playerId),
                    "HealingRain" => ParseHealingRainCommand(payload, playerId),
                    "LightningStorm" => ParseLightningStormCommand(payload, playerId),
                    "Tsunami" => ParseTsunamiCommand(payload, playerId),
                    _ => null
                };
            }
            catch (Exception e)
            {
                Debug.LogError($"[CommandSerializer] Failed to parse {commandType}: {e.Message}");
                return null;
            }
        }

        private static MoveCommand ParseMoveCommand(string payload, int playerId)
        {
            var data = JsonUtility.FromJson<MovePayload>(payload);
            var cmd = new MoveCommand(
                playerId,
                data.unitIds,
                new FixedVector3(new Fixed32(data.targetX), new Fixed32(data.targetY), new Fixed32(data.targetZ))
            );
            cmd.PreserveFormation = data.preserveFormation;
            cmd.IsAttackMove = data.isAttackMove;
            cmd.IsQueued = data.isQueued;
            cmd.HasFacing = data.hasFacing;
            if (data.hasFacing)
            {
                cmd.FacingDirection = new FixedVector3(
                    new Fixed32(data.facingX), new Fixed32(data.facingY), new Fixed32(data.facingZ)
                );
            }
            if (data.formationX != null && data.formationX.Length > 0)
            {
                cmd.FormationPositions = new FixedVector3[data.formationX.Length];
                for (int i = 0; i < data.formationX.Length; i++)
                {
                    cmd.FormationPositions[i] = new FixedVector3(
                        new Fixed32(data.formationX[i]),
                        new Fixed32(data.formationY[i]),
                        new Fixed32(data.formationZ[i])
                    );
                }
            }
            return cmd;
        }

        private static GatherCommand ParseGatherCommand(string payload, int playerId)
        {
            var data = JsonUtility.FromJson<GatherPayload>(payload);
            var cmd = new GatherCommand(playerId, data.unitIds, data.resourceNodeId);
            cmd.IsQueued = data.isQueued;
            return cmd;
        }

        private static StopCommand ParseStopCommand(string payload, int playerId)
        {
            var data = JsonUtility.FromJson<StopPayload>(payload);
            return new StopCommand(playerId, data.unitIds);
        }

        private static AttackBuildingCommand ParseAttackBuildingCommand(string payload, int playerId)
        {
            var data = JsonUtility.FromJson<AttackBuildingPayload>(payload);
            var cmd = new AttackBuildingCommand(playerId, data.unitIds, data.targetBuildingId);
            cmd.IsQueued = data.isQueued;
            return cmd;
        }

        private static AttackUnitCommand ParseAttackUnitCommand(string payload, int playerId)
        {
            var data = JsonUtility.FromJson<AttackUnitPayload>(payload);
            var cmd = new AttackUnitCommand(playerId, data.unitIds, data.targetUnitId);
            cmd.IsQueued = data.isQueued;
            return cmd;
        }

        private static TrainUnitCommand ParseTrainUnitCommand(string payload, int playerId)
        {
            var data = JsonUtility.FromJson<TrainUnitPayload>(payload);
            return new TrainUnitCommand(playerId, data.buildingId, data.unitType);
        }

        private static SetRallyPointCommand ParseSetRallyPointCommand(string payload, int playerId)
        {
            var data = JsonUtility.FromJson<SetRallyPointPayload>(payload);
            return new SetRallyPointCommand(
                playerId,
                data.buildingId,
                new FixedVector3(new Fixed32(data.posX), new Fixed32(data.posY), new Fixed32(data.posZ)),
                data.resourceNodeId,
                data.targetUnitId
            );
        }

        private static NoopCommand ParseNoopCommand(string payload, int playerId)
        {
            // Handle legacy empty payload
            if (string.IsNullOrEmpty(payload) || payload == "{}")
            {
                return new NoopCommand(playerId);
            }

            var data = JsonUtility.FromJson<NoopPayload>(payload);
            return new NoopCommand(playerId, data.checksum, data.simTick, data.systemHash, data.preCmdHash, data.postCmdHash, data.systemHashDetail);
        }

        private static PlaceBuildingCommand ParsePlaceBuildingCommand(string payload, int playerId)
        {
            var data = JsonUtility.FromJson<PlaceBuildingPayload>(payload);
            var cmd = new PlaceBuildingCommand(playerId, (BuildingType)data.buildingType,
                                               data.tileX, data.tileZ, data.villagerUnitIds);
            cmd.IsQueued = data.isQueued;
            cmd.LandmarkIdValue = data.landmarkId;
            return cmd;
        }

        private static ConstructBuildingCommand ParseConstructBuildingCommand(string payload, int playerId)
        {
            var data = JsonUtility.FromJson<ConstructBuildingPayload>(payload);
            var cmd = new ConstructBuildingCommand(playerId, data.unitIds, data.targetBuildingId);
            cmd.IsQueued = data.isQueued;
            return cmd;
        }

        private static DropOffCommand ParseDropOffCommand(string payload, int playerId)
        {
            var data = JsonUtility.FromJson<DropOffPayload>(payload);
            var cmd = new DropOffCommand(playerId, data.unitIds, data.targetBuildingId);
            cmd.IsQueued = data.isQueued;
            return cmd;
        }

        private static PlaceWallCommand ParsePlaceWallCommand(string payload, int playerId)
        {
            var data = JsonUtility.FromJson<PlaceWallPayload>(payload);
            var cmd = new PlaceWallCommand(playerId, data.startTileX, data.startTileZ,
                data.endTileX, data.endTileZ, data.villagerUnitIds);
            cmd.IsQueued = data.isQueued;
            return cmd;
        }

        private static ConvertToGateCommand ParseConvertToGateCommand(string payload, int playerId)
        {
            var data = JsonUtility.FromJson<ConvertToGatePayload>(payload);
            return new ConvertToGateCommand(playerId, data.buildingId);
        }

        private static CancelTrainCommand ParseCancelTrainCommand(string payload, int playerId)
        {
            var data = JsonUtility.FromJson<CancelTrainPayload>(payload);
            return new CancelTrainCommand(playerId, data.buildingId, data.queueIndex);
        }

        private static UpgradeTowerCommand ParseUpgradeTowerCommand(string payload, int playerId)
        {
            var data = JsonUtility.FromJson<UpgradeTowerPayload>(payload);
            return new UpgradeTowerCommand(playerId, data.buildingId, (TowerUpgradeType)data.upgradeType);
        }

        private static CancelUpgradeCommand ParseCancelUpgradeCommand(string payload, int playerId)
        {
            var data = JsonUtility.FromJson<CancelUpgradePayload>(payload);
            return new CancelUpgradeCommand(playerId, data.buildingId, data.queueIndex);
        }

        private static GarrisonCommand ParseGarrisonCommand(string payload, int playerId)
        {
            var data = JsonUtility.FromJson<GarrisonPayload>(payload);
            return new GarrisonCommand(playerId, data.unitIds, data.targetBuildingId);
        }

        private static UngarrisonCommand ParseUngarrisonCommand(string payload, int playerId)
        {
            var data = JsonUtility.FromJson<UngarrisonPayload>(payload);
            return new UngarrisonCommand(playerId, data.buildingId);
        }

        private static PatrolCommand ParsePatrolCommand(string payload, int playerId)
        {
            var data = JsonUtility.FromJson<PatrolPayload>(payload);
            var target = new FixedVector3(new Fixed32(data.targetX), new Fixed32(data.targetY), new Fixed32(data.targetZ));
            var cmd = new PatrolCommand(playerId, data.unitIds, target);
            cmd.IsQueued = data.isQueued;
            return cmd;
        }

        private static DeleteUnitsCommand ParseDeleteUnitsCommand(string payload, int playerId)
        {
            var data = JsonUtility.FromJson<DeleteUnitsPayload>(payload);
            return new DeleteUnitsCommand(playerId, data.unitIds);
        }

        private static DeleteBuildingCommand ParseDeleteBuildingCommand(string payload, int playerId)
        {
            var data = JsonUtility.FromJson<DeleteBuildingPayload>(payload);
            return new DeleteBuildingCommand(playerId, data.buildingId);
        }

        private static SurrenderVoteCommand ParseSurrenderCommand(string payload, int playerId)
        {
            var data = JsonUtility.FromJson<SurrenderPayload>(payload);
            return new SurrenderVoteCommand(playerId, data.voteYes);
        }

        // ========== JSON PAYLOAD TYPES ==========

        [Serializable]
        private class MovePayload
        {
            public int[] unitIds;
            public int targetX, targetY, targetZ;
            public int[] formationX, formationY, formationZ;
            public int facingX, facingY, facingZ;
            public bool hasFacing;
            public bool preserveFormation;
            public bool isAttackMove;
            public bool isQueued;

            public MovePayload() { }

            public MovePayload(MoveCommand cmd)
            {
                unitIds = cmd.UnitIds;
                targetX = cmd.TargetPosition.x.Raw;
                targetY = cmd.TargetPosition.y.Raw;
                targetZ = cmd.TargetPosition.z.Raw;
                hasFacing = cmd.HasFacing;
                facingX = cmd.FacingDirection.x.Raw;
                facingY = cmd.FacingDirection.y.Raw;
                facingZ = cmd.FacingDirection.z.Raw;
                preserveFormation = cmd.PreserveFormation;
                isAttackMove = cmd.IsAttackMove;
                isQueued = cmd.IsQueued;

                if (cmd.FormationPositions != null)
                {
                    formationX = new int[cmd.FormationPositions.Length];
                    formationY = new int[cmd.FormationPositions.Length];
                    formationZ = new int[cmd.FormationPositions.Length];
                    for (int i = 0; i < cmd.FormationPositions.Length; i++)
                    {
                        formationX[i] = cmd.FormationPositions[i].x.Raw;
                        formationY[i] = cmd.FormationPositions[i].y.Raw;
                        formationZ[i] = cmd.FormationPositions[i].z.Raw;
                    }
                }
            }
        }

        [Serializable]
        private class GatherPayload
        {
            public int[] unitIds;
            public int resourceNodeId;
            public bool isQueued;

            public GatherPayload() { }

            public GatherPayload(GatherCommand cmd)
            {
                unitIds = cmd.UnitIds;
                resourceNodeId = cmd.ResourceNodeId;
                isQueued = cmd.IsQueued;
            }
        }

        [Serializable]
        private class StopPayload
        {
            public int[] unitIds;

            public StopPayload() { }

            public StopPayload(StopCommand cmd)
            {
                unitIds = cmd.UnitIds;
            }
        }

        [Serializable]
        private class AttackBuildingPayload
        {
            public int[] unitIds;
            public int targetBuildingId;
            public bool isQueued;

            public AttackBuildingPayload() { }

            public AttackBuildingPayload(AttackBuildingCommand cmd)
            {
                unitIds = cmd.UnitIds;
                targetBuildingId = cmd.TargetBuildingId;
                isQueued = cmd.IsQueued;
            }
        }

        [Serializable]
        private class AttackUnitPayload
        {
            public int[] unitIds;
            public int targetUnitId;
            public bool isQueued;

            public AttackUnitPayload() { }

            public AttackUnitPayload(AttackUnitCommand cmd)
            {
                unitIds = cmd.UnitIds;
                targetUnitId = cmd.TargetUnitId;
                isQueued = cmd.IsQueued;
            }
        }

        [Serializable]
        private class TrainUnitPayload
        {
            public int buildingId;
            public int unitType;

            public TrainUnitPayload() { }

            public TrainUnitPayload(TrainUnitCommand cmd)
            {
                buildingId = cmd.BuildingId;
                unitType = cmd.UnitType;
            }
        }

        [Serializable]
        private class SetRallyPointPayload
        {
            public int buildingId;
            public int posX, posY, posZ;
            public int resourceNodeId = -1;
            public int targetUnitId = -1;

            public SetRallyPointPayload() { }

            public SetRallyPointPayload(SetRallyPointCommand cmd)
            {
                buildingId = cmd.BuildingId;
                posX = cmd.Position.x.Raw;
                posY = cmd.Position.y.Raw;
                posZ = cmd.Position.z.Raw;
                resourceNodeId = cmd.ResourceNodeId;
                targetUnitId = cmd.TargetUnitId;
            }
        }

        [Serializable]
        private class NoopPayload
        {
            public uint checksum;
            public int simTick;
            public uint systemHash;
            public uint preCmdHash;
            public uint postCmdHash;
            public string systemHashDetail;

            public NoopPayload() { }

            public NoopPayload(NoopCommand cmd)
            {
                checksum = cmd.StateChecksum;
                simTick = cmd.SimTick;
                systemHash = cmd.SystemHash;
                preCmdHash = cmd.PreCmdHash;
                postCmdHash = cmd.PostCmdHash;
                systemHashDetail = cmd.SystemHashDetail;
            }
        }

        [Serializable]
        private class PlaceBuildingPayload
        {
            public int buildingType;
            public int tileX;
            public int tileZ;
            public int[] villagerUnitIds;
            public bool isQueued;
            public int landmarkId = -1;

            public PlaceBuildingPayload() { }

            public PlaceBuildingPayload(PlaceBuildingCommand cmd)
            {
                buildingType = (int)cmd.BuildingType;
                tileX = cmd.TileX;
                tileZ = cmd.TileZ;
                villagerUnitIds = cmd.VillagerUnitIds ?? new int[0];
                isQueued = cmd.IsQueued;
                landmarkId = cmd.LandmarkIdValue;
            }
        }

        [Serializable]
        private class ConstructBuildingPayload
        {
            public int[] unitIds;
            public int targetBuildingId;
            public bool isQueued;

            public ConstructBuildingPayload() { }

            public ConstructBuildingPayload(ConstructBuildingCommand cmd)
            {
                unitIds = cmd.UnitIds;
                targetBuildingId = cmd.TargetBuildingId;
                isQueued = cmd.IsQueued;
            }
        }

        [Serializable]
        private class DropOffPayload
        {
            public int[] unitIds;
            public int targetBuildingId;
            public bool isQueued;

            public DropOffPayload() { }

            public DropOffPayload(DropOffCommand cmd)
            {
                unitIds = cmd.UnitIds;
                targetBuildingId = cmd.TargetBuildingId;
                isQueued = cmd.IsQueued;
            }
        }

        [Serializable]
        private class PlaceWallPayload
        {
            public int startTileX;
            public int startTileZ;
            public int endTileX;
            public int endTileZ;
            public int[] villagerUnitIds;
            public bool isQueued;

            public PlaceWallPayload() { }

            public PlaceWallPayload(PlaceWallCommand cmd)
            {
                startTileX = cmd.StartTileX;
                startTileZ = cmd.StartTileZ;
                endTileX = cmd.EndTileX;
                endTileZ = cmd.EndTileZ;
                villagerUnitIds = cmd.VillagerUnitIds ?? new int[0];
                isQueued = cmd.IsQueued;
            }
        }

        [Serializable]
        private class ConvertToGatePayload
        {
            public int buildingId;

            public ConvertToGatePayload() { }

            public ConvertToGatePayload(ConvertToGateCommand cmd)
            {
                buildingId = cmd.BuildingId;
            }
        }

        [Serializable]
        private class CancelTrainPayload
        {
            public int buildingId;
            public int queueIndex;

            public CancelTrainPayload() { }

            public CancelTrainPayload(CancelTrainCommand cmd)
            {
                buildingId = cmd.BuildingId;
                queueIndex = cmd.QueueIndex;
            }
        }

        [Serializable]
        private class CancelUpgradePayload
        {
            public int buildingId;
            public int queueIndex;

            public CancelUpgradePayload() { }

            public CancelUpgradePayload(CancelUpgradeCommand cmd)
            {
                buildingId = cmd.BuildingId;
                queueIndex = cmd.QueueIndex;
            }
        }

        [Serializable]
        private class GarrisonPayload
        {
            public int[] unitIds;
            public int targetBuildingId;

            public GarrisonPayload() { }

            public GarrisonPayload(GarrisonCommand cmd)
            {
                unitIds = cmd.UnitIds;
                targetBuildingId = cmd.TargetBuildingId;
            }
        }

        [Serializable]
        private class UpgradeTowerPayload
        {
            public int buildingId;
            public int upgradeType;

            public UpgradeTowerPayload() { }

            public UpgradeTowerPayload(UpgradeTowerCommand cmd)
            {
                buildingId = cmd.BuildingId;
                upgradeType = (int)cmd.UpgradeType;
            }
        }

        [Serializable]
        private class UngarrisonPayload
        {
            public int buildingId;

            public UngarrisonPayload() { }

            public UngarrisonPayload(UngarrisonCommand cmd)
            {
                buildingId = cmd.BuildingId;
            }
        }

        [Serializable]
        private class PatrolPayload
        {
            public int[] unitIds;
            public int targetX, targetY, targetZ;
            public bool isQueued;

            public PatrolPayload() { }

            public PatrolPayload(PatrolCommand cmd)
            {
                unitIds = cmd.UnitIds;
                targetX = cmd.TargetPosition.x.Raw;
                targetY = cmd.TargetPosition.y.Raw;
                targetZ = cmd.TargetPosition.z.Raw;
                isQueued = cmd.IsQueued;
            }
        }

        [Serializable]
        private class DeleteUnitsPayload
        {
            public int[] unitIds;

            public DeleteUnitsPayload() { }

            public DeleteUnitsPayload(DeleteUnitsCommand cmd)
            {
                unitIds = cmd.UnitIds;
            }
        }

        [Serializable]
        private class DeleteBuildingPayload
        {
            public int buildingId;

            public DeleteBuildingPayload() { }

            public DeleteBuildingPayload(DeleteBuildingCommand cmd)
            {
                buildingId = cmd.BuildingId;
            }
        }

        [Serializable]
        private class SurrenderPayload
        {
            public bool voteYes;

            public SurrenderPayload() { }

            public SurrenderPayload(SurrenderVoteCommand cmd)
            {
                voteYes = cmd.VoteYes;
            }
        }

        [Serializable]
        private class SlaughterSheepPayload
        {
            public int[] villagerIds;
            public int sheepUnitId;
            public bool isQueued;

            public SlaughterSheepPayload() { }

            public SlaughterSheepPayload(SlaughterSheepCommand cmd)
            {
                villagerIds = cmd.VillagerIds;
                sheepUnitId = cmd.SheepUnitId;
                isQueued = cmd.IsQueued;
            }
        }

        private static SlaughterSheepCommand ParseSlaughterSheepCommand(string payload, int playerId)
        {
            var data = JsonUtility.FromJson<SlaughterSheepPayload>(payload);
            return new SlaughterSheepCommand
            {
                PlayerId = playerId,
                VillagerIds = data.villagerIds,
                SheepUnitId = data.sheepUnitId,
                IsQueued = data.isQueued
            };
        }

        [System.Serializable]
        private class FollowUnitPayload
        {
            public int[] unitIds;
            public int targetUnitId;
            public bool isQueued;

            public FollowUnitPayload() { }

            public FollowUnitPayload(FollowUnitCommand cmd)
            {
                unitIds = cmd.UnitIds;
                targetUnitId = cmd.TargetUnitId;
                isQueued = cmd.IsQueued;
            }
        }

        private static FollowUnitCommand ParseFollowUnitCommand(string payload, int playerId)
        {
            var data = JsonUtility.FromJson<FollowUnitPayload>(payload);
            return new FollowUnitCommand
            {
                PlayerId = playerId,
                UnitIds = data.unitIds,
                TargetUnitId = data.targetUnitId,
                IsQueued = data.isQueued
            };
        }

        private class HealUnitPayload
        {
            public int[] unitIds;
            public int targetUnitId;
            public bool isQueued;

            public HealUnitPayload() { }

            public HealUnitPayload(HealUnitCommand cmd)
            {
                unitIds = cmd.UnitIds;
                targetUnitId = cmd.TargetUnitId;
                isQueued = cmd.IsQueued;
            }
        }

        private static HealUnitCommand ParseHealUnitCommand(string payload, int playerId)
        {
            var data = JsonUtility.FromJson<HealUnitPayload>(payload);
            return new HealUnitCommand
            {
                PlayerId = playerId,
                UnitIds = data.unitIds,
                TargetUnitId = data.targetUnitId,
                IsQueued = data.isQueued
            };
        }

        [System.Serializable]
        private class MeteorStrikePayload
        {
            public int tileX;
            public int tileZ;

            public MeteorStrikePayload() { }

            public MeteorStrikePayload(MeteorStrikeCommand cmd)
            {
                tileX = cmd.TargetTileX;
                tileZ = cmd.TargetTileZ;
            }
        }

        private static MeteorStrikeCommand ParseMeteorStrikeCommand(string payload, int playerId)
        {
            var data = JsonUtility.FromJson<MeteorStrikePayload>(payload);
            return new MeteorStrikeCommand(playerId, data.tileX, data.tileZ);
        }

        [System.Serializable]
        private class HealingRainPayload
        {
            public int tileX;
            public int tileZ;

            public HealingRainPayload() { }

            public HealingRainPayload(HealingRainCommand cmd)
            {
                tileX = cmd.TargetTileX;
                tileZ = cmd.TargetTileZ;
            }
        }

        private static HealingRainCommand ParseHealingRainCommand(string payload, int playerId)
        {
            var data = JsonUtility.FromJson<HealingRainPayload>(payload);
            return new HealingRainCommand(playerId, data.tileX, data.tileZ);
        }

        [System.Serializable]
        private class LightningStormPayload
        {
            public int tileX;
            public int tileZ;

            public LightningStormPayload() { }

            public LightningStormPayload(LightningStormCommand cmd)
            {
                tileX = cmd.TargetTileX;
                tileZ = cmd.TargetTileZ;
            }
        }

        private static LightningStormCommand ParseLightningStormCommand(string payload, int playerId)
        {
            var data = JsonUtility.FromJson<LightningStormPayload>(payload);
            return new LightningStormCommand(playerId, data.tileX, data.tileZ);
        }

        [System.Serializable]
        private class TsunamiPayload
        {
            public int tileX;
            public int tileZ;
            public int dirX;
            public int dirZ;

            public TsunamiPayload() { }

            public TsunamiPayload(TsunamiCommand cmd)
            {
                tileX = cmd.TargetTileX;
                tileZ = cmd.TargetTileZ;
                dirX = cmd.DirectionX;
                dirZ = cmd.DirectionZ;
            }
        }

        private static TsunamiCommand ParseTsunamiCommand(string payload, int playerId)
        {
            var data = JsonUtility.FromJson<TsunamiPayload>(payload);
            return new TsunamiCommand(playerId, data.tileX, data.tileZ, data.dirX, data.dirZ);
        }

        // ========== BINARY SERIALIZATION (kept for compatibility) ==========

        public static byte[] Serialize(List<ICommand> commands, int tick)
        {
            using (var ms = new MemoryStream())
            using (var w = new BinaryWriter(ms))
            {
                w.Write(tick);
                w.Write(commands.Count);
                foreach (var cmd in commands)
                {
                    w.Write((byte)cmd.Type);
                    w.Write(cmd.PlayerId);
                    switch (cmd)
                    {
                        case MoveCommand move:
                            WriteIntArray(w, move.UnitIds);
                            WriteFixedVector3(w, move.TargetPosition);
                            if (move.FormationPositions != null)
                            {
                                w.Write(move.FormationPositions.Length);
                                foreach (var pos in move.FormationPositions)
                                    WriteFixedVector3(w, pos);
                            }
                            else
                            {
                                w.Write(0);
                            }
                            w.Write(move.HasFacing);
                            if (move.HasFacing)
                                WriteFixedVector3(w, move.FacingDirection);
                            w.Write(move.PreserveFormation);
                            w.Write(move.IsAttackMove);
                            w.Write(move.IsQueued);
                            break;
                        case GatherCommand gather:
                            WriteIntArray(w, gather.UnitIds);
                            w.Write(gather.ResourceNodeId);
                            w.Write(gather.IsQueued);
                            break;
                        case StopCommand stop:
                            WriteIntArray(w, stop.UnitIds);
                            break;
                        case AttackBuildingCommand attackBuilding:
                            WriteIntArray(w, attackBuilding.UnitIds);
                            w.Write(attackBuilding.TargetBuildingId);
                            w.Write(attackBuilding.IsQueued);
                            break;
                        case AttackUnitCommand attackUnit:
                            WriteIntArray(w, attackUnit.UnitIds);
                            w.Write(attackUnit.TargetUnitId);
                            w.Write(attackUnit.IsQueued);
                            break;
                        case TrainUnitCommand train:
                            w.Write(train.BuildingId);
                            w.Write(train.UnitType);
                            break;
                        case SetRallyPointCommand rally:
                            w.Write(rally.BuildingId);
                            WriteFixedVector3(w, rally.Position);
                            w.Write(rally.ResourceNodeId);
                            w.Write(rally.TargetUnitId);
                            break;
                        case PlaceBuildingCommand place:
                            w.Write((int)place.BuildingType);
                            w.Write(place.TileX);
                            w.Write(place.TileZ);
                            if (place.VillagerUnitIds != null)
                                WriteIntArray(w, place.VillagerUnitIds);
                            else
                                w.Write(0);
                            w.Write(place.IsQueued);
                            w.Write(place.LandmarkIdValue);
                            break;
                        case ConstructBuildingCommand construct:
                            WriteIntArray(w, construct.UnitIds);
                            w.Write(construct.TargetBuildingId);
                            w.Write(construct.IsQueued);
                            break;
                        case DropOffCommand dropOff:
                            WriteIntArray(w, dropOff.UnitIds);
                            w.Write(dropOff.TargetBuildingId);
                            w.Write(dropOff.IsQueued);
                            break;
                        case PlaceWallCommand placeWall:
                            w.Write(placeWall.StartTileX);
                            w.Write(placeWall.StartTileZ);
                            w.Write(placeWall.EndTileX);
                            w.Write(placeWall.EndTileZ);
                            if (placeWall.VillagerUnitIds != null)
                                WriteIntArray(w, placeWall.VillagerUnitIds);
                            else
                                w.Write(0);
                            w.Write(placeWall.IsQueued);
                            break;
                        case ConvertToGateCommand convertToGate:
                            w.Write(convertToGate.BuildingId);
                            break;
                        case CancelTrainCommand cancelTrain:
                            w.Write(cancelTrain.BuildingId);
                            w.Write(cancelTrain.QueueIndex);
                            break;
                        case UpgradeTowerCommand upgradeTower:
                            w.Write(upgradeTower.BuildingId);
                            w.Write((int)upgradeTower.UpgradeType);
                            break;
                        case CancelUpgradeCommand cancelUpgrade:
                            w.Write(cancelUpgrade.BuildingId);
                            w.Write(cancelUpgrade.QueueIndex);
                            break;
                        case GarrisonCommand garrison:
                            WriteIntArray(w, garrison.UnitIds);
                            w.Write(garrison.TargetBuildingId);
                            break;
                        case UngarrisonCommand ungarrison:
                            w.Write(ungarrison.BuildingId);
                            break;
                        case PatrolCommand patrol:
                            WriteIntArray(w, patrol.UnitIds);
                            WriteFixedVector3(w, patrol.TargetPosition);
                            w.Write(patrol.IsQueued);
                            break;
                        case DeleteUnitsCommand deleteUnits:
                            WriteIntArray(w, deleteUnits.UnitIds);
                            break;
                        case DeleteBuildingCommand deleteBuilding:
                            w.Write(deleteBuilding.BuildingId);
                            break;
                        case SurrenderVoteCommand surrender:
                            w.Write(surrender.VoteYes);
                            break;
                        case SlaughterSheepCommand slaughter:
                            WriteIntArray(w, slaughter.VillagerIds);
                            w.Write(slaughter.SheepUnitId);
                            w.Write(slaughter.IsQueued);
                            break;
                        case FollowUnitCommand follow:
                            WriteIntArray(w, follow.UnitIds);
                            w.Write(follow.TargetUnitId);
                            w.Write(follow.IsQueued);
                            break;
                        case MeteorStrikeCommand meteor:
                            w.Write(meteor.TargetTileX);
                            w.Write(meteor.TargetTileZ);
                            break;
                        case HealingRainCommand healingRain:
                            w.Write(healingRain.TargetTileX);
                            w.Write(healingRain.TargetTileZ);
                            break;
                        case LightningStormCommand lightningStorm:
                            w.Write(lightningStorm.TargetTileX);
                            w.Write(lightningStorm.TargetTileZ);
                            break;
                        case TsunamiCommand tsunami:
                            w.Write(tsunami.TargetTileX);
                            w.Write(tsunami.TargetTileZ);
                            w.Write(tsunami.DirectionX);
                            w.Write(tsunami.DirectionZ);
                            break;
                        case CheatResourceCommand:
                        case CheatProductionCommand:
                        case CheatVisionCommand:
                            break;
                    }
                }
                w.Flush();
                return ms.ToArray();
            }
        }

        public static (int tick, List<ICommand> commands) Deserialize(byte[] data)
        {
            var commands = new List<ICommand>();
            using (var ms = new MemoryStream(data))
            using (var r = new BinaryReader(ms))
            {
                int tick = r.ReadInt32();
                int count = r.ReadInt32();
                for (int i = 0; i < count; i++)
                {
                    var type = (CommandType)r.ReadByte();
                    int playerId = r.ReadInt32();
                    switch (type)
                    {
                        case CommandType.Move:
                            int[] moveUnitIds = ReadIntArray(r);
                            FixedVector3 target = ReadFixedVector3(r);
                            int formCount = r.ReadInt32();
                            FixedVector3[] formations = null;
                            if (formCount > 0)
                            {
                                formations = new FixedVector3[formCount];
                                for (int j = 0; j < formCount; j++)
                                    formations[j] = ReadFixedVector3(r);
                            }
                            bool hasFacing = r.ReadBoolean();
                            FixedVector3 facing = default;
                            if (hasFacing)
                                facing = ReadFixedVector3(r);
                            bool preserveFormation = r.ReadBoolean();
                            bool isAttackMove = r.ReadBoolean();
                            bool isMoveQueued = r.ReadBoolean();
                            var moveCmd = hasFacing
                                ? new MoveCommand(playerId, moveUnitIds, target, formations, facing)
                                : new MoveCommand(playerId, moveUnitIds, target, formations);
                            moveCmd.PreserveFormation = preserveFormation;
                            moveCmd.IsAttackMove = isAttackMove;
                            moveCmd.IsQueued = isMoveQueued;
                            commands.Add(moveCmd);
                            break;
                        case CommandType.Gather:
                            int[] gatherUnitIds = ReadIntArray(r);
                            int resourceNodeId = r.ReadInt32();
                            bool isGatherQueued = r.ReadBoolean();
                            var gatherCmd = new GatherCommand(playerId, gatherUnitIds, resourceNodeId);
                            gatherCmd.IsQueued = isGatherQueued;
                            commands.Add(gatherCmd);
                            break;
                        case CommandType.Stop:
                            int[] stopUnitIds = ReadIntArray(r);
                            commands.Add(new StopCommand(playerId, stopUnitIds));
                            break;
                        case CommandType.AttackBuilding:
                            int[] atkBldUnitIds = ReadIntArray(r);
                            int targetBuildingId = r.ReadInt32();
                            bool isAtkBldQueued = r.ReadBoolean();
                            var atkBldCmd = new AttackBuildingCommand(playerId, atkBldUnitIds, targetBuildingId);
                            atkBldCmd.IsQueued = isAtkBldQueued;
                            commands.Add(atkBldCmd);
                            break;
                        case CommandType.AttackUnit:
                            int[] atkUnitUnitIds = ReadIntArray(r);
                            int targetUnitId = r.ReadInt32();
                            bool isAtkUnitQueued = r.ReadBoolean();
                            var atkUnitCmd = new AttackUnitCommand(playerId, atkUnitUnitIds, targetUnitId);
                            atkUnitCmd.IsQueued = isAtkUnitQueued;
                            commands.Add(atkUnitCmd);
                            break;
                        case CommandType.TrainUnit:
                            int trainBuildingId = r.ReadInt32();
                            int trainUnitType = r.ReadInt32();
                            commands.Add(new TrainUnitCommand(playerId, trainBuildingId, trainUnitType));
                            break;
                        case CommandType.SetRallyPoint:
                            int rallyBuildingId = r.ReadInt32();
                            FixedVector3 rallyPos = ReadFixedVector3(r);
                            int rallyResourceNodeId = r.ReadInt32();
                            int rallyTargetUnitId = r.ReadInt32();
                            commands.Add(new SetRallyPointCommand(playerId, rallyBuildingId, rallyPos, rallyResourceNodeId, rallyTargetUnitId));
                            break;
                        case CommandType.PlaceBuilding:
                            var bType = (BuildingType)r.ReadInt32();
                            int ptX = r.ReadInt32(), ptZ = r.ReadInt32();
                            int[] vIds = ReadIntArray(r);
                            bool isPlaceQueued = r.ReadBoolean();
                            int placeLandmarkId = r.ReadInt32();
                            var placeCmd = new PlaceBuildingCommand(playerId, bType, ptX, ptZ, vIds.Length > 0 ? vIds : null);
                            placeCmd.IsQueued = isPlaceQueued;
                            placeCmd.LandmarkIdValue = placeLandmarkId;
                            commands.Add(placeCmd);
                            break;
                        case CommandType.ConstructBuilding:
                            int[] constructUnitIds = ReadIntArray(r);
                            int constructBuildingId = r.ReadInt32();
                            bool isConstructQueued = r.ReadBoolean();
                            var constructCmd = new ConstructBuildingCommand(playerId, constructUnitIds, constructBuildingId);
                            constructCmd.IsQueued = isConstructQueued;
                            commands.Add(constructCmd);
                            break;
                        case CommandType.DropOff:
                            int[] dropOffUnitIds = ReadIntArray(r);
                            int dropOffBuildingId = r.ReadInt32();
                            bool isDropOffQueued = r.ReadBoolean();
                            var dropOffCmd = new DropOffCommand(playerId, dropOffUnitIds, dropOffBuildingId);
                            dropOffCmd.IsQueued = isDropOffQueued;
                            commands.Add(dropOffCmd);
                            break;
                        case CommandType.PlaceWall:
                            int wallStartX = r.ReadInt32(), wallStartZ = r.ReadInt32();
                            int wallEndX = r.ReadInt32(), wallEndZ = r.ReadInt32();
                            int[] wallVIds = ReadIntArray(r);
                            bool isWallQueued = r.ReadBoolean();
                            var wallCmd = new PlaceWallCommand(playerId, wallStartX, wallStartZ,
                                wallEndX, wallEndZ, wallVIds.Length > 0 ? wallVIds : null);
                            wallCmd.IsQueued = isWallQueued;
                            commands.Add(wallCmd);
                            break;
                        case CommandType.ConvertToGate:
                            int gateBuildingId = r.ReadInt32();
                            commands.Add(new ConvertToGateCommand(playerId, gateBuildingId));
                            break;
                        case CommandType.CancelTrain:
                            int cancelBuildingId = r.ReadInt32();
                            int cancelQueueIndex = r.ReadInt32();
                            commands.Add(new CancelTrainCommand(playerId, cancelBuildingId, cancelQueueIndex));
                            break;
                        case CommandType.UpgradeTower:
                            int upgradeBuildingId = r.ReadInt32();
                            int upgradeType = r.ReadInt32();
                            commands.Add(new UpgradeTowerCommand(playerId, upgradeBuildingId, (TowerUpgradeType)upgradeType));
                            break;
                        case CommandType.CancelUpgrade:
                            int cancelUpgradeBuildingId = r.ReadInt32();
                            int cancelUpgradeQueueIndex = r.ReadInt32();
                            commands.Add(new CancelUpgradeCommand(playerId, cancelUpgradeBuildingId, cancelUpgradeQueueIndex));
                            break;
                        case CommandType.Garrison:
                            int[] garrisonUnitIds = ReadIntArray(r);
                            int garrisonBuildingId = r.ReadInt32();
                            commands.Add(new GarrisonCommand(playerId, garrisonUnitIds, garrisonBuildingId));
                            break;
                        case CommandType.Ungarrison:
                            int ungarrisonBuildingId = r.ReadInt32();
                            commands.Add(new UngarrisonCommand(playerId, ungarrisonBuildingId));
                            break;
                        case CommandType.Patrol:
                            int[] patrolUnitIds = ReadIntArray(r);
                            FixedVector3 patrolTarget = ReadFixedVector3(r);
                            bool patrolQueued = r.ReadBoolean();
                            var patrolCmd = new PatrolCommand(playerId, patrolUnitIds, patrolTarget);
                            patrolCmd.IsQueued = patrolQueued;
                            commands.Add(patrolCmd);
                            break;
                        case CommandType.CheatResource:
                            commands.Add(new CheatResourceCommand { PlayerId = playerId });
                            break;
                        case CommandType.CheatProduction:
                            commands.Add(new CheatProductionCommand { PlayerId = playerId });
                            break;
                        case CommandType.CheatVision:
                            commands.Add(new CheatVisionCommand { PlayerId = playerId });
                            break;
                        case CommandType.DeleteUnits:
                            int[] deleteUnitIds = ReadIntArray(r);
                            commands.Add(new DeleteUnitsCommand(playerId, deleteUnitIds));
                            break;
                        case CommandType.DeleteBuilding:
                            int deleteBuildingId = r.ReadInt32();
                            commands.Add(new DeleteBuildingCommand(playerId, deleteBuildingId));
                            break;
                        case CommandType.Surrender:
                            bool surrenderVoteYes = r.ReadBoolean();
                            commands.Add(new SurrenderVoteCommand(playerId, surrenderVoteYes));
                            break;
                        case CommandType.SlaughterSheep:
                            int[] slaughterVillagerIds = ReadIntArray(r);
                            int slaughterSheepId = r.ReadInt32();
                            bool slaughterIsQueued = r.ReadBoolean();
                            commands.Add(new SlaughterSheepCommand
                            {
                                PlayerId = playerId,
                                VillagerIds = slaughterVillagerIds,
                                SheepUnitId = slaughterSheepId,
                                IsQueued = slaughterIsQueued
                            });
                            break;
                        case CommandType.FollowUnit:
                            int[] followUnitIds = ReadIntArray(r);
                            int followTargetId = r.ReadInt32();
                            bool followIsQueued = r.ReadBoolean();
                            commands.Add(new FollowUnitCommand
                            {
                                PlayerId = playerId,
                                UnitIds = followUnitIds,
                                TargetUnitId = followTargetId,
                                IsQueued = followIsQueued
                            });
                            break;
                        case CommandType.MeteorStrike:
                            int meteorTileX = r.ReadInt32();
                            int meteorTileZ = r.ReadInt32();
                            commands.Add(new MeteorStrikeCommand(playerId, meteorTileX, meteorTileZ));
                            break;
                        case CommandType.HealingRain:
                            int hrTileX = r.ReadInt32();
                            int hrTileZ = r.ReadInt32();
                            commands.Add(new HealingRainCommand(playerId, hrTileX, hrTileZ));
                            break;
                        case CommandType.LightningStorm:
                            int lsTileX = r.ReadInt32();
                            int lsTileZ = r.ReadInt32();
                            commands.Add(new LightningStormCommand(playerId, lsTileX, lsTileZ));
                            break;
                        case CommandType.Tsunami:
                            int tsTileX = r.ReadInt32();
                            int tsTileZ = r.ReadInt32();
                            int tsDirX = r.ReadInt32();
                            int tsDirZ = r.ReadInt32();
                            commands.Add(new TsunamiCommand(playerId, tsTileX, tsTileZ, tsDirX, tsDirZ));
                            break;
                    }
                }
                return (tick, commands);
            }
        }

        private static void WriteIntArray(BinaryWriter w, int[] arr)
        {
            w.Write(arr.Length);
            foreach (int v in arr)
                w.Write(v);
        }

        private static int[] ReadIntArray(BinaryReader r)
        {
            int len = r.ReadInt32();
            int[] arr = new int[len];
            for (int i = 0; i < len; i++)
                arr[i] = r.ReadInt32();
            return arr;
        }

        private static void WriteFixedVector3(BinaryWriter w, FixedVector3 v)
        {
            w.Write(v.x.Raw);
            w.Write(v.y.Raw);
            w.Write(v.z.Raw);
        }

        private static FixedVector3 ReadFixedVector3(BinaryReader r)
        {
            return new FixedVector3(
                new Fixed32(r.ReadInt32()),
                new Fixed32(r.ReadInt32()),
                new Fixed32(r.ReadInt32()));
        }
    }
}
