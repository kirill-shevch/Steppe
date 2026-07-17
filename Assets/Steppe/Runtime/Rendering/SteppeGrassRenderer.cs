using System;
using System.Collections.Generic;
using Steppe.Settings;
using Steppe.Terrain;
using Steppe.World;
using UnityEngine;
using UnityEngine.Rendering;

namespace Steppe.Rendering
{
    /// <summary>
    /// P4 grass is streamed in small canonical cells and drawn without one GameObject per
    /// plant. Placement remains stationary, while the shared vertex shader bends the mesh
    /// against the canonical P3 wind field without rebuilding buffers on the CPU.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SteppeGrassRenderer : MonoBehaviour, IWorldWorkSource
    {
        private const int InstanceStride = 64;
        private const string AuthoredModelResource =
            "Vegetation/NobiaxMultiStylizedGrass/SteppeFeatherGrass";
        private const string AuthoredTextureResource =
            "Vegetation/NobiaxMultiStylizedGrass/NobiaxGrassAtlas";
        private const float AuthoredModelScale = 1f / 32f;
        private static readonly int InstancesId = Shader.PropertyToID("_SteppeGrassInstances");
        private static readonly int CellOriginId = Shader.PropertyToID("_SteppeGrassCellOrigin");
        private static readonly int FullDensityRadiusId = Shader.PropertyToID("_GrassFullDensityRadius");
        private static readonly int DrawRadiusId = Shader.PropertyToID("_GrassDrawRadius");
        private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        private static readonly int AssetScaleId = Shader.PropertyToID("_GrassAssetScale");
        private static readonly int AlphaCutoffId = Shader.PropertyToID("_AlphaCutoff");
        private static readonly int WindBendStrengthId = Shader.PropertyToID("_WindBendStrength");

        private readonly Dictionary<ChunkCoordinate, GrassCell> loaded = new Dictionary<ChunkCoordinate, GrassCell>();
        private readonly HashSet<ChunkCoordinate> desired = new HashSet<ChunkCoordinate>();
        private readonly List<BuildRequest> pending = new List<BuildRequest>();
        // Unity can restore MonoBehaviours without running field initializers during some
        // editor/test domain reload paths. Keep this transient rendering object lazy.
        private MaterialPropertyBlock propertyBlock;

        private SteppeWorldSettings settings;
        private FloatingOriginSystem floatingOrigin;
        private Transform focus;
        private WorldWorkScheduler workScheduler;
        private GrassCellDataBuilder cellDataBuilder;
        private Mesh tuftMesh;
        private Material grassMaterial;
        private bool ownsMesh;
        private bool ownsMaterial;
        private bool hasCenter;
        private ChunkCoordinate center;

        public static bool HardwareSupported =>
            SystemInfo.supportsComputeShaders
            && SystemInfo.supportsInstancing
            && SystemInfo.supportsIndirectArgumentsBuffer;

        public bool IsRendering { get; private set; }
        public int LoadedCellCount => loaded.Count;
        public int PendingCount => pending.Count;
        public int InstanceCount { get; private set; }
        public bool UsesAuthoredMesh { get; private set; }
        public int TuftVertexCount => tuftMesh != null ? tuftMesh.vertexCount : 0;
        public int TuftTriangleCount => tuftMesh != null ? (int)tuftMesh.GetIndexCount(0) / 3 : 0;
        public bool HasPendingWorldWork => IsRendering && pending.Count > 0;

        public void Configure(
            SteppeWorldSettings worldSettings,
            FloatingOriginSystem origin,
            Transform focusTransform,
            WorldWorkScheduler scheduler,
            Material material = null)
        {
            settings = worldSettings != null ? worldSettings : throw new ArgumentNullException(nameof(worldSettings));
            floatingOrigin = origin != null ? origin : throw new ArgumentNullException(nameof(origin));
            focus = focusTransform != null ? focusTransform : throw new ArgumentNullException(nameof(focusTransform));
            workScheduler = scheduler != null ? scheduler : throw new ArgumentNullException(nameof(scheduler));
            cellDataBuilder = new GrassCellDataBuilder(settings);
            propertyBlock ??= new MaterialPropertyBlock();

            if (!HardwareSupported)
            {
                IsRendering = false;
                enabled = false;
                return;
            }

            var authoredTexture = LoadAuthoredGrass(out var authoredMesh);
            if (authoredMesh != null && authoredTexture != null)
            {
                tuftMesh = GrassWindMeshBuilder.Build(authoredMesh);
                if (tuftMesh == null)
                {
                    tuftMesh = authoredMesh;
                    ownsMesh = false;
                }
                else
                {
                    ownsMesh = true;
                }
                UsesAuthoredMesh = true;
            }
            else
            {
                tuftMesh = GrassTuftMeshBuilder.Build();
                authoredTexture = Texture2D.whiteTexture;
                ownsMesh = true;
                UsesAuthoredMesh = false;
            }

            if (material != null && material.shader != null && material.shader.name == "Steppe/Grass Indirect")
            {
                grassMaterial = material;
                ownsMaterial = false;
            }
            else
            {
                grassMaterial = CreateRuntimeMaterial();
                ownsMaterial = true;
            }

            grassMaterial.SetFloat(FullDensityRadiusId, settings.GrassFullDensityRadius);
            grassMaterial.SetFloat(DrawRadiusId, settings.GrassDrawRadius);
            grassMaterial.SetTexture(BaseMapId, authoredTexture);
            grassMaterial.SetFloat(AssetScaleId, UsesAuthoredMesh ? AuthoredModelScale : 1f);
            grassMaterial.SetFloat(AlphaCutoffId, UsesAuthoredMesh ? 0.36f : 0f);
            grassMaterial.SetFloat(WindBendStrengthId, settings.GrassWindBend);
            hasCenter = false;
            IsRendering = true;
            workScheduler.Register(this);
        }

        private static Texture2D LoadAuthoredGrass(out Mesh mesh)
        {
            mesh = null;
            var model = Resources.Load<GameObject>(AuthoredModelResource);
            var texture = Resources.Load<Texture2D>(AuthoredTextureResource);
            if (model == null || texture == null)
            {
                return null;
            }

            var meshFilter = model.GetComponentInChildren<MeshFilter>(true);
            mesh = meshFilter != null ? meshFilter.sharedMesh : null;
            return mesh != null ? texture : null;
        }

        private void Update()
        {
            if (!IsRendering || focus == null)
            {
                return;
            }

            var focusWorld = floatingOrigin.LocalToWorld(focus.position);
            var currentCenter = ChunkCoordinate.FromWorld(
                focusWorld.X,
                focusWorld.Z,
                settings.GrassCellSize);
            if (!hasCenter || currentCenter != center)
            {
                center = currentCenter;
                hasCenter = true;
                RefreshDesiredCells(focusWorld);
            }
        }

        private void LateUpdate()
        {
            if (!IsRendering || grassMaterial == null || tuftMesh == null)
            {
                return;
            }

            propertyBlock ??= new MaterialPropertyBlock();

            var verticalExtent = settings.MacroAmplitude
                                 + settings.MesoAmplitude
                                 + settings.MicroAmplitude
                                 + settings.GrassTuftHeight * 2f
                                 + 24f;
            var horizontalWindMargin = settings.GrassTuftHeight * settings.GrassWindBend + 1f;
            foreach (var cell in loaded.Values)
            {
                if (cell.InstanceCount == 0)
                {
                    continue;
                }

                var worldOriginX = cell.Coordinate.X * (double)settings.GrassCellSize;
                var worldOriginZ = cell.Coordinate.Z * (double)settings.GrassCellSize;
                var localOrigin = floatingOrigin.WorldToLocal(worldOriginX, 0.0, worldOriginZ);
                var localCenter = localOrigin + new Vector3(
                    settings.GrassCellSize * 0.5f,
                    settings.BaseHeight,
                    settings.GrassCellSize * 0.5f);

                propertyBlock.Clear();
                propertyBlock.SetBuffer(InstancesId, cell.Instances);
                propertyBlock.SetVector(CellOriginId, new Vector4(localOrigin.x, 0f, localOrigin.z, 0f));

                var renderParams = new RenderParams(grassMaterial)
                {
                    worldBounds = new Bounds(
                        localCenter,
                        new Vector3(
                            settings.GrassCellSize + horizontalWindMargin * 2f,
                            verticalExtent * 2f,
                            settings.GrassCellSize + horizontalWindMargin * 2f)),
                    matProps = propertyBlock,
                    receiveShadows = true,
                    shadowCastingMode = ShadowCastingMode.Off,
                    layer = gameObject.layer
                };
                Graphics.RenderMeshIndirect(renderParams, tuftMesh, cell.Arguments);
            }
        }

        private void RefreshDesiredCells(WorldPosition focusWorld)
        {
            desired.Clear();
            pending.Clear();
            var cellSize = settings.GrassCellSize;
            var radiusInCells = Mathf.CeilToInt(settings.GrassDrawRadius / cellSize) + 1;
            var inclusionRadius = settings.GrassDrawRadius + cellSize * 0.72f;
            var inclusionRadiusSquared = inclusionRadius * inclusionRadius;

            for (var z = -radiusInCells; z <= radiusInCells; z++)
            {
                for (var x = -radiusInCells; x <= radiusInCells; x++)
                {
                    var coordinate = center.Offset(x, z);
                    var cellCenterX = (coordinate.X + 0.5) * cellSize;
                    var cellCenterZ = (coordinate.Z + 0.5) * cellSize;
                    var deltaX = cellCenterX - focusWorld.X;
                    var deltaZ = cellCenterZ - focusWorld.Z;
                    var distanceSquared = deltaX * deltaX + deltaZ * deltaZ;
                    if (distanceSquared > inclusionRadiusSquared)
                    {
                        continue;
                    }

                    desired.Add(coordinate);
                    if (!loaded.ContainsKey(coordinate))
                    {
                        pending.Add(new BuildRequest(coordinate, distanceSquared));
                    }
                }
            }

            var removals = new List<ChunkCoordinate>();
            foreach (var pair in loaded)
            {
                if (!desired.Contains(pair.Key))
                {
                    removals.Add(pair.Key);
                }
            }

            for (var index = 0; index < removals.Count; index++)
            {
                var coordinate = removals[index];
                InstanceCount -= loaded[coordinate].InstanceCount;
                loaded[coordinate].Dispose();
                loaded.Remove(coordinate);
            }

            pending.Sort((left, right) => left.DistanceSquared.CompareTo(right.DistanceSquared));
        }

        public void ExecuteWorldWorkStep()
        {
            if (pending.Count == 0)
            {
                return;
            }

            var request = pending[0];
            pending.RemoveAt(0);
            if (!desired.Contains(request.Coordinate) || loaded.ContainsKey(request.Coordinate))
            {
                return;
            }

            var data = cellDataBuilder.Build(request.Coordinate);
            var cell = new GrassCell(request.Coordinate, tuftMesh, data);
            loaded.Add(request.Coordinate, cell);
            InstanceCount += cell.InstanceCount;
        }

        private static Material CreateRuntimeMaterial()
        {
            var shader = Shader.Find("Steppe/Grass Indirect");
            if (shader == null)
            {
                throw new InvalidOperationException("Steppe/Grass Indirect shader was not found.");
            }

            return new Material(shader)
            {
                name = "Steppe P4 Grass Material",
                hideFlags = HideFlags.DontSave,
                enableInstancing = true
            };
        }

        private void OnDestroy()
        {
            if (workScheduler != null)
            {
                workScheduler.Unregister(this);
            }
            foreach (var cell in loaded.Values)
            {
                cell.Dispose();
            }
            loaded.Clear();
            pending.Clear();
            desired.Clear();
            InstanceCount = 0;

            if (ownsMesh)
            {
                DestroyOwned(tuftMesh);
            }
            if (ownsMaterial)
            {
                DestroyOwned(grassMaterial);
            }
        }

        private static void DestroyOwned(UnityEngine.Object value)
        {
            if (value == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(value);
            }
            else
            {
                DestroyImmediate(value);
            }
        }

        private sealed class GrassCell : IDisposable
        {
            public GrassCell(ChunkCoordinate coordinate, Mesh mesh, GrassInstanceData[] data)
            {
                Coordinate = coordinate;
                InstanceCount = data.Length;
                if (InstanceCount == 0)
                {
                    return;
                }

                Instances = new GraphicsBuffer(GraphicsBuffer.Target.Structured, InstanceCount, InstanceStride);
                Instances.SetData(data);
                Arguments = new GraphicsBuffer(
                    GraphicsBuffer.Target.IndirectArguments,
                    1,
                    GraphicsBuffer.IndirectDrawIndexedArgs.size);
                var arguments = new[]
                {
                    new GraphicsBuffer.IndirectDrawIndexedArgs
                    {
                        indexCountPerInstance = mesh.GetIndexCount(0),
                        instanceCount = (uint)InstanceCount,
                        startIndex = mesh.GetIndexStart(0),
                        baseVertexIndex = (uint)mesh.GetBaseVertex(0),
                        startInstance = 0
                    }
                };
                Arguments.SetData(arguments);
            }

            public ChunkCoordinate Coordinate { get; }
            public int InstanceCount { get; }
            public GraphicsBuffer Instances { get; }
            public GraphicsBuffer Arguments { get; }

            public void Dispose()
            {
                Instances?.Release();
                Arguments?.Release();
            }
        }

        private readonly struct BuildRequest
        {
            public BuildRequest(ChunkCoordinate coordinate, double distanceSquared)
            {
                Coordinate = coordinate;
                DistanceSquared = distanceSquared;
            }

            public ChunkCoordinate Coordinate { get; }
            public double DistanceSquared { get; }
        }
    }
}
