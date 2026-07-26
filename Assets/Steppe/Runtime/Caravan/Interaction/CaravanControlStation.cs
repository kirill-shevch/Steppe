using System;
using UnityEngine;

namespace Steppe.Caravan
{
    public enum CaravanControlKind
    {
        Steering,
        SailTrim
    }

    [DisallowMultipleComponent]
    public sealed class CaravanControlStation : MonoBehaviour
    {
        private CaravanControlKind kind;
        private CaravanChassisController chassis;
        private CaravanSailModule sail;
        private Transform controlVisual;
        private GameObject focusIndicator;
        private Vector3 indicatorBaseScale;
        private float normalizedValue;
        private bool focused;
        private bool engaged;

        public CaravanControlKind Kind => kind;
        public float NormalizedValue => normalizedValue;

        public void ConfigureSteering(
            CaravanChassisController controller,
            Transform visual,
            GameObject indicator = null)
        {
            chassis = controller != null ? controller : throw new ArgumentNullException(nameof(controller));
            controlVisual = visual;
            focusIndicator = indicator;
            indicatorBaseScale = indicator != null ? indicator.transform.localScale : Vector3.one;
            kind = CaravanControlKind.Steering;
            SetFocused(false);
            SetNormalized(0f);
        }

        public void ConfigureSail(
            CaravanSailModule sailModule,
            Transform visual,
            GameObject indicator = null)
        {
            sail = sailModule != null ? sailModule : throw new ArgumentNullException(nameof(sailModule));
            controlVisual = visual;
            focusIndicator = indicator;
            indicatorBaseScale = indicator != null ? indicator.transform.localScale : Vector3.one;
            kind = CaravanControlKind.SailTrim;
            SetFocused(false);
            SetNormalized(sail.NormalizedTrim);
        }

        public void SetFocused(bool focused)
        {
            this.focused = focused;
            RefreshIndicator();
        }

        public void SetEngaged(bool engaged)
        {
            this.engaged = engaged;
            RefreshIndicator();
        }

        private void RefreshIndicator()
        {
            if (focusIndicator != null)
            {
                focusIndicator.SetActive(focused || engaged);
                focusIndicator.transform.localScale = engaged
                    ? indicatorBaseScale * 1.35f
                    : indicatorBaseScale;
            }
        }

        public void Adjust(float delta)
        {
            SetNormalized(normalizedValue + delta);
        }

        public void SetNormalized(float value)
        {
            normalizedValue = Mathf.Clamp(value, -1f, 1f);
            if (kind == CaravanControlKind.Steering)
            {
                chassis?.SetSteeringNormalized(normalizedValue);
                if (controlVisual != null)
                {
                    controlVisual.localRotation = Quaternion.Euler(0f, 0f, -normalizedValue * 125f);
                }
            }
            else
            {
                sail?.SetTrimNormalized(normalizedValue);
                if (controlVisual != null)
                {
                    controlVisual.localRotation = Quaternion.Euler(0f, normalizedValue * 160f, 0f);
                }
            }
        }
    }
}
