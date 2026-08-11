using System;
using System.Collections.Generic;
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
        private readonly List<CaravanElectricMotorModule> electricMotors =
            new List<CaravanElectricMotorModule>(2);
        private readonly List<ICaravanDriveSource> driveSources =
            new List<ICaravanDriveSource>(4);
        private readonly List<CaravanTransmissionModule> transmissions =
            new List<CaravanTransmissionModule>(2);
        private bool physicsStarted;
        private bool defaultDriveEnabled;
        private float electricDriveThrottle;
        private float steeringNormalized;
        private float brakeNormalized;
        private bool hasTravelSample;
        private double lastTravelWorldX;
        private double lastTravelWorldZ;
        private float travelledMetres;

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
        public float TravelledMetres => travelledMetres;
        public float CurrentAirTemperatureC { get; private set; }
        public float TrackRadius => 2.6f;
        public SteppeTraversalState CurrentSurface { get; private set; }
        public float SteeringNormalized => steeringNormalized;
        public float AppliedSteeringNormalized => vehicleInput != null
            ? vehicleInput.externalSteer
            : 0f;
        public float BrakeNormalized => brakeNormalized;
        public bool PhysicsStarted => physicsStarted;
        public bool DefaultDriveEnabled => defaultDriveEnabled;
        public float ElectricDriveThrottle => electricDriveThrottle;
        public float CurrentDriveForce { get; private set; }
        public IReadOnlyList<CaravanElectricMotorModule> ElectricMotors => electricMotors;
        public IReadOnlyList<ICaravanDriveSource> DriveSources => driveSources;
        public IReadOnlyList<CaravanTransmissionModule> Transmissions => transmissions;
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
            if (floatingOrigin != null)
            {
                floatingOrigin.Shifted -= HandleFloatingOriginShift;
            }
            settings = worldSettings != null ? worldSettings : throw new ArgumentNullException(nameof(worldSettings));
            floatingOrigin = origin != null ? origin : throw new ArgumentNullException(nameof(origin));
            floatingOrigin.Shifted += HandleFloatingOriginShift;
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
            vehicleInput.externalBrake = 0f;
            vehicleInput.externalHandbrake = 0f;
            vehicleInput.enabled = false;
            terrain = new TerrainHeightGenerator(settings);
            module = GetComponent<CaravanModule>();
            hasTravelSample = false;
            travelledMetres = 0f;
            CurrentAirTemperatureC = (float)environment.SampleAirTemperature(
                transform.position);
            RefreshMassProperties();
        }

        public void RestoreTravelledMetres(float value)
        {
            travelledMetres = Mathf.Max(0f, value);
            hasTravelSample = false;
        }

        public void SetSteeringNormalized(float value)
        {
            steeringNormalized = Mathf.Clamp(value, -1f, 1f);
            ApplySteeringCommand();
        }

        public void SetBrakeNormalized(float value)
        {
            brakeNormalized = Mathf.Clamp01(value);
            ApplyBrakeCommand();
        }

        public void SetDefaultDriveEnabled(bool enabled)
        {
            SetElectricDriveThrottle(enabled ? 1f : 0f);
        }

        public bool FitWheelsToPlatformBounds(
            float left,
            float right,
            float rear,
            float front)
        {
            if (wheels == null || wheels.Length != 4)
            {
                wheels = GetComponentsInChildren<VPWheelCollider>(true);
            }
            if (wheels.Length != 4)
            {
                return false;
            }

            for (var index = 0; index < wheels.Length; index++)
            {
                var wheel = wheels[index];
                if (wheel == null)
                {
                    return false;
                }

                var localPosition = transform.InverseTransformPoint(
                    wheel.transform.position);
                localPosition.x = localPosition.x < 0f ? left : right;
                localPosition.z = localPosition.z < 0f ? rear : front;
                wheel.transform.position = transform.TransformPoint(localPosition);
            }
            return true;
        }

        public void AttachElectricMotor(CaravanElectricMotorModule motor)
        {
            if (motor == null)
            {
                throw new ArgumentNullException(nameof(motor));
            }
            if (electricMotors.Contains(motor))
            {
                return;
            }

            electricMotors.Add(motor);
            if (!driveSources.Contains(motor))
            {
                driveSources.Add(motor);
            }
            motor.SetRequestedThrottle(0f);
        }

        public void AttachDriveSource(ICaravanDriveSource source)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }
            if (driveSources.Contains(source))
            {
                return;
            }

            driveSources.Add(source);
            source.SetRequestedThrottle(0f);
        }

        public void AttachTransmission(CaravanTransmissionModule transmission)
        {
            if (transmission != null && !transmissions.Contains(transmission))
            {
                transmissions.Add(transmission);
            }
        }

        public bool DetachElectricMotor(CaravanElectricMotorModule motor)
        {
            if (motor == null || !electricMotors.Remove(motor))
            {
                return false;
            }

            motor.SetRequestedThrottle(0f);
            motor.ApplyDeliveredPower(0f);
            driveSources.Remove(motor);
            return true;
        }

        public bool DetachDriveSource(ICaravanDriveSource source)
        {
            if (source == null || !driveSources.Remove(source))
            {
                return false;
            }

            source.SetRequestedThrottle(0f);
            if (source is CaravanElectricMotorModule motor)
            {
                electricMotors.Remove(motor);
                motor.ApplyDeliveredPower(0f);
            }
            return true;
        }

        public bool DetachTransmission(CaravanTransmissionModule transmission)
        {
            return transmission != null && transmissions.Remove(transmission);
        }

        public void SetElectricDriveThrottle(float normalizedThrottle)
        {
            electricDriveThrottle = Mathf.Clamp01(normalizedThrottle);
            defaultDriveEnabled = electricDriveThrottle > 0.001f;
            if (!defaultDriveEnabled)
            {
                for (var index = 0; index < driveSources.Count; index++)
                {
                    if (IsAlive(driveSources[index]))
                    {
                        driveSources[index].SetRequestedThrottle(0f);
                    }
                }
            }

            if (vehicleToolkit == null || !physicsStarted)
            {
                return;
            }

            if (defaultDriveEnabled)
            {
                vehicleToolkit.StartEngine();
                vehicleToolkit.SetAutomaticModeD();
            }
            else
            {
                CurrentDriveForce = 0f;
                VPVehicleToolkit.SetThrottle(vehicle, 0f);
                vehicleToolkit.SetAutomaticModeN();
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
            var rotation = body != null ? body.rotation : transform.rotation;
            Teleport(localPosition, rotation);
        }

        public void Teleport(Vector3 localPosition, Quaternion localRotation)
        {
            var previousPosition = transform.position;
            hasTravelSample = false;
            if (body == null)
            {
                transform.SetPositionAndRotation(localPosition, localRotation);
                Teleported?.Invoke(localPosition - previousPosition);
                return;
            }

            if (vehicle != null)
            {
                vehicle.HardReposition(localPosition, localRotation, true);
            }
            else
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
                body.position = localPosition;
                body.rotation = localRotation;
            }
            transform.SetPositionAndRotation(localPosition, localRotation);
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

            UpdateTravelMetrics();
            UpdateSurfaceState();
            ApplyVehicleInput();
        }

        private void UpdateTravelMetrics()
        {
            CurrentAirTemperatureC = (float)environment.SampleAirTemperature(
                transform.position);
            if (body.isKinematic)
            {
                hasTravelSample = false;
                return;
            }

            var world = floatingOrigin.LocalToWorld(transform.position);
            if (hasTravelSample)
            {
                var deltaX = world.X - lastTravelWorldX;
                var deltaZ = world.Z - lastTravelWorldZ;
                var distance = Math.Sqrt(deltaX * deltaX + deltaZ * deltaZ);
                // Teleports and restore operations reset the sample explicitly. The
                // upper guard also prevents a discontinuity from becoming mileage
                // if an external system repositions the rigidbody directly.
                if (distance <= settings.ChunkSize * 0.5f)
                {
                    travelledMetres += (float)distance;
                }
            }
            lastTravelWorldX = world.X;
            lastTravelWorldZ = world.Z;
            hasTravelSample = true;
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
            var torqueMultiplier = 0f;
            var speedMultiplier = 0f;
            var engagedTransmissionCount = 0;
            for (var index = transmissions.Count - 1; index >= 0; index--)
            {
                if (transmissions[index] == null)
                {
                    transmissions.RemoveAt(index);
                    continue;
                }

                if (!transmissions[index].IsEngaged)
                {
                    continue;
                }

                torqueMultiplier += transmissions[index].TorqueMultiplier;
                speedMultiplier += transmissions[index].SpeedMultiplier;
                engagedTransmissionCount++;
            }
            torqueMultiplier = engagedTransmissionCount > 0
                ? torqueMultiplier / engagedTransmissionCount
                : 1f;
            speedMultiplier = engagedTransmissionCount > 0
                ? speedMultiplier / engagedTransmissionCount
                : 1f;
            torqueMultiplier = Mathf.Clamp(torqueMultiplier, 0.6f, 1.8f);
            speedMultiplier = Mathf.Clamp(speedMultiplier, 0.6f, 1.4f);
            var maximumSpeed =
                Mathf.Lerp(8.5f, 4.2f, resistance) * speedMultiplier;
            var baseThrottle = Speed < maximumSpeed
                ? Mathf.Lerp(0.55f, 0.7f, resistance) * condition
                : 0f;
            var totalRequestedPower = 0f;
            var totalDeliveredPower = 0f;
            var maximumRequestedThrottle = 0f;
            for (var index = driveSources.Count - 1; index >= 0; index--)
            {
                var currentSource = driveSources[index];
                if (!IsAlive(currentSource))
                {
                    driveSources.RemoveAt(index);
                    continue;
                }

                var sourceControl = electricDriveThrottle;
                if (currentSource is CaravanBiofuelEngineModule engine)
                {
                    sourceControl = engine.IsDriveCoupled
                        ? engine.ManualThrottle
                        : 0f;
                }
                var sourceThrottle = baseThrottle * sourceControl;
                currentSource.SetRequestedThrottle(sourceThrottle);
                maximumRequestedThrottle = Mathf.Max(
                    maximumRequestedThrottle,
                    sourceThrottle);
                totalRequestedPower += currentSource.RequestedMechanicalKilowatts;
                totalDeliveredPower += currentSource.DeliveredMechanicalKilowatts;
            }

            var availablePower = totalRequestedPower > 0.001f
                ? Mathf.Clamp01(totalDeliveredPower / totalRequestedPower)
                : 0f;
            var throttle = Mathf.Clamp01(
                maximumRequestedThrottle
                * availablePower
                * torqueMultiplier
                * (1f - brakeNormalized));
            if (maximumRequestedThrottle > 0.001f
                && vehicleToolkit.isEngineStarted
                && vehicleToolkit.engagedGear == 0)
            {
                vehicleToolkit.SetAutomaticModeD();
                vehicleToolkit.SetGear(1);
            }
            else if (maximumRequestedThrottle > 0.001f
                     && !vehicleToolkit.isEngineStarted)
            {
                vehicleToolkit.StartEngine();
                vehicleToolkit.SetAutomaticModeD();
            }
            vehicleInput.externalThrottle = throttle;
            vehicleInput.externalHandbrake = 0f;
            ApplySteeringCommand();
            VPVehicleToolkit.SetThrottle(vehicle, throttle);
            ApplyBrakeCommand();
            VPVehicleToolkit.SetHandbrake(vehicle, 0f);
            vehicle.tireFriction.frictionMultiplier = Mathf.Lerp(0.95f, 0.64f, resistance);
            CurrentDriveForce = throttle * 1000f;
        }

        private void ApplySteeringCommand()
        {
            if (vehicleInput != null)
            {
                vehicleInput.externalSteer = steeringNormalized;
            }
            if (vehicle != null)
            {
                VPVehicleToolkit.SetSteering(vehicle, steeringNormalized);
            }
        }

        public void NotifyStructureCollidersChanged()
        {
            vehicle?.NotifyCollidersChanged();
        }

        private void ApplyBrakeCommand()
        {
            if (vehicleInput != null)
            {
                vehicleInput.externalBrake = brakeNormalized;
            }
            if (vehicle != null)
            {
                VPVehicleToolkit.SetBrake(vehicle, brakeNormalized);
            }
        }

        private static bool IsAlive(ICaravanDriveSource source)
        {
            return source != null
                   && (!(source is UnityEngine.Object unityObject)
                       || unityObject != null);
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
            var local = floatingOrigin.WorldToLocal(world.X, groundHeight + 1.35, world.Z);
            body.position = local;
            transform.position = local;
            Physics.SyncTransforms();
            body.isKinematic = false;
            physicsStarted = true;
            vehicleToolkit.SetAutomaticModeN();
            if (defaultDriveEnabled)
            {
                vehicleToolkit.StartEngine();
                vehicleToolkit.SetAutomaticModeD();
            }
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

        private void HandleFloatingOriginShift(Vector3 shift)
        {
            // VPP caches the vehicle's previous world-space position to derive
            // velocities and suspension state. A floating-origin shift moves the
            // Rigidbody without representing real motion, so the same delta must
            // be applied to that cache. Without this notification VPP interprets
            // every 2048 m recenter as an enormous lateral impact.
            vehicle?.NotifyPositionChanged(-shift);
        }

        private void OnDestroy()
        {
            if (floatingOrigin != null)
            {
                floatingOrigin.Shifted -= HandleFloatingOriginShift;
            }
        }

    }
}
