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

        // ----- Junction hubs (crossings AND touch-junctions) ---------------------------------
        // Where straight wall runs of two or more orientations MEET at a tile — whether they pass
        // straight through (an X) or one merely touches the side of another (a T / Y / branch) —
        // that tile becomes a single post (hub) and the tiles hugging it render as their straight
        // run instead of each growing a post. ClassifyAt() applies this; Classify() is the
        // fallback for ordinary shapes.

        public static WallNeighborMask SampleNeighbors(MapData map, BuildingRegistry reg, int tx, int tz)
        {
            WallNeighborMask m = WallNeighborMask.None;
            if (map.IsWallTile(tx,     tz + 1, reg)) m |= WallNeighborMask.N;
            if (map.IsWallTile(tx,     tz - 1, reg)) m |= WallNeighborMask.S;
            if (map.IsWallTile(tx + 1, tz,     reg)) m |= WallNeighborMask.E;
            if (map.IsWallTile(tx - 1, tz,     reg)) m |= WallNeighborMask.W;
            if (map.IsWallTile(tx + 1, tz + 1, reg)) m |= WallNeighborMask.NE;
            if (map.IsWallTile(tx - 1, tz + 1, reg)) m |= WallNeighborMask.NW;
            if (map.IsWallTile(tx + 1, tz - 1, reg)) m |= WallNeighborMask.SE;
            if (map.IsWallTile(tx - 1, tz - 1, reg)) m |= WallNeighborMask.SW;
            return m;
        }

        // The run orientation a neighbor offset lies on.
        private static WallSegmentKind AxisOfDir(int dx, int dz)
        {
            if (dx == 0) return WallSegmentKind.CardinalNS;       // (0,±1)
            if (dz == 0) return WallSegmentKind.CardinalEW;       // (±1,0)
            if (dx == dz) return WallSegmentKind.DiagonalNESW;    // (1,1)/(-1,-1)
            return WallSegmentKind.DiagonalNWSE;                  // (1,-1)/(-1,1)
        }

        // Is a straight run of the given orientation connected to tile (cx,cz)? True when the wall
        // passes straight through (opposite neighbors) OR a genuine arm of that orientation leaves
        // the tile — a neighbor whose own run continues one step further out (so it's a real run,
        // not an incidental diagonal touch).
        private static bool RunConnected(MapData map, BuildingRegistry reg, int cx, int cz,
            WallNeighborMask m, WallSegmentKind axis)
        {
            switch (axis)
            {
                case WallSegmentKind.CardinalNS:
                    return ((m & WallNeighborMask.N) != 0 && (m & WallNeighborMask.S) != 0)
                        || ((m & WallNeighborMask.N) != 0 && map.IsWallTile(cx, cz + 2, reg))
                        || ((m & WallNeighborMask.S) != 0 && map.IsWallTile(cx, cz - 2, reg));
                case WallSegmentKind.CardinalEW:
                    return ((m & WallNeighborMask.E) != 0 && (m & WallNeighborMask.W) != 0)
                        || ((m & WallNeighborMask.E) != 0 && map.IsWallTile(cx + 2, cz, reg))
                        || ((m & WallNeighborMask.W) != 0 && map.IsWallTile(cx - 2, cz, reg));
                case WallSegmentKind.DiagonalNESW:
                    return ((m & WallNeighborMask.NE) != 0 && (m & WallNeighborMask.SW) != 0)
                        || ((m & WallNeighborMask.NE) != 0 && map.IsWallTile(cx + 2, cz + 2, reg))
                        || ((m & WallNeighborMask.SW) != 0 && map.IsWallTile(cx - 2, cz - 2, reg));
                case WallSegmentKind.DiagonalNWSE:
                    return ((m & WallNeighborMask.NW) != 0 && (m & WallNeighborMask.SE) != 0)
                        || ((m & WallNeighborMask.NW) != 0 && map.IsWallTile(cx - 2, cz + 2, reg))
                        || ((m & WallNeighborMask.SE) != 0 && map.IsWallTile(cx + 2, cz - 2, reg));
                default: return false;
            }
        }

        // A hub is a tile where straight runs of two or more distinct orientations meet.
        public static bool IsHub(MapData map, BuildingRegistry reg, int cx, int cz)
        {
            var m = SampleNeighbors(map, reg, cx, cz);
            int axes = 0;
            if (RunConnected(map, reg, cx, cz, m, WallSegmentKind.CardinalNS))   axes++;
            if (RunConnected(map, reg, cx, cz, m, WallSegmentKind.CardinalEW))   axes++;
            if (RunConnected(map, reg, cx, cz, m, WallSegmentKind.DiagonalNESW)) axes++;
            if (RunConnected(map, reg, cx, cz, m, WallSegmentKind.DiagonalNWSE)) axes++;
            return axes >= 2;
        }

        private static readonly (int dx, int dz)[] EightDirs =
        {
            (0, 1), (0, -1), (1, 0), (-1, 0), (1, 1), (-1, -1), (-1, 1), (1, -1),
        };

        // Hub-aware classification: a hub collapses to one post; a tile hugging a hub along one of
        // that hub's runs renders as that run; everything else uses the per-tile Classify().
        public static WallSegmentKind ClassifyAt(MapData map, BuildingRegistry reg, int tx, int tz)
        {
            var mask = SampleNeighbors(map, reg, tx, tz);
            if (IsHub(map, reg, tx, tz)) return WallSegmentKind.Junction;

            for (int i = 0; i < EightDirs.Length; i++)
            {
                int dx = EightDirs[i].dx, dz = EightDirs[i].dz;   // offset from this tile to the hub
                int cx = tx + dx, cz = tz + dz;
                if (!map.IsWallTile(cx, cz, reg)) continue;
                if (!IsHub(map, reg, cx, cz)) continue;
                // This tile lies on the run leaving the hub in the (-dx,-dz) direction; render it
                // as that run only if the hub is actually connected along it.
                var axis = AxisOfDir(-dx, -dz);
                var hubMask = SampleNeighbors(map, reg, cx, cz);
                if (RunConnected(map, reg, cx, cz, hubMask, axis))
                    return axis;
            }
            return Classify(mask);
        }

        // Straight-run axes in priority order, each as the two opposite tile offsets:
        // E/W, N/S, NE/SW, NW/SE. Convention matches WallNeighborMask (E=+x, N=+z).
        private static readonly Vector2Int[] PairOffsetsA =
            { new Vector2Int(1, 0), new Vector2Int(0, 1), new Vector2Int(1, 1), new Vector2Int(-1, 1) };
        private static readonly Vector2Int[] PairOffsetsB =
            { new Vector2Int(-1, 0), new Vector2Int(0, -1), new Vector2Int(-1, -1), new Vector2Int(1, -1) };

        // A wall-family tile owned by playerId that is NOT currently a gate — i.e. a plain wall
        // segment a neighboring gate can absorb into its 3-tile span.
        private static bool IsOwnerPlainWall(MapData map, BuildingRegistry reg, int x, int z, int playerId)
        {
            var b = map.GetBuildingAt(x, z, reg);
            return b != null && !b.IsGate && b.PlayerId == playerId
                && (b.Type == BuildingType.Wall || b.Type == BuildingType.StoneWall
                    || b.Type == BuildingType.WoodGate || b.Type == BuildingType.StoneGate);
        }

        // True if (tx,tz) has owner plain-wall neighbors on BOTH ends of one straight axis,
        // outputting that axis's two tile offsets. This is the gate-eligibility test (a wall may
        // become a gate only mid-run) and defines the two tiles a gate absorbs. When several axes
        // qualify (junction/cross), the priority order above picks one.
        public static bool TryGetCollinearWallPair(MapData map, BuildingRegistry reg,
            int tx, int tz, int playerId, out Vector2Int offsetA, out Vector2Int offsetB)
        {
            for (int i = 0; i < PairOffsetsA.Length; i++)
            {
                var a = PairOffsetsA[i];
                var b = PairOffsetsB[i];
                if (IsOwnerPlainWall(map, reg, tx + a.x, tz + a.y, playerId)
                 && IsOwnerPlainWall(map, reg, tx + b.x, tz + b.y, playerId))
                {
                    offsetA = a; offsetB = b;
                    return true;
                }
            }
            offsetA = default; offsetB = default;
            return false;
        }

        // True if the plain-wall tile (x,z) is one of the two collinear neighbors absorbed by an
        // adjacent gate owned by the SAME player (a gate only claims its own walls). Outputs that
        // gate's owner so callers can apply owner-or-ally passability. Pure function of building
        // data, so both the walkability check and the view can call it.
        public static bool TryGetAbsorbingGate(MapData map, BuildingRegistry reg,
            int x, int z, out int gateOwner)
        {
            gateOwner = -1;
            var wall = map.GetBuildingAt(x, z, reg);
            if (wall == null || wall.IsGate) return false;
            if (wall.Type != BuildingType.Wall && wall.Type != BuildingType.StoneWall
                && wall.Type != BuildingType.WoodGate && wall.Type != BuildingType.StoneGate) return false;

            for (int dz = -1; dz <= 1; dz++)
            for (int dx = -1; dx <= 1; dx++)
            {
                if (dx == 0 && dz == 0) continue;
                var g = map.GetBuildingAt(x + dx, z + dz, reg);
                if (g == null || !g.IsGate || g.PlayerId != wall.PlayerId) continue;
                if (TryGetCollinearWallPair(map, reg, g.OriginTileX, g.OriginTileZ, g.PlayerId,
                        out var a, out var b))
                {
                    if ((g.OriginTileX + a.x == x && g.OriginTileZ + a.y == z)
                     || (g.OriginTileX + b.x == x && g.OriginTileZ + b.y == z))
                    {
                        gateOwner = g.PlayerId;
                        return true;
                    }
                }
            }
            return false;
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
        // Optional per-sprite local X/Z position of the gate billboard quad (relative to the gate
        // container). Used by gates only (ApplyGateSprite) to nudge the art so its towers line up
        // over the two absorbed neighbor tiles. Wall bodies ignore these.
        public readonly float LocalPosX;
        public readonly float LocalPosZ;

        public WallSpriteSelection(string resourceName, bool flipX = false, float rotationDegrees = 0f)
            : this(resourceName, flipX, rotationDegrees, new Vector2(1f, 1f), new Vector2(0f, 0f), 0f, 1f, 0f, 0f) { }

        public WallSpriteSelection(string resourceName, bool flipX, float rotationDegrees, Vector2 uvScale, Vector2 uvOffset)
            : this(resourceName, flipX, rotationDegrees, uvScale, uvOffset, 0f, 1f, 0f, 0f) { }

        public WallSpriteSelection(string resourceName, bool flipX, float rotationDegrees, Vector2 uvScale, Vector2 uvOffset, float worldYOffset)
            : this(resourceName, flipX, rotationDegrees, uvScale, uvOffset, worldYOffset, 1f, 0f, 0f) { }

        public WallSpriteSelection(string resourceName, bool flipX, float rotationDegrees, Vector2 uvScale, Vector2 uvOffset, float worldYOffset, float scaleMultiplier)
            : this(resourceName, flipX, rotationDegrees, uvScale, uvOffset, worldYOffset, scaleMultiplier, 0f, 0f) { }

        public WallSpriteSelection(string resourceName, bool flipX, float rotationDegrees, Vector2 uvScale, Vector2 uvOffset, float worldYOffset, float scaleMultiplier, float localPosX, float localPosZ)
        {
            ResourceName = resourceName;
            FlipX = flipX;
            RotationDegrees = rotationDegrees;
            UvScale = uvScale;
            UvOffset = uvOffset;
            WorldYOffset = worldYOffset;
            ScaleMultiplier = scaleMultiplier;
            LocalPosX = localPosX;
            LocalPosZ = localPosZ;
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
                // Per-orientation scale/position hand-tuned in-editor so each sprite's towers
                // sit over the two absorbed neighbor tiles. Args after UV are:
                // worldYOffset, scaleMultiplier (× WoodGateSpriteScale 5.82), localPosX, localPosZ.
                // Open and closed share the same transform per orientation.
                // Closed
                { (BuildingType.Wall, WallSegmentKind.CardinalEW,    false),
                    new WallSpriteSelection("Palisadegate90",       false, 0f, PalisadeUvScale, PalisadeUvOffset, 0f,       0.9469f, 0.02f,  -0.19f) },
                { (BuildingType.Wall, WallSegmentKind.CardinalNS,    false),
                    new WallSpriteSelection("Palisadegate90B",      false, 0f, PalisadeUvScale, PalisadeUvOffset, -0.6214f, 0.8575f, -0.32f, -0.52f) },
                { (BuildingType.Wall, WallSegmentKind.DiagonalNESW,  false),
                    new WallSpriteSelection("PalisadegateFrontB",   false, 0f, PalisadeUvScale, PalisadeUvOffset, 0f,       0.6970f, 0f,     0f) },
                { (BuildingType.Wall, WallSegmentKind.DiagonalNWSE,  false),
                    new WallSpriteSelection("PalisadegateFront",    false, 0f, PalisadeUvScale, PalisadeUvOffset, -0.4514f, 0.7080f, -0.39f, -0.38f) },
                // Open
                { (BuildingType.Wall, WallSegmentKind.CardinalEW,    true),
                    new WallSpriteSelection("Palisadegate90-open",       false, 0f, PalisadeUvScale, PalisadeUvOffset, 0f,       0.9469f, 0.02f,  -0.19f) },
                { (BuildingType.Wall, WallSegmentKind.CardinalNS,    true),
                    new WallSpriteSelection("Palisadegate90B-open",      false, 0f, PalisadeUvScale, PalisadeUvOffset, -0.6214f, 0.8575f, -0.32f, -0.52f) },
                { (BuildingType.Wall, WallSegmentKind.DiagonalNESW,  true),
                    new WallSpriteSelection("PalisadegateFrontB-open",   false, 0f, PalisadeUvScale, PalisadeUvOffset, 0f,       0.6970f, 0f,     0f) },
                { (BuildingType.Wall, WallSegmentKind.DiagonalNWSE,  true),
                    new WallSpriteSelection("PalisadegateFront-open",    false, 0f, PalisadeUvScale, PalisadeUvOffset, -0.4514f, 0.7080f, -0.39f, -0.38f) },

                // Stone (English) gates. Same orientation convention as the palisade gates:
                // grid-cardinal walls read as the isometric "90d" views, grid-diagonal walls
                // read as the flat "front"/"Side" views. Full-art UVs (no crop) for now —
                // the English art has wide transparent margins, so cropping it like the
                // palisade sprites would clip the tower tops. Tune later if overlap is wanted.
                //
                // The two cardinal axes map to the two isometric "90d" views: 90d_A runs along
                // the "/" screen diagonal (= Palisadegate90 / CardinalEW), 90d_B runs along the
                // "\" diagonal (= Palisadegate90B / CardinalNS).
                // Closed
                { (BuildingType.StoneWall, WallSegmentKind.CardinalEW,   false),
                    new WallSpriteSelection("Englishgate_90d_A") },
                { (BuildingType.StoneWall, WallSegmentKind.CardinalNS,   false),
                    new WallSpriteSelection("Englishgate_90d_B") },
                { (BuildingType.StoneWall, WallSegmentKind.DiagonalNWSE, false),
                    new WallSpriteSelection("Englishgate_front") },
                { (BuildingType.StoneWall, WallSegmentKind.DiagonalNESW, false),
                    new WallSpriteSelection("Englishgate_Side") },
                // Open
                { (BuildingType.StoneWall, WallSegmentKind.CardinalEW,   true),
                    new WallSpriteSelection("Englishgate_90d_A_Opened") },
                { (BuildingType.StoneWall, WallSegmentKind.CardinalNS,   true),
                    new WallSpriteSelection("Englishgate_90d_B_Opened") },
                { (BuildingType.StoneWall, WallSegmentKind.DiagonalNWSE, true),
                    new WallSpriteSelection("Englishgate_front_opened") },
                { (BuildingType.StoneWall, WallSegmentKind.DiagonalNESW, true),
                    new WallSpriteSelection("Englishgate_Side_Opened") },
            };

        public static bool TryLookup(BuildingType type, WallSegmentKind kind, bool isOpen, out WallSpriteSelection sel)
        {
            // The standalone WoodGate / StoneGate building types share gate art with their
            // wall counterparts (Wall / StoneWall), so normalize before lookup.
            BuildingType key = type == BuildingType.WoodGate ? BuildingType.Wall
                             : type == BuildingType.StoneGate ? BuildingType.StoneWall
                             : type;
            if (Map.TryGetValue((key, kind, isOpen), out sel)) return true;
            // Junction/Isolated/etc. — fall back to CardinalEW so a gate at a weird
            // neighbor position still renders a sensible orientation instead of nothing.
            if (Map.TryGetValue((key, WallSegmentKind.CardinalEW, isOpen), out sel)) return true;
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
                // Stonewall45B uvOffset.x bumped 0.085 → 0.098 to shift the rendered art
                // ~0.1 world units left in screen space.
                { (BuildingType.StoneWall, WallSegmentKind.DiagonalNESW),
                    new WallSpriteSelection("Stonewall45B",    false, 0f, new Vector2(0.85f, 0.85f), new Vector2(0.098f, 0f)) },
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
