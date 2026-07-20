using System;
using Steppe.Settings;
using Steppe.Surface;

namespace Steppe.Ecology
{
    public readonly struct SteppeDustState
    {
        public SteppeDustState(
            double emission,
            double thresholdWindSpeed,
            double surfaceDryness,
            double looseness,
            double exposedGround)
        {
            Emission = emission;
            ThresholdWindSpeed = thresholdWindSpeed;
            SurfaceDryness = surfaceDryness;
            Looseness = looseness;
            ExposedGround = exposedGround;
        }

        public double Emission { get; }
        public double ThresholdWindSpeed { get; }
        public double SurfaceDryness { get; }
        public double Looseness { get; }
        public double ExposedGround { get; }
    }

    /// <summary>
    /// Pure derivation of airborne dust from immutable soil potential, persistent
    /// ecology state and current weather. Dust is presentation, never stored state.
    /// </summary>
    public static class SteppeDustModel
    {
        public static SteppeDustState Evaluate(
            SteppeWorldSettings settings,
            SurfaceSample surface,
            SteppeEcoCellState ecology,
            double surfaceWindSpeed,
            double rainIntensity)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            var surfaceWater = Clamp01(ecology.SurfaceWater);
            var crust = Clamp01(ecology.SurfaceCrust);
            var snowCover = SmoothStep(0.015, 0.22, ecology.SnowWater);
            var canonicalCapacity = Math.Max(0.001, surface.VegetationPotential);
            var relativeBiomass = Clamp(ecology.Biomass / canonicalCapacity, 0.0, 1.25);
            var plantCover = Clamp01(surface.VegetationPotential * relativeBiomass);
            var exposedGround = Clamp01(1.0 - plantCover);

            // A small amount of surface water binds fine particles very effectively.
            // Root water is deliberately absent: moist roots beneath a dry skin do not
            // prevent wind erosion at the surface.
            var dryness = 1.0 - SmoothStep(0.025, 0.26, surfaceWater);
            var sandiness = 1.0 - Clamp01(surface.ClayContent);
            var crustRelease = 1.0 - crust * 0.88;
            var looseness = Clamp01(
                dryness
                * (0.38 + sandiness * 0.62)
                * crustRelease);

            var threshold = settings.DustBaseWindThreshold
                            + surfaceWater * 9.0
                            + crust * 6.0
                            + plantCover * 3.5
                            + snowCover * 20.0;
            var fullResponseSpeed = Math.Max(
                threshold + 0.5,
                settings.DustFullEmissionWindSpeed);
            var windResponse = SmoothStep(
                threshold,
                fullResponseSpeed,
                Math.Max(0.0, surfaceWindSpeed));
            var rainSuppression = Math.Pow(1.0 - Clamp01(rainIntensity), 3.0);
            var sourcePotential = Clamp01(surface.DustPotential)
                                  * (0.30 + exposedGround * 0.70);
            var emission = Clamp01(
                sourcePotential
                * looseness
                * windResponse
                * rainSuppression
                * Math.Pow(1.0 - snowCover, 4.0));

            return new SteppeDustState(
                emission,
                threshold,
                dryness,
                looseness,
                exposedGround);
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
