using System;

namespace Steppe.UnitySimulation
{
    /// <summary>
    /// Deterministic, continuous sub-cell variation in absolute world space.
    /// Macro fields provide the mean state; this field prevents a 250 metre cell
    /// from behaving like one perfectly uniform parcel of air.
    /// </summary>
    public static class SteppeLocalField
    {
        public static float SampleSigned(
            double worldX,
            double worldZ,
            double elapsedSeconds,
            int seed,
            double spatialScaleMeters,
            double temporalScaleSeconds)
        {
            if (spatialScaleMeters <= 0d)
            {
                throw new ArgumentOutOfRangeException(nameof(spatialScaleMeters));
            }

            if (temporalScaleSeconds <= 0d)
            {
                throw new ArgumentOutOfRangeException(nameof(temporalScaleSeconds));
            }

            var value = ValueNoise(
                worldX / spatialScaleMeters,
                worldZ / spatialScaleMeters,
                Math.Max(0d, elapsedSeconds) / temporalScaleSeconds,
                seed);
            return (float)(value * 2d - 1d);
        }

        private static double ValueNoise(double x, double y, double z, int seed)
        {
            var x0 = FastFloor(x);
            var y0 = FastFloor(y);
            var z0 = FastFloor(z);
            var tx = Smooth(x - x0);
            var ty = Smooth(y - y0);
            var tz = Smooth(z - z0);

            var lower0 = Lerp(Hash01(x0, y0, z0, seed), Hash01(x0 + 1, y0, z0, seed), tx);
            var lower1 = Lerp(Hash01(x0, y0 + 1, z0, seed), Hash01(x0 + 1, y0 + 1, z0, seed), tx);
            var upper0 = Lerp(Hash01(x0, y0, z0 + 1, seed), Hash01(x0 + 1, y0, z0 + 1, seed), tx);
            var upper1 = Lerp(Hash01(x0, y0 + 1, z0 + 1, seed), Hash01(x0 + 1, y0 + 1, z0 + 1, seed), tx);
            return Lerp(Lerp(lower0, lower1, ty), Lerp(upper0, upper1, ty), tz);
        }

        private static int FastFloor(double value)
        {
            var integer = (int)value;
            return value < integer ? integer - 1 : integer;
        }

        private static double Smooth(double value) =>
            value * value * (3d - 2d * value);

        private static double Lerp(double from, double to, double blend) =>
            from + (to - from) * blend;

        private static double Hash01(int x, int y, int z, int seed)
        {
            unchecked
            {
                var hash = (uint)seed;
                hash ^= (uint)x * 0x9E3779B9u;
                hash = RotateLeft(hash, 13);
                hash ^= (uint)y * 0x85EBCA6Bu;
                hash = RotateLeft(hash, 11);
                hash ^= (uint)z * 0xC2B2AE35u;
                hash ^= hash >> 16;
                hash *= 0x7FEB352Du;
                hash ^= hash >> 15;
                hash *= 0x846CA68Bu;
                hash ^= hash >> 16;
                return (hash & 0x00FFFFFFu) / 16777215d;
            }
        }

        private static uint RotateLeft(uint value, int count) =>
            (value << count) | (value >> (32 - count));
    }
}
