namespace OpenEmpires
{
    public enum TileVisibility : byte
    {
        Unexplored = 0,
        Explored = 1,
        Visible = 2
    }

    public class FogOfWarData
    {
        public int Width { get; private set; }
        public int Height { get; private set; }
        public int PlayerCount { get; private set; }

        private readonly byte[] visibility;
        private readonly System.Collections.Generic.HashSet<int> visionCheatPlayers = new System.Collections.Generic.HashSet<int>();
        private MapData mapData;

        public FogOfWarData(int width, int height, int playerCount)
        {
            Width = width;
            Height = height;
            PlayerCount = playerCount;
            visibility = new byte[playerCount * width * height];
        }

        public void SetMapData(MapData map)
        {
            mapData = map;
        }

        public void SetVisionCheat(int playerId, bool enabled)
        {
            if (enabled)
                visionCheatPlayers.Add(playerId);
            else
                visionCheatPlayers.Remove(playerId);
        }

        public bool HasVisionCheat(int playerId) => visionCheatPlayers.Contains(playerId);

        public TileVisibility GetVisibility(int playerId, int x, int z)
        {
            if (x < 0 || x >= Width || z < 0 || z >= Height) return TileVisibility.Unexplored;
            if (mapData != null && mapData.IsOutsideCircle(x, z)) return TileVisibility.Unexplored;
            if (visionCheatPlayers.Contains(playerId)) return TileVisibility.Visible;
            return (TileVisibility)visibility[playerId * Width * Height + z * Width + x];
        }

        public void SetVisible(int playerId, int x, int z)
        {
            if (playerId < 0 || playerId >= PlayerCount) return;
            if (x < 0 || x >= Width || z < 0 || z >= Height) return;
            if (mapData != null && mapData.IsOutsideCircle(x, z)) return;
            visibility[playerId * Width * Height + z * Width + x] = (byte)TileVisibility.Visible;
        }

        public void DemoteAllVisible(int playerId)
        {
            if (playerId < 0 || playerId >= PlayerCount) return;
            int start = playerId * Width * Height;
            int end = start + Width * Height;
            for (int i = start; i < end; i++)
            {
                if (visibility[i] == (byte)TileVisibility.Visible)
                    visibility[i] = (byte)TileVisibility.Explored;
            }
        }
    }
}
