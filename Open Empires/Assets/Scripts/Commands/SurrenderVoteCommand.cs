namespace OpenEmpires
{
    public struct SurrenderVoteCommand : ICommand
    {
        public CommandType Type => CommandType.Surrender;
        public int PlayerId { get; set; }
        public bool VoteYes;

        public SurrenderVoteCommand(int playerId, bool voteYes)
        {
            PlayerId = playerId;
            VoteYes = voteYes;
        }
    }
}
