namespace Steppe.Simulation;

/// <summary>
/// The single semantic contract for every state that can be observed in the world.
/// Observer clients consume this catalog instead of maintaining their own units,
/// ranges, palettes, and descriptions.
/// </summary>
public static class StateCatalog
{
    private static readonly string[] TerrainPalette = ["#101915", "#314537", "#756f4d", "#b6a879", "#eee4c2"];
    private static readonly string[] SoilPalette = ["#171510", "#493a26", "#84653b", "#bea167", "#e2d7aa"];
    private static readonly string[] FractionPalette = ["#111713", "#283c30", "#55724b", "#91a873", "#d8d6a6"];
    private static readonly string[] HeatPalette = ["#172b50", "#4a87a8", "#d7d79b", "#dc874d", "#8d342e"];
    private static readonly string[] SunPalette = ["#111812", "#4d5131", "#b88d42", "#f0d277", "#fff3c4"];
    private static readonly string[] AirPalette = ["#171822", "#343653", "#77708a", "#b3a3a6", "#e6dac1"];
    private static readonly string[] CloudPalette = ["#12191c", "#35464d", "#76888d", "#d8dcce", "#fff9df"];
    private static readonly string[] WaterPalette = ["#101714", "#143a3c", "#176b76", "#56b7b6", "#d2e2c8"];
    private static readonly string[] DeepWaterPalette = ["#17191e", "#283553", "#405e8c", "#8a9fc0", "#d7d7c6"];
    private static readonly string[] SnowPalette = ["#182021", "#3a555b", "#7f9da0", "#d9e5df", "#fffdf1"];
    private static readonly string[] LifePalette = ["#181c14", "#334326", "#5f7936", "#9eb454", "#d9ce75"];
    private static readonly string[] DryPalette = ["#1b1913", "#4b3d24", "#896a35", "#c6a358", "#e6d59a"];
    private static readonly string[] MatterPalette = ["#171714", "#4b3b30", "#86614a", "#c39a72", "#e0ceb0"];
    private static readonly string[] CyclicPalette = ["#78a7c4", "#9bbd77", "#d9c36a", "#ce8067", "#987ab3", "#78a7c4"];
    private static readonly string[] CategoryPalette = ["#2f3c34", "#8c7eaa"];

    private static readonly StateDescriptor[] Descriptors =
    [
        D(SimulationLayer.Elevation, StateGroup.Terrain, "ЗЕМЛЯ", 10, "Абсолютная высота", "Высота", "Высота видимой поверхности над условным уровнем моря.", "м", StateKind.Raw, StateScale.Linear, 100, 850, 0, "#cbb783", TerrainPalette),
        D(SimulationLayer.Slope, StateGroup.Terrain, "ЗЕМЛЯ", 20, "Крутизна склона", "Уклон", "Локальный перепад высоты на метр поверхности.", "м/м", StateKind.Raw, StateScale.Logarithmic, 0, 0.5f, 3, "#9aab72", TerrainPalette),
        D(SimulationLayer.Aspect, StateGroup.Terrain, "ЗЕМЛЯ", 30, "Экспозиция склона", "Экспозиция", "Направление, в которое обращён склон; палитра циклическая.", "рад", StateKind.Raw, StateScale.Cyclic, -MathF.PI, MathF.PI, 2, "#9da5c2", CyclicPalette),
        D(SimulationLayer.FaultInfluence, StateGroup.Terrain, "ЗЕМЛЯ", 40, "Влияние разломов", "Разломы", "След генеративных геологических структур в рельефе и субстрате.", "доля", StateKind.Raw, StateScale.Linear, 0, 1, 2, "#bc806e", FractionPalette),
        D(SimulationLayer.RockHardness, StateGroup.Terrain, "ЗЕМЛЯ", 50, "Твёрдость породы", "Твёрдость", "Сопротивление подстилающей породы водной и ветровой эрозии.", "доля", StateKind.Raw, StateScale.Linear, 0, 1, 2, "#a79b86", TerrainPalette),
        D(SimulationLayer.DepressionStorage, StateGroup.Terrain, "ЗЕМЛЯ", 60, "Ёмкость впадины", "Впадины", "Глубина воды до геометрической отметки перелива; фактический сток определяется текущей водной поверхностью.", "мм", StateKind.Raw, StateScale.Logarithmic, 0, 100000, 1, "#5faaa5", WaterPalette),
        D(SimulationLayer.Drainage, StateGroup.Terrain, "ЗЕМЛЯ", 70, "Направление стока", "Сток", "Вектор следующего перехода поверхностной воды по рассчитанному пути перелива.", "связь", StateKind.Diagnostic, StateScale.Linear, 0, 1, 0, "#6fc4c4", WaterPalette),
        D(SimulationLayer.Catchments, StateGroup.Terrain, "ЗЕМЛЯ", 80, "Водосборы", "Водосборы", "Области, из которых поверхностный сток приходит к одному выходу.", "id", StateKind.Diagnostic, StateScale.Categorical, 0, 1, 0, "#a799cf", CategoryPalette),

        D(SimulationLayer.SoilDepth, StateGroup.Soil, "ПОЧВА", 110, "Глубина почвы", "Глубина", "Мощность доступного почвенного профиля.", "м", StateKind.Raw, StateScale.Linear, 0, 3.2f, 2, "#a98252", SoilPalette),
        D(SimulationLayer.SandFraction, StateGroup.Soil, "ПОЧВА", 120, "Доля песка", "Песок", "Массовая доля песчаной фракции почвы.", "доля", StateKind.Raw, StateScale.Linear, 0, 1, 2, "#c5a963", SoilPalette),
        D(SimulationLayer.SiltFraction, StateGroup.Soil, "ПОЧВА", 130, "Доля ила", "Ил", "Массовая доля илистой фракции почвы.", "доля", StateKind.Raw, StateScale.Linear, 0, 1, 2, "#a89569", SoilPalette),
        D(SimulationLayer.ClayFraction, StateGroup.Soil, "ПОЧВА", 140, "Доля глины", "Глина", "Массовая доля глинистой фракции почвы.", "доля", StateKind.Raw, StateScale.Linear, 0, 1, 2, "#96765f", SoilPalette),
        D(SimulationLayer.Porosity, StateGroup.Soil, "ПОЧВА", 150, "Пористость", "Пористость", "Доля почвенного объёма, способная удерживать воду и воздух.", "доля", StateKind.Raw, StateScale.Linear, 0.25f, 0.65f, 2, "#7e9a81", FractionPalette),
        D(SimulationLayer.Permeability, StateGroup.Soil, "ПОЧВА", 160, "Проницаемость", "Проницаемость", "Предельная скорость поступления поверхностной воды в почву.", "мм/ч", StateKind.Raw, StateScale.Linear, 0, 30, 1, "#779e99", WaterPalette),
        D(SimulationLayer.MineralContent, StateGroup.Soil, "ПОЧВА", 170, "Минеральное богатство", "Минералы", "Геологическая составляющая потенциального плодородия.", "доля", StateKind.Raw, StateScale.Linear, 0, 1, 2, "#ad945b", SoilPalette),

        D(SimulationLayer.SolarRadiation, StateGroup.Atmosphere, "ЭНЕРГИЯ И ВОЗДУХ", 210, "Солнечная радиация", "Солнце", "Энергия, полученная поверхностью после облачной тени и альбедо.", "Вт/м²", StateKind.Raw, StateScale.Linear, 0, 1000, 0, "#edca69", SunPalette),
        D(SimulationLayer.SurfaceTemperature, StateGroup.Atmosphere, "ЭНЕРГИЯ И ВОЗДУХ", 220, "Температура поверхности", "Поверхность", "Температура верхней границы земли, реагирующая на солнце, снег, облака и влагу.", "°C", StateKind.Raw, StateScale.Diverging, -40, 45, 1, "#e07a55", HeatPalette),
        D(SimulationLayer.SoilTemperature, StateGroup.Atmosphere, "ЭНЕРГИЯ И ВОЗДУХ", 230, "Температура почвы", "Почва", "Температура почвенного резервуара с более медленной тепловой памятью.", "°C", StateKind.Raw, StateScale.Diverging, -25, 35, 1, "#cf8e64", HeatPalette),
        D(SimulationLayer.AirTemperature, StateGroup.Atmosphere, "ЭНЕРГИЯ И ВОЗДУХ", 240, "Температура воздуха", "Воздух", "Температура нижнего атмосферного слоя над ячейкой.", "°C", StateKind.Raw, StateScale.Diverging, -40, 40, 1, "#d17b65", HeatPalette),
        D(SimulationLayer.Pressure, StateGroup.Atmosphere, "ЭНЕРГИЯ И ВОЗДУХ", 250, "Давление воздуха", "Давление", "Поле давления, связывающее нагрев и движение воздуха.", "гПа", StateKind.Raw, StateScale.Linear, 930, 1035, 1, "#b18ad0", AirPalette),
        D(SimulationLayer.Wind, StateGroup.Atmosphere, "ЭНЕРГИЯ И ВОЗДУХ", 260, "Скорость и направление ветра", "Ветер", "Векторное поле фонового переноса и реакции на градиенты давления.", "м/с", StateKind.Raw, StateScale.Linear, 0, 20, 1, "#9fc6ac", FractionPalette),
        D(SimulationLayer.Humidity, StateGroup.Atmosphere, "ЭНЕРГИЯ И ВОЗДУХ", 270, "Влага в воздухе", "Влажность", "Водный эквивалент пара в атмосферном резервуаре ячейки.", "мм экв.", StateKind.Raw, StateScale.Linear, 0, 30, 2, "#7fb7b5", WaterPalette),
        D(SimulationLayer.CloudWater, StateGroup.Atmosphere, "ЭНЕРГИЯ И ВОЗДУХ", 280, "Облачная вода", "Облака", "Сконденсированная вода, переносимая атмосферным потоком.", "мм экв.", StateKind.Raw, StateScale.Logarithmic, 0, 10, 2, "#c4cdd2", CloudPalette),
        D(SimulationLayer.Precipitation, StateGroup.Atmosphere, "ЭНЕРГИЯ И ВОЗДУХ", 290, "Интенсивность осадков", "Осадки", "Скорость возврата облачной воды на поверхность дождём или снегом.", "мм/ч", StateKind.Raw, StateScale.Logarithmic, 0, 4, 3, "#69aede", WaterPalette),

        D(SimulationLayer.SurfaceWater, StateGroup.Water, "ВОДА", 310, "Поверхностная вода", "Поверхность", "Вода, способная просочиться, задержаться во впадине или уйти по стоку.", "мм", StateKind.Raw, StateScale.Logarithmic, 0, 260, 1, "#51bfc6", WaterPalette),
        D(SimulationLayer.RootWater, StateGroup.Water, "ВОДА", 320, "Вода корневой зоны", "Корневая зона", "Доступный растениям почвенный резервуар.", "мм", StateKind.Raw, StateScale.Linear, 0, 180, 1, "#5b9ac3", WaterPalette),
        D(SimulationLayer.Groundwater, StateGroup.Water, "ВОДА", 330, "Грунтовая вода", "Грунтовая", "Медленный подземный резервуар, обменивающийся с соседями и поверхностью.", "мм", StateKind.Raw, StateScale.Linear, 0, 225, 1, "#5e77b4", DeepWaterPalette),
        D(SimulationLayer.Snow, StateGroup.Water, "ВОДА", 340, "Снежный запас", "Снег", "Эквивалент воды в снежном покрове.", "мм SWE", StateKind.Raw, StateScale.Logarithmic, 0, 900, 1, "#d8e5df", SnowPalette),
        D(SimulationLayer.FrozenSoil, StateGroup.Water, "ВОДА", 350, "Промерзание почвы", "Промерзание", "Доля промёрзшего почвенного объёма, ограничивающая инфильтрацию.", "доля", StateKind.Raw, StateScale.Linear, 0, 1, 2, "#9bc4c8", SnowPalette),

        D(SimulationLayer.LiveBiomass, StateGroup.Life, "ЖИЗНЬ", 410, "Живая биомасса", "Живая масса", "Результат совместной доступности света, тепла, воды, семян и азота.", "г/м²", StateKind.Raw, StateScale.Linear, 0, 720, 0, "#86b45d", LifePalette),
        D(SimulationLayer.DryBiomass, StateGroup.Life, "ЖИЗНЬ", 420, "Сухая стоящая биомасса", "Сухостой", "Отмершая стоящая трава — память холода, жары и дефицита воды.", "г/м²", StateKind.Raw, StateScale.Linear, 0, 700, 0, "#c69c54", DryPalette),
        D(SimulationLayer.LitterBiomass, StateGroup.Life, "ЖИЗНЬ", 430, "Растительная подстилка", "Подстилка", "Полёгшая органическая масса, постепенно возвращающая вещество почве.", "г/м²", StateKind.Raw, StateScale.Linear, 0, 500, 0, "#a47d4f", DryPalette),
        D(SimulationLayer.SeedBank, StateGroup.Life, "ЖИЗНЬ", 440, "Семенной банк", "Семена", "Нормированный запас жизнеспособного восстановления растительности.", "доля", StateKind.Raw, StateScale.Linear, 0, 1.2f, 2, "#a7b75d", LifePalette),
        D(SimulationLayer.SoilOrganicMatter, StateGroup.Life, "ЖИЗНЬ", 450, "Органическое вещество почвы", "Органика", "Медленный почвенный резервуар углеродсодержащего органического вещества.", "г/м²", StateKind.Raw, StateScale.Linear, 0, 3000, 0, "#75633c", SoilPalette),
        D(SimulationLayer.AvailableNitrogen, StateGroup.Life, "ЖИЗНЬ", 460, "Доступный азот", "Азот", "Минеральный азот, доступный для текущего роста растений.", "г/м²", StateKind.Raw, StateScale.Linear, 0, 20, 2, "#a9b96c", LifePalette),
        D(SimulationLayer.PlantNitrogen, StateGroup.Life, "ЖИЗНЬ", 470, "Азот живых растений", "Азот растений", "Азот, связанный в живой растительной массе и возвращаемый в органический пул при отмирании.", "г/м²", StateKind.Raw, StateScale.Linear, 0, 15, 2, "#8fa956", LifePalette),
        D(SimulationLayer.OrganicNitrogen, StateGroup.Life, "ЖИЗНЬ", 480, "Органический азот", "Органический N", "Азот сухостоя, подстилки и почвенной органики, доступный медленной минерализации.", "г/м²", StateKind.Raw, StateScale.Linear, 0, 40, 2, "#756f45", SoilPalette),

        D(SimulationLayer.LooseSediment, StateGroup.Material, "ПЕРЕНОСИМОЕ ВЕЩЕСТВО", 510, "Рыхлый осадочный материал", "Рыхлый осадок", "Материал поверхности, доступный водной и ветровой эрозии.", "кг/м²", StateKind.Raw, StateScale.Linear, 0, 10, 2, "#b08a66", MatterPalette),
        D(SimulationLayer.SurfaceCrust, StateGroup.Material, "ПЕРЕНОСИМОЕ ВЕЩЕСТВО", 520, "Поверхностная корка", "Корка", "Доля поверхности, защищённая или запечатанная почвенной коркой.", "доля", StateKind.Raw, StateScale.Linear, 0, 1, 2, "#997961", MatterPalette),
        D(SimulationLayer.Dust, StateGroup.Material, "ПЕРЕНОСИМОЕ ВЕЩЕСТВО", 530, "Пыль в воздухе", "Пыль", "Поднятый ветром мелкий материал над ячейкой.", "г/м²", StateKind.Raw, StateScale.Logarithmic, 0, 2, 3, "#b98b69", MatterPalette)
    ];

    private static readonly IReadOnlyDictionary<SimulationLayer, StateDescriptor> ById =
        Descriptors.ToDictionary(descriptor => descriptor.Id);

    public static IReadOnlyList<StateDescriptor> All { get; } = Array.AsReadOnly(Descriptors);

    public static StateDescriptor Get(SimulationLayer id) =>
        ById.TryGetValue(id, out var descriptor)
            ? descriptor
            : throw new ArgumentOutOfRangeException(nameof(id), id, "The state is not registered in the catalog.");

    private static StateDescriptor D(
        SimulationLayer id,
        StateGroup group,
        string groupTitle,
        int order,
        string title,
        string shortTitle,
        string description,
        string unit,
        StateKind kind,
        StateScale scale,
        float minimum,
        float maximum,
        int precision,
        string swatch,
        string[] palette) =>
        new(id, group, groupTitle, order, title, shortTitle, description, unit, kind, scale, minimum, maximum, precision, swatch, palette);
}
