using System;
using System.Collections.Generic;

namespace OpenEmpires
{
    public partial class GameSimulation
    {
        // Local-only correlation: neither the marker nor the boxed command identity
        // is serialized. Unmarked/native ordinary Garrison keeps its usual fallback.
        private sealed class ExactGarrisonOrder
        {
            internal ICommand Original;
            internal int PlayerId;
            internal int BuildingId;
            internal int[] UnitIds;
            internal int ExpiresTick;
        }

        private const int MaxExactGarrisonSubjects = 200;
        private readonly List<ExactGarrisonOrder> pendingExactGarrison = new List<ExactGarrisonOrder>();
        private readonly Dictionary<int, ExactGarrisonOrder> activeExactGarrison =
            new Dictionary<int, ExactGarrisonOrder>();

        internal bool RegisterExactGarrisonCommand(ICommand original)
        {
            if (!(original is GarrisonCommand command) || command.UnitIds == null
                || command.UnitIds.Length == 0 || command.UnitIds.Length > MaxExactGarrisonSubjects
                || command.TargetBuildingId < 0)
                return false;
            PruneExpiredPendingExactGarrison();
            int pendingSubjects = 0;
            foreach (var pending in pendingExactGarrison) pendingSubjects += pending.UnitIds.Length;
            if (pendingSubjects + command.UnitIds.Length > MaxExactGarrisonSubjects) return false;
            var distinct = new HashSet<int>();
            foreach (int id in command.UnitIds)
            {
                var unit = UnitRegistry.GetUnit(id);
                if (!distinct.Add(id) || unit == null || unit.PlayerId != command.PlayerId
                    || unit.State == UnitState.Dead || unit.CurrentHealth <= 0) return false;
            }
            pendingExactGarrison.Add(new ExactGarrisonOrder {
                Original = original, PlayerId = command.PlayerId,
                BuildingId = command.TargetBuildingId,
                UnitIds = (int[])command.UnitIds.Clone(), ExpiresTick = currentTick + 256
            });
            return true;
        }

        private void PruneExpiredPendingExactGarrison()
        {
            pendingExactGarrison.RemoveAll(order => currentTick > order.ExpiresTick);
        }

        // A reconstructed relay order with no unique verified original cannot
        // inherit the opt-in guard. Suppress every identical candidate in this
        // tick: permitting one would allow an unauthorized shelter fallback.
        private bool RejectUnattributedExactGarrison(ICommand command, ICommand verifiedOriginal)
        {
            if (verifiedOriginal != null || !(command is GarrisonCommand garrison)) return false;
            PruneExpiredPendingExactGarrison();
            foreach (var pending in pendingExactGarrison)
            {
                if (ReferenceEquals(pending.Original, command)
                    || garrison.PlayerId != pending.PlayerId
                    || garrison.TargetBuildingId != pending.BuildingId
                    || garrison.UnitIds == null || garrison.UnitIds.Length != pending.UnitIds.Length)
                    continue;
                bool identical = true;
                for (int i = 0; i < pending.UnitIds.Length; i++)
                    if (garrison.UnitIds[i] != pending.UnitIds[i]) { identical = false; break; }
                if (!identical) continue;
                pending.ExpiresTick = currentTick;
                return true;
            }
            return false;
        }

        // This runs at the native command boundary after local suppression. A later
        // human or Commander unit order replaces the old marker before it executes.
        private bool ObserveExactGarrisonCommand(ICommand command, ICommand original,
            out bool exactGarrison)
        {
            exactGarrison = false;
            PruneExpiredPendingExactGarrison();
            int[] subjects = CommandBuffer.GetSubjectUnitIds(command);
            if (subjects == null) return true;
            ExactGarrisonOrder exact = null;
            for (int i = 0; i < pendingExactGarrison.Count; i++)
                if (ReferenceEquals(pendingExactGarrison[i].Original, original))
                {
                    exact = pendingExactGarrison[i];
                    pendingExactGarrison.RemoveAt(i);
                    break;
                }
            if (exact != null)
            {
                if (!(command is GarrisonCommand garrison)
                    || garrison.PlayerId != exact.PlayerId
                    || garrison.TargetBuildingId != exact.BuildingId
                    || garrison.UnitIds == null || garrison.UnitIds.Length != exact.UnitIds.Length)
                    return false;
                for (int i = 0; i < exact.UnitIds.Length; i++)
                {
                    if (garrison.UnitIds[i] != exact.UnitIds[i]) return false;
                    var unit = UnitRegistry.GetUnit(exact.UnitIds[i]);
                    if (unit == null || unit.PlayerId != exact.PlayerId
                        || unit.State == UnitState.Dead || unit.CurrentHealth <= 0)
                        return false;
                }
            }
            foreach (int id in subjects)
            {
                var unit = UnitRegistry.GetUnit(id);
                if (unit != null && unit.PlayerId == command.PlayerId)
                    activeExactGarrison.Remove(id);
            }
            if (exact == null) return true;
            foreach (int id in exact.UnitIds) activeExactGarrison[id] = exact;
            exactGarrison = true;
            return true;
        }

        private bool IsExactGarrisonTarget(UnitData unit, int buildingId)
        {
            return !activeExactGarrison.TryGetValue(unit.Id, out var exact)
                || (exact.PlayerId == unit.PlayerId && exact.BuildingId == buildingId);
        }

        private void CancelExactGarrisonOrder(UnitData unit)
        {
            activeExactGarrison.Remove(unit.Id);
            unit.TargetGarrisonBuildingId = -1;
            unit.ClearPath();
            unit.ClearPatrol();
            unit.State = UnitState.Idle;
            unit.PlayerCommanded = false;
        }

        private bool RejectExactGarrisonFallback(UnitData unit)
        {
            if (!activeExactGarrison.ContainsKey(unit.Id)) return false;
            CancelExactGarrisonOrder(unit);
            return true;
        }

        private void PruneExactGarrisonSubjects()
        {
            if (activeExactGarrison.Count == 0) return;
            var ids = new List<int>(activeExactGarrison.Keys);
            foreach (int id in ids)
            {
                var unit = UnitRegistry.GetUnit(id);
                if (unit == null || unit.PlayerId != activeExactGarrison[id].PlayerId
                    || unit.State == UnitState.Dead)
                {
                    activeExactGarrison.Remove(id);
                    continue;
                }
                if (unit.CurrentHealth <= 0)
                {
                    CancelExactGarrisonOrder(unit);
                    continue;
                }
                var exact = activeExactGarrison[id];
                if (unit.TargetGarrisonBuildingId < 0)
                {
                    activeExactGarrison.Remove(id);
                    continue;
                }
                if (unit.TargetGarrisonBuildingId != exact.BuildingId)
                    CancelExactGarrisonOrder(unit);
            }
        }
    }
}
