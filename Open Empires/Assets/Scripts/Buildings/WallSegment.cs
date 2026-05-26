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
            bool hasN = (m & WallNeighborMask.N) != 0;
            bool hasS = (m & WallNeighborMask.S) != 0;
            bool hasE = (m & WallNeighborMask.E) != 0;
            bool hasW = (m & WallNeighborMask.W) != 0;
            bool hasNE = (m & WallNeighborMask.NE) != 0;
            bool hasNW = (m & WallNeighborMask.NW) != 0;
            bool hasSE = (m & WallNeighborMask.SE) != 0;
            bool hasSW = (m & WallNeighborMask.SW) != 0;

            bool anyEW = hasE || hasW;
            bool anyNS = hasN || hasS;
            bool anyCard = anyEW || anyNS;
            bool anyDiag = hasNE || hasNW || hasSE || hasSW;
            bool anyNESW = hasNE || hasSW;
            bool anyNWSE = hasNW || hasSE;

            // Junction: cardinal on both axes, or cardinal mixed with any diagonal,
            // or both diagonal axes present at once.
            if (anyEW && anyNS) return WallSegmentKind.Junction;
            if (anyCard && anyDiag) return WallSegmentKind.Junction;
            if (anyNESW && anyNWSE) return WallSegmentKind.Junction;

            if (anyEW)   return WallSegmentKind.CardinalEW;
            if (anyNS)   return WallSegmentKind.CardinalNS;
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

        public WallSpriteSelection(string resourceName, bool flipX = false, float rotationDegrees = 0f)
        {
            ResourceName = resourceName;
            FlipX = flipX;
            RotationDegrees = rotationDegrees;
        }
    }

    // Maps (BuildingType, WallSegmentKind) -> sprite selection.
    // Extensible for future StoneWall, civ variants, alternate art.
    public static class WallSpriteRegistry
    {
        private static readonly Dictionary<(BuildingType, WallSegmentKind), WallSpriteSelection> Map =
            new Dictionary<(BuildingType, WallSegmentKind), WallSpriteSelection>
            {
                { (BuildingType.Wall, WallSegmentKind.CardinalEW),   new WallSpriteSelection("Palisadeside90") },
                { (BuildingType.Wall, WallSegmentKind.CardinalNS),   new WallSpriteSelection("Palisadeside90B") },
                { (BuildingType.Wall, WallSegmentKind.DiagonalNESW), new WallSpriteSelection("PalisadefrontB") },
                { (BuildingType.Wall, WallSegmentKind.DiagonalNWSE), new WallSpriteSelection("Palisadefront") },
                { (BuildingType.Wall, WallSegmentKind.Junction),     new WallSpriteSelection("PalisadeTower") },
                { (BuildingType.Wall, WallSegmentKind.Isolated),     new WallSpriteSelection("PalisadeTower") },
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
