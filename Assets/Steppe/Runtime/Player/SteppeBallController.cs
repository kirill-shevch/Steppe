using System;
using Steppe.Ecology;
using Steppe.Settings;
using Steppe.Terrain;
using Steppe.World;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Steppe.Player
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody), typeof(SphereCollider))]
    public sealed class SteppeBallController : MonoBehaviour, ISteppeTravelFocus
    {
        private SteppeWorldSettings settings;
        private FloatingOriginSystem floatingOrigin;
        private SteppeEcologySystem ecologySystem;
        private Transform viewTransform;
        private Transform visualTransform;
        private Rigidbody body;
        private TerrainChunkStreamer terrainStreamer;
        private TerrainHeightGenerator terrainGenerator;
        private float lastGroundContactTime = float.NegativeInfinity;
        private bool physicsStarted;

        public Rigidbody Body => body;
        public Transform FocusTransform => transform;
        public bool IsGrounded => physicsStarted && UnityEngine.Time.fixedTime - lastGroundContactTime < 0.12f;
        public float Speed => body != null ? new Vector2(body.linearVelocity.x, body.linearVelocity.z).magnitude : 0f;
        public float TrackRadius => settings != null ? settings.PlayerBallRadius * 1.12f : 1.4f;
        public SteppeTraversalState CurrentSurface { get; private set; }

        public void Configure(
            SteppeWorldSettings worldSettings,
            FloatingOriginSystem origin,
            SteppeEcologySystem ecology,
            Transform cameraTransform,
            Transform ballVisual)
        {
            settings = worldSettings != null ? worldSettings : throw new ArgumentNullException(nameof(worldSettings));
            floatingOrigin = origin != null ? origin : throw new ArgumentNullException(nameof(origin));
            ecologySystem = ecology != null ? ecology : throw new ArgumentNullException(nameof(ecology));
            viewTransform = cameraTransform != null ? cameraTransform : throw new ArgumentNullException(nameof(cameraTransform));
            visualTransform = ballVisual != null ? ballVisual : throw new ArgumentNullException(nameof(ballVisual));

            body = GetComponent<Rigidbody>();
            body.mass = settings.PlayerBallMass;
            body.useGravity = true;
            body.isKinematic = true;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.linearDamping = 0.08f;
            body.angularDamping = 0.08f;
            body.maxAngularVelocity = settings.PlayerMaximumSpeed / settings.PlayerBallRadius * 1.5f;
            terrainGenerator = new TerrainHeightGenerator(settings);
        }

        public void Teleport(Vector3 localPosition)
        {
            if (body == null)
            {
                transform.position = localPosition;
                return;
            }

            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.position = localPosition;
            transform.position = localPosition;
            Physics.SyncTransforms();
            body.isKinematic = true;
            physicsStarted = false;
            lastGroundContactTime = float.NegativeInfinity;
            CurrentSurface = default;
        }

        private void FixedUpdate()
        {
            if (settings == null || body == null || floatingOrigin == null || ecologySystem == null)
            {
                return;
            }

            if (!physicsStarted)
            {
                TryBeginPhysicsOnLoadedTerrain();
                return;
            }

            UpdateSurfaceState();
            ApplyDrive();
            ApplySurfaceResistance();
            UpdateVisualSink();
        }

        private void TryBeginPhysicsOnLoadedTerrain()
        {
            terrainStreamer ??= FindAnyObjectByType<TerrainChunkStreamer>();
            var world = floatingOrigin.LocalToWorld(transform.position);
            if (terrainStreamer == null || !terrainStreamer.HasPhysicsSurfaceAt(world.X, world.Z))
            {
                return;
            }

            // Physics and rendering use the same canonical height generator. Waiting
            // for the LOD0 collider before releasing gravity avoids both self-ray hits
            // and falling through a chunk that is still queued for mesh cooking.
            var groundHeight = terrainGenerator.SampleHeight(world.X, world.Z);
            var groundedPosition = floatingOrigin.WorldToLocal(
                world.X,
                groundHeight + settings.PlayerBallRadius + 0.04,
                world.Z);
            body.position = groundedPosition;
            transform.position = groundedPosition;
            Physics.SyncTransforms();
            body.isKinematic = false;
            physicsStarted = true;
            lastGroundContactTime = UnityEngine.Time.fixedTime;
        }

        private void UpdateSurfaceState()
        {
            var world = floatingOrigin.LocalToWorld(transform.position);
            var coordinate = EcoCellCoordinate.FromWorld(world.X, world.Z, settings.EcologyCellSize);
            if (ecologySystem.TryGetCell(coordinate, out var surface, out var ecology))
            {
                CurrentSurface = SteppeTraversalModel.Evaluate(settings, surface, ecology);
            }
        }

        private void ApplyDrive()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null || !IsGrounded)
            {
                return;
            }

            var input = Vector2.zero;
            if (keyboard.wKey.isPressed) input.y += 1f;
            if (keyboard.sKey.isPressed) input.y -= 1f;
            if (keyboard.dKey.isPressed) input.x += 1f;
            if (keyboard.aKey.isPressed) input.x -= 1f;
            if (input.sqrMagnitude < 0.001f)
            {
                return;
            }

            input.Normalize();
            var forward = Vector3.ProjectOnPlane(viewTransform.forward, Vector3.up).normalized;
            var right = Vector3.ProjectOnPlane(viewTransform.right, Vector3.up).normalized;
            var desiredDirection = (forward * input.y + right * input.x).normalized;
            var response = Mathf.Lerp(1f, 0.24f, (float)CurrentSurface.Resistance);
            var boost = keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed;
            var drive = settings.PlayerDriveAcceleration * response * (boost ? 1.35f : 1f);

            body.AddForce(desiredDirection * drive, ForceMode.Acceleration);
            var torqueAxis = Vector3.Cross(desiredDirection, Vector3.up);
            body.AddTorque(
                torqueAxis * drive * settings.PlayerBallRadius * 0.72f,
                ForceMode.Acceleration);

            var planarVelocity = new Vector3(body.linearVelocity.x, 0f, body.linearVelocity.z);
            var maximumSpeed = settings.PlayerMaximumSpeed * Mathf.Lerp(1f, 0.34f, (float)CurrentSurface.Resistance);
            if (planarVelocity.magnitude > maximumSpeed)
            {
                var limited = planarVelocity.normalized * maximumSpeed;
                body.linearVelocity = new Vector3(limited.x, body.linearVelocity.y, limited.z);
            }
        }

        private void ApplySurfaceResistance()
        {
            if (!IsGrounded)
            {
                return;
            }

            var planarVelocity = new Vector3(body.linearVelocity.x, 0f, body.linearVelocity.z);
            if (planarVelocity.sqrMagnitude < 0.0001f)
            {
                return;
            }

            var resistance = (float)CurrentSurface.Resistance;
            body.AddForce(
                -planarVelocity * Mathf.Lerp(0.35f, 4.8f, resistance),
                ForceMode.Acceleration);
            body.AddTorque(
                -body.angularVelocity * Mathf.Lerp(0.02f, 0.7f, resistance),
                ForceMode.Acceleration);
        }

        private void UpdateVisualSink()
        {
            if (visualTransform == null)
            {
                return;
            }

            // The rigidbody rotates, so a local downward offset would orbit around
            // the sphere. Keep depression aligned to world gravity while preserving
            // the visual child's inherited rolling rotation.
            var target = transform.position - Vector3.up * (float)CurrentSurface.SinkDepth;
            visualTransform.position = Vector3.Lerp(
                visualTransform.position,
                target,
                1f - Mathf.Exp(-UnityEngine.Time.fixedDeltaTime * 6f));
        }

        private void OnCollisionStay(Collision collision)
        {
            for (var index = 0; index < collision.contactCount; index++)
            {
                if (collision.GetContact(index).normal.y > 0.28f)
                {
                    lastGroundContactTime = UnityEngine.Time.fixedTime;
                    return;
                }
            }
        }
    }
}
