using UnityEngine;

namespace Steppe.Caravan
{
    public enum CaravanPartKind
    {
        Sail,
        PhotovoltaicLeaves,
        Battery,
        WaterReservoir,
        DualModePump,
        Radiator,
        Biofurnace,
        BiofuelEngine,
        ElectricMotor,
        Harvester,
        GrassDryer,
        BiomassStorage,
        Transmission,
        CouplingRope,
        ResourceCrate
    }

    /// <summary>
    /// Identifies a physical caravan part and owns its generic resource state.
    /// The electrical network uses capacity, storage and current output directly;
    /// later water and biomass networks can reuse the same contract.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CaravanModule))]
    public sealed class CaravanPart : MonoBehaviour
    {
        public CaravanPartKind Kind { get; private set; }
        public float Capacity { get; private set; }
        public float StoredAmount { get; private set; }
        public float TemperatureCelsius { get; private set; } = 18f;
        public float CurrentOutput { get; private set; }

        public void Configure(
            CaravanPartKind kind,
            float capacity = 0f,
            float initialStoredAmount = 0f,
            float initialTemperatureCelsius = 18f)
        {
            Kind = kind;
            Capacity = Mathf.Max(0f, capacity);
            StoredAmount = Mathf.Clamp(initialStoredAmount, 0f, Capacity);
            TemperatureCelsius = initialTemperatureCelsius;
        }

        public void SetStoredAmount(float amount)
        {
            StoredAmount = Mathf.Clamp(amount, 0f, Capacity);
        }

        public void SetTemperature(float temperatureCelsius)
        {
            TemperatureCelsius = temperatureCelsius;
        }

        public void SetCurrentOutput(float output)
        {
            CurrentOutput = Mathf.Max(0f, output);
        }
    }

    /// <summary>
    /// Converts the real sun direction and the local storm shadow into instantaneous
    /// electrical output for the caravan electrical network.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CaravanPart))]
    [RequireComponent(typeof(CaravanModule))]
    public sealed class CaravanPhotovoltaicModule :
        MonoBehaviour,
        ICaravanElectricalGenerator,
        ICaravanControlTarget
    {
        private CaravanEnvironmentSampler environment;
        private CaravanPart part;
        private CaravanModule module;
        private CaravanFluidNetwork fluidNetwork;
        private Transform[] panelSurfaces = System.Array.Empty<Transform>();
        private Quaternion[] panelBaseRotations = System.Array.Empty<Quaternion>();

        public float CurrentGenerationKilowatts { get; private set; }
        public CaravanPart ElectricalPart => part;
        public float AvailableGenerationKilowatts => CurrentGenerationKilowatts;
        public float CurrentIncidence { get; private set; }
        public float CurrentCloudTransmission { get; private set; } = 1f;
        public float CurrentCloudShadow => 1f - CurrentCloudTransmission;
        public float ControlNormalized { get; private set; }

        public void Configure(CaravanEnvironmentSampler sampler)
        {
            environment = sampler != null ? sampler : throw new System.ArgumentNullException(nameof(sampler));
            part = GetComponent<CaravanPart>();
            module = GetComponent<CaravanModule>();
            fluidNetwork = GetComponentInParent<CaravanFluidNetwork>();

            var descendants = GetComponentsInChildren<Transform>(true);
            var panels = new System.Collections.Generic.List<Transform>(4);
            for (var index = 0; index < descendants.Length; index++)
            {
                if (descendants[index].name.StartsWith(
                        "Solar Leaf",
                        System.StringComparison.Ordinal))
                {
                    panels.Add(descendants[index]);
                }
            }
            panelSurfaces = panels.ToArray();
            panelBaseRotations = new Quaternion[panelSurfaces.Length];
            for (var index = 0; index < panelSurfaces.Length; index++)
            {
                panelBaseRotations[index] = panelSurfaces[index].localRotation;
            }
            SetControlNormalized(ControlNormalized);
            EvaluateGeneration();
        }

        public void SetControlNormalized(float value)
        {
            ControlNormalized = Mathf.Clamp(value, -1f, 1f);
            var yaw = ControlNormalized * 80f;
            for (var index = 0; index < panelSurfaces.Length; index++)
            {
                panelSurfaces[index].localRotation =
                    Quaternion.Euler(0f, yaw, 0f)
                    * panelBaseRotations[index];
            }
        }

        private void Update()
        {
            if (environment != null)
            {
                EvaluateGeneration();
            }
        }

        private void EvaluateGeneration()
        {
            var incidenceSum = 0f;
            var cloudTransmissionSum = 0f;
            for (var index = 0; index < panelSurfaces.Length; index++)
            {
                var exposure = environment.SampleSolarExposure(
                    panelSurfaces[index].position,
                    panelSurfaces[index].up);
                incidenceSum += exposure.Incidence;
                cloudTransmissionSum += exposure.CloudTransmission;
            }

            var panelCount = panelSurfaces.Length;
            CurrentIncidence = panelCount > 0 ? incidenceSum / panelCount : 0f;
            CurrentCloudTransmission = panelCount > 0
                ? cloudTransmissionSum / panelCount
                : 1f;
            var daylightExposure = panelCount > 0
                ? environment.SampleSolarExposure(transform.position, Vector3.up)
                : default;
            var normalizedOutput = Mathf.Clamp01(
                (float)daylightExposure.Solar.Daylight
                * CurrentIncidence
                * CurrentCloudTransmission
                * module.State.Efficiency);
            var ambient = (float)environment.SampleAirTemperature(
                transform.position);
            var waterCooled = fluidNetwork != null
                              && fluidNetwork.IsPartConnected(part)
                              && fluidNetwork.CurrentFlowLitresPerSecond > 0.01f;
            var targetTemperature = ambient
                                    + normalizedOutput
                                    * (waterCooled ? 28f : 72f);
            part.SetTemperature(Mathf.MoveTowards(
                part.TemperatureCelsius,
                targetTemperature,
                UnityEngine.Time.deltaTime * (waterCooled ? 12f : 3.5f)));
            var thermalDerating = Mathf.Lerp(
                1f,
                0.52f,
                Mathf.InverseLerp(62f, 105f, part.TemperatureCelsius));
            normalizedOutput *= thermalDerating;
            CurrentGenerationKilowatts = part.Capacity * normalizedOutput;
            part.SetCurrentOutput(CurrentGenerationKilowatts);
            module.SetLoad(normalizedOutput);
        }
    }
}
