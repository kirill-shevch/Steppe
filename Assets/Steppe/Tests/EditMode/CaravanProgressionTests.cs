using NUnit.Framework;
using Steppe.Caravan;
using UnityEngine;

namespace Steppe.Tests
{
    public sealed class CaravanProgressionTests
    {
        [Test]
        public void NewGameKnowsOnlyCoreCaravanRecipes()
        {
            var gameObject = new GameObject("Progression Test");
            try
            {
                var progression =
                    gameObject.AddComponent<CaravanProgressionSystem>();

                Assert.That(
                    progression.IsRecipeKnown(CaravanPartKind.Sail),
                    Is.True);
                Assert.That(
                    progression.IsRecipeKnown(CaravanPartKind.ResourceCrate),
                    Is.True);
                Assert.That(
                    progression.IsRecipeKnown(
                        CaravanPartKind.PhotovoltaicLeaves),
                    Is.False);
                Assert.That(progression.KnownRecipeCount, Is.EqualTo(2));
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void RecipeSiteUnlocksThematicSetOnlyOnce()
        {
            var gameObject = new GameObject("Recipe Test");
            try
            {
                var progression =
                    gameObject.AddComponent<CaravanProgressionSystem>();

                Assert.That(
                    progression.TrySearchSite(
                        "power-01",
                        CaravanRecipeSiteKind.PowerStation,
                        out var learned),
                    Is.True);
                Assert.That(learned, Has.Count.EqualTo(3));
                Assert.That(
                    progression.IsRecipeKnown(
                        CaravanPartKind.PhotovoltaicLeaves),
                    Is.True);
                Assert.That(
                    progression.IsRecipeKnown(CaravanPartKind.Battery),
                    Is.True);
                Assert.That(
                    progression.IsRecipeKnown(CaravanPartKind.ElectricMotor),
                    Is.True);
                Assert.That(
                    progression.IsRecipeKnown(CaravanPartKind.DualModePump),
                    Is.False);
                Assert.That(
                    progression.TrySearchSite(
                        "power-01",
                        CaravanRecipeSiteKind.PowerStation,
                        out _),
                    Is.False);
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void LockedRecipePointsAtItsThematicRuin()
        {
            Assert.That(
                CaravanProgressionSystem.TryGetRecipeSite(
                    CaravanPartKind.Battery,
                    out var powerSite),
                Is.True);
            Assert.That(
                powerSite,
                Is.EqualTo(CaravanRecipeSiteKind.PowerStation));
            Assert.That(
                CaravanProgressionSystem.TryGetRecipeSite(
                    CaravanPartKind.Harvester,
                    out var farmSite),
                Is.True);
            Assert.That(farmSite, Is.EqualTo(CaravanRecipeSiteKind.Farm));
            Assert.That(
                CaravanProgressionSystem.TryGetRecipeSite(
                    CaravanPartKind.Sail,
                    out _),
                Is.False);
        }

        [Test]
        public void WreckPaysTypedResourcesAtomically()
        {
            var gameObject = new GameObject("Salvage Test");
            try
            {
                var progression =
                    gameObject.AddComponent<CaravanProgressionSystem>();
                var salvage = new[]
                {
                    new CaravanResourceAmount(
                        CaravanConstructionResourceKind.StructuralMaterial,
                        10),
                    new CaravanResourceAmount(
                        CaravanConstructionResourceKind.MechanicalParts,
                        3)
                };

                Assert.That(
                    progression.TryClaimWreck("wreck-01", salvage),
                    Is.True);
                Assert.That(
                    progression.GetResource(
                        CaravanConstructionResourceKind.StructuralMaterial),
                    Is.EqualTo(10));
                Assert.That(
                    progression.TryClaimWreck("wreck-01", salvage),
                    Is.False);
                Assert.That(
                    progression.GetResource(
                        CaravanConstructionResourceKind.StructuralMaterial),
                    Is.EqualTo(10));
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void DiscoveryGlowsTurnOffAfterTheirRewardIsCollected()
        {
            var root = new GameObject("Discovery Glow Test");
            try
            {
                var progression =
                    root.AddComponent<CaravanProgressionSystem>();

                var wreckObject = new GameObject("Wreck");
                wreckObject.transform.SetParent(root.transform, false);
                var intact = new GameObject("Intact").transform;
                intact.SetParent(wreckObject.transform, false);
                var depleted = new GameObject("Depleted").transform;
                depleted.SetParent(wreckObject.transform, false);
                var wreckGlow = new GameObject("Wreck Glow").transform;
                wreckGlow.SetParent(wreckObject.transform, false);
                var wreck = wreckObject.AddComponent<CaravanSalvageWreck>();
                wreck.Configure(
                    "glow-wreck",
                    progression,
                    new[]
                    {
                        new CaravanResourceAmount(
                            CaravanConstructionResourceKind.StructuralMaterial,
                            1)
                    },
                    intact,
                    depleted,
                    0.1f,
                    wreckGlow);

                Assert.That(wreck.DiscoveryGlowVisible, Is.True);
                Assert.That(
                    wreck.AdvanceDismantle(0.2f, out _, out _),
                    Is.True);
                Assert.That(wreck.DiscoveryGlowVisible, Is.False);

                var siteObject = new GameObject("Recipe Site");
                siteObject.transform.SetParent(root.transform, false);
                var siteGlow = new GameObject("Recipe Glow").transform;
                siteGlow.SetParent(siteObject.transform, false);
                var site = siteObject.AddComponent<CaravanRecipeSite>();
                site.Configure(
                    "glow-site",
                    CaravanRecipeSiteKind.PowerStation,
                    progression,
                    siteGlow);

                Assert.That(site.DiscoveryGlowVisible, Is.True);
                Assert.That(site.TryInteract(out _, out _), Is.True);
                Assert.That(site.DiscoveryGlowVisible, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ConstructionSpendIsAllOrNothing()
        {
            var gameObject = new GameObject("Construction Inventory Test");
            try
            {
                var progression =
                    gameObject.AddComponent<CaravanProgressionSystem>();
                progression.AddResources(new[]
                {
                    new CaravanResourceAmount(
                        CaravanConstructionResourceKind.StructuralMaterial,
                        6)
                });

                Assert.That(
                    progression.TrySpend(
                        CaravanConstructionCosts.PlatformTile),
                    Is.True);
                Assert.That(
                    progression.GetResource(
                        CaravanConstructionResourceKind.StructuralMaterial),
                    Is.Zero);
                Assert.That(
                    progression.TrySpend(
                        CaravanConstructionCosts.PlatformTile),
                    Is.False);
                Assert.That(
                    progression.GetResource(
                        CaravanConstructionResourceKind.StructuralMaterial),
                    Is.Zero);
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void ProgressionSnapshotRestoresKnowledgeResourcesAndDepletion()
        {
            var firstObject = new GameObject("First Progression");
            var secondObject = new GameObject("Restored Progression");
            try
            {
                var first =
                    firstObject.AddComponent<CaravanProgressionSystem>();
                first.TrySearchSite(
                    "farm-01",
                    CaravanRecipeSiteKind.Farm,
                    out _);
                first.TryClaimWreck(
                    "wreck-01",
                    new[]
                    {
                        new CaravanResourceAmount(
                            CaravanConstructionResourceKind.Fabric,
                            9)
                    });
                first.MarkStorageInspected();

                var restored =
                    secondObject.AddComponent<CaravanProgressionSystem>();
                restored.RestoreSnapshot(first.CaptureSnapshot());

                Assert.That(
                    restored.IsRecipeKnown(CaravanPartKind.Harvester),
                    Is.True);
                Assert.That(restored.IsSiteSearched("farm-01"), Is.True);
                Assert.That(restored.IsWreckDepleted("wreck-01"), Is.True);
                Assert.That(
                    restored.GetResource(
                        CaravanConstructionResourceKind.Fabric),
                    Is.EqualTo(9));
                Assert.That(restored.StorageInspected, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(firstObject);
                Object.DestroyImmediate(secondObject);
            }
        }

        [Test]
        public void PlatformGridAddsAConnectedLineAtomically()
        {
            var grid = new CaravanMountGridModel(3, 4);
            Assert.That(grid.PlatformCellCount, Is.EqualTo(12));
            var line = new[]
            {
                new CaravanGridCell(-1, 0),
                new CaravanGridCell(-1, 1),
                new CaravanGridCell(-1, 2),
                new CaravanGridCell(-1, 3)
            };
            Assert.That(grid.CanAddPlatformCells(line), Is.True);
            Assert.That(grid.TryAddPlatformCells(line), Is.True);
            Assert.That(grid.PlatformCellCount, Is.EqualTo(16));
            Assert.That(grid.TryGetPlatformBounds(out var bounds), Is.True);
            Assert.That(bounds.MinimumX, Is.EqualTo(-1));
            Assert.That(bounds.MaximumX, Is.EqualTo(2));
            Assert.That(bounds.Width, Is.EqualTo(4));
            Assert.That(bounds.Length, Is.EqualTo(4));
            Assert.That(
                grid.CanAddPlatformCells(new[]
                {
                    new CaravanGridCell(-2, 0),
                    new CaravanGridCell(-4, 2)
                }),
                Is.False);

            var module = new object();
            Assert.That(
                grid.TryPlace(
                    module,
                    new CaravanGridPlacement(-1, 1, 1, 1, 0)),
                Is.True);
            Assert.That(
                grid.CanPlace(
                    new object(),
                    new CaravanGridPlacement(-2, 1, 1, 1, 0)),
                Is.False);
        }

        [Test]
        public void PlatformLineCostScalesWithItsCellCount()
        {
            var cost = CaravanConstructionCosts.Scale(
                CaravanConstructionCosts.PlatformTile,
                4);

            Assert.That(cost, Has.Length.EqualTo(1));
            Assert.That(
                cost[0].Kind,
                Is.EqualTo(
                    CaravanConstructionResourceKind.StructuralMaterial));
            Assert.That(cost[0].Amount, Is.EqualTo(24));
        }

        [Test]
        public void PlatformRestoreRejectsDisconnectedShapes()
        {
            var grid = new CaravanMountGridModel(3, 4);
            var disconnected = new System.Collections.Generic.List<
                CaravanGridCell>();
            for (var z = 0; z < 4; z++)
            {
                for (var x = 0; x < 3; x++)
                {
                    disconnected.Add(new CaravanGridCell(x, z));
                }
            }
            disconnected.Add(new CaravanGridCell(20, 20));

            Assert.That(
                grid.TryReplacePlatformCells(disconnected),
                Is.False);
            Assert.That(grid.PlatformCellCount, Is.EqualTo(12));
        }
    }
}
