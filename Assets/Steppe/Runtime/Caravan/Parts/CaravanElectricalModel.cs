using UnityEngine;

namespace Steppe.Caravan
{
    public readonly struct CaravanElectricalFlow
    {
        public CaravanElectricalFlow(
            float generatedKilowatts,
            float requestedKilowatts,
            float deliveredKilowatts,
            float batteryChargeKilowatts,
            float batteryDischargeKilowatts,
            float spilledKilowatts,
            float deficitKilowatts,
            float storedKilowattHours)
        {
            GeneratedKilowatts = generatedKilowatts;
            RequestedKilowatts = requestedKilowatts;
            DeliveredKilowatts = deliveredKilowatts;
            BatteryChargeKilowatts = batteryChargeKilowatts;
            BatteryDischargeKilowatts = batteryDischargeKilowatts;
            SpilledKilowatts = spilledKilowatts;
            DeficitKilowatts = deficitKilowatts;
            StoredKilowattHours = storedKilowattHours;
        }

        public float GeneratedKilowatts { get; }
        public float RequestedKilowatts { get; }
        public float DeliveredKilowatts { get; }
        public float BatteryChargeKilowatts { get; }
        public float BatteryDischargeKilowatts { get; }
        public float SpilledKilowatts { get; }
        public float DeficitKilowatts { get; }
        public float StoredKilowattHours { get; }
    }

    /// <summary>
    /// Stateless power-balance calculation. Power is measured in kW and stored
    /// energy in kWh, so a simulation step can be tested without Unity physics.
    /// </summary>
    public static class CaravanElectricalModel
    {
        public static CaravanElectricalFlow Evaluate(
            float availableGenerationKilowatts,
            float requestedLoadKilowatts,
            float storedKilowattHours,
            float capacityKilowattHours,
            float maximumChargeKilowatts,
            float maximumDischargeKilowatts,
            float chargeEfficiency,
            float dischargeEfficiency,
            float deltaTimeHours,
            bool generatorConnected = true,
            bool consumerConnected = true)
        {
            var generation = generatorConnected
                ? Mathf.Max(0f, availableGenerationKilowatts)
                : 0f;
            var request = Mathf.Max(0f, requestedLoadKilowatts);
            var connectedRequest = consumerConnected ? request : 0f;
            var capacity = Mathf.Max(0f, capacityKilowattHours);
            var stored = Mathf.Clamp(storedKilowattHours, 0f, capacity);
            var hours = Mathf.Max(0f, deltaTimeHours);
            var chargeEfficiencyClamped = Mathf.Clamp(
                chargeEfficiency,
                0.01f,
                1f);
            var dischargeEfficiencyClamped = Mathf.Clamp(
                dischargeEfficiency,
                0.01f,
                1f);

            var directToLoad = Mathf.Min(generation, connectedRequest);
            var remainingLoad = connectedRequest - directToLoad;
            var availableDischarge = hours > 0f
                ? stored * dischargeEfficiencyClamped / hours
                : 0f;
            var batteryDischarge = Mathf.Min(
                remainingLoad,
                Mathf.Max(0f, maximumDischargeKilowatts),
                availableDischarge);
            var delivered = directToLoad + batteryDischarge;

            var surplus = generation - directToLoad;
            var availableChargeInput = hours > 0f
                ? (capacity - stored) / (chargeEfficiencyClamped * hours)
                : 0f;
            var batteryCharge = Mathf.Min(
                surplus,
                Mathf.Max(0f, maximumChargeKilowatts),
                availableChargeInput);

            if (hours > 0f)
            {
                stored += batteryCharge * chargeEfficiencyClamped * hours;
                stored -= batteryDischarge
                          / dischargeEfficiencyClamped
                          * hours;
                stored = Mathf.Clamp(stored, 0f, capacity);
            }

            return new CaravanElectricalFlow(
                generation,
                request,
                delivered,
                batteryCharge,
                batteryDischarge,
                Mathf.Max(0f, surplus - batteryCharge),
                Mathf.Max(0f, request - delivered),
                stored);
        }
    }
}
