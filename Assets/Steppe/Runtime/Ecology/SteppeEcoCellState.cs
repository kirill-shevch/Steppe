using System;

namespace Steppe.Ecology
{
    /// <summary>
    /// Dynamic memory of one patch of steppe. Values are normalized to 0..1.
    /// Biomass, snow and freezing are part of the contract now so later stages can
    /// extend the process without replacing the persistent storage format.
    /// </summary>
    public readonly struct SteppeEcoCellState
    {
        public SteppeEcoCellState(
            double surfaceWater,
            double rootWater,
            double biomass,
            double greenFraction,
            double surfaceCrust,
            double snowWater,
            double snowCompaction,
            double frozenFraction,
            double lastSimulationSeconds)
        {
            SurfaceWater = surfaceWater;
            RootWater = rootWater;
            Biomass = biomass;
            GreenFraction = greenFraction;
            SurfaceCrust = surfaceCrust;
            SnowWater = snowWater;
            SnowCompaction = snowCompaction;
            FrozenFraction = frozenFraction;
            LastSimulationSeconds = lastSimulationSeconds;
        }

        public double SurfaceWater { get; }
        public double RootWater { get; }
        public double Biomass { get; }
        public double GreenFraction { get; }
        public double LiveBiomass => Biomass * GreenFraction;
        public double DryBiomass => Math.Max(0.0, Biomass - LiveBiomass);
        public double SurfaceCrust { get; }
        public double SnowWater { get; }
        public double SnowCompaction { get; }
        public double FrozenFraction { get; }
        public double LastSimulationSeconds { get; }
    }
}
