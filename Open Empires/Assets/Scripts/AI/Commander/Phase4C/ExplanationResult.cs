namespace OpenEmpires
{
    public sealed class ExplanationResult
    {
        public const int MaximumDisplayTextLength = 8192;
        public string DisplayText { get; }
        public ExplanationOutcome Outcome { get; }

        public ExplanationResult(string displayText, ExplanationOutcome outcome)
        {
            displayText = displayText ?? string.Empty;
            DisplayText = displayText.Length <= MaximumDisplayTextLength
                ? displayText : displayText.Substring(0, MaximumDisplayTextLength);
            Outcome = outcome;
        }
    }
}
