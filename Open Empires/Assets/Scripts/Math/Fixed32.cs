using System;

namespace OpenEmpires
{
    /// <summary>
    /// Q16.16 fixed-point number backed by a 32-bit signed integer.
    /// Scale factor = 65536. Range: -32768 to +32767.99998.
    /// </summary>
    public struct Fixed32 : IEquatable<Fixed32>, IComparable<Fixed32>
    {
        public const int FractionalBits = 16;
        public const int Scale = 1 << FractionalBits; // 65536

        public int Raw;

        public Fixed32(int raw)
        {
            Raw = raw;
        }

        // --- Static constants ---
        public static readonly Fixed32 Zero = new Fixed32(0);
        public static readonly Fixed32 One = new Fixed32(Scale);
        public static readonly Fixed32 Half = new Fixed32(Scale >> 1);
        public static readonly Fixed32 Epsilon = new Fixed32(1); // ~0.000015

        // --- Constructors ---
        public static Fixed32 FromInt(int value) => new Fixed32(value << FractionalBits);
        public static Fixed32 FromFloat(float value) => new Fixed32((int)(value * Scale));
        public float ToFloat() => (float)Raw / Scale;

        // --- Arithmetic operators ---
        public static Fixed32 operator +(Fixed32 a, Fixed32 b) => new Fixed32(a.Raw + b.Raw);
        public static Fixed32 operator -(Fixed32 a, Fixed32 b) => new Fixed32(a.Raw - b.Raw);
        public static Fixed32 operator -(Fixed32 a) => new Fixed32(-a.Raw);

        public static Fixed32 operator *(Fixed32 a, Fixed32 b)
        {
            return new Fixed32((int)(((long)a.Raw * b.Raw) >> FractionalBits));
        }

        public static Fixed32 operator /(Fixed32 a, Fixed32 b)
        {
            return new Fixed32((int)(((long)a.Raw << FractionalBits) / b.Raw));
        }

        // --- Comparison operators ---
        public static bool operator ==(Fixed32 a, Fixed32 b) => a.Raw == b.Raw;
        public static bool operator !=(Fixed32 a, Fixed32 b) => a.Raw != b.Raw;
        public static bool operator <(Fixed32 a, Fixed32 b) => a.Raw < b.Raw;
        public static bool operator >(Fixed32 a, Fixed32 b) => a.Raw > b.Raw;
        public static bool operator <=(Fixed32 a, Fixed32 b) => a.Raw <= b.Raw;
        public static bool operator >=(Fixed32 a, Fixed32 b) => a.Raw >= b.Raw;

        // --- Math helpers ---
        public static Fixed32 Abs(Fixed32 a) => new Fixed32(a.Raw >= 0 ? a.Raw : -a.Raw);

        public static Fixed32 Min(Fixed32 a, Fixed32 b) => a.Raw <= b.Raw ? a : b;
        public static Fixed32 Max(Fixed32 a, Fixed32 b) => a.Raw >= b.Raw ? a : b;

        /// <summary>
        /// Integer square root via Newton's method on (long)raw &lt;&lt; 16.
        /// Returns the fixed-point square root.
        /// </summary>
        public static Fixed32 Sqrt(Fixed32 a)
        {
            if (a.Raw <= 0) return Zero;

            // We want sqrt(a) in Q16.16.
            // sqrt(a.Raw / 2^16) = sqrt(a.Raw) / 2^8
            // In Q16.16: result.Raw = sqrt(a.Raw * 2^16) = sqrt(a.Raw << 16)
            long val = (long)a.Raw << FractionalBits;

            // Newton's method: x_{n+1} = (x_n + val/x_n) / 2
            // Initial guess via bit-length: sqrt(2^n) = 2^(n/2)
            int bits = 0;
            long tmp = val;
            while (tmp > 0) { bits++; tmp >>= 1; }
            long x = 1L << (bits / 2);

            for (int i = 0; i < 16; i++)
            {
                long next = (x + val / x) >> 1;
                if (next >= x) break; // converged
                x = next;
            }

            return new Fixed32((int)x);
        }

        // --- IEquatable / IComparable ---
        public bool Equals(Fixed32 other) => Raw == other.Raw;
        public int CompareTo(Fixed32 other) => Raw.CompareTo(other.Raw);
        public override bool Equals(object obj) => obj is Fixed32 f && Raw == f.Raw;
        public override int GetHashCode() => Raw;
        public override string ToString() => ToFloat().ToString("F4");
    }
}
