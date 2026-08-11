namespace Steppe.Simulation;

internal static class DeterministicNoise
{
    public static float Fractal(float x, float y, int seed, int octaves = 5)
    {
        var sum = 0f;
        var amplitude = 0.5f;
        var frequency = 1f;
        var normalization = 0f;

        for (var octave = 0; octave < octaves; octave++)
        {
            sum += Value(x * frequency, y * frequency, seed + octave * 1013) * amplitude;
            normalization += amplitude;
            amplitude *= 0.5f;
            frequency *= 2.03f;
        }

        return sum / normalization;
    }

    public static float Value(float x, float y, int seed)
    {
        var x0 = (int)MathF.Floor(x);
        var y0 = (int)MathF.Floor(y);
        var tx = Smooth(x - x0);
        var ty = Smooth(y - y0);

        var a = Hash01(x0, y0, seed);
        var b = Hash01(x0 + 1, y0, seed);
        var c = Hash01(x0, y0 + 1, seed);
        var d = Hash01(x0 + 1, y0 + 1, seed);
        return Lerp(Lerp(a, b, tx), Lerp(c, d, tx), ty) * 2f - 1f;
    }

    public static float Hash01(int x, int y, int seed)
    {
        unchecked
        {
            uint h = (uint)seed;
            h ^= (uint)x * 0x9E3779B9u;
            h = (h << 13) | (h >> 19);
            h ^= (uint)y * 0x85EBCA6Bu;
            h ^= h >> 16;
            h *= 0x7FEB352Du;
            h ^= h >> 15;
            h *= 0x846CA68Bu;
            h ^= h >> 16;
            return (h & 0x00FFFFFFu) / 16777215f;
        }
    }

    private static float Smooth(float value) => value * value * (3f - 2f * value);
    private static float Lerp(float a, float b, float t) => a + (b - a) * t;
}
