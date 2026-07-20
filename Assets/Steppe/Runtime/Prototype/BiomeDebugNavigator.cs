using System;
using Steppe.Player;
using Steppe.Settings;
using Steppe.Surface;
using Steppe.Terrain;
using Steppe.World;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Steppe.Prototype
{
    public sealed class BiomeDebugNavigator : MonoBehaviour
    {
        private const double SearchStep = 4096.0;
        private const int MaximumSearchRing = 64;
        private const double RepresentativeWeight = 0.6;

        private readonly bool[] hasBookmark = new bool[4];
        private readonly WorldPosition[] bookmarks = new WorldPosition[4];

        private SteppeWorldSettings settings;
        private FloatingOriginSystem floatingOrigin;
        private Transform focus;
        private SteppeBallController ballController;
        private TerrainHeightGenerator terrainGenerator;
        private SteppeSurfaceGenerator surfaceGenerator;

        public void Configure(
            SteppeWorldSettings worldSettings,
            FloatingOriginSystem origin,
            Transform focusTransform,
            SteppeBallController playerBall = null)
        {
            settings = worldSettings;
            floatingOrigin = origin;
            focus = focusTransform;
            ballController = playerBall;
            terrainGenerator = new TerrainHeightGenerator(settings);
            surfaceGenerator = new SteppeSurfaceGenerator(settings);
        }

        private void Update()
        {
            if (Keyboard.current == null || settings == null || floatingOrigin == null || focus == null)
            {
                return;
            }

            if (Keyboard.current.digit1Key.wasPressedThisFrame)
            {
                TeleportTo(SteppeBiome.Meadow);
            }
            else if (Keyboard.current.digit2Key.wasPressedThisFrame)
            {
                TeleportTo(SteppeBiome.FeatherGrass);
            }
            else if (Keyboard.current.digit3Key.wasPressedThisFrame)
            {
                TeleportTo(SteppeBiome.Dry);
            }
            else if (Keyboard.current.digit4Key.wasPressedThisFrame)
            {
                TeleportTo(SteppeBiome.Desert);
            }
        }

        private void TeleportTo(SteppeBiome biome)
        {
            var index = (int)biome;
            if (!hasBookmark[index] && !TryFindRepresentative(biome, out bookmarks[index]))
            {
                Debug.LogWarning($"No representative {biome} biome was found inside the debug search radius.");
                return;
            }

            hasBookmark[index] = true;
            var target = bookmarks[index];
            var localTarget = floatingOrigin.WorldToLocal(target.X, target.Y, target.Z);
            if (ballController != null)
            {
                ballController.Teleport(localTarget);
            }
            else
            {
                focus.position = localTarget;
            }
        }

        private bool TryFindRepresentative(SteppeBiome targetBiome, out WorldPosition target)
        {
            for (var ring = 0; ring <= MaximumSearchRing; ring++)
            {
                if (ring == 0 && TryCandidate(0.0, 0.0, targetBiome, out target))
                {
                    return true;
                }

                for (var offset = -ring; offset <= ring; offset++)
                {
                    if (TryCandidate(offset * SearchStep, ring * SearchStep, targetBiome, out target)
                        || TryCandidate(offset * SearchStep, -ring * SearchStep, targetBiome, out target))
                    {
                        return true;
                    }
                }

                for (var offset = -ring + 1; offset < ring; offset++)
                {
                    if (TryCandidate(ring * SearchStep, offset * SearchStep, targetBiome, out target)
                        || TryCandidate(-ring * SearchStep, offset * SearchStep, targetBiome, out target))
                    {
                        return true;
                    }
                }
            }

            target = default;
            return false;
        }

        private bool TryCandidate(double worldX, double worldZ, SteppeBiome targetBiome, out WorldPosition target)
        {
            // Search points are deliberately offset from exact 512 m chunk seams so
            // a teleported physical sphere cannot balance between two mesh colliders.
            var candidateX = worldX + settings.ChunkSize * 0.37;
            var candidateZ = worldZ + settings.ChunkSize * 0.23;
            var height = terrainGenerator.SampleHeight(candidateX, candidateZ);
            var normal = terrainGenerator.SampleNormal(candidateX, candidateZ, 2.0);
            var surface = surfaceGenerator.Sample(candidateX, candidateZ, height, normal.y);
            if (surface.DominantBiome == targetBiome
                && GetWeight(surface.Biomes, targetBiome) >= RepresentativeWeight)
            {
                target = new WorldPosition(
                    candidateX,
                    height + settings.PlayerBallRadius + 2.0,
                    candidateZ);
                return true;
            }

            target = default;
            return false;
        }

        private static double GetWeight(BiomeWeights weights, SteppeBiome biome)
        {
            return biome switch
            {
                SteppeBiome.Meadow => weights.Meadow,
                SteppeBiome.FeatherGrass => weights.FeatherGrass,
                SteppeBiome.Dry => weights.Dry,
                SteppeBiome.Desert => weights.Desert,
                _ => throw new ArgumentOutOfRangeException(nameof(biome), biome, null)
            };
        }
    }
}
