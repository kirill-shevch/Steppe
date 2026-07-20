using System;
using UnityEngine;

namespace Steppe.Ecology
{
    /// <summary>
    /// Companion map to the soil/vegetation state map.
    /// R = snow water, G = snow compaction, B = frozen soil, A = reserved.
    /// </summary>
    public static class SteppeCryosphereMapEncoding
    {
        public static readonly Color32 Neutral = new Color32(0, 0, 0, 0);

        public static Color32 Encode(SteppeEcoCellState state)
        {
            return new Color32(
                ToByte(state.SnowWater),
                ToByte(state.SnowCompaction),
                ToByte(state.FrozenFraction),
                0);
        }

        public static double Decode(byte value)
        {
            return value / 255.0;
        }

        private static byte ToByte(double value)
        {
            return (byte)Math.Round(Math.Max(0.0, Math.Min(1.0, value)) * 255.0);
        }
    }
}
