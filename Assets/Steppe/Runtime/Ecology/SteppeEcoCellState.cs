using System;

namespace Steppe.Ecology
{
    public readonly struct SteppeEcoExtraction
    {
        public SteppeEcoExtraction(
            double surfaceWater,
            double rootWater,
            double snowWater,
            double biomass,
            double greenFraction)
        {
            SurfaceWater = surfaceWater;
            RootWater = rootWater;
            SnowWater = snowWater;
            Biomass = biomass;
            GreenFraction = greenFraction;
        }

        public double SurfaceWater { get; }
        public double RootWater { get; }
        public double SnowWater { get; }
        public double Biomass { get; }
        public double GreenFraction { get; }
        public double TotalWater => SurfaceWater + RootWater + SnowWater;
    }

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

        public SteppeEcoCellState Extract(
            double requestedSurfaceWater,
            double requestedRootWater,
            double requestedSnowWater,
            double requestedBiomass,
            out SteppeEcoExtraction extraction)
        {
            var surfaceWater = Math.Min(
                SurfaceWater,
                Math.Max(0.0, requestedSurfaceWater));
            var rootWater = Math.Min(
                RootWater,
                Math.Max(0.0, requestedRootWater));
            var snowWater = Math.Min(
                SnowWater,
                Math.Max(0.0, requestedSnowWater));
            var biomass = Math.Min(
                Biomass,
                Math.Max(0.0, requestedBiomass));
            extraction = new SteppeEcoExtraction(
                surfaceWater,
                rootWater,
                snowWater,
                biomass,
                GreenFraction);
            return new SteppeEcoCellState(
                SurfaceWater - surfaceWater,
                RootWater - rootWater,
                Biomass - biomass,
                GreenFraction,
                SurfaceCrust,
                SnowWater - snowWater,
                SnowCompaction,
                FrozenFraction,
                LastSimulationSeconds);
        }
    }
}
