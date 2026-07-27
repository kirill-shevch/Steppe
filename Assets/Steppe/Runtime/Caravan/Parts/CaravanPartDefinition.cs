using System;
using System.Collections.Generic;

namespace Steppe.Caravan
{
    /// <summary>
    /// Immutable construction data shared by procedural greybox parts, the future
    /// construction inventory and save-game restoration. Runtime state never lives
    /// in this definition.
    /// </summary>
    public sealed class CaravanPartDefinition
    {
        public CaravanPartDefinition(
            CaravanPartKind kind,
            string id,
            string displayName,
            int footprintWidth,
            int footprintLength,
            float massKilograms,
            float capacity)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException("A part definition requires a stable id.", nameof(id));
            }

            Kind = kind;
            Id = id;
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? id : displayName;
            FootprintWidth = Math.Max(1, footprintWidth);
            FootprintLength = Math.Max(1, footprintLength);
            MassKilograms = Math.Max(0.1f, massKilograms);
            Capacity = Math.Max(0f, capacity);
        }

        public CaravanPartKind Kind { get; }
        public string Id { get; }
        public string DisplayName { get; }
        public int FootprintWidth { get; }
        public int FootprintLength { get; }
        public float MassKilograms { get; }
        public float Capacity { get; }
    }

    /// <summary>
    /// The canonical technical-part catalogue. Keeping definitions outside the
    /// presentation factory lets construction, resource networks and persistence
    /// address the same parts without depending on greybox geometry.
    /// </summary>
    public static class CaravanPartCatalog
    {
        private static readonly CaravanPartDefinition[] Definitions =
        {
            new CaravanPartDefinition(
                CaravanPartKind.Sail,
                "sail",
                "Sail Module",
                2,
                2,
                185f,
                65f),
            new CaravanPartDefinition(
                CaravanPartKind.PhotovoltaicLeaves,
                "photovoltaic-leaves",
                "Photovoltaic Leaves",
                3,
                3,
                180f,
                18f),
            new CaravanPartDefinition(
                CaravanPartKind.Battery,
                "battery",
                "Electric Battery",
                2,
                2,
                310f,
                120f),
            new CaravanPartDefinition(
                CaravanPartKind.WaterReservoir,
                "water-reservoir",
                "Water Reservoir",
                2,
                3,
                820f,
                900f),
            new CaravanPartDefinition(
                CaravanPartKind.DualModePump,
                "dual-mode-pump",
                "Dual-mode Pump",
                1,
                2,
                125f,
                24f),
            new CaravanPartDefinition(
                CaravanPartKind.Radiator,
                "radiator",
                "Wind Radiator",
                2,
                2,
                175f,
                32f),
            new CaravanPartDefinition(
                CaravanPartKind.Biofurnace,
                "biofurnace",
                "Biofurnace",
                2,
                2,
                285f,
                80f),
            new CaravanPartDefinition(
                CaravanPartKind.BiofuelEngine,
                "biofuel-engine",
                "Biofuel Engine",
                2,
                2,
                430f,
                55f),
            new CaravanPartDefinition(
                CaravanPartKind.ElectricMotor,
                "electric-motor",
                "Electric Motor",
                2,
                2,
                255f,
                55f),
            new CaravanPartDefinition(
                CaravanPartKind.Harvester,
                "harvester",
                "Biomass Harvester",
                4,
                2,
                380f,
                90f),
            new CaravanPartDefinition(
                CaravanPartKind.GrassDryer,
                "grass-dryer",
                "Grass Dryer",
                2,
                3,
                315f,
                240f),
            new CaravanPartDefinition(
                CaravanPartKind.BiomassStorage,
                "biomass-storage",
                "Dry Biomass Storage",
                2,
                3,
                270f,
                600f),
            new CaravanPartDefinition(
                CaravanPartKind.Transmission,
                "transmission",
                "Transmission",
                1,
                2,
                190f,
                8f),
            new CaravanPartDefinition(
                CaravanPartKind.CouplingRope,
                "coupling-rope",
                "Coupling Rope",
                2,
                1,
                95f,
                24f)
        };

        private static readonly Dictionary<CaravanPartKind, CaravanPartDefinition> ByKind =
            BuildByKind();
        private static readonly Dictionary<string, CaravanPartDefinition> ById =
            BuildById();

        public static IReadOnlyList<CaravanPartDefinition> All => Definitions;

        public static CaravanPartDefinition Get(CaravanPartKind kind)
        {
            if (!ByKind.TryGetValue(kind, out var definition))
            {
                throw new ArgumentOutOfRangeException(nameof(kind), kind, null);
            }

            return definition;
        }

        public static bool TryGet(string id, out CaravanPartDefinition definition)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                definition = null;
                return false;
            }

            return ById.TryGetValue(id, out definition);
        }

        private static Dictionary<CaravanPartKind, CaravanPartDefinition> BuildByKind()
        {
            var result = new Dictionary<CaravanPartKind, CaravanPartDefinition>(
                Definitions.Length);
            for (var index = 0; index < Definitions.Length; index++)
            {
                result.Add(Definitions[index].Kind, Definitions[index]);
            }

            return result;
        }

        private static Dictionary<string, CaravanPartDefinition> BuildById()
        {
            var result = new Dictionary<string, CaravanPartDefinition>(
                Definitions.Length,
                StringComparer.Ordinal);
            for (var index = 0; index < Definitions.Length; index++)
            {
                result.Add(Definitions[index].Id, Definitions[index]);
            }

            return result;
        }
    }
}
