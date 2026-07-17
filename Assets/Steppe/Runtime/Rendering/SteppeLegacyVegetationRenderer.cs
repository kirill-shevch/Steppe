using System;
using System.Collections.Generic;
using Steppe.Settings;
using Steppe.Surface;
using Steppe.Terrain;
using Steppe.World;
using UnityEngine;
using UnityEngine.Rendering;

namespace Steppe.Rendering
{
    /// <summary>
    /// CPU mesh fallback for platforms without indirect rendering support. It listens to
    /// terrain lifecycle events but owns all vegetation generation and rendering state.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SteppeLegacyVegetationRenderer : MonoBehaviour, IWorldWorkSource
    {
        private readonly Dictionary<ChunkCoordinate, int> desired = new Dictionary<ChunkCoordinate, int>();
        private readonly Dictionary<ChunkCoordinate, LegacyVegetationCell> loaded =
            new Dictionary<ChunkCoordinate, LegacyVegetationCell>();
        private readonly Queue<BuildRequest> pending = new Queue<BuildRequest>();
        private readonly Stack<LegacyVegetationCell> pool = new Stack<LegacyVegetationCell>();

        private SteppeWorldSettings settings;
        private FloatingOriginSystem floatingOrigin;
        private TerrainChunkStreamer terrainStreamer;
        private WorldWorkScheduler workScheduler;
        private TerrainHeightGenerator terrainGenerator;
        private SteppeSurfaceGenerator surfaceGenerator;
        private Material vegetationMaterial;
        private bool ownsMaterial;

        public bool HasPendingWorldWork => pending.Count > 0;
        public int LoadedCellCount => loaded.Count;

        public void Configure(
            SteppeWorldSettings worldSettings,
            FloatingOriginSystem origin,
            TerrainChunkStreamer streamer,
            WorldWorkScheduler scheduler,
            Material material = null)
        {
            settings = worldSettings != null ? worldSettings : throw new ArgumentNullException(nameof(worldSettings));
            floatingOrigin = origin != null ? origin : throw new ArgumentNullException(nameof(origin));
            terrainStreamer = streamer != null ? streamer : throw new ArgumentNullException(nameof(streamer));
            workScheduler = scheduler != null ? scheduler : throw new ArgumentNullException(nameof(scheduler));
            terrainGenerator = new TerrainHeightGenerator(settings);
            surfaceGenerator = new SteppeSurfaceGenerator(settings);
            vegetationMaterial = material != null ? material : CreateRuntimeMaterial();
            ownsMaterial = material == null;

            terrainStreamer.ChunkReady += HandleChunkReady;
            terrainStreamer.ChunkRemoved += HandleChunkRemoved;
            workScheduler.Register(this);
        }

        public void ExecuteWorldWorkStep()
        {
            while (pending.Count > 0)
            {
                var request = pending.Dequeue();
                if (!desired.TryGetValue(request.Coordinate, out var desiredLod)
                    || desiredLod != request.Lod)
                {
                    continue;
                }

                if (loaded.TryGetValue(request.Coordinate, out var current) && current.Lod == request.Lod)
                {
                    return;
                }

                var data = VegetationMeshBuilder.Build(
                    terrainGenerator,
                    surfaceGenerator,
                    settings,
                    request.Coordinate,
                    request.Lod);
                if (!loaded.TryGetValue(request.Coordinate, out var cell))
                {
                    cell = pool.Count > 0
                        ? pool.Pop()
                        : new LegacyVegetationCell(transform, vegetationMaterial);
                    loaded.Add(request.Coordinate, cell);
                }

                cell.Apply(request.Coordinate, request.Lod, data, settings.ChunkSize, floatingOrigin);
                return;
            }
        }

        private void HandleChunkReady(ChunkCoordinate coordinate, int lod)
        {
            if (lod > 1)
            {
                desired.Remove(coordinate);
                RemoveCell(coordinate);
                return;
            }

            desired[coordinate] = lod;
            if (!loaded.TryGetValue(coordinate, out var existing) || existing.Lod != lod)
            {
                pending.Enqueue(new BuildRequest(coordinate, lod));
            }
        }

        private void HandleChunkRemoved(ChunkCoordinate coordinate)
        {
            desired.Remove(coordinate);
            RemoveCell(coordinate);
        }

        private void RemoveCell(ChunkCoordinate coordinate)
        {
            if (!loaded.TryGetValue(coordinate, out var cell))
            {
                return;
            }

            loaded.Remove(coordinate);
            cell.Deactivate();
            pool.Push(cell);
        }

        private static Material CreateRuntimeMaterial()
        {
            var shader = Shader.Find("Steppe/Vegetation")
                         ?? Shader.Find("Universal Render Pipeline/Unlit")
                         ?? Shader.Find("Unlit/Color");
            if (shader == null)
            {
                throw new InvalidOperationException("No supported vegetation shader was found.");
            }

            return new Material(shader)
            {
                name = "Steppe Legacy Vegetation Material",
                hideFlags = HideFlags.DontSave,
                enableInstancing = true
            };
        }

        private void OnDestroy()
        {
            if (terrainStreamer != null)
            {
                terrainStreamer.ChunkReady -= HandleChunkReady;
                terrainStreamer.ChunkRemoved -= HandleChunkRemoved;
            }
            if (workScheduler != null)
            {
                workScheduler.Unregister(this);
            }

            foreach (var cell in loaded.Values)
            {
                cell.Dispose();
            }
            while (pool.Count > 0)
            {
                pool.Pop().Dispose();
            }

            if (ownsMaterial && vegetationMaterial != null)
            {
                DestroyOwned(vegetationMaterial);
            }
        }

        private static void DestroyOwned(UnityEngine.Object value)
        {
            if (Application.isPlaying)
            {
                Destroy(value);
            }
            else
            {
                DestroyImmediate(value);
            }
        }

        private sealed class LegacyVegetationCell : IDisposable
        {
            private readonly GameObject gameObject;
            private readonly Mesh mesh;

            public LegacyVegetationCell(Transform parent, Material material)
            {
                gameObject = new GameObject("Legacy Vegetation Cell");
                gameObject.transform.SetParent(parent, false);
                var filter = gameObject.AddComponent<MeshFilter>();
                var renderer = gameObject.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = true;
                mesh = new Mesh
                {
                    name = "Steppe Legacy Vegetation Cell",
                    hideFlags = HideFlags.DontSave
                };
                mesh.MarkDynamic();
                filter.sharedMesh = mesh;
            }

            public int Lod { get; private set; } = -1;

            public void Apply(
                ChunkCoordinate coordinate,
                int lod,
                VegetationMeshData data,
                float chunkSize,
                FloatingOriginSystem floatingOrigin)
            {
                Lod = lod;
                gameObject.name = $"Legacy Vegetation {coordinate.X}, {coordinate.Z} [LOD {lod}]";
                gameObject.transform.position = floatingOrigin.WorldToLocal(
                    coordinate.X * (double)chunkSize,
                    0.0,
                    coordinate.Z * (double)chunkSize);
                mesh.Clear();
                mesh.indexFormat = data.Vertices.Length > ushort.MaxValue
                    ? IndexFormat.UInt32
                    : IndexFormat.UInt16;
                mesh.vertices = data.Vertices;
                mesh.normals = data.Normals;
                mesh.colors32 = data.Colors;
                mesh.triangles = data.Triangles;
                mesh.RecalculateBounds();
                gameObject.SetActive(!data.IsEmpty);
            }

            public void Deactivate()
            {
                Lod = -1;
                gameObject.SetActive(false);
            }

            public void Dispose()
            {
                DestroyOwned(mesh);
                DestroyOwned(gameObject);
            }
        }

        private readonly struct BuildRequest
        {
            public BuildRequest(ChunkCoordinate coordinate, int lod)
            {
                Coordinate = coordinate;
                Lod = lod;
            }

            public ChunkCoordinate Coordinate { get; }
            public int Lod { get; }
        }
    }
}
