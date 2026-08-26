using System;
using Steppe.Settings;
using Steppe.UnitySimulation;
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

    [DefaultExecutionOrder(-200)]
    public sealed class SteppeTimeSystem : MonoBehaviour
    {
        public const int CanonicalDaysPerYear = 365;
        private static readonly float[] DebugMultipliers = { 1f, 10f, 100f };

        private SteppeWorldSettings settings;
        private SteppeSimulationHost simulationHost;
        private double elapsedSimulationSeconds;
        private int debugMultiplierIndex;

        public bool IsPaused { get; private set; }
        public float DebugMultiplier => DebugMultipliers[debugMultiplierIndex];
        public float CurrentSimulationRate => settings == null
            ? 0f
            : settings.SimulationSecondsPerRealSecond * DebugMultiplier;
        public bool UsesFiniteWorld => simulationHost != null;
        public int DaysPerYear => UsesFiniteWorld
            ? CanonicalDaysPerYear
            : settings != null ? settings.DaysPerYear : 0;
        public double ElapsedSimulationSeconds => elapsedSimulationSeconds;
        public SteppeTimeSnapshot Current
        {
            get
            {
                if (simulationHost != null)
                {
                    return CreateCanonicalSnapshot(elapsedSimulationSeconds);
                }

                return settings == null
                    ? default
                    : CreateSnapshot(settings, elapsedSimulationSeconds);
            }
        }

        public void Configure(
            SteppeWorldSettings worldSettings,
            SteppeSimulationHost finiteSimulation = null)
        {
            settings = worldSettings != null ? worldSettings : throw new ArgumentNullException(nameof(worldSettings));
            simulationHost = finiteSimulation;
            elapsedSimulationSeconds = 0.0;
            debugMultiplierIndex = 0;
            IsPaused = false;
            ApplyCanonicalTimeControl();
        }

        public void AdvanceSimulationSeconds(double seconds)
        {
            if (double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds < 0d)
            {
                throw new ArgumentOutOfRangeException(nameof(seconds));
            }

            if (simulationHost != null)
            {
                elapsedSimulationSeconds = Math.Max(0.0, elapsedSimulationSeconds + seconds);
                simulationHost.SetPresentationTime(elapsedSimulationSeconds);
            }
            else
            {
                elapsedSimulationSeconds = Math.Max(0.0, elapsedSimulationSeconds + seconds);
            }
        }

        public void SetPaused(bool paused)
        {
            IsPaused = paused;
            ApplyCanonicalTimeControl();
        }

        public void CycleDebugMultiplier()
        {
            debugMultiplierIndex = (debugMultiplierIndex + 1) % DebugMultipliers.Length;
            ApplyCanonicalTimeControl();
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

        public static SteppeTimeSnapshot CreateCanonicalSnapshot(
            SteppeSimulationTimeSample canonical)
        {
            if (canonical == null)
            {
                throw new ArgumentNullException(nameof(canonical));
            }

            var dayFraction = canonical.HourOfDay / 24d;
            var zeroBasedDay = Math.Max(0d, canonical.DayOfYear - 1d) + dayFraction;
            return new SteppeTimeSnapshot(
                canonical.ElapsedSimulationSeconds / 86400d + 1d,
                canonical.DayOfYear + dayFraction,
                canonical.HourOfDay,
                zeroBasedDay / CanonicalDaysPerYear,
                Math.Max(0, canonical.Year - 1),
                ParseCanonicalSeason(canonical.Season));
        }

        public static SteppeTimeSnapshot CreateCanonicalSnapshot(double elapsedSeconds)
        {
            var clampedSeconds = Math.Max(0d, elapsedSeconds);
            var elapsedHours = clampedSeconds / 3600d;
            var wholeDays = (long)Math.Floor(elapsedHours / 24d);
            var hour = PositiveModulo(elapsedHours, 24d);
            var day = wholeDays % CanonicalDaysPerYear + 1d;
            var dayWithFraction = day + hour / 24d;
            var zeroBasedDay = day - 1d + hour / 24d;
            return new SteppeTimeSnapshot(
                clampedSeconds / 86400d + 1d,
                dayWithFraction,
                hour,
                zeroBasedDay / CanonicalDaysPerYear,
                wholeDays / CanonicalDaysPerYear,
                CanonicalSeason((int)day));
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
                SetPaused(!IsPaused);
            }

            if (Keyboard.current.f6Key.wasPressedThisFrame)
            {
                CycleDebugMultiplier();
            }
        }

        private void ApplyCanonicalTimeControl()
        {
            if (simulationHost == null)
            {
                return;
            }

            simulationHost.SetTimeControl(IsPaused, DebugMultiplier);
            simulationHost.SetPresentationTime(elapsedSimulationSeconds);
        }

        private static SteppeSeason CanonicalSeason(int dayOfYear)
        {
            if (dayOfYear >= 80 && dayOfYear < 172)
            {
                return SteppeSeason.Spring;
            }

            if (dayOfYear >= 172 && dayOfYear < 266)
            {
                return SteppeSeason.Summer;
            }

            if (dayOfYear >= 266 && dayOfYear < 355)
            {
                return SteppeSeason.Autumn;
            }

            return SteppeSeason.Winter;
        }

        private static SteppeSeason ParseCanonicalSeason(string season)
        {
            if (string.Equals(season, "Winter", StringComparison.OrdinalIgnoreCase))
            {
                return SteppeSeason.Winter;
            }

            if (string.Equals(season, "Summer", StringComparison.OrdinalIgnoreCase))
            {
                return SteppeSeason.Summer;
            }

            if (string.Equals(season, "Autumn", StringComparison.OrdinalIgnoreCase)
                || string.Equals(season, "Fall", StringComparison.OrdinalIgnoreCase))
            {
                return SteppeSeason.Autumn;
            }

            return SteppeSeason.Spring;
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
