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
    }
}
