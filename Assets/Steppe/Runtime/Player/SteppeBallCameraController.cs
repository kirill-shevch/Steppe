using System;
using Steppe.Settings;
using Steppe.World;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Steppe.Player
{
    [DisallowMultipleComponent]
    public sealed class SteppeBallCameraController : MonoBehaviour
    {
        private SteppeWorldSettings settings;
        private Transform target;
        private FloatingOriginSystem floatingOrigin;
        private float yaw;
        private float pitch = 18f;
        private float distance;
        private bool pointerLocked;

        public void Configure(
            SteppeWorldSettings worldSettings,
            Transform targetTransform,
            FloatingOriginSystem origin)
        {
            settings = worldSettings != null ? worldSettings : throw new ArgumentNullException(nameof(worldSettings));
            target = targetTransform != null ? targetTransform : throw new ArgumentNullException(nameof(targetTransform));
            floatingOrigin = origin != null ? origin : throw new ArgumentNullException(nameof(origin));
            floatingOrigin.Shifted += OnFloatingOriginShifted;
            distance = settings.PlayerCameraDistance;
            SetPointerLock(true);
            SnapToTarget();
        }

        private void Update()
        {
            if (settings == null || target == null)
            {
                return;
            }

            var keyboard = Keyboard.current;
            var mouse = Mouse.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            {
                SetPointerLock(false);
            }
            else if (!pointerLocked && mouse != null && mouse.leftButton.wasPressedThisFrame)
            {
                SetPointerLock(true);
            }

            if (pointerLocked && mouse != null)
            {
                var look = mouse.delta.ReadValue();
                yaw += look.x * settings.MouseSensitivity;
                pitch = Mathf.Clamp(pitch - look.y * settings.MouseSensitivity, 4f, 65f);
            }

            if (mouse != null)
            {
                var scroll = mouse.scroll.ReadValue().y;
                distance = Mathf.Clamp(
                    distance * Mathf.Exp(-scroll * 0.0012f),
                    settings.PlayerBallRadius * 3.2f,
                    settings.PlayerCameraDistance * 3f);
            }
        }

        private void LateUpdate()
        {
            if (settings == null || target == null)
            {
                return;
            }

            var lookTarget = target.position + Vector3.up * settings.PlayerCameraHeight;
            var rotation = Quaternion.Euler(pitch, yaw, 0f);
            var desiredPosition = lookTarget + rotation * (Vector3.back * distance);
            var blend = 1f - Mathf.Exp(-UnityEngine.Time.unscaledDeltaTime * 8f);
            transform.position = Vector3.Lerp(transform.position, desiredPosition, blend);
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                Quaternion.LookRotation(lookTarget - transform.position, Vector3.up),
                blend);
        }

        private void SnapToTarget()
        {
            var lookTarget = target.position + Vector3.up * settings.PlayerCameraHeight;
            var rotation = Quaternion.Euler(pitch, yaw, 0f);
            transform.position = lookTarget + rotation * (Vector3.back * distance);
            transform.rotation = Quaternion.LookRotation(lookTarget - transform.position, Vector3.up);
        }

        private void OnDisable()
        {
            if (pointerLocked)
            {
                SetPointerLock(false);
            }
        }

        private void OnDestroy()
        {
            if (floatingOrigin != null)
            {
                floatingOrigin.Shifted -= OnFloatingOriginShifted;
            }
        }

        private void OnFloatingOriginShifted(Vector3 shift)
        {
            transform.position -= shift;
        }

        private void SetPointerLock(bool locked)
        {
            pointerLocked = locked;
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }
    }
}
