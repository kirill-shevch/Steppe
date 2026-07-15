using System;
using Steppe.Settings;
using Steppe.World;

namespace Steppe.Terrain
{
    public sealed class TerrainHeightGenerator
    {
        private readonly SteppeWorldSettings settings;
        private readonly TerrainOverrideMap overrideMap;

        public TerrainHeightGenerator(SteppeWorldSettings settings, TerrainOverrideMap overrideMap = null)
        {
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            this.overrideMap = overrideMap != null ? overrideMap : settings.TerrainOverrides;
        }

        public double SampleHeight(double worldX, double worldZ)
        {
            var versionedSeed = unchecked(settings.WorldSeed + settings.GeneratorVersion * 7919);

            // The generator deliberately composes landforms instead of merely increasing
            // the amplitude of isotropic noise. A steppe needs broad uplands, long shallow
            // ridges and basins that remain legible from the travelling camera.
            var warpX = DeterministicNoise.GradientNoise(
                worldX / (settings.MacroScale * 1.9),
                worldZ / (settings.MacroScale * 1.9),
                versionedSeed + 17);
            var warpZ = DeterministicNoise.GradientNoise(
                worldX / (settings.MacroScale * 1.9),
                worldZ / (settings.MacroScale * 1.9),
                versionedSeed + 43);

            var warpedX = worldX + warpX * settings.MacroScale * 0.45;
            var warpedZ = worldZ + warpZ * settings.MacroScale * 0.45;

            var macro = DeterministicNoise.FractalBrownianMotion(
                warpedX / settings.MacroScale,
                warpedZ / settings.MacroScale,
                versionedSeed + 101,
                4,
                2.0,
                0.46);

            // A rotated anisotropic field produces long folds rather than a uniform carpet
            // of round procedural hills.
            const double directionX = 0.8191520442889918; // cos(35 degrees)
            const double directionZ = 0.5735764363510460; // sin(35 degrees)
            var alongRidge = warpedX * directionX + warpedZ * directionZ;
            var acrossRidge = -warpedX * directionZ + warpedZ * directionX;
            var ridgeField = DeterministicNoise.FractalBrownianMotion(
                alongRidge / (settings.MacroScale * 1.75),
                acrossRidge / (settings.MacroScale * 0.42),
                versionedSeed + 211,
                3,
                2.0,
                0.43);

            // Tanh softens peaks into large shoulders. The elongated field supplies a
            // recognizable direction to the country while the base field breaks repetition.
            var broadLandform = Math.Tanh((macro + ridgeField * 0.62) * 1.7)
                                * settings.MacroAmplitude;

            // Low parts of the ridge field become broad shallow basins. This is a smooth
            // subtraction, not a cliff or a river trench.
            var basinMask = SmoothStep(0.08, 0.42, -ridgeField);
            var basin = -basinMask * basinMask * settings.MacroAmplitude * 0.32;

            var mesoDamping = 0.62 + 0.38 * (1.0 - Math.Min(1.0, Math.Abs(macro)));
            var meso = DeterministicNoise.FractalBrownianMotion(
                warpedX / settings.MesoScale,
                warpedZ / settings.MesoScale,
                versionedSeed + 307,
                4,
                2.05,
                0.44) * settings.MesoAmplitude * mesoDamping;
            var micro = DeterministicNoise.FractalBrownianMotion(
                worldX / settings.MicroScale,
                worldZ / settings.MicroScale,
                versionedSeed + 601,
                2,
                2.0,
                0.42) * settings.MicroAmplitude;

            var height = settings.BaseHeight + broadLandform + basin + meso + micro;
            return overrideMap != null ? overrideMap.Apply(worldX, worldZ, height) : height;
        }

        private static double SmoothStep(double edge0, double edge1, double value)
        {
            var normalized = Math.Max(0.0, Math.Min(1.0, (value - edge0) / (edge1 - edge0)));
            return normalized * normalized * (3.0 - 2.0 * normalized);
        }

        public UnityEngine.Vector3 SampleNormal(double worldX, double worldZ, double sampleDistance)
        {
            var distance = Math.Max(0.1, sampleDistance);
            var left = SampleHeight(worldX - distance, worldZ);
            var right = SampleHeight(worldX + distance, worldZ);
            var back = SampleHeight(worldX, worldZ - distance);
            var forward = SampleHeight(worldX, worldZ + distance);

            return new UnityEngine.Vector3(
                (float)(left - right),
                (float)(distance * 2.0),
                (float)(back - forward)).normalized;
        }
    }
}
