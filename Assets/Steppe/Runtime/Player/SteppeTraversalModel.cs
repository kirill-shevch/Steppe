using System;
using Steppe.Ecology;
using Steppe.Settings;
using Steppe.Surface;
using UnityEngine;

namespace Steppe.Player
{
    public readonly struct SteppeTraversalState
    {
        public SteppeTraversalState(
            double mud,
            double looseGround,
            double snowSink,
            double frozenFirmness,
            double resistance,
            double sinkDepth)
        {
            Mud = mud;
            LooseGround = looseGround;
            SnowSink = snowSink;
            FrozenFirmness = frozenFirmness;
            Resistance = resistance;
            SinkDepth = sinkDepth;
        }

        public double Mud { get; }
        public double LooseGround { get; }
        public double SnowSink { get; }
        public double FrozenFirmness { get; }
        public double Resistance { get; }
        public double SinkDepth { get; }
    }

    /// <summary>
    /// Converts the same authoritative soil state used by rendering into tactile
    /// traversal. It deliberately has no physics dependency so it can be tested.
    /// </summary>
    public static class SteppeTraversalModel
    {
        public static SteppeTraversalState Evaluate(
            SteppeWorldSettings settings,
            SurfaceSample surface,
            SteppeEcoCellState ecology)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            var liquidSurface = Clamp01(ecology.SurfaceWater * 1.35 + ecology.RootWater * 0.24);
            var thawed = 1.0 - Clamp01(ecology.FrozenFraction);
            var clay = Clamp01(surface.ClayContent);
            var sandiness = 1.0 - clay;
            var snowCoverage = SmoothStep(0.015, 0.32, ecology.SnowWater);
            var mud = Clamp01(liquidSurface * clay * thawed * (1.0 - snowCoverage * 0.75));

            var dryness = 1.0 - Clamp01(ecology.SurfaceWater * 3.0);
            var looseGround = Clamp01(
                dryness
                * sandiness
                * (1.0 - Clamp01(ecology.SurfaceCrust))
                * (0.35 + Clamp01(surface.ExposedGround) * 0.65));
            var snowSink = Clamp01(
                snowCoverage * (1.0 - Clamp01(ecology.SnowCompaction) * 0.78));
            var frozenFirmness = Clamp01(ecology.FrozenFraction)
                                 * Clamp01(1.0 - snowSink * 0.65);

            var resistance = Clamp01(
                mud * 0.72
                + looseGround * 0.34
                + snowSink * 0.82
                - frozenFirmness * 0.22);
            var sinkDepth = mud * settings.MaximumMudSinkDepth
                            + snowSink * settings.MaximumSnowSinkDepth
                            + looseGround * 0.05;
            return new SteppeTraversalState(
                mud,
                looseGround,
                snowSink,
                frozenFirmness,
                resistance,
                Math.Max(0.0, sinkDepth));
        }

        private static double Clamp01(double value)
        {
            return Math.Max(0.0, Math.Min(1.0, value));
        }

        private static double SmoothStep(double minimum, double maximum, double value)
        {
            var t = Clamp01((value - minimum) / Math.Max(0.0001, maximum - minimum));
            return t * t * (3.0 - 2.0 * t);
        }
    }
}
