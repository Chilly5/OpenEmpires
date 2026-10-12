using System;
using System.Collections.Generic;
using UnityEngine;

namespace OpenEmpires
{
    internal enum NativeMovePathResult { Complete, AlreadyAtTarget, Partial, NoPath }

    // Detached, local-only observation of ordinary MoveCommand processing. It is
    // never serialized, hashed, or consulted by gameplay simulation decisions.
    internal sealed class NativeMoveProcessingReceipt
    {
        internal GameSimulation Runtime { get; }
        internal ICommand OriginalCommand { get; }
        internal int UnitId { get; }
        internal int Tick { get; }
        internal FixedVector3 Endpoint { get; }
        internal NativeMovePathResult Result { get; }

        internal NativeMoveProcessingReceipt(GameSimulation runtime, ICommand originalCommand,
            int unitId, int tick, FixedVector3 endpoint, NativeMovePathResult result)
        {
            Runtime = runtime; OriginalCommand = originalCommand; UnitId = unitId;
            Tick = tick; Endpoint = endpoint; Result = result;
        }
    }

    internal sealed class NativeUnitCommandObservation
    {
        internal GameSimulation Runtime { get; }
        internal ICommand OriginalCommand { get; }
        internal ICommand ExecutingCommand { get; }
        internal int Tick { get; }

        internal NativeUnitCommandObservation(GameSimulation runtime, ICommand original,
            ICommand executing, int tick)
        { Runtime = runtime; OriginalCommand = original; ExecutingCommand = executing; Tick = tick; }
    }

    public partial class GameSimulation
    {
        internal event Action<NativeMoveProcessingReceipt> NativeMoveProcessed;
        internal event Action<NativeUnitCommandObservation> NativeUnitCommandProcessed;

        private void PublishNativeUnitCommandProcessed(ICommand originalCommand,
            ICommand executingCommand)
        {
            var handlers = NativeUnitCommandProcessed;
            if (handlers == null) return;
            var observation = new NativeUnitCommandObservation(this, originalCommand,
                executingCommand, currentTick);
            foreach (Action<NativeUnitCommandObservation> observer in handlers.GetInvocationList())
            {
                try { observer(observation); }
                catch (Exception error) { ReportTrainingObservationError(error); }
            }
        }

        private void PublishNativeMoveProcessed(ICommand originalCommand, int unitId,
            FixedVector3 endpoint, NativeMovePathResult result)
        {
            var handlers = NativeMoveProcessed;
            if (handlers == null) return;
            var receipt = new NativeMoveProcessingReceipt(this, originalCommand,
                unitId, currentTick, endpoint, result);
            foreach (Action<NativeMoveProcessingReceipt> observer in handlers.GetInvocationList())
            {
                try { observer(receipt); }
                catch (Exception error) { ReportTrainingObservationError(error); }
            }
        }

        // The same connectivity adjustment runs in ordinary native processing and
        // Commander preflight; no alternative formation transform is introduced.
        internal void SnapNativeMoveFormationPositions(ref MoveCommand cmd)
        {
            if (cmd.FormationPositions == null) return;
            Vector2Int anchorTile = MapData.WorldToTile(cmd.TargetPosition);
            int maxChebyshev = 0;
            for (int fi = 0; fi < cmd.FormationPositions.Length; fi++)
            {
                Vector2Int fpTile = MapData.WorldToTile(cmd.FormationPositions[fi]);
                int chebyshev = Mathf.Max(Mathf.Abs(fpTile.x - anchorTile.x), Mathf.Abs(fpTile.y - anchorTile.y));
                if (chebyshev > maxChebyshev) maxChebyshev = chebyshev;
            }
            int floodRadius = maxChebyshev * 2;
            HashSet<int> reachable = GridPathfinder.FloodFillWalkable(MapData, anchorTile, floodRadius);
            if (reachable.Count == 0) return;
            int mapWidth = MapData.Width;
            for (int fi = 0; fi < cmd.FormationPositions.Length; fi++)
            {
                Vector2Int fpTile = MapData.WorldToTile(cmd.FormationPositions[fi]);
                int key = fpTile.y * mapWidth + fpTile.x;
                if (reachable.Contains(key)) continue;
                Vector2Int snapped = GridPathfinder.FindConnectedTileToward(fpTile, anchorTile,
                    reachable, mapWidth, floodRadius);
                if (snapped.x >= 0) cmd.FormationPositions[fi] = MapData.TileToWorldFixed(snapped.x, snapped.y);
            }
        }

        internal FixedVector3 NativeMoveFormationCenter(FixedVector3 anchor,
            FixedVector3 slot, UnitData unit)
        {
            Fixed32 radiusScale = unit.Radius / cachedUnitRadius;
            if (radiusScale == Fixed32.One) return slot;
            FixedVector3 offset = slot - anchor;
            return anchor + new FixedVector3(offset.x * radiusScale,
                offset.y, offset.z * radiusScale);
        }

        internal Fixed32 MaximumNativeMoveScatter(UnitData unit)
        {
            Fixed32 scatterFactor = Fixed32.Max(Fixed32.Zero,
                Fixed32.One - (unit.Mass - Fixed32.One) / (cachedSpearmanMass - Fixed32.One));
            Fixed32 scatter = cachedFormationScatter * scatterFactor;
            return scatter > cachedScatterThreshold ? scatter : Fixed32.Zero;
        }

        internal FixedVector3 ResolveNativeMoveFormationTarget(FixedVector3 anchor,
            FixedVector3 slot, UnitData unit, int executionTick)
        {
            FixedVector3 targetPos = NativeMoveFormationCenter(anchor, slot, unit);
            Fixed32 scatter = MaximumNativeMoveScatter(unit);
            if (scatter > Fixed32.Zero)
            {
                uint hash = (uint)(unit.Id * 2654435761u + (uint)executionTick * 1103515245u);
                int hashX = (int)(hash & 0xFFFF) - 32768;
                int hashZ = (int)((hash >> 16) & 0xFFFF) - 32768;
                Fixed32 normX = new Fixed32(hashX);
                Fixed32 normZ = new Fixed32(hashZ);
                targetPos.x = targetPos.x + normX * scatter;
                targetPos.z = targetPos.z + normZ * scatter;
            }
            return targetPos;
        }
    }
}
