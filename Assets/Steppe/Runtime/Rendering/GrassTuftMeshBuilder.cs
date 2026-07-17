using System.Collections.Generic;
using UnityEngine;

namespace Steppe.Rendering
{
    internal static class GrassTuftMeshBuilder
    {
        private static readonly Vector2[] BladeOffsets =
        {
            new Vector2(-0.28f, -0.12f),
            new Vector2(0.22f, -0.18f),
            new Vector2(-0.08f, 0.24f),
            new Vector2(0.31f, 0.18f),
            new Vector2(-0.31f, 0.22f)
        };

        public static Mesh Build()
        {
            var vertices = new List<Vector3>(BladeOffsets.Length * 7);
            var normals = new List<Vector3>(vertices.Capacity);
            var uvs = new List<Vector2>(vertices.Capacity);
            var triangles = new List<int>(BladeOffsets.Length * 15);

            for (var blade = 0; blade < BladeOffsets.Length; blade++)
            {
                var angle = blade * 2.39996323f + 0.37f;
                var height = 0.78f + blade * 0.047f;
                var curve = blade % 2 == 0 ? 0.11f : -0.075f;
                AddBlade(vertices, normals, uvs, triangles, BladeOffsets[blade], angle, height, curve);
            }

            var mesh = new Mesh
            {
                name = "Steppe P4 Segmented Grass Tuft",
                hideFlags = HideFlags.DontSave
            };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0, true);
            mesh.bounds = new Bounds(new Vector3(0f, 0.55f, 0f), new Vector3(1.25f, 1.2f, 1.25f));
            mesh.UploadMeshData(true);
            return mesh;
        }

        private static void AddBlade(
            List<Vector3> vertices,
            List<Vector3> normals,
            List<Vector2> uvs,
            List<int> triangles,
            Vector2 offset,
            float angle,
            float height,
            float curve)
        {
            var forward = new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle));
            var side = new Vector3(Mathf.Cos(angle), 0f, -Mathf.Sin(angle));
            var root = new Vector3(offset.x, 0f, offset.y);
            var start = vertices.Count;

            AddPair(0f, 0.085f, 0f);
            AddPair(0.34f, 0.068f, 0.18f);
            AddPair(0.69f, 0.039f, 0.58f);

            var tipCenter = root + Vector3.up * height + forward * curve;
            vertices.Add(tipCenter);
            normals.Add((forward + Vector3.up * 0.18f).normalized);
            uvs.Add(new Vector2(0.5f, 1f));

            triangles.Add(start);
            triangles.Add(start + 2);
            triangles.Add(start + 3);
            triangles.Add(start);
            triangles.Add(start + 3);
            triangles.Add(start + 1);

            triangles.Add(start + 2);
            triangles.Add(start + 4);
            triangles.Add(start + 5);
            triangles.Add(start + 2);
            triangles.Add(start + 5);
            triangles.Add(start + 3);

            triangles.Add(start + 4);
            triangles.Add(start + 6);
            triangles.Add(start + 5);

            void AddPair(float normalizedHeight, float halfWidth, float curveAmount)
            {
                var center = root
                             + Vector3.up * (height * normalizedHeight)
                             + forward * (curve * curveAmount);
                vertices.Add(center - side * halfWidth);
                vertices.Add(center + side * halfWidth);
                var normal = (forward + Vector3.up * 0.18f).normalized;
                normals.Add(normal);
                normals.Add(normal);
                uvs.Add(new Vector2(0f, normalizedHeight));
                uvs.Add(new Vector2(1f, normalizedHeight));
            }
        }
    }
}
