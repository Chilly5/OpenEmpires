namespace OpenEmpires
{
    // Trusted game-side provenance. Never part of the strategic JSON schema.
    public enum StrategicIntentSource
    {
        PlayerDirect,
        AIRecommendation,
        AIConfirmedPlayerCommand
    }
}
