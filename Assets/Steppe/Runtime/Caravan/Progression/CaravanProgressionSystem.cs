using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Steppe.Caravan
{
    public enum CaravanConstructionResourceKind
    {
        StructuralMaterial,
        MechanicalParts,
        ElectricalParts,
        Fabric
    }

    public enum CaravanRecipeSiteKind
    {
        PowerStation,
        WaterFacility,
        Farm,
        TransportWorkshop
    }

    [Serializable]
    public readonly struct CaravanResourceAmount
    {
        public CaravanResourceAmount(
            CaravanConstructionResourceKind kind,
            int amount)
        {
            Kind = kind;
            Amount = Math.Max(0, amount);
        }

        public CaravanConstructionResourceKind Kind { get; }
        public int Amount { get; }
    }

    [Serializable]
    public sealed class CaravanProgressionSnapshot
    {
        public CaravanPartKind[] knownRecipes = Array.Empty<CaravanPartKind>();
        public int[] resourceAmounts = Array.Empty<int>();
        public string[] searchedSiteIds = Array.Empty<string>();
        public string[] depletedWreckIds = Array.Empty<string>();
        public bool storageInspected;
    }

    public static class CaravanConstructionCosts
    {
        private static readonly CaravanResourceAmount[] PlatformTileCost =
        {
            new CaravanResourceAmount(
                CaravanConstructionResourceKind.StructuralMaterial,
                6)
        };

        public static IReadOnlyList<CaravanResourceAmount> PlatformTile =>
            PlatformTileCost;

        public static IReadOnlyList<CaravanResourceAmount> ForPart(
            CaravanPartKind kind)
        {
            return kind switch
            {
                CaravanPartKind.Sail => Cost(8, 2, 0, 8),
                CaravanPartKind.ResourceCrate => Cost(5, 1, 0, 2),
                CaravanPartKind.PhotovoltaicLeaves => Cost(7, 2, 8, 0),
                CaravanPartKind.Battery => Cost(9, 3, 10, 0),
                CaravanPartKind.ElectricMotor => Cost(9, 8, 7, 0),
                CaravanPartKind.WaterReservoir => Cost(12, 5, 0, 0),
                CaravanPartKind.DualModePump => Cost(7, 8, 4, 0),
                CaravanPartKind.Radiator => Cost(9, 5, 0, 0),
                CaravanPartKind.Harvester => Cost(14, 12, 4, 0),
                CaravanPartKind.GrassDryer => Cost(10, 4, 3, 4),
                CaravanPartKind.BiomassStorage => Cost(9, 3, 0, 5),
                CaravanPartKind.Biofurnace => Cost(10, 6, 0, 0),
                CaravanPartKind.BiofuelEngine => Cost(13, 14, 4, 0),
                CaravanPartKind.Transmission => Cost(8, 12, 0, 0),
                CaravanPartKind.CouplingRope => Cost(3, 3, 0, 8),
                _ => Array.Empty<CaravanResourceAmount>()
            };
        }

        public static CaravanResourceAmount[] Scale(
            IReadOnlyList<CaravanResourceAmount> cost,
            int multiplier)
        {
            if (cost == null || cost.Count == 0 || multiplier <= 0)
            {
                return Array.Empty<CaravanResourceAmount>();
            }

            var result = new CaravanResourceAmount[cost.Count];
            for (var index = 0; index < cost.Count; index++)
            {
                result[index] = new CaravanResourceAmount(
                    cost[index].Kind,
                    checked(cost[index].Amount * multiplier));
            }
            return result;
        }

        private static CaravanResourceAmount[] Cost(
            int structural,
            int mechanical,
            int electrical,
            int fabric)
        {
            var result = new List<CaravanResourceAmount>(4);
            Add(result, CaravanConstructionResourceKind.StructuralMaterial, structural);
            Add(result, CaravanConstructionResourceKind.MechanicalParts, mechanical);
            Add(result, CaravanConstructionResourceKind.ElectricalParts, electrical);
            Add(result, CaravanConstructionResourceKind.Fabric, fabric);
            return result.ToArray();
        }

        private static void Add(
            ICollection<CaravanResourceAmount> result,
            CaravanConstructionResourceKind kind,
            int amount)
        {
            if (amount > 0)
            {
                result.Add(new CaravanResourceAmount(kind, amount));
            }
        }
    }

    /// <summary>
    /// Authoritative P18 knowledge and construction inventory. World interactions
    /// claim stable ids through this component so re-entering an area cannot award
    /// the same recipe or salvage twice.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CaravanProgressionSystem : MonoBehaviour
    {
        private readonly HashSet<CaravanPartKind> knownRecipes =
            new HashSet<CaravanPartKind>();
        private readonly Dictionary<CaravanConstructionResourceKind, int> resources =
            new Dictionary<CaravanConstructionResourceKind, int>();
        private readonly HashSet<string> searchedSiteIds =
            new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> depletedWreckIds =
            new HashSet<string>(StringComparer.Ordinal);
        private bool initialized;

        public event Action Changed;
        public event Action<IReadOnlyList<CaravanPartKind>> RecipesLearned;
        public event Action<IReadOnlyList<CaravanResourceAmount>> SalvageReceived;

        public int KnownRecipeCount
        {
            get
            {
                EnsureInitialized();
                return knownRecipes.Count;
            }
        }
        public int SearchedSiteCount
        {
            get
            {
                EnsureInitialized();
                return searchedSiteIds.Count;
            }
        }
        public int DepletedWreckCount
        {
            get
            {
                EnsureInitialized();
                return depletedWreckIds.Count;
            }
        }
        public bool StorageInspected { get; private set; }

        private void Awake()
        {
            EnsureInitialized();
        }

        public void ResetToNewGame()
        {
            initialized = true;
            knownRecipes.Clear();
            resources.Clear();
            searchedSiteIds.Clear();
            depletedWreckIds.Clear();
            StorageInspected = false;
            knownRecipes.Add(CaravanPartKind.Sail);
            knownRecipes.Add(CaravanPartKind.ResourceCrate);
            foreach (CaravanConstructionResourceKind kind in
                     Enum.GetValues(typeof(CaravanConstructionResourceKind)))
            {
                resources[kind] = 0;
            }
            Changed?.Invoke();
        }

        public bool IsRecipeKnown(CaravanPartKind kind)
        {
            EnsureInitialized();
            return knownRecipes.Contains(kind);
        }

        public int GetResource(CaravanConstructionResourceKind kind)
        {
            EnsureInitialized();
            return resources.TryGetValue(kind, out var amount) ? amount : 0;
        }

        public bool HasResources(IReadOnlyList<CaravanResourceAmount> cost)
        {
            if (cost == null)
            {
                return true;
            }
            for (var index = 0; index < cost.Count; index++)
            {
                if (GetResource(cost[index].Kind) < cost[index].Amount)
                {
                    return false;
                }
            }
            return true;
        }

        public bool TrySpend(IReadOnlyList<CaravanResourceAmount> cost)
        {
            if (!HasResources(cost))
            {
                return false;
            }
            if (cost != null)
            {
                for (var index = 0; index < cost.Count; index++)
                {
                    resources[cost[index].Kind] =
                        GetResource(cost[index].Kind) - cost[index].Amount;
                }
            }
            Changed?.Invoke();
            return true;
        }

        public void AddResources(IReadOnlyList<CaravanResourceAmount> amounts)
        {
            EnsureInitialized();
            if (amounts == null)
            {
                return;
            }
            for (var index = 0; index < amounts.Count; index++)
            {
                resources[amounts[index].Kind] =
                    GetResource(amounts[index].Kind) + amounts[index].Amount;
            }
            Changed?.Invoke();
        }

        public bool TryClaimWreck(
            string wreckId,
            IReadOnlyList<CaravanResourceAmount> yield)
        {
            EnsureInitialized();
            if (string.IsNullOrWhiteSpace(wreckId)
                || depletedWreckIds.Contains(wreckId))
            {
                return false;
            }
            depletedWreckIds.Add(wreckId);
            AddResources(yield);
            SalvageReceived?.Invoke(yield ?? Array.Empty<CaravanResourceAmount>());
            return true;
        }

        public bool IsWreckDepleted(string wreckId)
        {
            EnsureInitialized();
            return !string.IsNullOrWhiteSpace(wreckId)
                   && depletedWreckIds.Contains(wreckId);
        }

        public bool TrySearchSite(
            string siteId,
            CaravanRecipeSiteKind siteKind,
            out IReadOnlyList<CaravanPartKind> learned)
        {
            EnsureInitialized();
            learned = Array.Empty<CaravanPartKind>();
            if (string.IsNullOrWhiteSpace(siteId)
                || searchedSiteIds.Contains(siteId))
            {
                return false;
            }

            searchedSiteIds.Add(siteId);
            var candidates = RecipesForSite(siteKind);
            var additions = new List<CaravanPartKind>(candidates.Count);
            for (var index = 0; index < candidates.Count; index++)
            {
                if (knownRecipes.Add(candidates[index]))
                {
                    additions.Add(candidates[index]);
                }
            }
            learned = additions;
            Changed?.Invoke();
            if (additions.Count > 0)
            {
                RecipesLearned?.Invoke(additions);
            }
            return true;
        }

        public bool IsSiteSearched(string siteId)
        {
            EnsureInitialized();
            return !string.IsNullOrWhiteSpace(siteId)
                   && searchedSiteIds.Contains(siteId);
        }

        public void MarkStorageInspected()
        {
            EnsureInitialized();
            if (StorageInspected)
            {
                return;
            }
            StorageInspected = true;
            Changed?.Invoke();
        }

        public void UnlockAllRecipes()
        {
            EnsureInitialized();
            for (var index = 0; index < CaravanPartCatalog.All.Count; index++)
            {
                knownRecipes.Add(CaravanPartCatalog.All[index].Kind);
            }
            Changed?.Invoke();
        }

        public string FormatInventory()
        {
            var builder = new StringBuilder();
            foreach (CaravanConstructionResourceKind kind in
                     Enum.GetValues(typeof(CaravanConstructionResourceKind)))
            {
                if (builder.Length > 0)
                {
                    builder.Append("  •  ");
                }
                builder.Append(ResourceName(kind));
                builder.Append(' ');
                builder.Append(GetResource(kind));
            }
            return builder.ToString();
        }

        public string FormatCost(IReadOnlyList<CaravanResourceAmount> cost)
        {
            if (cost == null || cost.Count == 0)
            {
                return "бесплатно";
            }
            var builder = new StringBuilder();
            for (var index = 0; index < cost.Count; index++)
            {
                if (index > 0)
                {
                    builder.Append("  •  ");
                }
                builder.Append(ResourceName(cost[index].Kind));
                builder.Append(' ');
                builder.Append(GetResource(cost[index].Kind));
                builder.Append('/');
                builder.Append(cost[index].Amount);
            }
            return builder.ToString();
        }

        public CaravanProgressionSnapshot CaptureSnapshot()
        {
            EnsureInitialized();
            var snapshot = new CaravanProgressionSnapshot
            {
                knownRecipes = Copy(knownRecipes),
                resourceAmounts = new int[
                    Enum.GetValues(typeof(CaravanConstructionResourceKind)).Length],
                searchedSiteIds = Copy(searchedSiteIds),
                depletedWreckIds = Copy(depletedWreckIds),
                storageInspected = StorageInspected
            };
            foreach (CaravanConstructionResourceKind kind in
                     Enum.GetValues(typeof(CaravanConstructionResourceKind)))
            {
                snapshot.resourceAmounts[(int)kind] = GetResource(kind);
            }
            return snapshot;
        }

        public void RestoreSnapshot(CaravanProgressionSnapshot snapshot)
        {
            ResetToNewGame();
            if (snapshot == null)
            {
                return;
            }
            if (snapshot.knownRecipes != null)
            {
                for (var index = 0; index < snapshot.knownRecipes.Length; index++)
                {
                    knownRecipes.Add(snapshot.knownRecipes[index]);
                }
            }
            if (snapshot.resourceAmounts != null)
            {
                foreach (CaravanConstructionResourceKind kind in
                         Enum.GetValues(typeof(CaravanConstructionResourceKind)))
                {
                    var index = (int)kind;
                    resources[kind] = index < snapshot.resourceAmounts.Length
                        ? Math.Max(0, snapshot.resourceAmounts[index])
                        : 0;
                }
            }
            CopyInto(snapshot.searchedSiteIds, searchedSiteIds);
            CopyInto(snapshot.depletedWreckIds, depletedWreckIds);
            StorageInspected = snapshot.storageInspected;
            Changed?.Invoke();
        }

        public static IReadOnlyList<CaravanPartKind> RecipesForSite(
            CaravanRecipeSiteKind kind)
        {
            return kind switch
            {
                CaravanRecipeSiteKind.PowerStation => new[]
                {
                    CaravanPartKind.PhotovoltaicLeaves,
                    CaravanPartKind.Battery,
                    CaravanPartKind.ElectricMotor
                },
                CaravanRecipeSiteKind.WaterFacility => new[]
                {
                    CaravanPartKind.WaterReservoir,
                    CaravanPartKind.DualModePump,
                    CaravanPartKind.Radiator
                },
                CaravanRecipeSiteKind.Farm => new[]
                {
                    CaravanPartKind.Harvester,
                    CaravanPartKind.GrassDryer,
                    CaravanPartKind.BiomassStorage,
                    CaravanPartKind.Biofurnace,
                    CaravanPartKind.BiofuelEngine
                },
                CaravanRecipeSiteKind.TransportWorkshop => new[]
                {
                    CaravanPartKind.Transmission,
                    CaravanPartKind.CouplingRope
                },
                _ => Array.Empty<CaravanPartKind>()
            };
        }

        public static bool TryGetRecipeSite(
            CaravanPartKind part,
            out CaravanRecipeSiteKind site)
        {
            switch (part)
            {
                case CaravanPartKind.PhotovoltaicLeaves:
                case CaravanPartKind.Battery:
                case CaravanPartKind.ElectricMotor:
                    site = CaravanRecipeSiteKind.PowerStation;
                    return true;
                case CaravanPartKind.WaterReservoir:
                case CaravanPartKind.DualModePump:
                case CaravanPartKind.Radiator:
                    site = CaravanRecipeSiteKind.WaterFacility;
                    return true;
                case CaravanPartKind.Harvester:
                case CaravanPartKind.GrassDryer:
                case CaravanPartKind.BiomassStorage:
                case CaravanPartKind.Biofurnace:
                case CaravanPartKind.BiofuelEngine:
                    site = CaravanRecipeSiteKind.Farm;
                    return true;
                case CaravanPartKind.Transmission:
                case CaravanPartKind.CouplingRope:
                    site = CaravanRecipeSiteKind.TransportWorkshop;
                    return true;
                default:
                    site = default;
                    return false;
            }
        }

        public static string ResourceName(CaravanConstructionResourceKind kind)
        {
            return kind switch
            {
                CaravanConstructionResourceKind.StructuralMaterial => "конструкции",
                CaravanConstructionResourceKind.MechanicalParts => "механика",
                CaravanConstructionResourceKind.ElectricalParts => "электрика",
                CaravanConstructionResourceKind.Fabric => "ткань",
                _ => kind.ToString()
            };
        }

        private static T[] Copy<T>(ICollection<T> source)
        {
            var result = new T[source.Count];
            source.CopyTo(result, 0);
            return result;
        }

        private static void CopyInto(
            IReadOnlyList<string> source,
            ISet<string> destination)
        {
            if (source == null)
            {
                return;
            }
            for (var index = 0; index < source.Count; index++)
            {
                if (!string.IsNullOrWhiteSpace(source[index]))
                {
                    destination.Add(source[index]);
                }
            }
        }

        private void EnsureInitialized()
        {
            if (!initialized)
            {
                ResetToNewGame();
            }
        }
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(CaravanModule))]
    public sealed class CaravanResourceCrateModule : CaravanWorldInteractable
    {
        private CaravanProgressionSystem progression;

        public CaravanProgressionSystem Progression => progression;
        public string InventorySummary => progression != null
            ? progression.FormatInventory()
            : "хранилище не подключено";

        public void Configure(CaravanProgressionSystem progressionSystem)
        {
            progression = progressionSystem != null
                ? progressionSystem
                : throw new ArgumentNullException(nameof(progressionSystem));
        }

        public string Inspect()
        {
            progression?.MarkStorageInspected();
            return InventorySummary;
        }

        public override string Title => "Ящик ресурсов";
        public override string ContextPrompt => "E — проверить ресурсы";

        public override bool TryInteract(
            out string feedback,
            out bool isError)
        {
            feedback = Inspect();
            isError = progression == null;
            return progression != null;
        }
    }
}
