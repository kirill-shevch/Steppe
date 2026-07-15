using Steppe.World;
using UnityEngine;
using UnityEngine.Rendering;

namespace Steppe.Terrain
{
    internal sealed class TerrainChunk
    {
        private readonly GameObject gameObject;
        private readonly MeshFilter meshFilter;
        private readonly MeshRenderer meshRenderer;
        private readonly Mesh mesh;

        public TerrainChunk(Transform parent, Material material)
        {
            gameObject = new GameObject("Terrain Chunk");
            gameObject.transform.SetParent(parent, false);

            meshFilter = gameObject.AddComponent<MeshFilter>();
            meshRenderer = gameObject.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = material;

            mesh = new Mesh
            {
                name = "Steppe Terrain Chunk",
                hideFlags = HideFlags.DontSave
            };
            mesh.MarkDynamic();
            meshFilter.sharedMesh = mesh;
        }

        public ChunkCoordinate Coordinate { get; private set; }
        public int Lod { get; private set; }

        public void Apply(
            ChunkCoordinate coordinate,
            int lod,
            TerrainMeshData data,
            float chunkSize,
            FloatingOriginSystem floatingOrigin)
        {
            Coordinate = coordinate;
            Lod = lod;

            gameObject.name = $"Terrain {coordinate.X}, {coordinate.Z} [LOD {lod}]";
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
            mesh.uv = data.Uvs;
            mesh.triangles = data.Triangles;
            mesh.RecalculateBounds();

            meshRenderer.shadowCastingMode = lod == 0
                ? ShadowCastingMode.On
                : ShadowCastingMode.Off;
            meshRenderer.receiveShadows = true;
            gameObject.SetActive(true);
        }

        public void Deactivate()
        {
            gameObject.SetActive(false);
        }

        public void Dispose()
        {
            if (Application.isPlaying)
            {
                Object.Destroy(mesh);
                Object.Destroy(gameObject);
            }
            else
            {
                Object.DestroyImmediate(mesh);
                Object.DestroyImmediate(gameObject);
            }
        }
    }
}
