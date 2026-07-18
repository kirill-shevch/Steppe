using System;
using Steppe.Surface;
using UnityEngine;

namespace Steppe.Ecology
{
    /// <summary>
    /// Stable CPU/GPU channel contract for the P8 state map.
    /// R = surface water, G = biomass relative to the canonical vegetation capacity,
    /// B = green fraction, A = dry surface crust.
    /// </summary>
    public static class SteppeEcologyMapEncoding
    {
        public static readonly Color32 Neutral = new Color32(0, 255, 128, 0);

        public static Color32 Encode(SteppeEcoCellState state, SurfaceSample surface)
        {
            var capacity = Math.Max(0.001, surface.VegetationPotential);
            var relativeBiomass = state.Biomass / capacity;
            return new Color32(
                ToByte(state.SurfaceWater),
                ToByte(relativeBiomass),
                ToByte(state.GreenFraction),
                ToByte(state.SurfaceCrust));
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
