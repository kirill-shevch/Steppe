using System.Linq;
using NUnit.Framework;
using Steppe.Rendering;
using Steppe.Simulation;
using UnityEngine;

namespace Steppe.Tests
{
    public sealed class NaturalVisualCatalogTests
    {
        [Test]
        public void EveryStateOwnsOneUniqueNaturalVisualChannel()
        {
            var registeredStates = NaturalVisualCatalog.States
                .Select(descriptor => descriptor.State)
                .ToArray();
            var registeredChannels = NaturalVisualCatalog.States
                .Select(descriptor => descriptor.Channel)
                .ToArray();
            var registeredAddresses = NaturalVisualCatalog.States
                .Select(descriptor => descriptor.Address)
                .ToArray();

            Assert.That(registeredStates, Has.Length.EqualTo(StateCatalog.All.Count));
            Assert.That(registeredStates.Distinct().Count(), Is.EqualTo(registeredStates.Length));
            Assert.That(registeredChannels.Distinct().Count(), Is.EqualTo(registeredChannels.Length));
            Assert.That(registeredAddresses.Distinct().Count(), Is.EqualTo(registeredAddresses.Length));
            foreach (var state in StateCatalog.All.Select(descriptor => descriptor.Id))
            {
                Assert.That(registeredStates, Does.Contain(state), state.ToString());
            }
        }

        [Test]
        public void RootWaterExclusivelyOwnsSoilBaseAlbedo()
        {
            var owners = NaturalVisualCatalog.States
                .Where(descriptor => descriptor.Channel == NaturalVisualChannel.SoilBaseAlbedo)
                .ToArray();

            Assert.That(owners, Has.Length.EqualTo(1));
            Assert.That(owners[0].State, Is.EqualTo(SimulationLayer.RootWater));
            Assert.That(owners[0].Address, Is.EqualTo("soil.material.baseAlbedo"));
        }

        [Test]
        public void SemanticPacksContainEveryStateExactlyOnce()
        {
            var packedStates = SteppeNaturalVisualFieldAtlas.Packs
                .SelectMany(pack => pack.Channels)
                .Where(layer => layer.HasValue)
                .Select(layer => layer.Value)
                .ToArray();

            Assert.That(packedStates, Has.Length.EqualTo(StateCatalog.All.Count));
            Assert.That(packedStates.Distinct().Count(), Is.EqualTo(packedStates.Length));
            foreach (var state in StateCatalog.All.Select(descriptor => descriptor.Id))
            {
                Assert.That(packedStates, Does.Contain(state), state.ToString());
            }
        }

        [Test]
        public void RawPackingPreservesPhysicalUnitsAndSanitizesNonFiniteValues()
        {
            var packed = SteppeNaturalVisualFieldAtlas.PackRaw(
                145.5f,
                -12.25f,
                float.NaN,
                float.PositiveInfinity);

            Assert.That(packed.r, Is.EqualTo(145.5f));
            Assert.That(packed.g, Is.EqualTo(-12.25f));
            Assert.That(packed.b, Is.Zero);
            Assert.That(packed.a, Is.Zero);
        }

        [Test]
        public void DrainageTopologyPreservesDirectionAndBuildsRealConfluences()
        {
            var vectorX = new float[9];
            var vectorY = new float[9];
            // 0 -> 1 <- 2, then 1 -> 4 and 3 -> 4. Cell 4 therefore receives
            // four contributing cells and a second-order branch.
            vectorX[0] = 1f;
            vectorX[2] = -1f;
            vectorX[1] = 0f;
            vectorY[1] = 1f;
            vectorX[3] = 1f;
            var statistics = new LayerStatistics(
                0f, 0f, 0f, 0f, 0f, 0f, 0, 0, 0, new int[0]);
            var drainage = new LayerSnapshot(
                SimulationLayer.Drainage,
                3,
                3,
                new float[9],
                0f,
                0f,
                "link",
                statistics,
                vectorX,
                vectorY);
            var catchments = new LayerSnapshot(
                SimulationLayer.Catchments,
                3,
                3,
                new float[9],
                0f,
                0f,
                "id",
                statistics);
            var topology = new Color[9];

            SteppeNaturalVisualFieldAtlas.BuildDrainageTopology(
                drainage,
                catchments,
                topology);

            Assert.That(topology[2].r, Is.EqualTo(-1f));
            Assert.That(topology[1].g, Is.EqualTo(1f));
            Assert.That(topology[4].b, Is.EqualTo(Mathf.Log(6f, 2f)).Within(0.0001f));
            Assert.That(topology[4].a, Is.EqualTo(2f));
        }

        [Test]
        public void ProcessAtlasContainsAllNonCaravanFluxesAndEveryVectorExactlyOnce()
        {
            var packedFluxes = SteppeNaturalProcessFieldAtlas.FluxPacks
                .SelectMany(pack => pack.Channels)
                .Where(flux => flux.HasValue)
                .Select(flux => flux.Value)
                .ToArray();
            var expectedFluxes = FluxCatalog.All
                .Where(descriptor => descriptor.Order < 600)
                .Select(descriptor => descriptor.Id)
                .ToArray();
            var packedVectors = SteppeNaturalProcessFieldAtlas.Vectors
                .Select(definition => definition.Process)
                .ToArray();

            Assert.That(expectedFluxes, Has.Length.EqualTo(56));
            Assert.That(packedFluxes, Is.EqualTo(expectedFluxes));
            Assert.That(packedFluxes.Distinct().Count(), Is.EqualTo(56));
            Assert.That(SteppeNaturalProcessFieldAtlas.NaturalFluxes, Is.EqualTo(expectedFluxes));
            Assert.That(packedVectors, Has.Length.EqualTo(VectorProcessCatalog.All.Count));
            Assert.That(packedVectors.Distinct().Count(), Is.EqualTo(packedVectors.Length));
            foreach (var descriptor in VectorProcessCatalog.All)
            {
                Assert.That(packedVectors, Does.Contain(descriptor.Process));
            }
        }

        [Test]
        public void TerrainMaterialExposesNoSecondaryBaseColorControls()
        {
            var shader = Shader.Find("Steppe/Terrain Surface");
            Assert.That(shader, Is.Not.Null);
            var material = new Material(shader);
            try
            {
                Assert.That(material.HasProperty("_BaseColor"), Is.False);
                Assert.That(material.HasProperty("_WetDarkening"), Is.False);
                Assert.That(material.HasProperty("_CrustLightening"), Is.False);
                Assert.That(material.HasProperty("_SnowColor"), Is.False);
            }
            finally
            {
                Object.DestroyImmediate(material);
            }
        }

        [Test]
        public void GroundDetailsUseElevenUniqueGeometryLanguagesAndCorrectOwners()
        {
            var details = NaturalGroundDetailCatalog.Descriptors;
            Assert.That(details, Has.Count.EqualTo(11));
            Assert.That(
                details.Select(detail => detail.Kind).Distinct().Count(),
                Is.EqualTo(details.Count));
            Assert.That(
                details.Select(detail => detail.State).Distinct().Count(),
                Is.EqualTo(details.Count));
            Assert.That(
                details.Select(detail => detail.Channel).Distinct().Count(),
                Is.EqualTo(details.Count));
            Assert.That(
                details.Select(detail => detail.GeometryLanguage).Distinct().Count(),
                Is.EqualTo(details.Count));

            foreach (var detail in details)
            {
                var registered = NaturalVisualCatalog.GetState(detail.State);
                Assert.That(detail.Channel, Is.EqualTo(registered.Channel), detail.State.ToString());
            }
        }

        [Test]
        public void EveryGroundDetailKindBuildsNonEmptyUniqueTopology()
        {
            var signatures = NaturalGroundDetailCatalog.Descriptors
                .Select(detail =>
                {
                    var mesh = GroundDetailMeshBuilder.Build(detail.Kind);
                    try
                    {
                        Assert.That(mesh.vertexCount, Is.GreaterThan(0), detail.Kind.ToString());
                        Assert.That(mesh.GetIndexCount(0), Is.GreaterThan(0), detail.Kind.ToString());
                        return $"{mesh.vertexCount}:{mesh.GetIndexCount(0)}:{mesh.bounds.size}";
                    }
                    finally
                    {
                        Object.DestroyImmediate(mesh);
                    }
                })
                .ToArray();

            Assert.That(signatures.Distinct().Count(), Is.EqualTo(signatures.Length));
        }
    }
}
