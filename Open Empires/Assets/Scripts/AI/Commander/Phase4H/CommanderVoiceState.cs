namespace OpenEmpires
{
    /// <summary>
    /// Explicit bounded state machine for Commander voice input lifecycle.
    /// </summary>
    public enum CommanderVoiceState
    {
        Idle,
        Recording,
        Transcribing,
        Preview,
        Error
    }
}
