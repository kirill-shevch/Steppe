using System;
using Steppe.Settings;
using Steppe.World;
using UnityEngine;

namespace Steppe.Surface
{
    public readonly struct SurfaceSample
    {
        public SurfaceSample(
            double meanAnnualPrecipitationMm,
            double meanAnnualTemperatureC,
            BiomeWeights biomes,
            double clayContent,
            double fertility,
            double waterRetention,
            double moisturePotential,
            double vegetationPotential,
            double exposedGround,
            double nominalVegetationHeight,
            double windCoherence,
            double motionFrequency,
            double dustPotential,
            Color32 groundColor,
            Color32 vegetationColor)
        {
            MeanAnnualPrecipitationMm = meanAnnualPrecipitationMm;
            MeanAnnualTemperatureC = meanAnnualTemperatureC;
            Biomes = biomes;
            ClayContent = clayContent;
            Fertility = fertility;
            WaterRetention = waterRetention;
            MoisturePotential = moisturePotential;
            VegetationPotential = vegetationPotential;
            ExposedGround = exposedGround;
            NominalVegetationHeight = nominalVegetationHeight;
            WindCoherence = windCoherence;
            MotionFrequency = motionFrequency;
            DustPotential = dustPotential;
            GroundColor = groundColor;
            VegetationColor = vegetationColor;
        }

        public double MeanAnnualPrecipitationMm { get; }
        public double MeanAnnualTemperatureC { get; }
        public BiomeWeights Biomes { get; }
        public SteppeBiome DominantBiome => Biomes.Dominant;
        public double ClayContent { get; }
        public double Fertility { get; }
        public double WaterRetention { get; }
        public double MoisturePotential { get; }
        public double VegetationPotential { get; }
        public double ExposedGround { get; }
        public double NominalVegetationHeight { get; }
        public double WindCoherence { get; }
        public double MotionFrequency { get; }
        public double DustPotential { get; }
        public Color32 GroundColor { get; }
        public Color32 VegetationColor { get; }
    }

    public sealed class SteppeSurfaceGenerator
    {
        private static readonly Color DryLoam = new Color(0.43f, 0.36f, 0.20f);
        private static readonly Color ClaySoil = new Color(0.47f, 0.29f, 0.15f);
        private static readonly Color DarkSoil = new Color(0.25f, 0.25f, 0.13f);

        private static readonly Color MeadowSurface = new Color(0.22f, 0.34f, 0.12f);
        private static readonly Color FeatherGrassSurface = new Color(0.42f, 0.43f, 0.27f);
        private static readonly Color DrySurface = new Color(0.45f, 0.38f, 0.24f);
        private static readonly Color DesertSurface = new Color(0.52f, 0.43f, 0.28f);

        private static readonly Color MeadowVegetation = new Color(0.29f, 0.48f, 0.13f);
        private static readonly Color FeatherGrassVegetation = new Color(0.57f, 0.58f, 0.38f);
        private static readonly Color DryVegetation = new Color(0.48f, 0.43f, 0.25f);
        private static readonly Color DesertVegetation = new Color(0.39f, 0.37f, 0.23f);

        private readonly SteppeWorldSettings settings;
        private readonly int surfaceSeed;

        public SteppeSurfaceGenerator(SteppeWorldSettings settings)
        {
            this.settings = settings != null ? settings : throw new ArgumentNullException(nameof(settings));
            surfaceSeed = unchecked(settings.WorldSeed + settings.SurfaceVersion * 104729);
        }

        public SurfaceSample Sample(double worldX, double worldZ, double height, double normalY)
        {
            var climateWetness = DeterministicNoise.FractalBrownianMotion(
                worldX / settings.ClimateScale,
                worldZ / settings.ClimateScale,
                surfaceSeed + 101,
                4,
                2.0,
                0.48);
            var climateTemperature = DeterministicNoise.FractalBrownianMotion(
                worldX / (settings.ClimateScale * 1.25),
                worldZ / (settings.ClimateScale * 1.25),
                surfaceSeed + 307,
                3,
                2.0,
                0.46);
            var climateDetail = DeterministicNoise.FractalBrownianMotion(
                worldX / settings.ClimateDetailScale,
                worldZ / settings.ClimateDetailScale,
                surfaceSeed + 503,
                2,
                2.0,
                0.42);
            var geology = DeterministicNoise.FractalBrownianMotion(
                worldX / settings.GeologyScale,
                worldZ / settings.GeologyScale,
                surfaceSeed + 1103,
                3,
                2.0,
                0.46);
            var soilDetail = DeterministicNoise.FractalBrownianMotion(
                worldX / settings.SoilDetailScale,
                worldZ / settings.SoilDetailScale,
                surfaceSeed + 1301,
                3,
                2.05,
                0.43);
            var vegetationPatch = DeterministicNoise.FractalBrownianMotion(
                worldX / settings.VegetationScale,
                worldZ / settings.VegetationScale,
                surfaceSeed + 1601,
                3,
                2.0,
                0.44);

            var slope = Clamp01((1.0 - Clamp01(normalY)) * 5.0);
            var elevationRange = Math.Max(1.0, settings.MacroAmplitude * 2.0);
            var relativeElevation = Clamp01(0.5 + (height - settings.BaseHeight) / elevationRange);
            var lowland = 1.0 - SmoothStep(0.28, 0.72, relativeElevation);

            // These are climate normals, not the current weather. A rainy week may change
            // the state of a biome later, but cannot move these boundaries.
            var precipitation = Clamp(
                380.0 + climateWetness * 520.0 + climateDetail * 90.0 + lowland * 35.0,
                90.0,
                720.0);
            var temperature = Clamp(
                9.0 + climateTemperature * 15.0 - (height - settings.BaseHeight) * 0.0065,
                -4.0,
                23.0);
            var effectivePrecipitation = precipitation - Math.Max(0.0, temperature - 8.0) * 13.0;
            var biomes = CalculateBiomeWeights(effectivePrecipitation);

            var clay = Clamp01(0.5 + geology * 0.82 + soilDetail * 0.24);
            var fertility = Clamp01(0.42 + soilDetail * 0.48 + lowland * 0.25 - slope * 0.28);
            var retention = Clamp01(0.18 + clay * 0.64 + lowland * 0.18 - slope * 0.42);

            var biomeMoisture = biomes.Blend(0.86, 0.61, 0.34, 0.13);
            var moisture = Clamp01(biomeMoisture * 0.7 + retention * 0.22 + lowland * 0.08);
            var baseCover = biomes.Blend(0.96, 0.76, 0.43, 0.12);
            var vegetation = Clamp01(
                baseCover + fertility * 0.12 + vegetationPatch * 0.16 - slope * 0.42 - 0.06);
            var exposedGround = Clamp01(1.0 - vegetation);
            var vegetationHeight = biomes.Blend(1.45, 0.92, 0.42, 0.18)
                                   * (0.78 + fertility * 0.22);
            var windCoherence = biomes.Blend(0.88, 1.0, 0.24, 0.07);
            var motionFrequency = biomes.Blend(0.26, 0.42, 0.92, 0.18);
            var dustPotential = Clamp01(
                biomes.Blend(0.01, 0.09, 0.67, 0.94) * (0.45 + exposedGround * 0.75));

            var soilColor = Color.Lerp(DryLoam, ClaySoil, (float)clay);
            soilColor = Color.Lerp(soilColor, DarkSoil, (float)(moisture * 0.38));
            var biomeSurfaceColor = BlendColor(
                biomes,
                MeadowSurface,
                FeatherGrassSurface,
                DrySurface,
                DesertSurface);
            var groundColor = Color.Lerp(soilColor, biomeSurfaceColor, (float)(vegetation * 0.9));
            var vegetationColor = BlendColor(
                biomes,
                MeadowVegetation,
                FeatherGrassVegetation,
                DryVegetation,
                DesertVegetation);

            return new SurfaceSample(
                precipitation,
                temperature,
                biomes,
                clay,
                fertility,
                retention,
                moisture,
                vegetation,
                exposedGround,
                vegetationHeight,
                windCoherence,
                motionFrequency,
                dustPotential,
                (Color32)groundColor,
                (Color32)vegetationColor);
        }

        private static BiomeWeights CalculateBiomeWeights(double effectivePrecipitation)
        {
            // Wide overlapping supports prevent categorical stripes. Their centers encode
            // the four canonical climate regimes while preserving ecotones between them.
            var meadow = TriangularWeight(effectivePrecipitation, 570.0, 205.0);
            var featherGrass = TriangularWeight(effectivePrecipitation, 380.0, 175.0);
            var dry = TriangularWeight(effectivePrecipitation, 265.0, 145.0);
            var desert = TriangularWeight(effectivePrecipitation, 145.0, 170.0);
            return new BiomeWeights(meadow, featherGrass, dry, desert);
        }

        private static double TriangularWeight(double value, double center, double halfWidth)
        {
            return Clamp01(1.0 - Math.Abs(value - center) / halfWidth);
        }

        private static Color BlendColor(
            BiomeWeights weights,
            Color meadow,
            Color featherGrass,
            Color dry,
            Color desert)
        {
            return meadow * (float)weights.Meadow
                   + featherGrass * (float)weights.FeatherGrass
                   + dry * (float)weights.Dry
                   + desert * (float)weights.Desert;
        }

        private static double Clamp(double value, double minimum, double maximum)
        {
            return Math.Max(minimum, Math.Min(maximum, value));
        }

        private static double Clamp01(double value)
        {
            return Clamp(value, 0.0, 1.0);
        }

        private static double SmoothStep(double edge0, double edge1, double value)
        {
            var normalized = Clamp01((value - edge0) / (edge1 - edge0));
            return normalized * normalized * (3.0 - 2.0 * normalized);
        }
    }
}
