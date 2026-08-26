namespace Steppe.Simulation;

/// <summary>
/// Diagnostic, non-persistent process amounts and state deltas accumulated over
/// the latest public AdvanceHours call.
/// </summary>
internal sealed class WorldFluxState
{
    private readonly float[][] fluxValues;
    private readonly float[][] vectorX;
    private readonly float[][] vectorY;
    private readonly float[][] vectorGross;
    private readonly float[][] stateBefore;
    private readonly float[][] stateDeltas;

    public WorldFluxState(int cellCount)
    {
        fluxValues = NewFields(RuntimeCompatibility.GetEnumValues<SimulationFlux>().Length, cellCount);
        vectorX = NewFields(RuntimeCompatibility.GetEnumValues<VectorProcess>().Length, cellCount);
        vectorY = NewFields(RuntimeCompatibility.GetEnumValues<VectorProcess>().Length, cellCount);
        vectorGross = NewFields(RuntimeCompatibility.GetEnumValues<VectorProcess>().Length, cellCount);
        stateBefore = NewFields(RuntimeCompatibility.GetEnumValues<SimulationLayer>().Length, cellCount);
        stateDeltas = NewFields(RuntimeCompatibility.GetEnumValues<SimulationLayer>().Length, cellCount);
    }

    public double PeriodHours { get; private set; }

    public void Begin(Func<SimulationLayer, int, float> sample)
    {
        PeriodHours = 0;
        foreach (var field in fluxValues)
        {
            RuntimeCompatibility.Clear(field);
        }

        foreach (var field in vectorX)
        {
            RuntimeCompatibility.Clear(field);
        }

        foreach (var field in vectorY)
        {
            RuntimeCompatibility.Clear(field);
        }

        foreach (var field in vectorGross)
        {
            RuntimeCompatibility.Clear(field);
        }

        foreach (var layer in RuntimeCompatibility.GetEnumValues<SimulationLayer>())
        {
            var field = stateBefore[(int)layer];
            for (var index = 0; index < field.Length; index++)
            {
                field[index] = sample(layer, index);
            }
        }
    }

    public void Complete(double periodHours, Func<SimulationLayer, int, float> sample)
    {
        PeriodHours = periodHours;
        foreach (var layer in RuntimeCompatibility.GetEnumValues<SimulationLayer>())
        {
            var before = stateBefore[(int)layer];
            var delta = stateDeltas[(int)layer];
            for (var index = 0; index < delta.Length; index++)
            {
                delta[index] = sample(layer, index) - before[index];
            }
        }
    }

    public void Add(SimulationFlux flux, int index, float amount)
    {
        fluxValues[(int)flux][index] += amount;
    }

    public void AddVector(VectorProcess process, int index, float x, float y)
    {
        vectorX[(int)process][index] += x;
        vectorY[(int)process][index] += y;
        vectorGross[(int)process][index] += MathF.Sqrt(x * x + y * y);
    }

    public float Value(SimulationFlux flux, int index) => fluxValues[(int)flux][index];

    public float[] CopyFlux(SimulationFlux flux) => (float[])fluxValues[(int)flux].Clone();

    public float Delta(SimulationLayer layer, int index) => stateDeltas[(int)layer][index];

    public (float[] X, float[] Y) CopyVector(VectorProcess process) =>
        ((float[])vectorX[(int)process].Clone(), (float[])vectorY[(int)process].Clone());

    public float[] CopyVectorGross(VectorProcess process) => (float[])vectorGross[(int)process].Clone();

    public (float X, float Y, float Gross) VectorValue(VectorProcess process, int index) =>
        (vectorX[(int)process][index], vectorY[(int)process][index], vectorGross[(int)process][index]);

    private static float[][] NewFields(int fieldCount, int cellCount)
    {
        var fields = new float[fieldCount][];
        for (var index = 0; index < fields.Length; index++)
        {
            fields[index] = new float[cellCount];
        }

        return fields;
    }
}
