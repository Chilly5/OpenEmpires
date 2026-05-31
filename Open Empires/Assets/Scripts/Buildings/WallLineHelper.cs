using System.Collections.Generic;
using UnityEngine;

namespace OpenEmpires
{
    public static class WallLineHelper
    {
        /// <summary>
        /// Computes a list of tile positions along a line from (x0,z0) to (x1,z1),
        /// snapping the drag to one of 8 directions (N, NE, E, SE, S, SW, W, NW).
        /// Drags within ~22.5° of an axis become pure cardinal lines; everything
        /// else becomes a perfect 45° diagonal. Integer-only, deterministic for
        /// multiplayer.
        /// </summary>
        public static List<Vector2Int> ComputeWallLine(int x0, int z0, int x1, int z1)
        {
            var tiles = new List<Vector2Int>();

            int dx = x1 - x0;
            int dz = z1 - z0;
            int absDx = dx < 0 ? -dx : dx;
            int absDz = dz < 0 ? -dz : dz;

            if (absDx == 0 && absDz == 0)
            {
                tiles.Add(new Vector2Int(x0, z0));
                return tiles;
            }

            int sx = dx > 0 ? 1 : (dx < 0 ? -1 : 0);
            int sz = dz > 0 ? 1 : (dz < 0 ? -1 : 0);

            int min = absDx < absDz ? absDx : absDz;
            int max = absDx > absDz ? absDx : absDz;

            // Diagonal vs cardinal boundary at 22.5° from the nearer axis:
            // tan(22.5°) ≈ 0.4142, so diagonal iff min/max ≥ 0.4142.
            // 12/5 = 2.4 is a close integer approximation of cot(22.5°).
            bool diagonal = 5 * max <= 12 * min;

            int stepX, stepZ;
            if (diagonal)
            {
                stepX = sx;
                stepZ = sz;
            }
            else if (absDx >= absDz)
            {
                stepX = sx;
                stepZ = 0;
            }
            else
            {
                stepX = 0;
                stepZ = sz;
            }

            int x = x0;
            int z = z0;
            for (int i = 0; i <= max; i++)
            {
                tiles.Add(new Vector2Int(x, z));
                x += stepX;
                z += stepZ;
            }

            return tiles;
        }

        /// <summary>
        /// Returns a corner-to-corner path through the interior of the box bounded by
        /// (x0,z0) and (x1,z1). The path takes diagonal steps until the shorter axis
        /// is exhausted, then continues cardinally along the longer axis to reach the
        /// opposite corner. Always starts at (x0,z0) and ends at (x1,z1) so the
        /// preview matches the user's drag direction.
        /// </summary>
        public static List<Vector2Int> ComputeWallBoxCenterPath(int x0, int z0, int x1, int z1)
        {
            var tiles = new List<Vector2Int>();

            int dx = x1 - x0;
            int dz = z1 - z0;
            int absDx = dx < 0 ? -dx : dx;
            int absDz = dz < 0 ? -dz : dz;

            int sx = dx > 0 ? 1 : (dx < 0 ? -1 : 0);
            int sz = dz > 0 ? 1 : (dz < 0 ? -1 : 0);

            int diagSteps = absDx < absDz ? absDx : absDz;
            int cardSteps = (absDx > absDz ? absDx : absDz) - diagSteps;

            int x = x0;
            int z = z0;
            tiles.Add(new Vector2Int(x, z));

            for (int i = 0; i < diagSteps; i++)
            {
                x += sx;
                z += sz;
                tiles.Add(new Vector2Int(x, z));
            }

            int stepX = absDx >= absDz ? sx : 0;
            int stepZ = absDz > absDx ? sz : 0;
            for (int i = 0; i < cardSteps; i++)
            {
                x += stepX;
                z += stepZ;
                tiles.Add(new Vector2Int(x, z));
            }

            return tiles;
        }
    }
}
