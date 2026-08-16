using System;
using Steppe.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Steppe.Caravan
{
    [DisallowMultipleComponent]
    public sealed class CaravanPlayerInteractor : MonoBehaviour
    {
        private Camera viewCamera;
        private CaravanFirstPersonController firstPerson;
        private CaravanBuildModeController buildMode;
        private CaravanResourceSystem resourceSystem;
        private CaravanControlStation activeStation;
        private CaravanControlStation focusedStation;
        private CaravanModule focusedModule;
        private CaravanWorldInteractable focusedInteractable;
        private CaravanWorldInteractable dismantleTarget;
        private float feedbackUntil;
        private string feedbackMessage;
        private bool feedbackIsError;
        private const float InteractionDistance = 4.8f;
        private const float StationAimRadius = 0.18f;

        public CaravanControlStation ActiveStation => activeStation;
        public CaravanControlStation FocusedStation => focusedStation;
        public CaravanModule FocusedModule => focusedModule;
        public CaravanWorldInteractable FocusedInteractable => focusedInteractable;
        public string FeedbackMessage => feedbackMessage;
        public bool FeedbackIsError => feedbackIsError;
        public bool FeedbackVisible =>
            !string.IsNullOrWhiteSpace(feedbackMessage)
            && UnityEngine.Time.unscaledTime < feedbackUntil;
        public string ContextPrompt
        {
            get
            {
                if (activeStation != null)
                {
                    return "A / D — изменить  •  E — отпустить";
                }
                if (focusedStation != null)
                {
                    return $"E — использовать: {GetControlName(focusedStation.Kind)}";
                }
                if (focusedInteractable != null)
                {
                    return focusedInteractable.ContextPrompt;
                }
                if (focusedModule != null)
                {
                    var clean = focusedModule.State.Dust > 0.001f
                        ? "C — очистить"
                        : "чисто";
                    var repair = focusedModule.State.Integrity < 0.999f
                        ? "R — ремонтировать"
                        : "исправно";
                    return $"{clean}  •  {repair}";
                }
                return string.Empty;
            }
        }

        public void Configure(
            Camera camera,
            CaravanFirstPersonController controller,
            CaravanBuildModeController builder)
        {
            viewCamera = camera != null ? camera : throw new ArgumentNullException(nameof(camera));
            firstPerson = controller != null ? controller : throw new ArgumentNullException(nameof(controller));
            buildMode = builder;
            resourceSystem = FindAnyObjectByType<CaravanResourceSystem>();
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null || viewCamera == null || firstPerson == null)
            {
                return;
            }

            if (buildMode != null && buildMode.IsActive)
            {
                SetFocusedStation(null);
                SetFocusedInteractable(null);
                focusedModule = null;
                if (activeStation != null)
                {
                    EndControl();
                }
                return;
            }

            if (activeStation != null)
            {
                SetFocusedStation(activeStation);
                SetFocusedInteractable(null);
                focusedModule = activeStation.GetComponentInParent<CaravanModule>();
                if (keyboard.eKey.wasPressedThisFrame)
                {
                    EndControl();
                    return;
                }

                var keyboardDelta = 0f;
                if (keyboard.aKey.isPressed) keyboardDelta -= UnityEngine.Time.deltaTime * 0.65f;
                if (keyboard.dKey.isPressed) keyboardDelta += UnityEngine.Time.deltaTime * 0.65f;
                AdjustActiveControl(keyboardDelta);
                return;
            }

            var target = RaycastTarget();
            var targetStation = FindTargetedStation();
            SetFocusedStation(targetStation);
            var targetInteractable = target.collider != null
                ? target.collider.GetComponentInParent<CaravanWorldInteractable>()
                : null;
            SetFocusedInteractable(targetStation == null
                ? targetInteractable
                : null);
            focusedModule = target.collider != null
                ? target.collider.GetComponentInParent<CaravanModule>()
                : null;
            if (keyboard.eKey.wasPressedThisFrame && targetStation != null)
            {
                TryBeginControl(targetStation);
                return;
            }

            if (focusedInteractable != null)
            {
                if (keyboard.eKey.wasPressedThisFrame)
                {
                    focusedInteractable.TryInteract(
                        out var interactionFeedback,
                        out var interactionError);
                    if (!string.IsNullOrWhiteSpace(interactionFeedback))
                    {
                        SetFeedback(
                            interactionFeedback,
                            interactionError,
                            3.2f);
                    }
                }

                if (keyboard.xKey.isPressed
                    && focusedInteractable.SupportsDismantle)
                {
                    if (firstPerson.IsOnCaravan)
                    {
                        focusedInteractable.CancelDismantle();
                        dismantleTarget = null;
                        if (keyboard.xKey.wasPressedThisFrame)
                        {
                            SetFeedback(
                                "Для разборки сойдите с платформы",
                                true,
                                1.8f);
                        }
                        return;
                    }
                    dismantleTarget = focusedInteractable;
                    if (focusedInteractable.AdvanceDismantle(
                            UnityEngine.Time.deltaTime,
                            out var dismantleFeedback,
                            out var dismantleError)
                        && !string.IsNullOrWhiteSpace(dismantleFeedback))
                    {
                        SetFeedback(
                            dismantleFeedback,
                            dismantleError,
                            4f);
                    }
                }
                else if (dismantleTarget != null)
                {
                    dismantleTarget.CancelDismantle();
                    dismantleTarget = null;
                }
                return;
            }

            if (target.collider == null)
            {
                SetFocusedInteractable(null);
                focusedModule = null;
                return;
            }

            var module = target.collider.GetComponentInParent<CaravanModule>();
            if (module == null)
            {
                return;
            }

            if (keyboard.cKey.isPressed)
            {
                var previousDust = module.State.Dust;
                module.Clean(UnityEngine.Time.deltaTime * 0.34f);
                if (previousDust > 0.001f && module.State.Dust <= 0.001f)
                {
                    SetFeedback("Модуль очищен", false, 1.4f);
                }
            }
            if (keyboard.rKey.isPressed)
            {
                var repairAmount = Mathf.Min(
                    UnityEngine.Time.deltaTime * 0.16f,
                    1f - module.State.Integrity);
                if (repairAmount > 0f
                    && resourceSystem != null
                    && resourceSystem.TryConsumeRepairMaterial(
                        repairAmount * 2f))
                {
                    module.Repair(repairAmount);
                    if (module.TryGetComponent<CaravanCouplingRopeModule>(
                            out var rope))
                    {
                        rope.RepairRope();
                    }
                    if (module.State.Integrity >= 0.999f)
                    {
                        SetFeedback("Ремонт завершён", false, 1.4f);
                    }
                }
                else if (repairAmount > 0f)
                {
                    SetFeedback(
                        "Для ремонта нужна сухая биомасса в подключённом хранилище",
                        true,
                        0.35f);
                }
            }
        }

        public void EndControl()
        {
            activeStation?.SetEngaged(false);
            activeStation = null;
            firstPerson?.SetInteractionControl(false);
        }

        public bool TryBeginControl(CaravanControlStation station)
        {
            if (station == null
                || !station.isActiveAndEnabled
                || firstPerson == null
                || (buildMode != null && buildMode.IsActive))
            {
                return false;
            }

            if (activeStation != null && activeStation != station)
            {
                activeStation.SetEngaged(false);
            }
            activeStation = station;
            SetFocusedStation(station);
            activeStation.SetEngaged(true);
            firstPerson.SetInteractionControl(true);
            return true;
        }

        public bool AdjustActiveControl(float delta)
        {
            if (activeStation == null)
            {
                return false;
            }

            activeStation.Adjust(delta);
            return true;
        }

        private void SetFocusedStation(CaravanControlStation station)
        {
            if (focusedStation == station)
            {
                return;
            }

            focusedStation?.SetFocused(false);
            focusedStation = station;
            focusedStation?.SetFocused(true);
        }

        private RaycastHit RaycastTarget()
        {
            var ray = new Ray(viewCamera.transform.position, viewCamera.transform.forward);
            Physics.Raycast(
                ray,
                out var hit,
                InteractionDistance,
                CaravanFirstPersonController.WorldQueryMask,
                QueryTriggerInteraction.Collide);
            return hit;
        }

        private CaravanControlStation FindTargetedStation()
        {
            return ResolveControlTarget(new Ray(
                viewCamera.transform.position,
                viewCamera.transform.forward));
        }

        private void SetFocusedInteractable(
            CaravanWorldInteractable interactable)
        {
            if (focusedInteractable == interactable)
            {
                return;
            }
            if (dismantleTarget != null
                && dismantleTarget != interactable)
            {
                dismantleTarget.CancelDismantle();
                dismantleTarget = null;
            }
            focusedInteractable = interactable;
        }

        public CaravanControlStation ResolveControlTarget(Ray ray)
        {
            var direction = ray.direction.normalized;
            if (direction.sqrMagnitude < 0.99f)
            {
                return null;
            }

            var directHitDistance = InteractionDistance;
            if (Physics.Raycast(
                    ray,
                    out var directHit,
                    InteractionDistance,
                    CaravanFirstPersonController.WorldQueryMask,
                    QueryTriggerInteraction.Collide))
            {
                directHitDistance = directHit.distance;
                var directlyHitStation = directHit.collider
                    .GetComponentInParent<CaravanControlStation>();
                if (directlyHitStation != null)
                {
                    return directlyHitStation;
                }
            }

            var hits = Physics.SphereCastAll(
                ray.origin,
                StationAimRadius,
                direction,
                InteractionDistance,
                CaravanFirstPersonController.WorldQueryMask,
                QueryTriggerInteraction.Collide);
            CaravanControlStation bestStation = null;
            var bestAimScore = float.PositiveInfinity;
            var bestDistance = float.PositiveInfinity;
            for (var index = 0; index < hits.Length; index++)
            {
                if (hits[index].distance
                    > directHitDistance + StationAimRadius)
                {
                    continue;
                }

                var station = hits[index].collider.GetComponentInParent<CaravanControlStation>();
                if (station == null || !station.isActiveAndEnabled)
                {
                    continue;
                }

                var center = hits[index].collider.bounds.center;
                var alongRay = Mathf.Clamp(
                    Vector3.Dot(center - ray.origin, direction),
                    0f,
                    InteractionDistance);
                var closestOnRay = ray.origin + direction * alongRay;
                var aimScore = hits[index].collider.bounds
                    .SqrDistance(closestOnRay);
                if (aimScore < bestAimScore - 0.0001f
                    || (Mathf.Abs(aimScore - bestAimScore) <= 0.0001f
                        && hits[index].distance < bestDistance))
                {
                    bestStation = station;
                    bestAimScore = aimScore;
                    bestDistance = hits[index].distance;
                }
            }

            return bestStation;
        }

        private void OnDisable()
        {
            SetFocusedStation(null);
            SetFocusedInteractable(null);
            focusedModule = null;
            EndControl();
        }

        private void SetFeedback(
            string message,
            bool isError,
            float duration)
        {
            feedbackMessage = message;
            feedbackIsError = isError;
            feedbackUntil = UnityEngine.Time.unscaledTime
                            + Mathf.Max(0.1f, duration);
        }

        private static string GetControlName(CaravanControlKind kind)
        {
            return kind switch
            {
                CaravanControlKind.Steering => "руль",
                CaravanControlKind.Brake => "рычаг тормоза",
                CaravanControlKind.SailTrim => "угол паруса",
                CaravanControlKind.ElectricThrottle => "тяга электромотора",
                CaravanControlKind.SolarOrientation => "солнечные листья",
                CaravanControlKind.PumpMode => "режим насоса",
                CaravanControlKind.RadiatorOpening => "заслонка радиатора",
                CaravanControlKind.FurnaceIntensity => "мощность печи",
                CaravanControlKind.BiofuelThrottle => "тяга биодвигателя",
                CaravanControlKind.HarvesterPower => "мощность жатки",
                CaravanControlKind.DryerPower => "мощность сушилки",
                CaravanControlKind.TransmissionRatio => "передача",
                _ => "управление"
            };
        }
    }
}
