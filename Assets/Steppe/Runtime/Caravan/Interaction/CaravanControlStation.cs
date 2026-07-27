using System;
using UnityEngine;

namespace Steppe.Caravan
{
    public enum CaravanControlKind
    {
        Steering,
        SailTrim,
        ElectricThrottle,
        SolarOrientation,
        PumpMode,
        RadiatorOpening,
        FurnaceIntensity,
        BiofuelThrottle,
        HarvesterPower,
        DryerPower,
        TransmissionRatio
    }

    [DisallowMultipleComponent]
    public sealed class CaravanControlStation : MonoBehaviour
    {
        private CaravanControlKind kind;
        private CaravanChassisController chassis;
        private CaravanSailModule sail;
        private ICaravanControlTarget moduleTarget;
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

        public void ConfigureElectricThrottle(
            CaravanChassisController controller,
            Transform visual,
            GameObject indicator = null)
        {
            chassis = controller != null ? controller : throw new ArgumentNullException(nameof(controller));
            controlVisual = visual;
            focusIndicator = indicator;
            indicatorBaseScale = indicator != null ? indicator.transform.localScale : Vector3.one;
            kind = CaravanControlKind.ElectricThrottle;
            SetFocused(false);
            SetNormalized(-1f);
        }

        public void ConfigureModule(
            CaravanControlKind controlKind,
            ICaravanControlTarget target,
            Transform visual,
            GameObject indicator = null)
        {
            if (controlKind <= CaravanControlKind.ElectricThrottle)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(controlKind),
                    controlKind,
                    "Use a specialized control configuration for chassis, sail or electric throttle.");
            }

            moduleTarget = target
                           ?? throw new ArgumentNullException(nameof(target));
            controlVisual = visual;
            focusIndicator = indicator;
            indicatorBaseScale = indicator != null
                ? indicator.transform.localScale
                : Vector3.one;
            kind = controlKind;
            SetFocused(false);
            SetNormalized(moduleTarget.ControlNormalized);
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
            switch (kind)
            {
                case CaravanControlKind.Steering:
                    chassis?.SetSteeringNormalized(normalizedValue);
                    if (controlVisual != null)
                    {
                        controlVisual.localRotation =
                            Quaternion.Euler(0f, 0f, -normalizedValue * 125f);
                    }
                    break;
                case CaravanControlKind.SailTrim:
                    sail?.SetTrimNormalized(normalizedValue);
                    if (controlVisual != null)
                    {
                        controlVisual.localRotation =
                            Quaternion.Euler(0f, normalizedValue * 160f, 0f);
                    }
                    break;
                case CaravanControlKind.ElectricThrottle:
                    chassis?.SetElectricDriveThrottle((normalizedValue + 1f) * 0.5f);
                    if (controlVisual != null)
                    {
                        controlVisual.localRotation =
                            Quaternion.Euler(normalizedValue * 42f, 0f, 0f);
                    }
                    break;
                case CaravanControlKind.SolarOrientation:
                case CaravanControlKind.PumpMode:
                case CaravanControlKind.RadiatorOpening:
                case CaravanControlKind.FurnaceIntensity:
                case CaravanControlKind.BiofuelThrottle:
                case CaravanControlKind.HarvesterPower:
                case CaravanControlKind.DryerPower:
                case CaravanControlKind.TransmissionRatio:
                    moduleTarget?.SetControlNormalized(normalizedValue);
                    if (controlVisual != null)
                    {
                        controlVisual.localRotation =
                            Quaternion.Euler(normalizedValue * 42f, 0f, 0f);
                    }
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }
}
