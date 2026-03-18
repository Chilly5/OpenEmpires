using System.Collections.Generic;
using UnityEngine;

namespace OpenEmpires
{
    public static class WallLineHelper
    {
        /// <summary>
        /// Computes a list of tile positions along a line from (x0,z0) to (x1,z1)
        /// using Bresenham's line algorithm. Integer-only, deterministic for multiplayer.
        /// </summary>
        public static List<Vector2Int> ComputeWallLine(int x0, int z0, int x1, int z1)
        {
            var tiles = new List<Vector2Int>();

            int dx = x1 - x0;
            int dz = z1 - z0;
            int sx = dx >= 0 ? 1 : -1;
            int sz = dz >= 0 ? 1 : -1;
            dx = dx < 0 ? -dx : dx;
            dz = dz < 0 ? -dz : dz;

            int x = x0;
            int z = z0;

            if (dx >= dz)
            {
                int err = dx / 2;
                for (int i = 0; i <= dx; i++)
                {
                    tiles.Add(new Vector2Int(x, z));
                    err -= dz;
                    if (err < 0)
                    {
                        z += sz;
                        err += dx;
                    }
                    x += sx;
                }
            }
            else
            {
                int err = dz / 2;
                for (int i = 0; i <= dz; i++)
                {
                    tiles.Add(new Vector2Int(x, z));
                    err -= dx;
                    if (err < 0)
                    {
                        x += sx;
                        err += dz;
                    }
                    z += sz;
                }
            }

            return tiles;
        }
    }
}
