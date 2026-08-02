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
            float capacity,
            string description = "")
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException("A part definition requires a stable id.", nameof(id));
            }

            Kind = kind;
            Id = id;
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? id : displayName;
            Description = description ?? string.Empty;
            FootprintWidth = Math.Max(1, footprintWidth);
            FootprintLength = Math.Max(1, footprintLength);
            MassKilograms = Math.Max(0.1f, massKilograms);
            Capacity = Math.Max(0f, capacity);
        }

        public CaravanPartKind Kind { get; }
        public string Id { get; }
        public string DisplayName { get; }
        public string Description { get; }
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
                "Парусный модуль",
                2,
                2,
                185f,
                65f,
                "Использует боковой ветер для движения каравана без расхода топлива."),
            new CaravanPartDefinition(
                CaravanPartKind.PhotovoltaicLeaves,
                "photovoltaic-leaves",
                "Солнечные листья",
                3,
                3,
                180f,
                18f,
                "Вырабатывают электричество на свету; эффективность зависит от ориентации и облачности."),
            new CaravanPartDefinition(
                CaravanPartKind.Battery,
                "battery",
                "Аккумулятор",
                2,
                2,
                310f,
                120f,
                "Накапливает энергию и питает подключённые электрические модули."),
            new CaravanPartDefinition(
                CaravanPartKind.WaterReservoir,
                "water-reservoir",
                "Водяной резервуар",
                2,
                3,
                820f,
                900f,
                "Хранит добытую воду и служит основой замкнутого жидкостного контура."),
            new CaravanPartDefinition(
                CaravanPartKind.DualModePump,
                "dual-mode-pump",
                "Двухрежимный насос",
                1,
                2,
                125f,
                24f,
                "Добывает воду из влажной почвы или прокачивает её по контуру; требует энергии."),
            new CaravanPartDefinition(
                CaravanPartKind.Radiator,
                "radiator",
                "Ветровой радиатор",
                2,
                2,
                175f,
                32f,
                "Отводит тепло потоком воздуха и замыкает рабочий водяной контур."),
            new CaravanPartDefinition(
                CaravanPartKind.Biofurnace,
                "biofurnace",
                "Биопечь",
                2,
                2,
                285f,
                80f,
                "Сжигает сухую биомассу и превращает её в полезную тепловую энергию."),
            new CaravanPartDefinition(
                CaravanPartKind.BiofuelEngine,
                "biofuel-engine",
                "Биотопливный двигатель",
                2,
                2,
                430f,
                55f,
                "Превращает тепло биопечи в тягу для движения каравана."),
            new CaravanPartDefinition(
                CaravanPartKind.ElectricMotor,
                "electric-motor",
                "Электродвигатель",
                2,
                2,
                255f,
                55f,
                "Создаёт тягу из электричества; подключается к аккумулятору."),
            new CaravanPartDefinition(
                CaravanPartKind.Harvester,
                "harvester",
                "Жатка биомассы",
                4,
                2,
                380f,
                90f,
                "Собирает влажную траву во время медленного движения по густой растительности."),
            new CaravanPartDefinition(
                CaravanPartKind.GrassDryer,
                "grass-dryer",
                "Сушилка травы",
                2,
                3,
                315f,
                240f,
                "Удаляет влагу из собранной травы; особенно эффективна в тёплый сухой ветер."),
            new CaravanPartDefinition(
                CaravanPartKind.BiomassStorage,
                "biomass-storage",
                "Хранилище сухой биомассы",
                2,
                3,
                270f,
                600f,
                "Принимает готовую сухую биомассу от сушилки и хранит запас топлива."),
            new CaravanPartDefinition(
                CaravanPartKind.Transmission,
                "transmission",
                "Трансмиссия",
                1,
                2,
                190f,
                8f,
                "Передаёт механическую мощность и меняет соотношение тяги и скорости."),
            new CaravanPartDefinition(
                CaravanPartKind.CouplingRope,
                "coupling-rope",
                "Сцепной канат",
                2,
                1,
                95f,
                24f,
                "Связывает механические узлы и передаёт усилие между ними.")
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
