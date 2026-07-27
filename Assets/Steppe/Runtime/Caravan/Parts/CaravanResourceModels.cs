using UnityEngine;

namespace Steppe.Caravan
{
    public readonly struct CaravanBiomassConversion
    {
        public CaravanBiomassConversion(
            float processedWetKilograms,
            float dryBiomassKilograms,
            float recoveredWaterLitres)
        {
            ProcessedWetKilograms = processedWetKilograms;
            DryBiomassKilograms = dryBiomassKilograms;
            RecoveredWaterLitres = recoveredWaterLitres;
        }

        public float ProcessedWetKilograms { get; }
        public float DryBiomassKilograms { get; }
        public float RecoveredWaterLitres { get; }
    }

    public static class CaravanBiomassModel
    {
        public static CaravanBiomassConversion Dry(
            float wetKilograms,
            float moistureFraction,
            float maximumWetKilogramsPerSecond,
            float activePowerAvailability,
            float passiveDryingAvailability,
            float deltaTimeSeconds)
        {
            var wet = Mathf.Max(0f, wetKilograms);
            var moisture = Mathf.Clamp01(moistureFraction);
            var seconds = Mathf.Max(0f, deltaTimeSeconds);
            var availability = Mathf.Clamp01(
                Mathf.Max(
                    activePowerAvailability,
                    passiveDryingAvailability));
            var processed = Mathf.Min(
                wet,
                Mathf.Max(0f, maximumWetKilogramsPerSecond)
                * availability
                * seconds);
            return new CaravanBiomassConversion(
                processed,
                processed * (1f - moisture),
                processed * moisture);
        }
    }

    /// <summary>
    /// Shared conversion between the bidirectional controls used by the build UI
    /// and normalized module demand.
    /// </summary>
    public static class CaravanControlModel
    {
        public static float Clamp(float value)
        {
            return Mathf.Clamp(value, -1f, 1f);
        }

        public static float ToOperatingLevel(float control)
        {
            return (Clamp(control) + 1f) * 0.5f;
        }

        public static CaravanPumpMode ToPumpMode(float control)
        {
            var clamped = Clamp(control);
            return clamped < -0.25f
                ? CaravanPumpMode.Extraction
                : clamped > 0.25f
                    ? CaravanPumpMode.Circulation
                    : CaravanPumpMode.Off;
        }

        public static float FromPumpMode(CaravanPumpMode mode)
        {
            return mode switch
            {
                CaravanPumpMode.Extraction => -1f,
                CaravanPumpMode.Circulation => 1f,
                _ => 0f
            };
        }
    }

    public readonly struct CaravanFuelFlow
    {
        public CaravanFuelFlow(
            float consumedFuelKilograms,
            float fuelPowerKilowatts,
            float usefulOutputKilowatts,
            float wasteHeatKilowatts)
        {
            ConsumedFuelKilograms = consumedFuelKilograms;
            FuelPowerKilowatts = fuelPowerKilowatts;
            UsefulOutputKilowatts = usefulOutputKilowatts;
            WasteHeatKilowatts = wasteHeatKilowatts;
        }

        public float ConsumedFuelKilograms { get; }
        public float FuelPowerKilowatts { get; }
        public float UsefulOutputKilowatts { get; }
        public float WasteHeatKilowatts { get; }
    }

    /// <summary>
    /// Pure dry-biomass energy conversion used by the furnace and engine.
    /// </summary>
    public static class CaravanFuelModel
    {
        public const float DryBiomassKilowattHoursPerKilogram = 4.2f;
        public const float FurnaceEfficiency = 0.82f;
        public const float EngineEfficiency = 0.31f;
        public const float EngineWasteHeatFraction = 0.48f;

        public static float RequiredFuelKilograms(
            float requestedOutputKilowatts,
            float conversionEfficiency,
            float deltaTimeSeconds)
        {
            var efficiency = Mathf.Clamp(conversionEfficiency, 0.01f, 1f);
            return Mathf.Max(0f, requestedOutputKilowatts)
                   / (DryBiomassKilowattHoursPerKilogram
                      * 3600f
                      * efficiency)
                   * Mathf.Max(0f, deltaTimeSeconds);
        }

        public static CaravanFuelFlow Evaluate(
            float suppliedFuelKilograms,
            float deltaTimeSeconds,
            float conversionEfficiency,
            float requestedOutputKilowatts,
            float outputEfficiency = 1f,
            float wasteHeatFraction = 0f)
        {
            var consumed = Mathf.Max(0f, suppliedFuelKilograms);
            var seconds = Mathf.Max(0.001f, deltaTimeSeconds);
            var fuelPower = consumed
                            / seconds
                            * DryBiomassKilowattHoursPerKilogram
                            * 3600f;
            var usefulOutput = Mathf.Min(
                Mathf.Max(0f, requestedOutputKilowatts),
                fuelPower
                * Mathf.Clamp(conversionEfficiency, 0.01f, 1f)
                * Mathf.Clamp01(outputEfficiency));
            return new CaravanFuelFlow(
                consumed,
                fuelPower,
                usefulOutput,
                fuelPower * Mathf.Clamp01(wasteHeatFraction));
        }
    }

    public readonly struct CaravanRopeTension
    {
        public CaravanRopeTension(float tensionNewtons, bool broken)
        {
            TensionNewtons = tensionNewtons;
            Broken = broken;
        }

        public float TensionNewtons { get; }
        public bool Broken { get; }
    }

    public static class CaravanCouplingRopeModel
    {
        public static CaravanRopeTension Evaluate(
            float distanceMetres,
            float slackLengthMetres,
            float stiffnessNewtonsPerMetre,
            float breakingForceNewtons)
        {
            var extension = Mathf.Max(
                0f,
                distanceMetres - Mathf.Max(0f, slackLengthMetres));
            var tension = extension
                          * Mathf.Max(0f, stiffnessNewtonsPerMetre);
            var limit = Mathf.Max(1f, breakingForceNewtons);
            return new CaravanRopeTension(
                Mathf.Min(tension, limit),
                tension > limit);
        }
    }
}
