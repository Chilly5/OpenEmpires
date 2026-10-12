using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Newtonsoft.Json.Linq;

namespace OpenEmpires
{
    // Reported observations only: no headers, response text, credentials or game authority.
    public sealed class CommanderProviderUsage
    {
        public long? PromptTokens { get; }
        public long? CompletionTokens { get; }
        public long? TotalTokens { get; }
        public long? CachedTokens { get; }
        public long? ReasoningTokens { get; }
        public decimal? Cost { get; }
        public bool HasReportedUsage { get; }
        public static CommanderProviderUsage Unknown { get; } = new CommanderProviderUsage(null);

        private CommanderProviderUsage(JObject value)
        {
            PromptTokens = NonnegativeInteger(value?["prompt_tokens"]);
            CompletionTokens = NonnegativeInteger(value?["completion_tokens"]);
            TotalTokens = NonnegativeInteger(value?["total_tokens"]);
            CachedTokens = NonnegativeInteger((value?["prompt_tokens_details"] as JObject)?["cached_tokens"]);
            ReasoningTokens = NonnegativeInteger((value?["completion_tokens_details"] as JObject)?["reasoning_tokens"]);
            try
            {
                JToken cost = value?["cost"];
                if (cost != null && (cost.Type == JTokenType.Float || cost.Type == JTokenType.Integer))
                { decimal amount = cost.Value<decimal>(); if (amount >= 0) Cost = amount; }
            }
            catch (Exception error) when (error is OverflowException || error is FormatException || error is InvalidCastException) { }
            HasReportedUsage = PromptTokens.HasValue || CompletionTokens.HasValue || TotalTokens.HasValue
                || CachedTokens.HasValue || ReasoningTokens.HasValue || Cost.HasValue;
        }

        public static CommanderProviderUsage Read(JObject envelope)
            => envelope?["usage"] is JObject usage ? new CommanderProviderUsage(usage) : Unknown;

        private static long? NonnegativeInteger(JToken value)
        {
            if (value == null || value.Type != JTokenType.Integer) return null;
            try { long count = value.Value<long>(); return count >= 0 ? count : (long?)null; }
            catch (Exception error) when (error is OverflowException || error is FormatException || error is InvalidCastException)
            { return null; }
        }
    }

    // Immutable, redacted observation of one HTTP attempt. It never retains payloads or headers.
    public sealed class CommanderProviderAttempt
    {
        public string Kind { get; }
        public int InputEstimate { get; }
        public IReadOnlyDictionary<string, int> SectionEstimates { get; }
        public CommanderProviderUsage Usage { get; }
        public long ElapsedMilliseconds { get; }
        public int? HttpStatusCode { get; }
        public string ResultStage { get; }

        public CommanderProviderAttempt(string kind, int inputEstimate, IReadOnlyDictionary<string, int> sections,
            CommanderProviderUsage usage, long elapsedMilliseconds, int? httpStatusCode, string resultStage)
        {
            Kind = kind;
            InputEstimate = inputEstimate;
            SectionEstimates = new ReadOnlyDictionary<string, int>(new Dictionary<string, int>(sections));
            Usage = usage ?? CommanderProviderUsage.Unknown;
            ElapsedMilliseconds = elapsedMilliseconds;
            HttpStatusCode = httpStatusCode;
            ResultStage = resultStage;
        }

        internal CommanderProviderAttempt WithResultStage(string stage) => new CommanderProviderAttempt(
            Kind, InputEstimate, SectionEstimates, Usage, ElapsedMilliseconds, HttpStatusCode, stage);
    }
}
