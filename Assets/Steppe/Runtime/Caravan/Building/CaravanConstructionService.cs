using System;
using UnityEngine;

namespace Steppe.Caravan
{
    [DisallowMultipleComponent]
    public sealed class CaravanConstructionService : MonoBehaviour
    {
        private static readonly CaravanPartKind[] AvailableDefinitions =
        {
            CaravanPartKind.Sail,
            CaravanPartKind.PhotovoltaicLeaves,
            CaravanPartKind.Battery,
            CaravanPartKind.WaterReservoir,
            CaravanPartKind.DualModePump,
            CaravanPartKind.Radiator,
            CaravanPartKind.Biofurnace,
            CaravanPartKind.BiofuelEngine,
            CaravanPartKind.ElectricMotor,
            CaravanPartKind.Harvester,
            CaravanPartKind.GrassDryer,
            CaravanPartKind.BiomassStorage,
            CaravanPartKind.Transmission,
            CaravanPartKind.CouplingRope
        };

        private CaravanPartPalette palette;
        private CaravanChassisController chassis;
        private CaravanElectricalNetwork electricalNetwork;
        private CaravanFluidNetwork fluidNetwork;
        private CaravanMaterialNetwork biomassNetwork;
        private CaravanMaterialNetwork mechanicalNetwork;
        private CaravanResourceSystem resourceSystem;
        private CaravanEnvironmentSampler environment;

        public static System.Collections.Generic.IReadOnlyList<CaravanPartKind>
            AvailablePartKinds => AvailableDefinitions;

        internal void Configure(
            CaravanPartPalette partPalette,
            CaravanChassisController caravan,
            CaravanElectricalNetwork electricity,
            CaravanFluidNetwork fluids,
            CaravanMaterialNetwork biomass,
            CaravanMaterialNetwork mechanical,
            CaravanResourceSystem resources)
        {
            palette = partPalette;
            chassis = caravan != null
                ? caravan
                : throw new ArgumentNullException(nameof(caravan));
            electricalNetwork = electricity != null
                ? electricity
                : throw new ArgumentNullException(nameof(electricity));
            fluidNetwork = fluids != null
                ? fluids
                : throw new ArgumentNullException(nameof(fluids));
            biomassNetwork = biomass != null
                ? biomass
                : throw new ArgumentNullException(nameof(biomass));
            mechanicalNetwork = mechanical != null
                ? mechanical
                : throw new ArgumentNullException(nameof(mechanical));
            resourceSystem = resources != null
                ? resources
                : throw new ArgumentNullException(nameof(resources));
        }

        public void SetEnvironment(CaravanEnvironmentSampler sampler)
        {
            environment = sampler;
        }

        public bool CanConstruct(CaravanPartKind kind)
        {
            for (var index = 0; index < AvailableDefinitions.Length; index++)
            {
                if (AvailableDefinitions[index] == kind)
                {
                    return true;
                }
            }

            return false;
        }

        public bool TryCreateBuffered(
            CaravanPartKind kind,
            out CaravanModule module)
        {
            module = null;
            if (!CanConstruct(kind)
                || chassis == null
                || electricalNetwork == null
                || fluidNetwork == null
                || biomassNetwork == null
                || mechanicalNetwork == null
                || resourceSystem == null)
            {
                return false;
            }

            module = CaravanGreyboxPartFactory.CreateUnplaced(palette, kind);
            module.transform.SetParent(chassis.transform, false);
            module.gameObject.SetActive(false);
            return true;
        }

        public void ActivatePlaced(CaravanModule module)
        {
            if (module == null)
            {
                return;
            }

            electricalNetwork.RegisterModule(module);
            fluidNetwork.RegisterModule(module);
            biomassNetwork.RegisterModule(module);
            mechanicalNetwork.RegisterModule(module);
            resourceSystem.RegisterModule(module);
            if (environment != null
                && module.TryGetComponent<CaravanPhotovoltaicModule>(
                    out var photovoltaic))
            {
                photovoltaic.Configure(environment);
            }
            ConfigureSail(module);
            ConfigureControl(module);

            chassis.RefreshMassProperties();
            chassis.NotifyStructureCollidersChanged();
        }

        public void DestroyBuffered(CaravanModule module)
        {
            if (module != null)
            {
                Destroy(module.gameObject);
            }
        }

        public bool TryDestroyPlaced(CaravanModule module, CaravanMountGrid grid)
        {
            if (module == null
                || grid == null
                || !module.IsMovable
                || !grid.Remove(module, out _))
            {
                return false;
            }

            var electricalPorts = module.GetComponentsInChildren<
                CaravanElectricalPort>(true);
            for (var index = 0; index < electricalPorts.Length; index++)
            {
                electricalNetwork.UnregisterPort(electricalPorts[index]);
            }

            var fluidPorts = module.GetComponentsInChildren<CaravanFluidPort>(true);
            for (var index = 0; index < fluidPorts.Length; index++)
            {
                fluidNetwork.UnregisterPort(fluidPorts[index]);
            }

            var materialPorts = module.GetComponentsInChildren<
                CaravanMaterialPort>(true);
            for (var index = 0; index < materialPorts.Length; index++)
            {
                biomassNetwork.UnregisterPort(materialPorts[index]);
                mechanicalNetwork.UnregisterPort(materialPorts[index]);
            }

            resourceSystem.UnregisterModule(module);
            module.gameObject.SetActive(false);
            module.transform.SetParent(null, true);
            chassis.NotifyStructureCollidersChanged();
            Destroy(module.gameObject);
            chassis.RefreshMassProperties();
            return true;
        }

        private void ConfigureSail(CaravanModule module)
        {
            if (environment == null
                || !module.TryGetComponent<CaravanSailModule>(out var sail))
            {
                return;
            }

            var pivot = module.VisualRoot.Find("Sail Pivot");
            var cloth = pivot != null ? pivot.Find("Sail Cloth") : null;
            sail.Configure(
                chassis.Body,
                environment,
                pivot,
                cloth,
                CaravanPartCatalog.Get(CaravanPartKind.Sail).Capacity,
                9500f);
            var vane = module.GetComponent<CaravanWindVane>();
            var vanePivot = module.VisualRoot.Find("Wind Vane/Vane Pivot");
            if (vane != null && vanePivot != null)
            {
                vane.Configure(environment, vanePivot);
            }
        }

        private void ConfigureControl(CaravanModule module)
        {
            var station = module.GetComponent<CaravanControlStation>();
            if (station == null)
            {
                return;
            }

            var part = module.GetComponent<CaravanPart>();
            if (part == null)
            {
                return;
            }

            if (part.Kind == CaravanPartKind.ElectricMotor)
            {
                station.ConfigureElectricThrottle(
                    chassis,
                    module.VisualRoot.Find(
                        "Electric Throttle/Control Visual"),
                    module.VisualRoot.Find(
                        "Electric Throttle/Focus Indicator")?.gameObject);
                return;
            }
            if (part.Kind == CaravanPartKind.Sail
                && module.TryGetComponent<CaravanSailModule>(out var sail))
            {
                station.ConfigureSail(
                    sail,
                    module.VisualRoot.Find("Module Control/Control Visual"),
                    module.VisualRoot.Find(
                        "Module Control/Focus Indicator")?.gameObject);
                return;
            }

            var controlTarget = FindControlTarget(module);
            if (controlTarget == null
                || !TryGetControlKind(part.Kind, out var controlKind))
            {
                return;
            }

            station.ConfigureModule(
                controlKind,
                controlTarget,
                module.VisualRoot.Find("Module Control/Control Visual"),
                module.VisualRoot.Find(
                    "Module Control/Focus Indicator")?.gameObject);
        }

        private static ICaravanControlTarget FindControlTarget(
            CaravanModule module)
        {
            var behaviours = module.GetComponents<MonoBehaviour>();
            for (var index = 0; index < behaviours.Length; index++)
            {
                if (behaviours[index] is ICaravanControlTarget target)
                {
                    return target;
                }
            }
            return null;
        }

        private static bool TryGetControlKind(
            CaravanPartKind partKind,
            out CaravanControlKind controlKind)
        {
            controlKind = partKind switch
            {
                CaravanPartKind.PhotovoltaicLeaves =>
                    CaravanControlKind.SolarOrientation,
                CaravanPartKind.DualModePump =>
                    CaravanControlKind.PumpMode,
                CaravanPartKind.Radiator =>
                    CaravanControlKind.RadiatorOpening,
                CaravanPartKind.Biofurnace =>
                    CaravanControlKind.FurnaceIntensity,
                CaravanPartKind.BiofuelEngine =>
                    CaravanControlKind.BiofuelThrottle,
                CaravanPartKind.Harvester =>
                    CaravanControlKind.HarvesterPower,
                CaravanPartKind.GrassDryer =>
                    CaravanControlKind.DryerPower,
                CaravanPartKind.Transmission =>
                    CaravanControlKind.TransmissionRatio,
                _ => default
            };
            return partKind == CaravanPartKind.PhotovoltaicLeaves
                   || partKind == CaravanPartKind.DualModePump
                   || partKind == CaravanPartKind.Radiator
                   || partKind == CaravanPartKind.Biofurnace
                   || partKind == CaravanPartKind.BiofuelEngine
                   || partKind == CaravanPartKind.Harvester
                   || partKind == CaravanPartKind.GrassDryer
                   || partKind == CaravanPartKind.Transmission;
        }
    }
}
