using System;
using System.Collections.Generic;
using UnityEngine;

namespace Steppe.Terrain
{
    [Serializable]
    public sealed class TerrainOverrideStamp
    {
        [SerializeField] private double centerX;
        [SerializeField] private double centerZ;
        [SerializeField, Min(1f)] private double radius = 1000.0;
        [SerializeField] private double heightOffset;
        [SerializeField, Range(0f, 1f)] private float flattenStrength;
        [SerializeField] private double targetHeight;

        public TerrainOverrideStamp()
        {
        }

        public TerrainOverrideStamp(
            double centerX,
            double centerZ,
            double radius,
            double heightOffset,
            float flattenStrength = 0f,
            double targetHeight = 0.0)
        {
            this.centerX = centerX;
            this.centerZ = centerZ;
            this.radius = Math.Max(1.0, radius);
            this.heightOffset = heightOffset;
            this.flattenStrength = Mathf.Clamp01(flattenStrength);
            this.targetHeight = targetHeight;
        }

        public double Apply(double worldX, double worldZ, double currentHeight)
        {
            var deltaX = worldX - centerX;
            var deltaZ = worldZ - centerZ;
            var distanceSquared = deltaX * deltaX + deltaZ * deltaZ;
            var safeRadius = Math.Max(1.0, radius);

            if (distanceSquared >= safeRadius * safeRadius)
            {
                return currentHeight;
            }

            var normalizedDistance = Math.Sqrt(distanceSquared) / safeRadius;
            var influence = 1.0 - normalizedDistance;
            influence = influence * influence * (3.0 - 2.0 * influence);

            var shiftedHeight = currentHeight + heightOffset * influence;
            var flatten = Mathf.Clamp01(flattenStrength) * influence;
            return shiftedHeight + (targetHeight - shiftedHeight) * flatten;
        }
    }

    [CreateAssetMenu(fileName = "TerrainOverrideMap", menuName = "Steppe/Terrain Override Map")]
    public sealed class TerrainOverrideMap : ScriptableObject
    {
        [SerializeField] private List<TerrainOverrideStamp> stamps = new List<TerrainOverrideStamp>();

        public IReadOnlyList<TerrainOverrideStamp> Stamps => stamps;

        public double Apply(double worldX, double worldZ, double currentHeight)
        {
            var result = currentHeight;
            for (var index = 0; index < stamps.Count; index++)
            {
                result = stamps[index].Apply(worldX, worldZ, result);
            }

            return result;
        }

        public void AddStamp(TerrainOverrideStamp stamp)
        {
            if (stamp == null)
            {
                throw new ArgumentNullException(nameof(stamp));
            }

            stamps.Add(stamp);
        }
    }
}
