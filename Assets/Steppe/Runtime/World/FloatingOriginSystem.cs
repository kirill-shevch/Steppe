using System;
using UnityEngine;

namespace Steppe.World
{
    public sealed class FloatingOriginSystem : MonoBehaviour
    {
        private Transform focus;
        private Transform worldSpaceRoot;
        private float threshold;
        private float shiftQuantum;
        private const double ShaderCoordinatePeriod = 65536.0;

        public double OriginX { get; private set; }
        public double OriginZ { get; private set; }

        public event Action<Vector3> Shifted;

        public void Configure(Transform focusTransform, Transform root, float shiftThreshold, float quantum)
        {
            focus = focusTransform != null ? focusTransform : throw new ArgumentNullException(nameof(focusTransform));
            worldSpaceRoot = root != null ? root : throw new ArgumentNullException(nameof(root));
            threshold = Mathf.Max(128f, shiftThreshold);
            shiftQuantum = Mathf.Max(1f, quantum);
            PublishShaderOrigin();
        }

        public WorldPosition LocalToWorld(Vector3 localPosition)
        {
            return new WorldPosition(
                OriginX + localPosition.x,
                localPosition.y,
                OriginZ + localPosition.z);
        }

        public Vector3 WorldToLocal(double worldX, double worldY, double worldZ)
        {
            return new Vector3(
                (float)(worldX - OriginX),
                (float)worldY,
                (float)(worldZ - OriginZ));
        }

        private void LateUpdate()
        {
            if (focus == null || worldSpaceRoot == null)
            {
                return;
            }

            var localPosition = focus.position;
            if (Mathf.Abs(localPosition.x) < threshold && Mathf.Abs(localPosition.z) < threshold)
            {
                return;
            }

            var shiftX = Mathf.Round(localPosition.x / shiftQuantum) * shiftQuantum;
            var shiftZ = Mathf.Round(localPosition.z / shiftQuantum) * shiftQuantum;
            var shift = new Vector3(shiftX, 0f, shiftZ);

            if (shift.sqrMagnitude < 1f)
            {
                return;
            }

            focus.position -= shift;
            for (var index = 0; index < worldSpaceRoot.childCount; index++)
            {
                worldSpaceRoot.GetChild(index).position -= shift;
            }

            OriginX += shift.x;
            OriginZ += shift.z;
            PublishShaderOrigin();
            Shifted?.Invoke(shift);
        }

        private void PublishShaderOrigin()
        {
            Shader.SetGlobalVector("_SteppeWorldOriginXZ", new Vector4(
                PositiveModulo(OriginX, ShaderCoordinatePeriod),
                0f,
                PositiveModulo(OriginZ, ShaderCoordinatePeriod),
                0f));
        }

        private static float PositiveModulo(double value, double modulus)
        {
            return (float)(value - Math.Floor(value / modulus) * modulus);
        }
    }
}
