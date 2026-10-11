using System;

namespace SoulBind.Core
{
    /// <summary>
    /// A hex tile position in axial coordinates (Q, R). The implied third
    /// coordinate is S = -Q - R. Pure data and math: no Unity types.
    /// </summary>
    public readonly struct HexCoord : IEquatable<HexCoord>
    {
        public const int DirectionCount = 6;

        // The six neighbor offsets, in a fixed order (index 0..5).
        // Index d and index (d + 3) % 6 are opposite directions.
        private static readonly HexCoord[] Directions =
        {
            new HexCoord(1, 0), new HexCoord(1, -1), new HexCoord(0, -1),
            new HexCoord(-1, 0), new HexCoord(-1, 1), new HexCoord(0, 1)
        };

        public readonly int Q;
        public readonly int R;

        public HexCoord(int q, int r)
        {
            Q = q;
            R = r;
        }

        public int S => -Q - R;

        private const float Sqrt3 = 1.7320508f;

        /// <summary>
        /// Center of this tile on a flat plane for pointy-top hexes. hexSize is the distance from
        /// a hex's center to a corner. Row 0 is at the top, so a larger R is lower on screen.
        /// </summary>
        public void ToPlane(float hexSize, out float x, out float y)
        {
            x = hexSize * Sqrt3 * (Q + R * 0.5f);
            y = -hexSize * 1.5f * R;
        }

        /// <summary>The tile that contains a point on the plane. The reverse of ToPlane.</summary>
        public static HexCoord FromPlane(float x, float y, float hexSize)
        {
            float fq = (x / Sqrt3 + y / 3f) / hexSize;
            float fr = (-2f * y / 3f) / hexSize;
            float fs = -fq - fr;

            int q = (int)Math.Round(fq);
            int r = (int)Math.Round(fr);
            int s = (int)Math.Round(fs);

            // Rounding each axis separately can break Q + R + S = 0.
            // Fix the axis that was rounded the most.
            float dq = Math.Abs(q - fq);
            float dr = Math.Abs(r - fr);
            float ds = Math.Abs(s - fs);

            if (dq > dr && dq > ds)
                q = -r - s;
            else if (dr > ds)
                r = -q - s;

            return new HexCoord(q, r);
        }

        public HexCoord Neighbor(int direction)
        {
            if (direction < 0 || direction >= DirectionCount)
                throw new ArgumentOutOfRangeException(nameof(direction), "Direction must be 0 to 5.");

            HexCoord d = Directions[direction];
            return new HexCoord(Q + d.Q, R + d.R);
        }

        public HexCoord[] GetNeighbors()
        {
            var result = new HexCoord[DirectionCount];
            for (int i = 0; i < DirectionCount; i++)
                result[i] = Neighbor(i);
            return result;
        }

        public static int Distance(HexCoord a, HexCoord b)
        {
            int dq = a.Q - b.Q;
            int dr = a.R - b.R;
            return (Math.Abs(dq) + Math.Abs(dr) + Math.Abs(dq + dr)) / 2;
        }

        public static bool AreAdjacent(HexCoord a, HexCoord b) => Distance(a, b) == 1;

        public bool Equals(HexCoord other) => Q == other.Q && R == other.R;
        public override bool Equals(object obj) => obj is HexCoord other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(Q, R);
        public static bool operator ==(HexCoord a, HexCoord b) => a.Equals(b);
        public static bool operator !=(HexCoord a, HexCoord b) => !a.Equals(b);
        public override string ToString() => $"({Q}, {R})";
    }
}