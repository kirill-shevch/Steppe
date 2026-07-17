using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

namespace Steppe.World
{
    public interface IWorldWorkSource
    {
        bool HasPendingWorldWork { get; }
        void ExecuteWorldWorkStep();
    }

    /// <summary>
    /// Shares one main-thread time budget between terrain, vegetation, and weather work.
    /// A single step may exceed the budget, but expensive sources can no longer stack
    /// several independent per-frame quotas on the same frame.
    /// </summary>
    [DefaultExecutionOrder(1000)]
    [DisallowMultipleComponent]
    public sealed class WorldWorkScheduler : MonoBehaviour
    {
        private readonly List<IWorldWorkSource> sources = new List<IWorldWorkSource>();
        private double budgetMilliseconds = 6.0;
        private int nextSourceIndex;

        public int RegisteredSourceCount => sources.Count;
        public int LastFrameStepCount { get; private set; }
        public double LastFrameWorkMilliseconds { get; private set; }
        public long TotalStepsExecuted { get; private set; }

        public void Configure(float millisecondsPerFrame)
        {
            budgetMilliseconds = Math.Max(0.25, millisecondsPerFrame);
        }

        public void Register(IWorldWorkSource source)
        {
            if (source == null || sources.Contains(source))
            {
                return;
            }

            sources.Add(source);
        }

        public void Unregister(IWorldWorkSource source)
        {
            var index = sources.IndexOf(source);
            if (index < 0)
            {
                return;
            }

            sources.RemoveAt(index);
            if (sources.Count == 0)
            {
                nextSourceIndex = 0;
            }
            else if (index < nextSourceIndex)
            {
                nextSourceIndex--;
            }
        }

        private void Update()
        {
            PruneDestroyedSources();
            LastFrameStepCount = 0;
            LastFrameWorkMilliseconds = 0.0;
            if (sources.Count == 0)
            {
                return;
            }

            var started = Stopwatch.GetTimestamp();
            var idleVisits = 0;
            var safety = 0;
            while (sources.Count > 0 && idleVisits < sources.Count && safety++ < 4096)
            {
                if (LastFrameStepCount > 0 && ElapsedMilliseconds(started) >= budgetMilliseconds)
                {
                    break;
                }

                if (nextSourceIndex >= sources.Count)
                {
                    nextSourceIndex = 0;
                }

                var source = sources[nextSourceIndex];
                nextSourceIndex = (nextSourceIndex + 1) % sources.Count;
                if (!source.HasPendingWorldWork)
                {
                    idleVisits++;
                    continue;
                }

                source.ExecuteWorldWorkStep();
                LastFrameStepCount++;
                TotalStepsExecuted++;
                idleVisits = 0;
            }

            LastFrameWorkMilliseconds = ElapsedMilliseconds(started);
        }

        private void PruneDestroyedSources()
        {
            for (var index = sources.Count - 1; index >= 0; index--)
            {
                var source = sources[index];
                if (source == null || source is UnityEngine.Object unityObject && unityObject == null)
                {
                    sources.RemoveAt(index);
                }
            }

            if (sources.Count == 0 || nextSourceIndex >= sources.Count)
            {
                nextSourceIndex = 0;
            }
        }

        private static double ElapsedMilliseconds(long started)
        {
            return (Stopwatch.GetTimestamp() - started) * 1000.0 / Stopwatch.Frequency;
        }
    }
}
