using Steppe.World;
using UnityEngine;
using UnityEngine.Rendering;

namespace Steppe.Terrain
{
    /// <summary>
    /// A double-buffered collision carpet around the caravan. Keeping every nearby
    /// terrain triangle in one MeshCollider prevents PhysX from treating chunk
    /// borders as separate collision edges and kicking wheels sideways.
    /// </summary>
    internal sealed class TerrainPhysicsSurface
    {
        private readonly SurfaceBuffer[] buffers;
        private int activeIndex = -1;
        private double minimumWorldX;
        private double minimumWorldZ;
        private double maximumWorldX;
        private double maximumWorldZ;

        public TerrainPhysicsSurface(Transform parent)
        {
            buffers = new[]
            {
                new SurfaceBuffer(parent, 0),
                new SurfaceBuffer(parent, 1)
            };
        }

        public bool IsReady => activeIndex >= 0;
        public int ActiveColliderCount => IsReady ? 1 : 0;
        public int VertexCount => IsReady ? buffers[activeIndex].Mesh.vertexCount : 0;

        public bool Contains(double worldX, double worldZ)
        {
            return IsReady
                   && worldX >= minimumWorldX
                   && worldX <= maximumWorldX
                   && worldZ >= minimumWorldZ
                   && worldZ <= maximumWorldZ;
        }

        public void Rebuild(
            TerrainHeightGenerator generator,
            ChunkCoordinate center,
            int chunkRadius,
            float chunkSize,
            int chunkResolution,
            FloatingOriginSystem floatingOrigin)
        {
            var radius = Mathf.Max(1, chunkRadius);
            var segmentsPerChunk = Mathf.Max(1, chunkResolution - 1);
            var chunksAcross = radius * 2 + 1;
            var resolution = chunksAcross * segmentsPerChunk + 1;
            var vertexCount = resolution * resolution;
            var triangleIndexCount = (resolution - 1) * (resolution - 1) * 6;
            var step = chunkSize / segmentsPerChunk;
            var minimumCoordinate = center.Offset(-radius, -radius);
            var originX = minimumCoordinate.X * (double)chunkSize;
            var originZ = minimumCoordinate.Z * (double)chunkSize;

            var vertices = new Vector3[vertexCount];
            var triangles = new int[triangleIndexCount];
            for (var z = 0; z < resolution; z++)
            {
                for (var x = 0; x < resolution; x++)
                {
                    var localX = x * step;
                    var localZ = z * step;
                    vertices[z * resolution + x] = new Vector3(
                        localX,
                        (float)generator.SampleHeight(originX + localX, originZ + localZ),
                        localZ);
                }
            }

            var triangleIndex = 0;
            for (var z = 0; z < resolution - 1; z++)
            {
                for (var x = 0; x < resolution - 1; x++)
                {
                    var bottomLeft = z * resolution + x;
                    var bottomRight = bottomLeft + 1;
                    var topLeft = bottomLeft + resolution;
                    var topRight = topLeft + 1;
                    triangles[triangleIndex++] = bottomLeft;
                    triangles[triangleIndex++] = topLeft;
                    triangles[triangleIndex++] = topRight;
                    triangles[triangleIndex++] = bottomLeft;
                    triangles[triangleIndex++] = topRight;
                    triangles[triangleIndex++] = bottomRight;
                }
            }

            var nextIndex = activeIndex == 0 ? 1 : 0;
            var next = buffers[nextIndex];
            next.Apply(
                vertices,
                triangles,
                floatingOrigin.WorldToLocal(originX, 0.0, originZ));

            if (activeIndex >= 0)
            {
                buffers[activeIndex].Deactivate();
            }

            activeIndex = nextIndex;
            minimumWorldX = originX;
            minimumWorldZ = originZ;
            maximumWorldX = originX + chunksAcross * (double)chunkSize;
            maximumWorldZ = originZ + chunksAcross * (double)chunkSize;
            Physics.SyncTransforms();
        }

        public void Dispose()
        {
            for (var index = 0; index < buffers.Length; index++)
            {
                buffers[index].Dispose();
            }
        }

        private sealed class SurfaceBuffer
        {
            private readonly GameObject gameObject;
            private readonly MeshCollider collider;

            public SurfaceBuffer(Transform parent, int index)
            {
                gameObject = new GameObject($"Terrain Physics Surface {index + 1}");
                gameObject.transform.SetParent(parent, false);
                collider = gameObject.AddComponent<MeshCollider>();
                collider.cookingOptions =
                    MeshColliderCookingOptions.CookForFasterSimulation
                    | MeshColliderCookingOptions.EnableMeshCleaning
                    | MeshColliderCookingOptions.WeldColocatedVertices
                    | MeshColliderCookingOptions.UseFastMidphase;
                Mesh = new Mesh
                {
                    name = $"Steppe Unified Terrain Collision {index + 1}",
                    hideFlags = HideFlags.DontSave,
                    indexFormat = IndexFormat.UInt32
                };
                Mesh.MarkDynamic();
                gameObject.SetActive(false);
            }

            public Mesh Mesh { get; }

            public void Apply(Vector3[] vertices, int[] triangles, Vector3 position)
            {
                collider.sharedMesh = null;
                Mesh.Clear();
                Mesh.vertices = vertices;
                Mesh.triangles = triangles;
                Mesh.RecalculateBounds();
                gameObject.transform.position = position;
                gameObject.SetActive(true);
                collider.sharedMesh = Mesh;
                collider.enabled = true;
            }

            public void Deactivate()
            {
                collider.enabled = false;
                collider.sharedMesh = null;
                gameObject.SetActive(false);
            }

            public void Dispose()
            {
                if (Application.isPlaying)
                {
                    Object.Destroy(Mesh);
                    Object.Destroy(gameObject);
                }
                else
                {
                    Object.DestroyImmediate(Mesh);
                    Object.DestroyImmediate(gameObject);
                }
            }
        }
    }
}
