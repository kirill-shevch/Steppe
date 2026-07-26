using System;
using UnityEngine;

namespace Steppe.Caravan
{
    [DisallowMultipleComponent]
    public sealed class CaravanMountGrid : MonoBehaviour
    {
        private CaravanMountGridModel model;
        private int width;
        private int length;
        private float cellSize;
        private float deckHeight;

        public int Width => width;
        public int Length => length;
        public float CellSize => cellSize;

        public void Configure(int cellsWide, int cellsLong, float size, float localDeckHeight)
        {
            width = Mathf.Max(1, cellsWide);
            length = Mathf.Max(1, cellsLong);
            cellSize = Mathf.Max(0.25f, size);
            deckHeight = localDeckHeight;
            model = new CaravanMountGridModel(width, length);
        }

        public bool Register(CaravanModule module, CaravanGridPlacement placement)
        {
            if (module == null || model == null || !model.TryPlace(module, placement))
            {
                return false;
            }

            ApplyPose(module, placement);
            return true;
        }

        public bool Remove(CaravanModule module, out CaravanGridPlacement placement)
        {
            if (model == null)
            {
                placement = default;
                return false;
            }

            return model.Remove(module, out placement);
        }

        public bool CanPlace(CaravanModule module, CaravanGridPlacement placement)
        {
            return model != null && model.CanPlace(module, placement);
        }

        public bool TryPlace(CaravanModule module, CaravanGridPlacement placement)
        {
            if (module == null || model == null || !model.TryPlace(module, placement))
            {
                return false;
            }

            ApplyPose(module, placement);
            return true;
        }

        public CaravanGridPlacement PlacementFromWorldPoint(
            Vector3 worldPoint,
            CaravanModule module,
            int quarterTurns)
        {
            if (module == null)
            {
                throw new ArgumentNullException(nameof(module));
            }

            var local = transform.InverseTransformPoint(worldPoint);
            var rotated = new CaravanGridPlacement(
                0,
                0,
                module.FootprintWidth,
                module.FootprintLength,
                quarterTurns);
            var minimumX = -width * cellSize * 0.5f;
            var minimumZ = -length * cellSize * 0.5f;
            var centerCellX = Mathf.FloorToInt((local.x - minimumX) / cellSize);
            var centerCellZ = Mathf.FloorToInt((local.z - minimumZ) / cellSize);
            var x = centerCellX - rotated.RotatedWidth / 2;
            var z = centerCellZ - rotated.RotatedLength / 2;
            return new CaravanGridPlacement(
                x,
                z,
                module.FootprintWidth,
                module.FootprintLength,
                quarterTurns);
        }

        public void GetWorldPose(
            CaravanGridPlacement placement,
            out Vector3 position,
            out Quaternion rotation)
        {
            GetLocalPose(placement, out var localPosition, out var localRotation);
            position = transform.TransformPoint(localPosition);
            rotation = transform.rotation * localRotation;
        }

        private void ApplyPose(CaravanModule module, CaravanGridPlacement placement)
        {
            GetLocalPose(placement, out var localPosition, out var localRotation);
            module.transform.SetParent(transform, false);
            module.transform.SetLocalPositionAndRotation(localPosition, localRotation);
        }

        private void GetLocalPose(
            CaravanGridPlacement placement,
            out Vector3 position,
            out Quaternion rotation)
        {
            var minimumX = -width * cellSize * 0.5f;
            var minimumZ = -length * cellSize * 0.5f;
            position = new Vector3(
                minimumX + (placement.X + placement.RotatedWidth * 0.5f) * cellSize,
                deckHeight,
                minimumZ + (placement.Z + placement.RotatedLength * 0.5f) * cellSize);
            rotation = Quaternion.Euler(0f, placement.QuarterTurns * 90f, 0f);
        }
    }
}
