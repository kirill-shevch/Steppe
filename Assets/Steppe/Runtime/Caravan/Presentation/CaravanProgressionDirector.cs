using System;
using UnityEngine;

namespace Steppe.Caravan
{
    public enum CaravanProgressionStage
    {
        ApproachWreck,
        StopAtWreck,
        DismantleWreck,
        InspectStorage,
        ExpandPlatform,
        SearchPowerStation,
        BuildElectricalDrive,
        Complete
    }

    [DisallowMultipleComponent]
    public sealed class CaravanProgressionDirector : MonoBehaviour
    {
        private CaravanChassisController chassis;
        private CaravanProgressionSystem progression;
        private CaravanPlatformController platform;
        private CaravanElectricalNetwork electricalNetwork;
        private CaravanProgressionWorldRig world;

        public CaravanProgressionStage Stage { get; private set; } =
            CaravanProgressionStage.ApproachWreck;
        public bool IsComplete => Stage == CaravanProgressionStage.Complete;
        public bool HasNavigationLead =>
            Stage <= CaravanProgressionStage.DismantleWreck
            || Stage == CaravanProgressionStage.SearchPowerStation;
        public Vector3 NavigationDirection { get; private set; }
        public float NavigationDistanceMetres { get; private set; }
        public string NavigationLabel { get; private set; } = string.Empty;

        public void Configure(
            CaravanChassisController caravan,
            CaravanProgressionSystem progressionSystem,
            CaravanPlatformController platformController,
            CaravanElectricalNetwork electricity,
            CaravanProgressionWorldRig progressionWorld)
        {
            chassis = caravan != null
                ? caravan
                : throw new ArgumentNullException(nameof(caravan));
            progression = progressionSystem != null
                ? progressionSystem
                : throw new ArgumentNullException(nameof(progressionSystem));
            platform = platformController != null
                ? platformController
                : throw new ArgumentNullException(nameof(platformController));
            electricalNetwork = electricity != null
                ? electricity
                : throw new ArgumentNullException(nameof(electricity));
            world = progressionWorld
                    ?? throw new ArgumentNullException(nameof(progressionWorld));
            RecalculateStage();
        }

        public void RecalculateStage()
        {
            if (chassis == null || progression == null || platform == null
                || electricalNetwork == null || world == null)
            {
                return;
            }
            if (HasElectricalDrive())
            {
                Stage = CaravanProgressionStage.Complete;
            }
            else if (world.PowerStation.IsSearched)
            {
                Stage = CaravanProgressionStage.BuildElectricalDrive;
            }
            else if (platform.ExpandedCellCount > 0)
            {
                Stage = CaravanProgressionStage.SearchPowerStation;
            }
            else if (progression.StorageInspected)
            {
                Stage = CaravanProgressionStage.ExpandPlatform;
            }
            else if (world.TutorialWreck.IsDepleted)
            {
                Stage = CaravanProgressionStage.InspectStorage;
            }
            else if (DistanceTo(world.TutorialWreck.transform) <= 11f)
            {
                Stage = chassis.Speed <= 0.45f
                    ? CaravanProgressionStage.DismantleWreck
                    : CaravanProgressionStage.StopAtWreck;
            }
            else
            {
                Stage = CaravanProgressionStage.ApproachWreck;
            }
            UpdateNavigation();
        }

        private void Update()
        {
            if (chassis == null || progression == null || world == null)
            {
                return;
            }

            AdvanceStage();
            UpdateNavigation();
        }

        public string GetTitle()
        {
            return Stage switch
            {
                CaravanProgressionStage.ApproachWreck =>
                    "Найдите разрушенный караван",
                CaravanProgressionStage.StopAtWreck =>
                    "Остановите караван",
                CaravanProgressionStage.DismantleWreck =>
                    "Добудьте первые материалы",
                CaravanProgressionStage.InspectStorage =>
                    "Проверьте ящик ресурсов",
                CaravanProgressionStage.ExpandPlatform =>
                    "Расширьте платформу",
                CaravanProgressionStage.SearchPowerStation =>
                    "Найдите электрические рецепты",
                CaravanProgressionStage.BuildElectricalDrive =>
                    "Соберите электрический привод",
                _ => "Караван готов к дальнейшему пути"
            };
        }

        public string GetInstruction()
        {
            return Stage switch
            {
                CaravanProgressionStage.ApproachWreck =>
                    "Настройте парус и руль. Следуйте к обломкам впереди.",
                CaravanProgressionStage.StopAtWreck =>
                    "Используйте рычаг тормоза и остановитесь рядом с обломками.",
                CaravanProgressionStage.DismantleWreck =>
                    "Сойдите на землю, наведитесь на обломки и удерживайте X.",
                CaravanProgressionStage.InspectStorage =>
                    "Вернитесь к каравану и нажмите E, глядя на ресурсный ящик.",
                CaravanProgressionStage.ExpandPlatform =>
                    "Остановитесь, нажмите B, затем G и пристройте целую линию по внешнему краю.",
                CaravanProgressionStage.SearchPowerStation =>
                    "Доберитесь до разрушенной электростанции и изучите шкаф с чертежами клавишей E.",
                CaravanProgressionStage.BuildElectricalDrive =>
                    "Разбирайте другие караваны. Постройте и соедините солнечные листья, батарею и электромотор.",
                _ =>
                    "Ищите новые типы руин: каждая открывает собственное семейство технологий."
            };
        }

        public string GetNavigationRange()
        {
            if (!HasNavigationLead)
            {
                return string.Empty;
            }
            if (NavigationDistanceMetres < 100f)
            {
                return $"около {Mathf.Max(5, Mathf.RoundToInt(NavigationDistanceMetres / 5f) * 5)} м";
            }
            return $"около {Mathf.RoundToInt(NavigationDistanceMetres / 50f) * 50} м";
        }

        private void AdvanceStage()
        {
            switch (Stage)
            {
                case CaravanProgressionStage.ApproachWreck:
                    if (DistanceTo(world.TutorialWreck.transform) <= 11f)
                    {
                        Stage = CaravanProgressionStage.StopAtWreck;
                    }
                    break;
                case CaravanProgressionStage.StopAtWreck:
                    if (chassis.Speed <= 0.45f)
                    {
                        Stage = CaravanProgressionStage.DismantleWreck;
                    }
                    break;
                case CaravanProgressionStage.DismantleWreck:
                    if (world.TutorialWreck.IsDepleted)
                    {
                        Stage = CaravanProgressionStage.InspectStorage;
                    }
                    break;
                case CaravanProgressionStage.InspectStorage:
                    if (progression.StorageInspected)
                    {
                        Stage = CaravanProgressionStage.ExpandPlatform;
                    }
                    break;
                case CaravanProgressionStage.ExpandPlatform:
                    if (platform.ExpandedCellCount > 0)
                    {
                        Stage = CaravanProgressionStage.SearchPowerStation;
                    }
                    break;
                case CaravanProgressionStage.SearchPowerStation:
                    if (world.PowerStation.IsSearched)
                    {
                        Stage = CaravanProgressionStage.BuildElectricalDrive;
                    }
                    break;
                case CaravanProgressionStage.BuildElectricalDrive:
                    if (HasElectricalDrive())
                    {
                        Stage = CaravanProgressionStage.Complete;
                    }
                    break;
            }
        }

        private bool HasElectricalDrive()
        {
            return electricalNetwork.PhotovoltaicPort != null
                   && electricalNetwork.BatteryPort != null
                   && electricalNetwork.MotorPort != null
                   && electricalNetwork.PanelToBatteryCable != null
                   && electricalNetwork.PanelToBatteryCable.IsConductive
                   && electricalNetwork.BatteryToMotorCable != null
                   && electricalNetwork.BatteryToMotorCable.IsConductive;
        }

        private void UpdateNavigation()
        {
            if (!HasNavigationLead)
            {
                NavigationDirection = Vector3.zero;
                NavigationDistanceMetres = 0f;
                NavigationLabel = string.Empty;
                return;
            }

            var target = Stage == CaravanProgressionStage.SearchPowerStation
                ? world.PowerStation.transform
                : world.TutorialWreck.transform;
            var delta = Vector3.ProjectOnPlane(
                target.position - chassis.transform.position,
                Vector3.up);
            NavigationDistanceMetres = delta.magnitude;
            NavigationDirection = delta.sqrMagnitude > 0.01f
                ? delta.normalized
                : chassis.transform.forward;
            NavigationLabel = Stage == CaravanProgressionStage.SearchPowerStation
                ? "разрушенная электростанция"
                : "разрушенный караван";
        }

        private float DistanceTo(Transform target)
        {
            return Vector3.ProjectOnPlane(
                target.position - chassis.transform.position,
                Vector3.up).magnitude;
        }
    }
}
