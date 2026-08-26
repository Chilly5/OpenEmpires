using System.Collections.Generic;

namespace OpenEmpires
{
    public class CommandBuffer
    {
        private readonly List<ICommand> pendingCommands = new List<ICommand>();
        private readonly List<ICommand> executingCommands = new List<ICommand>();

        /// <summary>
        /// When true, enqueued commands are silently dropped. Used by observe-only modes
        /// (AI Village) to keep the normal UI functional for selection/inspection while
        /// preventing the player from issuing orders.
        /// </summary>
        public bool BlockEnqueue;

        public void EnqueueCommand(ICommand command)
        {
            if (BlockEnqueue) return;
            pendingCommands.Add(command);
        }

        public List<ICommand> FlushCommands()
        {
            executingCommands.Clear();
            executingCommands.AddRange(pendingCommands);
            pendingCommands.Clear();
            return executingCommands;
        }
    }
}
