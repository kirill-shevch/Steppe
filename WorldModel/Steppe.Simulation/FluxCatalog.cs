namespace Steppe.Simulation;

public enum SimulationFlux
{
    HumidityTransport,
    CloudTransport,
    AirTemperatureTransport,
    Condensation,
    CloudEvaporation,
    Rainfall,
    Snowfall,
    SnowMelt,
    SnowSublimation,
    SnowTransport,
    SnowExport,
    Infiltration,
    Percolation,
    RunoffIn,
    RunoffOut,
    GroundwaterTransport,
    GroundwaterDischarge,
    SurfaceEvaporation,
    Transpiration,
    PlantGrowth,
    PlantMortality,
    PlantNitrogenSenescence,
    DryBiomassLodging,
    LitterDecomposition,
    SoilOrganicMatterRespiration,
    NitrogenUptake,
    NitrogenMineralization,
    SeedBankChange,
    WaterErosion,
    SedimentDeposition,
    SedimentExport,
    SoilDepthChange,
    DustLift,
    DustDeposition,
    DustTransport,
    SurfaceCrustChange,
    ExternalSurfaceWater,
    SoilCompactionRecovery,
    GiantHarvesterLiveGrazing,
    GiantHarvesterDryGrazing,
    GiantHarvesterTramplingLitter,
    GiantHarvesterPlantNitrogenReturn,
    GiantHarvesterManure,
    GiantHarvesterDrinking,
    GiantHarvesterSeedDispersal,
    GiantHarvesterCrustBreakdown,
    GiantHarvesterCompaction,
    FireActivityChange,
    FireLiveCombustion,
    FireDryCombustion,
    FireLitterCombustion,
    FirePlantNitrogenRelease,
    FireOrganicNitrogenRelease,
    FireSeedBankDamage,
    BurnScarChange,
    ExternalIgnition,
    CaravanSurfaceWaterWithdrawal,
    CaravanSurfaceWaterReturn,
    CaravanSnowWithdrawal,
    CaravanLiveHarvest,
    CaravanDryHarvest,
    CaravanPlantNitrogenWithdrawal,
    CaravanOrganicNitrogenWithdrawal,
    CaravanOrganicMatterReturn,
    CaravanOrganicNitrogenReturn,
    CaravanDustCapture,
    CaravanSedimentReturn,
    CaravanCompaction,
    CaravanAtmosphericWaterWithdrawal
}

public enum FluxGroup
{
    Atmosphere,
    Snow,
    Hydrology,
    Biology,
    Material,
    Intervention
}

public sealed record FluxEffect(
    SimulationLayer State,
    float Factor);

public sealed record FluxDescriptor(
    SimulationFlux Id,
    FluxGroup Group,
    string GroupTitle,
    int Order,
    string Title,
    string ShortTitle,
    string Description,
    string Unit,
    int Precision,
    bool Signed,
    FluxEffect[] Effects);

/// <summary>
/// Semantic contract for process amounts accumulated during the latest
/// FiniteWorld.AdvanceHours call. Effects convert a positive flux amount into
/// the signed contribution to each affected state reservoir.
/// </summary>
public static class FluxCatalog
{
    private static readonly FluxDescriptor[] Descriptors =
    [
        D(SimulationFlux.HumidityTransport, FluxGroup.Atmosphere, "АТМОСФЕРА", 10, "Перенос атмосферной влаги", "Перенос влаги", "Чистое изменение водяного пара из-за адвекции и открытой границы.", "мм", 3, true, E(SimulationLayer.Humidity, 1)),
        D(SimulationFlux.CloudTransport, FluxGroup.Atmosphere, "АТМОСФЕРА", 20, "Перенос облачной воды", "Перенос облаков", "Чистое изменение облачного резервуара из-за атмосферного переноса.", "мм", 3, true, E(SimulationLayer.CloudWater, 1)),
        D(SimulationFlux.AirTemperatureTransport, FluxGroup.Atmosphere, "АТМОСФЕРА", 30, "Перенос температуры воздуха", "Перенос тепла", "Изменение температуры воздуха на стадии атмосферной адвекции.", "°C", 3, true, E(SimulationLayer.AirTemperature, 1)),
        D(SimulationFlux.Condensation, FluxGroup.Atmosphere, "АТМОСФЕРА", 40, "Конденсация", "Конденсация", "Переход водяного пара в облачную воду.", "мм", 3, false, E(SimulationLayer.Humidity, -1), E(SimulationLayer.CloudWater, 1)),
        D(SimulationFlux.CloudEvaporation, FluxGroup.Atmosphere, "АТМОСФЕРА", 50, "Испарение облака", "Испарение облака", "Возврат облачной воды в атмосферный пар.", "мм", 3, false, E(SimulationLayer.CloudWater, -1), E(SimulationLayer.Humidity, 1)),
        D(SimulationFlux.Rainfall, FluxGroup.Atmosphere, "АТМОСФЕРА", 60, "Дождь", "Дождь", "Жидкие осадки из облачного резервуара на поверхность.", "мм", 3, false, E(SimulationLayer.CloudWater, -1), E(SimulationLayer.SurfaceWater, 1)),
        D(SimulationFlux.Snowfall, FluxGroup.Snow, "СНЕГ", 110, "Снегопад", "Снегопад", "Твёрдые осадки из облачного резервуара в снежный запас.", "мм", 3, false, E(SimulationLayer.CloudWater, -1), E(SimulationLayer.Snow, 1)),
        D(SimulationFlux.SnowMelt, FluxGroup.Snow, "СНЕГ", 120, "Таяние снега", "Таяние", "Переход снежного запаса в поверхностную воду.", "мм", 3, false, E(SimulationLayer.Snow, -1), E(SimulationLayer.SurfaceWater, 1)),
        D(SimulationFlux.SnowSublimation, FluxGroup.Snow, "СНЕГ", 130, "Сублимация снега", "Сублимация", "Переход снега непосредственно в атмосферную влагу.", "мм", 3, false, E(SimulationLayer.Snow, -1), E(SimulationLayer.Humidity, 1)),
        D(SimulationFlux.SnowTransport, FluxGroup.Snow, "СНЕГ", 140, "Ветровой перенос снега", "Перенос снега", "Чистый приход или уход снега из-за ветрового перераспределения.", "мм", 3, true, E(SimulationLayer.Snow, 1)),
        D(SimulationFlux.SnowExport, FluxGroup.Snow, "СНЕГ", 150, "Вынос снега за границу", "Экспорт снега", "Снег, вынесенный ветром через открытую границу конечного мира.", "мм", 3, false, E(SimulationLayer.Snow, -1)),
        D(SimulationFlux.Infiltration, FluxGroup.Hydrology, "ГИДРОЛОГИЯ", 210, "Инфильтрация", "Инфильтрация", "Переход поверхностной воды в корневую зону.", "мм", 3, false, E(SimulationLayer.SurfaceWater, -1), E(SimulationLayer.RootWater, 1)),
        D(SimulationFlux.Percolation, FluxGroup.Hydrology, "ГИДРОЛОГИЯ", 220, "Перколяция", "Перколяция", "Переход воды корневой зоны в грунтовый резервуар.", "мм", 3, false, E(SimulationLayer.RootWater, -1), E(SimulationLayer.Groundwater, 1)),
        D(SimulationFlux.RunoffIn, FluxGroup.Hydrology, "ГИДРОЛОГИЯ", 230, "Приход поверхностного стока", "Сток вошёл", "Поверхностная вода, пришедшая из соседних ячеек.", "мм", 3, false, E(SimulationLayer.SurfaceWater, 1)),
        D(SimulationFlux.RunoffOut, FluxGroup.Hydrology, "ГИДРОЛОГИЯ", 240, "Уход поверхностного стока", "Сток вышел", "Поверхностная вода, ушедшая в следующую ячейку или за границу мира.", "мм", 3, false, E(SimulationLayer.SurfaceWater, -1)),
        D(SimulationFlux.GroundwaterTransport, FluxGroup.Hydrology, "ГИДРОЛОГИЯ", 250, "Подземный обмен", "Подземный обмен", "Чистое изменение грунтовой воды из-за обмена с соседями.", "мм", 3, true, E(SimulationLayer.Groundwater, 1)),
        D(SimulationFlux.GroundwaterDischarge, FluxGroup.Hydrology, "ГИДРОЛОГИЯ", 260, "Разгрузка грунтовых вод", "Разгрузка", "Выход избытка грунтовой воды на поверхность.", "мм", 3, false, E(SimulationLayer.Groundwater, -1), E(SimulationLayer.SurfaceWater, 1)),
        D(SimulationFlux.SurfaceEvaporation, FluxGroup.Hydrology, "ГИДРОЛОГИЯ", 270, "Испарение с поверхности", "Испарение", "Переход поверхностной воды в атмосферную влагу.", "мм", 3, false, E(SimulationLayer.SurfaceWater, -1), E(SimulationLayer.Humidity, 1)),
        D(SimulationFlux.Transpiration, FluxGroup.Hydrology, "ГИДРОЛОГИЯ", 280, "Транспирация", "Транспирация", "Переход воды корневой зоны в атмосферу через растения.", "мм", 3, false, E(SimulationLayer.RootWater, -1), E(SimulationLayer.Humidity, 1)),
        D(SimulationFlux.PlantGrowth, FluxGroup.Biology, "БИОЛОГИЯ", 310, "Рост растений", "Рост", "Прирост живой растительной массы.", "г/м²", 3, false, E(SimulationLayer.LiveBiomass, 1)),
        D(SimulationFlux.PlantMortality, FluxGroup.Biology, "БИОЛОГИЯ", 320, "Отмирание растений", "Отмирание", "Переход живой биомассы в сухую стоящую массу.", "г/м²", 3, false, E(SimulationLayer.LiveBiomass, -1), E(SimulationLayer.DryBiomass, 1)),
        D(SimulationFlux.PlantNitrogenSenescence, FluxGroup.Biology, "БИОЛОГИЯ", 325, "Возврат азота при отмирании", "Возврат N", "Переход азота отмершей живой массы в органический резервуар.", "г/м²", 4, false, E(SimulationLayer.PlantNitrogen, -1), E(SimulationLayer.OrganicNitrogen, 1)),
        D(SimulationFlux.DryBiomassLodging, FluxGroup.Biology, "БИОЛОГИЯ", 330, "Полегание сухостоя", "Полегание", "Переход сухой стоящей массы в подстилку.", "г/м²", 3, false, E(SimulationLayer.DryBiomass, -1), E(SimulationLayer.LitterBiomass, 1)),
        D(SimulationFlux.LitterDecomposition, FluxGroup.Biology, "БИОЛОГИЯ", 340, "Разложение подстилки", "Разложение", "Разложение подстилки и поступление её устойчивой части в почву.", "г/м²", 3, false, E(SimulationLayer.LitterBiomass, -1), E(SimulationLayer.SoilOrganicMatter, 0.35f)),
        D(SimulationFlux.SoilOrganicMatterRespiration, FluxGroup.Biology, "БИОЛОГИЯ", 345, "Дыхание почвенной органики", "Дыхание SOM", "Медленная потеря органического вещества почвы при микробном дыхании.", "г/м²", 3, false, E(SimulationLayer.SoilOrganicMatter, -1)),
        D(SimulationFlux.NitrogenUptake, FluxGroup.Biology, "БИОЛОГИЯ", 350, "Поглощение азота", "Поглощение N", "Переход минерального азота в живую растительную массу.", "г/м²", 4, false, E(SimulationLayer.AvailableNitrogen, -1), E(SimulationLayer.PlantNitrogen, 1)),
        D(SimulationFlux.NitrogenMineralization, FluxGroup.Biology, "БИОЛОГИЯ", 360, "Минерализация азота", "Минерализация N", "Переход органического азота в доступный минеральный резервуар.", "г/м²", 4, false, E(SimulationLayer.OrganicNitrogen, -1), E(SimulationLayer.AvailableNitrogen, 1)),
        D(SimulationFlux.SeedBankChange, FluxGroup.Biology, "БИОЛОГИЯ", 370, "Изменение семенного банка", "Семенной банк", "Чистое пополнение или истощение способности растительности к восстановлению.", "доля", 5, true, E(SimulationLayer.SeedBank, 1)),
        D(SimulationFlux.WaterErosion, FluxGroup.Material, "ПЕРЕНОС ВЕЩЕСТВА", 410, "Водная эрозия", "Эрозия", "Рыхлый материал, поднятый поверхностным потоком и отправленный вниз по стоку.", "кг/м²", 4, false, E(SimulationLayer.LooseSediment, -1)),
        D(SimulationFlux.SedimentDeposition, FluxGroup.Material, "ПЕРЕНОС ВЕЩЕСТВА", 420, "Приход осадка", "Осаждение", "Рыхлый материал, принесённый водным потоком из соседней ячейки.", "кг/м²", 4, false, E(SimulationLayer.LooseSediment, 1)),
        D(SimulationFlux.SedimentExport, FluxGroup.Material, "ПЕРЕНОС ВЕЩЕСТВА", 425, "Вынос осадка за границу", "Экспорт осадка", "Рыхлый материал, покинувший конечный мир вместе с поверхностным стоком.", "кг/м²", 4, false, E(SimulationLayer.LooseSediment, -1)),
        D(SimulationFlux.SoilDepthChange, FluxGroup.Material, "ПЕРЕНОС ВЕЩЕСТВА", 430, "Изменение глубины почвы", "Глубина почвы", "Чистое изменение мощности почвы из-за эрозии и осаждения.", "м", 6, true, E(SimulationLayer.SoilDepth, 1)),
        D(SimulationFlux.DustLift, FluxGroup.Material, "ПЕРЕНОС ВЕЩЕСТВА", 440, "Подъём пыли", "Подъём пыли", "Переход рыхлого поверхностного материала в атмосферную пыль.", "г/м²", 4, false, E(SimulationLayer.LooseSediment, -0.001f), E(SimulationLayer.Dust, 1)),
        D(SimulationFlux.DustDeposition, FluxGroup.Material, "ПЕРЕНОС ВЕЩЕСТВА", 450, "Осаждение пыли", "Осаждение пыли", "Возврат атмосферной пыли в рыхлый поверхностный материал.", "г/м²", 4, false, E(SimulationLayer.Dust, -1), E(SimulationLayer.LooseSediment, 0.001f)),
        D(SimulationFlux.DustTransport, FluxGroup.Material, "ПЕРЕНОС ВЕЩЕСТВА", 460, "Перенос пыли", "Перенос пыли", "Чистый приход или уход атмосферной пыли с ветром.", "г/м²", 4, true, E(SimulationLayer.Dust, 1)),
        D(SimulationFlux.SurfaceCrustChange, FluxGroup.Material, "ПЕРЕНОС ВЕЩЕСТВА", 470, "Изменение поверхностной корки", "Изменение корки", "Чистое формирование или разрушение почвенной корки.", "доля", 6, true, E(SimulationLayer.SurfaceCrust, 1)),
        D(SimulationFlux.ExternalSurfaceWater, FluxGroup.Intervention, "ВНЕШНЕЕ ВОЗДЕЙСТВИЕ", 510, "Добавленная поверхностная вода", "Добавленная вода", "Вода, явно добавленная через внешний интерфейс симуляции.", "мм", 3, false, E(SimulationLayer.SurfaceWater, 1)),
        D(SimulationFlux.SoilCompactionRecovery, FluxGroup.Material, "ПЕРЕНОС ВЕЩЕСТВА", 480, "Разрыхление уплотнённой почвы", "Разрыхление", "Снятие уплотнения корнями, увлажнением и циклами промерзания.", "доля", 6, false, E(SimulationLayer.SoilCompaction, -1)),
        D(SimulationFlux.GiantHarvesterLiveGrazing, FluxGroup.Biology, "БИОЛОГИЯ", 380, "Выпас живой травы сенокосцами", "Выпас зелени", "Живая масса, снятая движущимся биологическим фронтом.", "г/м²", 3, false, E(SimulationLayer.LiveBiomass, -1)),
        D(SimulationFlux.GiantHarvesterDryGrazing, FluxGroup.Biology, "БИОЛОГИЯ", 381, "Выпас сухостоя сенокосцами", "Выпас сухостоя", "Сухая стоящая масса, снятая сенокосцами.", "г/м²", 3, false, E(SimulationLayer.DryBiomass, -1)),
        D(SimulationFlux.GiantHarvesterTramplingLitter, FluxGroup.Biology, "БИОЛОГИЯ", 382, "Полегание в следе сенокосцев", "След стада", "Растительная масса, примятая в подстилку тяжёлыми телами.", "г/м²", 3, false, E(SimulationLayer.LitterBiomass, 1)),
        D(SimulationFlux.GiantHarvesterPlantNitrogenReturn, FluxGroup.Biology, "БИОЛОГИЯ", 383, "Возврат азота сенокосцами", "Возврат N стадом", "Азот съеденной растительности, возвращённый в органический резервуар следа.", "г/м²", 4, false, E(SimulationLayer.PlantNitrogen, -1), E(SimulationLayer.OrganicNitrogen, 1)),
        D(SimulationFlux.GiantHarvesterManure, FluxGroup.Biology, "БИОЛОГИЯ", 384, "Органика сенокосцев", "Органика стада", "Часть съеденной массы, возвращённая в медленный почвенный резервуар.", "г/м²", 3, false, E(SimulationLayer.SoilOrganicMatter, 1)),
        D(SimulationFlux.GiantHarvesterDrinking, FluxGroup.Biology, "БИОЛОГИЯ", 385, "Водопой сенокосцев", "Водопой", "Поверхностная вода, поглощённая животными и учтённая как выход из резервуаров ландшафта.", "мм", 4, false, E(SimulationLayer.SurfaceWater, -1)),
        D(SimulationFlux.GiantHarvesterSeedDispersal, FluxGroup.Biology, "БИОЛОГИЯ", 386, "Расселение семян сенокосцами", "Семена в следе", "Пополнение банка семян вдоль миграционной трассы.", "доля", 6, false, E(SimulationLayer.SeedBank, 1)),
        D(SimulationFlux.GiantHarvesterCrustBreakdown, FluxGroup.Biology, "БИОЛОГИЯ", 387, "Разрушение корки сенокосцами", "Разбитая корка", "Механическое разрушение поверхностной корки в свежем следе.", "доля", 6, false, E(SimulationLayer.SurfaceCrust, -1)),
        D(SimulationFlux.GiantHarvesterCompaction, FluxGroup.Biology, "БИОЛОГИЯ", 388, "Уплотнение следа сенокосцев", "Уплотнение стадом", "Долговременное уплотнение почвы тяжёлыми животными.", "доля", 6, false, E(SimulationLayer.SoilCompaction, 1)),

        D(SimulationFlux.FireActivityChange, FluxGroup.Biology, "БИОЛОГИЯ", 390, "Изменение активности огня", "Динамика огня", "Чистое изменение интенсивности из-за естественного возгорания, распространения, выгорания топлива и тушения влагой.", "доля", 6, true, E(SimulationLayer.FireIntensity, 1)),
        D(SimulationFlux.FireLiveCombustion, FluxGroup.Biology, "БИОЛОГИЯ", 391, "Сгорание живой растительности", "Сгорела зелень", "Живая растительная масса, уничтоженная активным огнём.", "г/м²", 3, false, E(SimulationLayer.LiveBiomass, -1)),
        D(SimulationFlux.FireDryCombustion, FluxGroup.Biology, "БИОЛОГИЯ", 392, "Сгорание сухостоя", "Сгорел сухостой", "Сухая стоящая растительная масса, использованная пожаром как быстрое топливо.", "г/м²", 3, false, E(SimulationLayer.DryBiomass, -1)),
        D(SimulationFlux.FireLitterCombustion, FluxGroup.Biology, "БИОЛОГИЯ", 393, "Сгорание подстилки", "Сгорела подстилка", "Растительная подстилка, уничтоженная фронтом огня.", "г/м²", 3, false, E(SimulationLayer.LitterBiomass, -1)),
        D(SimulationFlux.FirePlantNitrogenRelease, FluxGroup.Biology, "БИОЛОГИЯ", 394, "Высвобождение азота растений огнём", "Азот из зелени", "Азот сгоревшей живой массы возвращается в доступный и органический почвенные резервуары.", "г/м²", 5, false, E(SimulationLayer.PlantNitrogen, -1), E(SimulationLayer.AvailableNitrogen, 0.7f), E(SimulationLayer.OrganicNitrogen, 0.3f)),
        D(SimulationFlux.FireOrganicNitrogenRelease, FluxGroup.Biology, "БИОЛОГИЯ", 395, "Минерализация органического азота огнём", "Азот из золы", "Часть органического азота сухостоя и подстилки быстро переходит в доступную минеральную форму.", "г/м²", 5, false, E(SimulationLayer.OrganicNitrogen, -1), E(SimulationLayer.AvailableNitrogen, 1)),
        D(SimulationFlux.FireSeedBankDamage, FluxGroup.Biology, "БИОЛОГИЯ", 396, "Повреждение семенного банка огнём", "Потеря семян", "Доля жизнеспособного семенного банка, потерянная при сильном нагреве поверхности.", "доля", 6, false, E(SimulationLayer.SeedBank, -1)),
        D(SimulationFlux.BurnScarChange, FluxGroup.Biology, "БИОЛОГИЯ", 397, "Изменение выгоревшего следа", "Динамика гари", "Формирование гари активным пожаром и её исчезновение при восстановлении растительности.", "доля", 6, true, E(SimulationLayer.BurnScar, 1)),
        D(SimulationFlux.ExternalIgnition, FluxGroup.Intervention, "ВНЕШНЕЕ ВОЗДЕЙСТВИЕ", 520, "Внешнее возгорание", "Поджог", "Огонь, явно добавленный через внешний интерфейс симуляции.", "доля", 6, false, E(SimulationLayer.FireIntensity, 1)),
        D(SimulationFlux.CaravanSurfaceWaterWithdrawal, FluxGroup.Intervention, "КАРАВАН", 610, "Забор поверхностной воды караваном", "Забор воды", "Вода, физически изъятая караваном из доступного поверхностного резервуара.", "мм", 4, false, E(SimulationLayer.SurfaceWater, -1)),
        D(SimulationFlux.CaravanSurfaceWaterReturn, FluxGroup.Intervention, "КАРАВАН", 620, "Возврат воды караваном", "Возврат воды", "Запасённая караваном вода, возвращённая на поверхность клетки.", "мм", 4, false, E(SimulationLayer.SurfaceWater, 1)),
        D(SimulationFlux.CaravanSnowWithdrawal, FluxGroup.Intervention, "КАРАВАН", 630, "Сбор и плавление снега", "Сбор снега", "Снежный водный эквивалент, изъятый караваном для плавления.", "мм SWE", 4, false, E(SimulationLayer.Snow, -1)),
        D(SimulationFlux.CaravanLiveHarvest, FluxGroup.Intervention, "КАРАВАН", 640, "Сбор живой биомассы", "Сбор зелени", "Живая растительная масса, физически снятая караваном.", "г/м²", 4, false, E(SimulationLayer.LiveBiomass, -1)),
        D(SimulationFlux.CaravanDryHarvest, FluxGroup.Intervention, "КАРАВАН", 650, "Сбор сухостоя", "Сбор сухостоя", "Сухая стоящая масса, физически снятая караваном.", "г/м²", 4, false, E(SimulationLayer.DryBiomass, -1)),
        D(SimulationFlux.CaravanPlantNitrogenWithdrawal, FluxGroup.Intervention, "КАРАВАН", 660, "Вынос растительного азота", "Вынос N растений", "Азот, вынесенный вместе с собранной живой биомассой.", "г/м²", 5, false, E(SimulationLayer.PlantNitrogen, -1)),
        D(SimulationFlux.CaravanOrganicNitrogenWithdrawal, FluxGroup.Intervention, "КАРАВАН", 670, "Вынос органического азота", "Вынос органического N", "Доля органического азота, вынесенная вместе с собранным сухостоем.", "г/м²", 5, false, E(SimulationLayer.OrganicNitrogen, -1)),
        D(SimulationFlux.CaravanOrganicMatterReturn, FluxGroup.Intervention, "КАРАВАН", 680, "Возврат органических остатков", "Возврат органики", "Органические остатки караванного цикла, возвращённые в растительную подстилку.", "г/м²", 4, false, E(SimulationLayer.LitterBiomass, 1)),
        D(SimulationFlux.CaravanOrganicNitrogenReturn, FluxGroup.Intervention, "КАРАВАН", 690, "Возврат органического азота", "Возврат N", "Азот органических остатков каравана, возвращённый в почвенный цикл.", "г/м²", 5, false, E(SimulationLayer.OrganicNitrogen, 1)),
        D(SimulationFlux.CaravanDustCapture, FluxGroup.Intervention, "КАРАВАН", 700, "Улавливание пыли караваном", "Уловленная пыль", "Пыль, задержанная фильтрами и поверхностями каравана до последующего возврата осадка.", "г/м²", 5, false, E(SimulationLayer.Dust, -1)),
        D(SimulationFlux.CaravanSedimentReturn, FluxGroup.Intervention, "КАРАВАН", 710, "Возврат минерального осадка", "Возврат осадка", "Уловленный минеральный материал, сброшенный караваном в рыхлый поверхностный резервуар.", "кг/м²", 6, false, E(SimulationLayer.LooseSediment, 1)),
        D(SimulationFlux.CaravanCompaction, FluxGroup.Intervention, "КАРАВАН", 720, "Уплотнение колеи каравана", "Колея каравана", "Долговременное уплотнение почвы на пройденном караваном пути.", "доля", 6, false, E(SimulationLayer.SoilCompaction, 1)),
        D(SimulationFlux.CaravanAtmosphericWaterWithdrawal, FluxGroup.Intervention, "КАРАВАН", 730, "Конденсация атмосферной влаги караваном", "Конденсация воды", "Водяной пар, физически изъятый из воздуха конденсатором каравана с затратой сухой биомассы.", "мм", 5, false, E(SimulationLayer.Humidity, -1))
    ];

    private static readonly IReadOnlyDictionary<SimulationFlux, FluxDescriptor> ById =
        Descriptors.ToDictionary(descriptor => descriptor.Id);

    public static IReadOnlyList<FluxDescriptor> All { get; } = Array.AsReadOnly(Descriptors);

    public static FluxDescriptor Get(SimulationFlux id) =>
        ById.TryGetValue(id, out var descriptor)
            ? descriptor
            : throw new ArgumentOutOfRangeException(nameof(id), id, "The flux is not registered in the catalog.");

    public static IEnumerable<(FluxDescriptor Descriptor, FluxEffect Effect)> Affecting(SimulationLayer state)
    {
        foreach (var descriptor in Descriptors)
        {
            foreach (var effect in descriptor.Effects)
            {
                if (effect.State == state)
                {
                    yield return (descriptor, effect);
                }
            }
        }
    }

    private static FluxEffect E(SimulationLayer state, float factor) => new(state, factor);

    private static FluxDescriptor D(
        SimulationFlux id,
        FluxGroup group,
        string groupTitle,
        int order,
        string title,
        string shortTitle,
        string description,
        string unit,
        int precision,
        bool signed,
        params FluxEffect[] effects) =>
        new(id, group, groupTitle, order, title, shortTitle, description, unit, precision, signed, effects);
}
