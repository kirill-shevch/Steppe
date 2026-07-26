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
        CouplingRope
    }

    /// <summary>
    /// Identifies a physical caravan part and reserves its future resource state.
    /// Networks do not consume these values yet; the component keeps the greybox
    /// hierarchy stable when water, electricity and biomass simulation arrives.
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
    /// electrical output. Storage and cable networks deliberately remain a later layer.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CaravanPart))]
    [RequireComponent(typeof(CaravanModule))]
    public sealed class CaravanPhotovoltaicModule : MonoBehaviour
    {
        private CaravanEnvironmentSampler environment;
        private CaravanPart part;
        private CaravanModule module;
        private Transform[] panelSurfaces = System.Array.Empty<Transform>();

        public float CurrentGenerationKilowatts { get; private set; }
        public float CurrentIncidence { get; private set; }
        public float CurrentCloudTransmission { get; private set; } = 1f;
        public float CurrentCloudShadow => 1f - CurrentCloudTransmission;

        public void Configure(CaravanEnvironmentSampler sampler)
        {
            environment = sampler != null ? sampler : throw new System.ArgumentNullException(nameof(sampler));
            part = GetComponent<CaravanPart>();
            module = GetComponent<CaravanModule>();

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
            EvaluateGeneration();
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
            CurrentGenerationKilowatts = part.Capacity * normalizedOutput;
            part.SetCurrentOutput(CurrentGenerationKilowatts);
            module.SetLoad(normalizedOutput);
        }
    }
}
