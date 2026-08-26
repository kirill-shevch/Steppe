using System;
using System.Collections.Generic;
using UnityEngine;

namespace Steppe.Rendering
{
    /// <summary>
    /// Small authored-in-code meshes make each hidden state readable by silhouette.
    /// They deliberately share no topology: ice is vertical, litter is broken and flat,
    /// seeds have heads, humus is volumetric, shoots are radial, and fungi branch.
    /// </summary>
    public static class GroundDetailMeshBuilder
    {
        public static Mesh Build(NaturalGroundDetailKind kind)
        {
            var vertices = new List<Vector3>(48);
            var normals = new List<Vector3>(48);
            var indices = new List<int>(96);

            switch (kind)
            {
                case NaturalGroundDetailKind.IceNeedles:
                    BuildIceNeedles(vertices, normals, indices);
                    break;
                case NaturalGroundDetailKind.FallenLitter:
                    BuildFallenLitter(vertices, normals, indices);
                    break;
                case NaturalGroundDetailKind.Seeds:
                    BuildSeeds(vertices, normals, indices);
                    break;
                case NaturalGroundDetailKind.HumusClods:
                    BuildHumusClods(vertices, normals, indices);
                    break;
                case NaturalGroundDetailKind.NewShoots:
                    BuildNewShoots(vertices, normals, indices);
                    break;
                case NaturalGroundDetailKind.FungalThreads:
                    BuildFungalThreads(vertices, normals, indices);
                    break;
                case NaturalGroundDetailKind.FaultFractures:
                    BuildFaultFractures(vertices, normals, indices);
                    break;
                case NaturalGroundDetailKind.RockOutcrops:
                    BuildRockOutcrops(vertices, normals, indices);
                    break;
                case NaturalGroundDetailKind.GroundwaterSeeps:
                    BuildGroundwaterSeeps(vertices, normals, indices);
                    break;
                case NaturalGroundDetailKind.CharredDebris:
                    BuildCharredDebris(vertices, normals, indices);
                    break;
                case NaturalGroundDetailKind.SoilProfileCuts:
                    BuildSoilProfileCuts(vertices, normals, indices);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(kind), kind, null);
            }

            var mesh = new Mesh
            {
                name = $"Steppe {kind} Mesh",
                hideFlags = HideFlags.DontSave,
            };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetTriangles(indices, 0, true);
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void BuildIceNeedles(
            List<Vector3> vertices,
            List<Vector3> normals,
            List<int> indices)
        {
            AddTaperedPlane(vertices, normals, indices, -0.08f, 0.08f, 0.00f, 0.31f, 0f);
            AddTaperedPlane(vertices, normals, indices, -0.065f, 0.065f, 0.00f, 0.25f, 1.0472f);
            AddTaperedPlane(vertices, normals, indices, -0.055f, 0.055f, 0.00f, 0.20f, 2.0944f);
        }

        private static void BuildFallenLitter(
            List<Vector3> vertices,
            List<Vector3> normals,
            List<int> indices)
        {
            AddGroundRibbon(vertices, normals, indices, new Vector2(-0.18f, -0.05f),
                new Vector2(0.22f, 0.08f), 0.026f, 0.014f);
            AddGroundRibbon(vertices, normals, indices, new Vector2(-0.06f, 0.18f),
                new Vector2(0.14f, -0.20f), 0.021f, 0.020f);
            AddGroundRibbon(vertices, normals, indices, new Vector2(-0.28f, 0.11f),
                new Vector2(-0.03f, -0.02f), 0.018f, 0.028f);
            AddGroundRibbon(vertices, normals, indices, new Vector2(0.02f, 0.00f),
                new Vector2(0.29f, 0.16f), 0.016f, 0.035f);
        }

        private static void BuildSeeds(
            List<Vector3> vertices,
            List<Vector3> normals,
            List<int> indices)
        {
            AddTaperedPlane(vertices, normals, indices, -0.012f, 0.012f, 0.00f, 0.50f, 0f);
            AddTaperedPlane(vertices, normals, indices, -0.010f, 0.010f, 0.00f, 0.50f, 1.5708f);
            for (var grain = 0; grain < 5; grain++)
            {
                var y = 0.35f + grain * 0.038f;
                var side = grain % 2 == 0 ? -0.018f : 0.018f;
                var center = new Vector3(side, y, -side * 0.45f);
                var width = 0.026f - grain * 0.0015f;
                AddDiamondPlane(vertices, normals, indices, center, width, 0.054f, 0f);
                AddDiamondPlane(vertices, normals, indices, center, width, 0.054f, 1.5708f);
            }
            AddGroundDiamond(vertices, normals, indices, new Vector2(-0.18f, -0.08f), 0.055f, 0.015f);
            AddGroundDiamond(vertices, normals, indices, new Vector2(0.13f, 0.12f), 0.045f, 0.019f);
            AddGroundDiamond(vertices, normals, indices, new Vector2(0.21f, -0.13f), 0.038f, 0.022f);
        }

        private static void BuildHumusClods(
            List<Vector3> vertices,
            List<Vector3> normals,
            List<int> indices)
        {
            AddClod(vertices, normals, indices, new Vector3(-0.07f, 0f, 0.01f),
                0.15f, 0.09f);
            AddClod(vertices, normals, indices, new Vector3(0.14f, 0f, -0.07f),
                0.085f, 0.055f);
        }

        private static void BuildNewShoots(
            List<Vector3> vertices,
            List<Vector3> normals,
            List<int> indices)
        {
            AddBlade(vertices, normals, indices, new Vector3(-0.025f, 0f, 0f),
                new Vector3(-0.11f, 0.25f, 0.02f), 0.035f);
            AddBlade(vertices, normals, indices, new Vector3(0.025f, 0f, 0f),
                new Vector3(0.13f, 0.23f, 0.04f), 0.032f);
            AddBlade(vertices, normals, indices, new Vector3(0f, 0f, -0.02f),
                new Vector3(0.02f, 0.20f, -0.12f), 0.029f);
            AddBlade(vertices, normals, indices, new Vector3(0f, 0f, 0.02f),
                new Vector3(-0.03f, 0.17f, 0.11f), 0.026f);
        }

        private static void BuildFungalThreads(
            List<Vector3> vertices,
            List<Vector3> normals,
            List<int> indices)
        {
            var center = new Vector2(0.02f, -0.01f);
            for (var branch = 0; branch < 7; branch++)
            {
                var angle = branch * 0.8976f + (branch % 2) * 0.18f;
                var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                var end = center + direction * (0.16f + branch * 0.012f);
                AddGroundRibbon(vertices, normals, indices, center, end, 0.009f, 0.012f);
                if (branch % 2 == 0)
                {
                    var side = new Vector2(-direction.y, direction.x);
                    AddGroundRibbon(vertices, normals, indices, end * 0.72f,
                        end + side * 0.085f, 0.007f, 0.015f);
                }
            }
            AddGroundDiamond(vertices, normals, indices, center, 0.035f, 0.018f);
        }

        private static void BuildFaultFractures(
            List<Vector3> vertices,
            List<Vector3> normals,
            List<int> indices)
        {
            var spine = new[]
            {
                new Vector2(-0.54f, -0.18f),
                new Vector2(-0.28f, -0.10f),
                new Vector2(-0.06f, 0.035f),
                new Vector2(0.20f, -0.025f),
                new Vector2(0.52f, 0.15f),
            };
            for (var index = 0; index < spine.Length - 1; index++)
            {
                AddGroundRibbon(
                    vertices,
                    normals,
                    indices,
                    spine[index],
                    spine[index + 1],
                    0.014f + index * 0.002f,
                    0.012f);
            }
            AddGroundRibbon(vertices, normals, indices, spine[1],
                new Vector2(-0.18f, 0.22f), 0.011f, 0.010f);
            AddGroundRibbon(vertices, normals, indices, spine[3],
                new Vector2(0.31f, -0.24f), 0.009f, 0.009f);
        }

        private static void BuildRockOutcrops(
            List<Vector3> vertices,
            List<Vector3> normals,
            List<int> indices)
        {
            AddAngularOutcrop(vertices, normals, indices,
                new Vector3(-0.08f, 0f, 0.01f), 0.24f, 0.31f);
            AddAngularOutcrop(vertices, normals, indices,
                new Vector3(0.24f, 0f, -0.12f), 0.13f, 0.17f);
        }

        private static void BuildGroundwaterSeeps(
            List<Vector3> vertices,
            List<Vector3> normals,
            List<int> indices)
        {
            AddGroundRing(vertices, normals, indices, Vector2.zero, 0.035f, 0.15f, 0.026f, 14);
            AddGroundRing(vertices, normals, indices, Vector2.zero, 0.19f, 0.24f, 0.024f, 14);
            AddGroundRing(vertices, normals, indices, new Vector2(0.11f, -0.07f),
                0.27f, 0.30f, 0.021f, 14);
            AddGroundDiamond(vertices, normals, indices, new Vector2(-0.04f, 0.025f), 0.07f, 0.029f);
        }

        private static void BuildCharredDebris(
            List<Vector3> vertices,
            List<Vector3> normals,
            List<int> indices)
        {
            AddTaperedPlane(vertices, normals, indices, -0.027f, 0.027f, 0f, 0.48f, 0f);
            AddTaperedPlane(vertices, normals, indices, -0.024f, 0.024f, 0f, 0.48f, 1.5708f);
            AddBlade(vertices, normals, indices, new Vector3(0f, 0.29f, 0f),
                new Vector3(0.20f, 0.46f, 0.02f), 0.021f);
            AddBlade(vertices, normals, indices, new Vector3(0f, 0.37f, 0f),
                new Vector3(-0.15f, 0.55f, -0.05f), 0.018f);
            AddGroundDiamond(vertices, normals, indices, new Vector2(-0.18f, 0.12f), 0.10f, 0.025f);
            AddGroundDiamond(vertices, normals, indices, new Vector2(0.17f, -0.08f), 0.075f, 0.030f);
        }

        private static void BuildSoilProfileCuts(
            List<Vector3> vertices,
            List<Vector3> normals,
            List<int> indices)
        {
            const float back = 0.085f;
            var levels = new[] { 0f, 0.22f, 0.49f, 0.76f, 1f };
            var ledges = new[] { 0.018f, 0.052f, 0.031f, 0.064f, 0.025f };
            var left = new[] { -0.42f, -0.72f, -0.61f, -0.49f, -0.27f };
            var right = new[] { 0.48f, 0.67f, 0.56f, 0.43f, 0.25f };
            for (var band = 0; band < levels.Length - 1; band++)
            {
                var lowerY = levels[band];
                var upperY = levels[band + 1];
                var lowerZ = ledges[band];
                var upperZ = ledges[band + 1];
                AddQuad(vertices, normals, indices,
                    new Vector3(left[band], lowerY, lowerZ),
                    new Vector3(right[band], lowerY, lowerZ),
                    new Vector3(right[band + 1], upperY, upperZ),
                    new Vector3(left[band + 1], upperY, upperZ));

                if (band > 0)
                {
                    AddQuad(vertices, normals, indices,
                        new Vector3(left[band], lowerY, -back),
                        new Vector3(right[band], lowerY, -back),
                        new Vector3(right[band], lowerY, lowerZ),
                        new Vector3(left[band], lowerY, lowerZ));
                }
            }

            AddQuad(vertices, normals, indices,
                new Vector3(left[4], 1f, -back),
                new Vector3(right[4], 1f, -back),
                new Vector3(right[4], 1f, ledges[4]),
                new Vector3(left[4], 1f, ledges[4]));
        }

        private static void AddAngularOutcrop(
            List<Vector3> vertices,
            List<Vector3> normals,
            List<int> indices,
            Vector3 center,
            float radius,
            float height)
        {
            var lower = center + Vector3.up * 0.012f;
            var lowerRing = new[]
            {
                center + new Vector3(-radius, height * 0.16f, -radius * 0.22f),
                center + new Vector3(-radius * 0.44f, height * 0.28f, radius * 0.82f),
                center + new Vector3(radius * 0.58f, height * 0.19f, radius * 0.72f),
                center + new Vector3(radius, height * 0.12f, -radius * 0.18f),
                center + new Vector3(radius * 0.18f, height * 0.23f, -radius),
            };
            var upperRing = new[]
            {
                center + new Vector3(-radius * 0.47f, height * 0.67f, -radius * 0.11f),
                center + new Vector3(-radius * 0.25f, height * 0.77f, radius * 0.40f),
                center + new Vector3(radius * 0.31f, height * 0.70f, radius * 0.34f),
                center + new Vector3(radius * 0.53f, height * 0.61f, -radius * 0.12f),
                center + new Vector3(radius * 0.09f, height * 0.73f, -radius * 0.48f),
            };
            var crown = center + new Vector3(radius * 0.06f, height * 0.84f, -radius * 0.03f);
            for (var index = 0; index < lowerRing.Length; index++)
            {
                var next = (index + 1) % lowerRing.Length;
                AddQuad(vertices, normals, indices,
                    lowerRing[index], lowerRing[next], upperRing[next], upperRing[index]);
                AddTriangle(vertices, normals, indices, crown, upperRing[index], upperRing[next]);
                AddTriangle(vertices, normals, indices, lower, lowerRing[next], lowerRing[index]);
            }
        }

        private static void AddGroundRing(
            List<Vector3> vertices,
            List<Vector3> normals,
            List<int> indices,
            Vector2 center,
            float innerRadius,
            float outerRadius,
            float height,
            int segments)
        {
            for (var segment = 0; segment < segments; segment++)
            {
                var a0 = segment * Mathf.PI * 2f / segments;
                var a1 = (segment + 1) * Mathf.PI * 2f / segments;
                var inner0 = center + new Vector2(Mathf.Cos(a0), Mathf.Sin(a0)) * innerRadius;
                var outer0 = center + new Vector2(Mathf.Cos(a0), Mathf.Sin(a0)) * outerRadius;
                var outer1 = center + new Vector2(Mathf.Cos(a1), Mathf.Sin(a1)) * outerRadius;
                var inner1 = center + new Vector2(Mathf.Cos(a1), Mathf.Sin(a1)) * innerRadius;
                AddQuad(vertices, normals, indices,
                    new Vector3(inner0.x, height, inner0.y),
                    new Vector3(inner1.x, height, inner1.y),
                    new Vector3(outer1.x, height, outer1.y),
                    new Vector3(outer0.x, height, outer0.y));
            }
        }

        private static void AddTaperedPlane(
            List<Vector3> vertices,
            List<Vector3> normals,
            List<int> indices,
            float left,
            float right,
            float bottom,
            float top,
            float angle)
        {
            var tangent = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            var a = tangent * left + Vector3.up * bottom;
            var b = tangent * right + Vector3.up * bottom;
            var c = Vector3.up * top + tangent * (right * 0.06f);
            var d = Vector3.up * top + tangent * (left * 0.06f);
            AddQuad(vertices, normals, indices, a, b, c, d);
        }

        private static void AddDiamondPlane(
            List<Vector3> vertices,
            List<Vector3> normals,
            List<int> indices,
            Vector3 center,
            float width,
            float height,
            float angle)
        {
            var tangent = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            AddQuad(vertices, normals, indices,
                center - Vector3.up * height * 0.5f,
                center + tangent * width,
                center + Vector3.up * height * 0.5f,
                center - tangent * width);
        }

        private static void AddGroundRibbon(
            List<Vector3> vertices,
            List<Vector3> normals,
            List<int> indices,
            Vector2 start,
            Vector2 end,
            float halfWidth,
            float height)
        {
            var direction = (end - start).normalized;
            var side = new Vector2(-direction.y, direction.x) * halfWidth;
            AddQuad(vertices, normals, indices,
                new Vector3(start.x - side.x, height, start.y - side.y),
                new Vector3(start.x + side.x, height, start.y + side.y),
                new Vector3(end.x + side.x, height, end.y + side.y),
                new Vector3(end.x - side.x, height, end.y - side.y));
        }

        private static void AddGroundDiamond(
            List<Vector3> vertices,
            List<Vector3> normals,
            List<int> indices,
            Vector2 center,
            float radius,
            float height)
        {
            AddQuad(vertices, normals, indices,
                new Vector3(center.x - radius, height, center.y),
                new Vector3(center.x, height, center.y - radius * 0.42f),
                new Vector3(center.x + radius, height, center.y),
                new Vector3(center.x, height, center.y + radius * 0.42f));
        }

        private static void AddClod(
            List<Vector3> vertices,
            List<Vector3> normals,
            List<int> indices,
            Vector3 center,
            float radius,
            float height)
        {
            var top = center + new Vector3(radius * 0.16f, height, -radius * 0.12f);
            var bottom = center + Vector3.up * 0.008f;
            var ring = new[]
            {
                center + new Vector3(-radius, height * 0.28f, -radius * 0.20f),
                center + new Vector3(-radius * 0.28f, height * 0.38f, radius * 0.86f),
                center + new Vector3(radius * 0.88f, height * 0.22f, radius * 0.42f),
                center + new Vector3(radius * 0.62f, height * 0.32f, -radius * 0.74f),
            };
            for (var index = 0; index < ring.Length; index++)
            {
                var next = (index + 1) % ring.Length;
                AddTriangle(vertices, normals, indices, top, ring[index], ring[next]);
                AddTriangle(vertices, normals, indices, bottom, ring[next], ring[index]);
            }
        }

        private static void AddBlade(
            List<Vector3> vertices,
            List<Vector3> normals,
            List<int> indices,
            Vector3 root,
            Vector3 tip,
            float halfWidth)
        {
            var direction = (tip - root).normalized;
            var sideDirection = Vector3.Cross(direction, Vector3.forward);
            if (sideDirection.sqrMagnitude < 0.1f)
            {
                sideDirection = Vector3.right;
            }
            var side = sideDirection.normalized * halfWidth;
            AddTriangle(vertices, normals, indices, root - side, root + side, tip);
        }

        private static void AddQuad(
            List<Vector3> vertices,
            List<Vector3> normals,
            List<int> indices,
            Vector3 a,
            Vector3 b,
            Vector3 c,
            Vector3 d)
        {
            AddTriangle(vertices, normals, indices, a, b, c);
            AddTriangle(vertices, normals, indices, a, c, d);
        }

        private static void AddTriangle(
            List<Vector3> vertices,
            List<Vector3> normals,
            List<int> indices,
            Vector3 a,
            Vector3 b,
            Vector3 c)
        {
            var normal = Vector3.Cross(b - a, c - a).normalized;
            if (normal.sqrMagnitude < 0.5f)
            {
                normal = Vector3.up;
            }
            var start = vertices.Count;
            vertices.Add(a);
            vertices.Add(b);
            vertices.Add(c);
            normals.Add(normal);
            normals.Add(normal);
            normals.Add(normal);
            indices.Add(start);
            indices.Add(start + 1);
            indices.Add(start + 2);
        }
    }
}
