using System;
using System.Collections.Generic;
using Steppe.Settings;
using Steppe.Surface;
using Steppe.World;
using UnityEngine;

namespace Steppe.Terrain
{
    public sealed class TerrainChunkStreamer : MonoBehaviour, IWorldWorkSource
    {
        private readonly Dictionary<ChunkCoordinate, TerrainChunk> loaded = new Dictionary<ChunkCoordinate, TerrainChunk>();
        private readonly Dictionary<ChunkCoordinate, int> desired = new Dictionary<ChunkCoordinate, int>();
        private readonly List<BuildRequest> pending = new List<BuildRequest>();
        private readonly Stack<TerrainChunk> pool = new Stack<TerrainChunk>();

        private SteppeWorldSettings settings;
        private FloatingOriginSystem floatingOrigin;
        private Transform focus;
        private Transform worldSpaceRoot;
        private WorldWorkScheduler workScheduler;
        private TerrainHeightGenerator generator;
        private SteppeSurfaceGenerator surfaceGenerator;
        private Material terrainMaterial;
        private bool ownsMaterial;
        private bool hasCenter;
        private ChunkCoordinate center;

        public int LoadedCount => loaded.Count;
        public int PendingCount => pending.Count;
        public ChunkCoordinate CenterCoordinate => center;
        public bool HasPendingWorldWork => pending.Count > 0;

        public event Action<ChunkCoordinate, int> ChunkReady;
        public event Action<ChunkCoordinate> ChunkRemoved;

        public void Configure(
            SteppeWorldSettings worldSettings,
            FloatingOriginSystem origin,
            Transform focusTransform,
            Transform root,
            WorldWorkScheduler scheduler,
            Material material = null)
        {
            settings = worldSettings != null ? worldSettings : throw new ArgumentNullException(nameof(worldSettings));
            floatingOrigin = origin != null ? origin : throw new ArgumentNullException(nameof(origin));
            focus = focusTransform != null ? focusTransform : throw new ArgumentNullException(nameof(focusTransform));
            worldSpaceRoot = root != null ? root : throw new ArgumentNullException(nameof(root));
            workScheduler = scheduler != null ? scheduler : throw new ArgumentNullException(nameof(scheduler));
            generator = new TerrainHeightGenerator(settings);
            surfaceGenerator = new SteppeSurfaceGenerator(settings);

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

            hasCenter = false;
            workScheduler.Register(this);
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
                ChunkRemoved?.Invoke(coordinate);
            }

            pending.Sort((left, right) => left.DistanceSquared.CompareTo(right.DistanceSquared));
        }

        public void ExecuteWorldWorkStep()
        {
            if (pending.Count == 0)
            {
                return;
            }

            var request = pending[0];
            pending.RemoveAt(0);

            if (!desired.TryGetValue(request.Coordinate, out var desiredLod) || desiredLod != request.Lod)
            {
                return;
            }

            if (!loaded.TryGetValue(request.Coordinate, out var chunk))
            {
                chunk = pool.Count > 0
                    ? pool.Pop()
                    : new TerrainChunk(worldSpaceRoot, terrainMaterial);
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
            chunk.Apply(
                request.Coordinate,
                request.Lod,
                meshData,
                settings.ChunkSize,
                floatingOrigin);
            ChunkReady?.Invoke(request.Coordinate, request.Lod);
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

        private void OnDestroy()
        {
            if (workScheduler != null)
            {
                workScheduler.Unregister(this);
            }
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
