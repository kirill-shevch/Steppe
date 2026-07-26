using System;
using Steppe.Player;
using Steppe.Settings;
using Steppe.Terrain;
using Steppe.World;
using UnityEngine;
using VehiclePhysics;

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
        private TerrainHeightGenerator terrain;
        private TerrainChunkStreamer terrainStreamer;
        private CaravanModule module;
        private VPVehicleController vehicle;
        private VPStandardInput vehicleInput;
        private VPVehicleToolkit vehicleToolkit;
        private VPWheelCollider[] wheels;
        private bool physicsStarted;
        private bool defaultDriveEnabled = true;
        private float steeringNormalized;

        public Rigidbody Body => body;
        public Transform FocusTransform => transform;
        public bool IsGrounded
        {
            get
            {
                if (!physicsStarted || body == null || body.isKinematic || wheels == null)
                {
                    return false;
                }

                for (var index = 0; index < wheels.Length; index++)
                {
                    if (wheels[index] != null && wheels[index].visualGrounded)
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
        public bool DefaultDriveEnabled => defaultDriveEnabled;
        public float CurrentDriveForce { get; private set; }
        public bool VehicleEngineStarted => vehicleToolkit != null && vehicleToolkit.isEngineStarted;
        public int VehicleEngagedGear => vehicleToolkit != null ? vehicleToolkit.engagedGear : 0;
        public int GroundedWheelCount
        {
            get
            {
                var count = 0;
                if (wheels == null)
                {
                    return count;
                }

                for (var index = 0; index < wheels.Length; index++)
                {
                    if (wheels[index] != null && wheels[index].visualGrounded)
                    {
                        count++;
                    }
                }

                return count;
            }
        }
        public event Action<Vector3> Teleported;

        public void Configure(
            SteppeWorldSettings worldSettings,
            FloatingOriginSystem origin,
            CaravanEnvironmentSampler environmentSampler)
        {
            settings = worldSettings != null ? worldSettings : throw new ArgumentNullException(nameof(worldSettings));
            floatingOrigin = origin != null ? origin : throw new ArgumentNullException(nameof(origin));
            environment = environmentSampler ?? throw new ArgumentNullException(nameof(environmentSampler));
            body = GetComponent<Rigidbody>();
            vehicle = GetComponent<VPVehicleController>();
            vehicleInput = GetComponent<VPStandardInput>();
            vehicleToolkit = GetComponent<VPVehicleToolkit>();
            wheels = GetComponentsInChildren<VPWheelCollider>(true);
            if (vehicle == null || vehicleInput == null || vehicleToolkit == null
                || wheels.Length != 4)
            {
                throw new InvalidOperationException(
                    "The caravan chassis requires a configured VPP four-wheel physics core.");
            }

            body.useGravity = true;
            body.isKinematic = true;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            body.linearDamping = 0.02f;
            body.angularDamping = 0.35f;
            body.maxAngularVelocity = 4f;
            body.constraints = RigidbodyConstraints.None;
            vehicleInput.externalSteer = 0f;
            vehicleInput.externalThrottle = 0f;
            vehicleInput.externalBrake = 1f;
            vehicleInput.externalHandbrake = 0f;
            vehicleInput.enabled = false;
            terrain = new TerrainHeightGenerator(settings);
            module = GetComponent<CaravanModule>();
            RefreshMassProperties();
        }

        public void SetSteeringNormalized(float value)
        {
            steeringNormalized = Mathf.Clamp(value, -1f, 1f);
        }

        public void SetDefaultDriveEnabled(bool enabled)
        {
            defaultDriveEnabled = enabled;
            if (!enabled)
            {
                CurrentDriveForce = 0f;
            }
        }

        public void ApplySailForce(Vector3 force, Vector3 applicationPoint)
        {
            if (body == null || body.isKinematic)
            {
                return;
            }

            var planarForce = Vector3.ProjectOnPlane(force, Vector3.up);
            body.AddForceAtPosition(planarForce, applicationPoint, ForceMode.Force);
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
            body.ResetInertiaTensor();
            body.centerOfMass = weightedCenter / totalMass;
            if (vehicle != null && vehicle.centerOfMass != null)
            {
                vehicle.centerOfMass.position = transform.TransformPoint(body.centerOfMass);
            }
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

            if (vehicle != null)
            {
                vehicle.HardReposition(localPosition, Quaternion.identity, true);
            }
            else
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
                body.position = localPosition;
                body.rotation = Quaternion.identity;
                transform.SetPositionAndRotation(localPosition, Quaternion.identity);
            }
            Physics.SyncTransforms();
            body.isKinematic = true;
            physicsStarted = false;
            CurrentDriveForce = 0f;
            CurrentSurface = default;
            Teleported?.Invoke(localPosition - previousPosition);
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
            ApplyVehicleInput();
        }

        private void ApplyVehicleInput()
        {
            if (body.isKinematic || vehicleInput == null)
            {
                CurrentDriveForce = 0f;
                return;
            }

            var resistance = (float)CurrentSurface.Resistance;
            var condition = module != null ? module.State.Efficiency : 1f;
            var maximumSpeed = Mathf.Lerp(8.5f, 4.2f, resistance);
            var throttle = defaultDriveEnabled && Speed < maximumSpeed
                ? Mathf.Lerp(0.55f, 0.7f, resistance) * condition
                : 0f;
            if (vehicleToolkit.isEngineStarted && vehicleToolkit.engagedGear == 0)
            {
                vehicleToolkit.SetAutomaticModeD();
                vehicleToolkit.SetGear(1);
            }
            vehicleInput.externalSteer = steeringNormalized;
            vehicleInput.externalThrottle = throttle;
            vehicleInput.externalBrake = defaultDriveEnabled
                ? 0f
                : (Speed > 0.15f ? 0.35f : 1f);
            vehicleInput.externalHandbrake = 0f;
            VPVehicleToolkit.SetSteering(vehicle, steeringNormalized);
            VPVehicleToolkit.SetThrottle(vehicle, throttle);
            VPVehicleToolkit.SetBrake(vehicle, vehicleInput.externalBrake);
            VPVehicleToolkit.SetHandbrake(vehicle, 0f);
            vehicle.tireFriction.frictionMultiplier = Mathf.Lerp(0.95f, 0.64f, resistance);
            CurrentDriveForce = throttle * 1000f;
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
            vehicleToolkit.StartEngine();
            vehicleToolkit.SetAutomaticModeD();
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

    }
}
