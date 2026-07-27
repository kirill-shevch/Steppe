using UnityEngine;

namespace Steppe.Caravan
{
    public readonly struct CaravanWaterFlow
    {
        public CaravanWaterFlow(
            float flowLitresPerSecond,
            float coolingKilowatts,
            float temperatureCelsius)
        {
            FlowLitresPerSecond = flowLitresPerSecond;
            CoolingKilowatts = coolingKilowatts;
            TemperatureCelsius = temperatureCelsius;
        }

        public float FlowLitresPerSecond { get; }
        public float CoolingKilowatts { get; }
        public float TemperatureCelsius { get; }
    }

    public static class CaravanWaterCircuitModel
    {
        private const float WaterHeatCapacityKilojoulesPerLitreCelsius = 4.186f;

        public static CaravanWaterFlow Evaluate(
            float storedWaterLitres,
            float waterTemperatureCelsius,
            float ambientTemperatureCelsius,
            float maximumFlowLitresPerSecond,
            float pumpPowerAvailability,
            float maximumCoolingKilowatts,
            float windSpeedMetresPerSecond,
            float deltaTimeSeconds,
            bool circuitClosed)
        {
            var water = Mathf.Max(0f, storedWaterLitres);
            var temperature = waterTemperatureCelsius;
            var seconds = Mathf.Max(0f, deltaTimeSeconds);
            if (!circuitClosed || water <= 0.001f || seconds <= 0f)
            {
                return new CaravanWaterFlow(0f, 0f, temperature);
            }

            var maximumFlow = Mathf.Max(0f, maximumFlowLitresPerSecond);
            var powerAvailability = Mathf.Clamp01(pumpPowerAvailability);
            var flow = maximumFlow * powerAvailability;
            if (flow <= 0.0001f)
            {
                return new CaravanWaterFlow(0f, 0f, temperature);
            }

            var temperatureDifference = Mathf.Max(
                0f,
                temperature - ambientTemperatureCelsius);
            var windFactor = Mathf.Lerp(
                0.55f,
                1.4f,
                Mathf.Clamp01(
                    Mathf.Max(0f, windSpeedMetresPerSecond) / 14f));
            var flowFactor = maximumFlow > 0f
                ? Mathf.Clamp01(flow / maximumFlow)
                : 0f;
            var cooling = Mathf.Min(
                Mathf.Max(0f, maximumCoolingKilowatts),
                temperatureDifference * 1.25f * windFactor * flowFactor);
            var thermalCapacity = water
                                  * WaterHeatCapacityKilojoulesPerLitreCelsius;
            var cooledTemperature = thermalCapacity > 0f
                ? temperature - cooling * seconds / thermalCapacity
                : temperature;
            cooledTemperature = Mathf.Max(
                ambientTemperatureCelsius,
                cooledTemperature);
            return new CaravanWaterFlow(flow, cooling, cooledTemperature);
        }
    }
}
