using System;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace OpenEmpires
{
    public sealed class MockAIProvider : ICommanderAIProvider
    {
        private static readonly Regex MakeUnits = new Regex(
            @"^\s*make\s+(\d+)\s+(spearmen|archers|knights)\s*[.!]?\s*$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        private static readonly Regex Allocate = new Regex(
            @"^\s*put\s+(\d+)\s+villagers?\s+on\s+(wood|food)\s*[.!]?\s*$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        private static readonly Regex BuildBarracks = new Regex(
            @"^\s*build\s+(?:a\s+|one\s+)?barracks\s*[.!]?\s*$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        public Task<CommanderAIProviderResult> TranslateAsync(CommanderAIRequest request,
            CancellationToken cancellationToken)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            cancellationToken.ThrowIfCancellationRequested();

            string json = BuildJson(request.PlayerMessage);
            CommanderAIProviderResult result = json == null
                ? CommanderAIProviderResult.Rejected(CommanderIntentErrorCode.UnknownCommand,
                    "The mock provider supports only the Phase 4A tactical phrases.")
                : CommanderAIJson.ParseTacticalIntent(json, request.Context);
            return Task.FromResult(result);
        }

        public static string BuildJson(string playerMessage)
        {
            string text = playerMessage ?? string.Empty;
            Match units = MakeUnits.Match(text);
            if (units.Success)
            {
                string plural = units.Groups[2].Value.ToLowerInvariant();
                string unit = plural == "spearmen" ? "Spearman"
                    : plural == "archers" ? "Archer" : "Knight";
                return Json("EnsureUnitCount", new JObject
                {
                    ["unit"] = unit,
                    ["count"] = int.Parse(units.Groups[1].Value)
                });
            }

            if (BuildBarracks.IsMatch(text))
                return Json("BuildStructure", new JObject
                {
                    ["structure"] = "Barracks",
                    ["count"] = 1
                });

            Match allocation = Allocate.Match(text);
            if (allocation.Success)
                return Json("SetResourceAllocation", new JObject
                {
                    ["resource"] = char.ToUpperInvariant(allocation.Groups[2].Value[0])
                        + allocation.Groups[2].Value.Substring(1).ToLowerInvariant(),
                    ["count"] = int.Parse(allocation.Groups[1].Value)
                });
            return null;
        }

        private static string Json(string intentType, JObject parameters)
        {
            return new JObject
            {
                ["intentCategory"] = "Tactical",
                ["intentType"] = intentType,
                ["parameters"] = parameters
            }.ToString(Formatting.None);
        }
    }
}
