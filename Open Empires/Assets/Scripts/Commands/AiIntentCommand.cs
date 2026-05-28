namespace OpenEmpires
{
    // Structured intent emitted by the LLM teammate controller (non-deterministic, local-only)
    // and broadcast to every client as a deterministic lockstep command. The LLM never
    // touches the sim directly — it produces these enum + int values, which are applied
    // identically on every client inside GameSimulation.ProcessAiIntentCommand.
    //
    // HARD RULE: this struct may only contain int fields. No strings, no floats. Positions
    // ride as Fixed32.Raw ints. Changing this rule will silently break lockstep determinism.
    public enum AiIntentKind
    {
        AttackAt           = 0, // ParamA/B = Fixed32 raw worldX/worldZ
        DefendAt           = 1, // ParamA/B = Fixed32 raw worldX/worldZ
        SetAggression      = 2, // ParamA = attackThreshold override (2..32), ParamB = retreatPercent (10..80)
        PrioritizeResource = 3, // ParamA = ResourceType, ParamB = weight delta (-5..+10)
        FocusEconomy       = 4, // ParamA = strength 0..100 (preset bundle)
        FocusMilitary      = 5, // ParamA = strength 0..100 (preset bundle)
        PushAgeUp          = 6, // ParamA = target age (2 or 3)
        Acknowledge        = 7, // chat-only, no behavior change
        Decline            = 8, // chat-only, no behavior change
        // ── Expanded "full control surface" intents (native function calling) ──
        BuildStructure     = 9,  // ParamA = BuildingType, ParamB/C = Fixed32 raw worldX/worldZ (converted to tile)
        TrainUnits         = 10, // ParamA = menu unit type, ParamB = count
        SetProductionMix   = 11, // ParamA = archer weight, ParamB = cavalry weight, ParamC = infantry weight (0..100)
        SetArmyRally       = 12, // ParamA/B = Fixed32 raw worldX/worldZ (rally for all military buildings)
        ScoutArea          = 13, // ParamA/B = Fixed32 raw worldX/worldZ
        RegroupArmy        = 14, // ParamA/B = Fixed32 raw worldX/worldZ (gather army)
        RetreatToBase      = 15, // ParamA/B = Fixed32 raw worldX/worldZ (own TC)
        Research           = 16, // ParamA = TechnologyType
    }

    public struct AiIntentCommand : ICommand
    {
        public CommandType Type => CommandType.AiIntent;
        public int PlayerId { get; set; }   // target AI player whose priorities change
        public int IssuerPlayerId;          // human who said it (for ally check + logging)
        public int IntentKind;
        public int ParamA;
        public int ParamB;
        public int ParamC;
        public int ParamD;
        public int DurationTicks;
        public int TriggerType;             // 0=immediate, 1=delay, 2=on_age_up, 3=on_army_size, 4=on_enemy_attack
        public int TriggerMagnitude;        // delay-ticks / target-age / army-count

        public AiIntentCommand(int playerId, int issuerPlayerId, AiIntentKind kind,
            int paramA, int paramB, int paramC, int paramD, int durationTicks,
            int triggerType = 0, int triggerMagnitude = 0)
        {
            PlayerId = playerId;
            IssuerPlayerId = issuerPlayerId;
            IntentKind = (int)kind;
            ParamA = paramA;
            ParamB = paramB;
            ParamC = paramC;
            ParamD = paramD;
            DurationTicks = durationTicks;
            TriggerType = triggerType;
            TriggerMagnitude = triggerMagnitude;
        }
    }
}
