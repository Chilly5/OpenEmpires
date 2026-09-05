using System;
using System.Threading;
using System.Threading.Tasks;

namespace OpenEmpires
{
    // No networking. A future provider replaces this class at dispatcher construction.
    public sealed class MockLlmIntentInterpreter : ICommanderIntentInterpreter
    {
        private readonly int delayMilliseconds;
        private readonly string responseOverride;

        public MockLlmIntentInterpreter(int delayMilliseconds = 750, string responseOverride = null)
        {
            if (delayMilliseconds < 500 || delayMilliseconds > 2000)
                throw new ArgumentOutOfRangeException(nameof(delayMilliseconds), "Mock delay must be 500-2000 ms.");
            this.delayMilliseconds = delayMilliseconds;
            this.responseOverride = responseOverride;
        }

        public async Task<CommanderIntentInterpretation> InterpretAsync(string playerInput,
            CommanderContext context, CancellationToken cancellationToken)
        {
            await Task.Delay(delayMilliseconds, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            string json = responseOverride;
            if (json == null)
            {
                // Deterministic stand-in for model reasoning, followed by the real JSON boundary.
                var parsed = new SimpleTextIntentParser().Interpret(playerInput, context.PlayerId);
                if (!parsed.Success) return parsed;
                json = CommanderIntentDtoCodec.Serialize(CommanderIntentDtoCodec.FromIntent(parsed.Intent));
            }
            return CommanderIntentDtoCodec.InterpretJson(json, context);
        }
    }
}
