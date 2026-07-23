using System;
using System.Collections.Generic;

namespace Steppe.Caravan
{
    public readonly struct CaravanGridPlacement
    {
        public CaravanGridPlacement(int x, int z, int width, int length, int quarterTurns)
        {
            X = x;
            Z = z;
            Width = Math.Max(1, width);
            Length = Math.Max(1, length);
            QuarterTurns = PositiveModulo(quarterTurns, 4);
        }

        public int X { get; }
        public int Z { get; }
        public int Width { get; }
        public int Length { get; }
        public int QuarterTurns { get; }
        public int RotatedWidth => QuarterTurns % 2 == 0 ? Width : Length;
        public int RotatedLength => QuarterTurns % 2 == 0 ? Length : Width;

        private static int PositiveModulo(int value, int modulus)
        {
            return ((value % modulus) + modulus) % modulus;
        }
    }

    /// <summary>
    /// Pure occupancy model for the first-person build mode. It knows nothing about
    /// Unity objects, raycasts or visuals and can therefore be validated in EditMode.
    /// </summary>
    public sealed class CaravanMountGridModel
    {
        private readonly Dictionary<object, CaravanGridPlacement> placements =
            new Dictionary<object, CaravanGridPlacement>();

        public CaravanMountGridModel(int width, int length)
        {
            Width = Math.Max(1, width);
            Length = Math.Max(1, length);
        }

        public int Width { get; }
        public int Length { get; }
        public int Count => placements.Count;

        public bool CanPlace(object module, CaravanGridPlacement candidate)
        {
            if (module == null
                || candidate.X < 0
                || candidate.Z < 0
                || candidate.X + candidate.RotatedWidth > Width
                || candidate.Z + candidate.RotatedLength > Length)
            {
                return false;
            }

            foreach (var pair in placements)
            {
                if (ReferenceEquals(pair.Key, module))
                {
                    continue;
                }

                if (Overlaps(candidate, pair.Value))
                {
                    return false;
                }
            }

            return true;
        }

        public bool TryPlace(object module, CaravanGridPlacement placement)
        {
            if (!CanPlace(module, placement))
            {
                return false;
            }

            placements[module] = placement;
            return true;
        }

        public bool Remove(object module, out CaravanGridPlacement previous)
        {
            if (module != null && placements.TryGetValue(module, out previous))
            {
                placements.Remove(module);
                return true;
            }

            previous = default;
            return false;
        }

        public bool TryGetPlacement(object module, out CaravanGridPlacement placement)
        {
            if (module != null && placements.TryGetValue(module, out placement))
            {
                return true;
            }

            placement = default;
            return false;
        }

        private static bool Overlaps(CaravanGridPlacement first, CaravanGridPlacement second)
        {
            return first.X < second.X + second.RotatedWidth
                   && first.X + first.RotatedWidth > second.X
                   && first.Z < second.Z + second.RotatedLength
                   && first.Z + first.RotatedLength > second.Z;
        }
    }
}
