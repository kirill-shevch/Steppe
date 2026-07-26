using System;
using System.Collections.Generic;
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
        public const int CollisionProxyLayer = 7;
        public const int WorldQueryMask = ~(1 << CollisionProxyLayer);

        private SteppeWorldSettings settings;
        private FloatingOriginSystem floatingOrigin;
        private CaravanChassisController caravan;
        private CharacterController character;
        private CaravanPlayerCollisionProxy caravanCollisionProxy;
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
        public bool CaravanContactIsolationEnabled =>
            caravanCollisionProxy != null && caravanCollisionProxy.IsActive;
        public int CaravanCollisionProxyCount =>
            caravanCollisionProxy != null ? caravanCollisionProxy.ColliderCount : 0;

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
            caravanCollisionProxy?.Dispose();
            caravanCollisionProxy = new CaravanPlayerCollisionProxy(character, caravan);

            viewCamera.transform.SetParent(transform, false);
            viewCamera.transform.localPosition = new Vector3(0f, 1.63f, 0f);
            viewCamera.transform.localRotation = Quaternion.identity;
            previousCarrierPosition = caravan.transform.position;
            previousCarrierRotation = caravan.transform.rotation;
            yaw = transform.eulerAngles.y;
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
            caravanCollisionProxy.Synchronize();
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
                WorldQueryMask,
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
            caravanCollisionProxy?.Synchronize();
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
            caravanCollisionProxy?.Dispose();
            caravanCollisionProxy = null;
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
            caravanCollisionProxy?.Synchronize();
        }

        private void SetPointerLock(bool locked)
        {
            pointerLocked = locked;
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }
    }

    /// <summary>
    /// A player-only kinematic copy of the caravan collision geometry. The keeper
    /// collides with this copy instead of the dynamic chassis, so character motion
    /// can never contribute an impulse or torque to the caravan Rigidbody.
    /// </summary>
    internal sealed class CaravanPlayerCollisionProxy : IDisposable
    {
        private const int PlayerLayer = 6;

        private readonly CharacterController character;
        private readonly CaravanChassisController caravan;
        private readonly GameObject root;
        private readonly Rigidbody body;
        private readonly List<ColliderBinding> bindings = new List<ColliderBinding>();
        private readonly List<Collider> sourceColliderBuffer = new List<Collider>();
        private bool disposed;

        public CaravanPlayerCollisionProxy(
            CharacterController playerCharacter,
            CaravanChassisController chassis)
        {
            character = playerCharacter != null
                ? playerCharacter
                : throw new ArgumentNullException(nameof(playerCharacter));
            caravan = chassis != null
                ? chassis
                : throw new ArgumentNullException(nameof(chassis));

            character.gameObject.layer = PlayerLayer;
            ConfigureCollisionLayers();

            root = new GameObject("Keeper Caravan Collision Proxy");
            root.layer = CaravanFirstPersonController.CollisionProxyLayer;
            root.transform.SetParent(caravan.transform.parent, true);
            body = root.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            body.detectCollisions = true;
            body.interpolation = RigidbodyInterpolation.None;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

            RefreshColliders();
            Synchronize();
        }

        public bool IsActive => !disposed && root != null && body != null && body.isKinematic;
        public int ColliderCount
        {
            get
            {
                var count = 0;
                for (var index = 0; index < bindings.Count; index++)
                {
                    if (bindings[index].Proxy != null)
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        public void Synchronize()
        {
            if (disposed || root == null || caravan == null)
            {
                return;
            }

            RefreshColliders();

            root.transform.SetPositionAndRotation(
                caravan.transform.position,
                caravan.transform.rotation);
            root.transform.localScale = Vector3.one;

            for (var index = bindings.Count - 1; index >= 0; index--)
            {
                var binding = bindings[index];
                if (binding.Source == null)
                {
                    if (binding.Proxy != null)
                    {
                        UnityEngine.Object.Destroy(binding.Proxy.gameObject);
                    }

                    bindings.RemoveAt(index);
                    continue;
                }

                if (binding.Proxy == null)
                {
                    continue;
                }

                binding.Proxy.enabled =
                    binding.Source.enabled
                    && binding.Source.gameObject.activeInHierarchy;
                binding.Proxy.transform.SetPositionAndRotation(
                    binding.Source.transform.position,
                    binding.Source.transform.rotation);
                binding.Proxy.transform.localScale = binding.Source.transform.lossyScale;
            }

            Physics.SyncTransforms();
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            for (var index = 0; index < bindings.Count; index++)
            {
                var binding = bindings[index];
                if (!binding.WasIgnored
                    && character != null
                    && binding.Source != null)
                {
                    Physics.IgnoreCollision(character, binding.Source, false);
                }
            }

            bindings.Clear();
            if (root != null)
            {
                UnityEngine.Object.Destroy(root);
            }
        }

        private static void ConfigureCollisionLayers()
        {
            for (var layer = 0; layer < 32; layer++)
            {
                Physics.IgnoreLayerCollision(
                    CaravanFirstPersonController.CollisionProxyLayer,
                    layer,
                    layer != PlayerLayer);
            }
        }

        private void RefreshColliders()
        {
            sourceColliderBuffer.Clear();
            caravan.GetComponentsInChildren(true, sourceColliderBuffer);
            for (var sourceIndex = 0;
                 sourceIndex < sourceColliderBuffer.Count;
                 sourceIndex++)
            {
                var source = sourceColliderBuffer[sourceIndex];
                if (source == null
                    || !source.enabled
                    || source.isTrigger
                    || HasBinding(source))
                {
                    continue;
                }

                var wasIgnored = Physics.GetIgnoreCollision(character, source);
                if (!wasIgnored)
                {
                    Physics.IgnoreCollision(character, source, true);
                }

                var proxy = CloneCollider(source);
                bindings.Add(new ColliderBinding(source, proxy, wasIgnored));
            }
        }

        private bool HasBinding(Collider source)
        {
            for (var index = 0; index < bindings.Count; index++)
            {
                if (bindings[index].Source == source)
                {
                    return true;
                }
            }

            return false;
        }

        private Collider CloneCollider(Collider source)
        {
            var proxyObject = new GameObject($"Player Proxy - {source.name}");
            proxyObject.layer = CaravanFirstPersonController.CollisionProxyLayer;
            proxyObject.transform.SetParent(root.transform, false);

            Collider proxy = null;
            if (source is BoxCollider sourceBox)
            {
                var proxyBox = proxyObject.AddComponent<BoxCollider>();
                proxyBox.center = sourceBox.center;
                proxyBox.size = sourceBox.size;
                proxy = proxyBox;
            }
            else if (source is SphereCollider sourceSphere)
            {
                var proxySphere = proxyObject.AddComponent<SphereCollider>();
                proxySphere.center = sourceSphere.center;
                proxySphere.radius = sourceSphere.radius;
                proxy = proxySphere;
            }
            else if (source is CapsuleCollider sourceCapsule)
            {
                var proxyCapsule = proxyObject.AddComponent<CapsuleCollider>();
                proxyCapsule.center = sourceCapsule.center;
                proxyCapsule.radius = sourceCapsule.radius;
                proxyCapsule.height = sourceCapsule.height;
                proxyCapsule.direction = sourceCapsule.direction;
                proxy = proxyCapsule;
            }
            else if (source is MeshCollider sourceMesh)
            {
                var proxyMesh = proxyObject.AddComponent<MeshCollider>();
                proxyMesh.cookingOptions = sourceMesh.cookingOptions;
                proxyMesh.convex = sourceMesh.convex;
                proxyMesh.sharedMesh = sourceMesh.sharedMesh;
                proxy = proxyMesh;
            }

            if (proxy == null)
            {
                UnityEngine.Object.Destroy(proxyObject);
                return null;
            }

            proxy.sharedMaterial = source.sharedMaterial;
            proxy.contactOffset = source.contactOffset;
            return proxy;
        }

        private sealed class ColliderBinding
        {
            public ColliderBinding(Collider source, Collider proxy, bool wasIgnored)
            {
                Source = source;
                Proxy = proxy;
                WasIgnored = wasIgnored;
            }

            public Collider Source { get; }
            public Collider Proxy { get; }
            public bool WasIgnored { get; }
        }
    }
}
