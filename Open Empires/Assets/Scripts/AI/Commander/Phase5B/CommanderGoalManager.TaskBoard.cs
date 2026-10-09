using System.Threading;

namespace OpenEmpires
{
    public sealed partial class CommanderGoalManager
    {
        private static long nextTaskBoardRuntimeToken;
        // A detached card may outlive its UI or manager. This identity is never supplied by the provider.
        internal long TaskBoardRuntimeToken { get; } = Interlocked.Increment(ref nextTaskBoardRuntimeToken);
    }
}
