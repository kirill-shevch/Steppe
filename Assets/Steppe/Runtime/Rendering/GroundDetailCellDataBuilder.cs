using System;
using System.Runtime.InteropServices;
using Steppe.Settings;
using Steppe.Terrain;
using Steppe.World;
using UnityEngine;

namespace Steppe.Rendering
{
    [StructLayout(LayoutKind.Sequential)]
    public struct GroundDetailInstanceData
    {
        public Vector4 PositionRotation;
        public Vector4 NormalRandom;
    }

    /// <summary>
    /// Builds one deterministic candidate lattice shared by every semantic detail type.
    /// The shader independently accepts candidates for each state, so populations can
    /// change smoothly without rebuilding this stationary ground geometry.
    /// </summary>
    public sealed class GroundDetailCellDataBuilder
    {
        private readonly SteppeWorldSettings settings;
        private readonly TerrainHeightGenerator terrainGenerator;
        private readonly int axisCount;
        private readonly float spacing;

        public GroundDetailCellDataBuilder(SteppeWorldSettings worldSettings)
        {
            settings = worldSettings != null
                ? worldSettings
                : throw new ArgumentNullException(nameof(worldSettings));
            terrainGenerator = new TerrainHeightGenerator(settings);
            axisCount = Mathf.Max(
                1,
                Mathf.CeilToInt(settings.GrassCellSize / (settings.GrassCandidateSpacing * 2f)));
            spacing = settings.GrassCellSize / axisCount;
        }

        public int CandidateCount => axisCount * axisCount;

        public GroundDetailInstanceData[] Build(ChunkCoordinate coordinate)
        {
            var data = new GroundDetailInstanceData[CandidateCount];
            var worldOriginX = coordinate.X * (double)settings.GrassCellSize;
            var worldOriginZ = coordinate.Z * (double)settings.GrassCellSize;
            var seed = unchecked(settings.WorldSeed + settings.SurfaceVersion * 130363 + 99173);
            var dataIndex = 0;

            for (var z = 0; z < axisCount; z++)
            {
                for (var x = 0; x < axisCount; x++)
                {
                    var hash = DeterministicNoise.Hash(
                        coordinate.X * axisCount + x,
                        coordinate.Z * axisCount + z,
                        seed);
                    var localX = (x + 0.12 + Hash01(hash) * 0.76) * spacing;
                    var localZ = (z + 0.12 + Hash01(Rotate(hash, 19)) * 0.76) * spacing;
                    var worldX = worldOriginX + localX;
                    var worldZ = worldOriginZ + localZ;
                    var height = terrainGenerator.SampleHeight(worldX, worldZ);
                    var normal = terrainGenerator.SampleNormal(worldX, worldZ, 1.2);
                    var angle = (float)(Hash01(Rotate(hash, 37)) * Math.PI * 2.0);

                    data[dataIndex++] = new GroundDetailInstanceData
                    {
                        PositionRotation = new Vector4(
                            (float)localX,
                            (float)height + 0.018f,
                            (float)localZ,
                            angle),
                        NormalRandom = new Vector4(
                            normal.x,
                            normal.y,
                            normal.z,
                            (float)Hash01(Rotate(hash, 53)))
                    };
                }
            }

            return data;
        }

        private static double Hash01(ulong hash) =>
            (hash >> 11) * (1.0 / 9007199254740992.0);

        private static ulong Rotate(ulong value, int count) =>
            (value << count) | (value >> (64 - count));
    }
}
