using System;

namespace Steppe.UnitySimulation
{
    /// <summary>
    /// Maps canonical, floating-origin-independent Unity world metres to finite simulation cells.
    /// The finite world is centred around Unity world coordinate (0, 0).
    /// </summary>
    public sealed class WorldCoordinateMapper
    {
        public WorldCoordinateMapper(int width, int height, float cellSizeMeters)
        {
            if (width <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(width));
            }

            if (height <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(height));
            }

            if (float.IsNaN(cellSizeMeters) || float.IsInfinity(cellSizeMeters) || cellSizeMeters <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(cellSizeMeters));
            }

            Width = width;
            Height = height;
            CellSizeMeters = cellSizeMeters;
            MinimumWorldX = -width * cellSizeMeters * 0.5d;
            MinimumWorldZ = -height * cellSizeMeters * 0.5d;
        }

        public int Width { get; }
        public int Height { get; }
        public float CellSizeMeters { get; }
        public double MinimumWorldX { get; }
        public double MinimumWorldZ { get; }
        public double MaximumWorldX => MinimumWorldX + Width * CellSizeMeters;
        public double MaximumWorldZ => MinimumWorldZ + Height * CellSizeMeters;

        public bool TryWorldToCell(double worldX, double worldZ, out int cellX, out int cellY)
        {
            cellX = (int)Math.Floor((worldX - MinimumWorldX) / CellSizeMeters);
            cellY = (int)Math.Floor((worldZ - MinimumWorldZ) / CellSizeMeters);
            return cellX >= 0 && cellX < Width && cellY >= 0 && cellY < Height;
        }

        /// <summary>
        /// Returns the four cell-centre samples surrounding a world position.
        /// The half-cell rim of the finite map uses edge extension so fields stay
        /// continuous all the way to the playable boundary.
        /// </summary>
        public bool TryGetBilinearSample(
            double worldX,
            double worldZ,
            out int x0,
            out int y0,
            out int x1,
            out int y1,
            out float blendX,
            out float blendY)
        {
            x0 = y0 = x1 = y1 = 0;
            blendX = blendY = 0f;
            if (!TryWorldToCell(worldX, worldZ, out _, out _))
            {
                return false;
            }

            var gridX = (worldX - MinimumWorldX) / CellSizeMeters - 0.5d;
            var gridY = (worldZ - MinimumWorldZ) / CellSizeMeters - 0.5d;
            var lowerX = (int)Math.Floor(gridX);
            var lowerY = (int)Math.Floor(gridY);
            blendX = (float)(gridX - lowerX);
            blendY = (float)(gridY - lowerY);
            x0 = Math.Max(0, Math.Min(Width - 1, lowerX));
            y0 = Math.Max(0, Math.Min(Height - 1, lowerY));
            x1 = Math.Max(0, Math.Min(Width - 1, lowerX + 1));
            y1 = Math.Max(0, Math.Min(Height - 1, lowerY + 1));
            return true;
        }

        public void CellCenterToWorld(int cellX, int cellY, out double worldX, out double worldZ)
        {
            if (cellX < 0 || cellX >= Width)
            {
                throw new ArgumentOutOfRangeException(nameof(cellX));
            }

            if (cellY < 0 || cellY >= Height)
            {
                throw new ArgumentOutOfRangeException(nameof(cellY));
            }

            worldX = MinimumWorldX + (cellX + 0.5d) * CellSizeMeters;
            worldZ = MinimumWorldZ + (cellY + 0.5d) * CellSizeMeters;
        }

        public void GridPositionToWorld(float cellX, float cellY, out double worldX, out double worldZ)
        {
            worldX = MinimumWorldX + (cellX + 0.5d) * CellSizeMeters;
            worldZ = MinimumWorldZ + (cellY + 0.5d) * CellSizeMeters;
        }
    }
}
