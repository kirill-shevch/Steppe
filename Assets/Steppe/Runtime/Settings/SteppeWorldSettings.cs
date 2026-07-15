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

        [Header("Chunk streaming")]
        [SerializeField, Min(64f)] private float chunkSize = 512f;
        [SerializeField, Range(9, 129)] private int nearResolution = 33;
        [SerializeField, Range(9, 65)] private int middleResolution = 17;
        [SerializeField, Range(5, 33)] private int farResolution = 9;
        [SerializeField, Min(0)] private int nearRadius = 2;
        [SerializeField, Min(1)] private int middleRadius = 5;
        [SerializeField, Min(2)] private int farRadius = 9;
        [SerializeField, Range(1, 16)] private int chunksBuiltPerFrame = 4;
        [SerializeField, Min(0.5f)] private float skirtDepth = 20f;

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
        public float ChunkSize => chunkSize;
        public int NearResolution => NormalizeResolution(nearResolution);
        public int MiddleResolution => NormalizeResolution(middleResolution);
        public int FarResolution => NormalizeResolution(farResolution);
        public int NearRadius => Mathf.Max(0, nearRadius);
        public int MiddleRadius => Mathf.Max(NearRadius + 1, middleRadius);
        public int FarRadius => Mathf.Max(MiddleRadius + 1, farRadius);
        public int ChunksBuiltPerFrame => Mathf.Max(1, chunksBuiltPerFrame);
        public float SkirtDepth => skirtDepth;
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
        }
    }
}
