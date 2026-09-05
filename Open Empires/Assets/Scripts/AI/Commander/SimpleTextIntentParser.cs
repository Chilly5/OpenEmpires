using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace OpenEmpires
{
    public sealed class SimpleTextIntentParser : ISynchronousCommanderIntentInterpreter
    {
        public System.Threading.Tasks.Task<CommanderIntentInterpretation> InterpretAsync(string playerInput,
            CommanderContext context, System.Threading.CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return System.Threading.Tasks.Task.FromResult(Interpret(playerInput, context.PlayerId));
        }

        private const RegexOptions PatternOptions = RegexOptions.IgnoreCase
            | RegexOptions.CultureInvariant | RegexOptions.Compiled;

        private static readonly Regex UnitPattern = new Regex(
            @"^(?:please\s+)?(?:make|create|train|produce|recruit)\s+([+-]?\d+)\s+(.+?)\s*[.!?]*$",
            PatternOptions);
        private static readonly Regex ExactResourcePattern = new Regex(
            @"^(?:please\s+)?(?:put|move|assign)\s+([+-]?\d+)\s+(?:villagers?|workers?)\s+(?:on|to)\s+(.+?)\s*[.!?]*$",
            PatternOptions);
        private static readonly Regex MoreResourcePattern = new Regex(
            @"^(?:please\s+)?(?:more|increase)\s+(.+?)\s+(?:villagers?|workers?)\s*[.!?]*$",
            PatternOptions);
        private static readonly Regex BuildingPattern = new Regex(
            @"^(?:please\s+)?(?:build|construct|create|make)\s+(?:([+-]?\d+)\s+)?(?:(?:a|an)\s+)?(.+?)\s*[.!?]*$",
            PatternOptions);

        private static readonly Regex ProtectedResourcePattern = new Regex(
            @"\s*(?:,?\s*(?:but|and))?\s*(?:do\s+not|don't|dont)\s+touch\s+(food|wood|gold|stone)\b",
            PatternOptions);
        private static readonly Regex IdleOnlyPattern = new Regex(
            @"\s*(?:,?\s*(?:but|and))?\s*use\s+(?:only\s+)?idle\s+(?:villagers?|workers?)(?:\s+only)?\b",
            PatternOptions);
        private static readonly Regex MaximumQueuePattern = new Regex(
            @"\s*(?:,?\s*(?:but|and))?\s*(?:(?:maximum|max)\s+queue(?:\s+(?:of|to))?|(?:do\s+not|don't|dont)\s+queue\s+more\s+than)\s+([+-]?\d+)\b",
            PatternOptions);
        private static readonly Regex TrailingConnectorPattern = new Regex(
            @"\s*(?:,|\b(?:but|and)\b)\s*$", PatternOptions);

        public CommanderIntentInterpretation Interpret(string playerInput, int playerId)
        {
            if (string.IsNullOrWhiteSpace(playerInput))
                return CommanderIntentInterpretation.Rejected(
                    CommanderIntentErrorCode.EmptyInput, "Enter a Commander command.");

            var constraints = new List<CommanderConstraint>();
            string command = ExtractConstraints(playerInput.Trim(), constraints);

            Match match = UnitPattern.Match(command);
            if (match.Success)
            {
                CommanderIntentInterpretation unit = ParseUnit(match, playerId, constraints);
                if (unit.Success || unit.ErrorCode != CommanderIntentErrorCode.UnknownUnit)
                    return unit;

                if (CommanderIntentCatalog.TryResolveStructure(match.Groups[2].Value,
                    out BuildingType countedStructure))
                {
                    if (!TryParseAmount(match.Groups[1].Value, out int structureCount))
                        return InvalidAmount(match.Groups[1].Value);
                    return CommanderIntentInterpretation.Accepted(
                        new BuildStructureIntent(playerId, countedStructure, structureCount, constraints));
                }
                return unit;
            }

            match = ExactResourcePattern.Match(command);
            if (match.Success) return ParseExactResource(match, playerId, constraints);

            match = MoreResourcePattern.Match(command);
            if (match.Success) return ParseMoreResource(match, playerId, constraints);

            match = BuildingPattern.Match(command);
            if (match.Success) return ParseBuilding(match, playerId, constraints);

            return CommanderIntentInterpretation.Rejected(
                CommanderIntentErrorCode.UnknownCommand,
                "The command does not match a supported unit, resource, or building request.");
        }

        private static CommanderIntentInterpretation ParseUnit(Match match, int playerId,
            List<CommanderConstraint> constraints)
        {
            if (!TryParseAmount(match.Groups[1].Value, out int amount))
                return InvalidAmount(match.Groups[1].Value);
            if (amount <= 0)
                return CommanderIntentInterpretation.Rejected(
                    CommanderIntentErrorCode.InvalidAmount, "Unit amount must be greater than zero.");
            if (!CommanderIntentCatalog.TryResolveUnit(match.Groups[2].Value, out int unitType))
                return CommanderIntentInterpretation.Rejected(
                    CommanderIntentErrorCode.UnknownUnit,
                    $"Unknown unit '{match.Groups[2].Value.Trim()}'.");
            return CommanderIntentInterpretation.Accepted(
                new EnsureUnitCountIntent(playerId, unitType, amount, constraints));
        }

        private static CommanderIntentInterpretation ParseExactResource(Match match, int playerId,
            List<CommanderConstraint> constraints)
        {
            if (!TryParseAmount(match.Groups[1].Value, out int amount))
                return InvalidAmount(match.Groups[1].Value);
            if (amount < 0)
                return CommanderIntentInterpretation.Rejected(
                    CommanderIntentErrorCode.InvalidAmount,
                    "Resource worker count cannot be negative.");
            if (!CommanderIntentCatalog.TryResolveResource(match.Groups[2].Value,
                out ResourceType resource))
            {
                return CommanderIntentInterpretation.Rejected(
                    CommanderIntentErrorCode.UnknownResource,
                    $"Unknown resource '{match.Groups[2].Value.Trim()}'.");
            }
            return CommanderIntentInterpretation.Accepted(
                new SetResourceAllocationIntent(playerId, resource,
                    ResourceAllocationMode.SetExact, amount, constraints));
        }

        private static CommanderIntentInterpretation ParseMoreResource(Match match, int playerId,
            List<CommanderConstraint> constraints)
        {
            if (!CommanderIntentCatalog.TryResolveResource(match.Groups[1].Value,
                out ResourceType resource))
            {
                return CommanderIntentInterpretation.Rejected(
                    CommanderIntentErrorCode.UnknownResource,
                    $"Unknown resource '{match.Groups[1].Value.Trim()}'.");
            }
            return CommanderIntentInterpretation.Accepted(
                new SetResourceAllocationIntent(playerId, resource,
                    ResourceAllocationMode.Increase, null, constraints));
        }

        private static CommanderIntentInterpretation ParseBuilding(Match match, int playerId,
            List<CommanderConstraint> constraints)
        {
            int count = 1;
            if (match.Groups[1].Success && !TryParseAmount(match.Groups[1].Value, out count))
                return InvalidAmount(match.Groups[1].Value);
            if (count <= 0)
                return CommanderIntentInterpretation.Rejected(
                    CommanderIntentErrorCode.InvalidAmount,
                    "Structure count must be greater than zero.");
            if (!CommanderIntentCatalog.TryResolveStructure(match.Groups[2].Value,
                out BuildingType structureType))
            {
                return CommanderIntentInterpretation.Rejected(
                    CommanderIntentErrorCode.UnknownStructure,
                    $"Unknown structure '{match.Groups[2].Value.Trim()}'.");
            }
            return CommanderIntentInterpretation.Accepted(
                new BuildStructureIntent(playerId, structureType, count, constraints));
        }

        private static string ExtractConstraints(string input, List<CommanderConstraint> constraints)
        {
            string command = input.Replace('\u2019', '\'');

            Match protectedResource = ProtectedResourcePattern.Match(command);
            if (protectedResource.Success
                && CommanderIntentCatalog.TryResolveResource(protectedResource.Groups[1].Value,
                    out ResourceType resource))
            {
                constraints.Add(new ProtectedResourceConstraint(resource));
                command = ProtectedResourcePattern.Replace(command, string.Empty, 1);
            }

            if (IdleOnlyPattern.IsMatch(command))
            {
                constraints.Add(new PreferredWorkersConstraint(
                    CommanderPreferredWorkerSource.IdleOnly));
                command = IdleOnlyPattern.Replace(command, string.Empty, 1);
            }

            Match maximumQueue = MaximumQueuePattern.Match(command);
            if (maximumQueue.Success)
            {
                int queue = int.TryParse(maximumQueue.Groups[1].Value, NumberStyles.Integer,
                    CultureInfo.InvariantCulture, out int value) ? value : int.MinValue;
                constraints.Add(new MaximumQueueConstraint(queue));
                command = MaximumQueuePattern.Replace(command, string.Empty, 1);
            }

            return TrailingConnectorPattern.Replace(command, string.Empty).Trim();
        }

        private static bool TryParseAmount(string text, out int amount)
        {
            return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out amount);
        }

        private static CommanderIntentInterpretation InvalidAmount(string text)
        {
            return CommanderIntentInterpretation.Rejected(
                CommanderIntentErrorCode.InvalidAmount, $"Invalid amount '{text}'.");
        }
    }
}
