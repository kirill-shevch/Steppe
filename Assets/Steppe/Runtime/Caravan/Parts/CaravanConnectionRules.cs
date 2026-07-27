using System;
using System.Collections.Generic;

namespace Steppe.Caravan
{
    /// <summary>
    /// Pure compatibility rules shared by runtime networks and construction UI.
    /// Keeping these rules independent from GameObjects makes every allowed and
    /// rejected connection deterministic and directly testable.
    /// </summary>
    public static class CaravanConnectionRules
    {
        public static bool AreCompatible(
            CaravanElectricalPortKind first,
            CaravanElectricalPortKind second)
        {
            return first == CaravanElectricalPortKind.Storage
                ? second != CaravanElectricalPortKind.Storage
                : second == CaravanElectricalPortKind.Storage;
        }

        public static bool AreCompatible(
            CaravanFluidPortRole first,
            CaravanFluidPortRole second)
        {
            return Matches(
                       first,
                       second,
                       CaravanFluidPortRole.ReservoirSupply,
                       CaravanFluidPortRole.PumpInlet)
                   || Matches(
                       first,
                       second,
                       CaravanFluidPortRole.PumpOutlet,
                       CaravanFluidPortRole.RadiatorInlet)
                   || Matches(
                       first,
                       second,
                       CaravanFluidPortRole.RadiatorOutlet,
                       CaravanFluidPortRole.ReservoirReturn)
                   || Matches(
                       first,
                       second,
                       CaravanFluidPortRole.ThermalTap,
                       CaravanFluidPortRole.ReservoirReturn);
        }

        public static bool AreCompatible(
            CaravanMaterialNetworkKind networkKind,
            CaravanMaterialPortRole first,
            CaravanMaterialPortRole second)
        {
            if (GetNetworkKind(first) != networkKind
                || GetNetworkKind(second) != networkKind)
            {
                return false;
            }

            return networkKind == CaravanMaterialNetworkKind.Biomass
                ? IsBiomassPair(first, second)
                : IsMechanicalPair(first, second);
        }

        public static CaravanMaterialNetworkKind GetNetworkKind(
            CaravanMaterialPortRole role)
        {
            if (!Enum.IsDefined(typeof(CaravanMaterialPortRole), role))
            {
                throw new ArgumentOutOfRangeException(nameof(role), role, null);
            }

            return role <= CaravanMaterialPortRole.DryBiomassInput
                ? CaravanMaterialNetworkKind.Biomass
                : CaravanMaterialNetworkKind.Mechanical;
        }

        private static bool IsBiomassPair(
            CaravanMaterialPortRole first,
            CaravanMaterialPortRole second)
        {
            return Matches(
                       first,
                       second,
                       CaravanMaterialPortRole.WetBiomassOutput,
                       CaravanMaterialPortRole.WetBiomassInput)
                   || Matches(
                       first,
                       second,
                       CaravanMaterialPortRole.DryBiomassOutput,
                       CaravanMaterialPortRole.DryBiomassInput);
        }

        private static bool IsMechanicalPair(
            CaravanMaterialPortRole first,
            CaravanMaterialPortRole second)
        {
            return Matches(
                       first,
                       second,
                       CaravanMaterialPortRole.MechanicalSource,
                       CaravanMaterialPortRole.TransmissionInput)
                   || Matches(
                       first,
                       second,
                       CaravanMaterialPortRole.MechanicalSource,
                       CaravanMaterialPortRole.MechanicalConsumer)
                   || Matches(
                       first,
                       second,
                       CaravanMaterialPortRole.TransmissionOutput,
                       CaravanMaterialPortRole.MechanicalConsumer)
                   || (first == CaravanMaterialPortRole.CouplingEndpoint
                       && second == CaravanMaterialPortRole.CouplingEndpoint);
        }

        private static bool Matches<T>(
            T first,
            T second,
            T left,
            T right)
        {
            var comparer = EqualityComparer<T>.Default;
            return comparer.Equals(first, left)
                       && comparer.Equals(second, right)
                   || comparer.Equals(first, right)
                       && comparer.Equals(second, left);
        }
    }
}
