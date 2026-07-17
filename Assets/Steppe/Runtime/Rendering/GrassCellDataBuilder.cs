using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Steppe.Settings;
using Steppe.Surface;
using Steppe.Terrain;
using Steppe.World;
using UnityEngine;

namespace Steppe.Rendering
{
    [StructLayout(LayoutKind.Sequential)]
    public struct GrassInstanceData
    {
        public Vector4 PositionHeight;
        public Vector4 ColorWidth;
        public Vector4 Parameters;
        public Vector4 Motion;
    }

    /// <summary>
    /// Pure, deterministic CPU construction of one canonical grass cell. It owns no
    /// GameObjects, materials, or GPU buffers and can later move to a worker/job backend.
    /// </summary>
    public sealed class GrassCellDataBuilder
    {
        private readonly SteppeWorldSettings settings;
        private readonly TerrainHeightGenerator terrainGenerator;
        private readonly SteppeSurfaceGenerator surfaceGenerator;

        public GrassCellDataBuilder(SteppeWorldSettings worldSettings)
        {
            settings = worldSettings != null ? worldSettings : throw new ArgumentNullException(nameof(worldSettings));
            terrainGenerator = new TerrainHeightGenerator(settings);
            surfaceGenerator = new SteppeSurfaceGenerator(settings);
        }

        public GrassInstanceData[] Build(ChunkCoordinate coordinate)
        {
            var cellSize = settings.GrassCellSize;
            var axisCount = Mathf.Max(1, Mathf.CeilToInt(cellSize / settings.GrassCandidateSpacing));
            var spacing = cellSize / axisCount;
            var instances = new List<GrassInstanceData>(axisCount * axisCount);
            var worldOriginX = coordinate.X * (double)cellSize;
            var worldOriginZ = coordinate.Z * (double)cellSize;
            var seed = unchecked(settings.WorldSeed + settings.SurfaceVersion * 104729 + 47111);

            for (var z = 0; z < axisCount; z++)
            {
                for (var x = 0; x < axisCount; x++)
                {
                    var hash = DeterministicNoise.Hash(
                        coordinate.X * axisCount + x,
                        coordinate.Z * axisCount + z,
                        seed);
                    var localX = (x + 0.1 + Hash01(hash) * 0.8) * spacing;
                    var localZ = (z + 0.1 + Hash01(Rotate(hash, 17)) * 0.8) * spacing;
                    var worldX = worldOriginX + localX;
                    var worldZ = worldOriginZ + localZ;
                    var height = terrainGenerator.SampleHeight(worldX, worldZ);
                    var normal = terrainGenerator.SampleNormal(worldX, worldZ, 2.5);
                    var surface = surfaceGenerator.Sample(worldX, worldZ, height, normal.y);

                    var presence = Hash01(Rotate(hash, 39));
                    if (presence > surface.VegetationPotential * 0.94)
                    {
                        continue;
                    }

                    var scaleVariation = 0.78 + Hash01(Rotate(hash, 7)) * 0.44;
                    var tuftHeight = (float)(settings.GrassTuftHeight
                                              * surface.NominalVegetationHeight
                                              * scaleVariation);
                    if (tuftHeight < 0.09f)
                    {
                        continue;
                    }

                    var tuftWidth = settings.GrassTuftWidth
                                    * (0.78f + (float)Hash01(Rotate(hash, 27)) * 0.36f);
                    var angle = (float)(Hash01(Rotate(hash, 51)) * Math.PI * 2.0);
                    var color = (Color)surface.VegetationColor;
                    color *= 0.86f + (float)Hash01(Rotate(hash, 13)) * 0.22f;

                    instances.Add(new GrassInstanceData
                    {
                        PositionHeight = new Vector4((float)localX, (float)height + 0.015f, (float)localZ, tuftHeight),
                        ColorWidth = new Vector4(color.r, color.g, color.b, tuftWidth),
                        Parameters = new Vector4(
                            Mathf.Sin(angle),
                            Mathf.Cos(angle),
                            (float)surface.WindCoherence,
                            (float)Hash01(Rotate(hash, 33))),
                        Motion = new Vector4(
                            (float)surface.MotionFrequency,
                            (float)Hash01(Rotate(hash, 45)),
                            0f,
                            0f)
                    });
                }
            }

            return instances.ToArray();
        }

        private static double Hash01(ulong hash)
        {
            return (hash >> 11) * (1.0 / 9007199254740992.0);
        }

        private static ulong Rotate(ulong value, int count)
        {
            return (value << count) | (value >> (64 - count));
        }
    }
}
