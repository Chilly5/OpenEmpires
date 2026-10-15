using System;

namespace OpenEmpires
{
    // One vocabulary for the two accepted movement representations. Request keeps its
    // saved display selectors; DynamicPlan uses canonical unit IDs for named units.
    internal static class CommanderMovementContract
    {
        internal const string RequestSelectionSlotPath = "unitSelector";
        internal const string MarkerLocationSlotPath = "location";
        internal const string CountSlotPath = "count";

        internal static readonly string[] RequestSelectors =
        {
            "Military", "Scout", "Villagers", "Spearman", "Archer", "Knight", "DamagedMilitary"
        };

        internal static bool TryRequestSelector(string token, out CommanderSemanticUnitSelector selector)
        {
            for (int i = 0; i < RequestSelectors.Length; i++)
                if (string.Equals(token, RequestSelectors[i], StringComparison.Ordinal))
                {
                    selector = (CommanderSemanticUnitSelector)Enum.Parse(typeof(CommanderSemanticUnitSelector), token);
                    return true;
                }
            selector = default;
            return false;
        }

        internal static bool IsDynamicKind(string token)
            => string.Equals(token, "AllUnits", StringComparison.Ordinal)
                || TryRequestSelector(token, out _);

        internal static string RequestSelectorVocabulary => string.Join("|", RequestSelectors);
        internal static string DynamicKindVocabulary => "AllUnits|" + RequestSelectorVocabulary;

        internal static string ProviderGuidance =>
            "Existing-unit marker movement accepts both strictly validated forms. "
            + "Request MoveUnits uses unitSelector:" + RequestSelectorVocabulary
            + ", count:1..50 and location:PlayerMarker; its named selectors are legacy display tokens. "
            + "DynamicPlan select-units uses unit:canonical unit:id for named units or kind:"
            + DynamicKindVocabulary
            + "; exactly one of count:1..50 or countMode:AllMatching, with optional typed excludeKind/excludeUnit. "
            + "Use the canonical catalog id for every DynamicPlan named unit, including civilization replacements. "
            + "A Request display token is never a DynamicPlan unit id. Both forms keep the same gameplay approval requirement. ";
    }
}
