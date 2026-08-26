using System;
using System.Collections.Generic;
using Steppe.Settings;
using Steppe.World;
using UnityEngine;
using UnityEngine.Rendering;

namespace Steppe.Rendering
{
    /// <summary>
    /// Streams a shared, deterministic near-field candidate lattice and submits
    /// independent semantic geometry populations. Simulation state is sampled in the
    /// vertex shader, so macro snapshots blend continuously and never rebuild buffers.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SteppeGroundDetailRenderer : MonoBehaviour, IWorldWorkSource
    {
        private const int InstanceStride = 32;
        private const float FullDensityRadius = 58f;
        private const float DrawRadius = 112f;
        private static readonly int InstancesId =
            Shader.PropertyToID("_SteppeGroundDetailInstances");
        private static readonly int CellOriginId =
            Shader.PropertyToID("_SteppeGroundDetailCellOrigin");
        private static readonly int KindId = Shader.PropertyToID("_DetailKind");
        private static readonly int ColorId = Shader.PropertyToID("_DetailColor");
        private static readonly int ScaleId = Shader.PropertyToID("_DetailScale");
        private static readonly int FullDensityRadiusId =
            Shader.PropertyToID("_DetailFullDensityRadius");
        private static readonly int DrawRadiusId = Shader.PropertyToID("_DetailDrawRadius");

        private readonly Dictionary<ChunkCoordinate, DetailCell> loaded =
            new Dictionary<ChunkCoordinate, DetailCell>();
        private readonly HashSet<ChunkCoordinate> desired = new HashSet<ChunkCoordinate>();
        private readonly List<BuildRequest> pending = new List<BuildRequest>();

        private SteppeWorldSettings settings;
        private FloatingOriginSystem floatingOrigin;
        private Transform focus;
        private WorldWorkScheduler workScheduler;
        private GroundDetailCellDataBuilder cellDataBuilder;
        private Material material;
        private Mesh[] meshes;
        private GraphicsBuffer[] arguments;
        private MaterialPropertyBlock propertyBlock;
        private bool hasCenter;
        private bool subscribedToCameraRendering;
        private ChunkCoordinate center;

        public bool IsRendering { get; private set; }
        public int LoadedCellCount => loaded.Count;
        public int PendingCount => pending.Count;
        public int CandidateCount { get; private set; }
        public int DetailKindCount => NaturalGroundDetailCatalog.Descriptors.Count;
        public bool UsesSemanticField => true;
        public bool HasPendingWorldWork => IsRendering && pending.Count > 0;

        public void Configure(
            SteppeWorldSettings worldSettings,
            FloatingOriginSystem origin,
            Transform focusTransform,
            WorldWorkScheduler scheduler)
        {
            settings = worldSettings != null
                ? worldSettings
                : throw new ArgumentNullException(nameof(worldSettings));
            floatingOrigin = origin != null
                ? origin
                : throw new ArgumentNullException(nameof(origin));
            focus = focusTransform != null
                ? focusTransform
                : throw new ArgumentNullException(nameof(focusTransform));
            workScheduler = scheduler != null
                ? scheduler
                : throw new ArgumentNullException(nameof(scheduler));

            if (!SteppeGrassRenderer.HardwareSupported)
            {
                IsRendering = false;
                enabled = false;
                return;
            }

            cellDataBuilder = new GroundDetailCellDataBuilder(settings);
            propertyBlock = new MaterialPropertyBlock();
            material = CreateRuntimeMaterial();
            material.SetFloat(FullDensityRadiusId, FullDensityRadius);
            material.SetFloat(DrawRadiusId, DrawRadius);
            BuildMeshesAndArguments();
            IsRendering = true;
            hasCenter = false;
            workScheduler.Register(this);
            SubscribeToCameraRendering();
        }

        private void BuildMeshesAndArguments()
        {
            var descriptors = NaturalGroundDetailCatalog.Descriptors;
            meshes = new Mesh[descriptors.Count];
            arguments = new GraphicsBuffer[descriptors.Count];
            for (var index = 0; index < descriptors.Count; index++)
            {
                var descriptor = descriptors[index];
                var mesh = GroundDetailMeshBuilder.Build(descriptor.Kind);
                meshes[index] = mesh;
                var buffer = new GraphicsBuffer(
                    GraphicsBuffer.Target.IndirectArguments,
                    1,
                    GraphicsBuffer.IndirectDrawIndexedArgs.size);
                buffer.SetData(new[]
                {
                    new GraphicsBuffer.IndirectDrawIndexedArgs
                    {
                        indexCountPerInstance = mesh.GetIndexCount(0),
                        instanceCount = (uint)cellDataBuilder.CandidateCount,
                        startIndex = mesh.GetIndexStart(0),
                        baseVertexIndex = (uint)mesh.GetBaseVertex(0),
                        startInstance = 0
                    }
                });
                arguments[index] = buffer;
            }
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

        private void SubscribeToCameraRendering()
        {
            if (subscribedToCameraRendering)
            {
                return;
            }

            RenderPipelineManager.beginCameraRendering += RenderForCamera;
            subscribedToCameraRendering = true;
        }

        private void RenderForCamera(ScriptableRenderContext context, Camera renderCamera)
        {
            if (!IsRendering
                || renderCamera == null
                || material == null
                || meshes == null
                || arguments == null)
            {
                return;
            }

            var verticalExtent = settings.MacroAmplitude
                                 + settings.MesoAmplitude
                                 + settings.MicroAmplitude
                                 + 24f;
            foreach (var cell in loaded.Values)
            {
                var worldOriginX = cell.Coordinate.X * (double)settings.GrassCellSize;
                var worldOriginZ = cell.Coordinate.Z * (double)settings.GrassCellSize;
                var localOrigin = floatingOrigin.WorldToLocal(worldOriginX, 0.0, worldOriginZ);
                var localCenter = localOrigin + new Vector3(
                    settings.GrassCellSize * 0.5f,
                    settings.BaseHeight,
                    settings.GrassCellSize * 0.5f);
                var bounds = new Bounds(
                    localCenter,
                    new Vector3(
                        settings.GrassCellSize + 3f,
                        verticalExtent * 2f,
                        settings.GrassCellSize + 3f));

                for (var index = 0; index < meshes.Length; index++)
                {
                    var descriptor = NaturalGroundDetailCatalog.Descriptors[index];
                    propertyBlock.Clear();
                    propertyBlock.SetBuffer(InstancesId, cell.Instances);
                    propertyBlock.SetVector(
                        CellOriginId,
                        new Vector4(localOrigin.x, 0f, localOrigin.z, 0f));
                    propertyBlock.SetFloat(KindId, (float)descriptor.Kind);
                    propertyBlock.SetColor(ColorId, descriptor.FixedMaterialColor);
                    propertyBlock.SetFloat(ScaleId, descriptor.Scale);

                    var renderParams = new RenderParams(material)
                    {
                        worldBounds = bounds,
                        matProps = propertyBlock,
                        receiveShadows = true,
                        shadowCastingMode = ShadowCastingMode.Off,
                        layer = gameObject.layer,
                        camera = renderCamera
                    };
                    Graphics.RenderMeshIndirect(
                        renderParams,
                        meshes[index],
                        arguments[index]);
                }
            }
        }

        private void RefreshDesiredCells(WorldPosition focusWorld)
        {
            desired.Clear();
            pending.Clear();
            var cellSize = settings.GrassCellSize;
            var radiusInCells = Mathf.CeilToInt(DrawRadius / cellSize) + 1;
            var inclusionRadius = DrawRadius + cellSize * 0.72f;
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
                CandidateCount -= loaded[coordinate].InstanceCount;
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
            var cell = new DetailCell(request.Coordinate, data);
            loaded.Add(request.Coordinate, cell);
            CandidateCount += cell.InstanceCount;
        }

        private static Material CreateRuntimeMaterial()
        {
            var shader = Shader.Find("Steppe/Ground Semantic Details");
            if (shader == null)
            {
                throw new InvalidOperationException(
                    "Steppe/Ground Semantic Details shader was not found.");
            }

            return new Material(shader)
            {
                name = "Steppe Semantic Ground Detail Material",
                hideFlags = HideFlags.DontSave,
                enableInstancing = true
            };
        }

        private void OnDestroy()
        {
            if (subscribedToCameraRendering)
            {
                RenderPipelineManager.beginCameraRendering -= RenderForCamera;
                subscribedToCameraRendering = false;
            }

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
            CandidateCount = 0;

            if (arguments != null)
            {
                for (var index = 0; index < arguments.Length; index++)
                {
                    arguments[index]?.Release();
                }
            }

            if (meshes != null)
            {
                for (var index = 0; index < meshes.Length; index++)
                {
                    DestroyOwned(meshes[index]);
                }
            }
            DestroyOwned(material);
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

        private sealed class DetailCell : IDisposable
        {
            public DetailCell(ChunkCoordinate coordinate, GroundDetailInstanceData[] data)
            {
                Coordinate = coordinate;
                InstanceCount = data.Length;
                Instances = new GraphicsBuffer(
                    GraphicsBuffer.Target.Structured,
                    InstanceCount,
                    InstanceStride);
                Instances.SetData(data);
            }

            public ChunkCoordinate Coordinate { get; }
            public int InstanceCount { get; }
            public GraphicsBuffer Instances { get; }

            public void Dispose() => Instances?.Release();
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
