using System;
using UnityEngine;

namespace Steppe.World
{
    public readonly struct WorldPosition : IEquatable<WorldPosition>
    {
        public WorldPosition(double x, double y, double z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public double X { get; }
        public double Y { get; }
        public double Z { get; }

        public bool Equals(WorldPosition other)
        {
            return X.Equals(other.X) && Y.Equals(other.Y) && Z.Equals(other.Z);
        }

        public override bool Equals(object obj)
        {
            return obj is WorldPosition other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(X, Y, Z);
        }

        public override string ToString()
        {
            return $"({X:F1}, {Y:F1}, {Z:F1})";
        }
    }

    public readonly struct ChunkCoordinate : IEquatable<ChunkCoordinate>
    {
        public ChunkCoordinate(long x, long z)
        {
            X = x;
            Z = z;
        }

        public long X { get; }
        public long Z { get; }

        public static ChunkCoordinate FromWorld(double worldX, double worldZ, double chunkSize)
        {
            if (chunkSize <= 0.0)
            {
                throw new ArgumentOutOfRangeException(nameof(chunkSize));
            }

            return new ChunkCoordinate(
                (long)Math.Floor(worldX / chunkSize),
                (long)Math.Floor(worldZ / chunkSize));
        }

        public ChunkCoordinate Offset(long x, long z)
        {
            return new ChunkCoordinate(X + x, Z + z);
        }

        public bool Equals(ChunkCoordinate other)
        {
            return X == other.X && Z == other.Z;
        }

        public override bool Equals(object obj)
        {
            return obj is ChunkCoordinate other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (X.GetHashCode() * 397) ^ Z.GetHashCode();
            }
        }

        public override string ToString()
        {
            return $"({X}, {Z})";
        }

        public static bool operator ==(ChunkCoordinate left, ChunkCoordinate right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(ChunkCoordinate left, ChunkCoordinate right)
        {
            return !left.Equals(right);
        }
    }
}
