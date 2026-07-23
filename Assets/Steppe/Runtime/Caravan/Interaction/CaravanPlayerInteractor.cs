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
        private CaravanControlStation activeStation;
        private CaravanControlStation focusedStation;
        private const float InteractionDistance = 4.8f;
        private const float StationAimRadius = 0.18f;

        public CaravanControlStation ActiveStation => activeStation;

        public void Configure(
            Camera camera,
            CaravanFirstPersonController controller,
            CaravanBuildModeController builder)
        {
            viewCamera = camera != null ? camera : throw new ArgumentNullException(nameof(camera));
            firstPerson = controller != null ? controller : throw new ArgumentNullException(nameof(controller));
            buildMode = builder;
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            var mouse = Mouse.current;
            if (keyboard == null || viewCamera == null || firstPerson == null)
            {
                return;
            }

            if (buildMode != null && buildMode.IsActive)
            {
                SetFocusedStation(null);
                if (activeStation != null)
                {
                    EndControl();
                }
                return;
            }

            if (activeStation != null)
            {
                SetFocusedStation(activeStation);
                if (keyboard.eKey.wasPressedThisFrame)
                {
                    EndControl();
                    return;
                }

                var keyboardDelta = 0f;
                if (keyboard.aKey.isPressed) keyboardDelta -= UnityEngine.Time.deltaTime * 0.65f;
                if (keyboard.dKey.isPressed) keyboardDelta += UnityEngine.Time.deltaTime * 0.65f;
                var mouseDelta = mouse != null ? mouse.delta.ReadValue().x * 0.003f : 0f;
                activeStation.Adjust(keyboardDelta + mouseDelta);
                return;
            }

            var target = RaycastTarget();
            var targetStation = FindTargetedStation();
            SetFocusedStation(targetStation);
            if (keyboard.eKey.wasPressedThisFrame && targetStation != null)
            {
                activeStation = targetStation;
                activeStation.SetEngaged(true);
                firstPerson.SetInteractionControl(true);
                return;
            }

            if (target.collider == null)
            {
                return;
            }

            var module = target.collider.GetComponentInParent<CaravanModule>();
            if (module == null)
            {
                return;
            }

            if (keyboard.cKey.isPressed)
            {
                module.Clean(UnityEngine.Time.deltaTime * 0.34f);
            }
            if (keyboard.rKey.isPressed)
            {
                module.Repair(UnityEngine.Time.deltaTime * 0.16f);
            }
        }

        public void EndControl()
        {
            activeStation?.SetEngaged(false);
            activeStation = null;
            firstPerson?.SetInteractionControl(false);
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
                ~0,
                QueryTriggerInteraction.Collide);
            return hit;
        }

        private CaravanControlStation FindTargetedStation()
        {
            var hits = Physics.SphereCastAll(
                viewCamera.transform.position,
                StationAimRadius,
                viewCamera.transform.forward,
                InteractionDistance,
                ~0,
                QueryTriggerInteraction.Collide);
            Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));
            for (var index = 0; index < hits.Length; index++)
            {
                var station = hits[index].collider.GetComponentInParent<CaravanControlStation>();
                if (station != null)
                {
                    return station;
                }
            }

            return null;
        }

        private void OnDisable()
        {
            SetFocusedStation(null);
            EndControl();
        }
    }
}
