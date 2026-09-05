using System.Threading;
using System.Threading.Tasks;

namespace OpenEmpires
{
    public enum CommanderIntentInterpretationStatus
    {
        Interpreted,
        Rejected
    }

    public enum CommanderIntentErrorCode
    {
        None,
        EmptyInput,
        UnknownCommand,
        UnknownUnit,
        UnknownResource,
        UnknownStructure,
        MissingAmount,
        InvalidAmount,
        AmountOutOfRange,
        InvalidPlayer,
        PlayerMismatch,
        UnsupportedConstraint,
        UnsupportedIntentExecution,
        InvalidJson,
        ProviderFailure,
        TimedOut,
        Cancelled,
        SubmissionInProgress
    }

    public sealed class CommanderIntentInterpretation
    {
        public CommanderIntentInterpretationStatus Status { get; }
        public CommanderIntent Intent { get; }
        public CommanderIntentErrorCode ErrorCode { get; }
        public string Reason { get; }
        public string ErrorField { get; }
        public bool Success => Status == CommanderIntentInterpretationStatus.Interpreted;

        private CommanderIntentInterpretation(CommanderIntentInterpretationStatus status,
            CommanderIntent intent, CommanderIntentErrorCode errorCode, string reason, string errorField = "")
        {
            Status = status;
            Intent = intent;
            ErrorCode = errorCode;
            Reason = reason ?? string.Empty;
            ErrorField = errorField ?? string.Empty;
        }

        public static CommanderIntentInterpretation Accepted(CommanderIntent intent)
        {
            return new CommanderIntentInterpretation(
                CommanderIntentInterpretationStatus.Interpreted, intent,
                CommanderIntentErrorCode.None, string.Empty);
        }

        public static CommanderIntentInterpretation Rejected(
            CommanderIntentErrorCode errorCode, string reason, string errorField = "")
        {
            return new CommanderIntentInterpretation(
                CommanderIntentInterpretationStatus.Rejected, null, errorCode, reason, errorField);
        }
    }

    public interface ICommanderIntentInterpreter
    {
        Task<CommanderIntentInterpretation> InterpretAsync(string playerInput,
            CommanderContext context, CancellationToken cancellationToken);
    }

    // Optional compatibility path for local, immediate parsers. Never waits on a Task.
    public interface ISynchronousCommanderIntentInterpreter : ICommanderIntentInterpreter
    {
        CommanderIntentInterpretation Interpret(string playerInput, int playerId);
    }
}
