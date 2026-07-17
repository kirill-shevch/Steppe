using Steppe.World;
using Steppe.Surface;
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
        private readonly GameObject vegetationObject;
        private readonly Mesh vegetationMesh;
        private readonly MeshRenderer vegetationRenderer;

        public TerrainChunk(Transform parent, Material material, Material vegetationMaterial)
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

            vegetationObject = new GameObject("Vegetation");
            vegetationObject.transform.SetParent(gameObject.transform, false);
            var vegetationFilter = vegetationObject.AddComponent<MeshFilter>();
            vegetationRenderer = vegetationObject.AddComponent<MeshRenderer>();
            vegetationRenderer.sharedMaterial = vegetationMaterial;
            vegetationRenderer.shadowCastingMode = ShadowCastingMode.Off;
            vegetationRenderer.receiveShadows = true;
            vegetationMesh = new Mesh
            {
                name = "Steppe Vegetation Chunk",
                hideFlags = HideFlags.DontSave
            };
            vegetationMesh.MarkDynamic();
            vegetationFilter.sharedMesh = vegetationMesh;
        }

        public ChunkCoordinate Coordinate { get; private set; }
        public int Lod { get; private set; }

        public void Apply(
            ChunkCoordinate coordinate,
            int lod,
            TerrainMeshData data,
            VegetationMeshData vegetationData,
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
            mesh.colors32 = data.Colors;
            mesh.triangles = data.Triangles;
            mesh.RecalculateBounds();

            ApplyVegetation(vegetationData);

            meshRenderer.shadowCastingMode = lod == 0
                ? ShadowCastingMode.On
                : ShadowCastingMode.Off;
            meshRenderer.receiveShadows = true;
            gameObject.SetActive(true);
        }

        private void ApplyVegetation(VegetationMeshData data)
        {
            vegetationMesh.Clear();
            if (data == null || data.IsEmpty)
            {
                vegetationObject.SetActive(false);
                return;
            }

            vegetationMesh.indexFormat = data.Vertices.Length > ushort.MaxValue
                ? IndexFormat.UInt32
                : IndexFormat.UInt16;
            vegetationMesh.vertices = data.Vertices;
            vegetationMesh.normals = data.Normals;
            vegetationMesh.colors32 = data.Colors;
            vegetationMesh.triangles = data.Triangles;
            vegetationMesh.RecalculateBounds();
            vegetationObject.SetActive(true);
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
                Object.Destroy(vegetationMesh);
                Object.Destroy(gameObject);
            }
            else
            {
                Object.DestroyImmediate(mesh);
                Object.DestroyImmediate(vegetationMesh);
                Object.DestroyImmediate(gameObject);
            }
        }
    }
}
