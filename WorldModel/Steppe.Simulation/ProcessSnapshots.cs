namespace Steppe.Simulation;

public enum VectorProcess
{
    Wind,
    DrainageDirection,
    AirTemperatureAdvection,
    HumidityAdvection,
    CloudAdvection,
    SurfaceRunoff,
    GroundwaterFlow,
    SnowTransport,
    SedimentTransport,
    DustAdvection,
    GiantHarvesterMovement,
    FireSpread
}

public enum VectorProcessMeasure
{
    Instantaneous,
    Direction,
    AccumulatedTransfer,
    TransportMoment
}

public sealed record VectorProcessDescriptor(
    VectorProcess Process,
    string Title,
    string Description,
    string Unit,
    VectorProcessMeasure Measure,
    SimulationLayer CarrierLayer);

public static class VectorProcessCatalog
{
    private static readonly VectorProcessDescriptor[] Descriptors =
    [
        new(VectorProcess.Wind, "Ветер", "Мгновенная скорость переноса воздуха.", "м/с", VectorProcessMeasure.Instantaneous, SimulationLayer.Wind),
        new(VectorProcess.DrainageDirection, "Направление сухого дренажа", "Статический единичный вектор спуска по сухому рельефу; заполненные впадины переливаются по текущей гидравлической поверхности.", "направление", VectorProcessMeasure.Direction, SimulationLayer.Drainage),
        new(VectorProcess.AirTemperatureAdvection, "Адвекция температуры", "Кинематический момент переноса температуры ветром за период.", "°C·ячейка", VectorProcessMeasure.TransportMoment, SimulationLayer.AirTemperature),
        new(VectorProcess.HumidityAdvection, "Адвекция влажности", "Кинематический момент переноса атмосферной влаги ветром за период.", "мм·ячейка", VectorProcessMeasure.TransportMoment, SimulationLayer.Humidity),
        new(VectorProcess.CloudAdvection, "Адвекция облаков", "Кинематический момент переноса облачной воды ветром за период.", "мм·ячейка", VectorProcessMeasure.TransportMoment, SimulationLayer.CloudWater),
        new(VectorProcess.SurfaceRunoff, "Поверхностный сток", "Фактический объём воды, отправленный из ячейки вниз по дренажу за период.", "мм", VectorProcessMeasure.AccumulatedTransfer, SimulationLayer.SurfaceWater),
        new(VectorProcess.GroundwaterFlow, "Подземный поток", "Фактический обмен грунтовой воды между соседними ячейками за период.", "мм", VectorProcessMeasure.AccumulatedTransfer, SimulationLayer.Groundwater),
        new(VectorProcess.SnowTransport, "Ветровой перенос снега", "Фактический снежный запас, перемещённый в соседнюю ячейку за период.", "мм SWE", VectorProcessMeasure.AccumulatedTransfer, SimulationLayer.Snow),
        new(VectorProcess.SedimentTransport, "Перенос осадка", "Рыхлый материал, отправленный вниз по поверхностному стоку за период.", "кг/м²", VectorProcessMeasure.AccumulatedTransfer, SimulationLayer.LooseSediment),
        new(VectorProcess.DustAdvection, "Адвекция пыли", "Кинематический момент переноса атмосферной пыли ветром за период.", "г/м²·ячейка", VectorProcessMeasure.TransportMoment, SimulationLayer.Dust),
        new(VectorProcess.GiantHarvesterMovement, "Миграция гигантских сенокосцев", "Фактическое направление и длина перемещения биологических фронтов за период.", "ячейка", VectorProcessMeasure.AccumulatedTransfer, SimulationLayer.LiveBiomass),
        new(VectorProcess.FireSpread, "Распространение огня", "Принятое соседними ячейками направление распространения фронта огня; ветер усиливает перенос по своей оси.", "индекс огня", VectorProcessMeasure.AccumulatedTransfer, SimulationLayer.FireIntensity)
    ];

    private static readonly IReadOnlyDictionary<VectorProcess, VectorProcessDescriptor> ByProcess =
        Descriptors.ToDictionary(descriptor => descriptor.Process);

    public static IReadOnlyList<VectorProcessDescriptor> All { get; } = Array.AsReadOnly(Descriptors);

    public static VectorProcessDescriptor Get(VectorProcess process) =>
        ByProcess.TryGetValue(process, out var descriptor)
            ? descriptor
            : throw new ArgumentOutOfRangeException(nameof(process), process, null);
}

public sealed record FluxSnapshot(
    SimulationFlux Flux,
    int Width,
    int Height,
    double PeriodHours,
    string Unit,
    float[] Values,
    double SignedTotal,
    double AbsoluteTotal,
    double PositiveTotal,
    double NegativeTotal,
    int ActiveCellCount,
    int NonFiniteCount);

public sealed record VectorProcessSnapshot(
    VectorProcess Process,
    int Width,
    int Height,
    double PeriodHours,
    string Unit,
    VectorProcessMeasure Measure,
    float[] VectorX,
    float[] VectorY,
    float[] Magnitude,
    float[] GrossMagnitude,
    double TotalMagnitude,
    double TotalGrossMagnitude,
    float DirectionalCoherence,
    float MeanMagnitude,
    float MaximumMagnitude,
    int ActiveCellCount,
    int NonFiniteCount);
