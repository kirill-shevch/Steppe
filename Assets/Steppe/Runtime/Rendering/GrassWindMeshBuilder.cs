using System;
using System.Collections.Generic;
using UnityEngine;

namespace Steppe.Rendering
{
    /// <summary>
    /// Adds a few vertical joints to the selected crossed-quad grass asset. The source
    /// remains a tiny CC0 mesh on disk; the runtime copy can curve instead of moving its
    /// entire top edge as one rigid card when the wind vertex shader deforms it.
    /// </summary>
    internal static class GrassWindMeshBuilder
    {
        private const int VerticesPerSourceCard = 4;
        private const int VerticalSegments = 4;
        private const int ClusterCopies = 3;
        private static readonly Vector2[] ClusterOffsets =
        {
            new Vector2(-8.5f, -3.8f),
            new Vector2(8.7f, -2.9f),
            new Vector2(-0.4f, 9.3f)
        };
        private static readonly float[] ClusterAngles = { -0.31f, 1.83f, 3.96f };
        private static readonly float[] ClusterScales = { 0.94f, 1.03f, 0.88f };
        private static readonly float[] ClusterPhases = { 0.08f, 0.43f, 0.79f };

        public static Mesh Build(Mesh source)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            var sourceVertices = source.vertices;
            var sourceNormals = source.normals;
            var sourceUv = source.uv;
            if (sourceVertices.Length == 0
                || sourceVertices.Length % VerticesPerSourceCard != 0
                || sourceNormals.Length != sourceVertices.Length
                || sourceUv.Length != sourceVertices.Length)
            {
                return null;
            }

            var cardCount = sourceVertices.Length / VerticesPerSourceCard;
            var vertices = new List<Vector3>(
                ClusterCopies * cardCount * (VerticalSegments + 1) * 2);
            var normals = new List<Vector3>(vertices.Capacity);
            var uv = new List<Vector2>(vertices.Capacity);
            var clusterData = new List<Vector2>(vertices.Capacity);
            var triangles = new List<int>(
                ClusterCopies * cardCount * VerticalSegments * 6);

            for (var copy = 0; copy < ClusterCopies; copy++)
            {
                for (var card = 0; card < cardCount; card++)
                {
                    var sourceOffset = card * VerticesPerSourceCard;
                    var bottomLeft = sourceOffset;
                    var bottomRight = sourceOffset + 1;
                    var topRight = sourceOffset + 2;
                    var topLeft = sourceOffset + 3;
                    var outputOffset = vertices.Count;

                    for (var level = 0; level <= VerticalSegments; level++)
                    {
                        var t = level / (float)VerticalSegments;
                        AddInterpolatedVertex(
                            sourceVertices,
                            sourceNormals,
                            sourceUv,
                            bottomLeft,
                            topLeft,
                            t,
                            copy,
                            vertices,
                            normals,
                            uv,
                            clusterData);
                        AddInterpolatedVertex(
                            sourceVertices,
                            sourceNormals,
                            sourceUv,
                            bottomRight,
                            topRight,
                            t,
                            copy,
                            vertices,
                            normals,
                            uv,
                            clusterData);
                    }

                    for (var segment = 0; segment < VerticalSegments; segment++)
                    {
                        var lowerLeft = outputOffset + segment * 2;
                        var lowerRight = lowerLeft + 1;
                        var upperLeft = lowerLeft + 2;
                        var upperRight = lowerLeft + 3;
                        triangles.Add(lowerLeft);
                        triangles.Add(lowerRight);
                        triangles.Add(upperRight);
                        triangles.Add(lowerLeft);
                        triangles.Add(upperRight);
                        triangles.Add(upperLeft);
                    }
                }
            }

            var mesh = new Mesh
            {
                name = $"{source.name} (P4 wind segmented)",
                hideFlags = HideFlags.DontSave
            };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uv);
            mesh.SetUVs(1, clusterData);
            mesh.SetTriangles(triangles, 0, true);
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void AddInterpolatedVertex(
            Vector3[] sourceVertices,
            Vector3[] sourceNormals,
            Vector2[] sourceUv,
            int bottom,
            int top,
            float t,
            int copy,
            List<Vector3> vertices,
            List<Vector3> normals,
            List<Vector2> uv,
            List<Vector2> clusterData)
        {
            var position = Vector3.Lerp(sourceVertices[bottom], sourceVertices[top], t)
                           * ClusterScales[copy];
            var sine = Mathf.Sin(ClusterAngles[copy]);
            var cosine = Mathf.Cos(ClusterAngles[copy]);
            position = new Vector3(
                position.x * cosine - position.z * sine + ClusterOffsets[copy].x,
                position.y,
                position.x * sine + position.z * cosine + ClusterOffsets[copy].y);
            var normal = Vector3.Slerp(sourceNormals[bottom], sourceNormals[top], t).normalized;
            normal = new Vector3(
                normal.x * cosine - normal.z * sine,
                normal.y,
                normal.x * sine + normal.z * cosine);

            vertices.Add(position);
            normals.Add(normal.normalized);
            uv.Add(Vector2.Lerp(sourceUv[bottom], sourceUv[top], t));
            clusterData.Add(new Vector2(ClusterPhases[copy], 0f));
        }
    }
}
