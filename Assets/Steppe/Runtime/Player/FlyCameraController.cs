using UnityEngine;
using UnityEngine.InputSystem;

namespace Steppe.Player
{
    [DisallowMultipleComponent]
    public sealed class FlyCameraController : MonoBehaviour
    {
        private float moveSpeed = 80f;
        private float boostMultiplier = 6f;
        private float mouseSensitivity = 0.08f;
        private float yaw;
        private float pitch;
        private bool pointerLocked;

        public float CurrentMoveSpeed { get; private set; }

        public void Configure(float speed, float boost, float sensitivity)
        {
            moveSpeed = Mathf.Max(1f, speed);
            boostMultiplier = Mathf.Max(1f, boost);
            mouseSensitivity = Mathf.Max(0.001f, sensitivity);
        }

        private void Start()
        {
            var euler = transform.eulerAngles;
            yaw = euler.y;
            pitch = NormalizePitch(euler.x);
            SetPointerLock(true);
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            var mouse = Mouse.current;

            if (keyboard == null)
            {
                return;
            }

            if (keyboard.escapeKey.wasPressedThisFrame)
            {
                SetPointerLock(false);
            }
            else if (!pointerLocked && mouse != null && mouse.leftButton.wasPressedThisFrame)
            {
                SetPointerLock(true);
            }

            if (pointerLocked && mouse != null)
            {
                var lookDelta = mouse.delta.ReadValue();
                yaw += lookDelta.x * mouseSensitivity;
                pitch = Mathf.Clamp(pitch - lookDelta.y * mouseSensitivity, -89f, 89f);
                transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
            }

            if (mouse != null)
            {
                var scroll = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(scroll) > 0.01f)
                {
                    moveSpeed = Mathf.Clamp(moveSpeed * Mathf.Exp(scroll * 0.0015f), 2f, 4000f);
                }
            }

            var movement = Vector3.zero;
            if (keyboard.wKey.isPressed) movement += transform.forward;
            if (keyboard.sKey.isPressed) movement -= transform.forward;
            if (keyboard.dKey.isPressed) movement += transform.right;
            if (keyboard.aKey.isPressed) movement -= transform.right;
            if (keyboard.eKey.isPressed) movement += Vector3.up;
            if (keyboard.qKey.isPressed) movement -= Vector3.up;

            var boost = keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed;
            CurrentMoveSpeed = moveSpeed * (boost ? boostMultiplier : 1f);

            if (movement.sqrMagnitude > 0f)
            {
                transform.position += movement.normalized * (CurrentMoveSpeed * UnityEngine.Time.unscaledDeltaTime);
            }
        }

        private void OnDisable()
        {
            if (pointerLocked)
            {
                SetPointerLock(false);
            }
        }

        private void SetPointerLock(bool locked)
        {
            pointerLocked = locked;
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }

        private static float NormalizePitch(float pitchValue)
        {
            return pitchValue > 180f ? pitchValue - 360f : pitchValue;
        }
    }
}
