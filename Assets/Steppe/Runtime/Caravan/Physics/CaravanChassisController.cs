using System;
using Steppe.Player;
using Steppe.Settings;
using Steppe.Terrain;
using Steppe.World;
using UnityEngine;

namespace Steppe.Caravan
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class CaravanChassisController : MonoBehaviour, ISteppeTravelFocus
    {
        private SteppeWorldSettings settings;
        private FloatingOriginSystem floatingOrigin;
        private CaravanEnvironmentSampler environment;
        private Rigidbody body;
        private WheelCollider[] wheelColliders = Array.Empty<WheelCollider>();
        private Transform[] wheelVisuals = Array.Empty<Transform>();
        private TerrainHeightGenerator terrain;
        private TerrainChunkStreamer terrainStreamer;
        private CaravanModule module;
        private bool physicsStarted;
        private float steeringNormalized;

        public Rigidbody Body => body;
        public Transform FocusTransform => transform;
        public bool IsGrounded
        {
            get
            {
                if (!physicsStarted)
                {
                    return false;
                }

                for (var index = 0; index < wheelColliders.Length; index++)
                {
                    if (wheelColliders[index] != null && wheelColliders[index].isGrounded)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        public float Speed => body != null
            ? new Vector2(body.linearVelocity.x, body.linearVelocity.z).magnitude
            : 0f;
        public float TrackRadius => 2.6f;
        public SteppeTraversalState CurrentSurface { get; private set; }
        public float SteeringNormalized => steeringNormalized;
        public bool PhysicsStarted => physicsStarted;
        public event Action<Vector3> Teleported;

        public void Configure(
            SteppeWorldSettings worldSettings,
            FloatingOriginSystem origin,
            CaravanEnvironmentSampler environmentSampler,
            WheelCollider[] wheels,
            Transform[] visuals)
        {
            settings = worldSettings != null ? worldSettings : throw new ArgumentNullException(nameof(worldSettings));
            floatingOrigin = origin != null ? origin : throw new ArgumentNullException(nameof(origin));
            environment = environmentSampler ?? throw new ArgumentNullException(nameof(environmentSampler));
            wheelColliders = wheels ?? Array.Empty<WheelCollider>();
            wheelVisuals = visuals ?? Array.Empty<Transform>();
            body = GetComponent<Rigidbody>();
            body.useGravity = true;
            body.isKinematic = true;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            body.linearDamping = 0.08f;
            body.angularDamping = 1.15f;
            body.maxAngularVelocity = 2.5f;
            body.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
            terrain = new TerrainHeightGenerator(settings);
            module = GetComponent<CaravanModule>();
            RefreshMassProperties();
        }

        public void SetSteeringNormalized(float value)
        {
            steeringNormalized = Mathf.Clamp(value, -1f, 1f);
        }

        public void ApplySailForce(Vector3 force, Vector3 applicationPoint)
        {
            if (body == null || body.isKinematic)
            {
                SetSailRollingTorque(0f);
                return;
            }

            var planarForce = Vector3.ProjectOnPlane(force, Vector3.up);
            var forwardForceMagnitude = Vector3.Dot(planarForce, transform.forward);
            var longitudinalForce = transform.forward * forwardForceMagnitude;
            var lateralForce = planarForce - longitudinalForce;

            // WheelCollider does not reliably turn a freely rolling wheel from a force
            // applied to the chassis. Convert only the longitudinal part to the
            // equivalent rolling torque: sum(torque / radius) == sail force.
            SetSailRollingTorque(forwardForceMagnitude);
            body.AddForceAtPosition(lateralForce, applicationPoint, ForceMode.Force);

            var lever = applicationPoint - body.worldCenterOfMass;
            var yawTorque = Vector3.Cross(lever, longitudinalForce).y;
            body.AddTorque(Vector3.up * yawTorque, ForceMode.Force);
        }

        public void RefreshMassProperties()
        {
            if (body == null)
            {
                return;
            }

            var modules = GetComponentsInChildren<CaravanModule>(false);
            var totalMass = 0f;
            var weightedCenter = Vector3.zero;
            for (var index = 0; index < modules.Length; index++)
            {
                var caravanModule = modules[index];
                var mass = caravanModule.MassKilograms;
                var localCenter = transform.InverseTransformPoint(caravanModule.WorldMassCenter);
                totalMass += mass;
                weightedCenter += localCenter * mass;
            }

            if (totalMass < 1f)
            {
                totalMass = 1f;
                weightedCenter = new Vector3(0f, -0.5f, 0f);
            }

            body.mass = totalMass;
            body.centerOfMass = weightedCenter / totalMass;
            body.ResetInertiaTensor();
        }

        public void Teleport(Vector3 localPosition)
        {
            var previousPosition = transform.position;
            if (body == null)
            {
                transform.position = localPosition;
                Teleported?.Invoke(localPosition - previousPosition);
                return;
            }

            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.position = localPosition;
            body.rotation = Quaternion.identity;
            transform.SetPositionAndRotation(localPosition, Quaternion.identity);
            Physics.SyncTransforms();
            body.isKinematic = true;
            physicsStarted = false;
            CurrentSurface = default;
            Teleported?.Invoke(localPosition - previousPosition);
        }

        private void SetSailRollingTorque(float forwardForce)
        {
            var wheelCount = 0;
            for (var index = 0; index < wheelColliders.Length; index++)
            {
                if (wheelColliders[index] != null)
                {
                    wheelCount++;
                }
            }

            if (wheelCount == 0)
            {
                return;
            }

            for (var index = 0; index < wheelColliders.Length; index++)
            {
                var wheel = wheelColliders[index];
                if (wheel != null)
                {
                    wheel.motorTorque = forwardForce * wheel.radius / wheelCount;
                }
            }
        }

        private void FixedUpdate()
        {
            if (settings == null || body == null || floatingOrigin == null || environment == null)
            {
                return;
            }

            if (!physicsStarted)
            {
                TryBeginPhysicsOnLoadedTerrain();
                return;
            }

            UpdateSurfaceState();
            ApplySteeringAndSurface();
        }

        private void LateUpdate()
        {
            var count = Mathf.Min(wheelColliders.Length, wheelVisuals.Length);
            for (var index = 0; index < count; index++)
            {
                if (wheelColliders[index] == null || wheelVisuals[index] == null)
                {
                    continue;
                }

                wheelColliders[index].GetWorldPose(out var position, out var rotation);
                wheelVisuals[index].SetPositionAndRotation(position, rotation);
            }
        }

        private void TryBeginPhysicsOnLoadedTerrain()
        {
            terrainStreamer ??= FindAnyObjectByType<TerrainChunkStreamer>();
            var world = floatingOrigin.LocalToWorld(transform.position);
            if (terrainStreamer == null || !terrainStreamer.HasPhysicsSurfaceAt(world.X, world.Z))
            {
                return;
            }

            var groundHeight = terrain.SampleHeight(world.X, world.Z);
            var local = floatingOrigin.WorldToLocal(world.X, groundHeight + 1.05, world.Z);
            body.position = local;
            transform.position = local;
            Physics.SyncTransforms();
            body.isKinematic = false;
            physicsStarted = true;
        }

        private void UpdateSurfaceState()
        {
            if (environment.TrySample(transform.position, out var sample))
            {
                CurrentSurface = sample.Traversal;
                if (module != null)
                {
                    module.SetLoad((float)sample.Traversal.Resistance);
                    module.AccumulateDust(
                        (float)sample.Dust.Emission
                        * UnityEngine.Time.fixedDeltaTime
                        * 0.0016f);
                    if (Speed > 7f && sample.Traversal.Resistance > 0.72)
                    {
                        module.Damage(
                            (float)(sample.Traversal.Resistance - 0.72)
                            * UnityEngine.Time.fixedDeltaTime
                            * 0.0006f);
                    }
                }
            }
        }

        private void ApplySteeringAndSurface()
        {
            var steerAngle = steeringNormalized * 30f;
            if (wheelColliders.Length > 0 && wheelColliders[0] != null)
            {
                wheelColliders[0].steerAngle = steerAngle;
            }
            if (wheelColliders.Length > 1 && wheelColliders[1] != null)
            {
                wheelColliders[1].steerAngle = steerAngle;
            }

            var resistance = (float)CurrentSurface.Resistance;
            var sidewaysStiffness = Mathf.Lerp(1.65f, 0.48f, resistance);
            for (var index = 0; index < wheelColliders.Length; index++)
            {
                var wheel = wheelColliders[index];
                if (wheel == null)
                {
                    continue;
                }

                var sideways = wheel.sidewaysFriction;
                sideways.stiffness = sidewaysStiffness;
                wheel.sidewaysFriction = sideways;
                var forward = wheel.forwardFriction;
                forward.stiffness = Mathf.Lerp(1.25f, 0.7f, resistance);
                wheel.forwardFriction = forward;
            }

            var planarVelocity = Vector3.ProjectOnPlane(body.linearVelocity, Vector3.up);
            if (planarVelocity.sqrMagnitude > 0.001f)
            {
                body.AddForce(
                    -planarVelocity * Mathf.Lerp(18f, 210f, resistance),
                    ForceMode.Force);
            }

            var maximumSpeed = Mathf.Lerp(12f, 6.5f, resistance);
            if (planarVelocity.magnitude > maximumSpeed)
            {
                var limited = planarVelocity.normalized * maximumSpeed;
                body.linearVelocity = new Vector3(limited.x, body.linearVelocity.y, limited.z);
            }
        }
    }
}
