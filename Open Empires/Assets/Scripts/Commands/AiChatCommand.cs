namespace OpenEmpires
{
    // Templated chat lines an AI player can emit. The actual rendered string is
    // produced on each client from the enum + paramA, so the chat stays in lockstep
    // without sending freeform text through the deterministic command channel.
    public enum AiChatLineType
    {
        UnderAttack    = 0, // "My base is under attack!"
        NeedHelp       = 1, // "I need help!"
        AgingUp        = 2, // "Aging up to {age}" (paramA = target age 2/3)
        ArmyMoving     = 3, // "Army assembled, moving out!"
        BaseLost       = 4, // "I've lost my base..."
        Acknowledge    = 5, // "Got it."
        OnTheWayAttack = 6, // "On my way to attack!"
        OnTheWayDefend = 7, // "On my way to defend!"
        Acknowledging  = 8, // "Got it, on it."
    }

    public struct AiChatCommand : ICommand
    {
        public CommandType Type => CommandType.AiChat;
        public int PlayerId { get; set; }
        public int LineType;
        public int ParamA;

        public AiChatCommand(int playerId, AiChatLineType type, int paramA = 0)
        {
            PlayerId = playerId;
            LineType = (int)type;
            ParamA = paramA;
        }
    }
}
