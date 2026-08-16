using System;
using System.Collections.Generic;

namespace Steppe.Caravan
{
    public readonly struct CaravanGridCell : IEquatable<CaravanGridCell>
    {
        public CaravanGridCell(int x, int z)
        {
            X = x;
            Z = z;
        }

        public int X { get; }
        public int Z { get; }

        public bool Equals(CaravanGridCell other)
        {
            return X == other.X && Z == other.Z;
        }

        public override bool Equals(object obj)
        {
            return obj is CaravanGridCell other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (X * 397) ^ Z;
            }
        }
    }

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

    public readonly struct CaravanGridBounds
    {
        public CaravanGridBounds(int minimumX, int maximumX, int minimumZ, int maximumZ)
        {
            MinimumX = minimumX;
            MaximumX = maximumX;
            MinimumZ = minimumZ;
            MaximumZ = maximumZ;
        }

        public int MinimumX { get; }
        public int MaximumX { get; }
        public int MinimumZ { get; }
        public int MaximumZ { get; }
        public int Width => MaximumX - MinimumX + 1;
        public int Length => MaximumZ - MinimumZ + 1;
    }

    /// <summary>
    /// Pure occupancy model for the first-person build mode. It knows nothing about
    /// Unity objects, raycasts or visuals and can therefore be validated in EditMode.
    /// </summary>
    public sealed class CaravanMountGridModel
    {
        private readonly Dictionary<object, CaravanGridPlacement> placements =
            new Dictionary<object, CaravanGridPlacement>();
        private readonly HashSet<CaravanGridCell> platformCells =
            new HashSet<CaravanGridCell>();

        public CaravanMountGridModel(int width, int length)
        {
            Width = Math.Max(1, width);
            Length = Math.Max(1, length);
            for (var z = 0; z < Length; z++)
            {
                for (var x = 0; x < Width; x++)
                {
                    platformCells.Add(new CaravanGridCell(x, z));
                }
            }
        }

        public int Width { get; }
        public int Length { get; }
        public int Count => placements.Count;
        public int PlatformCellCount => platformCells.Count;

        public bool CanPlace(object module, CaravanGridPlacement candidate)
        {
            if (module == null
                || !HasPlatformFor(candidate))
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

        public CaravanGridCell[] CopyPlatformCells()
        {
            var result = new CaravanGridCell[platformCells.Count];
            platformCells.CopyTo(result);
            return result;
        }

        public bool TryReplacePlatformCells(
            IReadOnlyList<CaravanGridCell> cells)
        {
            if (cells == null || cells.Count == 0 || placements.Count > 0)
            {
                return false;
            }

            var candidate = new HashSet<CaravanGridCell>();
            for (var index = 0; index < cells.Count; index++)
            {
                candidate.Add(cells[index]);
            }
            for (var z = 0; z < Length; z++)
            {
                for (var x = 0; x < Width; x++)
                {
                    if (!candidate.Contains(new CaravanGridCell(x, z)))
                    {
                        return false;
                    }
                }
            }

            var visited = new HashSet<CaravanGridCell>();
            var pending = new Stack<CaravanGridCell>();
            pending.Push(new CaravanGridCell(0, 0));
            while (pending.Count > 0)
            {
                var current = pending.Pop();
                if (!candidate.Contains(current) || !visited.Add(current))
                {
                    continue;
                }
                pending.Push(new CaravanGridCell(current.X - 1, current.Z));
                pending.Push(new CaravanGridCell(current.X + 1, current.Z));
                pending.Push(new CaravanGridCell(current.X, current.Z - 1));
                pending.Push(new CaravanGridCell(current.X, current.Z + 1));
            }
            if (visited.Count != candidate.Count)
            {
                return false;
            }

            platformCells.Clear();
            platformCells.UnionWith(candidate);
            return true;
        }

        public bool HasPlatformCell(int x, int z)
        {
            return platformCells.Contains(new CaravanGridCell(x, z));
        }

        public bool TryGetPlatformBounds(out CaravanGridBounds bounds)
        {
            if (platformCells.Count == 0)
            {
                bounds = default;
                return false;
            }

            var minimumX = int.MaxValue;
            var maximumX = int.MinValue;
            var minimumZ = int.MaxValue;
            var maximumZ = int.MinValue;
            foreach (var cell in platformCells)
            {
                minimumX = Math.Min(minimumX, cell.X);
                maximumX = Math.Max(maximumX, cell.X);
                minimumZ = Math.Min(minimumZ, cell.Z);
                maximumZ = Math.Max(maximumZ, cell.Z);
            }

            bounds = new CaravanGridBounds(
                minimumX,
                maximumX,
                minimumZ,
                maximumZ);
            return true;
        }

        public bool CanAddPlatformCell(int x, int z)
        {
            if (HasPlatformCell(x, z))
            {
                return false;
            }

            return HasPlatformCell(x - 1, z)
                   || HasPlatformCell(x + 1, z)
                   || HasPlatformCell(x, z - 1)
                   || HasPlatformCell(x, z + 1);
        }

        public bool TryAddPlatformCell(int x, int z)
        {
            return CanAddPlatformCell(x, z)
                   && platformCells.Add(new CaravanGridCell(x, z));
        }

        public bool CanAddPlatformCells(IReadOnlyList<CaravanGridCell> cells)
        {
            if (cells == null || cells.Count == 0)
            {
                return false;
            }

            var additions = new HashSet<CaravanGridCell>();
            for (var index = 0; index < cells.Count; index++)
            {
                if (HasPlatformCell(cells[index].X, cells[index].Z)
                    || !additions.Add(cells[index]))
                {
                    return false;
                }
            }

            var connected = false;
            foreach (var cell in additions)
            {
                if (HasPlatformCell(cell.X - 1, cell.Z)
                    || HasPlatformCell(cell.X + 1, cell.Z)
                    || HasPlatformCell(cell.X, cell.Z - 1)
                    || HasPlatformCell(cell.X, cell.Z + 1))
                {
                    connected = true;
                    break;
                }
            }
            if (!connected)
            {
                return false;
            }

            var reachable = new HashSet<CaravanGridCell>();
            var pending = new Stack<CaravanGridCell>();
            foreach (var cell in additions)
            {
                if (HasPlatformCell(cell.X - 1, cell.Z)
                    || HasPlatformCell(cell.X + 1, cell.Z)
                    || HasPlatformCell(cell.X, cell.Z - 1)
                    || HasPlatformCell(cell.X, cell.Z + 1))
                {
                    pending.Push(cell);
                }
            }
            while (pending.Count > 0)
            {
                var current = pending.Pop();
                if (!additions.Contains(current) || !reachable.Add(current))
                {
                    continue;
                }
                pending.Push(new CaravanGridCell(current.X - 1, current.Z));
                pending.Push(new CaravanGridCell(current.X + 1, current.Z));
                pending.Push(new CaravanGridCell(current.X, current.Z - 1));
                pending.Push(new CaravanGridCell(current.X, current.Z + 1));
            }
            return reachable.Count == additions.Count;
        }

        public bool TryAddPlatformCells(IReadOnlyList<CaravanGridCell> cells)
        {
            if (!CanAddPlatformCells(cells))
            {
                return false;
            }

            for (var index = 0; index < cells.Count; index++)
            {
                platformCells.Add(cells[index]);
            }
            return true;
        }

        private bool HasPlatformFor(CaravanGridPlacement placement)
        {
            for (var z = placement.Z;
                 z < placement.Z + placement.RotatedLength;
                 z++)
            {
                for (var x = placement.X;
                     x < placement.X + placement.RotatedWidth;
                     x++)
                {
                    if (!HasPlatformCell(x, z))
                    {
                        return false;
                    }
                }
            }
            return true;
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
