using System;
using System.Collections.Generic;
using UnityEngine;

namespace Steppe.Caravan
{
    public abstract class CaravanWorldInteractable : MonoBehaviour
    {
        public abstract string Title { get; }
        public abstract string ContextPrompt { get; }
        public virtual bool SupportsDismantle => false;
        public virtual float ProgressNormalized => 0f;

        public abstract bool TryInteract(
            out string feedback,
            out bool isError);

        public virtual bool AdvanceDismantle(
            float deltaTime,
            out string feedback,
            out bool isError)
        {
            feedback = string.Empty;
            isError = true;
            return false;
        }

        public virtual void CancelDismantle()
        {
        }
    }

    [DisallowMultipleComponent]
    public sealed class CaravanSalvageWreck : CaravanWorldInteractable
    {
        private string wreckId;
        private CaravanProgressionSystem progression;
        private IReadOnlyList<CaravanResourceAmount> yield =
            Array.Empty<CaravanResourceAmount>();
        private Transform intactVisual;
        private Transform depletedVisual;
        private Transform discoveryGlow;
        private float requiredSeconds;
        private float progressSeconds;
        private bool depleted;

        public override string Title => "Разрушенный караван";
        public override bool SupportsDismantle => !depleted;
        public override float ProgressNormalized => depleted
            ? 1f
            : Mathf.Clamp01(progressSeconds / requiredSeconds);
        public bool IsDepleted => depleted;
        public string WreckId => wreckId;
        public IReadOnlyList<CaravanResourceAmount> Yield => yield;
        public bool DiscoveryGlowVisible => discoveryGlow != null
                                             && discoveryGlow.gameObject.activeSelf;
        public override string ContextPrompt => depleted
            ? "Караван полностью разобран"
            : $"Удерживайте X — разобрать  {ProgressNormalized:P0}";

        public void Configure(
            string stableId,
            CaravanProgressionSystem progressionSystem,
            IReadOnlyList<CaravanResourceAmount> salvageYield,
            Transform intact,
            Transform depletedReplacement,
            float dismantleSeconds = 2.4f,
            Transform uncollectedGlow = null)
        {
            wreckId = string.IsNullOrWhiteSpace(stableId)
                ? throw new ArgumentException(
                    "A salvage wreck requires a stable id.",
                    nameof(stableId))
                : stableId;
            progression = progressionSystem != null
                ? progressionSystem
                : throw new ArgumentNullException(nameof(progressionSystem));
            yield = salvageYield ?? Array.Empty<CaravanResourceAmount>();
            intactVisual = intact;
            depletedVisual = depletedReplacement;
            discoveryGlow = uncollectedGlow;
            requiredSeconds = Mathf.Max(0.1f, dismantleSeconds);
            depleted = progression.IsWreckDepleted(wreckId);
            progressSeconds = depleted ? requiredSeconds : 0f;
            RefreshVisuals();
        }

        public override bool TryInteract(
            out string feedback,
            out bool isError)
        {
            feedback = depleted
                ? "Здесь больше нет полезных материалов"
                : "Для разборки удерживайте X";
            isError = depleted;
            return !depleted;
        }

        public override bool AdvanceDismantle(
            float deltaTime,
            out string feedback,
            out bool isError)
        {
            if (depleted)
            {
                feedback = "Караван уже разобран";
                isError = true;
                return false;
            }

            progressSeconds = Mathf.Min(
                requiredSeconds,
                progressSeconds + Mathf.Max(0f, deltaTime));
            if (progressSeconds < requiredSeconds)
            {
                feedback = string.Empty;
                isError = false;
                return false;
            }

            if (!progression.TryClaimWreck(wreckId, yield))
            {
                depleted = progression.IsWreckDepleted(wreckId);
                feedback = "Этот караван уже был разобран";
                isError = true;
                RefreshVisuals();
                return false;
            }

            depleted = true;
            RefreshVisuals();
            feedback = $"Ресурсы отправлены в ящик: {FormatAmounts(yield)}";
            isError = false;
            return true;
        }

        public override void CancelDismantle()
        {
            if (!depleted)
            {
                progressSeconds = 0f;
            }
        }

        public void RefreshFromProgression()
        {
            if (progression == null || string.IsNullOrWhiteSpace(wreckId))
            {
                return;
            }
            depleted = progression.IsWreckDepleted(wreckId);
            progressSeconds = depleted ? requiredSeconds : 0f;
            RefreshVisuals();
        }

        private void RefreshVisuals()
        {
            if (intactVisual != null)
            {
                intactVisual.gameObject.SetActive(!depleted);
            }
            if (depletedVisual != null)
            {
                depletedVisual.gameObject.SetActive(depleted);
            }
            if (discoveryGlow != null)
            {
                discoveryGlow.gameObject.SetActive(!depleted);
            }
        }

        internal static string FormatAmounts(
            IReadOnlyList<CaravanResourceAmount> amounts)
        {
            if (amounts == null || amounts.Count == 0)
            {
                return "нет пригодных материалов";
            }
            var result = string.Empty;
            for (var index = 0; index < amounts.Count; index++)
            {
                if (index > 0)
                {
                    result += "  •  ";
                }
                result += CaravanProgressionSystem.ResourceName(amounts[index].Kind);
                result += " +" + amounts[index].Amount;
            }
            return result;
        }
    }

    [DisallowMultipleComponent]
    public sealed class CaravanRecipeSite : CaravanWorldInteractable
    {
        private string siteId;
        private CaravanRecipeSiteKind siteKind;
        private CaravanProgressionSystem progression;
        private Transform discoveryGlow;
        private bool searched;

        public CaravanRecipeSiteKind SiteKind => siteKind;
        public bool IsSearched => searched;
        public string SiteId => siteId;
        public bool DiscoveryGlowVisible => discoveryGlow != null
                                             && discoveryGlow.gameObject.activeSelf;
        public override string Title => SiteName(siteKind);
        public override string ContextPrompt => searched
            ? "Чертежи уже изучены"
            : "E — изучить сохранившиеся чертежи";

        public void Configure(
            string stableId,
            CaravanRecipeSiteKind kind,
            CaravanProgressionSystem progressionSystem,
            Transform uncollectedGlow = null)
        {
            siteId = string.IsNullOrWhiteSpace(stableId)
                ? throw new ArgumentException(
                    "A recipe site requires a stable id.",
                    nameof(stableId))
                : stableId;
            siteKind = kind;
            progression = progressionSystem != null
                ? progressionSystem
                : throw new ArgumentNullException(nameof(progressionSystem));
            discoveryGlow = uncollectedGlow;
            searched = progression.IsSiteSearched(siteId);
            RefreshVisuals();
        }

        public override bool TryInteract(
            out string feedback,
            out bool isError)
        {
            if (searched)
            {
                feedback = "Все сохранившиеся рецепты здесь уже изучены";
                isError = true;
                return false;
            }

            if (!progression.TrySearchSite(siteId, siteKind, out var learned))
            {
                searched = progression.IsSiteSearched(siteId);
                feedback = "Не удалось восстановить чертежи";
                isError = true;
                return false;
            }

            searched = true;
            RefreshVisuals();
            feedback = "Открыты рецепты: " + FormatRecipes(learned);
            isError = false;
            return true;
        }

        public void RefreshFromProgression()
        {
            if (progression != null && !string.IsNullOrWhiteSpace(siteId))
            {
                searched = progression.IsSiteSearched(siteId);
            }
            RefreshVisuals();
        }

        private void RefreshVisuals()
        {
            if (discoveryGlow != null)
            {
                discoveryGlow.gameObject.SetActive(!searched);
            }
        }

        public static string SiteName(CaravanRecipeSiteKind kind)
        {
            return kind switch
            {
                CaravanRecipeSiteKind.PowerStation => "Разрушенная электростанция",
                CaravanRecipeSiteKind.WaterFacility => "Заброшенная водная станция",
                CaravanRecipeSiteKind.Farm => "Разрушенная биотопливная ферма",
                CaravanRecipeSiteKind.TransportWorkshop => "Разрушенная транспортная мастерская",
                _ => "Разрушенная постройка"
            };
        }

        private static string FormatRecipes(IReadOnlyList<CaravanPartKind> recipes)
        {
            if (recipes == null || recipes.Count == 0)
            {
                return "все найденные рецепты уже известны";
            }
            var result = string.Empty;
            for (var index = 0; index < recipes.Count; index++)
            {
                if (index > 0)
                {
                    result += ", ";
                }
                result += CaravanPartCatalog.Get(recipes[index]).DisplayName;
            }
            return result;
        }
    }
}
