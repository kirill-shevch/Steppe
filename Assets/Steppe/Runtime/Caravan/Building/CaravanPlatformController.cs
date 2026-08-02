using System;
using System.Collections.Generic;
using UnityEngine;

namespace Steppe.Caravan
{
    /// <summary>
    /// Owns the physical cells of the P18 caravan platform. The mount grid remains
    /// the occupancy authority; this component pays for valid adjacent cells and
    /// gives them collision, visuals and structural mass.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CaravanPlatformController : MonoBehaviour
    {
        private CaravanMountGrid grid;
        private CaravanChassisController chassis;
        private CaravanProgressionSystem progression;
        private Material deckMaterial;
        private Material frameMaterial;
        private int startingCellCount;
        private readonly Dictionary<CaravanGridCell, GameObject> cellObjects =
            new Dictionary<CaravanGridCell, GameObject>();

        public int CellCount => grid != null ? grid.PlatformCellCount : 0;
        public int ExpandedCellCount => Mathf.Max(0, CellCount - startingCellCount);

        public void Configure(
            CaravanMountGrid mountGrid,
            CaravanChassisController caravan,
            CaravanProgressionSystem progressionSystem,
            Material deck,
            Material frame)
        {
            grid = mountGrid != null
                ? mountGrid
                : throw new ArgumentNullException(nameof(mountGrid));
            chassis = caravan != null
                ? caravan
                : throw new ArgumentNullException(nameof(caravan));
            progression = progressionSystem != null
                ? progressionSystem
                : throw new ArgumentNullException(nameof(progressionSystem));
            deckMaterial = deck;
            frameMaterial = frame;
            startingCellCount = grid.PlatformCellCount;
            cellObjects.Clear();

            for (var z = 0; z < grid.Length; z++)
            {
                for (var x = 0; x < grid.Width; x++)
                {
                    CreateCell(x, z, false);
                }
            }
        }

        public bool CanExpand(int x, int z)
        {
            if (grid == null
                || progression == null
                || !TryGetExpansionLine(x, z, out var cells))
            {
                return false;
            }

            return grid.CanAddPlatformCells(cells)
                   && progression.HasResources(ExpansionCost(cells.Length));
        }

        public bool TryExpand(int x, int z)
        {
            if (!TryGetExpansionLine(x, z, out var cells)
                || !grid.CanAddPlatformCells(cells))
            {
                return false;
            }

            var cost = ExpansionCost(cells.Length);
            if (!progression.TrySpend(cost))
            {
                return false;
            }
            if (!grid.TryAddPlatformCells(cells))
            {
                progression.AddResources(cost);
                return false;
            }

            for (var index = 0; index < cells.Length; index++)
            {
                CreateCell(cells[index].X, cells[index].Z, true);
            }
            UpdateWheelFootprint();
            chassis.RefreshMassProperties();
            chassis.NotifyStructureCollidersChanged();
            return true;
        }

        public bool TryGetExpansionLine(
            int x,
            int z,
            out CaravanGridCell[] cells)
        {
            cells = Array.Empty<CaravanGridCell>();
            if (grid == null
                || !grid.TryGetPlatformBounds(out var bounds))
            {
                return false;
            }

            if (x == bounds.MinimumX - 1
                && z >= bounds.MinimumZ
                && z <= bounds.MaximumZ)
            {
                cells = Column(x, bounds.MinimumZ, bounds.MaximumZ);
                return true;
            }
            if (x == bounds.MaximumX + 1
                && z >= bounds.MinimumZ
                && z <= bounds.MaximumZ)
            {
                cells = Column(x, bounds.MinimumZ, bounds.MaximumZ);
                return true;
            }
            if (z == bounds.MinimumZ - 1
                && x >= bounds.MinimumX
                && x <= bounds.MaximumX)
            {
                cells = Row(z, bounds.MinimumX, bounds.MaximumX);
                return true;
            }
            if (z == bounds.MaximumZ + 1
                && x >= bounds.MinimumX
                && x <= bounds.MaximumX)
            {
                cells = Row(z, bounds.MinimumX, bounds.MaximumX);
                return true;
            }
            return false;
        }

        public CaravanResourceAmount[] GetExpansionCost(int x, int z)
        {
            return TryGetExpansionLine(x, z, out var cells)
                ? ExpansionCost(cells.Length)
                : Array.Empty<CaravanResourceAmount>();
        }

        public string FormatExpansionCost(int x, int z)
        {
            var cost = GetExpansionCost(x, z);
            return cost.Length > 0 && progression != null
                ? progression.FormatCost(cost)
                : string.Empty;
        }

        public bool TryRestoreCells(IReadOnlyList<CaravanGridCell> cells)
        {
            if (grid == null || cells == null
                || !grid.TryRestorePlatformCells(cells))
            {
                return false;
            }

            var desired = new HashSet<CaravanGridCell>();
            for (var index = 0; index < cells.Count; index++)
            {
                desired.Add(cells[index]);
            }
            var removed = new List<CaravanGridCell>();
            foreach (var pair in cellObjects)
            {
                if (!desired.Contains(pair.Key))
                {
                    pair.Value.SetActive(false);
                    Destroy(pair.Value);
                    removed.Add(pair.Key);
                }
            }
            for (var index = 0; index < removed.Count; index++)
            {
                cellObjects.Remove(removed[index]);
            }
            foreach (var cell in desired)
            {
                if (!cellObjects.ContainsKey(cell))
                {
                    CreateCell(
                        cell.X,
                        cell.Z,
                        !IsStartingCell(cell.X, cell.Z));
                }
            }

            UpdateWheelFootprint();
            chassis.RefreshMassProperties();
            chassis.NotifyStructureCollidersChanged();
            return true;
        }

        private static CaravanGridCell[] Row(
            int z,
            int minimumX,
            int maximumX)
        {
            var result = new CaravanGridCell[maximumX - minimumX + 1];
            for (var x = minimumX; x <= maximumX; x++)
            {
                result[x - minimumX] = new CaravanGridCell(x, z);
            }
            return result;
        }

        private static CaravanGridCell[] Column(
            int x,
            int minimumZ,
            int maximumZ)
        {
            var result = new CaravanGridCell[maximumZ - minimumZ + 1];
            for (var z = minimumZ; z <= maximumZ; z++)
            {
                result[z - minimumZ] = new CaravanGridCell(x, z);
            }
            return result;
        }

        private static CaravanResourceAmount[] ExpansionCost(int cellCount)
        {
            return CaravanConstructionCosts.Scale(
                CaravanConstructionCosts.PlatformTile,
                cellCount);
        }

        private void UpdateWheelFootprint()
        {
            if (grid == null
                || chassis == null
                || !grid.TryGetPlatformBounds(out var bounds))
            {
                return;
            }

            grid.GetWorldCellPose(
                bounds.MinimumX,
                bounds.MinimumZ,
                out var minimumWorld,
                out _);
            grid.GetWorldCellPose(
                bounds.MaximumX,
                bounds.MaximumZ,
                out var maximumWorld,
                out _);
            var minimum = chassis.transform.InverseTransformPoint(minimumWorld);
            var maximum = chassis.transform.InverseTransformPoint(maximumWorld);
            chassis.FitWheelsToPlatformBounds(
                minimum.x - grid.CellSize * 0.72f,
                maximum.x + grid.CellSize * 0.72f,
                minimum.z + grid.CellSize * 0.18f,
                maximum.z - grid.CellSize * 0.18f);
        }

        private void CreateCell(int x, int z, bool contributesExpansionMass)
        {
            var cell = new CaravanGridCell(x, z);
            if (cellObjects.ContainsKey(cell))
            {
                return;
            }
            grid.GetWorldCellPose(x, z, out var worldPosition, out var worldRotation);
            var root = GameObject.CreatePrimitive(PrimitiveType.Cube);
            root.name = $"Platform Cell {x},{z}";
            root.transform.SetParent(transform, true);
            root.transform.SetPositionAndRotation(
                worldPosition + transform.up * -0.055f,
                worldRotation);
            root.transform.localScale = new Vector3(
                grid.CellSize * 0.94f,
                0.11f,
                grid.CellSize * 0.94f);
            root.GetComponent<Renderer>().sharedMaterial = deckMaterial;
            root.AddComponent<CaravanBuildSurface>();
            cellObjects[cell] = root;

            CreateBeam(
                root.transform,
                "Underframe X",
                new Vector3(0f, -1.55f, 0f),
                new Vector3(1.02f, 0.8f, 0.12f));
            CreateBeam(
                root.transform,
                "Underframe Z",
                new Vector3(0f, -1.55f, 0f),
                new Vector3(0.12f, 0.8f, 1.02f));

            if (!contributesExpansionMass)
            {
                return;
            }

            var module = root.AddComponent<CaravanModule>();
            module.Configure(
                "platform-tile",
                root.transform,
                false,
                1,
                1,
                null,
                55f,
                new Vector3(0f, -0.06f, 0f),
                $"platform-{x}-{z}");
        }

        private bool IsStartingCell(int x, int z)
        {
            return x >= 0 && x < grid.Width
                          && z >= 0 && z < grid.Length;
        }

        private void CreateBeam(
            Transform parent,
            string name,
            Vector3 localPosition,
            Vector3 localScale)
        {
            var beam = GameObject.CreatePrimitive(PrimitiveType.Cube);
            beam.name = name;
            beam.transform.SetParent(parent, false);
            beam.transform.localPosition = localPosition;
            beam.transform.localScale = localScale;
            beam.GetComponent<Renderer>().sharedMaterial = frameMaterial;
            var collider = beam.GetComponent<Collider>();
            if (collider != null)
            {
                collider.enabled = false;
            }
        }
    }
}
