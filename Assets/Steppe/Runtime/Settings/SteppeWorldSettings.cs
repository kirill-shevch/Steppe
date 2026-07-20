using Steppe.Terrain;
using UnityEngine;

namespace Steppe.Settings
{
    [CreateAssetMenu(fileName = "SteppeWorldSettings", menuName = "Steppe/World Settings")]
    public sealed class SteppeWorldSettings : ScriptableObject
    {
        [Header("Canonical world")]
        [SerializeField] private int worldSeed = 14072026;
        [SerializeField, Min(1)] private int generatorVersion = 2;
        [SerializeField] private TerrainOverrideMap terrainOverrides;

        [Header("Terrain shape")]
        [Tooltip("Width of the broad uplands and basins. It must remain readable inside the streamed horizon.")]
        [SerializeField, Min(100f)] private float macroScale = 6500f;
        [SerializeField, Min(0f)] private float macroAmplitude = 160f;
        [Tooltip("Width of valleys, shoulders and low ridges encountered during a few minutes of flight.")]
        [SerializeField, Min(100f)] private float mesoScale = 1500f;
        [SerializeField, Min(0f)] private float mesoAmplitude = 42f;
        [Tooltip("Small ground relief. This should never dominate the silhouette of the steppe.")]
        [SerializeField, Min(10f)] private float microScale = 260f;
        [SerializeField, Min(0f)] private float microAmplitude = 5f;
        [SerializeField] private float baseHeight = 40f;

        [Header("Surface ecology")]
        [SerializeField, Min(1)] private int surfaceVersion = 1;
        [Tooltip("Scale of long-lived temperature and precipitation regions.")]
        [SerializeField, Min(5000f)] private float climateScale = 42000f;
        [Tooltip("Scale of secondary variation inside a climate region.")]
        [SerializeField, Min(1000f)] private float climateDetailScale = 11000f;
        [Tooltip("Scale of broad geological and soil regions.")]
        [SerializeField, Min(500f)] private float geologyScale = 9000f;
        [Tooltip("Scale of local soil variation inside geological regions.")]
        [SerializeField, Min(100f)] private float soilDetailScale = 2400f;
        [Tooltip("Scale of broad vegetation patches visible across the landscape.")]
        [SerializeField, Min(100f)] private float vegetationScale = 1400f;

        [Header("Prototype vegetation")]
        [SerializeField, Min(2f)] private float nearGrassSpacing = 12f;
        [SerializeField, Min(4f)] private float middleGrassSpacing = 28f;
        [SerializeField, Min(0.1f)] private float grassTuftHeight = 1.25f;
        [SerializeField, Min(0.05f)] private float grassTuftWidth = 0.7f;

        [Header("P4 grass rendering")]
        [SerializeField, Min(16f)] private float grassCellSize = 64f;
        [Tooltip("Spacing between ecological samples. Each sample renders a three-tuft cluster.")]
        [SerializeField, Min(0.35f)] private float grassCandidateSpacing = 0.7f;
        [SerializeField, Min(16f)] private float grassFullDensityRadius = 145f;
        [SerializeField, Min(32f)] private float grassDrawRadius = 320f;

        [Header("World time and clear-sky climate")]
        [Tooltip("One game day lasts twenty real minutes at the default value.")]
        [SerializeField, Min(1f)] private float simulationSecondsPerRealSecond = 72f;
        [Tooltip("Length of one biological season. Four equal seasons form the canonical year.")]
        [SerializeField, Range(1, 90)] private int daysPerSeason = 15;
        [SerializeField, Range(-66f, 66f)] private float latitudeDegrees = 48f;
        [Tooltip("Day 10 preserves the former early-spring starting phase in the new 60-day year.")]
        [SerializeField, Range(0f, 359f)] private float startingDayOfYear = 10f;
        [SerializeField, Range(0f, 24f)] private float startingHour = 8f;
        [SerializeField, Min(0f)] private float seasonalTemperatureAmplitude = 14f;
        [SerializeField, Min(0f)] private float diurnalTemperatureAmplitude = 6f;

        [Header("Weather front and cloud field")]
        [Tooltip("Direction clouds travel towards. Zero points north (+Z), 180 points south (-Z).")]
        [SerializeField, Range(0f, 360f)] private float prevailingWindDirectionDegrees = 180f;
        [SerializeField, Min(0.1f)] private float prevailingWindSpeed = 8f;
        [Tooltip("Weather deliberately remains readable while the seasonal debug clock runs at x100.")]
        [SerializeField, Min(0f)] private float weatherSecondsPerRealSecond = 1f;
        [Tooltip("Duration of one smoothly blended large-scale wind regime on the atmosphere timeline.")]
        [SerializeField, Min(30f)] private float windRegimeDuration = 240f;
        [Tooltip("Maximum turn away from the prevailing direction before seasonal modulation.")]
        [SerializeField, Range(0f, 120f)] private float windDirectionVariationDegrees = 72f;
        [SerializeField, Range(0f, 0.9f)] private float windSpeedVariation = 0.48f;
        [Tooltip("Surface air is slowed by ground drag relative to the cloud-bearing flow.")]
        [SerializeField, Range(0.2f, 1f)] private float surfaceWindSpeedRatio = 0.74f;
        [Tooltip("Additional surface-level turning relative to the cloud flow.")]
        [SerializeField, Range(0f, 45f)] private float surfaceWindDirectionVariationDegrees = 18f;
        [Tooltip("Peak extra surface speed produced on the leading edge of a wet front.")]
        [SerializeField, Range(0f, 20f)] private float stormGustSpeed = 7.5f;
        [Tooltip("The first wet front starts north of the prototype camera and drifts south with the wind.")]
        [SerializeField] private float initialFrontDistanceAlongWind = -4400f;
        [SerializeField, Min(250f)] private float frontHalfWidth = 5200f;
        [Tooltip("Distance between successive wet fronts. It exceeds the visible cloud horizon so only one storm body can be seen at a time.")]
        [SerializeField, Min(20000f)] private float weatherFrontSpacing = 42000f;
        [SerializeField, Range(0.1f, 0.99f)] private float rainWaterThreshold = 0.68f;
        [SerializeField, Range(32, 256)] private int weatherMapResolution = 128;
        [SerializeField, Min(4000f)] private float weatherMapWorldSize = 24000f;
        [SerializeField, Min(0.1f)] private float weatherMapUpdateInterval = 0.6f;
        [SerializeField, Min(200f)] private float cloudBaseHeight = 1350f;
        [Tooltip("Vertical depth of the raymarched atmosphere occupied by clouds.")]
        [SerializeField, Min(300f)] private float cloudLayerThickness = 1800f;
        [SerializeField, Min(1000f)] private float cloudLayerRadius = 11000f;

        [Header("P5 rain presentation")]
        [Tooltip("Width and depth of the camera-centred rain volume in metres.")]
        [SerializeField, Min(20f)] private float rainEmissionArea = 150f;
        [SerializeField, Min(5f)] private float rainSpawnHeight = 32f;
        [SerializeField, Min(1f)] private float rainFallSpeed = 34f;
        [SerializeField, Range(0f, 1f)] private float rainWindInfluence = 0.55f;
        [SerializeField, Min(100)] private int rainMaxParticles = 6000;
        [SerializeField, Min(10f)] private float rainMaximumEmissionRate = 3000f;

        [Header("Persistent ecology")]
        [Tooltip("Canonical size of one persistent soil cell. It is independent of terrain and grass streaming chunks.")]
        [SerializeField, Min(32f)] private float ecologyCellSize = 128f;
        [Tooltip("Fixed canonical simulation step. Thirty game minutes keeps rain tracks spatially continuous.")]
        [SerializeField, Min(60f)] private float ecologySimulationStepSeconds = 1800f;
        [Tooltip("Newly visited cells reconstruct this much recent weather before joining the live simulation.")]
        [SerializeField, Min(0f)] private float ecologyWarmupSimulationSeconds = 21600f;
        [Tooltip("Normalized surface-water input produced by one hour of maximum rain.")]
        [SerializeField, Range(0.01f, 1f)] private float ecologyRainStoragePerHour = 0.2f;
        [Tooltip("Maximum normalized surface-water loss per warm, bright and windy hour.")]
        [SerializeField, Range(0.001f, 0.2f)] private float ecologySurfaceEvaporationPerHour = 0.025f;
        [Tooltip("Slow hourly loss from the root layer through drainage and plant use.")]
        [SerializeField, Range(0.0001f, 0.05f)] private float ecologyRootWaterLossPerHour = 0.0035f;
        [Tooltip("One texel represents one ecology cell. 128 cells cover 16.4 km with the default cell size.")]
        [SerializeField, Range(32, 256)] private int ecologyStateMapResolution = 128;
        [Tooltip("Minimum real-time interval between CPU state changes and one batched GPU upload.")]
        [SerializeField, Min(0.05f)] private float ecologyStateMapUploadInterval = 0.25f;

        [Header("P10 vegetation phenology")]
        [SerializeField, Range(0.05f, 4f)] private float vegetationGreeningPerDay = 1.35f;
        [SerializeField, Range(0.05f, 4f)] private float vegetationCuringPerDay = 0.72f;
        [SerializeField, Range(0.005f, 1f)] private float vegetationBiomassGrowthPerDay = 0.12f;
        [Tooltip("Daily decomposition rate of cured standing material under favourable conditions.")]
        [SerializeField, Range(0.001f, 0.25f)] private float vegetationDryBiomassDecayPerDay = 0.025f;
        [Tooltip("Additional collapse of dry stems under persistent wind, snow load and thaw.")]
        [SerializeField, Range(0f, 0.25f)] private float vegetationLodgingPerDay = 0.018f;

        [Header("P11 snow and freezing")]
        [Tooltip("Normalized snow-water storage produced by one hour of maximum cold precipitation.")]
        [SerializeField, Range(0.01f, 1f)] private float snowStoragePerHour = 0.18f;
        [SerializeField, Range(0.001f, 0.25f)] private float snowMeltPerHour = 0.04f;
        [SerializeField, Range(0.005f, 0.5f)] private float soilFreezePerHour = 0.075f;
        [SerializeField, Range(0.005f, 0.5f)] private float soilThawPerHour = 0.12f;
        [SerializeField, Min(20f)] private float snowEmissionArea = 180f;
        [SerializeField, Min(5f)] private float snowSpawnHeight = 36f;
        [SerializeField, Min(0.5f)] private float snowFallSpeed = 4.2f;
        [SerializeField, Range(0f, 1.5f)] private float snowWindInfluence = 0.82f;
        [SerializeField, Min(100)] private int snowMaxParticles = 5000;
        [SerializeField, Min(10f)] private float snowMaximumEmissionRate = 1800f;

        [Header("P9 wind-driven dust")]
        [Tooltip("Radius of the camera-local dust source field. Authoritative soil state remains world anchored.")]
        [SerializeField, Min(40f)] private float dustEmissionRadius = 240f;
        [Tooltip("Stable spacing used to quantize dust source points in canonical world space.")]
        [SerializeField, Min(2f)] private float dustSourceSpacing = 12f;
        [SerializeField, Min(0f)] private float dustSpawnHeight = 0.22f;
        [SerializeField, Min(100)] private int dustMaxParticles = 3600;
        [Tooltip("Maximum dry-ground source attempts per second. Soil and wind decide which attempts emit.")]
        [SerializeField, Min(10f)] private float dustMaximumCandidateRate = 520f;
        [Tooltip("Minimum wind speed for completely bare, dry and uncrusted soil.")]
        [SerializeField, Range(1f, 20f)] private float dustBaseWindThreshold = 5.5f;
        [Tooltip("Wind speed at which an exposed dust source reaches its full response.")]
        [SerializeField, Range(4f, 30f)] private float dustFullEmissionWindSpeed = 16f;
        [Tooltip("Fraction of surface wind inherited by airborne dust.")]
        [SerializeField, Range(0.2f, 1.5f)] private float dustWindVelocityRatio = 0.82f;

        [Header("P4 wind presentation")]
        [Tooltip("Distance between the broad dark/silver gust bands visible across feather grass.")]
        [SerializeField, Min(80f)] private float windGustWavelength = 420f;
        [Tooltip("Distance over which one broad gust remains coherent across the wind.")]
        [SerializeField, Min(160f)] private float windGustCrossScale = 1250f;
        [SerializeField, Min(8f)] private float windFineScale = 44f;
        [Tooltip("Visual gusts move more slowly than the air itself so their passage remains readable.")]
        [SerializeField, Range(0.02f, 1f)] private float windGustAdvection = 0.22f;
        [Tooltip("Maximum tip displacement as a fraction of plant height before biome stiffness is applied.")]
        [SerializeField, Range(0f, 0.75f)] private float grassWindBend = 0.48f;

        [Header("Chunk streaming")]
        [SerializeField, Min(64f)] private float chunkSize = 512f;
        [SerializeField, Range(9, 129)] private int nearResolution = 33;
        [SerializeField, Range(9, 65)] private int middleResolution = 17;
        [SerializeField, Range(5, 33)] private int farResolution = 9;
        [SerializeField, Min(0)] private int nearRadius = 2;
        [SerializeField, Min(1)] private int middleRadius = 5;
        [SerializeField, Min(2)] private int farRadius = 9;
        [SerializeField, Min(0.5f)] private float skirtDepth = 20f;

        [Header("Streamed world work")]
        [Tooltip("Shared main-thread budget for terrain, vegetation, and weather generation. A single indivisible step may exceed it.")]
        [SerializeField, Min(0.25f)] private float worldWorkBudgetMilliseconds = 6f;

        [Header("Floating origin")]
        [SerializeField, Min(256f)] private float floatingOriginThreshold = 2048f;

        [Header("Prototype camera")]
        [SerializeField, Min(1f)] private float cameraMoveSpeed = 80f;
        [SerializeField, Min(1f)] private float cameraBoostMultiplier = 6f;
        [SerializeField, Range(0.01f, 1f)] private float mouseSensitivity = 0.08f;
        [SerializeField, Min(5f)] private float initialCameraHeight = 120f;
        [SerializeField, Min(1000f)] private float cameraFarClip = 20000f;

        [Header("Physical player")]
        [SerializeField, Min(0.25f)] private float playerBallRadius = 1.25f;
        [SerializeField, Min(1f)] private float playerBallMass = 18f;
        [SerializeField, Min(1f)] private float playerDriveAcceleration = 24f;
        [SerializeField, Min(1f)] private float playerMaximumSpeed = 22f;
        [SerializeField, Min(2f)] private float playerCameraDistance = 10f;
        [SerializeField, Min(0.5f)] private float playerCameraHeight = 2.8f;

        [Header("Physical tracks")]
        [SerializeField, Min(0.5f)] private float trackCellSize = 1.5f;
        [SerializeField, Range(128, 512)] private int trackMapResolution = 512;
        [SerializeField, Min(0.02f)] private float trackMapUploadInterval = 0.1f;
        [SerializeField, Min(0.1f)] private float maximumMudSinkDepth = 0.22f;
        [SerializeField, Min(0.1f)] private float maximumSnowSinkDepth = 0.42f;
        [SerializeField, Min(0.01f)] private float maximumTrackRutDepth = 0.12f;

        public int WorldSeed => worldSeed;
        public int GeneratorVersion => generatorVersion;
        public TerrainOverrideMap TerrainOverrides => terrainOverrides;
        public float MacroScale => macroScale;
        public float MacroAmplitude => macroAmplitude;
        public float MesoScale => mesoScale;
        public float MesoAmplitude => mesoAmplitude;
        public float MicroScale => microScale;
        public float MicroAmplitude => microAmplitude;
        public float BaseHeight => baseHeight;
        public int SurfaceVersion => surfaceVersion;
        public float ClimateScale => climateScale;
        public float ClimateDetailScale => climateDetailScale;
        public float GeologyScale => geologyScale;
        public float SoilDetailScale => soilDetailScale;
        public float VegetationScale => vegetationScale;
        public float NearGrassSpacing => nearGrassSpacing;
        public float MiddleGrassSpacing => middleGrassSpacing;
        public float GrassTuftHeight => grassTuftHeight;
        public float GrassTuftWidth => grassTuftWidth;
        public float GrassCellSize => Mathf.Max(16f, grassCellSize);
        public float GrassCandidateSpacing => Mathf.Clamp(grassCandidateSpacing, 0.35f, GrassCellSize * 0.25f);
        public float GrassFullDensityRadius => Mathf.Max(16f, grassFullDensityRadius);
        public float GrassDrawRadius => Mathf.Max(grassDrawRadius, GrassFullDensityRadius + GrassCellSize);
        public float SimulationSecondsPerRealSecond => simulationSecondsPerRealSecond;
        public int DaysPerSeason => Mathf.Clamp(daysPerSeason, 1, 90);
        public int DaysPerYear => DaysPerSeason * 4;
        public float LatitudeDegrees => latitudeDegrees;
        public float StartingDayOfYear => Mathf.Repeat(startingDayOfYear, DaysPerYear);
        public float StartingHour => Mathf.Repeat(startingHour, 24f);
        public float SeasonalTemperatureAmplitude => seasonalTemperatureAmplitude;
        public float DiurnalTemperatureAmplitude => diurnalTemperatureAmplitude;
        public float PrevailingWindDirectionDegrees => prevailingWindDirectionDegrees;
        public float PrevailingWindSpeed => prevailingWindSpeed;
        public float WeatherSecondsPerRealSecond => weatherSecondsPerRealSecond;
        public float WindRegimeDuration => Mathf.Max(30f, windRegimeDuration);
        public float WindDirectionVariationDegrees => Mathf.Clamp(windDirectionVariationDegrees, 0f, 120f);
        public float WindSpeedVariation => Mathf.Clamp(windSpeedVariation, 0f, 0.9f);
        public float SurfaceWindSpeedRatio => Mathf.Clamp(surfaceWindSpeedRatio, 0.2f, 1f);
        public float SurfaceWindDirectionVariationDegrees => Mathf.Clamp(
            surfaceWindDirectionVariationDegrees,
            0f,
            45f);
        public float StormGustSpeed => Mathf.Clamp(stormGustSpeed, 0f, 20f);
        public float InitialFrontDistanceAlongWind => initialFrontDistanceAlongWind;
        public float FrontHalfWidth => frontHalfWidth;
        public float WeatherFrontSpacing => Mathf.Max(
            weatherFrontSpacing,
            Mathf.Max(FrontHalfWidth * 5f, CloudLayerRadius * 3.2f));
        public float RainWaterThreshold => rainWaterThreshold;
        public int WeatherMapResolution => Mathf.Clamp(Mathf.ClosestPowerOfTwo(weatherMapResolution), 32, 256);
        public float WeatherMapWorldSize => Mathf.Max(weatherMapWorldSize, FarRadius * ChunkSize * 2.4f);
        public float WeatherMapUpdateInterval => weatherMapUpdateInterval;
        public float CloudBaseHeight => cloudBaseHeight;
        public float CloudLayerThickness => Mathf.Max(300f, cloudLayerThickness);
        public float CloudLayerRadius => Mathf.Min(cloudLayerRadius, WeatherMapWorldSize * 0.48f);
        public float RainEmissionArea => Mathf.Max(20f, rainEmissionArea);
        public float RainSpawnHeight => Mathf.Max(5f, rainSpawnHeight);
        public float RainFallSpeed => Mathf.Max(1f, rainFallSpeed);
        public float RainWindInfluence => Mathf.Clamp01(rainWindInfluence);
        public int RainMaxParticles => Mathf.Max(100, rainMaxParticles);
        public float RainMaximumEmissionRate => Mathf.Max(10f, rainMaximumEmissionRate);
        public float EcologyCellSize => Mathf.Max(32f, ecologyCellSize);
        public float EcologySimulationStepSeconds => Mathf.Max(60f, ecologySimulationStepSeconds);
        public float EcologyWarmupSimulationSeconds => Mathf.Max(0f, ecologyWarmupSimulationSeconds);
        public float EcologyRainStoragePerHour => Mathf.Clamp(ecologyRainStoragePerHour, 0.01f, 1f);
        public float EcologySurfaceEvaporationPerHour => Mathf.Clamp(
            ecologySurfaceEvaporationPerHour,
            0.001f,
            0.2f);
        public float EcologyRootWaterLossPerHour => Mathf.Clamp(
            ecologyRootWaterLossPerHour,
            0.0001f,
            0.05f);
        public float EcologyActiveRadius => (FarRadius + 1) * ChunkSize;
        public int EcologyStateMapResolution
        {
            get
            {
                var configured = Mathf.ClosestPowerOfTwo(ecologyStateMapResolution);
                var activeDiameterInCells = Mathf.CeilToInt(EcologyActiveRadius / EcologyCellSize) * 2 + 4;
                var required = Mathf.NextPowerOfTwo(activeDiameterInCells);
                return Mathf.Clamp(Mathf.Max(configured, required), 32, 256);
            }
        }
        public float EcologyStateMapUploadInterval => Mathf.Max(0.05f, ecologyStateMapUploadInterval);
        public float EcologyStateMapWorldSize => EcologyStateMapResolution * EcologyCellSize;
        public float VegetationGreeningPerDay => Mathf.Clamp(vegetationGreeningPerDay, 0.05f, 4f);
        public float VegetationCuringPerDay => Mathf.Clamp(vegetationCuringPerDay, 0.05f, 4f);
        public float VegetationBiomassGrowthPerDay => Mathf.Clamp(
            vegetationBiomassGrowthPerDay,
            0.005f,
            1f);
        public float VegetationDryBiomassDecayPerDay => Mathf.Clamp(
            vegetationDryBiomassDecayPerDay,
            0.001f,
            0.25f);
        public float VegetationLodgingPerDay => Mathf.Clamp(vegetationLodgingPerDay, 0f, 0.25f);
        public float SnowStoragePerHour => Mathf.Clamp(snowStoragePerHour, 0.01f, 1f);
        public float SnowMeltPerHour => Mathf.Clamp(snowMeltPerHour, 0.001f, 0.25f);
        public float SoilFreezePerHour => Mathf.Clamp(soilFreezePerHour, 0.005f, 0.5f);
        public float SoilThawPerHour => Mathf.Clamp(soilThawPerHour, 0.005f, 0.5f);
        public float SnowEmissionArea => Mathf.Max(20f, snowEmissionArea);
        public float SnowSpawnHeight => Mathf.Max(5f, snowSpawnHeight);
        public float SnowFallSpeed => Mathf.Max(0.5f, snowFallSpeed);
        public float SnowWindInfluence => Mathf.Clamp(snowWindInfluence, 0f, 1.5f);
        public int SnowMaxParticles => Mathf.Max(100, snowMaxParticles);
        public float SnowMaximumEmissionRate => Mathf.Max(10f, snowMaximumEmissionRate);
        public float DustEmissionRadius => Mathf.Max(40f, dustEmissionRadius);
        public float DustSourceSpacing => Mathf.Max(2f, dustSourceSpacing);
        public float DustSpawnHeight => Mathf.Max(0f, dustSpawnHeight);
        public int DustMaxParticles => Mathf.Max(100, dustMaxParticles);
        public float DustMaximumCandidateRate => Mathf.Max(10f, dustMaximumCandidateRate);
        public float DustBaseWindThreshold => Mathf.Clamp(dustBaseWindThreshold, 1f, 20f);
        public float DustFullEmissionWindSpeed => Mathf.Max(
            DustBaseWindThreshold + 0.5f,
            Mathf.Clamp(dustFullEmissionWindSpeed, 4f, 30f));
        public float DustWindVelocityRatio => Mathf.Clamp(dustWindVelocityRatio, 0.2f, 1.5f);
        public float WindGustWavelength => Mathf.Max(80f, windGustWavelength);
        public float WindGustCrossScale => Mathf.Max(WindGustWavelength, windGustCrossScale);
        public float WindFineScale => Mathf.Max(8f, windFineScale);
        public float WindGustAdvection => Mathf.Clamp(windGustAdvection, 0.02f, 1f);
        public float GrassWindBend => Mathf.Clamp(grassWindBend, 0f, 0.75f);
        public float ChunkSize => chunkSize;
        public int NearResolution => NormalizeResolution(nearResolution);
        public int MiddleResolution => NormalizeResolution(middleResolution);
        public int FarResolution => NormalizeResolution(farResolution);
        public int NearRadius => Mathf.Max(0, nearRadius);
        public int MiddleRadius => Mathf.Max(NearRadius + 1, middleRadius);
        public int FarRadius => Mathf.Max(MiddleRadius + 1, farRadius);
        public float SkirtDepth => skirtDepth;
        public float WorldWorkBudgetMilliseconds => Mathf.Max(0.25f, worldWorkBudgetMilliseconds);
        public float FloatingOriginThreshold => floatingOriginThreshold;
        public float CameraMoveSpeed => cameraMoveSpeed;
        public float CameraBoostMultiplier => cameraBoostMultiplier;
        public float MouseSensitivity => mouseSensitivity;
        public float InitialCameraHeight => initialCameraHeight;
        public float CameraFarClip => cameraFarClip;
        public float PlayerBallRadius => Mathf.Max(0.25f, playerBallRadius);
        public float PlayerBallMass => Mathf.Max(1f, playerBallMass);
        public float PlayerDriveAcceleration => Mathf.Max(1f, playerDriveAcceleration);
        public float PlayerMaximumSpeed => Mathf.Max(1f, playerMaximumSpeed);
        public float PlayerCameraDistance => Mathf.Max(2f, playerCameraDistance);
        public float PlayerCameraHeight => Mathf.Max(0.5f, playerCameraHeight);
        public float TrackCellSize => Mathf.Max(0.5f, trackCellSize);
        public int TrackMapResolution => Mathf.Clamp(Mathf.ClosestPowerOfTwo(trackMapResolution), 128, 512);
        public float TrackMapWorldSize => TrackCellSize * TrackMapResolution;
        public float TrackMapUploadInterval => Mathf.Max(0.02f, trackMapUploadInterval);
        public float MaximumMudSinkDepth => Mathf.Max(0.1f, maximumMudSinkDepth);
        public float MaximumSnowSinkDepth => Mathf.Max(0.1f, maximumSnowSinkDepth);
        public float MaximumTrackRutDepth => Mathf.Max(0.01f, maximumTrackRutDepth);

        public static SteppeWorldSettings CreateRuntimeDefaults()
        {
            var settings = CreateInstance<SteppeWorldSettings>();
            settings.hideFlags = HideFlags.DontSave;
            return settings;
        }

        public int ResolutionForLod(int lod)
        {
            switch (lod)
            {
                case 0: return NearResolution;
                case 1: return MiddleResolution;
                default: return FarResolution;
            }
        }

        private static int NormalizeResolution(int resolution)
        {
            var quads = Mathf.Max(4, resolution - 1);
            var power = Mathf.ClosestPowerOfTwo(quads);
            return power + 1;
        }

        private void OnValidate()
        {
            nearResolution = NormalizeResolution(nearResolution);
            middleResolution = Mathf.Min(nearResolution, NormalizeResolution(middleResolution));
            farResolution = Mathf.Min(middleResolution, NormalizeResolution(farResolution));
            middleRadius = Mathf.Max(nearRadius + 1, middleRadius);
            farRadius = Mathf.Max(middleRadius + 1, farRadius);
            weatherMapResolution = Mathf.Clamp(Mathf.ClosestPowerOfTwo(weatherMapResolution), 32, 256);
            weatherMapWorldSize = Mathf.Max(weatherMapWorldSize, farRadius * chunkSize * 2.4f);
            cloudLayerRadius = Mathf.Min(cloudLayerRadius, weatherMapWorldSize * 0.48f);
        }
    }
}
