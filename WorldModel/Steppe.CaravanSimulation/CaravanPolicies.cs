using Steppe.Simulation;

namespace Steppe.CaravanSimulation;

public interface ICaravanPolicy
{
    string Name { get; }
    CaravanDecision Decide(CaravanObservation observation, CaravanStateSnapshot caravan);
}

public static class CaravanPolicyCatalog
{
    public static ICaravanPolicy Create(CaravanPolicyKind kind) => kind switch
    {
        CaravanPolicyKind.StayPut => new StayPutPolicy(),
        CaravanPolicyKind.NearestWater => new NearestWaterPolicy(),
        CaravanPolicyKind.FollowBiomass => new FollowBiomassPolicy(),
        CaravanPolicyKind.FollowSun => new FollowSunPolicy(),
        CaravanPolicyKind.FollowWind => new FollowWindPolicy(),
        CaravanPolicyKind.BalancedNomad => new BalancedNomadPolicy(),
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };
}

internal abstract class CaravanPolicyBase : ICaravanPolicy
{
    public abstract string Name { get; }
    public abstract CaravanDecision Decide(CaravanObservation observation, CaravanStateSnapshot caravan);

    protected static CaravanOpportunity? Find(CaravanObservation observation, params CaravanOpportunityKind[] kinds)
    {
        foreach (var kind in kinds)
        {
            var found = observation.Opportunities.Opportunities.FirstOrDefault(item => item.Kind == kind);
            if (found is not null) return found;
        }
        return null;
    }

    protected static CaravanDecision At(
        CaravanObservation observation,
        CaravanActivity activity,
        CaravanOpportunity? opportunity,
        IReadOnlyDictionary<CaravanOrganKind, float> growth,
        string reason) =>
        new(
            activity,
            opportunity?.X ?? observation.XCells,
            opportunity?.Y ?? observation.YCells,
            growth,
            reason);

    protected static IReadOnlyDictionary<CaravanOrganKind, float> Priorities(
        params (CaravanOrganKind Organ, float Priority)[] values)
    {
        var priorities = RuntimeCompatibility.GetEnumValues<CaravanOrganKind>().ToDictionary(item => item, _ => 0f);
        foreach (var (organ, priority) in values)
        {
            priorities[organ] = Math.Clamp(priority, 0f, 1f);
        }
        return priorities;
    }

    protected static CaravanDecision EscapeFire(CaravanObservation observation)
    {
        var fire = Find(observation, CaravanOpportunityKind.Wildfire);
        if (fire is null)
        {
            return At(observation, CaravanActivity.Rest, null, Priorities(), "fire signal disappeared");
        }
        var dx = observation.XCells - fire.X;
        var dy = observation.YCells - fire.Y;
        var length = MathF.Max(1f, MathF.Sqrt(dx * dx + dy * dy));
        return new CaravanDecision(
            CaravanActivity.EscapeWildfire,
            observation.XCells + dx / length * 12f,
            observation.YCells + dy / length * 12f,
            Priorities(
                (CaravanOrganKind.ElectricMotor, 0.8f),
                (CaravanOrganKind.Sail, 0.5f),
                (CaravanOrganKind.Frame, 0.4f)),
            "wildfire inside safety radius");
    }
}

internal sealed class StayPutPolicy : CaravanPolicyBase
{
    public override string Name => "stay-put";

    public override CaravanDecision Decide(CaravanObservation observation, CaravanStateSnapshot caravan) =>
        At(
            observation,
            caravan.PendingOrganicResidueKg > 3f ? CaravanActivity.ReturnResidues : CaravanActivity.Maintain,
            null,
            Priorities(
                (CaravanOrganKind.WaterReservoir, 0.25f),
                (CaravanOrganKind.OrganicStorage, 0.25f),
                (CaravanOrganKind.ThermalOrgan, 0.2f)),
            "stationary control policy");
}

internal sealed class NearestWaterPolicy : CaravanPolicyBase
{
    public override string Name => "nearest-water";

    public override CaravanDecision Decide(CaravanObservation observation, CaravanStateSnapshot caravan)
    {
        if (Find(observation, CaravanOpportunityKind.Wildfire) is { DistanceCells: < 6f })
            return EscapeFire(observation);
        var water = Find(
            observation,
            CaravanOpportunityKind.SurfaceWater,
            CaravanOpportunityKind.SnowmeltRunoff,
            CaravanOpportunityKind.Snowpack,
            CaravanOpportunityKind.RainFront,
            CaravanOpportunityKind.GroundwaterDischarge);
        var activity = water?.Kind == CaravanOpportunityKind.Snowpack
            ? CaravanActivity.CollectSnow
            : CaravanActivity.CollectWater;
        return At(
            observation,
            activity,
            water,
            Priorities(
                (CaravanOrganKind.WaterIntake, 1f),
                (CaravanOrganKind.WaterReservoir, 0.85f),
                (CaravanOrganKind.SnowCollector, 0.65f),
                (CaravanOrganKind.ThermalOrgan, 0.45f)),
            water is null ? "following drainage while searching for water" : $"tracking {water.Kind}");
    }
}

internal sealed class FollowBiomassPolicy : CaravanPolicyBase
{
    public override string Name => "follow-biomass";

    public override CaravanDecision Decide(CaravanObservation observation, CaravanStateSnapshot caravan)
    {
        if (Find(observation, CaravanOpportunityKind.Wildfire) is { DistanceCells: < 6f })
            return EscapeFire(observation);
        var biomass = Find(
            observation,
            CaravanOpportunityKind.GreenGrowth,
            CaravanOpportunityKind.DryStandingBiomass,
            CaravanOpportunityKind.HarvesterFront);
        var activity = biomass?.Kind == CaravanOpportunityKind.DryStandingBiomass
            ? CaravanActivity.HarvestDryBiomass
            : CaravanActivity.HarvestLiveBiomass;
        return At(
            observation,
            activity,
            biomass,
            Priorities(
                (CaravanOrganKind.LiveBiomassHarvester, 1f),
                (CaravanOrganKind.DryBiomassCollector, 0.9f),
                (CaravanOrganKind.Dryer, 0.75f),
                (CaravanOrganKind.OrganicStorage, 0.65f),
                (CaravanOrganKind.Furnace, 0.45f)),
            biomass is null ? "searching along biomass gradients" : $"tracking {biomass.Kind}");
    }
}

internal sealed class FollowSunPolicy : CaravanPolicyBase
{
    public override string Name => "follow-sun";

    public override CaravanDecision Decide(CaravanObservation observation, CaravanStateSnapshot caravan)
    {
        if (Find(observation, CaravanOpportunityKind.Wildfire) is { DistanceCells: < 6f })
            return EscapeFire(observation);
        var drying = Find(observation, CaravanOpportunityKind.DryingWindow);
        var activity = caravan.WetOrganicDryKg > 10f
            ? CaravanActivity.DryOrganicMatter
            : CaravanActivity.Travel;
        return At(
            observation,
            activity,
            drying,
            Priorities(
                (CaravanOrganKind.SolarLeaf, 1f),
                (CaravanOrganKind.Battery, 0.8f),
                (CaravanOrganKind.ElectricMotor, 0.65f),
                (CaravanOrganKind.Dryer, 0.55f)),
            drying is null ? "searching for solar and drying window" : "moving into solar drying window");
    }
}

internal sealed class FollowWindPolicy : CaravanPolicyBase
{
    public override string Name => "follow-wind";

    public override CaravanDecision Decide(CaravanObservation observation, CaravanStateSnapshot caravan)
    {
        if (Find(observation, CaravanOpportunityKind.Wildfire) is { DistanceCells: < 6f })
            return EscapeFire(observation);
        var wind = observation.Opportunities.LocalFlows.First(item => item.Process == VectorProcess.Wind);
        var magnitude = MathF.Max(1e-6f, wind.Magnitude);
        var targetX = observation.XCells + wind.X / magnitude * 16f;
        var targetY = observation.YCells + wind.Y / magnitude * 16f;
        return new CaravanDecision(
            CaravanActivity.Travel,
            targetX,
            targetY,
            Priorities(
                (CaravanOrganKind.Sail, 1f),
                (CaravanOrganKind.Frame, 0.55f),
                (CaravanOrganKind.Radiator, 0.35f)),
            "following the observed wind vector");
    }
}

internal sealed class BalancedNomadPolicy : CaravanPolicyBase
{
    public override string Name => "balanced-nomad";

    public override CaravanDecision Decide(CaravanObservation observation, CaravanStateSnapshot caravan)
    {
        if (Find(observation, CaravanOpportunityKind.Wildfire) is { DistanceCells: < 7f })
            return EscapeFire(observation);

        var waterCapacity = caravan.Organs[CaravanOrganKind.WaterReservoir].Size;
        var organicCapacity = caravan.Organs[CaravanOrganKind.OrganicStorage].Size;
        var organic = caravan.WetOrganicDryKg + caravan.DryOrganicKg;
        if (caravan.WaterLiters < waterCapacity * 0.32f)
        {
            return new NearestWaterPolicy().Decide(observation, caravan) with
            {
                Reason = "balanced policy: water reserve below 32%"
            };
        }
        var organicNitrogenFraction = caravan.OrganicNitrogenKg / Math.Max(1f, organic);
        var nitrogenRichGrowth = Find(observation, CaravanOpportunityKind.GreenGrowth);
        var localLiveBiomass = observation.Cell.States
            .First(item => item.State == SimulationLayer.LiveBiomass)
            .Value;
        if (organicNitrogenFraction < 0.0117f
            && (nitrogenRichGrowth is not null || localLiveBiomass > 20f))
        {
            return At(
                observation,
                CaravanActivity.HarvestLiveBiomass,
                nitrogenRichGrowth,
                Priorities(
                    (CaravanOrganKind.LiveBiomassHarvester, 1f),
                    (CaravanOrganKind.OrganicStorage, 0.65f),
                    (CaravanOrganKind.Dryer, 0.45f)),
                $"restoring organic nitrogen concentration from {organicNitrogenFraction:P1}");
        }
        if (organic < organicCapacity * 0.25f)
        {
            return new FollowBiomassPolicy().Decide(observation, caravan) with
            {
                Reason = "balanced policy: organic reserve below 25%"
            };
        }
        if (caravan.DryOrganicKg < Math.Max(120f, organicCapacity * 0.04f))
        {
            if (Find(observation, CaravanOpportunityKind.DryStandingBiomass) is { } dryFuel)
            {
                return At(
                    observation,
                    CaravanActivity.HarvestDryBiomass,
                    dryFuel,
                    Priorities(
                        (CaravanOrganKind.DryBiomassCollector, 0.9f),
                        (CaravanOrganKind.Furnace, 0.7f),
                        (CaravanOrganKind.OrganicStorage, 0.45f)),
                    "securing dry winter fuel");
            }
            if (caravan.WetOrganicDryKg > 20f)
            {
                return At(
                    observation,
                    CaravanActivity.DryOrganicMatter,
                    Find(observation, CaravanOpportunityKind.DryingWindow),
                    Priorities(
                        (CaravanOrganKind.Dryer, 1f),
                        (CaravanOrganKind.Furnace, 0.7f),
                        (CaravanOrganKind.ThermalOrgan, 0.55f)),
                    "converting wet cargo into winter fuel");
            }
        }
        if (caravan.WetOrganicDryKg > Math.Max(20f, organicCapacity * 0.08f)
            && Find(observation, CaravanOpportunityKind.DryingWindow) is { } drying)
        {
            return At(
                observation,
                CaravanActivity.DryOrganicMatter,
                drying,
                Priorities(
                    (CaravanOrganKind.Dryer, 0.9f),
                    (CaravanOrganKind.SolarLeaf, 0.6f),
                    (CaravanOrganKind.Radiator, 0.45f)),
                "curing wet organic cargo");
        }
        if (Find(observation, CaravanOpportunityKind.HarvesterMolt) is { } molt)
        {
            return At(
                observation,
                CaravanActivity.CollectChitin,
                molt,
                Priorities(
                    (CaravanOrganKind.GrowthTissue, 0.9f),
                    (CaravanOrganKind.Frame, 0.65f)),
                "following a structural-material pulse");
        }
        if (caravan.PendingOrganicResidueKg > 8f
            && Find(
                observation,
                CaravanOpportunityKind.OrganicPoorSoil,
                CaravanOpportunityKind.RecoverableSoil) is { } soil)
        {
            return At(
                observation,
                CaravanActivity.ReturnResidues,
                soil,
                Priorities((CaravanOrganKind.Frame, 0.25f)),
                "returning residues to depleted soil");
        }

        var opportunity = Find(
            observation,
            CaravanOpportunityKind.GreenGrowth,
            CaravanOpportunityKind.HarvesterFront,
            CaravanOpportunityKind.DryingWindow,
            CaravanOpportunityKind.DryStandingBiomass,
            CaravanOpportunityKind.FrozenCorridor,
            CaravanOpportunityKind.RainFront);
        return At(
            observation,
            CaravanActivity.Travel,
            opportunity,
            Priorities(
                (CaravanOrganKind.WaterReservoir, 0.35f),
                (CaravanOrganKind.OrganicStorage, 0.35f),
                (CaravanOrganKind.SolarLeaf, 0.3f),
                (CaravanOrganKind.Sail, 0.3f),
                (CaravanOrganKind.Frame, 0.25f)),
            opportunity is null ? "crossing the local process field" : $"exploring {opportunity.Kind}");
    }
}
