using System;
using System.Collections.Generic;
using Steppe.Settings;
using Steppe.Terrain;
using Steppe.World;
using UnityEngine;

namespace Steppe.Surface
{
    public sealed class VegetationMeshData
    {
        public VegetationMeshData(Vector3[] vertices, Vector3[] normals, Color32[] colors, int[] triangles)
        {
            Vertices = vertices;
            Normals = normals;
            Colors = colors;
            Triangles = triangles;
        }

        public Vector3[] Vertices { get; }
        public Vector3[] Normals { get; }
        public Color32[] Colors { get; }
        public int[] Triangles { get; }
        public bool IsEmpty => Vertices.Length == 0;
    }

    public static class VegetationMeshBuilder
    {
        public static VegetationMeshData Build(
            TerrainHeightGenerator terrainGenerator,
            SteppeSurfaceGenerator surfaceGenerator,
            SteppeWorldSettings settings,
            ChunkCoordinate coordinate,
            int lod)
        {
            if (lod > 1)
            {
                return Empty();
            }

            var spacing = lod == 0 ? settings.NearGrassSpacing : settings.MiddleGrassSpacing;
            var cells = Mathf.Max(1, Mathf.CeilToInt(settings.ChunkSize / spacing));
            var actualSpacing = settings.ChunkSize / cells;
            var estimatedTufts = cells * cells / 2;
            var vertices = new List<Vector3>(estimatedTufts * 9);
            var normals = new List<Vector3>(estimatedTufts * 9);
            var colors = new List<Color32>(estimatedTufts * 9);
            var triangles = new List<int>(estimatedTufts * 9);
            var worldOriginX = coordinate.X * (double)settings.ChunkSize;
            var worldOriginZ = coordinate.Z * (double)settings.ChunkSize;
            var seed = unchecked(settings.WorldSeed + settings.GeneratorVersion * 7919 + lod * 3571);

            for (var cellZ = 0; cellZ < cells; cellZ++)
            {
                for (var cellX = 0; cellX < cells; cellX++)
                {
                    var hash = DeterministicNoise.Hash(
                        coordinate.X * (long)cells + cellX,
                        coordinate.Z * (long)cells + cellZ,
                        seed);
                    var jitterX = Hash01(hash) * 0.76 + 0.12;
                    var jitterZ = Hash01(Rotate(hash, 21)) * 0.76 + 0.12;
                    var localX = (cellX + jitterX) * actualSpacing;
                    var localZ = (cellZ + jitterZ) * actualSpacing;
                    var worldX = worldOriginX + localX;
                    var worldZ = worldOriginZ + localZ;
                    var height = terrainGenerator.SampleHeight(worldX, worldZ);
                    var normal = terrainGenerator.SampleNormal(worldX, worldZ, 2.0);
                    var surface = surfaceGenerator.Sample(worldX, worldZ, height, normal.y);

                    var presence = Hash01(Rotate(hash, 42));
                    var lodDensity = lod == 0 ? 0.92 : 0.56;
                    if (presence > surface.VegetationPotential * lodDensity)
                    {
                        continue;
                    }

                    var scaleVariation = 0.72 + Hash01(Rotate(hash, 9)) * 0.56;
                    var heightScale = (float)(settings.GrassTuftHeight
                                              * surface.NominalVegetationHeight
                                              * scaleVariation);
                    var widthScale = settings.GrassTuftWidth
                                     * (0.65f + heightScale * 0.22f)
                                     * (0.78f + (float)Hash01(Rotate(hash, 31)) * 0.44f);
                    var rotation = (float)(Hash01(Rotate(hash, 53)) * Math.PI);
                    var color = (Color)surface.VegetationColor;
                    color *= 0.88f + (float)Hash01(Rotate(hash, 15)) * 0.2f;
                    var bladeCount = surface.DominantBiome switch
                    {
                        SteppeBiome.Meadow => 3,
                        SteppeBiome.FeatherGrass => 3,
                        SteppeBiome.Dry => 2,
                        _ => 1
                    };

                    AddTuft(
                        vertices,
                        normals,
                        colors,
                        triangles,
                        new Vector3((float)localX, (float)height + 0.02f, (float)localZ),
                        widthScale,
                        heightScale,
                        rotation,
                        (Color32)color,
                        bladeCount);
                }
            }

            return new VegetationMeshData(
                vertices.ToArray(),
                normals.ToArray(),
                colors.ToArray(),
                triangles.ToArray());
        }

        private static void AddTuft(
            List<Vector3> vertices,
            List<Vector3> normals,
            List<Color32> colors,
            List<int> triangles,
            Vector3 center,
            float width,
            float height,
            float rotation,
            Color32 color,
            int bladeCount)
        {
            for (var blade = 0; blade < bladeCount; blade++)
            {
                var angle = rotation + blade * Mathf.PI / Mathf.Max(1, bladeCount);
                var side = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * width * 0.5f;
                var start = vertices.Count;
                vertices.Add(center - side);
                vertices.Add(center + side);
                vertices.Add(center + Vector3.up * height);
                normals.Add(Vector3.up);
                normals.Add(Vector3.up);
                normals.Add(Vector3.up);
                colors.Add(color);
                colors.Add(color);
                colors.Add(color);
                triangles.Add(start);
                triangles.Add(start + 1);
                triangles.Add(start + 2);
            }
        }

        private static VegetationMeshData Empty()
        {
            return new VegetationMeshData(
                Array.Empty<Vector3>(),
                Array.Empty<Vector3>(),
                Array.Empty<Color32>(),
                Array.Empty<int>());
        }

        private static double Hash01(ulong hash)
        {
            return (hash >> 11) * (1.0 / 9007199254740992.0);
        }

        private static ulong Rotate(ulong value, int count)
        {
            return (value << count) | (value >> (64 - count));
        }
    }
}
