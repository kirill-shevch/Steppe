using System;
using System.Collections.Generic;
using Steppe.Ecology;
using UnityEngine;

namespace Steppe.Caravan
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CaravanPart))]
    [RequireComponent(typeof(CaravanModule))]
    public sealed class CaravanHarvesterModule :
        MonoBehaviour,
        ICaravanElectricalConsumer,
        ICaravanControlTarget
    {
        private CaravanPart part;
        private CaravanModule module;
        private bool processDemand;
        private float mechanicalAvailability;
        private float storedDryMatterKilograms;
        private float storedWaterKilograms;

        public CaravanPart ElectricalPart => part;
        public float ControlNormalized { get; private set; } = -1f;
        public float OperatingLevel =>
            CaravanControlModel.ToOperatingLevel(ControlNormalized);
        public float MaximumElectricalPowerKilowatts { get; private set; } = 14f;
        public float RequestedPowerKilowatts =>
            processDemand
                ? MaximumElectricalPowerKilowatts
                  * OperatingLevel
                  * module.State.Efficiency
                  * (1f - mechanicalAvailability)
                : 0f;
        public float DeliveredElectricalKilowatts { get; private set; }
        public float PowerAvailability { get; private set; }
        public float StoredWetBiomassKilograms =>
            storedDryMatterKilograms + storedWaterKilograms;
        public float MoistureFraction =>
            StoredWetBiomassKilograms > 0.001f
                ? storedWaterKilograms / StoredWetBiomassKilograms
                : 0f;
        public float CurrentHarvestKilogramsPerSecond { get; private set; }
        public float TotalHarvestedKilograms { get; private set; }

        public void Configure(float maximumElectricalPowerKilowatts = 14f)
        {
            part = GetComponent<CaravanPart>();
            module = GetComponent<CaravanModule>();
            MaximumElectricalPowerKilowatts = Mathf.Max(
                0.1f,
                maximumElectricalPowerKilowatts);
            SetControlNormalized(-1f);
            RefreshState();
        }

        public void SetControlNormalized(float value)
        {
            ControlNormalized = CaravanControlModel.Clamp(value);
        }

        public void SetProcessDemand(bool active)
        {
            processDemand = active && OperatingLevel > 0.001f;
            if (!processDemand)
            {
                ApplyDeliveredPower(0f);
                SetHarvestRate(0f);
            }
        }

        public void SetMechanicalPowerAvailability(float availability)
        {
            mechanicalAvailability = Mathf.Clamp01(availability);
            RefreshPowerAvailability();
        }

        public void ApplyDeliveredPower(float electricalKilowatts)
        {
            DeliveredElectricalKilowatts = Mathf.Clamp(
                electricalKilowatts,
                0f,
                RequestedPowerKilowatts);
            RefreshPowerAvailability();
        }

        public void AcceptHarvest(
            float dryMatterKilograms,
            float waterKilograms)
        {
            var capacity = Mathf.Max(0f, part.Capacity);
            var room = Mathf.Max(
                0f,
                capacity - StoredWetBiomassKilograms);
            var incoming = Mathf.Max(0f, dryMatterKilograms)
                           + Mathf.Max(0f, waterKilograms);
            if (incoming <= 0.0001f || room <= 0f)
            {
                return;
            }

            var acceptedFraction = Mathf.Min(1f, room / incoming);
            var acceptedDry =
                Mathf.Max(0f, dryMatterKilograms) * acceptedFraction;
            var acceptedWater =
                Mathf.Max(0f, waterKilograms) * acceptedFraction;
            storedDryMatterKilograms += acceptedDry;
            storedWaterKilograms += acceptedWater;
            TotalHarvestedKilograms += acceptedDry + acceptedWater;
            RefreshState();
        }

        public CaravanBiomassConversion TransferWet(float maximumKilograms)
        {
            var transferred = Mathf.Min(
                StoredWetBiomassKilograms,
                Mathf.Max(0f, maximumKilograms));
            if (transferred <= 0f)
            {
                return default;
            }

            var moisture = MoistureFraction;
            storedDryMatterKilograms -= transferred * (1f - moisture);
            storedWaterKilograms -= transferred * moisture;
            RefreshState();
            return new CaravanBiomassConversion(
                transferred,
                transferred * (1f - moisture),
                transferred * moisture);
        }

        public void SetHarvestRate(float kilogramsPerSecond)
        {
            CurrentHarvestKilogramsPerSecond = Mathf.Max(
                0f,
                kilogramsPerSecond);
            part.SetCurrentOutput(CurrentHarvestKilogramsPerSecond);
            RefreshState();
        }

        private void RefreshPowerAvailability()
        {
            var electricalAvailability = RequestedPowerKilowatts > 0.001f
                ? DeliveredElectricalKilowatts / RequestedPowerKilowatts
                : 0f;
            PowerAvailability = Mathf.Clamp01(
                Mathf.Max(mechanicalAvailability, electricalAvailability));
            RefreshState();
        }

        private void RefreshState()
        {
            part.SetStoredAmount(StoredWetBiomassKilograms);
            module.SetPayloadMass(StoredWetBiomassKilograms);
            module.SetLoad(Mathf.Max(
                part.Capacity > 0f
                    ? StoredWetBiomassKilograms / part.Capacity
                    : 0f,
                PowerAvailability * OperatingLevel));
        }
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(CaravanPart))]
    [RequireComponent(typeof(CaravanModule))]
    public sealed class CaravanGrassDryerModule :
        MonoBehaviour,
        ICaravanElectricalConsumer,
        ICaravanControlTarget
    {
        private CaravanPart part;
        private CaravanModule module;
        private bool processDemand;
        private float wetDryMatterKilograms;
        private float wetWaterKilograms;

        public CaravanPart ElectricalPart => part;
        public float ControlNormalized { get; private set; } = -1f;
        public float OperatingLevel =>
            CaravanControlModel.ToOperatingLevel(ControlNormalized);
        public float MaximumElectricalPowerKilowatts { get; private set; } = 11f;
        public float MaximumWetThroughputKilogramsPerSecond { get; private set; } =
            0.85f;
        public float RequestedPowerKilowatts =>
            processDemand
                ? MaximumElectricalPowerKilowatts
                  * OperatingLevel
                  * module.State.Efficiency
                : 0f;
        public float DeliveredElectricalKilowatts { get; private set; }
        public float PowerAvailability { get; private set; }
        public float StoredWetBiomassKilograms =>
            wetDryMatterKilograms + wetWaterKilograms;
        public float MoistureFraction =>
            StoredWetBiomassKilograms > 0.001f
                ? wetWaterKilograms / StoredWetBiomassKilograms
                : 0f;
        public float DryOutputKilograms { get; private set; }
        public float CurrentDryingKilogramsPerSecond { get; private set; }
        public float TotalDriedWetKilograms { get; private set; }

        public void Configure(
            float maximumElectricalPowerKilowatts = 11f,
            float maximumWetThroughputKilogramsPerSecond = 0.85f)
        {
            part = GetComponent<CaravanPart>();
            module = GetComponent<CaravanModule>();
            MaximumElectricalPowerKilowatts = Mathf.Max(
                0.1f,
                maximumElectricalPowerKilowatts);
            MaximumWetThroughputKilogramsPerSecond = Mathf.Max(
                0.01f,
                maximumWetThroughputKilogramsPerSecond);
            SetControlNormalized(-1f);
            RefreshState();
        }

        public void SetControlNormalized(float value)
        {
            ControlNormalized = CaravanControlModel.Clamp(value);
        }

        public void SetProcessDemand(bool active)
        {
            processDemand = active
                            && OperatingLevel > 0.001f
                            && StoredWetBiomassKilograms > 0.001f;
            if (!processDemand)
            {
                ApplyDeliveredPower(0f);
            }
        }

        public void ApplyDeliveredPower(float electricalKilowatts)
        {
            DeliveredElectricalKilowatts = Mathf.Clamp(
                electricalKilowatts,
                0f,
                RequestedPowerKilowatts);
            PowerAvailability = RequestedPowerKilowatts > 0.001f
                ? Mathf.Clamp01(
                    DeliveredElectricalKilowatts / RequestedPowerKilowatts)
                : 0f;
            RefreshState();
        }

        public void AcceptWet(CaravanBiomassConversion wetMaterial)
        {
            var incoming = wetMaterial.DryBiomassKilograms
                           + wetMaterial.RecoveredWaterLitres;
            var room = Mathf.Max(
                0f,
                part.Capacity
                - StoredWetBiomassKilograms
                - DryOutputKilograms);
            if (incoming <= 0f || room <= 0f)
            {
                return;
            }

            var fraction = Mathf.Min(1f, room / incoming);
            wetDryMatterKilograms +=
                wetMaterial.DryBiomassKilograms * fraction;
            wetWaterKilograms +=
                wetMaterial.RecoveredWaterLitres * fraction;
            RefreshState();
        }

        public CaravanBiomassConversion SimulateDrying(
            float passiveDryingAvailability,
            float deltaTimeSeconds)
        {
            var activeAvailability =
                PowerAvailability * OperatingLevel * module.State.Efficiency;
            var result = CaravanBiomassModel.Dry(
                StoredWetBiomassKilograms,
                MoistureFraction,
                MaximumWetThroughputKilogramsPerSecond,
                activeAvailability,
                passiveDryingAvailability,
                deltaTimeSeconds);
            if (result.ProcessedWetKilograms <= 0f)
            {
                CurrentDryingKilogramsPerSecond = 0f;
                part.SetCurrentOutput(0f);
                RefreshState();
                return default;
            }

            wetDryMatterKilograms = Mathf.Max(
                0f,
                wetDryMatterKilograms - result.DryBiomassKilograms);
            wetWaterKilograms = Mathf.Max(
                0f,
                wetWaterKilograms - result.RecoveredWaterLitres);
            DryOutputKilograms += result.DryBiomassKilograms;
            TotalDriedWetKilograms += result.ProcessedWetKilograms;
            CurrentDryingKilogramsPerSecond =
                result.ProcessedWetKilograms
                / Mathf.Max(0.001f, deltaTimeSeconds);
            part.SetCurrentOutput(CurrentDryingKilogramsPerSecond);
            RefreshState();
            return result;
        }

        public float TransferDry(float maximumKilograms)
        {
            var transferred = Mathf.Min(
                DryOutputKilograms,
                Mathf.Max(0f, maximumKilograms));
            DryOutputKilograms -= transferred;
            RefreshState();
            return transferred;
        }

        public void ReturnDry(float kilograms)
        {
            var room = Mathf.Max(
                0f,
                part.Capacity
                - StoredWetBiomassKilograms
                - DryOutputKilograms);
            DryOutputKilograms += Mathf.Min(
                Mathf.Max(0f, kilograms),
                room);
            RefreshState();
        }

        private void RefreshState()
        {
            var stored = StoredWetBiomassKilograms + DryOutputKilograms;
            part.SetStoredAmount(stored);
            module.SetPayloadMass(stored);
            module.SetLoad(Mathf.Max(
                part.Capacity > 0f ? stored / part.Capacity : 0f,
                PowerAvailability * OperatingLevel));
        }
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(CaravanPart))]
    [RequireComponent(typeof(CaravanModule))]
    public sealed class CaravanBiomassStorageModule : MonoBehaviour
    {
        private CaravanPart part;
        private CaravanModule module;

        public float StoredDryBiomassKilograms =>
            part != null ? part.StoredAmount : 0f;
        public float CapacityKilograms => part != null ? part.Capacity : 0f;

        public void Configure()
        {
            part = GetComponent<CaravanPart>();
            module = GetComponent<CaravanModule>();
            RefreshState();
        }

        public float Accept(float kilograms)
        {
            var accepted = Mathf.Min(
                Mathf.Max(0f, kilograms),
                Mathf.Max(
                    0f,
                    CapacityKilograms - StoredDryBiomassKilograms));
            part.SetStoredAmount(StoredDryBiomassKilograms + accepted);
            RefreshState();
            return accepted;
        }

        public float Supply(float kilograms)
        {
            var supplied = Mathf.Min(
                Mathf.Max(0f, kilograms),
                StoredDryBiomassKilograms);
            part.SetStoredAmount(StoredDryBiomassKilograms - supplied);
            RefreshState();
            return supplied;
        }

        private void RefreshState()
        {
            module.SetPayloadMass(StoredDryBiomassKilograms);
            module.SetLoad(CapacityKilograms > 0f
                ? StoredDryBiomassKilograms / CapacityKilograms
                : 0f);
            part.SetCurrentOutput(0f);
        }
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(CaravanPart))]
    [RequireComponent(typeof(CaravanModule))]
    public sealed class CaravanBiofurnaceModule :
        MonoBehaviour,
        ICaravanControlTarget
    {
        private CaravanPart part;
        private CaravanModule module;

        public float ControlNormalized { get; private set; } = -1f;
        public float OperatingLevel =>
            CaravanControlModel.ToOperatingLevel(ControlNormalized);
        public float MaximumThermalKilowatts =>
            part != null ? part.Capacity : 0f;
        public float ThermalOutputKilowatts { get; private set; }
        public float CurrentFuelKilogramsPerSecond { get; private set; }
        public float TotalFuelConsumedKilograms { get; private set; }

        public void Configure()
        {
            part = GetComponent<CaravanPart>();
            module = GetComponent<CaravanModule>();
            SetControlNormalized(-1f);
            SimulateFuel(0f, 1f);
        }

        public void SetControlNormalized(float value)
        {
            ControlNormalized = CaravanControlModel.Clamp(value);
        }

        public float RequestedFuelKilograms(float deltaTimeSeconds)
        {
            return CaravanFuelModel.RequiredFuelKilograms(
                MaximumThermalKilowatts * OperatingLevel,
                CaravanFuelModel.FurnaceEfficiency,
                deltaTimeSeconds);
        }

        public void SimulateFuel(
            float suppliedFuelKilograms,
            float deltaTimeSeconds)
        {
            var seconds = Mathf.Max(0.001f, deltaTimeSeconds);
            var flow = CaravanFuelModel.Evaluate(
                suppliedFuelKilograms,
                seconds,
                CaravanFuelModel.FurnaceEfficiency,
                MaximumThermalKilowatts * OperatingLevel,
                module.State.Efficiency);
            CurrentFuelKilogramsPerSecond =
                flow.ConsumedFuelKilograms / seconds;
            TotalFuelConsumedKilograms += flow.ConsumedFuelKilograms;
            ThermalOutputKilowatts = flow.UsefulOutputKilowatts;
            part.SetCurrentOutput(ThermalOutputKilowatts);
            part.SetTemperature(Mathf.Lerp(
                18f,
                540f,
                MaximumThermalKilowatts > 0f
                    ? ThermalOutputKilowatts / MaximumThermalKilowatts
                    : 0f));
            module.SetLoad(MaximumThermalKilowatts > 0f
                ? ThermalOutputKilowatts / MaximumThermalKilowatts
                : 0f);
        }
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(CaravanPart))]
    [RequireComponent(typeof(CaravanModule))]
    public sealed class CaravanBiofuelEngineModule :
        MonoBehaviour,
        ICaravanDriveSource,
        ICaravanControlTarget
    {
        private CaravanPart part;
        private CaravanModule module;
        private float requestedThrottle;

        public float ControlNormalized { get; private set; } = -1f;
        public float FuelControl =>
            CaravanControlModel.ToOperatingLevel(ControlNormalized);
        public float ManualThrottle => FuelControl;
        public bool IsDriveCoupled { get; private set; }
        public float MaximumPowerKilowatts =>
            part != null ? part.Capacity : 0f;
        public float RequestedMechanicalKilowatts =>
            MaximumPowerKilowatts
            * requestedThrottle
            * FuelControl
            * module.State.Efficiency;
        public float DeliveredMechanicalKilowatts { get; private set; }
        public float WasteHeatKilowatts { get; private set; }
        public float CurrentFuelKilogramsPerSecond { get; private set; }
        public float TotalFuelConsumedKilograms { get; private set; }

        public void Configure()
        {
            part = GetComponent<CaravanPart>();
            module = GetComponent<CaravanModule>();
            SetControlNormalized(-1f);
            SetDriveCoupled(false);
            SetRequestedThrottle(0f);
            SimulateFuel(0f, 1f);
        }

        public void SetControlNormalized(float value)
        {
            ControlNormalized = CaravanControlModel.Clamp(value);
        }

        public void SetRequestedThrottle(float normalizedThrottle)
        {
            requestedThrottle = Mathf.Clamp01(normalizedThrottle);
        }

        public void SetDriveCoupled(bool coupled)
        {
            IsDriveCoupled = coupled;
            if (!coupled)
            {
                SetRequestedThrottle(0f);
            }
        }

        public float RequestedFuelKilograms(float deltaTimeSeconds)
        {
            return CaravanFuelModel.RequiredFuelKilograms(
                RequestedMechanicalKilowatts,
                CaravanFuelModel.EngineEfficiency,
                deltaTimeSeconds);
        }

        public void SimulateFuel(
            float suppliedFuelKilograms,
            float deltaTimeSeconds)
        {
            var seconds = Mathf.Max(0.001f, deltaTimeSeconds);
            var flow = CaravanFuelModel.Evaluate(
                suppliedFuelKilograms,
                seconds,
                CaravanFuelModel.EngineEfficiency,
                RequestedMechanicalKilowatts,
                wasteHeatFraction:
                CaravanFuelModel.EngineWasteHeatFraction);
            CurrentFuelKilogramsPerSecond =
                flow.ConsumedFuelKilograms / seconds;
            TotalFuelConsumedKilograms += flow.ConsumedFuelKilograms;
            DeliveredMechanicalKilowatts = flow.UsefulOutputKilowatts;
            WasteHeatKilowatts = flow.WasteHeatKilowatts;
            part.SetCurrentOutput(DeliveredMechanicalKilowatts);
            part.SetTemperature(Mathf.Lerp(
                18f,
                165f,
                MaximumPowerKilowatts > 0f
                    ? DeliveredMechanicalKilowatts / MaximumPowerKilowatts
                    : 0f));
            module.SetLoad(MaximumPowerKilowatts > 0f
                ? DeliveredMechanicalKilowatts / MaximumPowerKilowatts
                : 0f);
        }
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(CaravanPart))]
    [RequireComponent(typeof(CaravanModule))]
    public sealed class CaravanTransmissionModule :
        MonoBehaviour,
        ICaravanControlTarget
    {
        private CaravanPart part;
        private CaravanModule module;

        public float ControlNormalized { get; private set; }
        public bool IsEngaged { get; private set; }
        public float TorqueMultiplier =>
            Mathf.Lerp(1.65f, 0.72f, (ControlNormalized + 1f) * 0.5f);
        public float SpeedMultiplier =>
            Mathf.Lerp(0.62f, 1.38f, (ControlNormalized + 1f) * 0.5f);

        public void Configure()
        {
            part = GetComponent<CaravanPart>();
            module = GetComponent<CaravanModule>();
            SetControlNormalized(0f);
            SetEngaged(false);
        }

        public void SetControlNormalized(float value)
        {
            ControlNormalized = CaravanControlModel.Clamp(value);
            part.SetCurrentOutput(SpeedMultiplier);
            module.SetLoad(Mathf.Abs(ControlNormalized));
        }

        public void SetEngaged(bool engaged)
        {
            IsEngaged = engaged;
            part.SetCurrentOutput(engaged ? SpeedMultiplier : 0f);
            module.SetLoad(engaged
                ? Mathf.Max(0.15f, Mathf.Abs(ControlNormalized))
                : 0f);
        }
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(CaravanPart))]
    [RequireComponent(typeof(CaravanModule))]
    public sealed class CaravanCouplingRopeModule : MonoBehaviour
    {
        private CaravanPart part;
        private CaravanModule module;
        private CaravanCouplingRopeModule linkedRope;
        private Rigidbody attachedBody;

        public float SlackLengthMetres { get; private set; } = 12f;
        public float StiffnessNewtonsPerMetre { get; private set; } = 18000f;
        public float BreakingForceNewtons =>
            part != null ? part.Capacity * 1000f : 24000f;
        public float CurrentTensionNewtons { get; private set; }
        public bool IsBroken { get; private set; }
        public CaravanCouplingRopeModule LinkedRope => linkedRope;
        public bool IsLinked => linkedRope != null;

        public void Configure(
            float slackLengthMetres = 12f,
            float stiffnessNewtonsPerMetre = 18000f)
        {
            part = GetComponent<CaravanPart>();
            module = GetComponent<CaravanModule>();
            attachedBody = GetComponentInParent<Rigidbody>();
            SlackLengthMetres = Mathf.Max(0.5f, slackLengthMetres);
            StiffnessNewtonsPerMetre = Mathf.Max(
                100f,
                stiffnessNewtonsPerMetre);
            SetTension(0f, false);
        }

        public void LinkTo(CaravanCouplingRopeModule other)
        {
            if (other == this)
            {
                return;
            }

            linkedRope = other;
            attachedBody ??= GetComponentInParent<Rigidbody>();
            if (other != null && other.linkedRope != this)
            {
                other.LinkTo(this);
            }
            RepairRope();
        }

        public void Unlink()
        {
            var previous = linkedRope;
            linkedRope = null;
            SetTension(0f, false);
            if (previous != null && previous.linkedRope == this)
            {
                previous.linkedRope = null;
                previous.SetTension(0f, false);
            }
        }

        public CaravanRopeTension EvaluateDistance(float distanceMetres)
        {
            var sample = CaravanCouplingRopeModel.Evaluate(
                distanceMetres,
                SlackLengthMetres,
                StiffnessNewtonsPerMetre,
                BreakingForceNewtons);
            SetTension(sample.TensionNewtons, sample.Broken);
            return sample;
        }

        public void RepairRope()
        {
            IsBroken = false;
            SetTension(0f, false);
        }

        private void FixedUpdate()
        {
            if (linkedRope == null || IsBroken)
            {
                return;
            }

            attachedBody ??= GetComponentInParent<Rigidbody>();
            linkedRope.attachedBody ??=
                linkedRope.GetComponentInParent<Rigidbody>();
            var otherBody = linkedRope.attachedBody;
            if (attachedBody == null
                || otherBody == null
                || attachedBody == otherBody
                || string.CompareOrdinal(
                    module.InstanceId,
                    linkedRope.module.InstanceId) > 0)
            {
                return;
            }

            var offset = linkedRope.transform.position - transform.position;
            var sample = EvaluateDistance(offset.magnitude);
            linkedRope.SetTension(
                sample.TensionNewtons,
                sample.Broken);
            if (sample.Broken || sample.TensionNewtons <= 0f)
            {
                return;
            }

            var force = offset.normalized * sample.TensionNewtons;
            if (!attachedBody.isKinematic)
            {
                attachedBody.AddForceAtPosition(
                    force,
                    transform.position,
                    ForceMode.Force);
            }
            if (!otherBody.isKinematic)
            {
                otherBody.AddForceAtPosition(
                    -force,
                    linkedRope.transform.position,
                    ForceMode.Force);
            }
        }

        private void OnDisable()
        {
            SetTension(0f, false);
        }

        private void OnDestroy()
        {
            Unlink();
        }

        private void SetTension(float tensionNewtons, bool broken)
        {
            CurrentTensionNewtons = Mathf.Max(0f, tensionNewtons);
            IsBroken |= broken;
            part.SetCurrentOutput(CurrentTensionNewtons / 1000f);
            module.SetLoad(BreakingForceNewtons > 0f
                ? CurrentTensionNewtons / BreakingForceNewtons
                : 0f);
            if (broken)
            {
                module.Damage(0.25f);
            }
        }
    }

    [DefaultExecutionOrder(-20)]
    [DisallowMultipleComponent]
    public sealed class CaravanResourceSystem : MonoBehaviour
    {
        private const float BiomassKilogramsPerNormalizedCell = 120f;
        private const float WaterLitresPerNormalizedCell = 500f;

        private readonly List<CaravanHarvesterModule> harvesters =
            new List<CaravanHarvesterModule>(2);
        private readonly List<CaravanGrassDryerModule> dryers =
            new List<CaravanGrassDryerModule>(2);
        private readonly List<CaravanBiomassStorageModule> storages =
            new List<CaravanBiomassStorageModule>(2);
        private readonly List<CaravanBiofurnaceModule> furnaces =
            new List<CaravanBiofurnaceModule>(2);
        private readonly List<CaravanBiofuelEngineModule> engines =
            new List<CaravanBiofuelEngineModule>(2);
        private readonly List<CaravanElectricPumpModule> pumps =
            new List<CaravanElectricPumpModule>(2);
        private readonly List<CaravanTransmissionModule> transmissions =
            new List<CaravanTransmissionModule>(2);

        private CaravanChassisController chassis;
        private CaravanMaterialNetwork biomassNetwork;
        private CaravanMaterialNetwork mechanicalNetwork;
        private CaravanFluidNetwork fluidNetwork;
        private CaravanEnvironmentSampler environment;
        private float lastResourcePayloadKilograms = -1f;

        public IReadOnlyList<CaravanHarvesterModule> Harvesters => harvesters;
        public IReadOnlyList<CaravanGrassDryerModule> Dryers => dryers;
        public IReadOnlyList<CaravanBiomassStorageModule> Storages => storages;
        public IReadOnlyList<CaravanBiofurnaceModule> Furnaces => furnaces;
        public IReadOnlyList<CaravanBiofuelEngineModule> Engines => engines;

        public void Configure(
            CaravanChassisController caravan,
            CaravanMaterialNetwork biomass,
            CaravanMaterialNetwork mechanical,
            CaravanFluidNetwork fluids)
        {
            chassis = caravan != null
                ? caravan
                : throw new ArgumentNullException(nameof(caravan));
            biomassNetwork = biomass != null
                ? biomass
                : throw new ArgumentNullException(nameof(biomass));
            mechanicalNetwork = mechanical != null
                ? mechanical
                : throw new ArgumentNullException(nameof(mechanical));
            fluidNetwork = fluids != null
                ? fluids
                : throw new ArgumentNullException(nameof(fluids));
        }

        public void SetEnvironment(CaravanEnvironmentSampler sampler)
        {
            environment = sampler;
        }

        public void RegisterModule(CaravanModule module)
        {
            if (module == null)
            {
                return;
            }

            RegisterUnique(module.GetComponent<CaravanHarvesterModule>(), harvesters);
            RegisterUnique(module.GetComponent<CaravanGrassDryerModule>(), dryers);
            RegisterUnique(module.GetComponent<CaravanBiomassStorageModule>(), storages);
            RegisterUnique(module.GetComponent<CaravanBiofurnaceModule>(), furnaces);
            var engine = module.GetComponent<CaravanBiofuelEngineModule>();
            if (RegisterUnique(engine, engines))
            {
                chassis.AttachDriveSource(engine);
            }
            RegisterUnique(module.GetComponent<CaravanElectricPumpModule>(), pumps);
            var transmission = module.GetComponent<CaravanTransmissionModule>();
            if (RegisterUnique(transmission, transmissions))
            {
                chassis.AttachTransmission(transmission);
            }
        }

        public void UnregisterModule(CaravanModule module)
        {
            if (module == null)
            {
                return;
            }

            harvesters.Remove(module.GetComponent<CaravanHarvesterModule>());
            dryers.Remove(module.GetComponent<CaravanGrassDryerModule>());
            storages.Remove(module.GetComponent<CaravanBiomassStorageModule>());
            furnaces.Remove(module.GetComponent<CaravanBiofurnaceModule>());
            pumps.Remove(module.GetComponent<CaravanElectricPumpModule>());

            var engine = module.GetComponent<CaravanBiofuelEngineModule>();
            if (engines.Remove(engine))
            {
                chassis.DetachDriveSource(engine);
            }
            var transmission = module.GetComponent<CaravanTransmissionModule>();
            if (transmissions.Remove(transmission))
            {
                chassis.DetachTransmission(transmission);
            }
            lastResourcePayloadKilograms = -1f;
        }

        public void Simulate(float deltaTimeSeconds)
        {
            var seconds = Mathf.Max(0f, deltaTimeSeconds);
            if (seconds <= 0f)
            {
                return;
            }

            SetProcessDemands();
            SimulateHarvesters(seconds);
            TransferWetBiomass(seconds);
            SimulateDryers(seconds);
            TransferDryBiomass(seconds);
            var heating = SimulateFuelConsumers(seconds);
            SimulateWaterExtraction(seconds);
            fluidNetwork.SetExternalHeatingKilowatts(heating);
            RefreshPayloadMassIfNeeded();
        }

        public bool TryConsumeRepairMaterial(float kilograms)
        {
            var remaining = Mathf.Max(0f, kilograms);
            var available = 0f;
            for (var index = 0; index < storages.Count; index++)
            {
                available += storages[index].StoredDryBiomassKilograms;
            }
            if (available + 0.0001f < remaining)
            {
                return false;
            }

            for (var index = 0; index < storages.Count && remaining > 0f; index++)
            {
                remaining -= storages[index].Supply(remaining);
            }

            return remaining <= 0.0001f;
        }

        private void SetProcessDemands()
        {
            RefreshMechanicalCouplings();
            for (var index = 0; index < harvesters.Count; index++)
            {
                var harvester = harvesters[index];
                var connected = FindConnectedDryer(harvester) != null;
                harvester.SetMechanicalPowerAvailability(
                    GetMechanicalAvailability(harvester.GetComponent<CaravanPart>()));
                harvester.SetProcessDemand(connected);
            }

            for (var index = 0; index < dryers.Count; index++)
            {
                var dryer = dryers[index];
                dryer.SetProcessDemand(
                    FindConnectedStorage(dryer) != null);
            }

            for (var index = 0; index < pumps.Count; index++)
            {
                var pump = pumps[index];
                pump.SetMechanicalPowerAvailability(
                    GetMechanicalAvailability(pump.GetComponent<CaravanPart>()));
                pump.SetExtractionDemand(
                    pump.Mode == CaravanPumpMode.Extraction);
            }
        }

        private void RefreshMechanicalCouplings()
        {
            for (var transmissionIndex = 0;
                 transmissionIndex < transmissions.Count;
                 transmissionIndex++)
            {
                var transmission = transmissions[transmissionIndex];
                var transmissionPart =
                    transmission.GetComponent<CaravanPart>();
                var engaged = false;
                var sources = chassis.DriveSources;
                for (var sourceIndex = 0;
                     sourceIndex < sources.Count;
                     sourceIndex++)
                {
                    if (!(sources[sourceIndex] is Component component))
                    {
                        continue;
                    }

                    var sourcePart = component.GetComponent<CaravanPart>();
                    if (sourcePart != null
                        && mechanicalNetwork.AreDirectlyConnected(
                            sourcePart,
                            transmissionPart))
                    {
                        engaged = true;
                        break;
                    }
                }
                transmission.SetEngaged(engaged);
            }

            for (var engineIndex = 0;
                 engineIndex < engines.Count;
                 engineIndex++)
            {
                var engine = engines[engineIndex];
                var enginePart = engine.GetComponent<CaravanPart>();
                var coupled = false;
                for (var transmissionIndex = 0;
                     transmissionIndex < transmissions.Count;
                     transmissionIndex++)
                {
                    var transmission = transmissions[transmissionIndex];
                    if (transmission.IsEngaged
                        && mechanicalNetwork.AreDirectlyConnected(
                            enginePart,
                            transmission.GetComponent<CaravanPart>()))
                    {
                        coupled = true;
                        break;
                    }
                }
                engine.SetDriveCoupled(coupled);
            }
        }

        private void SimulateHarvesters(float seconds)
        {
            if (environment == null)
            {
                return;
            }

            for (var index = 0; index < harvesters.Count; index++)
            {
                var harvester = harvesters[index];
                if (harvester.PowerAvailability <= 0.001f
                    || harvester.OperatingLevel <= 0.001f
                    || harvester.StoredWetBiomassKilograms
                       >= harvester.GetComponent<CaravanPart>().Capacity)
                {
                    harvester.SetHarvestRate(0f);
                    continue;
                }

                var speedFactor = Mathf.Clamp01(chassis.Speed / 1.5f);
                var request = 0.0009f
                              * harvester.PowerAvailability
                              * harvester.OperatingLevel
                              * Mathf.Lerp(0.35f, 1f, speedFactor)
                              * seconds;
                if (!environment.TryExtractResources(
                        harvester.transform.position,
                        0f,
                        0f,
                        0f,
                        request,
                        out var extraction))
                {
                    harvester.SetHarvestRate(0f);
                    continue;
                }

                var totalKilograms =
                    (float)extraction.Biomass
                    * BiomassKilogramsPerNormalizedCell;
                var waterFraction = Mathf.Lerp(
                    0.16f,
                    0.68f,
                    (float)extraction.GreenFraction);
                harvester.AcceptHarvest(
                    totalKilograms * (1f - waterFraction),
                    totalKilograms * waterFraction);
                harvester.SetHarvestRate(totalKilograms / seconds);
            }
        }

        private void TransferWetBiomass(float seconds)
        {
            for (var index = 0; index < harvesters.Count; index++)
            {
                var harvester = harvesters[index];
                var dryer = FindConnectedDryer(harvester);
                if (dryer == null)
                {
                    continue;
                }

                var transfer = harvester.TransferWet(1.5f * seconds);
                dryer.AcceptWet(transfer);
                biomassNetwork.SetLinkActivity(
                    harvester.GetComponent<CaravanPart>(),
                    dryer.GetComponent<CaravanPart>(),
                    transfer.ProcessedWetKilograms > 0f ? 1f : 0f);
            }
        }

        private void SimulateDryers(float seconds)
        {
            for (var index = 0; index < dryers.Count; index++)
            {
                var dryer = dryers[index];
                var passiveAvailability = 0f;
                if (environment != null
                    && environment.TrySample(
                        dryer.transform.position,
                        out var sample))
                {
                    var wind = Mathf.Clamp01(
                        sample.Weather.SurfaceWind.magnitude / 12f);
                    var temperature = Mathf.InverseLerp(
                        -5f,
                        32f,
                        (float)environment.SampleAirTemperature(
                            dryer.transform.position));
                    passiveAvailability = wind * temperature * 0.22f;
                }

                var result = dryer.SimulateDrying(
                    passiveAvailability,
                    seconds);
                if (result.RecoveredWaterLitres > 0f
                    && fluidNetwork.Reservoir != null)
                {
                    fluidNetwork.Reservoir.SetStoredWater(
                        fluidNetwork.Reservoir.StoredWaterLitres
                        + result.RecoveredWaterLitres);
                }
            }
        }

        private void TransferDryBiomass(float seconds)
        {
            for (var index = 0; index < dryers.Count; index++)
            {
                var dryer = dryers[index];
                var storage = FindConnectedStorage(dryer);
                if (storage == null)
                {
                    continue;
                }

                var offered = dryer.TransferDry(1.1f * seconds);
                var accepted = storage.Accept(offered);
                if (accepted < offered)
                {
                    // Keep overflow inside the dryer instead of destroying matter.
                    dryer.ReturnDry(offered - accepted);
                }
                biomassNetwork.SetLinkActivity(
                    dryer.GetComponent<CaravanPart>(),
                    storage.GetComponent<CaravanPart>(),
                    accepted > 0f ? 1f : 0f);
            }
        }

        private float SimulateFuelConsumers(float seconds)
        {
            var heating = 0f;
            for (var index = 0; index < furnaces.Count; index++)
            {
                var furnace = furnaces[index];
                var fuel = SupplyFuel(
                    furnace.GetComponent<CaravanPart>(),
                    furnace.RequestedFuelKilograms(seconds));
                furnace.SimulateFuel(fuel, seconds);
                if (fluidNetwork.IsPartConnected(
                        furnace.GetComponent<CaravanPart>()))
                {
                    heating += furnace.ThermalOutputKilowatts;
                }
            }

            for (var index = 0; index < engines.Count; index++)
            {
                var engine = engines[index];
                var fuel = SupplyFuel(
                    engine.GetComponent<CaravanPart>(),
                    engine.RequestedFuelKilograms(seconds));
                engine.SimulateFuel(fuel, seconds);
                if (fluidNetwork.IsPartConnected(
                        engine.GetComponent<CaravanPart>()))
                {
                    heating += engine.WasteHeatKilowatts;
                }
            }

            return heating;
        }

        private void SimulateWaterExtraction(float seconds)
        {
            if (environment == null || fluidNetwork.Reservoir == null)
            {
                return;
            }

            for (var index = 0; index < pumps.Count; index++)
            {
                var pump = pumps[index];
                if (pump.Mode != CaravanPumpMode.Extraction
                    || !fluidNetwork.IsClosed
                    || pump.PowerAvailability <= 0.001f)
                {
                    continue;
                }

                var room = fluidNetwork.Reservoir.CapacityLitres
                           - fluidNetwork.Reservoir.StoredWaterLitres;
                if (room <= 0.001f)
                {
                    continue;
                }

                var requestedLitres = Mathf.Min(
                    room,
                    pump.MaximumFlowLitresPerSecond
                    * pump.PowerAvailability
                    * seconds);
                var normalizedRequest =
                    requestedLitres / WaterLitresPerNormalizedCell;
                if (!environment.TryExtractResources(
                        pump.transform.position,
                        normalizedRequest * 0.45f,
                        normalizedRequest * 0.25f,
                        normalizedRequest * 0.3f,
                        0f,
                        out var extraction))
                {
                    continue;
                }

                var extractedLitres =
                    Mathf.Min(
                        room,
                        (float)extraction.TotalWater
                        * WaterLitresPerNormalizedCell);
                fluidNetwork.Reservoir.SetStoredWater(
                    fluidNetwork.Reservoir.StoredWaterLitres
                    + extractedLitres);
                pump.ApplyFlow(extractedLitres / seconds);
            }
        }

        private void RefreshPayloadMassIfNeeded()
        {
            var payload = fluidNetwork.Reservoir != null
                ? fluidNetwork.Reservoir.StoredWaterLitres
                : 0f;
            for (var index = 0; index < harvesters.Count; index++)
            {
                payload += harvesters[index].StoredWetBiomassKilograms;
            }
            for (var index = 0; index < dryers.Count; index++)
            {
                payload += dryers[index].StoredWetBiomassKilograms
                           + dryers[index].DryOutputKilograms;
            }
            for (var index = 0; index < storages.Count; index++)
            {
                payload += storages[index].StoredDryBiomassKilograms;
            }

            if (lastResourcePayloadKilograms >= 0f
                && Mathf.Abs(payload - lastResourcePayloadKilograms) < 0.05f)
            {
                return;
            }

            lastResourcePayloadKilograms = payload;
            chassis.RefreshMassProperties();
        }

        private CaravanGrassDryerModule FindConnectedDryer(
            CaravanHarvesterModule harvester)
        {
            var source = harvester.GetComponent<CaravanPart>();
            for (var index = 0; index < dryers.Count; index++)
            {
                if (biomassNetwork.AreDirectlyConnected(
                        source,
                        dryers[index].GetComponent<CaravanPart>()))
                {
                    return dryers[index];
                }
            }
            return null;
        }

        private CaravanBiomassStorageModule FindConnectedStorage(
            CaravanGrassDryerModule dryer)
        {
            var source = dryer.GetComponent<CaravanPart>();
            for (var index = 0; index < storages.Count; index++)
            {
                if (biomassNetwork.AreDirectlyConnected(
                        source,
                        storages[index].GetComponent<CaravanPart>()))
                {
                    return storages[index];
                }
            }
            return null;
        }

        private float SupplyFuel(CaravanPart consumer, float requestedKilograms)
        {
            var remaining = Mathf.Max(0f, requestedKilograms);
            var supplied = 0f;
            for (var index = 0; index < storages.Count && remaining > 0f; index++)
            {
                var storage = storages[index];
                if (!biomassNetwork.AreDirectlyConnected(
                        storage.GetComponent<CaravanPart>(),
                        consumer))
                {
                    continue;
                }

                var amount = storage.Supply(remaining);
                supplied += amount;
                remaining -= amount;
                biomassNetwork.SetLinkActivity(
                    storage.GetComponent<CaravanPart>(),
                    consumer,
                    amount > 0f ? 1f : 0f);
            }
            return supplied;
        }

        private float GetMechanicalAvailability(CaravanPart consumer)
        {
            var requested = 0f;
            var delivered = 0f;
            var sources = chassis.DriveSources;
            for (var index = 0; index < sources.Count; index++)
            {
                if (!(sources[index] is Component sourceComponent))
                {
                    continue;
                }
                var sourcePart = sourceComponent.GetComponent<CaravanPart>();
                if (sourcePart == null
                    || !mechanicalNetwork.HasPath(sourcePart, consumer))
                {
                    continue;
                }

                requested += sources[index].RequestedMechanicalKilowatts;
                delivered += sources[index].DeliveredMechanicalKilowatts;
            }

            return requested > 0.001f
                ? Mathf.Clamp01(delivered / requested)
                : 0f;
        }

        private void FixedUpdate()
        {
            Simulate(UnityEngine.Time.fixedDeltaTime);
        }

        private static bool RegisterUnique<T>(
            T component,
            List<T> collection)
            where T : Component
        {
            if (component == null || collection.Contains(component))
            {
                return false;
            }
            collection.Add(component);
            return true;
        }
    }
}
