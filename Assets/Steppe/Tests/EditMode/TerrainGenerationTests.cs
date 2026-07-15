using System;
using NUnit.Framework;
using Steppe.Settings;
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
