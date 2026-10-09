using System;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace OpenEmpires
{
    public sealed partial class CommanderChatUI
    {
        private CommanderPendingClarification pendingClarification;
        private long clarificationSequence;
        private bool destroyCleanupComplete;
        public CommanderPendingClarification PendingClarification => pendingClarification;

        private bool HandlePendingControl(string message)
        {
            if (pendingClarification == null) return false;
            if (pendingClarification.RuntimeGeneration != runtimeGeneration) { pendingClarification = null; return false; }
            string whole = message.Trim().ToLowerInvariant();
            if (whole == "cancel" || whole == "never mind" || whole == "nevermind" || whole == "forget it")
            {
                pendingClarification = null; AppendLine("Player", message); AppendLine("Commander", "Pending worker request cancelled; no goal was created."); return true;
            }
            // This only chooses whether a new turn supersedes a pending question.
            // It neither parses nor executes any gameplay action.
            string[] standalone = { "build ", "train ", "make ", "produce ", "research ", "attack ", "scout ", "patrol ", "retreat ", "repair ", "defend ", "advance ", "put ", "gather ", "send ", "reach ", "assign ", "have ", "keep " };
            if (standalone.Any(prefix => whole.StartsWith(prefix, StringComparison.Ordinal)))
            { pendingClarification = null; return false; }
            var previous = pendingClarification;
            pendingClarification = new CommanderPendingClarification(previous.Draft, previous.OriginalText, previous.Question,
                previous.RuntimeGeneration, previous.Sequence, previous.Turns + 1, previous.RequestTicket);
            if (whole == "yes" || whole == "no" || whole == "those" || whole == "there")
            { AppendLine("Player", message); ReaskPending("Please specify the missing worker criteria."); return true; }
            return false;
        }

        private void ReaskPending(string reason)
        {
            if (pendingClarification == null) return;
            if (pendingClarification.Turns >= 3)
            { pendingClarification = null; AppendLine("Commander", "Worker clarification expired after three replies; please give a new complete request."); }
            else AppendLine("Commander", reason + " " + pendingClarification.Question);
        }

        private CommanderSemanticResult LocalCountContinuation(CommanderPendingClarification pending, string reply)
        {
            if (pending == null || pending.Draft.CountMode != CommanderWorkerCountMode.Exact || pending.Draft.Count.HasValue
                || !CommanderClarificationReplies.TryParseCount(reply, out int count)) return null;
            var updated = pending.Draft.WithCount(count);
            if (updated.MissingFields.Count == 0) return updated.CompleteResult();
            var missing = new JArray(); foreach (var field in updated.MissingFields) missing.Add(field.ToString());
            return CommanderSemanticJson.Parse(new JObject { ["outcome"] = "Clarify", ["message"] = "Which resource should those villagers gather?",
                ["pending"] = updated.ToJson(), ["missingFields"] = missing }.ToString(Formatting.None));
        }

        private bool HandleClarificationResult(CommanderSemanticResult result, CommanderPendingClarification previous,
            string reply, int generation, CommanderRequestTicket requestTicket)
        {
            if (previous != null && !ReferenceEquals(previous, pendingClarification)) return true;
            if (result.PendingDraft != null)
            {
                if (previous != null && !PreservesResolvedCriteria(previous.Draft, result.PendingDraft, reply))
                { ReaskPending("The reply did not safely preserve the existing worker request."); return true; }
                if (previous != null && previous.Turns >= 3)
                { ReaskPending(string.Empty); return true; }
                pendingClarification = new CommanderPendingClarification(result.PendingDraft,
                    previous?.OriginalText ?? reply, string.IsNullOrWhiteSpace(result.SafeExplanation) ? "Please specify the missing worker criteria." : result.SafeExplanation,
                    generation, previous?.Sequence ?? ++clarificationSequence, previous?.Turns ?? 0, previous?.RequestTicket ?? requestTicket);
                return false;
            }
            if (previous == null) return false;
            if (result.Outcome != CommanderSemanticOutcome.Request)
            { ReaskPending("That reply did not resolve the worker request."); return true; }
            if (result.Nodes.Count != 1 || result.Nodes[0].WorkerAllocation == null)
            { ReaskPending("That reply cannot change the pending worker request into a different action."); return true; }
            var completed = result.Nodes[0].WorkerAllocation;
            var draft = new CommanderWorkerAllocationDraft(completed.Mode, completed.CountMode, completed.Count, completed.Workers, completed.Destination,
                completed.ResourceAmount, completed.ResourceAmountMode);
            if (!PreservesResolvedCriteria(previous.Draft, draft, reply))
            { ReaskPending("The reply did not safely preserve the existing worker request."); return true; }
            // Clear only after strict parsing produced a compatible complete request;
            // normal admission still owns population, authority and gameplay checks.
            pendingClarification = null;
            return false;
        }

        private static bool PreservesResolvedCriteria(CommanderWorkerAllocationDraft previous, CommanderWorkerAllocationDraft next, string reply)
        {
            string[] tokens = reply.Trim().ToLowerInvariant().Split(new[] { ' ', '\t', ',', '.', '!', '?' }, StringSplitOptions.RemoveEmptyEntries);
            if (previous.Mode != next.Mode || previous.CountMode != next.CountMode) return false;
            if (previous.Count.HasValue && previous.Count != next.Count)
            {
                bool explicitCorrection = tokens.Any(t => t == "actually" || t == "instead" || t == "change" || t == "rather");
                bool namesNewCount = next.Count.HasValue && tokens.Any(t =>
                    CommanderClarificationReplies.TryParseCount(t, out int named) && named == next.Count.Value);
                if (!explicitCorrection || !namesNewCount) return false;
            }
            // A resource correction must name the new amount/meaning. A bare worker-slot
            // number cannot silently replace already resolved resource facts.
            if (previous.ResourceAmount != next.ResourceAmount)
            {
                if (!previous.ResourceAmount.HasValue || !next.ResourceAmount.HasValue
                    || !tokens.Contains(next.ResourceAmount.Value.ToString(System.Globalization.CultureInfo.InvariantCulture))
                    || !tokens.Any(t => t == "actually" || t == "instead" || t == "change" || t == "rather")) return false;
            }
            if (previous.ResourceAmountMode != next.ResourceAmountMode)
            {
                if (!next.ResourceAmountMode.HasValue || (next.ResourceAmountMode == CommanderResourceAmountMode.AdditionalGathered
                    ? !tokens.Contains("additional") : !tokens.Contains("stockpile") && !tokens.Contains("have"))) return false;
            }
            // Token confirmation is a correction guard, not an action parser: the
            // provider still supplies the typed selector and strict admission validates it.
            if (previous.Workers.State != next.Workers.State
                && !tokens.Contains(next.Workers.State.ToString().ToLowerInvariant())) return false;
            if (previous.Workers.CurrentResource != next.Workers.CurrentResource)
            {
                if (!next.Workers.CurrentResource.HasValue) return false;
                string current = next.Workers.CurrentResource.Value.ToString().ToLowerInvariant();
                bool explicitFrom = false;
                for (int i = 0; i + 1 < tokens.Length; i++)
                    if (tokens[i] == "from" && tokens[i + 1] == current) explicitFrom = true;
                if (!explicitFrom) return false;
            }
            if (previous.Destination == null) return true;
            if (next.Destination == null) return false;
            if (previous.Destination.Resource == next.Destination.Resource && previous.Destination.SourceKind == next.Destination.SourceKind) return true;
            // An explicitly named replacement destination may change that slot; the
            // provider is not allowed to silently drop an already-resolved source.
            string resource = next.Destination.Resource.ToString().ToLowerInvariant();
            string source = next.Destination.SourceKind.ToString().ToLowerInvariant();
            if (next.Destination.Resource != previous.Destination.Resource)
                return tokens.Contains(resource) && (next.Destination.SourceKind == ResourceSourceKind.Any || tokens.Contains(source));
            return next.Destination.SourceKind != ResourceSourceKind.Any && tokens.Contains(source);
        }
    }
}
