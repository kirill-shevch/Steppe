using System;
using System.Collections.Generic;
using Steppe.Settings;
using Steppe.Surface;
using Steppe.World;
using UnityEngine;

namespace Steppe.Terrain
{
    public sealed class TerrainChunkStreamer : MonoBehaviour
    {
        private readonly Dictionary<ChunkCoordinate, TerrainChunk> loaded = new Dictionary<ChunkCoordinate, TerrainChunk>();
        private readonly Dictionary<ChunkCoordinate, int> desired = new Dictionary<ChunkCoordinate, int>();
        private readonly List<BuildRequest> pending = new List<BuildRequest>();
        private readonly Stack<TerrainChunk> pool = new Stack<TerrainChunk>();

        private SteppeWorldSettings settings;
        private FloatingOriginSystem floatingOrigin;
        private Transform focus;
        private Transform worldSpaceRoot;
        private TerrainHeightGenerator generator;
        private SteppeSurfaceGenerator surfaceGenerator;
        private Material terrainMaterial;
        private Material vegetationMaterial;
        private bool ownsMaterial;
        private bool ownsVegetationMaterial;
        private bool generateLegacyVegetation;
        private bool hasCenter;
        private ChunkCoordinate center;

        public int LoadedCount => loaded.Count;
        public int PendingCount => pending.Count;
        public ChunkCoordinate CenterCoordinate => center;

        public void Configure(
            SteppeWorldSettings worldSettings,
            FloatingOriginSystem origin,
            Transform focusTransform,
            Transform root,
            Material material = null,
            Material grassMaterial = null,
            bool buildLegacyVegetation = true)
        {
            settings = worldSettings != null ? worldSettings : throw new ArgumentNullException(nameof(worldSettings));
            floatingOrigin = origin != null ? origin : throw new ArgumentNullException(nameof(origin));
            focus = focusTransform != null ? focusTransform : throw new ArgumentNullException(nameof(focusTransform));
            worldSpaceRoot = root != null ? root : throw new ArgumentNullException(nameof(root));
            generator = new TerrainHeightGenerator(settings);
            surfaceGenerator = new SteppeSurfaceGenerator(settings);
            generateLegacyVegetation = buildLegacyVegetation;

            if (material != null)
            {
                terrainMaterial = material;
                ownsMaterial = false;
            }
            else
            {
                terrainMaterial = CreateRuntimeMaterial();
                ownsMaterial = true;
            }

            if (grassMaterial != null)
            {
                vegetationMaterial = grassMaterial;
                ownsVegetationMaterial = false;
            }
            else
            {
                vegetationMaterial = CreateRuntimeVegetationMaterial();
                ownsVegetationMaterial = true;
            }

            hasCenter = false;
        }

        public void GetLodCounts(out int near, out int middle, out int far)
        {
            near = 0;
            middle = 0;
            far = 0;

            foreach (var chunk in loaded.Values)
            {
                switch (chunk.Lod)
                {
                    case 0: near++; break;
                    case 1: middle++; break;
                    default: far++; break;
                }
            }
        }

        private void Update()
        {
            if (settings == null || floatingOrigin == null || focus == null || worldSpaceRoot == null)
            {
                return;
            }

            var worldPosition = floatingOrigin.LocalToWorld(focus.position);
            var currentCenter = ChunkCoordinate.FromWorld(worldPosition.X, worldPosition.Z, settings.ChunkSize);

            if (!hasCenter || currentCenter != center)
            {
                center = currentCenter;
                hasCenter = true;
                RefreshDesiredChunks();
            }

            ProcessPendingBuilds();
        }

        private void RefreshDesiredChunks()
        {
            desired.Clear();
            pending.Clear();

            var farRadius = settings.FarRadius;
            var farRadiusSquared = (farRadius + 0.35f) * (farRadius + 0.35f);

            for (var z = -farRadius; z <= farRadius; z++)
            {
                for (var x = -farRadius; x <= farRadius; x++)
                {
                    var distanceSquared = x * x + z * z;
                    if (distanceSquared > farRadiusSquared)
                    {
                        continue;
                    }

                    var distance = Mathf.Sqrt(distanceSquared);
                    var lod = distance <= settings.NearRadius
                        ? 0
                        : distance <= settings.MiddleRadius ? 1 : 2;
                    var coordinate = center.Offset(x, z);
                    desired[coordinate] = lod;

                    if (!loaded.TryGetValue(coordinate, out var existing) || existing.Lod != lod)
                    {
                        pending.Add(new BuildRequest(coordinate, lod, distanceSquared));
                    }
                }
            }

            var removals = new List<ChunkCoordinate>();
            foreach (var pair in loaded)
            {
                if (!desired.ContainsKey(pair.Key))
                {
                    removals.Add(pair.Key);
                }
            }

            for (var index = 0; index < removals.Count; index++)
            {
                var coordinate = removals[index];
                var chunk = loaded[coordinate];
                loaded.Remove(coordinate);
                chunk.Deactivate();
                pool.Push(chunk);
            }

            pending.Sort((left, right) => left.DistanceSquared.CompareTo(right.DistanceSquared));
        }

        private void ProcessPendingBuilds()
        {
            var buildCount = Mathf.Min(settings.ChunksBuiltPerFrame, pending.Count);
            for (var index = 0; index < buildCount; index++)
            {
                var request = pending[0];
                pending.RemoveAt(0);

                if (!desired.TryGetValue(request.Coordinate, out var desiredLod) || desiredLod != request.Lod)
                {
                    continue;
                }

                if (!loaded.TryGetValue(request.Coordinate, out var chunk))
                {
                    chunk = pool.Count > 0
                        ? pool.Pop()
                        : new TerrainChunk(worldSpaceRoot, terrainMaterial, vegetationMaterial);
                    loaded.Add(request.Coordinate, chunk);
                }

                var resolution = settings.ResolutionForLod(request.Lod);
                var meshData = TerrainMeshBuilder.Build(
                    generator,
                    request.Coordinate,
                    settings.ChunkSize,
                    resolution,
                    settings.SkirtDepth,
                    surfaceGenerator);
                var vegetationData = generateLegacyVegetation
                    ? VegetationMeshBuilder.Build(
                        generator,
                        surfaceGenerator,
                        settings,
                        request.Coordinate,
                        request.Lod)
                    : null;
                chunk.Apply(
                    request.Coordinate,
                    request.Lod,
                    meshData,
                    vegetationData,
                    settings.ChunkSize,
                    floatingOrigin);
            }
        }

        private static Material CreateRuntimeMaterial()
        {
            var shader = Shader.Find("Steppe/Terrain Surface")
                         ?? Shader.Find("Universal Render Pipeline/Lit")
                         ?? Shader.Find("Universal Render Pipeline/Simple Lit")
                         ?? Shader.Find("Standard");

            if (shader == null)
            {
                throw new InvalidOperationException("No supported terrain shader was found.");
            }

            var material = new Material(shader)
            {
                name = "Steppe P1 Terrain Material",
                hideFlags = HideFlags.DontSave
            };

            var baseColor = Color.white;
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", baseColor);
            }
            else if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", baseColor);
            }

            if (material.HasProperty("_Smoothness"))
            {
                material.SetFloat("_Smoothness", 0.05f);
            }

            return material;
        }

        private static Material CreateRuntimeVegetationMaterial()
        {
            var shader = Shader.Find("Steppe/Vegetation")
                         ?? Shader.Find("Universal Render Pipeline/Unlit")
                         ?? Shader.Find("Unlit/Color");

            if (shader == null)
            {
                throw new InvalidOperationException("No supported vegetation shader was found.");
            }

            var material = new Material(shader)
            {
                name = "Steppe P1 Vegetation Material",
                hideFlags = HideFlags.DontSave,
                enableInstancing = true
            };
            return material;
        }

        private void OnDestroy()
        {
            foreach (var chunk in loaded.Values)
            {
                chunk.Dispose();
            }

            while (pool.Count > 0)
            {
                pool.Pop().Dispose();
            }

            loaded.Clear();
            pool.Clear();

            if (ownsMaterial && terrainMaterial != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(terrainMaterial);
                }
                else
                {
                    DestroyImmediate(terrainMaterial);
                }
            }

            if (ownsVegetationMaterial && vegetationMaterial != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(vegetationMaterial);
                }
                else
                {
                    DestroyImmediate(vegetationMaterial);
                }
            }
        }

        private readonly struct BuildRequest
        {
            public BuildRequest(ChunkCoordinate coordinate, int lod, float distanceSquared)
            {
                Coordinate = coordinate;
                Lod = lod;
                DistanceSquared = distanceSquared;
            }

            public ChunkCoordinate Coordinate { get; }
            public int Lod { get; }
            public float DistanceSquared { get; }
        }
    }
}
