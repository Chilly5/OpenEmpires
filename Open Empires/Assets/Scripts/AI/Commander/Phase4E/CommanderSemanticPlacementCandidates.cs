using System;
using System.Collections.Generic;
using UnityEngine;

namespace OpenEmpires
{
    // Geometry only: callers validate terrain, occupancy, visibility, builder paths and rules.
    // No simulation state, concrete provider ID or command authority enters this class.
    public static class CommanderSemanticPlacementCandidates
    {
        private struct RankedCandidate
        {
            public Vector2Int Origin;
            public int GapError;
            public int Displacement;
        }

        public static IReadOnlyList<Vector2Int> Generate(int anchorX, int anchorZ,
            int anchorWidth, int anchorHeight, int newWidth, int newHeight,
            int mapWidth, int mapHeight, CommanderSemanticPlacementRelation relation,
            int clearGapTiles)
        {
            var result = new List<Vector2Int>();
            if (anchorWidth <= 0 || anchorHeight <= 0 || newWidth <= 0 || newHeight <= 0
                || mapWidth <= 0 || mapHeight <= 0 || anchorX < 0 || anchorZ < 0
                || (long)anchorX + anchorWidth > mapWidth
                || (long)anchorZ + anchorHeight > mapHeight
                || newWidth > mapWidth || newHeight > mapHeight
                || clearGapTiles < 1 || clearGapTiles > 20
                || (relation == CommanderSemanticPlacementRelation.Near && clearGapTiles != 1))
                return result;

            var ranked = new List<RankedCandidate>();
            int centeredX = anchorX + FloorHalf(anchorWidth - newWidth);
            int centeredZ = anchorZ + FloorHalf(anchorHeight - newHeight);

            switch (relation)
            {
                case CommanderSemanticPlacementRelation.MapWest:
                case CommanderSemanticPlacementRelation.MapEast:
                    int exactX = relation == CommanderSemanticPlacementRelation.MapWest
                        ? anchorX - clearGapTiles - newWidth
                        : anchorX + anchorWidth + clearGapTiles;
                    for (int dx = -2; dx <= 2; dx++)
                    {
                        int x = exactX + dx;
                        int gap = relation == CommanderSemanticPlacementRelation.MapWest
                            ? anchorX - (x + newWidth)
                            : x - (anchorX + anchorWidth);
                        int gapError = Math.Abs(gap - clearGapTiles);
                        if (gap < 0 || gapError > 1) continue;
                        for (int dz = -2; dz <= 2; dz++)
                        {
                            int z = centeredZ + dz;
                            if (!Overlaps(z, newHeight, anchorZ, anchorHeight)) continue;
                            AddIfInBounds(ranked, x, z, newWidth, newHeight, mapWidth,
                                mapHeight, gapError, Math.Abs(dx) + Math.Abs(dz));
                        }
                    }
                    break;

                case CommanderSemanticPlacementRelation.Near:
                    for (int gap = 0; gap <= 3; gap++)
                    {
                        int gapError = Math.Abs(gap - 1);
                        for (int offset = -2; offset <= 2; offset++)
                        {
                            int displacement = gapError + Math.Abs(offset);
                            int z = centeredZ + offset;
                            if (Overlaps(z, newHeight, anchorZ, anchorHeight))
                            {
                                AddIfInBounds(ranked, anchorX - gap - newWidth, z,
                                    newWidth, newHeight, mapWidth, mapHeight,
                                    gapError, displacement);
                                AddIfInBounds(ranked, anchorX + anchorWidth + gap, z,
                                    newWidth, newHeight, mapWidth, mapHeight,
                                    gapError, displacement);
                            }

                            int x = centeredX + offset;
                            if (!Overlaps(x, newWidth, anchorX, anchorWidth)) continue;
                            AddIfInBounds(ranked, x, anchorZ - gap - newHeight,
                                newWidth, newHeight, mapWidth, mapHeight,
                                gapError, displacement);
                            AddIfInBounds(ranked, x, anchorZ + anchorHeight + gap,
                                newWidth, newHeight, mapWidth, mapHeight,
                                gapError, displacement);
                        }
                    }
                    break;

                default:
                    return result;
            }

            // Exact first; ties are stable integer coordinates, independent of map/registry order.
            ranked.Sort((left, right) =>
            {
                int comparison = left.GapError.CompareTo(right.GapError);
                if (comparison != 0) return comparison;
                comparison = left.Displacement.CompareTo(right.Displacement);
                if (comparison != 0) return comparison;
                comparison = left.Origin.x.CompareTo(right.Origin.x);
                return comparison != 0 ? comparison : left.Origin.y.CompareTo(right.Origin.y);
            });
            for (int i = 0; i < ranked.Count; i++) result.Add(ranked[i].Origin);
            return result;
        }

        private static int FloorHalf(int value)
        {
            int quotient = value / 2;
            return value < 0 && value % 2 != 0 ? quotient - 1 : quotient;
        }

        private static bool Overlaps(int firstStart, int firstSize, int secondStart, int secondSize)
        {
            return (long)firstStart < (long)secondStart + secondSize
                && (long)firstStart + firstSize > secondStart;
        }

        private static void AddIfInBounds(List<RankedCandidate> candidates, int x, int z,
            int width, int height, int mapWidth, int mapHeight, int gapError,
            int displacement)
        {
            if (x < 0 || z < 0 || (long)x + width > mapWidth
                || (long)z + height > mapHeight) return;
            candidates.Add(new RankedCandidate
            {
                Origin = new Vector2Int(x, z),
                GapError = gapError,
                Displacement = displacement
            });
        }
    }
}
