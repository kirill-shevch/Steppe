using System;

namespace Steppe.Ecology
{
    /// <summary>
    /// Integer address in the canonical ecology grid. Coordinates are derived with
    /// floor division so cells remain stable across floating-origin shifts and on
    /// the negative side of the world origin.
    /// </summary>
    public readonly struct EcoCellCoordinate : IEquatable<EcoCellCoordinate>
    {
        public EcoCellCoordinate(long x, long z)
        {
            X = x;
            Z = z;
        }

        public long X { get; }
        public long Z { get; }

        public static EcoCellCoordinate FromWorld(double worldX, double worldZ, double cellSize)
        {
            if (cellSize <= 0.0)
            {
                throw new ArgumentOutOfRangeException(nameof(cellSize));
            }

            return new EcoCellCoordinate(
                (long)Math.Floor(worldX / cellSize),
                (long)Math.Floor(worldZ / cellSize));
        }

        public double CenterX(double cellSize)
        {
            return (X + 0.5) * cellSize;
        }

        public double CenterZ(double cellSize)
        {
            return (Z + 0.5) * cellSize;
        }

        public bool Equals(EcoCellCoordinate other)
        {
            return X == other.X && Z == other.Z;
        }

        public override bool Equals(object obj)
        {
            return obj is EcoCellCoordinate other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var xHash = (int)(X ^ (X >> 32));
                var zHash = (int)(Z ^ (Z >> 32));
                return (xHash * 397) ^ zHash;
            }
        }

        public override string ToString()
        {
            return $"({X}, {Z})";
        }

        public static bool operator ==(EcoCellCoordinate left, EcoCellCoordinate right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(EcoCellCoordinate left, EcoCellCoordinate right)
        {
            return !left.Equals(right);
        }
    }
}
