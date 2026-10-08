using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;

namespace OpenEmpires
{
    // Translation-only input. Identity and time come from the existing detached snapshot.
    // The caller assigns identity but does not submit it to any execution service here.
    public sealed class StrategicAIRequest
    {
        private readonly StrategicIntentIdProvider identityOwner;
        private readonly object identitySync=new object();
        private StrategicIntent boundIntent;
        private bool ownershipClosed;
        public string PlayerMessage { get; }
        public StrategicContext Context { get; }
        public int IntentId { get; }
        public IReadOnlyList<CommanderConversationMessage> ConversationHistory { get; }
        public IReadOnlyList<MemoryEntry> MemorySnapshot { get; }

        public StrategicAIRequest(string playerMessage, StrategicContext context,
            StrategicIntentIdProvider intentIds,
            IReadOnlyList<CommanderConversationMessage> conversationHistory = null,
            IReadOnlyList<MemoryEntry> memorySnapshot = null)
            : this(playerMessage, context, 0, conversationHistory, memorySnapshot,true)
        {
            identityOwner = intentIds??throw new ArgumentNullException(nameof(intentIds));
            IntentId=identityOwner.Allocate();
            identityOwner.BindAllocated(IntentId, this);
        }

        internal bool BindIntent(StrategicIntent intent)
        {
            if(identityOwner==null)return true; // Detached legacy parser input owns no claim.
            lock(identitySync)
            {
                if(ownershipClosed||!identityOwner.Transfer(IntentId,this,intent))return false;
                boundIntent=intent;return true;
            }
        }
        internal void CompleteOwnershipTransfer(){lock(identitySync){ownershipClosed=true;boundIntent=null;}}
        internal void ReleaseOwnership()
        {
            if(identityOwner==null)return;
            lock(identitySync)
            {if(ownershipClosed)return;ownershipClosed=true;identityOwner.Retire(IntentId,(object)boundIntent??this);boundIntent=null;}
        }

        public StrategicAIRequest(string playerMessage, StrategicContext context, int intentId,
            IReadOnlyList<CommanderConversationMessage> conversationHistory = null,
            IReadOnlyList<MemoryEntry> memorySnapshot = null)
            :this(playerMessage,context,intentId,conversationHistory,memorySnapshot,false){}

        private StrategicAIRequest(string playerMessage, StrategicContext context, int intentId,
            IReadOnlyList<CommanderConversationMessage> conversationHistory,
            IReadOnlyList<MemoryEntry> memorySnapshot,bool allowUnassigned)
        {
            if (!allowUnassigned&&intentId < 1) throw new ArgumentOutOfRangeException(nameof(intentId));
            Context = context ?? throw new ArgumentNullException(nameof(context));
            PlayerMessage = playerMessage ?? string.Empty;
            IntentId = intentId;
            // Reuse Phase 4A history limits and detach from caller-owned mutable lists.
            var history = new CommanderConversationHistory();
            if (conversationHistory != null)
                for (int i = Math.Max(0, conversationHistory.Count - history.Capacity);
                    i < conversationHistory.Count; i++)
                {
                    var turn = conversationHistory[i];
                    if (turn == null) continue;
                    if (turn.Role != CommanderConversationRole.Player
                        && turn.Role != CommanderConversationRole.Commander)
                        throw new ArgumentException("Unsupported conversation role.", nameof(conversationHistory));
                    history.Append(turn.Role, turn.Text);
                }
            ConversationHistory = history.Snapshot();
            var memory = new List<MemoryEntry>();
            if (memorySnapshot != null)
                for (int i = Math.Max(0, memorySnapshot.Count - CommanderMemory.MaximumCapacity);
                    i < memorySnapshot.Count; i++)
                {
                    MemoryEntry entry = memorySnapshot[i];
                    if (entry != null) memory.Add(entry.Copy());
                }
            MemorySnapshot = new ReadOnlyCollection<MemoryEntry>(memory);
        }
    }

    public interface IStrategicAIInterpreter
    {
        Task<StrategicAIProviderResult> InterpretStrategicIntentAsync(
            StrategicAIRequest request, CancellationToken cancellationToken);
    }

    // Dedicated immutable wire DTO: no tactical fields, identity, priority, or runtime types.
    public sealed class StrategicIntentDTO
    {
        public string intentCategory => "Strategic";
        public string objectiveType { get; }
        public IReadOnlyDictionary<string, string> parameters { get; }

        internal StrategicIntentDTO(string objective, IDictionary<string, string> values)
        {
            objectiveType = objective;
            parameters = new ReadOnlyDictionary<string, string>(
                new Dictionary<string, string>(values, StringComparer.Ordinal));
        }
    }

    public sealed class StrategicAIProviderResult
    {
        public bool Success => Intent != null;
        public StrategicIntentDTO IntentDto { get; }
        public StrategicIntent Intent { get; }
        public string IntentJson { get; }
        public string ExplanationText { get; }
        public IReadOnlyList<string> ValidationErrors { get; }

        private StrategicAIProviderResult(StrategicIntentDTO dto, StrategicIntent intent,
            string json, string explanation, string[] errors)
        {
            IntentDto = dto;
            Intent = intent;
            IntentJson = json;
            ExplanationText = explanation;
            ValidationErrors = Array.AsReadOnly(errors);
        }

        internal static StrategicAIProviderResult Accepted(StrategicIntentDTO dto,
            StrategicIntent intent, string json) => new StrategicAIProviderResult(dto, intent,
                json, intent.ObjectiveType + " intent created. No execution.", Array.Empty<string>());

        public static StrategicAIProviderResult Rejected(string safeReason) =>
            new StrategicAIProviderResult(null, null, string.Empty, safeReason, new[] { safeReason });
    }
}
