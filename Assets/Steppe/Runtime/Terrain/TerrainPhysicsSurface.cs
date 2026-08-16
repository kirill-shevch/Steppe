using System;
using System.Collections.Generic;
using Steppe.World;
using UnityEngine;

namespace Steppe.Terrain
{
    /// <summary>
    /// Streams height samples into one continuous TerrainCollider. The collider
    /// covers the full expedition area and never changes identity or position
    /// relative to the world root, so wheels cannot cross a collider edge during
    /// normal play. Only distant heightmap patches are populated as the caravan
    /// travels; generated patches remain resident for the lifetime of the world.
    /// </summary>
    internal sealed class TerrainPhysicsSurface
    {
        private const int ChunksPerPatch = 4;
        private const int PatchRadius = 1;
        private const int WorldChunksAcross = 128;
        private const float MinimumCollisionHeight = -1024f;
        private const float CollisionHeightRange = 2048f;

        private readonly HashSet<ChunkCoordinate> generated =
            new HashSet<ChunkCoordinate>();
        private readonly HashSet<ChunkCoordinate> desired =
            new HashSet<ChunkCoordinate>();
        private readonly List<BuildRequest> pending = new List<BuildRequest>();
        private readonly Transform parent;
        private readonly TerrainHeightGenerator generator;
        private readonly FloatingOriginSystem floatingOrigin;
        private readonly float patchSize;
        private readonly float sampleSpacing;
        private readonly int patchResolution;
        private readonly float worldSize;
        private readonly int worldResolution;

        private ContinuousSurface surface;
        private bool hasCenter;
        private ChunkCoordinate center;

        public TerrainPhysicsSurface(
            Transform parent,
            TerrainHeightGenerator generator,
            float chunkSize,
            int chunkResolution,
            FloatingOriginSystem floatingOrigin)
        {
            this.parent = parent;
            this.generator = generator;
            this.floatingOrigin = floatingOrigin;
            patchSize = chunkSize * ChunksPerPatch;
            sampleSpacing = chunkSize / Mathf.Max(1, chunkResolution - 1);
            patchResolution = Mathf.RoundToInt(patchSize / sampleSpacing) + 1;
            worldSize = chunkSize * WorldChunksAcross;
            worldResolution = Mathf.RoundToInt(worldSize / sampleSpacing) + 1;
        }

        public bool IsReady => generated.Count > 0;
        public bool HasPendingWork => pending.Count > 0;
        public int ActiveColliderCount => surface != null ? 1 : 0;
        public int VertexCount => surface != null
            ? worldResolution * worldResolution
            : 0;
        public float TileSize => patchSize;

        public bool Contains(double worldX, double worldZ)
        {
            if (surface == null || !surface.Contains(worldX, worldZ))
            {
                return false;
            }

            var coordinate = ChunkCoordinate.FromWorld(
                worldX,
                worldZ,
                patchSize);
            return generated.Contains(coordinate);
        }

        public void Refresh(double worldX, double worldZ)
        {
            if (surface == null)
            {
                var halfWorld = worldSize * 0.5;
                var minimumWorldX = Math.Floor(
                    (worldX + halfWorld) / worldSize) * worldSize - halfWorld;
                var minimumWorldZ = Math.Floor(
                    (worldZ + halfWorld) / worldSize) * worldSize - halfWorld;
                surface = new ContinuousSurface(
                    parent,
                    floatingOrigin,
                    minimumWorldX,
                    minimumWorldZ,
                    worldSize,
                    worldResolution);
            }

            var nextCenter = ChunkCoordinate.FromWorld(
                worldX,
                worldZ,
                patchSize);
            if (hasCenter && nextCenter == center)
            {
                return;
            }

            center = nextCenter;
            hasCenter = true;
            desired.Clear();
            pending.Clear();
            for (var z = -PatchRadius; z <= PatchRadius; z++)
            {
                for (var x = -PatchRadius; x <= PatchRadius; x++)
                {
                    var coordinate = center.Offset(x, z);
                    if (!surface.ContainsPatch(coordinate, patchSize))
                    {
                        continue;
                    }

                    desired.Add(coordinate);
                    if (!generated.Contains(coordinate))
                    {
                        pending.Add(new BuildRequest(
                            coordinate,
                            x * x + z * z));
                    }
                }
            }

            pending.Sort((left, right) =>
                left.DistanceSquared.CompareTo(right.DistanceSquared));
        }

        public void ExecuteWorkStep()
        {
            if (surface == null || pending.Count == 0)
            {
                return;
            }

            var request = pending[0];
            pending.RemoveAt(0);
            if (!desired.Contains(request.Coordinate)
                || generated.Contains(request.Coordinate))
            {
                return;
            }

            var minimumWorldX = request.Coordinate.X * (double)patchSize;
            var minimumWorldZ = request.Coordinate.Z * (double)patchSize;
            var heights = new float[patchResolution, patchResolution];
            for (var z = 0; z < patchResolution; z++)
            {
                for (var x = 0; x < patchResolution; x++)
                {
                    var height = generator.SampleHeight(
                        minimumWorldX + x * sampleSpacing,
                        minimumWorldZ + z * sampleSpacing);
                    heights[z, x] = Mathf.Clamp01(
                        ((float)height - MinimumCollisionHeight)
                        / CollisionHeightRange);
                }
            }

            surface.ApplyPatch(
                minimumWorldX,
                minimumWorldZ,
                sampleSpacing,
                heights);
            generated.Add(request.Coordinate);
        }

        public void Dispose()
        {
            surface?.Dispose();
            surface = null;
            generated.Clear();
            desired.Clear();
            pending.Clear();
        }

        private sealed class ContinuousSurface
        {
            private readonly GameObject gameObject;
            private readonly TerrainData terrainData;
            private readonly double minimumWorldX;
            private readonly double minimumWorldZ;
            private readonly double maximumWorldX;
            private readonly double maximumWorldZ;

            public ContinuousSurface(
                Transform parent,
                FloatingOriginSystem floatingOrigin,
                double minimumWorldX,
                double minimumWorldZ,
                float worldSize,
                int resolution)
            {
                this.minimumWorldX = minimumWorldX;
                this.minimumWorldZ = minimumWorldZ;
                maximumWorldX = minimumWorldX + worldSize;
                maximumWorldZ = minimumWorldZ + worldSize;

                gameObject = new GameObject("Terrain Physics Surface");
                gameObject.transform.SetParent(parent, false);
                terrainData = new TerrainData
                {
                    name = "Steppe Continuous Terrain Collision",
                    hideFlags = HideFlags.DontSave,
                    heightmapResolution = resolution,
                    size = new Vector3(
                        worldSize,
                        CollisionHeightRange,
                        worldSize)
                };
                var collider = gameObject.AddComponent<TerrainCollider>();
                gameObject.transform.position = floatingOrigin.WorldToLocal(
                    minimumWorldX,
                    MinimumCollisionHeight,
                    minimumWorldZ);
                collider.terrainData = terrainData;
            }

            public bool Contains(double worldX, double worldZ)
            {
                return worldX >= minimumWorldX
                       && worldX <= maximumWorldX
                       && worldZ >= minimumWorldZ
                       && worldZ <= maximumWorldZ;
            }

            public bool ContainsPatch(
                ChunkCoordinate coordinate,
                float patchSize)
            {
                var patchMinimumX = coordinate.X * (double)patchSize;
                var patchMinimumZ = coordinate.Z * (double)patchSize;
                return patchMinimumX >= minimumWorldX
                       && patchMinimumX + patchSize <= maximumWorldX
                       && patchMinimumZ >= minimumWorldZ
                       && patchMinimumZ + patchSize <= maximumWorldZ;
            }

            public void ApplyPatch(
                double patchMinimumWorldX,
                double patchMinimumWorldZ,
                float sampleSpacing,
                float[,] heights)
            {
                var sampleX = Mathf.RoundToInt(
                    (float)((patchMinimumWorldX - minimumWorldX)
                            / sampleSpacing));
                var sampleZ = Mathf.RoundToInt(
                    (float)((patchMinimumWorldZ - minimumWorldZ)
                            / sampleSpacing));
                terrainData.SetHeights(sampleX, sampleZ, heights);
                Physics.SyncTransforms();
            }

            public void Dispose()
            {
                if (Application.isPlaying)
                {
                    UnityEngine.Object.Destroy(terrainData);
                    UnityEngine.Object.Destroy(gameObject);
                }
                else
                {
                    UnityEngine.Object.DestroyImmediate(terrainData);
                    UnityEngine.Object.DestroyImmediate(gameObject);
                }
            }
        }

        private readonly struct BuildRequest
        {
            public BuildRequest(
                ChunkCoordinate coordinate,
                int distanceSquared)
            {
                Coordinate = coordinate;
                DistanceSquared = distanceSquared;
            }

            public ChunkCoordinate Coordinate { get; }
            public int DistanceSquared { get; }
        }
    }
}
