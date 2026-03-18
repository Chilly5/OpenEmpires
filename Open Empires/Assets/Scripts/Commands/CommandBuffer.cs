using System.Collections.Generic;

namespace OpenEmpires
{
    public class CommandBuffer
    {
        private readonly List<ICommand> pendingCommands = new List<ICommand>();
        private readonly List<ICommand> executingCommands = new List<ICommand>();

        public void EnqueueCommand(ICommand command)
        {
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
