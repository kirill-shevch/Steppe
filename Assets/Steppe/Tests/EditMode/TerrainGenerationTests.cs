using System;
using NUnit.Framework;
using Steppe.Settings;
using Steppe.Surface;
using Steppe.Terrain;
using Steppe.World;
using UnityEngine;

namespace Steppe.Tests
{
    public sealed class TerrainGenerationTests
    {
        private SteppeWorldSettings settings;

        [SetUp]
        public void SetUp()
        {
            settings = ScriptableObject.CreateInstance<SteppeWorldSettings>();
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(settings);
        }

        [Test]
        public void ChunkCoordinatesFloorAcrossNegativeBoundaries()
        {
            Assert.That(ChunkCoordinate.FromWorld(0.0, 0.0, 512.0), Is.EqualTo(new ChunkCoordinate(0, 0)));
            Assert.That(ChunkCoordinate.FromWorld(511.99, 512.0, 512.0), Is.EqualTo(new ChunkCoordinate(0, 1)));
            Assert.That(ChunkCoordinate.FromWorld(-0.01, -512.0, 512.0), Is.EqualTo(new ChunkCoordinate(-1, -1)));
            Assert.That(ChunkCoordinate.FromWorld(-512.01, -512.01, 512.0), Is.EqualTo(new ChunkCoordinate(-2, -2)));
        }

        [Test]
        public void HeightIsBitwiseDeterministicAtLargeCoordinates()
        {
            var generator = new TerrainHeightGenerator(settings);
            var positions = new[]
            {
                (0.0, 0.0),
                (1234.5, -9876.25),
                (1000000000.125, -1000000000.875),
                (-450000000000.5, 781234567890.25)
            };

            foreach (var position in positions)
            {
                var first = generator.SampleHeight(position.Item1, position.Item2);
                var second = generator.SampleHeight(position.Item1, position.Item2);
                Assert.That(BitConverter.DoubleToInt64Bits(first), Is.EqualTo(BitConverter.DoubleToInt64Bits(second)));
            }
        }

        [Test]
        public void InitialStreamingAreaContainsReadableRelief()
        {
            var generator = new TerrainHeightGenerator(settings);
            const int radius = 4096;
            const int step = 128;
            var minimum = double.MaxValue;
            var maximum = double.MinValue;
            var sum = 0.0;
            var sumOfSquares = 0.0;
            var count = 0;

            for (var z = -radius; z <= radius; z += step)
            {
                for (var x = -radius; x <= radius; x += step)
                {
                    var height = generator.SampleHeight(x, z);
                    minimum = Math.Min(minimum, height);
                    maximum = Math.Max(maximum, height);
                    sum += height;
                    sumOfSquares += height * height;
                    count++;
                }
            }

            var mean = sum / count;
            var standardDeviation = Math.Sqrt(sumOfSquares / count - mean * mean);

            TestContext.WriteLine(
                $"Initial terrain: min={minimum:F1}m max={maximum:F1}m " +
                $"range={maximum - minimum:F1}m sigma={standardDeviation:F1}m");

            Assert.That(maximum - minimum, Is.GreaterThan(120.0),
                "The initial streamed horizon must contain clearly different elevations.");
            Assert.That(standardDeviation, Is.GreaterThan(25.0),
                "Relief must occupy the landscape instead of appearing as one isolated bump.");
        }

        [Test]
        public void SurfaceFieldsAreDeterministicAndContinuous()
        {
            var terrain = new TerrainHeightGenerator(settings);
            var surface = new SteppeSurfaceGenerator(settings);
            const double x = 12345.67;
            const double z = -7654.32;
            var height = terrain.SampleHeight(x, z);
            var normal = terrain.SampleNormal(x, z, 2.0);
            var first = surface.Sample(x, z, height, normal.y);
            var second = surface.Sample(x, z, height, normal.y);
            var neighborHeight = terrain.SampleHeight(x + 1.0, z + 1.0);
            var neighborNormal = terrain.SampleNormal(x + 1.0, z + 1.0, 2.0);
            var neighbor = surface.Sample(x + 1.0, z + 1.0, neighborHeight, neighborNormal.y);

            Assert.That(first.MeanAnnualPrecipitationMm, Is.EqualTo(second.MeanAnnualPrecipitationMm));
            Assert.That(first.MeanAnnualTemperatureC, Is.EqualTo(second.MeanAnnualTemperatureC));
            Assert.That(first.DominantBiome, Is.EqualTo(second.DominantBiome));
            Assert.That(first.ClayContent, Is.EqualTo(second.ClayContent));
            Assert.That(first.Fertility, Is.EqualTo(second.Fertility));
            Assert.That(first.WaterRetention, Is.EqualTo(second.WaterRetention));
            Assert.That(first.MoisturePotential, Is.EqualTo(second.MoisturePotential));
            Assert.That(first.VegetationPotential, Is.EqualTo(second.VegetationPotential));
            Assert.That(first.GroundColor, Is.EqualTo(second.GroundColor));
            Assert.That(Math.Abs(first.VegetationPotential - neighbor.VegetationPotential), Is.LessThan(0.03));
            Assert.That(Math.Abs(first.WaterRetention - neighbor.WaterRetention), Is.LessThan(0.03));
            Assert.That(
                first.Biomes.Meadow + first.Biomes.FeatherGrass + first.Biomes.Dry + first.Biomes.Desert,
                Is.EqualTo(1.0).Within(0.0000001));
        }

        [Test]
        public void CanonicalClimateContainsAllFourSteppeBiomes()
        {
            var surface = new SteppeSurfaceGenerator(settings);
            var found = new bool[4];

            for (var z = -250000; z <= 250000; z += 10000)
            {
                for (var x = -250000; x <= 250000; x += 10000)
                {
                    var sample = surface.Sample(x, z, settings.BaseHeight, 1.0);
                    found[(int)sample.DominantBiome] = true;
                }
            }

            Assert.That(found[(int)SteppeBiome.Meadow], Is.True, "The canonical world needs meadow steppe regions.");
            Assert.That(found[(int)SteppeBiome.FeatherGrass], Is.True, "Feather-grass steppe must be the climate model's central regime.");
            Assert.That(found[(int)SteppeBiome.Dry], Is.True, "The canonical world needs dry steppe regions.");
            Assert.That(found[(int)SteppeBiome.Desert], Is.True, "The canonical world needs desert-steppe regions.");
        }

        [Test]
        public void StartingRegionReachesAnEcotoneWithinThirtyKilometers()
        {
            var terrain = new TerrainHeightGenerator(settings);
            var surface = new SteppeSurfaceGenerator(settings);
            var startHeight = terrain.SampleHeight(0.0, 0.0);
            var startNormal = terrain.SampleNormal(0.0, 0.0, 2.0);
            var startingBiome = surface.Sample(0.0, 0.0, startHeight, startNormal.y).DominantBiome;
            var nearestDistance = double.MaxValue;
            var nearestX = 0.0;
            var nearestZ = 0.0;

            const double radialStep = 2048.0;
            const int directionCount = 48;
            for (var radius = radialStep; radius <= 100000.0; radius += radialStep)
            {
                for (var direction = 0; direction < directionCount; direction++)
                {
                    var angle = direction * Math.PI * 2.0 / directionCount;
                    var x = Math.Cos(angle) * radius;
                    var z = Math.Sin(angle) * radius;
                    var height = terrain.SampleHeight(x, z);
                    var normal = terrain.SampleNormal(x, z, 2.0);
                    var biome = surface.Sample(x, z, height, normal.y).DominantBiome;
                    if (biome == startingBiome)
                    {
                        continue;
                    }

                    nearestDistance = radius;
                    nearestX = x;
                    nearestZ = z;
                    break;
                }

                if (nearestDistance < double.MaxValue)
                {
                    break;
                }
            }

            TestContext.WriteLine(
                $"Nearest biome transition from {startingBiome}: {nearestDistance:F0} m " +
                $"near ({nearestX:F0}, {nearestZ:F0})");
            Assert.That(nearestDistance, Is.LessThanOrEqualTo(30000.0),
                "A normal journey should reach an ecotone without crossing an entire continent.");
        }

        [Test]
        public void TerrainColorsAndVegetationUseTheSameSurfaceField()
        {
            var terrain = new TerrainHeightGenerator(settings);
            var surface = new SteppeSurfaceGenerator(settings);
            var mesh = TerrainMeshBuilder.Build(
                terrain,
                new ChunkCoordinate(0, 0),
                settings.ChunkSize,
                settings.NearResolution,
                settings.SkirtDepth,
                surface);
            var nearVegetation = VegetationMeshBuilder.Build(
                terrain, surface, settings, new ChunkCoordinate(0, 0), 0);
            var reloadedNearVegetation = VegetationMeshBuilder.Build(
                terrain, surface, settings, new ChunkCoordinate(0, 0), 0);
            var middleVegetation = VegetationMeshBuilder.Build(
                terrain, surface, settings, new ChunkCoordinate(0, 0), 1);
            var farVegetation = VegetationMeshBuilder.Build(
                terrain, surface, settings, new ChunkCoordinate(0, 0), 2);

            Assert.That(mesh.Colors.Length, Is.EqualTo(mesh.Vertices.Length));
            Assert.That(nearVegetation.Vertices.Length, Is.GreaterThan(0));
            Assert.That(middleVegetation.Vertices.Length, Is.LessThan(nearVegetation.Vertices.Length));
            Assert.That(farVegetation.IsEmpty, Is.True);
            Assert.That(nearVegetation.Colors.Length, Is.EqualTo(nearVegetation.Vertices.Length));
            CollectionAssert.AreEqual(nearVegetation.Vertices, reloadedNearVegetation.Vertices,
                "Reloading a chunk must reproduce identical vegetation placement.");
            CollectionAssert.AreEqual(nearVegetation.Colors, reloadedNearVegetation.Colors);
        }

        [Test]
        public void AdjacentDifferentLodMeshesShareBoundaryHeights()
        {
            var generator = new TerrainHeightGenerator(settings);
            var left = TerrainMeshBuilder.Build(generator, new ChunkCoordinate(0, 0), 512f, 33, 20f);
            var right = TerrainMeshBuilder.Build(generator, new ChunkCoordinate(1, 0), 512f, 17, 20f);

            for (var rightZ = 0; rightZ < right.TopResolution; rightZ++)
            {
                var leftZ = rightZ * 2;
                var leftHeight = left.GetTopVertex(left.TopResolution - 1, leftZ).y;
                var rightHeight = right.GetTopVertex(0, rightZ).y;
                Assert.That(leftHeight, Is.EqualTo(rightHeight).Within(0.0001f));
            }
        }

        [Test]
        public void OverrideStampChangesOnlyItsAuthoredArea()
        {
            var baseGenerator = new TerrainHeightGenerator(settings);
            var overrideMap = ScriptableObject.CreateInstance<TerrainOverrideMap>();
            overrideMap.AddStamp(new TerrainOverrideStamp(0.0, 0.0, 1000.0, 50.0));
            var overriddenGenerator = new TerrainHeightGenerator(settings, overrideMap);

            var baseCenter = baseGenerator.SampleHeight(0.0, 0.0);
            Assert.That(overriddenGenerator.SampleHeight(0.0, 0.0), Is.EqualTo(baseCenter + 50.0).Within(0.000001));

            var baseOutside = baseGenerator.SampleHeight(2000.0, 0.0);
            Assert.That(overriddenGenerator.SampleHeight(2000.0, 0.0), Is.EqualTo(baseOutside).Within(0.000001));

            UnityEngine.Object.DestroyImmediate(overrideMap);
        }
    }
}
