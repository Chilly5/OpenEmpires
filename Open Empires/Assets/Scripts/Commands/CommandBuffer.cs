using System;
using System.Collections.Generic;

namespace OpenEmpires
{
    public enum CommandEnqueueSource
    {
        Human,
        Commander
    }

    public class CommandBuffer
    {
        private readonly List<ICommand> pendingCommands = new List<ICommand>();
        private readonly List<ICommand> executingCommands = new List<ICommand>();
        public event Action<ICommand, CommandEnqueueSource> CommandEnqueued;

        public void EnqueueCommand(ICommand command)
        {
            EnqueueCommand(command, CommandEnqueueSource.Human);
        }

        public void EnqueueCommand(ICommand command, CommandEnqueueSource source)
        {
            if (command == null) throw new ArgumentNullException(nameof(command));
            pendingCommands.Add(command);
            CommandEnqueued?.Invoke(command, source);
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
