using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace OpenEmpires
{
    // Owns translation and the exact displayed recommendation, never execution.
    public sealed class StrategicAIApprovalBridge : IDisposable
    {
        private readonly object sync = new object();
        private readonly IStrategicAIInterpreter interpreter;
        private readonly StrategicIntentIdProvider identities;
        private readonly Func<StrategicContext> contextProvider;
        private readonly TimeSpan timeout;
        private readonly Func<IReadOnlyList<MemoryEntry>> memorySnapshotProvider;
        private readonly CommanderConversationHistory history = new CommanderConversationHistory();
        private CancellationTokenSource activeRequest;
        private StrategicIntent pending;
        private bool busy;
        private bool disposed;
        private int generation;

        public StrategicIntent PendingIntent { get { lock (sync) return pending; } }
        public IReadOnlyList<CommanderConversationMessage> HistorySnapshot
        {
            get { lock (sync) return history.Snapshot(); }
        }

        public StrategicAIApprovalBridge(IStrategicAIInterpreter interpreter,
            StrategicIntentIdProvider identities, Func<StrategicContext> contextProvider,
            TimeSpan? providerTimeout = null)
            : this(interpreter, identities, contextProvider, providerTimeout, null)
        {
        }

        public StrategicAIApprovalBridge(IStrategicAIInterpreter interpreter,
            StrategicIntentIdProvider identities, Func<StrategicContext> contextProvider,
            TimeSpan? providerTimeout,
            Func<IReadOnlyList<MemoryEntry>> memorySnapshotProvider)
        {
            this.interpreter = interpreter ?? throw new ArgumentNullException(nameof(interpreter));
            this.identities = identities ?? throw new ArgumentNullException(nameof(identities));
            this.contextProvider = contextProvider ?? throw new ArgumentNullException(nameof(contextProvider));
            timeout = providerTimeout ?? TimeSpan.FromSeconds(15);
            this.memorySnapshotProvider = memorySnapshotProvider;
            if (timeout <= TimeSpan.Zero || timeout.TotalMilliseconds > int.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(providerTimeout));
        }

        public Task<StrategicAIProviderResult> TranslateAsync(string message,
            CancellationToken cancellationToken = default) =>
            TranslateCoreAsync(message, cancellationToken, null, false);

        public Task<StrategicAIProviderResult> TranslateWithMemoryAsync(string message,
            IReadOnlyList<MemoryEntry> memorySnapshot,
            CancellationToken cancellationToken = default)
        {
            if (memorySnapshot == null) throw new ArgumentNullException(nameof(memorySnapshot));
            return TranslateCoreAsync(message, cancellationToken, memorySnapshot, true);
        }

        private async Task<StrategicAIProviderResult> TranslateCoreAsync(string message,
            CancellationToken cancellationToken, IReadOnlyList<MemoryEntry> memorySnapshot,
            bool hasExplicitSnapshot)
        {
            CancellationTokenSource deadline;
            int requestGeneration;
            IReadOnlyList<CommanderConversationMessage> requestHistory;
            lock (sync)
            {
                if (disposed) return StrategicAIProviderResult.Rejected("Strategic chat is closed.");
                if (busy) return StrategicAIProviderResult.Rejected("A strategic translation is already in progress.");
                ClearPendingLocked();
                busy = true;
                requestGeneration = ++generation;
                deadline = activeRequest = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                requestHistory = memorySnapshotProvider == null
                    ? history.Snapshot() : Array.Empty<CommanderConversationMessage>();
            }
            try
            {
                deadline.CancelAfter(timeout);
                deadline.Token.ThrowIfCancellationRequested();
                IReadOnlyList<MemoryEntry> memory = memorySnapshot
                    ?? memorySnapshotProvider?.Invoke()
                    ?? Array.Empty<MemoryEntry>();
                if (!hasExplicitSnapshot)
                    memory = WithoutCurrentPlayerTurn(memory, message);
                if (IsGenericAttack(message) && !HasCavalryPreference(memory))
                    return StrategicAIProviderResult.Rejected(
                        "Choose the supported attack focus first: focus cavalry.");
                var request = new StrategicAIRequest(message, contextProvider(), identities,
                    requestHistory, memory);
                lock (sync)
                {
                    if (disposed || generation != requestGeneration || deadline.IsCancellationRequested)
                        return StrategicAIProviderResult.Rejected("Strategic translation was cancelled.");
                    history.Append(CommanderConversationRole.Player, message);
                }
                Task<StrategicAIProviderResult> translation = interpreter.InterpretStrategicIntentAsync(request, deadline.Token);
                if (translation == null) return StrategicAIProviderResult.Rejected("Strategic translation failed safely.");
                Task cancelled = Task.Delay(Timeout.Infinite, deadline.Token);
                if (await Task.WhenAny(translation, cancelled) != translation)
                {
                    _ = translation.ContinueWith(t => { _ = t.Exception; },
                        CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.Default);
                    deadline.Token.ThrowIfCancellationRequested();
                }
                StrategicAIProviderResult result = await translation;
                deadline.Token.ThrowIfCancellationRequested();
                StrategicIntent intent = result?.Intent;
                if (intent == null || !result.Success)
                    return StrategicAIProviderResult.Rejected("The request could not be translated into a safe strategic intent.");
                if (intent.IntentId != request.IntentId || intent.PlayerId != request.Context.PlayerId
                    || intent.CreatedTick != request.Context.SnapshotTick
                    || intent.Source != StrategicIntentSource.AIRecommendation
                    || intent.Status != StrategicIntentStatus.Created || intent.Priority.HasValue
                    || !identities.Owns(intent))
                    return StrategicAIProviderResult.Rejected("Strategic response did not match the trusted request.");
                var validation = new StrategicIntentValidator().Validate(intent, request.Context.PlayerId,
                    StrategicPlanRegistry.CreateDefault());
                if (!validation.IsValid) return StrategicAIProviderResult.Rejected("Strategic response failed validation.");
                lock (sync)
                {
                    if (disposed || generation != requestGeneration || deadline.IsCancellationRequested)
                        return StrategicAIProviderResult.Rejected("Strategic translation was cancelled.");
                    pending = intent;
                    history.Append(CommanderConversationRole.Commander,
                        intent.ObjectiveType + " recommendation ready for review.");
                }
                return result;
            }
            catch (OperationCanceledException)
            {
                return StrategicAIProviderResult.Rejected(cancellationToken.IsCancellationRequested
                    || disposed || generation != requestGeneration
                    ? "Strategic translation was cancelled." : "Strategic translation timed out.");
            }
            catch (Exception)
            {
                return StrategicAIProviderResult.Rejected("Strategic translation failed safely.");
            }
            finally
            {
                lock (sync)
                {
                    if (ReferenceEquals(activeRequest, deadline))
                    {
                        activeRequest = null;
                        busy = false;
                    }
                }
                deadline.Cancel();
                deadline.Dispose();
            }
        }

        public StrategicIntent TakeRecommendation(int intentId)
        {
            lock (sync)
            {
                if (disposed || busy || pending == null || pending.IntentId != intentId
                    || pending.Status != StrategicIntentStatus.Created || !identities.Owns(pending)) return null;
                StrategicIntent result = pending;
                pending = null;
                return result;
            }
        }

        public StrategicIntent Confirm(int intentId)
        {
            lock (sync)
            {
                if (disposed || busy || pending == null || pending.IntentId != intentId
                    || pending.Status != StrategicIntentStatus.Created) return null;
                var parameters = new Dictionary<string, string>();
                foreach (var pair in pending.Parameters) parameters.Add(pair.Key, pair.Value);
                var confirmed = new StrategicIntent(pending.IntentId, pending.PlayerId,
                    pending.ObjectiveType, pending.CreatedTick, parameters, null,
                    StrategicIntentSource.AIConfirmedPlayerCommand);
                if (!identities.Transfer(intentId, pending, confirmed)) return null;
                ClearPendingLocked();
                return confirmed;
            }
        }

        public void ClearPending()
        {
            lock (sync)
            {
                ++generation;
                ClearPendingLocked();
                activeRequest?.Cancel();
            }
        }

        public void Reset()
        {
            lock (sync)
            {
                ++generation;
                history.Clear();
                ClearPendingLocked();
                activeRequest?.Cancel();
                activeRequest = null;
                busy = false;
            }
        }

        private static bool IsGenericAttack(string message)
        {
            string text = Regex.Replace((message ?? string.Empty).Trim().ToLowerInvariant(),
                @"\s+", " ");
            if (text.EndsWith(".", StringComparison.Ordinal)) text = text.TrimEnd('.').TrimEnd();
            return text == "prepare attack" || text == "prepare an attack";
        }

        private static bool HasCavalryPreference(IReadOnlyList<MemoryEntry> memory)
        {
            for (int i = memory.Count - 1; i >= 0; i--)
                if (memory[i].Kind == MemoryEntryKind.Preference
                    && memory[i].CavalryPreference == CommanderCavalryPreference.Cavalry)
                    return true;
            return false;
        }

        private static IReadOnlyList<MemoryEntry> WithoutCurrentPlayerTurn(
            IReadOnlyList<MemoryEntry> memory, string message)
        {
            if (memory.Count == 0) return memory;
            MemoryEntry last = memory[memory.Count - 1];
            if (last.Kind != MemoryEntryKind.PlayerConversation
                || !string.Equals(last.Text, message ?? string.Empty, StringComparison.Ordinal))
                return memory;
            var copy = new List<MemoryEntry>(memory.Count - 1);
            for (int i = 0; i < memory.Count - 1; i++) copy.Add(memory[i]);
            return copy.AsReadOnly();
        }

        private void ClearPendingLocked()
        {
            if (pending != null && pending.Status == StrategicIntentStatus.Created)
                pending.Status = StrategicIntentStatus.Cancelled;
            pending = null;
        }

        public void Dispose()
        {
            lock (sync)
            {
                if (disposed) return;
                disposed = true;
                ++generation;
                history.Clear();
                ClearPendingLocked();
                activeRequest?.Cancel();
                activeRequest = null;
                busy = false;
            }
        }
    }
}
