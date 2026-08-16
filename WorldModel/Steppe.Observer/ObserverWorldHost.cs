using Steppe.Simulation;

namespace Steppe.Observer;

internal sealed class ObserverWorldHost(WorldConfig initialConfig)
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private FiniteWorld world = new(initialConfig);

    public T Read<T>(Func<FiniteWorld, T> operation) => operation(world);

    public async Task<T> AdvanceAsync<T>(double hours, Func<FiniteWorld, T> result, CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            await Task.Run(() => world.AdvanceHours(hours, cancellationToken), cancellationToken);
            return result(world);
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<WorldSummary> ResetAsync(int seed, int size, CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            var previous = world.Config;
            world = await Task.Run(() => new FiniteWorld(previous with
            {
                Seed = seed,
                Width = size,
                Height = size
            }), cancellationToken);
            return world.GetSummary();
        }
        finally
        {
            gate.Release();
        }
    }
}
