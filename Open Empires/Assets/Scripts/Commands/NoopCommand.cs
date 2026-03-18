namespace OpenEmpires
{
    public struct NoopCommand : ICommand
    {
        public CommandType Type => CommandType.Noop;
        public int PlayerId { get; set; }

        /// <summary>
        /// Checksum of the sender's game state at the tick this Noop was sent.
        /// Used to detect desyncs between players.
        /// </summary>
        public uint StateChecksum;

        /// <summary>
        /// The sim tick at which the checksum was computed.
        /// With asymmetric input delays, players compute checksums at different sim ticks
        /// for the same command tick. Keying by SimTick ensures we only compare matching states.
        /// </summary>
        public int SimTick;

        /// <summary>
        /// XOR of all per-system hashes from RunSystems(). When state checksums mismatch,
        /// comparing system hashes narrows down which system first diverged.
        /// </summary>
        public uint SystemHash;

        /// <summary>Quick hash before commands are processed (distinguishes command vs system divergence).</summary>
        public uint PreCmdHash;

        /// <summary>Quick hash after commands are processed (distinguishes command vs system divergence).</summary>
        public uint PostCmdHash;

        /// <summary>
        /// Comma-separated hex string of all 9 per-system hashes (e.g. "AABBCCDD,11223344,...").
        /// Allows pinpointing exactly which system first diverged on desync.
        /// </summary>
        public string SystemHashDetail;

        public NoopCommand(int playerId)
        {
            PlayerId = playerId;
            StateChecksum = 0;
            SimTick = 0;
            SystemHash = 0;
            PreCmdHash = 0;
            PostCmdHash = 0;
            SystemHashDetail = null;
        }

        public NoopCommand(int playerId, uint checksum, int simTick, uint systemHash = 0, uint preCmdHash = 0, uint postCmdHash = 0, string systemHashDetail = null)
        {
            PlayerId = playerId;
            StateChecksum = checksum;
            SimTick = simTick;
            SystemHash = systemHash;
            PreCmdHash = preCmdHash;
            PostCmdHash = postCmdHash;
            SystemHashDetail = systemHashDetail;
        }
    }
}
