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
            bool hasN  = (m & WallNeighborMask.N)  != 0;
            bool hasS  = (m & WallNeighborMask.S)  != 0;
            bool hasE  = (m & WallNeighborMask.E)  != 0;
            bool hasW  = (m & WallNeighborMask.W)  != 0;
            bool hasNE = (m & WallNeighborMask.NE) != 0;
            bool hasNW = (m & WallNeighborMask.NW) != 0;
            bool hasSE = (m & WallNeighborMask.SE) != 0;
            bool hasSW = (m & WallNeighborMask.SW) != 0;

            int cardCount = (hasN ? 1 : 0) + (hasS ? 1 : 0) + (hasE ? 1 : 0) + (hasW ? 1 : 0);
            int diagCount = (hasNE ? 1 : 0) + (hasNW ? 1 : 0) + (hasSE ? 1 : 0) + (hasSW ? 1 : 0);

            bool anyEW   = hasE || hasW;
            bool anyNS   = hasN || hasS;
            bool anyNESW = hasNE || hasSW;
            bool anyNWSE = hasNW || hasSE;
            bool anyCard = anyEW || anyNS;

            // True junctions:
            //   1) cardinals on both axes  (L / T / cross)
            //   2) both diagonal axes present with no cardinal at all (diagonal X-cross)
            //   3) cardinal-to-diagonal BEND: exactly one cardinal + any diagonal.
            //      The cardinal-having tile becomes the post; the adjacent diagonal-only
            //      tile stays as a clean diagonal-run sprite. Restricting to cardCount==1
            //      avoids re-introducing the L-corner false-tower chain (those incidental
            //      tiles have 2 cardinals on the same axis).
            if (anyEW && anyNS) return WallSegmentKind.Junction;
            if (anyNESW && anyNWSE && !anyCard) return WallSegmentKind.Junction;
            if (cardCount == 1 && diagCount >= 1) return WallSegmentKind.Junction;

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
        // Optional per-sprite vertical nudge in world units, added on top of the shared
        // PalisadeSpriteYOffsetRatio. Positive raises the sprite, negative lowers it.
        // Use this to compensate for sprites whose art sits at a different canvas v than
        // the others (e.g. diagonal "front" walls drawn lower in their canvas).
        public readonly float WorldYOffset;
        // Optional per-sprite scale multiplier applied to the quad's localScale.
        // 1.0 = same size as PalisadeSpriteScale, 0.9 = 10% smaller, etc.
        public readonly float ScaleMultiplier;

        public WallSpriteSelection(string resourceName, bool flipX = false, float rotationDegrees = 0f)
            : this(resourceName, flipX, rotationDegrees, new Vector2(1f, 1f), new Vector2(0f, 0f), 0f, 1f) { }

        public WallSpriteSelection(string resourceName, bool flipX, float rotationDegrees, Vector2 uvScale, Vector2 uvOffset)
            : this(resourceName, flipX, rotationDegrees, uvScale, uvOffset, 0f, 1f) { }

        public WallSpriteSelection(string resourceName, bool flipX, float rotationDegrees, Vector2 uvScale, Vector2 uvOffset, float worldYOffset)
            : this(resourceName, flipX, rotationDegrees, uvScale, uvOffset, worldYOffset, 1f) { }

        public WallSpriteSelection(string resourceName, bool flipX, float rotationDegrees, Vector2 uvScale, Vector2 uvOffset, float worldYOffset, float scaleMultiplier)
        {
            ResourceName = resourceName;
            FlipX = flipX;
            RotationDegrees = rotationDegrees;
            UvScale = uvScale;
            UvOffset = uvOffset;
            WorldYOffset = worldYOffset;
            ScaleMultiplier = scaleMultiplier;
        }
    }

    // Maps (BuildingType, WallSegmentKind, isOpen) -> gate sprite selection.
    // Parallel to WallSpriteRegistry but for the open/closed gate states.
    public static class WallGateSpriteRegistry
    {
        // Same UV crop as the palisade walls so gate art lines up with its tile.
        private static readonly Vector2 PalisadeUvScale  = new Vector2(0.85f, 0.85f);
        private static readonly Vector2 PalisadeUvOffset = new Vector2(0.085f, 0f);

        private static readonly Dictionary<(BuildingType, WallSegmentKind, bool), WallSpriteSelection> Map =
            new Dictionary<(BuildingType, WallSegmentKind, bool), WallSpriteSelection>
            {
                // Closed
                { (BuildingType.Wall, WallSegmentKind.CardinalEW,    false),
                    new WallSpriteSelection("Palisadegate90",       false, 0f, PalisadeUvScale, PalisadeUvOffset) },
                { (BuildingType.Wall, WallSegmentKind.CardinalNS,    false),
                    new WallSpriteSelection("Palisadegate90B",      false, 0f, PalisadeUvScale, PalisadeUvOffset) },
                { (BuildingType.Wall, WallSegmentKind.DiagonalNESW,  false),
                    new WallSpriteSelection("PalisadegateFrontB",   false, 0f, PalisadeUvScale, PalisadeUvOffset) },
                { (BuildingType.Wall, WallSegmentKind.DiagonalNWSE,  false),
                    new WallSpriteSelection("PalisadegateFront",    false, 0f, PalisadeUvScale, PalisadeUvOffset) },
                // Open
                { (BuildingType.Wall, WallSegmentKind.CardinalEW,    true),
                    new WallSpriteSelection("Palisadegate90-open",       false, 0f, PalisadeUvScale, PalisadeUvOffset) },
                { (BuildingType.Wall, WallSegmentKind.CardinalNS,    true),
                    new WallSpriteSelection("Palisadegate90B-open",      false, 0f, PalisadeUvScale, PalisadeUvOffset) },
                { (BuildingType.Wall, WallSegmentKind.DiagonalNESW,  true),
                    new WallSpriteSelection("PalisadegateFrontB-open",   false, 0f, PalisadeUvScale, PalisadeUvOffset) },
                { (BuildingType.Wall, WallSegmentKind.DiagonalNWSE,  true),
                    new WallSpriteSelection("PalisadegateFront-open",    false, 0f, PalisadeUvScale, PalisadeUvOffset) },
            };

        public static bool TryLookup(BuildingType type, WallSegmentKind kind, bool isOpen, out WallSpriteSelection sel)
        {
            if (Map.TryGetValue((type, kind, isOpen), out sel)) return true;
            // Junction/Isolated/etc. — fall back to CardinalEW so a gate at a weird
            // neighbor position still renders a sensible orientation instead of nothing.
            if (Map.TryGetValue((type, WallSegmentKind.CardinalEW, isOpen), out sel)) return true;
            sel = default;
            return false;
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
                // Front-facing diagonal sprites are drawn slightly lower in their canvas
                // than the cardinal "side90" sprites, so we nudge them up a touch so the
                // wall foot lines up with the cardinal walls.
                { (BuildingType.Wall, WallSegmentKind.DiagonalNESW),
                    new WallSpriteSelection("PalisadefrontB",   false, 0f, new Vector2(0.85f, 0.85f), new Vector2(0.085f, 0f), 0.17f) },
                { (BuildingType.Wall, WallSegmentKind.DiagonalNWSE),
                    new WallSpriteSelection("Palisadefront",    false, 0f, new Vector2(0.85f, 0.85f), new Vector2(0.085f, 0f), 0.17f) },
                // Post/tower sprites scaled to 0.9 so they read as slightly smaller than
                // a run wall — keeps the corner cap from dominating the wall it terminates.
                { (BuildingType.Wall, WallSegmentKind.Junction),
                    new WallSpriteSelection("PalisadeTower",    false, 0f, new Vector2(0.85f, 0.85f), new Vector2(0.085f, 0f), 0f, 0.9f) },
                { (BuildingType.Wall, WallSegmentKind.Isolated),
                    new WallSpriteSelection("PalisadeTower",    false, 0f, new Vector2(0.85f, 0.85f), new Vector2(0.085f, 0f), 0f, 0.9f) },

                // Stone walls — same UV crop / Y offset as palisade, mapping mirrors
                // palisade convention: "90" sprites are cardinal axis, "45" are diagonal.
                // Tower scaled to 0.9 like the palisade tower for the same reason.
                { (BuildingType.StoneWall, WallSegmentKind.CardinalEW),
                    new WallSpriteSelection("Stonewall90",     false, 0f, new Vector2(0.85f, 0.85f), new Vector2(0.085f, 0f), -0.2f) },
                { (BuildingType.StoneWall, WallSegmentKind.CardinalNS),
                    new WallSpriteSelection("Stonewall90B",    false, 0f, new Vector2(0.85f, 0.85f), new Vector2(0.085f, 0f), -0.2f) },
                // Stonewall45B uvOffset.x set to 0.077 to shift the rendered art ~0.06 world
                // units right in screen space (from the default 0.085 baseline).
                { (BuildingType.StoneWall, WallSegmentKind.DiagonalNESW),
                    new WallSpriteSelection("Stonewall45B",    false, 0f, new Vector2(0.85f, 0.85f), new Vector2(0.077f, 0f)) },
                { (BuildingType.StoneWall, WallSegmentKind.DiagonalNWSE),
                    new WallSpriteSelection("Stonewall45",     false, 0f, new Vector2(0.85f, 0.85f), new Vector2(0.085f, 0f), -0.07f) },
                { (BuildingType.StoneWall, WallSegmentKind.Junction),
                    new WallSpriteSelection("StonewallTower",  false, 0f, new Vector2(0.85f, 0.85f), new Vector2(0.085f, 0f), -0.3f, 0.9f) },
                { (BuildingType.StoneWall, WallSegmentKind.Isolated),
                    new WallSpriteSelection("StonewallTower",  false, 0f, new Vector2(0.85f, 0.85f), new Vector2(0.085f, 0f), -0.3f, 0.9f) },
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
