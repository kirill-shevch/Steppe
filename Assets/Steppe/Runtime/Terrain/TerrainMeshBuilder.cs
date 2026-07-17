using System;
using System.Collections.Generic;
using Steppe.Surface;
using Steppe.World;
using UnityEngine;

namespace Steppe.Terrain
{
    public sealed class TerrainMeshData
    {
        public TerrainMeshData(
            Vector3[] vertices,
            Vector3[] normals,
            Vector2[] uvs,
            Color32[] colors,
            int[] triangles,
            int topResolution)
        {
            Vertices = vertices;
            Normals = normals;
            Uvs = uvs;
            Colors = colors;
            Triangles = triangles;
            TopResolution = topResolution;
        }

        public Vector3[] Vertices { get; }
        public Vector3[] Normals { get; }
        public Vector2[] Uvs { get; }
        public Color32[] Colors { get; }
        public int[] Triangles { get; }
        public int TopResolution { get; }

        public Vector3 GetTopVertex(int x, int z)
        {
            if (x < 0 || x >= TopResolution || z < 0 || z >= TopResolution)
            {
                throw new ArgumentOutOfRangeException();
            }

            return Vertices[z * TopResolution + x];
        }
    }

    public static class TerrainMeshBuilder
    {
        public static TerrainMeshData Build(
            TerrainHeightGenerator generator,
            ChunkCoordinate coordinate,
            float chunkSize,
            int resolution,
            float skirtDepth,
            SteppeSurfaceGenerator surfaceGenerator = null)
        {
            if (generator == null)
            {
                throw new ArgumentNullException(nameof(generator));
            }

            if (resolution < 2)
            {
                throw new ArgumentOutOfRangeException(nameof(resolution));
            }

            var vertices = new List<Vector3>(resolution * resolution + resolution * 16);
            var normals = new List<Vector3>(vertices.Capacity);
            var uvs = new List<Vector2>(vertices.Capacity);
            var colors = new List<Color32>(vertices.Capacity);
            var triangles = new List<int>((resolution - 1) * (resolution - 1) * 6 + resolution * 24);

            var worldOriginX = coordinate.X * (double)chunkSize;
            var worldOriginZ = coordinate.Z * (double)chunkSize;
            var step = chunkSize / (resolution - 1);

            for (var z = 0; z < resolution; z++)
            {
                for (var x = 0; x < resolution; x++)
                {
                    var localX = x * step;
                    var localZ = z * step;
                    var worldX = worldOriginX + localX;
                    var worldZ = worldOriginZ + localZ;
                    var height = generator.SampleHeight(worldX, worldZ);

                    var normal = generator.SampleNormal(worldX, worldZ, Math.Max(1.0, step * 0.5));
                    vertices.Add(new Vector3(localX, (float)height, localZ));
                    normals.Add(normal);
                    uvs.Add(new Vector2(localX / chunkSize, localZ / chunkSize));
                    colors.Add(surfaceGenerator != null
                        ? surfaceGenerator.Sample(worldX, worldZ, height, normal.y).GroundColor
                        : new Color32(255, 255, 255, 255));
                }
            }

            for (var z = 0; z < resolution - 1; z++)
            {
                for (var x = 0; x < resolution - 1; x++)
                {
                    var bottomLeft = z * resolution + x;
                    var bottomRight = bottomLeft + 1;
                    var topLeft = bottomLeft + resolution;
                    var topRight = topLeft + 1;

                    triangles.Add(bottomLeft);
                    triangles.Add(topLeft);
                    triangles.Add(topRight);
                    triangles.Add(bottomLeft);
                    triangles.Add(topRight);
                    triangles.Add(bottomRight);
                }
            }

            var depth = Mathf.Max(0.5f, skirtDepth);
            for (var index = 0; index < resolution - 1; index++)
            {
                AddSkirtSegment(vertices, normals, uvs, colors, triangles,
                    index, index + 1, Vector3.back, depth);
                AddSkirtSegment(vertices, normals, uvs, colors, triangles,
                    (resolution - 1) * resolution + index + 1,
                    (resolution - 1) * resolution + index,
                    Vector3.forward,
                    depth);
                AddSkirtSegment(vertices, normals, uvs, colors, triangles,
                    (index + 1) * resolution,
                    index * resolution,
                    Vector3.left,
                    depth);
                AddSkirtSegment(vertices, normals, uvs, colors, triangles,
                    index * resolution + resolution - 1,
                    (index + 1) * resolution + resolution - 1,
                    Vector3.right,
                    depth);
            }

            return new TerrainMeshData(
                vertices.ToArray(),
                normals.ToArray(),
                uvs.ToArray(),
                colors.ToArray(),
                triangles.ToArray(),
                resolution);
        }

        private static void AddSkirtSegment(
            List<Vector3> vertices,
            List<Vector3> normals,
            List<Vector2> uvs,
            List<Color32> colors,
            List<int> triangles,
            int firstTopIndex,
            int secondTopIndex,
            Vector3 outwardNormal,
            float depth)
        {
            var firstTop = vertices[firstTopIndex];
            var secondTop = vertices[secondTopIndex];
            var start = vertices.Count;

            vertices.Add(firstTop);
            vertices.Add(secondTop);
            vertices.Add(firstTop + Vector3.down * depth);
            vertices.Add(secondTop + Vector3.down * depth);

            normals.Add(outwardNormal);
            normals.Add(outwardNormal);
            normals.Add(outwardNormal);
            normals.Add(outwardNormal);

            uvs.Add(Vector2.zero);
            uvs.Add(Vector2.right);
            uvs.Add(Vector2.up);
            uvs.Add(Vector2.one);

            colors.Add(colors[firstTopIndex]);
            colors.Add(colors[secondTopIndex]);
            colors.Add(colors[firstTopIndex]);
            colors.Add(colors[secondTopIndex]);

            triangles.Add(start);
            triangles.Add(start + 2);
            triangles.Add(start + 3);
            triangles.Add(start);
            triangles.Add(start + 3);
            triangles.Add(start + 1);
        }
    }
}
