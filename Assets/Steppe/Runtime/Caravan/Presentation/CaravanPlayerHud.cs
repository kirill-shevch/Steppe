using System;
using Steppe.Presentation;
using Steppe.Time;
using Steppe.World;
using UnityEngine;

namespace Steppe.Caravan
{
    [DisallowMultipleComponent]
    public sealed class CaravanPlayerHud : MonoBehaviour
    {
        private CaravanPlayerInteractor interactor;
        private CaravanBuildModeController buildMode;
        private CaravanChassisController chassis;
        private CaravanElectricalNetwork electricalNetwork;
        private CaravanProgressionDirector progressionDirector;
        private SteppeFirstExpeditionDirector expedition;
        private CaravanSaveService saveService;
        private FloatingOriginSystem floatingOrigin;
        private SteppeTimeSystem timeSystem;
        private GUIStyle objectiveTitleStyle;
        private GUIStyle objectiveBodyStyle;
        private GUIStyle panelTitleStyle;
        private GUIStyle panelStateStyle;
        private GUIStyle panelBodyStyle;
        private GUIStyle promptStyle;
        private GUIStyle reticleStyle;
        private GUIStyle feedbackStyle;
        private Texture2D panelTexture;
        private Texture2D objectiveTexture;

        public CaravanProgressionStage ProgressionStage =>
            progressionDirector != null
                ? progressionDirector.Stage
                : CaravanProgressionStage.ApproachWreck;
        public string SteeringTelemetryText => BuildSteeringTelemetry();

        public void Configure(
            CaravanPlayerInteractor playerInteractor,
            CaravanBuildModeController builder,
            CaravanChassisController caravan,
            CaravanElectricalNetwork electricity,
            CaravanProgressionDirector progression,
            SteppeFirstExpeditionDirector expeditionDirector,
            CaravanSaveService persistence,
            FloatingOriginSystem origin,
            SteppeTimeSystem clock)
        {
            interactor = playerInteractor != null
                ? playerInteractor
                : throw new ArgumentNullException(nameof(playerInteractor));
            buildMode = builder != null
                ? builder
                : throw new ArgumentNullException(nameof(builder));
            chassis = caravan != null
                ? caravan
                : throw new ArgumentNullException(nameof(caravan));
            electricalNetwork = electricity != null
                ? electricity
                : throw new ArgumentNullException(nameof(electricity));
            progressionDirector = progression != null
                ? progression
                : throw new ArgumentNullException(nameof(progression));
            expedition = expeditionDirector != null
                ? expeditionDirector
                : throw new ArgumentNullException(nameof(expeditionDirector));
            saveService = persistence != null
                ? persistence
                : throw new ArgumentNullException(nameof(persistence));
            floatingOrigin = origin != null
                ? origin
                : throw new ArgumentNullException(nameof(origin));
            timeSystem = clock != null
                ? clock
                : throw new ArgumentNullException(nameof(clock));
        }

        private void Update()
        {
            if (chassis == null || progressionDirector == null)
            {
                return;
            }
            expedition?.SetIntroComplete(
                progressionDirector.IsComplete);
        }

        private void OnGUI()
        {
            if (interactor == null || buildMode == null || chassis == null)
            {
                return;
            }

            var previousMatrix = SteppeGuiScale.Begin();
            try
            {
                EnsureStyles();
                DrawObjective();
                DrawNavigation();
                DrawReticle();
                DrawTargetInspector();
                DrawContextPrompt();
                DrawTransientFeedback();
            }
            finally
            {
                SteppeGuiScale.End(previousMatrix);
            }
        }

        private void DrawObjective()
        {
            var width = Mathf.Min(680f, SteppeGuiScale.Width - 40f);
            var area = new Rect(
                (SteppeGuiScale.Width - width) * 0.5f,
                18f,
                width,
                78f);
            GUI.Box(area, GUIContent.none, new GUIStyle(GUI.skin.box)
            {
                normal = { background = objectiveTexture }
            });
            GUI.Label(
                new Rect(area.x + 18f, area.y + 10f, area.width - 36f, 24f),
                CurrentObjectiveTitle(),
                objectiveTitleStyle);
            GUI.Label(
                new Rect(area.x + 18f, area.y + 35f, area.width - 36f, 34f),
                CurrentObjectiveInstruction(),
                objectiveBodyStyle);
        }

        private void DrawNavigation()
        {
            var useProgression = progressionDirector != null
                                 && !progressionDirector.IsComplete
                                 && progressionDirector.HasNavigationLead;
            var useExpedition = !useProgression
                                && progressionDirector != null
                                && progressionDirector.IsComplete
                                && expedition != null
                                && expedition.HasNavigationLead;
            if (!useProgression && !useExpedition)
            {
                return;
            }

            var direction = useProgression
                ? progressionDirector.NavigationDirection
                : expedition.NavigationDirectionLocal;
            if (direction.sqrMagnitude < 0.01f)
            {
                return;
            }

            var relativeAngle = Vector3.SignedAngle(
                transform.forward,
                direction,
                Vector3.up);
            var arrow = relativeAngle < -18f
                ? "←"
                : relativeAngle > 18f
                    ? "→"
                    : "↑";
            var cardinal = CardinalDirection(direction);
            var width = Mathf.Min(560f, SteppeGuiScale.Width - 40f);
            var area = new Rect(
                (SteppeGuiScale.Width - width) * 0.5f,
                103f,
                width,
                38f);
            GUI.Box(area, GUIContent.none, PanelBoxStyle());
            GUI.Label(
                area,
                useProgression
                    ? $"{arrow}  {progressionDirector.NavigationLabel}  •  {cardinal}  •  {progressionDirector.GetNavigationRange()}"
                    : $"{arrow}  {expedition.NavigationLabel}  •  {cardinal}  •  {expedition.GetNavigationRange()}",
                promptStyle);
        }

        private void DrawReticle()
        {
            if (buildMode.IsActive)
            {
                return;
            }

            GUI.Label(
                new Rect(
                    SteppeGuiScale.Width * 0.5f - 12f,
                    SteppeGuiScale.Height * 0.5f - 17f,
                    24f,
                    28f),
                "•",
                reticleStyle);
        }

        private void DrawTargetInspector()
        {
            var interactable = interactor.FocusedInteractable;
            if (interactable != null && !buildMode.IsActive)
            {
                DrawWorldInteractable(interactable);
                return;
            }
            var module = interactor.FocusedModule;
            if (module == null || buildMode.IsActive)
            {
                DrawActiveControl();
                return;
            }

            var feedback = CaravanModuleFeedbackBuilder.Evaluate(module);
            var showSteeringTelemetry =
                interactor.FocusedStation != null
                && interactor.FocusedStation.Kind == CaravanControlKind.Steering;
            var width = Mathf.Min(390f, SteppeGuiScale.Width * 0.36f);
            var height = showSteeringTelemetry ? 292f : 190f;
            var area = new Rect(
                SteppeGuiScale.Width - width - 22f,
                SteppeGuiScale.Height * 0.5f - height * 0.5f,
                width,
                height);
            GUI.Box(area, GUIContent.none, PanelBoxStyle());
            GUI.Label(
                new Rect(area.x + 16f, area.y + 13f, area.width - 32f, 28f),
                feedback.Title,
                panelTitleStyle);

            panelStateStyle.normal.textColor = StateColor(feedback.State);
            GUI.Label(
                new Rect(area.x + 16f, area.y + 45f, area.width - 32f, 24f),
                feedback.StateLabel,
                panelStateStyle);
            GUI.Label(
                new Rect(area.x + 16f, area.y + 72f, area.width - 32f, 44f),
                feedback.Reason,
                panelBodyStyle);
            if (!string.IsNullOrWhiteSpace(feedback.Input))
            {
                GUI.Label(
                    new Rect(area.x + 16f, area.y + 119f, area.width - 32f, 23f),
                    feedback.Input,
                    panelBodyStyle);
            }
            if (!string.IsNullOrWhiteSpace(feedback.Output))
            {
                GUI.Label(
                    new Rect(area.x + 16f, area.y + 143f, area.width - 32f, 23f),
                    feedback.Output,
                    panelBodyStyle);
            }
            GUI.Label(
                new Rect(area.x + 16f, area.y + 166f, area.width - 32f, 20f),
                $"Пыль {module.State.Dust:P0}  •  целостность {module.State.Integrity:P0}",
                panelBodyStyle);
            if (showSteeringTelemetry)
            {
                GUI.Label(
                    new Rect(area.x + 16f, area.y + 190f, area.width - 32f, 88f),
                    BuildSteeringTelemetry(),
                    panelBodyStyle);
            }
        }

        private void DrawWorldInteractable(
            CaravanWorldInteractable interactable)
        {
            var width = Mathf.Min(390f, SteppeGuiScale.Width * 0.36f);
            var area = new Rect(
                SteppeGuiScale.Width - width - 22f,
                SteppeGuiScale.Height * 0.5f - 72f,
                width,
                144f);
            GUI.Box(area, GUIContent.none, PanelBoxStyle());
            GUI.Label(
                new Rect(area.x + 16f, area.y + 13f, area.width - 32f, 30f),
                interactable.Title,
                panelTitleStyle);
            GUI.Label(
                new Rect(area.x + 16f, area.y + 51f, area.width - 32f, 54f),
                interactable is CaravanResourceCrateModule crate
                    ? crate.InventorySummary
                    : interactable.ContextPrompt,
                panelBodyStyle);
            if (interactable.SupportsDismantle)
            {
                GUI.Label(
                    new Rect(area.x + 16f, area.y + 108f, area.width - 32f, 22f),
                    $"Разборка: {interactable.ProgressNormalized:P0}",
                    panelStateStyle);
            }
        }

        private void DrawActiveControl()
        {
            var station = interactor.ActiveStation;
            if (station == null || buildMode.IsActive)
            {
                return;
            }

            var width = 330f;
            var area = new Rect(
                SteppeGuiScale.Width - width - 22f,
                SteppeGuiScale.Height * 0.5f - 58f,
                width,
                116f);
            GUI.Box(area, GUIContent.none, PanelBoxStyle());
            GUI.Label(
                new Rect(area.x + 16f, area.y + 13f, area.width - 32f, 28f),
                ControlName(station.Kind),
                panelTitleStyle);
            GUI.Label(
                new Rect(area.x + 16f, area.y + 48f, area.width - 32f, 26f),
                ControlValue(station),
                panelStateStyle);
            GUI.Label(
                new Rect(area.x + 16f, area.y + 78f, area.width - 32f, 24f),
                "A / D — изменить  •  E — отпустить",
                panelBodyStyle);
        }

        private void DrawContextPrompt()
        {
            var prompt = buildMode.IsActive
                ? BuildPrompt()
                : interactor.ContextPrompt;
            if (string.IsNullOrWhiteSpace(prompt))
            {
                return;
            }

            var width = Mathf.Min(660f, SteppeGuiScale.Width - 40f);
            var area = new Rect(
                (SteppeGuiScale.Width - width) * 0.5f,
                SteppeGuiScale.Height - 78f,
                width,
                42f);
            GUI.Box(area, GUIContent.none, PanelBoxStyle());
            GUI.Label(area, prompt, promptStyle);
        }

        private void DrawTransientFeedback()
        {
            var message = buildMode.FeedbackVisible
                ? buildMode.FeedbackMessage
                : interactor.FeedbackVisible
                    ? interactor.FeedbackMessage
                    : saveService != null && saveService.FeedbackVisible
                        ? saveService.FeedbackMessage
                        : string.Empty;
            if (string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            var isError = buildMode.FeedbackVisible
                ? buildMode.FeedbackIsError
                : interactor.FeedbackVisible
                    ? interactor.FeedbackIsError
                    : saveService != null && saveService.FeedbackIsError;
            feedbackStyle.normal.textColor = isError
                ? new Color(1f, 0.42f, 0.3f)
                : new Color(0.58f, 1f, 0.72f);
            GUI.Label(
                new Rect(
                    SteppeGuiScale.Width * 0.5f - 340f,
                    SteppeGuiScale.Height - 128f,
                    680f,
                    36f),
                message,
                feedbackStyle);
        }

        private string BuildPrompt()
        {
            if (buildMode.IsCommunicationMode)
            {
                return "ЛКМ — выбрать и соединить  •  ПКМ — отменить или удалить  •  Tab — другой слой  •  B — выйти";
            }
            if (buildMode.HeldModule != null)
            {
                return buildMode.CandidateValid
                    ? "ЛКМ — установить  •  R — повернуть  •  ПКМ — отменить"
                    : "Нет свободного места под модулем  •  R — повернуть  •  ПКМ — отменить";
            }
            if (buildMode.IsPlatformExpansionMode)
            {
                return buildMode.CandidateValid
                    ? "ЛКМ — построить линию  •  цена указана слева  •  ПКМ / G — отменить  •  B — выйти"
                    : "Выберите свободный внешний край  •  ПКМ / G — отменить";
            }
            return "Q / E — выбрать  •  F — создать  •  G — расширить платформу  •  ЛКМ — переставить  •  ПКМ / X — разобрать  •  Tab — сети  •  B — выйти";
        }

        private string ControlValue(CaravanControlStation station)
        {
            if (station.Kind == CaravanControlKind.ElectricThrottle)
            {
                return $"Тяга: {chassis.ElectricDriveThrottle:P0}";
            }
            if (station.Kind == CaravanControlKind.Steering)
            {
                return $"Руль: {chassis.SteeringNormalized:+0%;-0%;0%}  •  скорость: {chassis.Speed:F1} м/с  •  тормоз: {chassis.BrakeNormalized:P0}";
            }
            if (station.Kind == CaravanControlKind.Brake)
            {
                return $"Тормоз: {chassis.BrakeNormalized:P0}  •  скорость: {chassis.Speed:F1} м/с";
            }
            if (station.Kind == CaravanControlKind.PumpMode)
            {
                var pump = station.GetComponentInParent<CaravanElectricPumpModule>();
                return pump != null
                    ? pump.Mode switch
                    {
                        CaravanPumpMode.Extraction => "Режим: добыча",
                        CaravanPumpMode.Circulation => "Режим: циркуляция",
                        _ => "Режим: выключен"
                    }
                    : "Режим насоса";
            }
            return $"Положение: {(station.NormalizedValue + 1f) * 0.5f:P0}";
        }

        private string BuildSteeringTelemetry()
        {
            if (chassis == null || floatingOrigin == null || timeSystem == null)
            {
                return string.Empty;
            }

            var world = floatingOrigin.LocalToWorld(chassis.transform.position);
            var time = timeSystem.Current;
            var day = Mathf.FloorToInt((float)time.DayOfYear) + 1;
            return
                $"Скорость {chassis.Speed:F1} м/с  •  температура {chassis.CurrentAirTemperatureC:F0} °C\n"
                + $"Пройдено {chassis.TravelledMetres:F0} м\n"
                + $"X {world.X:F0}  •  Z {world.Z:F0}  •  высота {world.Y:F0} м\n"
                + $"День {day}  •  год {time.Year + 1}  •  {SeasonName(time.Season)}";
        }

        private static string SeasonName(SteppeSeason season)
        {
            return season switch
            {
                SteppeSeason.Winter => "зима",
                SteppeSeason.Spring => "весна",
                SteppeSeason.Summer => "лето",
                SteppeSeason.Autumn => "осень",
                _ => string.Empty
            };
        }

        private static string ControlName(CaravanControlKind kind)
        {
            return kind switch
            {
                CaravanControlKind.Steering => "Рулевое колесо",
                CaravanControlKind.Brake => "Рычаг тормоза",
                CaravanControlKind.SailTrim => "Угол паруса",
                CaravanControlKind.ElectricThrottle => "Тяга электромотора",
                CaravanControlKind.SolarOrientation => "Ориентация солнечных листьев",
                CaravanControlKind.PumpMode => "Режим насоса",
                CaravanControlKind.RadiatorOpening => "Заслонка радиатора",
                CaravanControlKind.FurnaceIntensity => "Интенсивность печи",
                CaravanControlKind.BiofuelThrottle => "Тяга биодвигателя",
                CaravanControlKind.HarvesterPower => "Мощность жатки",
                CaravanControlKind.DryerPower => "Мощность сушилки",
                CaravanControlKind.TransmissionRatio => "Передаточное отношение",
                _ => "Орган управления"
            };
        }

        private string CurrentObjectiveTitle()
        {
            if (progressionDirector == null
                || !progressionDirector.IsComplete
                || expedition == null
                || expedition.Stage == SteppeExpeditionStage.Locked)
            {
                return progressionDirector != null
                    ? progressionDirector.GetTitle()
                    : string.Empty;
            }
            return expedition.GetTitle();
        }

        private string CurrentObjectiveInstruction()
        {
            if (progressionDirector == null
                || !progressionDirector.IsComplete
                || expedition == null
                || expedition.Stage == SteppeExpeditionStage.Locked)
            {
                return progressionDirector != null
                    ? progressionDirector.GetInstruction()
                    : string.Empty;
            }
            return expedition.GetInstruction();
        }

        private static string CardinalDirection(Vector3 direction)
        {
            var degrees = Mathf.Repeat(
                Mathf.Atan2(direction.x, direction.z)
                * Mathf.Rad2Deg
                + 360f,
                360f);
            var index = Mathf.RoundToInt(degrees / 45f) % 8;
            return index switch
            {
                0 => "север",
                1 => "северо-восток",
                2 => "восток",
                3 => "юго-восток",
                4 => "юг",
                5 => "юго-запад",
                6 => "запад",
                _ => "северо-запад"
            };
        }

        private void EnsureStyles()
        {
            if (objectiveTitleStyle != null)
            {
                return;
            }

            panelTexture = SolidTexture(
                new Color(0.035f, 0.055f, 0.052f, 0.91f));
            objectiveTexture = SolidTexture(
                new Color(0.055f, 0.075f, 0.062f, 0.92f));
            objectiveTitleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 18,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.UpperLeft,
                normal = { textColor = new Color(0.93f, 0.84f, 0.58f) }
            };
            objectiveBodyStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                wordWrap = true,
                normal = { textColor = new Color(0.88f, 0.92f, 0.86f) }
            };
            panelTitleStyle = new GUIStyle(objectiveTitleStyle)
            {
                fontSize = 19,
                normal = { textColor = Color.white }
            };
            panelStateStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 15,
                fontStyle = FontStyle.Bold
            };
            panelBodyStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                wordWrap = true,
                normal = { textColor = new Color(0.82f, 0.87f, 0.82f) }
            };
            promptStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 15,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };
            reticleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 28,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.92f, 0.95f, 0.88f, 0.9f) }
            };
            feedbackStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
        }

        private GUIStyle PanelBoxStyle()
        {
            return new GUIStyle(GUI.skin.box)
            {
                normal = { background = panelTexture }
            };
        }

        private static Color StateColor(CaravanOperationalState state)
        {
            return state switch
            {
                CaravanOperationalState.Working =>
                    new Color(0.36f, 1f, 0.58f),
                CaravanOperationalState.Starved =>
                    new Color(1f, 0.68f, 0.2f),
                CaravanOperationalState.Blocked =>
                    new Color(1f, 0.48f, 0.25f),
                CaravanOperationalState.Full =>
                    new Color(0.35f, 0.76f, 1f),
                CaravanOperationalState.Dirty =>
                    new Color(0.9f, 0.55f, 0.2f),
                CaravanOperationalState.Damaged =>
                    new Color(1f, 0.25f, 0.2f),
                _ => new Color(0.78f, 0.86f, 0.78f)
            };
        }

        private static Texture2D SolidTexture(Color color)
        {
            var texture = new Texture2D(1, 1)
            {
                hideFlags = HideFlags.DontSave,
                name = "Caravan HUD Color"
            };
            texture.SetPixel(0, 0, color);
            texture.Apply();
            return texture;
        }

        private void OnDestroy()
        {
            if (panelTexture != null)
            {
                Destroy(panelTexture);
            }
            if (objectiveTexture != null)
            {
                Destroy(objectiveTexture);
            }
        }
    }
}
