namespace OpenEmpires
{
    public static class TeamHelper
    {
        public static bool AreAllies(int[] playerTeamIds, int playerA, int playerB)
        {
            if (playerA == playerB) return true;
            if (playerTeamIds == null) return false;
            if (playerA < 0 || playerA >= playerTeamIds.Length) return false;
            if (playerB < 0 || playerB >= playerTeamIds.Length) return false;
            return playerTeamIds[playerA] == playerTeamIds[playerB];
        }
    }
}
