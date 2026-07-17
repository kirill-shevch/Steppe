using System;
using Steppe.Settings;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Steppe.Time
{
    public enum SteppeSeason
    {
        Winter,
        Spring,
        Summer,
        Autumn
    }

    public readonly struct SteppeTimeSnapshot
    {
        public SteppeTimeSnapshot(
            double absoluteDay,
            double dayOfYear,
            double hour,
            double yearFraction,
            long year,
            SteppeSeason season)
        {
            AbsoluteDay = absoluteDay;
            DayOfYear = dayOfYear;
            Hour = hour;
            YearFraction = yearFraction;
            Year = year;
            Season = season;
        }

        public double AbsoluteDay { get; }
        public double DayOfYear { get; }
        public double Hour { get; }
        public double YearFraction { get; }
        public long Year { get; }
        public SteppeSeason Season { get; }
    }

    public sealed class SteppeTimeSystem : MonoBehaviour
    {
        private static readonly float[] DebugMultipliers = { 1f, 10f, 100f };

        private SteppeWorldSettings settings;
        private double elapsedSimulationSeconds;
        private int debugMultiplierIndex;

        public bool IsPaused { get; private set; }
        public float DebugMultiplier => DebugMultipliers[debugMultiplierIndex];
        public float CurrentSimulationRate => settings == null
            ? 0f
            : settings.SimulationSecondsPerRealSecond * DebugMultiplier;
        public double ElapsedSimulationSeconds => elapsedSimulationSeconds;
        public SteppeTimeSnapshot Current => settings == null
            ? default
            : CreateSnapshot(settings, elapsedSimulationSeconds);

        public void Configure(SteppeWorldSettings worldSettings)
        {
            settings = worldSettings != null ? worldSettings : throw new ArgumentNullException(nameof(worldSettings));
            elapsedSimulationSeconds = 0.0;
            debugMultiplierIndex = 0;
            IsPaused = false;
        }

        public void AdvanceSimulationSeconds(double seconds)
        {
            elapsedSimulationSeconds = Math.Max(0.0, elapsedSimulationSeconds + seconds);
        }

        public static SteppeTimeSnapshot CreateSnapshot(SteppeWorldSettings settings, double elapsedSeconds)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            var startingDay = settings.StartingDayOfYear + settings.StartingHour / 24.0;
            var absoluteDay = startingDay + Math.Max(0.0, elapsedSeconds) / 86400.0;
            var dayOfYear = PositiveModulo(absoluteDay, settings.DaysPerYear);
            var hour = PositiveModulo(absoluteDay * 24.0, 24.0);
            var yearFraction = dayOfYear / settings.DaysPerYear;
            var year = (long)Math.Floor(absoluteDay / settings.DaysPerYear);
            return new SteppeTimeSnapshot(
                absoluteDay,
                dayOfYear,
                hour,
                yearFraction,
                year,
                SeasonFor(yearFraction));
        }

        private void Update()
        {
            HandleDebugInput();
            if (!IsPaused)
            {
                AdvanceSimulationSeconds(UnityEngine.Time.deltaTime * CurrentSimulationRate);
            }
        }

        private void HandleDebugInput()
        {
            if (Keyboard.current == null)
            {
                return;
            }

            if (Keyboard.current.f5Key.wasPressedThisFrame)
            {
                IsPaused = !IsPaused;
            }

            if (Keyboard.current.f6Key.wasPressedThisFrame)
            {
                debugMultiplierIndex = (debugMultiplierIndex + 1) % DebugMultipliers.Length;
            }
        }

        private static SteppeSeason SeasonFor(double yearFraction)
        {
            if (yearFraction < 0.125 || yearFraction >= 0.875)
            {
                return SteppeSeason.Winter;
            }

            if (yearFraction < 0.375)
            {
                return SteppeSeason.Spring;
            }

            if (yearFraction < 0.625)
            {
                return SteppeSeason.Summer;
            }

            return SteppeSeason.Autumn;
        }

        private static double PositiveModulo(double value, double modulus)
        {
            return ((value % modulus) + modulus) % modulus;
        }
    }
}
