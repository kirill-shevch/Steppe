using System;
using System.Collections.Generic;
using Steppe.Simulation;
using UnityEngine;

namespace Steppe.Rendering
{
    public enum NaturalGroundDetailKind
    {
        IceNeedles,
        FallenLitter,
        Seeds,
        HumusClods,
        NewShoots,
        FungalThreads,
        FaultFractures,
        RockOutcrops,
        GroundwaterSeeps,
        CharredDebris,
        SoilProfileCuts,
    }

    public sealed class NaturalGroundDetailDescriptor
    {
        public NaturalGroundDetailDescriptor(
            NaturalGroundDetailKind kind,
            SimulationLayer state,
            NaturalVisualChannel channel,
            string geometryLanguage,
            Color fixedMaterialColor,
            float scale)
        {
            Kind = kind;
            State = state;
            Channel = channel;
            GeometryLanguage = geometryLanguage;
            FixedMaterialColor = fixedMaterialColor;
            Scale = scale;
        }

        public NaturalGroundDetailKind Kind { get; }
        public SimulationLayer State { get; }
        public NaturalVisualChannel Channel { get; }
        public string GeometryLanguage { get; }
        public Color FixedMaterialColor { get; }
        public float Scale { get; }
    }

    /// <summary>
    /// The semantic near-field carriers are geometry languages, not a diagnostic
    /// colour overlay. Their simulation values own population density only; fixed
    /// material colours merely make the physical objects readable under natural light.
    /// </summary>
    public static class NaturalGroundDetailCatalog
    {
        private static readonly NaturalGroundDetailDescriptor[] DescriptorArray =
        {
            D(NaturalGroundDetailKind.IceNeedles, SimulationLayer.FrozenSoil,
                NaturalVisualChannel.SoilIceNeedleCoverage, "upright crystalline needles",
                new Color(0.70f, 0.84f, 0.90f), 1.15f),
            D(NaturalGroundDetailKind.FallenLitter, SimulationLayer.LitterBiomass,
                NaturalVisualChannel.LitterGroundCoverage, "flat broken straw ribbons",
                new Color(0.43f, 0.29f, 0.12f), 1.25f),
            D(NaturalGroundDetailKind.Seeds, SimulationLayer.SeedBank,
                NaturalVisualChannel.SeedPopulation, "seed heads and loose grains",
                new Color(0.58f, 0.42f, 0.17f), 1.12f),
            D(NaturalGroundDetailKind.HumusClods, SimulationLayer.SoilOrganicMatter,
                NaturalVisualChannel.HumusClodCoverage, "raised irregular humus clods",
                new Color(0.28f, 0.16f, 0.065f), 0.82f),
            D(NaturalGroundDetailKind.NewShoots, SimulationLayer.AvailableNitrogen,
                NaturalVisualChannel.MeristemNewShootPopulation, "short radial fresh shoots",
                new Color(0.25f, 0.48f, 0.10f), 1.08f),
            D(NaturalGroundDetailKind.FungalThreads, SimulationLayer.OrganicNitrogen,
                NaturalVisualChannel.DecomposerFungalThreadPopulation, "ground-hugging branched mycelium",
                new Color(0.66f, 0.61f, 0.45f), 1.10f),
            D(NaturalGroundDetailKind.FaultFractures, SimulationLayer.FaultInfluence,
                NaturalVisualChannel.RockFractureLineDensity, "long branching fracture seams",
                new Color(0.24f, 0.21f, 0.18f), 4.60f),
            D(NaturalGroundDetailKind.RockOutcrops, SimulationLayer.RockHardness,
                NaturalVisualChannel.RockOutcropAngularCoverage, "faceted resistant rock outcrops",
                new Color(0.50f, 0.47f, 0.42f), 1.90f),
            D(NaturalGroundDetailKind.GroundwaterSeeps, SimulationLayer.Groundwater,
                NaturalVisualChannel.GroundwaterSeepPopulation, "small concentric seep surfaces",
                new Color(0.17f, 0.34f, 0.36f), 1.75f),
            D(NaturalGroundDetailKind.CharredDebris, SimulationLayer.BurnScar,
                NaturalVisualChannel.BurnCharredDebrisCoverage, "forked charred stems and coal fragments",
                new Color(0.075f, 0.061f, 0.047f), 1.55f),
            D(NaturalGroundDetailKind.SoilProfileCuts, SimulationLayer.SoilDepth,
                NaturalVisualChannel.SoilProfileThickness, "layered vertical rill-bank profiles",
                new Color(0.52f, 0.34f, 0.17f), 0.92f),
        };

        public static IReadOnlyList<NaturalGroundDetailDescriptor> Descriptors { get; } =
            Array.AsReadOnly(DescriptorArray);

        private static NaturalGroundDetailDescriptor D(
            NaturalGroundDetailKind kind,
            SimulationLayer state,
            NaturalVisualChannel channel,
            string geometryLanguage,
            Color fixedMaterialColor,
            float scale) =>
            new NaturalGroundDetailDescriptor(
                kind,
                state,
                channel,
                geometryLanguage,
                fixedMaterialColor,
                scale);
    }
}
