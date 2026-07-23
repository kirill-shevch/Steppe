using System;
using Steppe.Caravan;
using Steppe.Settings;
using Steppe.World;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Steppe.Player
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController))]
    public sealed class CaravanFirstPersonController : MonoBehaviour
    {
        private SteppeWorldSettings settings;
        private FloatingOriginSystem floatingOrigin;
        private CaravanChassisController caravan;
        private CharacterController character;
        private Camera viewCamera;
        private float yaw;
        private float pitch;
        private float verticalVelocity;
        private Vector3 inheritedVelocity;
        private Vector3 previousCarrierPosition;
        private Quaternion previousCarrierRotation;
        private bool pointerLocked;
        private bool interactionControl;
        private readonly RaycastHit[] groundHits = new RaycastHit[12];

        public Camera ViewCamera => viewCamera;
        public bool InteractionControl => interactionControl;
        public bool IsOnCaravan { get; private set; }

        public void Configure(
            SteppeWorldSettings worldSettings,
            FloatingOriginSystem origin,
            CaravanChassisController chassis,
            Camera camera)
        {
            settings = worldSettings != null ? worldSettings : throw new ArgumentNullException(nameof(worldSettings));
            floatingOrigin = origin != null ? origin : throw new ArgumentNullException(nameof(origin));
            caravan = chassis != null ? chassis : throw new ArgumentNullException(nameof(chassis));
            viewCamera = camera != null ? camera : throw new ArgumentNullException(nameof(camera));
            character = GetComponent<CharacterController>();
            character.radius = 0.34f;
            character.height = 1.78f;
            character.center = new Vector3(0f, 0.89f, 0f);
            character.stepOffset = 0.34f;
            character.slopeLimit = 48f;
            character.skinWidth = 0.035f;
            character.minMoveDistance = 0f;

            viewCamera.transform.SetParent(transform, false);
            viewCamera.transform.localPosition = new Vector3(0f, 1.63f, 0f);
            viewCamera.transform.localRotation = Quaternion.identity;
            previousCarrierPosition = caravan.transform.position;
            previousCarrierRotation = caravan.transform.rotation;
            floatingOrigin.Shifted += OnFloatingOriginShifted;
            caravan.Teleported += OnCaravanTeleported;
            SetPointerLock(true);
        }

        public void SetInteractionControl(bool active)
        {
            interactionControl = active;
        }

        private void Update()
        {
            if (settings == null || character == null || caravan == null || viewCamera == null)
            {
                return;
            }

            HandlePointerLock();
            ApplyCarrierMotion();
            UpdateGroundCarrier();
            UpdateLook();
            UpdateMovement();
            previousCarrierPosition = caravan.transform.position;
            previousCarrierRotation = caravan.transform.rotation;
        }

        private void HandlePointerLock()
        {
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
        }

        private void ApplyCarrierMotion()
        {
            if (!IsOnCaravan || !character.enabled)
            {
                return;
            }

            var rotationDelta = caravan.transform.rotation * Quaternion.Inverse(previousCarrierRotation);
            var previousRelative = transform.position - previousCarrierPosition;
            var carrierPosition = caravan.transform.position + rotationDelta * previousRelative;
            character.Move(carrierPosition - transform.position);
        }

        private void UpdateGroundCarrier()
        {
            var origin = transform.position + Vector3.up * 0.5f;
            var hitCount = Physics.RaycastNonAlloc(
                origin,
                Vector3.down,
                groundHits,
                0.78f,
                ~0,
                QueryTriggerInteraction.Ignore);
            IsOnCaravan = false;
            for (var index = 0; index < hitCount; index++)
            {
                var hitTransform = groundHits[index].transform;
                if (hitTransform == caravan.transform
                    || hitTransform.IsChildOf(caravan.transform))
                {
                    IsOnCaravan = true;
                    break;
                }
            }
        }

        private void UpdateLook()
        {
            if (!pointerLocked || interactionControl || Mouse.current == null)
            {
                return;
            }

            var look = Mouse.current.delta.ReadValue();
            yaw += look.x * settings.MouseSensitivity;
            pitch = Mathf.Clamp(pitch - look.y * settings.MouseSensitivity, -82f, 82f);
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            viewCamera.transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }

        private void UpdateMovement()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null || interactionControl)
            {
                return;
            }

            var input = Vector2.zero;
            if (keyboard.wKey.isPressed) input.y += 1f;
            if (keyboard.sKey.isPressed) input.y -= 1f;
            if (keyboard.dKey.isPressed) input.x += 1f;
            if (keyboard.aKey.isPressed) input.x -= 1f;
            input = Vector2.ClampMagnitude(input, 1f);

            var grounded = character.isGrounded;
            if (grounded && verticalVelocity < 0f)
            {
                verticalVelocity = -2f;
                inheritedVelocity = Vector3.zero;
            }

            if (grounded && keyboard.spaceKey.wasPressedThisFrame)
            {
                verticalVelocity = 5.2f;
                inheritedVelocity = caravan.Body != null
                    ? Vector3.ProjectOnPlane(caravan.Body.GetPointVelocity(transform.position), Vector3.up)
                    : Vector3.zero;
                IsOnCaravan = false;
            }

            verticalVelocity += Physics.gravity.y * UnityEngine.Time.deltaTime;
            if (!grounded)
            {
                inheritedVelocity = Vector3.Lerp(
                    inheritedVelocity,
                    Vector3.zero,
                    1f - Mathf.Exp(-UnityEngine.Time.deltaTime * 0.35f));
            }

            var run = keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed;
            var speed = run ? 6.7f : 4.1f;
            var desired = transform.forward * input.y + transform.right * input.x;
            var motion = desired * speed + inheritedVelocity + Vector3.up * verticalVelocity;
            character.Move(motion * UnityEngine.Time.deltaTime);
        }

        private void OnFloatingOriginShifted(Vector3 shift)
        {
            if (character == null)
            {
                transform.position -= shift;
                return;
            }

            var wasEnabled = character.enabled;
            character.enabled = false;
            transform.position -= shift;
            character.enabled = wasEnabled;
            previousCarrierPosition = caravan != null
                ? caravan.transform.position
                : previousCarrierPosition - shift;
            previousCarrierRotation = caravan != null
                ? caravan.transform.rotation
                : previousCarrierRotation;
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
            if (caravan != null)
            {
                caravan.Teleported -= OnCaravanTeleported;
            }
        }

        private void OnCaravanTeleported(Vector3 delta)
        {
            if (character == null)
            {
                transform.position += delta;
                return;
            }

            var wasEnabled = character.enabled;
            character.enabled = false;
            transform.position += delta;
            character.enabled = wasEnabled;
            previousCarrierPosition = caravan.transform.position;
            previousCarrierRotation = caravan.transform.rotation;
        }

        private void SetPointerLock(bool locked)
        {
            pointerLocked = locked;
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }
    }
}
