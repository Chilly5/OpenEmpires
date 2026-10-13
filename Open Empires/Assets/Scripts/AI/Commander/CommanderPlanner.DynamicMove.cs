using System;
using System.Linq;

namespace OpenEmpires
{
    internal sealed partial class CommanderPlanner
    {
        private CommanderPlan PlanDynamicWorkerMove(CommanderCapabilityGoal goal, int currentTick)
        {
            var actorIds = goal.DynamicMoveUnitIds ?? goal.FrozenWorkerIds;
            if (goal.Action.ActionType != CommanderCapabilityActionType.MoveUnits
                || actorIds == null || actorIds.Count < 1
                || actorIds.Count > 200
                || !goal.DynamicMoveLocation.TryResolveResourceSidePoint(simulation, goal.PlayerId,
                    out FixedVector3 target, out int resourceId))
                return new CommanderPlan(CommanderGoalStatus.Blocked,
                    "The complete selected unit set or visible resource-side destination is unavailable.", 0, 0);
            goal.DynamicMoveResourceId = resourceId;
            var command = new MoveCommand(goal.PlayerId, actorIds.ToArray(), target);
            if (!TryResolveNativeMovementSlots(goal, command, currentTick,
                    out MoveCommand formation, out string reason))
                return new CommanderPlan(CommanderGoalStatus.Blocked,
                    "No complete reachable formation exists for the selected units; no prefix moved. " + reason,
                    0, 0);
            return new CommanderPlan(CommanderGoalStatus.Executing,
                "Moving the full selected unit set to the exact visible resource using native Move.",
                0, 0, formation);
        }
    }
}
