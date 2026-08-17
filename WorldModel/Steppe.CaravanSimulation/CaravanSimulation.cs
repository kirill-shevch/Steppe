using Steppe.Simulation;

namespace Steppe.CaravanSimulation;

public sealed class CaravanSimulation
{
    private const float BiomassEnergyKwhKg = 4.2f;
    private const float FurnaceElectricEfficiency = 0.12f;
    private const float FurnaceHeatEfficiency = 0.55f;
    private const float MotorEfficiency = 0.84f;
    private const float BaseWaterLitersDay = 120f;
    private const float BaseOrganicKgDay = 5f;
    private const float BaseElectricityKwhDay = 4f;

    private readonly CaravanState state;

    public CaravanSimulation(
        CaravanBlueprint blueprint,
        float initialXCells,
        float initialYCells)
    {
        ArgumentNullException.ThrowIfNull(blueprint);
        if (!float.IsFinite(initialXCells) || !float.IsFinite(initialYCells))
        {
            throw new ArgumentOutOfRangeException(nameof(initialXCells));
        }
        state = new CaravanState(blueprint, initialXCells, initialYCells);
    }

    internal CaravanSimulation(CaravanBlueprint blueprint, CaravanStateSnapshot snapshot)
    {
        state = new CaravanState(blueprint, snapshot);
    }

    public CaravanStateSnapshot Capture() => state.Capture();
    public CaravanBlueprint Blueprint => state.Blueprint;

    internal CaravanPhysicalExchangeRequest BuildIntakeRequest(
        CaravanObservation observation,
        CaravanDecision decision,
        double hours)
    {
        if (state.IsHibernating)
        {
            return new CaravanPhysicalExchangeRequest
            {
                X = observation.Cell.X,
                Y = observation.Cell.Y
            };
        }

        var waterRoom = Math.Max(
            0f,
            state.WaterCapacityLiters - state.WaterLiters - state.SnowWaterLiters);
        var organicRoom = Math.Max(
            0f,
            state.OrganicCapacityKg - state.WetOrganicDryKg - state.DryOrganicKg);
        var waterLow = state.WaterLiters < state.WaterCapacityLiters * 0.58f;
        var organicLow = state.WetOrganicDryKg + state.DryOrganicKg
            < state.OrganicCapacityKg * 0.58f;

        var surfaceWater = 0f;
        var snow = 0f;
        if (decision.Activity == CaravanActivity.CollectSnow)
        {
            snow = Math.Min(
                waterRoom,
                state.OrganSize(CaravanOrganKind.SnowCollector) * 0.7f * (float)hours);
        }
        else if (decision.Activity == CaravanActivity.CollectWater || waterLow)
        {
            surfaceWater = Math.Min(
                waterRoom,
                state.OrganSize(CaravanOrganKind.WaterIntake) * (float)hours);
        }

        var live = 0f;
        var dry = 0f;
        if (decision.Activity == CaravanActivity.HarvestLiveBiomass || organicLow)
        {
            live = Math.Min(
                organicRoom,
                state.OrganSize(CaravanOrganKind.LiveBiomassHarvester) * (float)hours);
        }
        if (decision.Activity == CaravanActivity.HarvestDryBiomass
            || organicLow && live < organicRoom * 0.4f)
        {
            dry = Math.Min(
                Math.Max(0f, organicRoom - live),
                state.OrganSize(CaravanOrganKind.DryBiomassCollector) * (float)hours);
        }

        var dustCapacity = Math.Max(0f, 100f - state.CapturedDustKg);
        var dustCapture = observation.Cell.DustGm2 > 0.03f
            ? Math.Min(dustCapacity, Math.Max(0.1f, state.OrganSize(CaravanOrganKind.Radiator) * 0.01f * (float)hours))
            : 0f;
        var collectChitin = decision.Activity == CaravanActivity.CollectChitin
            || observation.Opportunities.Opportunities.Any(item =>
                item.Kind == CaravanOpportunityKind.HarvesterMolt && item.DistanceCells < 2f);

        return new CaravanPhysicalExchangeRequest
        {
            X = observation.Cell.X,
            Y = observation.Cell.Y,
            SurfaceWaterWithdrawalLiters = surfaceWater,
            SnowWithdrawalLiters = snow,
            LiveBiomassHarvestKg = live,
            DryBiomassHarvestKg = dry,
            DustCaptureKg = dustCapture,
            ChitinCollectionKg = collectChitin ? 35f : 0f
        };
    }

    internal InternalStep AdvanceInternal(
        CaravanObservation observation,
        CaravanDecision decision,
        CaravanPhysicalExchangeResult intake,
        double hours,
        float cellSizeMeters)
    {
        if (state.IsHibernating)
        {
            return AdvanceHibernation(observation, hours);
        }

        var accounting = StepAccounting.Start(state);
        var actions = new List<CaravanActionRecord>(24);
        var usages = Enum.GetValues<CaravanOrganKind>().ToDictionary(item => item, _ => 0f);

        ApplyGrowthPriorities(decision.GrowthPriorities);
        ApplyIntake(intake, accounting, actions, usages, hours);
        GenerateSolar(observation, accounting, actions, usages, hours);
        DispatchFurnace(observation, decision, accounting, actions, usages, hours);
        ChargeIntakeWork(intake, accounting, actions);
        var thermalFulfillment = RegulateTemperature(
            observation,
            accounting,
            actions,
            usages,
            hours);
        MeltSnow(observation, accounting, actions, usages, hours);
        DryAndSpoilOrganic(observation, decision, accounting, actions, usages, hours);

        var maintenance = ConsumeMaintenance(
            observation,
            accounting,
            actions,
            hours);
        var movement = Move(
            observation,
            decision,
            accounting,
            actions,
            usages,
            hours,
            cellSizeMeters);
        var movementNeeds = ConsumeMovementNeeds(
            movement.DistanceKilometers,
            accounting,
            actions);

        var waterDemand = maintenance.WaterDemand + movementNeeds.WaterDemand;
        var waterConsumed = maintenance.WaterConsumed + movementNeeds.WaterConsumed;
        var organicDemand = maintenance.OrganicDemand + movementNeeds.OrganicDemand;
        var organicConsumed = maintenance.OrganicConsumed + movementNeeds.OrganicConsumed;
        var waterFulfillment = Fulfillment(waterDemand, waterConsumed);
        var organicFulfillment = Fulfillment(organicDemand, organicConsumed);

        var maintenanceElectricityFulfillment = Fulfillment(
            maintenance.ElectricityDemand,
            maintenance.ElectricityConsumed);
        var maintenanceFulfillment = Math.Min(
            Math.Min(waterFulfillment, organicFulfillment),
            maintenanceElectricityFulfillment);
        ApplyMaintenanceToOrgans(
            maintenanceFulfillment * (0.75f + thermalFulfillment * 0.25f),
            hours);
        GrowAndAtrophy(
            waterFulfillment,
            organicFulfillment,
            thermalFulfillment,
            accounting,
            actions,
            usages,
            hours);
        ClampStores(accounting);
        UpdateHibernationState(
            waterFulfillment,
            organicFulfillment,
            thermalFulfillment,
            actions,
            hours);

        usages[CaravanOrganKind.WaterReservoir] = Math.Clamp(
            (state.WaterLiters + state.SnowWaterLiters) / Math.Max(1f, state.WaterCapacityLiters),
            0f,
            1f);
        usages[CaravanOrganKind.OrganicStorage] = Math.Clamp(
            (state.WetOrganicDryKg + state.DryOrganicKg) / Math.Max(1f, state.OrganicCapacityKg),
            0f,
            1f);
        usages[CaravanOrganKind.Battery] = Math.Max(
            usages[CaravanOrganKind.Battery],
            Math.Clamp(
                accounting.ElectricityConsumed / Math.Max(1f, state.BatteryCapacityKwh),
                0f,
                1f));

        foreach (var organ in state.Organs.Values)
        {
            organ.RecordUsage(usages[organ.Definition.Kind], hours);
        }

        state.DistanceKilometers += movement.DistanceKilometers;
        state.SimulatedHours += hours;
        state.RecordActivity(decision.Activity);
        state.RecordRegime(observation.Regime, hours);

        return new InternalStep(
            accounting,
            actions.ToArray(),
            movement.Trail,
            movement.DistanceKilometers,
            waterFulfillment,
            organicFulfillment,
            thermalFulfillment);
    }

    internal CaravanPhysicalExchangeRequest BuildReturnRequest(CaravanDecision decision)
    {
        var x = (int)MathF.Round(state.XCells);
        var y = (int)MathF.Round(state.YCells);
        var returnOrganic = decision.Activity == CaravanActivity.ReturnResidues
            || state.PendingOrganicResidueKg >= 5f;
        var organicMatter = returnOrganic ? state.PendingOrganicResidueKg : 0f;
        var organicNitrogen = organicMatter > 0f && state.PendingOrganicResidueKg > 1e-7f
            ? state.PendingNitrogenKg * organicMatter / state.PendingOrganicResidueKg
            : 0f;
        var sediment = decision.Activity == CaravanActivity.ReturnResidues || state.CapturedDustKg > 50f
            ? state.CapturedDustKg
            : Math.Min(state.CapturedDustKg, 0.15f);
        return new CaravanPhysicalExchangeRequest
        {
            X = x,
            Y = y,
            SurfaceWaterReturnLiters = state.PendingSurfaceWaterReturnLiters,
            AtmosphericWaterReturnLiters = state.PendingWaterVaporLiters,
            OrganicMatterReturnKg = organicMatter,
            OrganicNitrogenReturnKg = organicNitrogen,
            SedimentReturnKg = sediment,
            WasteHeatKwh = state.PendingWasteHeatKwh
        };
    }

    internal void AcknowledgeReturn(
        CaravanPhysicalExchangeResult returned,
        StepAccounting accounting)
    {
        state.PendingSurfaceWaterReturnLiters = Math.Max(
            0f,
            state.PendingSurfaceWaterReturnLiters - returned.SurfaceWaterReturnedLiters);
        state.PendingWaterVaporLiters = Math.Max(
            0f,
            state.PendingWaterVaporLiters - returned.AtmosphericWaterReturnedLiters);
        state.PendingOrganicResidueKg = Math.Max(
            0f,
            state.PendingOrganicResidueKg - returned.OrganicMatterReturnedKg);
        state.PendingNitrogenKg = Math.Max(
            0f,
            state.PendingNitrogenKg - returned.OrganicNitrogenReturnedKg);
        state.CapturedDustKg = Math.Max(
            0f,
            state.CapturedDustKg - returned.SedimentReturnedKg);
        state.PendingWasteHeatKwh = Math.Max(
            0f,
            state.PendingWasteHeatKwh - returned.WasteHeatAcceptedKwh);

        accounting.WaterOutput += returned.SurfaceWaterReturnedLiters
            + returned.AtmosphericWaterReturnedLiters;
        accounting.OrganicOutput += returned.OrganicMatterReturnedKg;
        accounting.HeatOutput += returned.WasteHeatAcceptedKwh;
        state.CumulativeOrganicMatterReturnedKg += returned.OrganicMatterReturnedKg;
        state.CumulativeWaterVaporReturnedLiters += returned.AtmosphericWaterReturnedLiters;
        state.CumulativeWasteHeatReturnedKwh += returned.WasteHeatAcceptedKwh;
    }

    internal CaravanStepLedger CompleteLedger(StepAccounting accounting) => accounting.Complete(state);

    private void ApplyIntake(
        CaravanPhysicalExchangeResult intake,
        StepAccounting accounting,
        List<CaravanActionRecord> actions,
        Dictionary<CaravanOrganKind, float> usages,
        double hours)
    {
        state.WaterLiters += intake.SurfaceWaterWithdrawnLiters;
        state.SnowWaterLiters += intake.SnowWithdrawnLiters;
        state.WetOrganicDryKg += intake.LiveBiomassHarvestedKg;
        state.WetOrganicWaterLiters += intake.PlantTissueWaterWithdrawnLiters;
        state.DryOrganicKg += intake.DryBiomassHarvestedKg;
        state.OrganicNitrogenKg += intake.OrganicNitrogenWithdrawnKg;
        state.CapturedDustKg += intake.DustCapturedKg;
        state.StructuralReserveKg += intake.ChitinCollectedKg;

        accounting.WaterInput += intake.SurfaceWaterWithdrawnLiters
            + intake.SnowWithdrawnLiters
            + intake.PlantTissueWaterWithdrawnLiters;
        accounting.OrganicInput += intake.LiveBiomassHarvestedKg
            + intake.DryBiomassHarvestedKg;
        accounting.StructuralInput += intake.ChitinCollectedKg;

        state.CumulativeSurfaceWaterLiters += intake.SurfaceWaterWithdrawnLiters;
        state.CumulativeSnowWaterLiters += intake.SnowWithdrawnLiters;
        state.CumulativePlantTissueWaterLiters += intake.PlantTissueWaterWithdrawnLiters;
        state.CumulativeLiveBiomassKg += intake.LiveBiomassHarvestedKg;
        state.CumulativeDryBiomassKg += intake.DryBiomassHarvestedKg;
        state.CumulativeChitinKg += intake.ChitinCollectedKg;

        AddAction(actions, "withdraw surface water", intake.SurfaceWaterWithdrawnLiters, "л", CaravanOrganKind.WaterIntake);
        AddAction(actions, "collect snow", intake.SnowWithdrawnLiters, "л SWE", CaravanOrganKind.SnowCollector);
        AddAction(actions, "harvest live biomass", intake.LiveBiomassHarvestedKg, "кг сух. в-ва", CaravanOrganKind.LiveBiomassHarvester);
        AddAction(actions, "harvest dry biomass", intake.DryBiomassHarvestedKg, "кг", CaravanOrganKind.DryBiomassCollector);
        AddAction(actions, "capture dust", intake.DustCapturedKg, "кг", CaravanOrganKind.Radiator);
        AddAction(actions, "collect chitin", intake.ChitinCollectedKg, "кг", CaravanOrganKind.GrowthTissue);

        usages[CaravanOrganKind.WaterIntake] = RateUsage(
            intake.SurfaceWaterWithdrawnLiters,
            state.OrganSize(CaravanOrganKind.WaterIntake),
            hours);
        usages[CaravanOrganKind.SnowCollector] = RateUsage(
            intake.SnowWithdrawnLiters,
            state.OrganSize(CaravanOrganKind.SnowCollector) * 0.7f,
            hours);
        usages[CaravanOrganKind.LiveBiomassHarvester] = RateUsage(
            intake.LiveBiomassHarvestedKg,
            state.OrganSize(CaravanOrganKind.LiveBiomassHarvester),
            hours);
        usages[CaravanOrganKind.DryBiomassCollector] = RateUsage(
            intake.DryBiomassHarvestedKg,
            state.OrganSize(CaravanOrganKind.DryBiomassCollector),
            hours);
        usages[CaravanOrganKind.GrowthTissue] = Math.Max(
            usages[CaravanOrganKind.GrowthTissue],
            Math.Clamp(intake.ChitinCollectedKg / 35f, 0f, 1f));
    }

    private void GenerateSolar(
        CaravanObservation observation,
        StepAccounting accounting,
        List<CaravanActionRecord> actions,
        Dictionary<CaravanOrganKind, float> usages,
        double hours)
    {
        var area = state.OrganSize(CaravanOrganKind.SolarLeaf);
        var dustPenalty = Math.Clamp(1f - observation.Cell.DustGm2 * 0.6f, 0.35f, 1f);
        var solarRadiation = CellValue(observation.Cell, SimulationLayer.SolarRadiation);
        var potential = solarRadiation * area * 0.19f * dustPenalty * (float)hours / 1000f;
        var room = Math.Max(0f, state.BatteryCapacityKwh - state.StoredElectricityKwh);
        var stored = Math.Min(room, potential);
        state.StoredElectricityKwh += stored;
        accounting.ElectricityGenerated += potential;
        accounting.ElectricityLost += potential - stored;
        state.CumulativeSolarElectricityKwh += potential;
        usages[CaravanOrganKind.SolarLeaf] = area <= 1e-6f
            ? 0f
            : Math.Clamp(solarRadiation / 750f, 0f, 1f);
        usages[CaravanOrganKind.Battery] = Math.Clamp(stored / Math.Max(1e-6f, room), 0f, 1f);
        AddAction(actions, "generate solar electricity", potential, "кВт·ч", CaravanOrganKind.SolarLeaf);

        var wasteHeat = potential * 0.16f;
        AddHeat(wasteHeat, accounting);
    }

    private void DispatchFurnace(
        CaravanObservation observation,
        CaravanDecision decision,
        StepAccounting accounting,
        List<CaravanActionRecord> actions,
        Dictionary<CaravanOrganKind, float> usages,
        double hours)
    {
        var electricityDeficit = Math.Max(
            0f,
            state.BatteryCapacityKwh * 0.22f - state.StoredElectricityKwh);
        var cold = Math.Max(0f, 12f - observation.Cell.AirTemperatureC);
        var heatDeficit = Math.Max(0f, state.HeatCapacityKwh * 0.45f - state.StoredHeatKwh)
            + cold * state.TotalMassKg * 0.0000035f * (float)hours;
        if (decision.Activity == CaravanActivity.CollectSnow)
        {
            heatDeficit += Math.Min(state.SnowWaterLiters, 500f) * 0.093f;
        }
        if (decision.Activity == CaravanActivity.DryOrganicMatter)
        {
            heatDeficit += Math.Min(state.WetOrganicDryKg, 100f) * 0.25f;
        }

        var fuelForElectricity = electricityDeficit
            / Math.Max(1e-6f, BiomassEnergyKwhKg * FurnaceElectricEfficiency);
        var fuelForHeat = heatDeficit
            / Math.Max(1e-6f, BiomassEnergyKwhKg * FurnaceHeatEfficiency);
        var requestedFuel = Math.Max(fuelForElectricity, fuelForHeat);
        var furnacePower = state.OrganSize(CaravanOrganKind.Furnace);
        var powerLimitedFuel = furnacePower * (float)hours / BiomassEnergyKwhKg;
        var fuel = Math.Min(state.DryOrganicKg, Math.Min(requestedFuel, powerLimitedFuel));
        if (fuel <= 1e-7f) return;

        var nitrogenBefore = state.OrganicNitrogenKg;
        var organicBefore = state.WetOrganicDryKg + state.DryOrganicKg;
        state.DryOrganicKg -= fuel;
        var nitrogen = organicBefore > 1e-7f
            ? Math.Min(nitrogenBefore, nitrogenBefore * fuel / organicBefore)
            : 0f;
        state.OrganicNitrogenKg -= nitrogen;
        var residue = fuel * 0.08f;
        state.PendingOrganicResidueKg += residue;
        state.PendingNitrogenKg += nitrogen;
        accounting.OrganicLost += fuel - residue;

        var electricity = fuel * BiomassEnergyKwhKg * FurnaceElectricEfficiency;
        var heat = fuel * BiomassEnergyKwhKg * FurnaceHeatEfficiency;
        var electricityRoom = Math.Max(0f, state.BatteryCapacityKwh - state.StoredElectricityKwh);
        var storedElectricity = Math.Min(electricityRoom, electricity);
        state.StoredElectricityKwh += storedElectricity;
        accounting.ElectricityGenerated += electricity;
        accounting.ElectricityLost += electricity - storedElectricity;
        AddHeat(heat, accounting);
        state.CumulativeFurnaceElectricityKwh += electricity;
        usages[CaravanOrganKind.Furnace] = Math.Clamp(
            fuel * BiomassEnergyKwhKg / Math.Max(1e-6f, furnacePower * (float)hours),
            0f,
            1f);
        AddAction(actions, "burn dry organic matter", fuel, "кг", CaravanOrganKind.Furnace);
    }

    private float ChargeIntakeWork(
        CaravanPhysicalExchangeResult intake,
        StepAccounting accounting,
        List<CaravanActionRecord> actions)
    {
        var demand = intake.SurfaceWaterWithdrawnLiters * 0.0006f
            + intake.LiveBiomassHarvestedKg * 0.03f
            + intake.DryBiomassHarvestedKg * 0.02f
            + intake.DustCapturedKg * 0.01f;
        var consumed = TakeElectricity(demand, accounting);
        AddAction(actions, "power intake and harvest", consumed, "кВт·ч");
        return Fulfillment(demand, consumed);
    }

    private float RegulateTemperature(
        CaravanObservation observation,
        StepAccounting accounting,
        List<CaravanActionRecord> actions,
        Dictionary<CaravanOrganKind, float> usages,
        double hours)
    {
        var ambient = observation.Cell.AirTemperatureC;
        var mass = Math.Max(1000f, state.TotalMassKg);
        var coldDemand = Math.Max(0f, 12f - ambient) * mass * 0.0000035f * (float)hours;
        var fromHeat = TakeHeat(coldDemand, accounting);
        var remaining = Math.Max(0f, coldDemand - fromHeat);
        var thermalPower = state.OrganSize(CaravanOrganKind.ThermalOrgan);
        var electricityDemand = Math.Min(remaining / 0.95f, thermalPower * (float)hours);
        var electricity = TakeElectricity(electricityDemand, accounting);
        var supplied = fromHeat + electricity * 0.95f;
        accounting.HeatGenerated += electricity * 0.95f;
        accounting.HeatConsumed += electricity * 0.95f;
        var fulfillment = Fulfillment(coldDemand, supplied);
        usages[CaravanOrganKind.ThermalOrgan] = Math.Clamp(
            supplied / Math.Max(1e-6f, thermalPower * (float)hours),
            0f,
            1f);

        var hotLoad = Math.Max(0f, ambient - 26f) * mass * 0.000008f * (float)hours;
        accounting.HeatGenerated += hotLoad;
        var wind = MathF.Sqrt(
            observation.Cell.WindXMs * observation.Cell.WindXMs
            + observation.Cell.WindYMs * observation.Cell.WindYMs);
        var radiatorCapacity = state.OrganSize(CaravanOrganKind.Radiator)
            * (0.45f + Math.Clamp(wind / 12f, 0f, 1f) * 0.55f)
            * (float)hours;
        var rejected = Math.Min(
            state.StoredHeatKwh + hotLoad,
            radiatorCapacity);
        var storedRejected = Math.Min(state.StoredHeatKwh, rejected);
        state.StoredHeatKwh -= storedRejected;
        state.PendingWasteHeatKwh += rejected;
        usages[CaravanOrganKind.Radiator] = Math.Clamp(
            rejected / Math.Max(1e-6f, radiatorCapacity),
            0f,
            1f);

        var targetTemperature = ambient < 12f
            ? 12f + 6f * fulfillment
            : ambient > 26f
                ? 26f + Math.Max(0f, hotLoad - rejected) / Math.Max(1f, mass * 0.0003f)
                : 18f;
        var alpha = 1f - MathF.Exp(-(float)hours / 12f);
        state.BodyTemperatureC += (targetTemperature - state.BodyTemperatureC) * alpha;
        if (coldDemand > 1e-6f)
        {
            AddAction(actions, "heat caravan", supplied, "кВт·ч", CaravanOrganKind.ThermalOrgan);
        }
        if (rejected > 1e-6f)
        {
            AddAction(actions, "reject heat", rejected, "кВт·ч", CaravanOrganKind.Radiator);
        }
        return ambient > 26f
            ? Fulfillment(hotLoad, rejected)
            : fulfillment;
    }

    private void MeltSnow(
        CaravanObservation observation,
        StepAccounting accounting,
        List<CaravanActionRecord> actions,
        Dictionary<CaravanOrganKind, float> usages,
        double hours)
    {
        if (state.SnowWaterLiters <= 1e-6f) return;
        var room = Math.Max(0f, state.WaterCapacityLiters - state.WaterLiters);
        if (room <= 1e-6f) return;

        var collectorArea = state.OrganSize(CaravanOrganKind.SnowCollector);
        var solarHeat = CellValue(observation.Cell, SimulationLayer.SolarRadiation) * collectorArea * 0.28f
            * (float)hours / 1000f;
        AddHeat(solarHeat, accounting);
        var thermalPower = state.OrganSize(CaravanOrganKind.ThermalOrgan);
        var maximumByPower = thermalPower * (float)hours / 0.093f;
        var amount = Math.Min(
            Math.Min(state.SnowWaterLiters, room),
            Math.Min(maximumByPower, state.StoredHeatKwh / 0.093f));
        if (amount <= 1e-6f) return;
        var heat = amount * 0.093f;
        TakeHeat(heat, accounting);
        state.SnowWaterLiters -= amount;
        state.WaterLiters += amount;
        usages[CaravanOrganKind.SnowCollector] = Math.Max(
            usages[CaravanOrganKind.SnowCollector],
            Math.Clamp(amount / Math.Max(1e-6f, collectorArea * 0.7f * (float)hours), 0f, 1f));
        usages[CaravanOrganKind.ThermalOrgan] = Math.Max(
            usages[CaravanOrganKind.ThermalOrgan],
            Math.Clamp(heat / Math.Max(1e-6f, thermalPower * (float)hours), 0f, 1f));
        AddAction(actions, "melt collected snow", amount, "л", CaravanOrganKind.ThermalOrgan);
    }

    private void DryAndSpoilOrganic(
        CaravanObservation observation,
        CaravanDecision decision,
        StepAccounting accounting,
        List<CaravanActionRecord> actions,
        Dictionary<CaravanOrganKind, float> usages,
        double hours)
    {
        if (state.WetOrganicDryKg <= 1e-6f) return;
        var wind = MathF.Sqrt(
            observation.Cell.WindXMs * observation.Cell.WindXMs
            + observation.Cell.WindYMs * observation.Cell.WindYMs);
        var weather = Math.Clamp(
            CellValue(observation.Cell, SimulationLayer.SolarRadiation) / 650f
            + wind / 18f
            + Math.Max(0f, 16f - observation.Cell.HumidityMm) / 32f,
            0.1f,
            1.4f);
        var active = decision.Activity == CaravanActivity.DryOrganicMatter
            || state.DryOrganicKg < 150f && state.StoredHeatKwh > 2f
            || observation.Cell.PrecipitationMmPerHour < 0.01f
            && CellValue(observation.Cell, SimulationLayer.SolarRadiation) > 160f;
        if (active)
        {
            var dryerRate = state.OrganSize(CaravanOrganKind.Dryer);
            var requested = Math.Min(
                state.WetOrganicDryKg,
                dryerRate * weather * (float)hours);
            var electricDemand = requested * Math.Max(0.015f, 0.06f - weather * 0.02f);
            var heatDemand = requested * Math.Max(0.06f, 0.35f - weather * 0.16f);
            var electricity = TakeElectricity(electricDemand, accounting);
            var heat = TakeHeat(heatDemand, accounting);
            var fulfillment = Math.Min(
                Fulfillment(electricDemand, electricity),
                Fulfillment(heatDemand, heat));
            var amount = requested * fulfillment;
            if (amount > 1e-6f)
            {
                var wetBefore = state.WetOrganicDryKg;
                var water = wetBefore > 1e-6f
                    ? state.WetOrganicWaterLiters * amount / wetBefore
                    : 0f;
                state.WetOrganicDryKg -= amount;
                state.WetOrganicWaterLiters -= water;
                state.DryOrganicKg += amount;
                state.PendingWaterVaporLiters += water;
                usages[CaravanOrganKind.Dryer] = Math.Clamp(
                    amount / Math.Max(1e-6f, dryerRate * (float)hours),
                    0f,
                    1f);
                AddAction(actions, "dry wet organic matter", amount, "кг сух. в-ва", CaravanOrganKind.Dryer);
            }
        }

        if (state.WetOrganicDryKg <= 1e-6f) return;
        var days = (float)(hours / 24d);
        var humidityFactor = Math.Clamp(observation.Cell.HumidityMm / 12f, 0.35f, 1.4f);
        var temperatureFactor = Math.Clamp((observation.Cell.AirTemperatureC + 5f) / 25f, 0f, 1.3f);
        var spoilage = Math.Min(
            state.WetOrganicDryKg,
            state.WetOrganicDryKg * 0.012f * humidityFactor * temperatureFactor * days);
        if (spoilage <= 1e-7f) return;
        var wetMassBefore = state.WetOrganicDryKg;
        var waterSpoiled = state.WetOrganicWaterLiters * spoilage / wetMassBefore;
        var nitrogen = ProportionalNitrogen(spoilage, wetMassBefore + state.DryOrganicKg);
        state.WetOrganicDryKg -= spoilage;
        state.WetOrganicWaterLiters -= waterSpoiled;
        state.OrganicNitrogenKg -= nitrogen;
        state.PendingOrganicResidueKg += spoilage;
        state.PendingNitrogenKg += nitrogen;
        state.PendingWaterVaporLiters += waterSpoiled;
        AddAction(actions, "spoil wet organic matter", spoilage, "кг");
    }

    private NeedConsumption ConsumeMaintenance(
        CaravanObservation observation,
        StepAccounting accounting,
        List<CaravanActionRecord> actions,
        double hours)
    {
        var days = (float)(hours / 24d);
        var wind = MathF.Sqrt(
            observation.Cell.WindXMs * observation.Cell.WindXMs
            + observation.Cell.WindYMs * observation.Cell.WindYMs);
        var heatStress = Math.Clamp((observation.Cell.AirTemperatureC - 18f) / 18f, 0f, 1f);
        var coldStress = Math.Clamp((5f - observation.Cell.AirTemperatureC) / 25f, 0f, 1f);
        var waterDemand = BaseWaterLitersDay * (1f + heatStress * 0.8f + wind / 30f) * days;
        var organicDemand = BaseOrganicKgDay * (1f + coldStress * 0.55f) * days;
        var electricityDemand = BaseElectricityKwhDay * days;

        foreach (var organ in state.Organs.Values)
        {
            var definition = organ.Definition;
            waterDemand += organ.Size * definition.MaintenanceWaterLitersPerSizeDay * days;
            organicDemand += organ.Size * definition.MaintenanceOrganicKgPerSizeDay * days;
            electricityDemand += organ.Size * definition.MaintenanceElectricityKwhPerSizeDay * days;
        }

        var water = ConsumeWater(waterDemand);
        var organic = ConsumeOrganic(organicDemand, 0.28f, accounting);
        var electricity = TakeElectricity(electricityDemand, accounting);
        AddAction(actions, "maintain caravan water circuit", water, "л");
        AddAction(actions, "maintain living structure", organic, "кг");
        AddAction(actions, "maintain electrical systems", electricity, "кВт·ч");
        return new NeedConsumption(
            waterDemand,
            water,
            organicDemand,
            organic,
            electricityDemand,
            electricity);
    }

    private MovementResult Move(
        CaravanObservation observation,
        CaravanDecision decision,
        StepAccounting accounting,
        List<CaravanActionRecord> actions,
        Dictionary<CaravanOrganKind, float> usages,
        double hours,
        float cellSizeMeters)
    {
        var targetX = Math.Clamp(decision.TargetXCells, 0f, observation.WorldWidthCells - 1f);
        var targetY = Math.Clamp(decision.TargetYCells, 0f, observation.WorldHeightCells - 1f);
        var dxCells = targetX - state.XCells;
        var dyCells = targetY - state.YCells;
        var distanceCells = MathF.Sqrt(dxCells * dxCells + dyCells * dyCells);
        if (distanceCells < 0.2f
            || decision.Activity is CaravanActivity.Rest or CaravanActivity.Maintain or CaravanActivity.ReturnResidues)
        {
            usages[CaravanOrganKind.Frame] = Math.Clamp(state.TotalMassKg / Math.Max(1f, state.FrameCapacityKg), 0f, 1.5f);
            return new MovementResult(0f, []);
        }

        var directionX = dxCells / distanceCells;
        var directionY = dyCells / distanceCells;
        var wind = MathF.Sqrt(
            observation.Cell.WindXMs * observation.Cell.WindXMs
            + observation.Cell.WindYMs * observation.Cell.WindYMs);
        var windDot = wind > 1e-6f
            ? (observation.Cell.WindXMs * directionX + observation.Cell.WindYMs * directionY) / wind
            : 0f;
        var alignment = Math.Clamp((windDot + 0.25f) / 1.25f, 0f, 1f);
        var sailArea = state.OrganSize(CaravanOrganKind.Sail);
        var sailPowerKw = 0.5f * 1.2f * sailArea * 0.25f * wind * wind * wind / 1000f
            * alignment;
        var sailPotential = sailPowerKw * (float)hours;

        var motorPower = state.OrganSize(CaravanOrganKind.ElectricMotor);
        var motorSpeedKmh = 0.6f + MathF.Sqrt(Math.Max(0f, motorPower)) * 0.12f;
        var sailSpeedKmh = wind * (0.18f + alignment * 0.34f);
        var maximumSpeedKmh = Math.Max(0.35f, Math.Max(motorSpeedKmh, sailSpeedKmh));
        var desiredKm = Math.Min(
            distanceCells * cellSizeMeters / 1000f,
            maximumSpeedKmh * (float)hours);

        // A sail or a charged motor cannot bypass the material cost of moving
        // the living body. Limit the route before changing position so every
        // completed kilometre can pay its water and organic requirements.
        var materialLimitedKm = Math.Min(
            state.WaterLiters / 0.75f,
            (state.WetOrganicDryKg + state.DryOrganicKg) / 0.035f);
        desiredKm = Math.Min(desiredKm, Math.Max(0f, materialLimitedKm));

        var mass = Math.Max(1000f, state.TotalMassKg);
        var frameRatio = mass / Math.Max(1f, state.FrameCapacityKg);
        var loadPenalty = frameRatio <= 1f ? 1f : 1f / (frameRatio * frameRatio);
        desiredKm *= loadPenalty;
        var rolling = 0.016f
            + observation.Cell.Slope * 0.18f
            + Math.Clamp(observation.Cell.SurfaceWaterMm / 12f, 0f, 0.08f)
            + observation.Cell.SoilCompactionFraction * 0.012f;
        if (observation.Cell.FrozenSoilFraction > 0.65f && observation.Cell.SnowWaterEquivalentMm < 5f)
            rolling *= 0.82f;
        var energyPerKm = Math.Max(0.08f, mass * 9.81f * rolling / 3600f);
        var desiredEnergy = desiredKm * energyPerKm;
        var sailUsed = Math.Min(sailPotential, desiredEnergy);
        var motorMechanicalDemand = Math.Max(0f, desiredEnergy - sailUsed);
        var motorMechanicalCapacity = Math.Min(
            motorPower * (float)hours,
            state.StoredElectricityKwh * MotorEfficiency);
        var motorMechanical = Math.Min(motorMechanicalDemand, motorMechanicalCapacity);
        var motorElectricity = motorMechanical / MotorEfficiency;
        TakeElectricity(motorElectricity, accounting);
        var motorWasteHeat = Math.Max(0f, motorElectricity - motorMechanical);
        AddHeat(motorWasteHeat, accounting);

        var availableEnergy = sailUsed + motorMechanical;
        var actualKm = Math.Min(desiredKm, availableEnergy / energyPerKm);
        if (actualKm <= 1e-6f) return new MovementResult(0f, []);
        var actualCells = actualKm * 1000f / cellSizeMeters;
        var fromX = state.XCells;
        var fromY = state.YCells;
        state.XCells += directionX * actualCells;
        state.YCells += directionY * actualCells;
        state.CumulativeSailMechanicalKwh += sailUsed;
        state.CumulativeMotorMechanicalKwh += motorMechanical;
        usages[CaravanOrganKind.Sail] = Math.Clamp(
            sailUsed / Math.Max(1e-6f, sailPotential),
            0f,
            1f);
        usages[CaravanOrganKind.ElectricMotor] = Math.Clamp(
            motorMechanical / Math.Max(1e-6f, motorPower * (float)hours),
            0f,
            1f);
        usages[CaravanOrganKind.Frame] = Math.Clamp(frameRatio, 0f, 1.5f);
        AddAction(actions, "move", actualKm, "км");
        return new MovementResult(
            actualKm,
            BuildTrail(fromX, fromY, state.XCells, state.YCells, actualKm));
    }

    private NeedConsumption ConsumeMovementNeeds(
        float distanceKilometers,
        StepAccounting accounting,
        List<CaravanActionRecord> actions)
    {
        var waterDemand = distanceKilometers * 0.75f;
        var organicDemand = distanceKilometers * 0.035f;
        var water = ConsumeWater(waterDemand);
        var organic = ConsumeOrganic(organicDemand, 0.22f, accounting);
        AddAction(actions, "movement water loss", water, "л");
        AddAction(actions, "movement organic maintenance", organic, "кг");
        return new NeedConsumption(waterDemand, water, organicDemand, organic, 0f, 0f);
    }

    private void GrowAndAtrophy(
        float waterFulfillment,
        float organicFulfillment,
        float thermalFulfillment,
        StepAccounting accounting,
        List<CaravanActionRecord> actions,
        Dictionary<CaravanOrganKind, float> usages,
        double hours)
    {
        if (waterFulfillment > 0.95f
            && organicFulfillment > 0.95f
            && thermalFulfillment > 0.85f
            && state.StructuralReserveKg > 0.1f
            && state.WaterLiters > state.WaterCapacityLiters * 0.2f
            && state.DryOrganicKg > Math.Max(10f, state.OrganicCapacityKg * 0.05f)
            && state.StoredElectricityKwh > state.BatteryCapacityKwh * 0.08f)
        {
            var growthCapacityKg = state.OrganSize(CaravanOrganKind.GrowthTissue)
                * (float)(hours / 24d);
            var weighted = state.Organs.Values
                .Where(item => item.GrowthPriority > 0f && item.Size < item.Definition.MaximumSize)
                .Select(item => (
                    Organ: item,
                    Weight: item.GrowthPriority * (0.35f + item.UsageEma)))
                .Where(item => item.Weight > 0f)
                .ToArray();
            var weightSum = weighted.Sum(item => item.Weight);
            foreach (var item in weighted)
            {
                var desiredStructure = growthCapacityKg * item.Weight / Math.Max(1e-6f, weightSum);
                desiredStructure = Math.Min(desiredStructure, state.StructuralReserveKg);
                var resourceFactor = Math.Min(
                    state.WaterLiters / Math.Max(1e-6f, desiredStructure * 0.4f),
                    Math.Min(
                        state.DryOrganicKg / Math.Max(1e-6f, desiredStructure * 0.25f),
                        state.StoredElectricityKwh / Math.Max(1e-6f, desiredStructure * 0.8f)));
                var structure = desiredStructure * Math.Clamp(resourceFactor, 0f, 1f);
                var usedStructure = item.Organ.GrowByStructuralMass(structure);
                if (usedStructure <= 1e-7f) continue;
                state.StructuralReserveKg -= usedStructure;
                ConsumeWater(usedStructure * 0.4f);
                ConsumeOrganic(usedStructure * 0.25f, 0.12f, accounting);
                TakeElectricity(usedStructure * 0.8f, accounting);
                usages[CaravanOrganKind.GrowthTissue] = Math.Max(
                    usages[CaravanOrganKind.GrowthTissue],
                    Math.Clamp(usedStructure / Math.Max(1e-6f, growthCapacityKg), 0f, 1f));
                AddAction(actions, $"grow {item.Organ.Definition.Name}", usedStructure, "кг структуры", item.Organ.Definition.Kind);
            }
        }

        foreach (var organ in state.Organs.Values)
        {
            var (recovered, lost) = organ.Atrophy(hours);
            if (recovered <= 0f && lost <= 0f) continue;
            state.StructuralReserveKg += recovered;
            accounting.StructuralLost += lost;
            AddAction(actions, $"atrophy {organ.Definition.Name}", recovered + lost, "кг структуры", organ.Definition.Kind);
        }
    }

    private void ApplyMaintenanceToOrgans(float fulfillment, double hours)
    {
        foreach (var organ in state.Organs.Values)
        {
            var loadPenalty = organ.Usage > 1f ? (organ.Usage - 1f) * 0.2f : 0f;
            organ.ApplyMaintenance(Math.Max(0f, fulfillment - loadPenalty), hours);
        }
    }

    private void ApplyGrowthPriorities(IReadOnlyDictionary<CaravanOrganKind, float> priorities)
    {
        var loadRatio = state.TotalMassKg / Math.Max(1f, state.FrameCapacityKg);
        foreach (var organ in state.Organs.Values)
        {
            organ.GrowthPriority = priorities.TryGetValue(organ.Definition.Kind, out var priority)
                ? Math.Clamp(priority, 0f, 1f)
                : 0f;
            if (loadRatio > 0.88f && organ.Definition.Kind != CaravanOrganKind.Frame)
            {
                organ.GrowthPriority *= 0.12f;
            }
        }
        if (loadRatio > 0.88f)
        {
            state.Organs[CaravanOrganKind.Frame].GrowthPriority = 1f;
        }
    }

    private void ClampStores(StepAccounting accounting)
    {
        if (state.StoredElectricityKwh > state.BatteryCapacityKwh)
        {
            accounting.ElectricityLost += state.StoredElectricityKwh - state.BatteryCapacityKwh;
            state.StoredElectricityKwh = state.BatteryCapacityKwh;
        }
        if (state.StoredHeatKwh > state.HeatCapacityKwh)
        {
            var excess = state.StoredHeatKwh - state.HeatCapacityKwh;
            state.StoredHeatKwh -= excess;
            state.PendingWasteHeatKwh += excess;
        }
        var organic = state.WetOrganicDryKg + state.DryOrganicKg;
        if (organic > state.OrganicCapacityKg)
        {
            var overflow = organic - state.OrganicCapacityKg;
            var dryOverflow = Math.Min(state.DryOrganicKg, overflow);
            state.DryOrganicKg -= dryOverflow;
            var wetOverflow = overflow - dryOverflow;
            if (wetOverflow > 0f)
            {
                var wetBefore = state.WetOrganicDryKg;
                var water = wetBefore > 1e-6f
                    ? state.WetOrganicWaterLiters * wetOverflow / wetBefore
                    : 0f;
                state.WetOrganicDryKg -= wetOverflow;
                state.WetOrganicWaterLiters -= water;
                state.PendingWaterVaporLiters += water;
            }
            state.PendingOrganicResidueKg += overflow;
        }
    }

    private void UpdateHibernationState(
        float waterFulfillment,
        float organicFulfillment,
        float thermalFulfillment,
        List<CaravanActionRecord> actions,
        double hours)
    {
        state.WaterDeficitHours = UpdateStressDebt(state.WaterDeficitHours, waterFulfillment, hours);
        state.OrganicDeficitHours = UpdateStressDebt(state.OrganicDeficitHours, organicFulfillment, hours);
        state.ThermalDeficitHours = UpdateStressDebt(state.ThermalDeficitHours, thermalFulfillment, hours);
        var overload = state.TotalMassKg / Math.Max(1f, state.FrameCapacityKg);
        state.StructuralOverloadHours = overload > 1.35f
            ? state.StructuralOverloadHours + (float)hours * (overload - 1.1f)
            : Math.Max(0f, state.StructuralOverloadHours - (float)hours * 0.5f);

        var reason = CaravanHibernationReason.None;
        if (state.WaterDeficitHours >= 30f * 24f
            || waterFulfillment <= 0.05f && state.WaterDeficitHours >= 24f)
            reason = CaravanHibernationReason.WaterShortage;
        else if (state.OrganicDeficitHours >= 45f * 24f
                 || organicFulfillment <= 0.05f && state.OrganicDeficitHours >= 24f)
            reason = CaravanHibernationReason.OrganicShortage;
        else if (state.ThermalDeficitHours >= 30f * 24f
                 || thermalFulfillment <= 0.05f && state.ThermalDeficitHours >= 24f)
            reason = CaravanHibernationReason.ThermalProtection;
        else if (state.StructuralOverloadHours >= 30f * 24f)
            reason = CaravanHibernationReason.StructuralOverload;

        if (reason == CaravanHibernationReason.None) return;
        state.OperatingMode = CaravanOperatingMode.Hibernating;
        state.HibernationReason = reason;
        state.CurrentHibernationHours = 0f;
        state.HibernationEpisodes++;
        AddAction(actions, "enter hibernation", 1f, "эпизод");
    }

    private InternalStep AdvanceHibernation(
        CaravanObservation observation,
        double hours)
    {
        var accounting = StepAccounting.Start(state);
        var actions = new List<CaravanActionRecord>(2);
        var stepHours = (float)hours;
        state.CurrentHibernationHours += stepHours;
        state.CumulativeHibernationHours += stepHours;

        // Shutdown has no material throughput. Stored deficit debt can relax
        // while the body is inactive, but truly empty reserves leave it stuck
        // until an external intervention changes its physical state.
        state.WaterDeficitHours = Math.Max(0f, state.WaterDeficitHours - stepHours * 0.5f);
        state.OrganicDeficitHours = Math.Max(0f, state.OrganicDeficitHours - stepHours * 0.5f);
        state.ThermalDeficitHours = Math.Max(0f, state.ThermalDeficitHours - stepHours * 0.5f);
        var passiveThermalAlpha = 1f - MathF.Exp(-stepHours / (24f * 4f));
        state.BodyTemperatureC +=
            (observation.Cell.AirTemperatureC - state.BodyTemperatureC) * passiveThermalAlpha;

        foreach (var organ in state.Organs.Values) organ.RecordUsage(0f, hours);
        AddAction(actions, "hibernate", stepHours, "ч");
        state.SimulatedHours += hours;
        state.RecordActivity(CaravanActivity.Hibernate);
        state.RecordRegime(observation.Regime, hours);

        var hasWater = state.WaterLiters >= BaseWaterLitersDay * 0.25f;
        var hasOrganic = state.WetOrganicDryKg + state.DryOrganicKg >= BaseOrganicKgDay * 0.5f;
        var debtsRecovered = state.WaterDeficitHours <= 6f
            && state.OrganicDeficitHours <= 6f
            && state.ThermalDeficitHours <= 6f;
        var structurallyStable = state.TotalMassKg <= state.FrameCapacityKg * 1.25f
            && state.StructuralOverloadHours < 30f * 24f;
        if (hasWater && hasOrganic && debtsRecovered && structurallyStable)
        {
            state.OperatingMode = CaravanOperatingMode.Active;
            state.HibernationReason = CaravanHibernationReason.None;
            state.CurrentHibernationHours = 0f;
            AddAction(actions, "leave hibernation", 1f, "эпизод");
        }

        return new InternalStep(
            accounting,
            actions.ToArray(),
            [],
            0f,
            hasWater ? 1f : 0f,
            hasOrganic ? 1f : 0f,
            debtsRecovered ? 1f : 0f);
    }

    private float ConsumeWater(float demand)
    {
        if (demand <= 0f) return 0f;
        var consumed = Math.Min(state.WaterLiters, demand);
        state.WaterLiters -= consumed;
        state.PendingWaterVaporLiters += consumed;
        return consumed;
    }

    private float ConsumeOrganic(float demand, float residueFraction, StepAccounting accounting)
    {
        if (demand <= 0f) return 0f;
        var totalBefore = state.WetOrganicDryKg + state.DryOrganicKg;
        var dry = Math.Min(state.DryOrganicKg, demand);
        state.DryOrganicKg -= dry;
        var remaining = demand - dry;
        var wet = Math.Min(state.WetOrganicDryKg, remaining);
        var wetBefore = state.WetOrganicDryKg;
        var wetWater = wetBefore > 1e-6f
            ? state.WetOrganicWaterLiters * wet / wetBefore
            : 0f;
        state.WetOrganicDryKg -= wet;
        state.WetOrganicWaterLiters -= wetWater;
        state.PendingWaterVaporLiters += wetWater;
        var consumed = dry + wet;
        var nitrogen = totalBefore > 1e-6f
            ? Math.Min(state.OrganicNitrogenKg, state.OrganicNitrogenKg * consumed / totalBefore)
            : 0f;
        state.OrganicNitrogenKg -= nitrogen;
        var residue = consumed * residueFraction;
        state.PendingOrganicResidueKg += residue;
        state.PendingNitrogenKg += nitrogen;
        accounting.OrganicLost += consumed - residue;
        return consumed;
    }

    private float ProportionalNitrogen(float organicKg, float totalOrganicBefore)
    {
        if (organicKg <= 0f || totalOrganicBefore <= 1e-6f) return 0f;
        return Math.Min(state.OrganicNitrogenKg, state.OrganicNitrogenKg * organicKg / totalOrganicBefore);
    }

    private float TakeElectricity(float demand, StepAccounting accounting)
    {
        if (demand <= 0f) return 0f;
        var consumed = Math.Min(state.StoredElectricityKwh, demand);
        state.StoredElectricityKwh -= consumed;
        accounting.ElectricityConsumed += consumed;
        return consumed;
    }

    private float TakeHeat(float demand, StepAccounting accounting)
    {
        if (demand <= 0f) return 0f;
        var consumed = Math.Min(state.StoredHeatKwh, demand);
        state.StoredHeatKwh -= consumed;
        accounting.HeatConsumed += consumed;
        return consumed;
    }

    private void AddHeat(float heat, StepAccounting accounting)
    {
        if (heat <= 0f) return;
        state.StoredHeatKwh += heat;
        accounting.HeatGenerated += heat;
    }

    private static CaravanTrailCell[] BuildTrail(
        float fromX,
        float fromY,
        float toX,
        float toY,
        float distanceKilometers)
    {
        var distanceCells = MathF.Sqrt((toX - fromX) * (toX - fromX) + (toY - fromY) * (toY - fromY));
        var count = Math.Max(1, (int)MathF.Ceiling(distanceCells));
        var result = new List<CaravanTrailCell>(count);
        for (var step = 1; step <= count; step++)
        {
            var x = (int)MathF.Round(fromX + (toX - fromX) * step / count);
            var y = (int)MathF.Round(fromY + (toY - fromY) * step / count);
            if (result.Count > 0 && result[^1].X == x && result[^1].Y == y) continue;
            result.Add(new CaravanTrailCell(x, y, distanceKilometers / count));
        }
        return result.ToArray();
    }

    private static float Fulfillment(float demand, float supplied) =>
        demand <= 1e-7f ? 1f : Math.Clamp(supplied / demand, 0f, 1f);

    private static float CellValue(CellSnapshot cell, SimulationLayer layer) =>
        cell.States.First(item => item.State == layer).Value;

    private static float RateUsage(float amount, float rate, double hours) =>
        rate <= 1e-7f || hours <= 0d
            ? 0f
            : Math.Clamp(amount / (rate * (float)hours), 0f, 1.5f);

    private static float UpdateStressDebt(float current, float fulfillment, double hours) =>
        fulfillment < 0.7f
            ? current + (float)hours * (1f - fulfillment)
            : Math.Max(0f, current - (float)hours * 0.65f);

    private static void AddAction(
        List<CaravanActionRecord> actions,
        string action,
        float amount,
        string unit,
        CaravanOrganKind? organ = null)
    {
        if (amount > 1e-7f && float.IsFinite(amount))
        {
            actions.Add(new CaravanActionRecord(action, amount, unit, organ));
        }
    }

    internal sealed record InternalStep(
        StepAccounting Accounting,
        CaravanActionRecord[] Actions,
        CaravanTrailCell[] Trail,
        float DistanceKilometers,
        float WaterFulfillment,
        float OrganicFulfillment,
        float ThermalFulfillment);

    internal sealed class StepAccounting
    {
        private StepAccounting(
            float electricityStart,
            float waterStart,
            float organicStart,
            float structuralStart,
            float heatStart)
        {
            ElectricityStart = electricityStart;
            WaterStart = waterStart;
            OrganicStart = organicStart;
            StructuralStart = structuralStart;
            HeatStart = heatStart;
        }

        public float ElectricityStart { get; }
        public float WaterStart { get; }
        public float OrganicStart { get; }
        public float StructuralStart { get; }
        public float HeatStart { get; }
        public float WaterInput { get; set; }
        public float OrganicInput { get; set; }
        public float StructuralInput { get; set; }
        public float ElectricityGenerated { get; set; }
        public float HeatGenerated { get; set; }
        public float ElectricityConsumed { get; set; }
        public float HeatConsumed { get; set; }
        public float WaterOutput { get; set; }
        public float OrganicOutput { get; set; }
        public float HeatOutput { get; set; }
        public float ElectricityLost { get; set; }
        public float OrganicLost { get; set; }
        public float StructuralLost { get; set; }

        public static StepAccounting Start(CaravanState value) => new(
            value.StoredElectricityKwh,
            WaterTotal(value),
            OrganicTotal(value),
            value.TotalStructuralMassKg,
            HeatTotal(value));

        public CaravanStepLedger Complete(CaravanState value)
        {
            var electricityEnd = value.StoredElectricityKwh;
            var waterEnd = WaterTotal(value);
            var organicEnd = OrganicTotal(value);
            var structuralEnd = value.TotalStructuralMassKg;
            var heatEnd = HeatTotal(value);
            return new CaravanStepLedger(
                Balance(
                    CaravanCircuit.Electricity,
                    ElectricityStart,
                    0f,
                    ElectricityGenerated,
                    ElectricityConsumed,
                    0f,
                    ElectricityLost,
                    electricityEnd),
                Balance(
                    CaravanCircuit.Water,
                    WaterStart,
                    WaterInput,
                    0f,
                    0f,
                    WaterOutput,
                    0f,
                    waterEnd),
                Balance(
                    CaravanCircuit.Organic,
                    OrganicStart,
                    OrganicInput,
                    0f,
                    0f,
                    OrganicOutput,
                    OrganicLost,
                    organicEnd),
                Balance(
                    CaravanCircuit.Structural,
                    StructuralStart,
                    StructuralInput,
                    0f,
                    0f,
                    0f,
                    StructuralLost,
                    structuralEnd),
                Balance(
                    CaravanCircuit.Heat,
                    HeatStart,
                    0f,
                    HeatGenerated,
                    HeatConsumed,
                    HeatOutput,
                    0f,
                    heatEnd));
        }

        private static CaravanCircuitBalance Balance(
            CaravanCircuit circuit,
            float start,
            float input,
            float generated,
            float consumed,
            float output,
            float lost,
            float end)
        {
            var error = start + input + generated - consumed - output - lost - end;
            return new CaravanCircuitBalance(
                circuit,
                start,
                input,
                generated,
                consumed,
                output,
                lost,
                end,
                error);
        }

        private static float WaterTotal(CaravanState value) =>
            value.WaterLiters
            + value.SnowWaterLiters
            + value.WetOrganicWaterLiters
            + value.PendingWaterVaporLiters
            + value.PendingSurfaceWaterReturnLiters;

        private static float OrganicTotal(CaravanState value) =>
            value.WetOrganicDryKg
            + value.DryOrganicKg
            + value.PendingOrganicResidueKg;

        private static float HeatTotal(CaravanState value) =>
            value.StoredHeatKwh + value.PendingWasteHeatKwh;
    }

    private sealed record NeedConsumption(
        float WaterDemand,
        float WaterConsumed,
        float OrganicDemand,
        float OrganicConsumed,
        float ElectricityDemand,
        float ElectricityConsumed);

    private sealed record MovementResult(
        float DistanceKilometers,
        CaravanTrailCell[] Trail);
}
