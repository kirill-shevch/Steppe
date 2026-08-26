using System;
using System.Collections.Generic;
using System.Linq;
using Steppe.Simulation;

namespace Steppe.Rendering
{
    [Flags]
    public enum NaturalVisualDistance
    {
        Near = 1,
        Middle = 2,
        Far = 4,
        Horizon = 8,
    }

    /// <summary>
    /// Stable ownership identifiers for natural state presentation. One channel is
    /// owned by exactly one SimulationLayer; a presenter may add LOD implementations
    /// for that channel but another state may never write into it.
    /// </summary>
    public enum NaturalVisualChannel
    {
        TerrainVertexHeight,
        TerrainNormalTilt,
        TerrainNormalAzimuth,
        RockFractureLineDensity,
        RockOutcropAngularCoverage,
        TerrainBasinRimFloorDepth,
        TerrainRillAxisDirection,
        TerrainChannelBranchTopology,
        SoilProfileThickness,
        SoilCoarseGrainPopulation,
        SoilFinePowderNormalAmplitude,
        SoilShrinkCrackTopology,
        SoilPoreCavityDensity,
        SoilInfiltrationFrontSpeed,
        SoilMineralInclusionPopulation,
        SoilReliefCompressionDepth,
        SunDirectIlluminance,
        GroundRefractionAmplitudeHeight,
        SoilPoreVaporVerticalPolarityRate,
        HorizonSpectrumTemperatureOffset,
        CloudDeckBaseAltitude,
        VegetationMeanBendVector,
        AirExtinctionDistance,
        CloudOpticalThickness,
        PrecipitationParticleDensity,
        WaterSurfaceAreaDepth,
        SoilBaseAlbedo,
        GroundwaterSeepPopulation,
        SnowPhysicalThickness,
        SoilIceNeedleCoverage,
        LiveCanopyVolume,
        DryStandingVolume,
        LitterGroundCoverage,
        SeedPopulation,
        HumusClodCoverage,
        MeristemNewShootPopulation,
        LiveLeafChlorophyllSaturation,
        DecomposerFungalThreadPopulation,
        FireFlameHeightLuminance,
        BurnCharredDebrisCoverage,
        SedimentMobileBedRelief,
        SoilRaisedCrustPlateCoverage,
        DustAirborneOpticalDensity,
    }

    public sealed class NaturalStateVisualDescriptor
    {
        public NaturalStateVisualDescriptor(
            SimulationLayer state,
            NaturalVisualChannel channel,
            string carrier,
            string property,
            NaturalVisualDistance distance,
            string playerCue)
        {
            State = state;
            Channel = channel;
            Carrier = carrier;
            Property = property;
            Distance = distance;
            PlayerCue = playerCue;
        }

        public SimulationLayer State { get; }
        public NaturalVisualChannel Channel { get; }
        public string Carrier { get; }
        public string Property { get; }
        public NaturalVisualDistance Distance { get; }
        public string PlayerCue { get; }
        public string Address => $"{Carrier}.{Property}";
    }

    /// <summary>
    /// Machine-readable counterpart of Unity_Steppe_Natural_Visual_Plan.md.
    /// This first increment registers state ownership. Flux and vector ownership are
    /// added to the same catalog as their presenters are implemented.
    /// </summary>
    public static class NaturalVisualCatalog
    {
        private const NaturalVisualDistance NearMiddle =
            NaturalVisualDistance.Near | NaturalVisualDistance.Middle;
        private const NaturalVisualDistance MiddleFar =
            NaturalVisualDistance.Middle | NaturalVisualDistance.Far;
        private const NaturalVisualDistance NearToFar =
            NaturalVisualDistance.Near | NaturalVisualDistance.Middle | NaturalVisualDistance.Far;
        private const NaturalVisualDistance AllDistances =
            NaturalVisualDistance.Near
            | NaturalVisualDistance.Middle
            | NaturalVisualDistance.Far
            | NaturalVisualDistance.Horizon;

        private static readonly NaturalStateVisualDescriptor[] StateDescriptors =
        {
            D(SimulationLayer.Elevation, NaturalVisualChannel.TerrainVertexHeight,
                "terrain.geometry", "vertexY", AllDistances, "Высота поверхности и горизонт."),
            D(SimulationLayer.Slope, NaturalVisualChannel.TerrainNormalTilt,
                "terrain.geometry", "normalTilt", AllDistances, "Фактический наклон поверхности."),
            D(SimulationLayer.Aspect, NaturalVisualChannel.TerrainNormalAzimuth,
                "terrain.geometry", "normalAzimuth", AllDistances, "Азимут плоскости склона."),
            D(SimulationLayer.FaultInfluence, NaturalVisualChannel.RockFractureLineDensity,
                "rock.fracture", "lineDensity", MiddleFar, "Длина и плотность разломных швов."),
            D(SimulationLayer.RockHardness, NaturalVisualChannel.RockOutcropAngularCoverage,
                "rock.outcrop", "angularityCoverage", NearToFar, "Число и угловатость выходов породы."),
            D(SimulationLayer.DepressionStorage, NaturalVisualChannel.TerrainBasinRimFloorDepth,
                "terrain.basin", "rimFloorDepth", MiddleFar, "Глубина чаши до порога перелива."),
            D(SimulationLayer.Drainage, NaturalVisualChannel.TerrainRillAxisDirection,
                "terrain.rill", "axisDirection", NearToFar, "Ось и направление сухого русла."),
            D(SimulationLayer.Catchments, NaturalVisualChannel.TerrainChannelBranchTopology,
                "terrain.channel", "branchTopology",
                NaturalVisualDistance.Middle | NaturalVisualDistance.Far | NaturalVisualDistance.Horizon,
                "Рисунок ветвления и слияния русел."),

            D(SimulationLayer.SoilDepth, NaturalVisualChannel.SoilProfileThickness,
                "soil.cut", "profileThickness", NearMiddle, "Толщина почвы в открытом профиле."),
            D(SimulationLayer.SandFraction, NaturalVisualChannel.SoilCoarseGrainPopulation,
                "soil.grain", "coarsePopulation", NearMiddle, "Количество крупных песчинок."),
            D(SimulationLayer.SiltFraction, NaturalVisualChannel.SoilFinePowderNormalAmplitude,
                "soil.normal", "finePowderAmplitude", NearMiddle, "Мелкая пылеватая микронормаль."),
            D(SimulationLayer.ClayFraction, NaturalVisualChannel.SoilShrinkCrackTopology,
                "soil.crack", "shrinkTopology", NearToFar, "Связность усадочных полигонов."),
            D(SimulationLayer.Porosity, NaturalVisualChannel.SoilPoreCavityDensity,
                "soil.pore", "cavityDensity", NaturalVisualDistance.Near, "Плотность пор и каверн."),
            D(SimulationLayer.Permeability, NaturalVisualChannel.SoilInfiltrationFrontSpeed,
                "soil.infiltration", "frontSpeed", NearMiddle, "Скорость фронта впитывания."),
            D(SimulationLayer.MineralContent, NaturalVisualChannel.SoilMineralInclusionPopulation,
                "soil.inclusion", "specularPopulation", NearMiddle, "Число минеральных включений."),
            D(SimulationLayer.SoilCompaction, NaturalVisualChannel.SoilReliefCompressionDepth,
                "soil.relief", "compressionDepth", NearToFar, "Сжатие мезорельефа без перекраски."),

            D(SimulationLayer.SolarRadiation, NaturalVisualChannel.SunDirectIlluminance,
                "lighting.sun", "directIlluminance", AllDistances, "Мощность прямого света."),
            D(SimulationLayer.SurfaceTemperature, NaturalVisualChannel.GroundRefractionAmplitudeHeight,
                "air.groundRefraction", "amplitudeHeight", NearToFar, "Марево у поверхности."),
            D(SimulationLayer.SoilTemperature, NaturalVisualChannel.SoilPoreVaporVerticalPolarityRate,
                "soil.poreVapor", "verticalPolarityRate", NearMiddle, "Направление порового теплового дыхания."),
            D(SimulationLayer.AirTemperature, NaturalVisualChannel.HorizonSpectrumTemperatureOffset,
                "horizon.spectrum", "temperatureOffset",
                NaturalVisualDistance.Far | NaturalVisualDistance.Horizon, "Спектральный сдвиг воздушной перспективы."),
            D(SimulationLayer.Pressure, NaturalVisualChannel.CloudDeckBaseAltitude,
                "cloud.deck", "baseAltitude",
                NaturalVisualDistance.Far | NaturalVisualDistance.Horizon, "Высота нижней границы погоды."),
            D(SimulationLayer.Wind, NaturalVisualChannel.VegetationMeanBendVector,
                "vegetation.stem", "meanBendVector", NearToFar, "Средний наклон стеблей."),
            D(SimulationLayer.Humidity, NaturalVisualChannel.AirExtinctionDistance,
                "air.aerosol", "extinctionDistance", AllDistances, "Дальность видимости во влажной дымке."),
            D(SimulationLayer.CloudWater, NaturalVisualChannel.CloudOpticalThickness,
                "cloud.volume", "opticalThickness",
                NaturalVisualDistance.Far | NaturalVisualDistance.Horizon, "Толщина и непрозрачность облака."),
            D(SimulationLayer.Precipitation, NaturalVisualChannel.PrecipitationParticleDensity,
                "precipitation.volume", "particleDensity", NearMiddle, "Плотность капель или снежинок."),

            D(SimulationLayer.SurfaceWater, NaturalVisualChannel.WaterSurfaceAreaDepth,
                "water.surface", "areaDepth", NearToFar, "Площадь и глубина отдельной поверхности воды."),
            D(SimulationLayer.RootWater, NaturalVisualChannel.SoilBaseAlbedo,
                "soil.material", "baseAlbedo", NearToFar, "Единственный сухой/влажный цвет открытой почвы."),
            D(SimulationLayer.Groundwater, NaturalVisualChannel.GroundwaterSeepPopulation,
                "groundwater.seep", "sourcePopulation", NearToFar, "Число родников и сочений."),
            D(SimulationLayer.Snow, NaturalVisualChannel.SnowPhysicalThickness,
                "snow.layer", "physicalThickness", AllDistances, "Толщина отдельного снежного слоя."),
            D(SimulationLayer.FrozenSoil, NaturalVisualChannel.SoilIceNeedleCoverage,
                "soil.iceNeedle", "coverage", NearMiddle, "Покрытие иглами инея и ледяными линзами."),

            D(SimulationLayer.LiveBiomass, NaturalVisualChannel.LiveCanopyVolume,
                "vegetation.live", "canopyVolume", NearToFar, "Число и объём живых стеблей."),
            D(SimulationLayer.DryBiomass, NaturalVisualChannel.DryStandingVolume,
                "vegetation.dry", "standingVolume", NearToFar, "Число и объём сухостоя."),
            D(SimulationLayer.LitterBiomass, NaturalVisualChannel.LitterGroundCoverage,
                "vegetation.litter", "groundCoverage", NearMiddle, "Покрытие полёгшими фрагментами."),
            D(SimulationLayer.SeedBank, NaturalVisualChannel.SeedPopulation,
                "vegetation.seed", "population", NearMiddle, "Семенные головки и свободные семена."),
            D(SimulationLayer.SoilOrganicMatter, NaturalVisualChannel.HumusClodCoverage,
                "soil.humus", "clodCoverage", NearMiddle, "Гумусовые агрегаты без перекраски почвы."),
            D(SimulationLayer.AvailableNitrogen, NaturalVisualChannel.MeristemNewShootPopulation,
                "vegetation.meristem", "newShootPopulation", NearMiddle, "Количество молодых побегов."),
            D(SimulationLayer.PlantNitrogen, NaturalVisualChannel.LiveLeafChlorophyllSaturation,
                "vegetation.liveLeaf", "chlorophyllSaturation", NearToFar, "Насыщенность зелени живых листьев."),
            D(SimulationLayer.OrganicNitrogen, NaturalVisualChannel.DecomposerFungalThreadPopulation,
                "decomposer.fungalThread", "population", NaturalVisualDistance.Near, "Грибница в подстилке и гумусе."),
            D(SimulationLayer.FireIntensity, NaturalVisualChannel.FireFlameHeightLuminance,
                "fire.flame", "heightLuminance", AllDistances, "Высота и светимость активного пламени."),
            D(SimulationLayer.BurnScar, NaturalVisualChannel.BurnCharredDebrisCoverage,
                "burn.charredDebris", "coverage", NearToFar, "Обугленные стебли и угольные фрагменты."),

            D(SimulationLayer.LooseSediment, NaturalVisualChannel.SedimentMobileBedRelief,
                "sediment.mobileBed", "rippleRelief", NearToFar, "Рельеф рыхлой ряби и наносных вееров."),
            D(SimulationLayer.SurfaceCrust, NaturalVisualChannel.SoilRaisedCrustPlateCoverage,
                "soil.crust", "raisedPlateCoverage", NearToFar, "Площадь приподнятых корковых пластин."),
            D(SimulationLayer.Dust, NaturalVisualChannel.DustAirborneOpticalDensity,
                "dust.airborne", "opticalDensity", AllDistances, "Плотность приземного пылевого объёма."),
        };

        private static readonly IReadOnlyDictionary<SimulationLayer, NaturalStateVisualDescriptor>
            StateById = StateDescriptors.ToDictionary(descriptor => descriptor.State);

        public static IReadOnlyList<NaturalStateVisualDescriptor> States { get; } =
            Array.AsReadOnly(StateDescriptors);

        public static NaturalStateVisualDescriptor GetState(SimulationLayer state) =>
            StateById.TryGetValue(state, out var descriptor)
                ? descriptor
                : throw new ArgumentOutOfRangeException(nameof(state), state,
                    "The simulation state has no natural visual owner.");

        private static NaturalStateVisualDescriptor D(
            SimulationLayer state,
            NaturalVisualChannel channel,
            string carrier,
            string property,
            NaturalVisualDistance distance,
            string playerCue) =>
            new NaturalStateVisualDescriptor(
                state,
                channel,
                carrier,
                property,
                distance,
                playerCue);
    }
}
