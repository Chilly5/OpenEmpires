using System;
using System.Collections.Generic;
using UnityEngine;

namespace OpenEmpires
{
    [Flags]
    public enum WallNeighborMask : byte
    {
        None = 0,
        N    = 1 << 0,
        S    = 1 << 1,
        E    = 1 << 2,
        W    = 1 << 3,
        NE   = 1 << 4,
        NW   = 1 << 5,
        SE   = 1 << 6,
        SW   = 1 << 7,

        AnyCardinal = N | S | E | W,
        AnyDiagonal = NE | NW | SE | SW,
        CardinalEW  = E | W,
        CardinalNS  = N | S,
        DiagonalNESW = NE | SW,
        DiagonalNWSE = NW | SE,
    }

    public enum WallSegmentKind
    {
        Isolated,
        CardinalEW,
        CardinalNS,
        DiagonalNESW,
        DiagonalNWSE,
        Junction,
    }

    public static class WallSegmentClassifier
    {
        public static WallSegmentKind Classify(WallNeighborMask m)
        {
            bool anyEW   = (m & WallNeighborMask.CardinalEW)   != 0;
            bool anyNS   = (m & WallNeighborMask.CardinalNS)   != 0;
            bool anyNESW = (m & WallNeighborMask.DiagonalNESW) != 0;
            bool anyNWSE = (m & WallNeighborMask.DiagonalNWSE) != 0;
            bool anyCard = anyEW || anyNS;

            // True junctions: cardinals on both axes (L / T / cross), or both diagonal
            // axes present at once. Cardinal + an incidental diagonal does NOT count —
            // along a straight L turn, the tiles adjacent to the corner naturally have
            // a diagonal neighbor (the wall on the other leg), and forcing them to
            // Junction produces a chain of false towers.
            if (anyEW && anyNS) return WallSegmentKind.Junction;
            if (anyNESW && anyNWSE && !anyCard) return WallSegmentKind.Junction;

            // Cardinals dominate over incidental diagonals.
            if (anyEW) return WallSegmentKind.CardinalEW;
            if (anyNS) return WallSegmentKind.CardinalNS;

            if (anyNESW) return WallSegmentKind.DiagonalNESW;
            if (anyNWSE) return WallSegmentKind.DiagonalNWSE;

            return WallSegmentKind.Isolated;
        }
    }

    public readonly struct WallSpriteSelection
    {
        public readonly string ResourceName;
        public readonly bool FlipX;
        public readonly float RotationDegrees;
        // UV crop — the palisade textures are 3000x3000 with the wall art occupying only
        // ~25% of the canvas in the lower-middle, so we crop to that region to make the
        // visible art larger relative to the quad. UvScale < 1 zooms in; UvOffset shifts
        // the cropped window in texture space.
        public readonly Vector2 UvScale;
        public readonly Vector2 UvOffset;

        public WallSpriteSelection(string resourceName, bool flipX = false, float rotationDegrees = 0f)
            : this(resourceName, flipX, rotationDegrees, new Vector2(1f, 1f), new Vector2(0f, 0f)) { }

        public WallSpriteSelection(string resourceName, bool flipX, float rotationDegrees, Vector2 uvScale, Vector2 uvOffset)
        {
            ResourceName = resourceName;
            FlipX = flipX;
            RotationDegrees = rotationDegrees;
            UvScale = uvScale;
            UvOffset = uvOffset;
        }
    }

    // Maps (BuildingType, WallSegmentKind) -> sprite selection.
    // Extensible for future StoneWall, civ variants, alternate art.
    public static class WallSpriteRegistry
    {
        private static readonly Dictionary<(BuildingType, WallSegmentKind), WallSpriteSelection> Map =
            new Dictionary<(BuildingType, WallSegmentKind), WallSpriteSelection>
            {
                // Gentle UV crop: tiling (0.85, 0.85) zooms ~1.18x (vs 2x earlier),
                // making the visible wall art just slightly wider so adjacent walls have
                // a very small overlap and flow into each other. The wall art lives in
                // the lower portion of the canvas, so offset.y stays at 0 to keep the
                // texture's bottom edge anchored to the quad's bottom.
                { (BuildingType.Wall, WallSegmentKind.CardinalEW),
                    new WallSpriteSelection("Palisadeside90",   false, 0f, new Vector2(0.85f, 0.85f), new Vector2(0.085f, 0f)) },
                { (BuildingType.Wall, WallSegmentKind.CardinalNS),
                    new WallSpriteSelection("Palisadeside90B",  false, 0f, new Vector2(0.85f, 0.85f), new Vector2(0.085f, 0f)) },
                { (BuildingType.Wall, WallSegmentKind.DiagonalNESW),
                    new WallSpriteSelection("PalisadefrontB",   false, 0f, new Vector2(0.85f, 0.85f), new Vector2(0.085f, 0f)) },
                { (BuildingType.Wall, WallSegmentKind.DiagonalNWSE),
                    new WallSpriteSelection("Palisadefront",    false, 0f, new Vector2(0.85f, 0.85f), new Vector2(0.085f, 0f)) },
                { (BuildingType.Wall, WallSegmentKind.Junction),
                    new WallSpriteSelection("PalisadeTower",    false, 0f, new Vector2(0.85f, 0.85f), new Vector2(0.085f, 0f)) },
                { (BuildingType.Wall, WallSegmentKind.Isolated),
                    new WallSpriteSelection("PalisadeTower",    false, 0f, new Vector2(0.85f, 0.85f), new Vector2(0.085f, 0f)) },
            };

        private static readonly Dictionary<string, Texture2D> TextureCache = new Dictionary<string, Texture2D>();

        public static bool TryLookup(BuildingType type, WallSegmentKind kind, out WallSpriteSelection sel)
        {
            return Map.TryGetValue((type, kind), out sel);
        }

        public static Texture2D LoadTexture(string resourceName)
        {
            if (string.IsNullOrEmpty(resourceName)) return null;
            if (TextureCache.TryGetValue(resourceName, out var cached)) return cached;
            var tex = Resources.Load<Texture2D>($"BuildingSprites/{resourceName}");
            TextureCache[resourceName] = tex;
            return tex;
        }
    }
}
