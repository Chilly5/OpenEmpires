namespace OpenEmpires
{
    public enum PingType
    {
        Attention = 0, // Generic "look here" (yellow)
        Attack    = 1, // "Attack at this location" (red)
        Defend    = 2, // "Defend this location" (blue)
        Help      = 3, // "I need help here" (orange)
    }

    // Deterministic networked ping. Issued by humans (alt-click / chat keywords) and
    // by AI (auto-warn when under attack). All clients see the ping fire on the same
    // tick; UI renders it for the issuing player + allies; AIPlayerSystem reads it
    // as a team-intent signal.
    public struct PingCommand : ICommand
    {
        public CommandType Type => CommandType.Ping;
        public int PlayerId { get; set; }
        public int WorldX;  // Fixed32.Raw
        public int WorldZ;  // Fixed32.Raw
        public int PingTypeValue;

        public PingCommand(int playerId, int worldXRaw, int worldZRaw, PingType pingType)
        {
            PlayerId = playerId;
            WorldX = worldXRaw;
            WorldZ = worldZRaw;
            PingTypeValue = (int)pingType;
        }
    }
}
