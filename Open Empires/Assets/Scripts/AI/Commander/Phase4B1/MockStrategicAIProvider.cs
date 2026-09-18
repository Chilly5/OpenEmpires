using System;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace OpenEmpires
{
    public sealed class MockStrategicAIProvider : IStrategicAIInterpreter
    {
        public Task<StrategicAIProviderResult> InterpretStrategicIntentAsync(
            StrategicAIRequest request, CancellationToken cancellationToken)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            cancellationToken.ThrowIfCancellationRequested();
            string text = Regex.Replace(request.PlayerMessage.Trim().ToLowerInvariant(), @"\s+", " ");
            if (text.EndsWith(".", StringComparison.Ordinal)) text = text.TrimEnd('.').TrimEnd();
            string objective;
            string parameters = "{}";
            // Whole-phrase matching prevents silently ignoring extra or hostile instructions.
            switch (text)
            {
                case "prepare cavalry attack":
                case "prepare a cavalry attack":
                    objective = "AttackPreparation";
                    parameters = "{\"focus\":\"cavalry\"}";
                    break;
                case "prepare attack":
                case "prepare an attack":
                    if (!HasCavalryPreference(request.MemorySnapshot))
                        return Task.FromResult(StrategicAIProviderResult.Rejected(
                            "Choose the supported attack focus first: focus cavalry."));
                    objective = "AttackPreparation";
                    parameters = "{\"focus\":\"cavalry\"}";
                    break;
                case "prepare defenses":
                case "prepare our defenses": objective = "DefensivePreparation"; break;
                case "expand economy":
                case "focus on expanding our economy": objective = "EconomicExpansion"; break;
                case "build up army":
                case "build up our army": objective = "MilitaryReinforcement"; break;
                default: return Task.FromResult(StrategicAIProviderResult.Rejected(
                    "Unsupported strategic request. Try preparing an attack, defenses, economy, or army."));
            }
            string json = "{\"intentCategory\":\"Strategic\",\"objectiveType\":\"" + objective
                + "\",\"parameters\":" + parameters + "}";
            return Task.FromResult(StrategicAIJson.Parse(json, request));
        }

        private static bool HasCavalryPreference(
            System.Collections.Generic.IReadOnlyList<MemoryEntry> memory)
        {
            for (int i = memory.Count - 1; i >= 0; i--)
                if (memory[i].Kind == MemoryEntryKind.Preference
                    && memory[i].CavalryPreference == CommanderCavalryPreference.Cavalry)
                    return true;
            return false;
        }
    }
}
