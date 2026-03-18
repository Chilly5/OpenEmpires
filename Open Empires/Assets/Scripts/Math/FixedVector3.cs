using UnityEngine;

namespace OpenEmpires
{
    /// <summary>
    /// Fixed-point 3D vector using Fixed32 components.
    /// Y is typically Zero for 2D ground-plane simulation.
    /// </summary>
    public struct FixedVector3
    {
        public Fixed32 x;
        public Fixed32 y;
        public Fixed32 z;

        public FixedVector3(Fixed32 x, Fixed32 y, Fixed32 z)
        {
            this.x = x;
            this.y = y;
            this.z = z;
        }

        public static readonly FixedVector3 Zero = new FixedVector3(Fixed32.Zero, Fixed32.Zero, Fixed32.Zero);

        // --- Arithmetic operators ---
        public static FixedVector3 operator +(FixedVector3 a, FixedVector3 b)
        {
            return new FixedVector3(a.x + b.x, a.y + b.y, a.z + b.z);
        }

        public static FixedVector3 operator -(FixedVector3 a, FixedVector3 b)
        {
            return new FixedVector3(a.x - b.x, a.y - b.y, a.z - b.z);
        }

        public static FixedVector3 operator -(FixedVector3 a)
        {
            return new FixedVector3(-a.x, -a.y, -a.z);
        }

        public static FixedVector3 operator *(FixedVector3 v, Fixed32 s)
        {
            return new FixedVector3(v.x * s, v.y * s, v.z * s);
        }

        public static FixedVector3 operator *(Fixed32 s, FixedVector3 v)
        {
            return new FixedVector3(v.x * s, v.y * s, v.z * s);
        }

        public static FixedVector3 operator /(FixedVector3 v, Fixed32 s)
        {
            return new FixedVector3(v.x / s, v.y / s, v.z / s);
        }

        // --- Magnitude ---
        public Fixed32 SqrMagnitude()
        {
            return x * x + y * y + z * z;
        }

        public Fixed32 Magnitude()
        {
            // Use long arithmetic to avoid int32 overflow in x*x for large vectors
            long lx = (long)x.Raw;
            long ly = (long)y.Raw;
            long lz = (long)z.Raw;

            // x*x + y*y + z*z in Q16.16, stored as long
            long sqrMag = ((lx * lx) >> Fixed32.FractionalBits)
                       + ((ly * ly) >> Fixed32.FractionalBits)
                       + ((lz * lz) >> Fixed32.FractionalBits);

            if (sqrMag <= 0) return Fixed32.Zero;

            // Newton's method sqrt on the 64-bit squared magnitude
            long val = sqrMag << Fixed32.FractionalBits;
            int bits = 0;
            long tmp = val;
            while (tmp > 0) { bits++; tmp >>= 1; }
            long guess = 1L << (bits / 2);
            for (int i = 0; i < 32; i++)
            {
                long next = (guess + val / guess) >> 1;
                if (next >= guess) break;
                guess = next;
            }
            return new Fixed32((int)guess);
        }

        public FixedVector3 Normalized()
        {
            Fixed32 mag = Magnitude();
            if (mag.Raw == 0) return Zero;
            return this / mag;
        }

        // --- Conversion ---
        public Vector3 ToVector3()
        {
            return new Vector3(x.ToFloat(), y.ToFloat(), z.ToFloat());
        }

        public static FixedVector3 FromVector3(Vector3 v)
        {
            return new FixedVector3(
                Fixed32.FromFloat(v.x),
                Fixed32.FromFloat(v.y),
                Fixed32.FromFloat(v.z));
        }

        // --- 2D helpers (XZ plane) ---
        public static Fixed32 Dot2D(FixedVector3 a, FixedVector3 b)
            => a.x * b.x + a.z * b.z;

        public static Fixed32 Det2D(FixedVector3 a, FixedVector3 b)
            => a.x * b.z - a.z * b.x;

        public Fixed32 SqrMagnitude2D()
            => x * x + z * z;

        public static bool operator ==(FixedVector3 a, FixedVector3 b)
        {
            return a.x == b.x && a.y == b.y && a.z == b.z;
        }

        public static bool operator !=(FixedVector3 a, FixedVector3 b)
        {
            return !(a == b);
        }

        public override bool Equals(object obj) => obj is FixedVector3 v && this == v;
        public override int GetHashCode() => x.GetHashCode() ^ (y.GetHashCode() << 8) ^ (z.GetHashCode() << 16);
        public override string ToString() => $"({x}, {y}, {z})";
    }
}
