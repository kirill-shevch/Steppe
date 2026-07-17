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
        [Tooltip("Temporary test year: four seasons of approximately three game days each.")]
        [SerializeField, Range(4, 365)] private int daysPerYear = 12;
        [SerializeField, Range(-66f, 66f)] private float latitudeDegrees = 48f;
        [SerializeField, Range(0f, 364f)] private float startingDayOfYear = 2f;
        [SerializeField, Range(0f, 24f)] private float startingHour = 8f;
        [SerializeField, Min(0f)] private float seasonalTemperatureAmplitude = 14f;
        [SerializeField, Min(0f)] private float diurnalTemperatureAmplitude = 6f;

        [Header("Weather front and cloud field")]
        [Tooltip("Direction clouds travel towards. Zero points north (+Z), 180 points south (-Z).")]
        [SerializeField, Range(0f, 360f)] private float prevailingWindDirectionDegrees = 180f;
        [SerializeField, Min(0.1f)] private float prevailingWindSpeed = 8f;
        [Tooltip("Weather deliberately remains readable while the seasonal debug clock runs at x100.")]
        [SerializeField, Min(0f)] private float weatherSecondsPerRealSecond = 1f;
        [Tooltip("The first wet front starts north of the prototype camera and drifts south with the wind.")]
        [SerializeField] private float initialFrontDistanceAlongWind = -4400f;
        [SerializeField, Min(250f)] private float frontHalfWidth = 2800f;
        [Tooltip("Distance between successive wet fronts along the prevailing wind.")]
        [SerializeField, Min(4000f)] private float weatherFrontSpacing = 8200f;
        [SerializeField, Range(0.1f, 0.99f)] private float rainWaterThreshold = 0.68f;
        [SerializeField, Range(32, 256)] private int weatherMapResolution = 128;
        [SerializeField, Min(4000f)] private float weatherMapWorldSize = 24000f;
        [SerializeField, Min(0.1f)] private float weatherMapUpdateInterval = 0.6f;
        [SerializeField, Min(200f)] private float cloudBaseHeight = 1350f;
        [SerializeField, Min(1000f)] private float cloudLayerRadius = 11000f;

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
        public int DaysPerYear => daysPerYear;
        public float LatitudeDegrees => latitudeDegrees;
        public float StartingDayOfYear => Mathf.Repeat(startingDayOfYear, DaysPerYear);
        public float StartingHour => Mathf.Repeat(startingHour, 24f);
        public float SeasonalTemperatureAmplitude => seasonalTemperatureAmplitude;
        public float DiurnalTemperatureAmplitude => diurnalTemperatureAmplitude;
        public float PrevailingWindDirectionDegrees => prevailingWindDirectionDegrees;
        public float PrevailingWindSpeed => prevailingWindSpeed;
        public float WeatherSecondsPerRealSecond => weatherSecondsPerRealSecond;
        public float InitialFrontDistanceAlongWind => initialFrontDistanceAlongWind;
        public float FrontHalfWidth => frontHalfWidth;
        public float WeatherFrontSpacing => Mathf.Max(weatherFrontSpacing, FrontHalfWidth * 2.75f);
        public float RainWaterThreshold => rainWaterThreshold;
        public int WeatherMapResolution => Mathf.Clamp(Mathf.ClosestPowerOfTwo(weatherMapResolution), 32, 256);
        public float WeatherMapWorldSize => Mathf.Max(weatherMapWorldSize, FarRadius * ChunkSize * 2.4f);
        public float WeatherMapUpdateInterval => weatherMapUpdateInterval;
        public float CloudBaseHeight => cloudBaseHeight;
        public float CloudLayerRadius => Mathf.Min(cloudLayerRadius, WeatherMapWorldSize * 0.48f);
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
