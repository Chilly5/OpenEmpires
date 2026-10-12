using System;
using System.Linq;

namespace OpenEmpires
{
    internal sealed partial class CommanderPlanner
    {
        private CommanderPlan PlanDynamicWorkerMove(CommanderCapabilityGoal goal, int currentTick)
        {
            if (goal.Action.ActionType != CommanderCapabilityActionType.MoveUnits
                || goal.FrozenWorkerIds == null || goal.FrozenWorkerIds.Count < 1
                || goal.FrozenWorkerIds.Count > 200
                || !goal.DynamicMoveLocation.TryResolveResourceSidePoint(simulation, goal.PlayerId,
                    out FixedVector3 target, out int resourceId))
                return new CommanderPlan(CommanderGoalStatus.Blocked,
                    "The complete frozen worker set or visible owned resource-side destination is unavailable.", 0, 0);
            goal.DynamicMoveResourceId = resourceId;
            var command = new MoveCommand(goal.PlayerId, goal.FrozenWorkerIds.ToArray(), target);
            if (!TryResolveNativeMovementSlots(goal, command, currentTick,
                    out MoveCommand formation, out string reason))
                return new CommanderPlan(CommanderGoalStatus.Blocked,
                    "No complete west-side native formation exists for the frozen worker set; no prefix moved. " + reason,
                    0, 0);
            return new CommanderPlan(CommanderGoalStatus.Executing,
                "Moving the full frozen worker set to the exact visible western woodline using native Move.",
                0, 0, formation);
        }
    }
}
