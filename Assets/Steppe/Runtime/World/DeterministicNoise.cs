using System;

namespace Steppe.World
{
    public static class DeterministicNoise
    {
        private const double InverseSqrtTwo = 0.7071067811865475244;

        public static double GradientNoise(double x, double z, int seed)
        {
            var x0 = FastFloor(x);
            var z0 = FastFloor(z);
            var tx = x - x0;
            var tz = z - z0;

            var n00 = Gradient(Hash(x0, z0, seed), tx, tz);
            var n10 = Gradient(Hash(x0 + 1, z0, seed), tx - 1.0, tz);
            var n01 = Gradient(Hash(x0, z0 + 1, seed), tx, tz - 1.0);
            var n11 = Gradient(Hash(x0 + 1, z0 + 1, seed), tx - 1.0, tz - 1.0);

            var u = Fade(tx);
            var v = Fade(tz);
            return Lerp(Lerp(n00, n10, u), Lerp(n01, n11, u), v);
        }

        public static double FractalBrownianMotion(
            double x,
            double z,
            int seed,
            int octaves,
            double lacunarity = 2.0,
            double gain = 0.5)
        {
            var value = 0.0;
            var amplitude = 1.0;
            var frequency = 1.0;
            var normalization = 0.0;

            for (var octave = 0; octave < octaves; octave++)
            {
                value += GradientNoise(x * frequency, z * frequency, seed + octave * 1013) * amplitude;
                normalization += amplitude;
                frequency *= lacunarity;
                amplitude *= gain;
            }

            return normalization > 0.0 ? value / normalization : 0.0;
        }

        public static ulong Hash(long x, long z, int seed)
        {
            unchecked
            {
                var value = (ulong)x;
                value ^= RotateLeft((ulong)z, 32);
                value ^= (ulong)(uint)seed * 0x9E3779B185EBCA87UL;
                return Mix(value);
            }
        }

        private static long FastFloor(double value)
        {
            return (long)Math.Floor(value);
        }

        private static double Fade(double value)
        {
            return value * value * value * (value * (value * 6.0 - 15.0) + 10.0);
        }

        private static double Lerp(double from, double to, double t)
        {
            return from + (to - from) * t;
        }

        private static double Gradient(ulong hash, double x, double z)
        {
            switch (hash & 7UL)
            {
                case 0: return x;
                case 1: return -x;
                case 2: return z;
                case 3: return -z;
                case 4: return (x + z) * InverseSqrtTwo;
                case 5: return (-x + z) * InverseSqrtTwo;
                case 6: return (x - z) * InverseSqrtTwo;
                default: return (-x - z) * InverseSqrtTwo;
            }
        }

        private static ulong Mix(ulong value)
        {
            unchecked
            {
                value += 0x9E3779B97F4A7C15UL;
                value = (value ^ (value >> 30)) * 0xBF58476D1CE4E5B9UL;
                value = (value ^ (value >> 27)) * 0x94D049BB133111EBUL;
                return value ^ (value >> 31);
            }
        }

        private static ulong RotateLeft(ulong value, int count)
        {
            return (value << count) | (value >> (64 - count));
        }
    }
}
