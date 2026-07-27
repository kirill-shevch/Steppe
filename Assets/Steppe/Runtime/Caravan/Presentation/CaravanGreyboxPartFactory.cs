using System;
using System.Collections.Generic;
using UnityEngine;

namespace Steppe.Caravan
{
    internal readonly struct CaravanPartPalette
    {
        public CaravanPartPalette(
            Material metal,
            Material darkMetal,
            Material copper,
            Material solar,
            Material water,
            Material biomass,
            Material hot,
            Material dust,
            Material condition,
            Material load)
        {
            Metal = metal;
            DarkMetal = darkMetal;
            Copper = copper;
            Solar = solar;
            Water = water;
            Biomass = biomass;
            Hot = hot;
            Dust = dust;
            Condition = condition;
            Load = load;
        }

        public Material Metal { get; }
        public Material DarkMetal { get; }
        public Material Copper { get; }
        public Material Solar { get; }
        public Material Water { get; }
        public Material Biomass { get; }
        public Material Hot { get; }
        public Material Dust { get; }
        public Material Condition { get; }
        public Material Load { get; }
    }

    internal static class CaravanGreyboxPartFactory
    {
        public static IReadOnlyList<CaravanModule> CreateInitialParts(
            CaravanMountGrid grid,
            CaravanPartPalette palette)
        {
            var modules = new List<CaravanModule>
            {
                Create(
                    grid,
                    palette,
                    CaravanPartKind.Battery,
                    new CaravanGridPlacement(0, 4, 2, 2, 0),
                    "starter-battery"),
                Create(
                    grid,
                    palette,
                    CaravanPartKind.ElectricMotor,
                    new CaravanGridPlacement(2, 4, 2, 2, 0),
                    "starter-electric-motor"),
                Create(
                    grid,
                    palette,
                    CaravanPartKind.PhotovoltaicLeaves,
                    new CaravanGridPlacement(0, 8, 3, 3, 0),
                    "starter-photovoltaic-leaves")
            };

            return modules;
        }

        private static CaravanModule Create(
            CaravanMountGrid grid,
            CaravanPartPalette palette,
            CaravanPartKind kind,
            CaravanGridPlacement placement,
            string instanceId = null)
        {
            var module = CreateUnplaced(palette, kind, instanceId);
            if (!grid.Register(module, placement))
            {
                UnityEngine.Object.Destroy(module.gameObject);
                throw new InvalidOperationException(
                    $"Could not place initial caravan part '{module.name}'.");
            }

            return module;
        }

        internal static CaravanModule CreateUnplaced(
            CaravanPartPalette palette,
            CaravanPartKind kind,
            string instanceId = null)
        {
            var spec = CaravanPartCatalog.Get(kind);
            var root = new GameObject(spec.DisplayName);
            var visual = new GameObject("Visual");
            visual.transform.SetParent(root.transform, false);
            BuildVisual(kind, visual.transform, palette);

            var collider = root.AddComponent<BoxCollider>();
            collider.center = new Vector3(0f, 0.72f, 0f);
            collider.size = new Vector3(
                Mathf.Max(0.72f, spec.FootprintWidth * 0.82f),
                1.45f,
                Mathf.Max(0.72f, spec.FootprintLength * 0.82f));

            var display = CreateStatusDisplay(
                visual.transform,
                new Vector3(
                    -spec.FootprintWidth * 0.24f,
                    0.72f,
                    -spec.FootprintLength * 0.34f),
                Quaternion.Euler(12f, 0f, 0f),
                palette);
            var module = root.AddComponent<CaravanModule>();
            module.Configure(
                spec.Id,
                visual.transform,
                true,
                spec.FootprintWidth,
                spec.FootprintLength,
                display,
                spec.MassKilograms,
                new Vector3(0f, 0.62f, 0f),
                instanceId);
            module.State.SetForTests(0.08f, 0.96f, 0f);
            var part = root.AddComponent<CaravanPart>();
            var initialStoredAmount = kind switch
            {
                CaravanPartKind.Battery => spec.Capacity * 0.35f,
                CaravanPartKind.WaterReservoir => spec.Capacity * 0.35f,
                _ => 0f
            };
            part.Configure(kind, spec.Capacity, initialStoredAmount);
            switch (kind)
            {
                case CaravanPartKind.Sail:
                    root.AddComponent<CaravanSailModule>();
                    root.AddComponent<CaravanWindVane>();
                    CreateFluidTap(root.transform, part, palette);
                    CreateModuleControl(root.transform, visual.transform, palette);
                    break;
                case CaravanPartKind.PhotovoltaicLeaves:
                    root.AddComponent<CaravanPhotovoltaicModule>();
                    CreateElectricalPort(root.transform, part, kind, palette);
                    CreateFluidTap(root.transform, part, palette);
                    CreateModuleControl(root.transform, visual.transform, palette);
                    break;
                case CaravanPartKind.Battery:
                    var battery = root.AddComponent<CaravanBatteryModule>();
                    battery.Configure(visual.transform.Find("Charge Window"));
                    CreateElectricalPort(root.transform, part, kind, palette);
                    break;
                case CaravanPartKind.WaterReservoir:
                    root.AddComponent<CaravanWaterReservoirModule>().Configure();
                    CreateFluidPorts(root.transform, part, kind, palette);
                    break;
                case CaravanPartKind.DualModePump:
                    root.AddComponent<CaravanElectricPumpModule>().Configure();
                    CreateElectricalPort(root.transform, part, kind, palette);
                    CreateFluidPorts(root.transform, part, kind, palette);
                    CreateMaterialPorts(root.transform, part, kind, palette);
                    CreateModuleControl(root.transform, visual.transform, palette);
                    break;
                case CaravanPartKind.Radiator:
                    root.AddComponent<CaravanRadiatorModule>().Configure();
                    CreateFluidPorts(root.transform, part, kind, palette);
                    CreateModuleControl(root.transform, visual.transform, palette);
                    break;
                case CaravanPartKind.Biofurnace:
                    root.AddComponent<CaravanBiofurnaceModule>().Configure();
                    CreateFluidTap(root.transform, part, palette);
                    CreateMaterialPorts(root.transform, part, kind, palette);
                    CreateModuleControl(root.transform, visual.transform, palette);
                    break;
                case CaravanPartKind.BiofuelEngine:
                    root.AddComponent<CaravanBiofuelEngineModule>().Configure();
                    CreateFluidTap(root.transform, part, palette);
                    CreateMaterialPorts(root.transform, part, kind, palette);
                    CreateModuleControl(root.transform, visual.transform, palette);
                    break;
                case CaravanPartKind.ElectricMotor:
                    var motor = root.AddComponent<CaravanElectricMotorModule>();
                    motor.Configure(visual.transform.Find("Motor Shaft"));
                    root.AddComponent<CaravanControlStation>();
                    CreateElectricalPort(root.transform, part, kind, palette);
                    CreateMaterialPorts(root.transform, part, kind, palette);
                    break;
                case CaravanPartKind.Harvester:
                    root.AddComponent<CaravanHarvesterModule>().Configure();
                    CreateElectricalPort(root.transform, part, kind, palette);
                    CreateMaterialPorts(root.transform, part, kind, palette);
                    CreateModuleControl(root.transform, visual.transform, palette);
                    break;
                case CaravanPartKind.GrassDryer:
                    root.AddComponent<CaravanGrassDryerModule>().Configure();
                    CreateElectricalPort(root.transform, part, kind, palette);
                    CreateMaterialPorts(root.transform, part, kind, palette);
                    CreateModuleControl(root.transform, visual.transform, palette);
                    break;
                case CaravanPartKind.BiomassStorage:
                    root.AddComponent<CaravanBiomassStorageModule>().Configure();
                    CreateMaterialPorts(root.transform, part, kind, palette);
                    break;
                case CaravanPartKind.Transmission:
                    root.AddComponent<CaravanTransmissionModule>().Configure();
                    CreateMaterialPorts(root.transform, part, kind, palette);
                    CreateModuleControl(root.transform, visual.transform, palette);
                    break;
                case CaravanPartKind.CouplingRope:
                    root.AddComponent<CaravanCouplingRopeModule>().Configure();
                    CreateMaterialPorts(root.transform, part, kind, palette);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(kind), kind, null);
            }

            module.RefreshRendererCache();
            return module;
        }

        private static void BuildVisual(
            CaravanPartKind kind,
            Transform root,
            CaravanPartPalette palette)
        {
            switch (kind)
            {
                case CaravanPartKind.Sail:
                    BuildSail(root, palette);
                    break;
                case CaravanPartKind.PhotovoltaicLeaves:
                    BuildPhotovoltaicLeaves(root, palette);
                    break;
                case CaravanPartKind.Battery:
                    BuildBattery(root, palette);
                    break;
                case CaravanPartKind.WaterReservoir:
                    BuildWaterReservoir(root, palette);
                    break;
                case CaravanPartKind.DualModePump:
                    BuildPump(root, palette);
                    break;
                case CaravanPartKind.Radiator:
                    BuildRadiator(root, palette);
                    break;
                case CaravanPartKind.Biofurnace:
                    BuildBiofurnace(root, palette);
                    break;
                case CaravanPartKind.BiofuelEngine:
                    BuildBiofuelEngine(root, palette);
                    break;
                case CaravanPartKind.ElectricMotor:
                    BuildElectricMotor(root, palette);
                    break;
                case CaravanPartKind.Harvester:
                    BuildHarvester(root, palette);
                    break;
                case CaravanPartKind.GrassDryer:
                    BuildDryer(root, palette);
                    break;
                case CaravanPartKind.BiomassStorage:
                    BuildBiomassStorage(root, palette);
                    break;
                case CaravanPartKind.Transmission:
                    BuildTransmission(root, palette);
                    break;
                case CaravanPartKind.CouplingRope:
                    BuildCouplingRope(root, palette);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(kind), kind, null);
            }
        }

        private static void BuildSail(Transform root, CaravanPartPalette p)
        {
            Primitive(
                "Mast",
                PrimitiveType.Cylinder,
                root,
                new Vector3(-0.72f, 2.15f, 0f),
                new Vector3(0.1f, 2.28f, 0.1f),
                p.DarkMetal);
            var pivot = new GameObject("Sail Pivot");
            pivot.transform.SetParent(root, false);
            pivot.transform.localPosition = new Vector3(-0.66f, 2.2f, 0f);
            Primitive(
                "Top Spar",
                PrimitiveType.Cube,
                pivot.transform,
                new Vector3(1.32f, 1.75f, 0f),
                new Vector3(2.82f, 0.08f, 0.08f),
                p.DarkMetal);
            Primitive(
                "Bottom Spar",
                PrimitiveType.Cube,
                pivot.transform,
                new Vector3(1.32f, -1.65f, 0f),
                new Vector3(2.82f, 0.08f, 0.08f),
                p.DarkMetal);
            Primitive(
                "Sail Cloth",
                PrimitiveType.Cube,
                pivot.transform,
                new Vector3(1.32f, 0.05f, 0f),
                new Vector3(2.55f, 3.25f, 0.04f),
                p.Solar);

            var vane = new GameObject("Wind Vane");
            vane.transform.SetParent(root, false);
            vane.transform.localPosition = new Vector3(-0.72f, 4.58f, 0f);
            var vanePivot = new GameObject("Vane Pivot");
            vanePivot.transform.SetParent(vane.transform, false);
            Primitive(
                "Vane Arrow",
                PrimitiveType.Cube,
                vanePivot.transform,
                new Vector3(0f, 0f, 0.4f),
                new Vector3(0.08f, 0.08f, 1.15f),
                p.Copper);
            Primitive(
                "Vane Tail",
                PrimitiveType.Cube,
                vanePivot.transform,
                new Vector3(0f, 0.18f, -0.48f),
                new Vector3(0.5f, 0.42f, 0.06f),
                p.Solar);
        }

        private static void BuildPhotovoltaicLeaves(Transform root, CaravanPartPalette p)
        {
            Primitive("Stem", PrimitiveType.Cylinder, root, new Vector3(0f, 0.72f, 0f),
                new Vector3(0.1f, 0.72f, 0.1f), p.DarkMetal);
            for (var index = 0; index < 4; index++)
            {
                var angle = index * 90f + 45f;
                var radians = angle * Mathf.Deg2Rad;
                var leaf = Primitive(
                    $"Solar Leaf {index + 1}",
                    PrimitiveType.Cube,
                    root,
                    new Vector3(Mathf.Sin(radians) * 0.76f, 1.35f, Mathf.Cos(radians) * 0.76f),
                    new Vector3(1.25f, 0.07f, 0.72f),
                    p.Solar);
                leaf.transform.localRotation = Quaternion.Euler(12f, angle, 0f);
                Primitive(
                    $"Leaf Hinge {index + 1}",
                    PrimitiveType.Cylinder,
                    root,
                    new Vector3(Mathf.Sin(radians) * 0.26f, 1.16f, Mathf.Cos(radians) * 0.26f),
                    new Vector3(0.1f, 0.24f, 0.1f),
                    p.Copper).transform.localRotation = Quaternion.Euler(90f, angle, 0f);
            }
        }

        private static void BuildBattery(Transform root, CaravanPartPalette p)
        {
            Primitive("Battery Case", PrimitiveType.Cube, root, new Vector3(0f, 0.48f, 0f),
                new Vector3(1.55f, 0.88f, 1.45f), p.DarkMetal);
            for (var index = 0; index < 4; index++)
            {
                Primitive($"Cell {index + 1}", PrimitiveType.Cylinder, root,
                    new Vector3(-0.52f + index * 0.35f, 1.03f, 0f),
                    new Vector3(0.13f, 0.25f, 0.13f), p.Copper);
            }
            Primitive("Charge Window", PrimitiveType.Cube, root, new Vector3(0f, 0.54f, -0.76f),
                new Vector3(0.7f, 0.22f, 0.05f), p.Solar);
        }

        private static void BuildWaterReservoir(Transform root, CaravanPartPalette p)
        {
            var tank = Primitive("Water Tank", PrimitiveType.Cylinder, root, new Vector3(0f, 0.82f, 0f),
                new Vector3(0.76f, 1.12f, 0.76f), p.Water);
            tank.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            foreach (var z in new[] { -0.7f, 0.7f })
            {
                Primitive("Tank Strap", PrimitiveType.Cylinder, root, new Vector3(0f, 0.82f, z),
                    new Vector3(0.82f, 0.07f, 0.82f), p.DarkMetal)
                    .transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            }
            Primitive("Fill Neck", PrimitiveType.Cylinder, root, new Vector3(0f, 1.62f, -0.55f),
                new Vector3(0.16f, 0.18f, 0.16f), p.Copper);
        }

        private static void BuildPump(Transform root, CaravanPartPalette p)
        {
            Primitive("Pump Body", PrimitiveType.Cylinder, root, new Vector3(0f, 0.52f, 0f),
                new Vector3(0.38f, 0.48f, 0.38f), p.Metal);
            Primitive("Pump Motor", PrimitiveType.Cylinder, root, new Vector3(0f, 1.12f, 0f),
                new Vector3(0.26f, 0.36f, 0.26f), p.Copper);
            Primitive("Surface Intake", PrimitiveType.Cylinder, root, new Vector3(-0.38f, 0.28f, 0f),
                new Vector3(0.11f, 0.42f, 0.11f), p.Water)
                .transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            Primitive("Circuit Outlet", PrimitiveType.Cylinder, root, new Vector3(0.38f, 0.74f, 0f),
                new Vector3(0.11f, 0.42f, 0.11f), p.Water)
                .transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
        }

        private static void BuildRadiator(Transform root, CaravanPartPalette p)
        {
            Primitive("Radiator Frame", PrimitiveType.Cube, root, new Vector3(0f, 0.9f, 0f),
                new Vector3(1.6f, 1.55f, 0.22f), p.DarkMetal);
            for (var index = 0; index < 11; index++)
            {
                Primitive($"Cooling Fin {index + 1}", PrimitiveType.Cube, root,
                    new Vector3(-0.7f + index * 0.14f, 0.9f, -0.15f),
                    new Vector3(0.055f, 1.35f, 0.12f), p.Copper);
            }
            Primitive("Upper Water Pipe", PrimitiveType.Cylinder, root, new Vector3(0f, 1.58f, 0f),
                new Vector3(0.09f, 0.72f, 0.09f), p.Water)
                .transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
        }

        private static void BuildBiofurnace(Transform root, CaravanPartPalette p)
        {
            Primitive("Furnace Body", PrimitiveType.Cylinder, root, new Vector3(0f, 0.7f, 0f),
                new Vector3(0.68f, 0.7f, 0.68f), p.DarkMetal);
            Primitive("Fire Door", PrimitiveType.Cube, root, new Vector3(0f, 0.55f, -0.68f),
                new Vector3(0.64f, 0.52f, 0.08f), p.Hot);
            Primitive("Chimney", PrimitiveType.Cylinder, root, new Vector3(0.35f, 1.65f, 0.15f),
                new Vector3(0.16f, 0.72f, 0.16f), p.Copper);
            Primitive("Hot Water Coil", PrimitiveType.Cylinder, root, new Vector3(-0.52f, 0.92f, 0f),
                new Vector3(0.12f, 0.6f, 0.12f), p.Water);
        }

        private static void BuildBiofuelEngine(Transform root, CaravanPartPalette p)
        {
            Primitive("Engine Block", PrimitiveType.Cube, root, new Vector3(0f, 0.58f, 0f),
                new Vector3(1.5f, 0.95f, 1.35f), p.Metal);
            for (var index = 0; index < 3; index++)
            {
                Primitive($"Combustion Pod {index + 1}", PrimitiveType.Cylinder, root,
                    new Vector3(-0.5f + index * 0.5f, 1.22f, 0f),
                    new Vector3(0.2f, 0.38f, 0.2f), p.Hot);
            }
            Primitive("Exhaust", PrimitiveType.Cylinder, root, new Vector3(0.58f, 1.55f, 0.3f),
                new Vector3(0.12f, 0.72f, 0.12f), p.DarkMetal);
        }

        private static void BuildElectricMotor(Transform root, CaravanPartPalette p)
        {
            var motor = Primitive("Motor Body", PrimitiveType.Cylinder, root, new Vector3(0f, 0.7f, 0f),
                new Vector3(0.64f, 0.72f, 0.64f), p.Copper);
            motor.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            Primitive("Motor Shaft", PrimitiveType.Cylinder, root, new Vector3(0.82f, 0.7f, 0f),
                new Vector3(0.18f, 0.48f, 0.18f), p.DarkMetal)
                .transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            for (var index = 0; index < 7; index++)
            {
                Primitive($"Motor Fin {index + 1}", PrimitiveType.Cube, root,
                    new Vector3(-0.48f + index * 0.16f, 0.7f, 0f),
                    new Vector3(0.05f, 1.35f, 0.82f), p.DarkMetal);
            }

            var throttle = new GameObject("Electric Throttle");
            throttle.transform.SetParent(root, false);
            throttle.transform.localPosition = new Vector3(0.1f, 1.52f, -0.54f);
            Primitive("Throttle Base", PrimitiveType.Cylinder, throttle.transform,
                Vector3.zero, new Vector3(0.18f, 0.1f, 0.18f), p.DarkMetal);
            var controlVisual = new GameObject("Control Visual");
            controlVisual.transform.SetParent(throttle.transform, false);
            Primitive("Lever", PrimitiveType.Cube, controlVisual.transform,
                new Vector3(0f, 0.28f, 0f), new Vector3(0.08f, 0.54f, 0.08f), p.Copper);
            Primitive("Grip", PrimitiveType.Sphere, controlVisual.transform,
                new Vector3(0f, 0.58f, 0f), new Vector3(0.16f, 0.16f, 0.16f), p.Metal);
            var indicator = Primitive("Focus Indicator", PrimitiveType.Sphere, throttle.transform,
                new Vector3(0.34f, 0.22f, -0.04f), new Vector3(0.1f, 0.1f, 0.1f), p.Condition);
            var light = indicator.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = 0.8f;
            light.intensity = 0.55f;
            light.shadows = LightShadows.None;
            light.color = p.Condition.HasProperty("_BaseColor")
                ? p.Condition.GetColor("_BaseColor")
                : p.Condition.color;
            indicator.SetActive(false);
        }

        private static CaravanElectricalPort CreateElectricalPort(
            Transform moduleRoot,
            CaravanPart part,
            CaravanPartKind kind,
            CaravanPartPalette palette)
        {
            var portRoot = new GameObject("Electrical Port");
            portRoot.transform.SetParent(moduleRoot, false);
            portRoot.transform.localPosition = kind switch
            {
                CaravanPartKind.PhotovoltaicLeaves => new Vector3(-1.08f, 0.38f, -1.08f),
                CaravanPartKind.Battery => new Vector3(0.62f, 0.78f, -0.62f),
                CaravanPartKind.ElectricMotor => new Vector3(-0.68f, 0.74f, -0.58f),
                CaravanPartKind.DualModePump => new Vector3(0f, 1.42f, -0.28f),
                CaravanPartKind.Harvester => new Vector3(-1.35f, 0.82f, -0.58f),
                CaravanPartKind.GrassDryer => new Vector3(-0.7f, 1.45f, -0.88f),
                _ => Vector3.zero
            };
            Primitive("Terminal Block", PrimitiveType.Cube, portRoot.transform,
                Vector3.zero, new Vector3(0.36f, 0.18f, 0.22f), palette.DarkMetal);
            Primitive("Positive Terminal", PrimitiveType.Cylinder, portRoot.transform,
                new Vector3(-0.09f, 0.13f, 0f), new Vector3(0.06f, 0.1f, 0.06f), palette.Copper);
            Primitive("Return Terminal", PrimitiveType.Cylinder, portRoot.transform,
                new Vector3(0.09f, 0.13f, 0f), new Vector3(0.06f, 0.1f, 0.06f), palette.Metal);
            var buildMarker = Primitive(
                "Communication Build Marker",
                PrimitiveType.Sphere,
                portRoot.transform,
                new Vector3(0f, 0.36f, 0f),
                new Vector3(0.14f, 0.14f, 0.14f),
                palette.Condition);
            var markerLight = buildMarker.AddComponent<Light>();
            markerLight.type = LightType.Point;
            markerLight.range = 0.75f;
            markerLight.intensity = 0.45f;
            markerLight.shadows = LightShadows.None;

            var port = portRoot.AddComponent<CaravanElectricalPort>();
            var portKind = kind switch
            {
                CaravanPartKind.PhotovoltaicLeaves => CaravanElectricalPortKind.Generator,
                CaravanPartKind.Battery => CaravanElectricalPortKind.Storage,
                CaravanPartKind.ElectricMotor => CaravanElectricalPortKind.Consumer,
                CaravanPartKind.DualModePump => CaravanElectricalPortKind.Consumer,
                CaravanPartKind.Harvester => CaravanElectricalPortKind.Consumer,
                CaravanPartKind.GrassDryer => CaravanElectricalPortKind.Consumer,
                _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
            };
            port.Configure(part, portKind, buildMarker);
            return port;
        }

        private static void CreateFluidPorts(
            Transform moduleRoot,
            CaravanPart part,
            CaravanPartKind kind,
            CaravanPartPalette palette)
        {
            switch (kind)
            {
                case CaravanPartKind.WaterReservoir:
                    CreateFluidPort(
                        moduleRoot,
                        part,
                        CaravanFluidPortRole.ReservoirSupply,
                        new Vector3(-0.72f, 0.42f, -0.92f),
                        palette);
                    CreateFluidPort(
                        moduleRoot,
                        part,
                        CaravanFluidPortRole.ReservoirReturn,
                        new Vector3(0.72f, 0.42f, -0.92f),
                        palette);
                    break;
                case CaravanPartKind.DualModePump:
                    CreateFluidPort(
                        moduleRoot,
                        part,
                        CaravanFluidPortRole.PumpInlet,
                        new Vector3(-0.42f, 0.3f, 0f),
                        palette);
                    CreateFluidPort(
                        moduleRoot,
                        part,
                        CaravanFluidPortRole.PumpOutlet,
                        new Vector3(0.42f, 0.78f, 0f),
                        palette);
                    break;
                case CaravanPartKind.Radiator:
                    CreateFluidPort(
                        moduleRoot,
                        part,
                        CaravanFluidPortRole.RadiatorInlet,
                        new Vector3(-0.72f, 0.92f, -0.24f),
                        palette);
                    CreateFluidPort(
                        moduleRoot,
                        part,
                        CaravanFluidPortRole.RadiatorOutlet,
                        new Vector3(0.72f, 0.92f, -0.24f),
                        palette);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(kind), kind, null);
            }
        }

        private static CaravanFluidPort CreateFluidPort(
            Transform moduleRoot,
            CaravanPart part,
            CaravanFluidPortRole role,
            Vector3 position,
            CaravanPartPalette palette)
        {
            var portRoot = new GameObject(role.ToString());
            portRoot.transform.SetParent(moduleRoot, false);
            portRoot.transform.localPosition = position;
            Primitive(
                "Pipe Socket",
                PrimitiveType.Cylinder,
                portRoot.transform,
                Vector3.zero,
                new Vector3(0.13f, 0.16f, 0.13f),
                palette.Water).transform.localRotation =
                Quaternion.Euler(90f, 0f, 0f);
            var buildMarker = Primitive(
                "Fluid Build Marker",
                PrimitiveType.Sphere,
                portRoot.transform,
                new Vector3(0f, 0.3f, 0f),
                new Vector3(0.14f, 0.14f, 0.14f),
                palette.Condition);
            var markerLight = buildMarker.AddComponent<Light>();
            markerLight.type = LightType.Point;
            markerLight.range = 0.75f;
            markerLight.intensity = 0.45f;
            markerLight.shadows = LightShadows.None;
            var port = portRoot.AddComponent<CaravanFluidPort>();
            port.Configure(part, role, buildMarker);
            return port;
        }

        private static void CreateFluidTap(
            Transform moduleRoot,
            CaravanPart part,
            CaravanPartPalette palette)
        {
            CreateFluidPort(
                moduleRoot,
                part,
                CaravanFluidPortRole.ThermalTap,
                new Vector3(0.72f, 0.48f, -0.72f),
                palette);
        }

        private static void CreateMaterialPorts(
            Transform moduleRoot,
            CaravanPart part,
            CaravanPartKind kind,
            CaravanPartPalette palette)
        {
            switch (kind)
            {
                case CaravanPartKind.DualModePump:
                    CreateMaterialPort(
                        moduleRoot,
                        part,
                        CaravanMaterialPortRole.MechanicalConsumer,
                        new Vector3(0.36f, 1.22f, 0.42f),
                        palette);
                    break;
                case CaravanPartKind.Biofurnace:
                    CreateMaterialPort(
                        moduleRoot,
                        part,
                        CaravanMaterialPortRole.DryBiomassInput,
                        new Vector3(-0.62f, 0.42f, -0.52f),
                        palette);
                    break;
                case CaravanPartKind.BiofuelEngine:
                    CreateMaterialPort(
                        moduleRoot,
                        part,
                        CaravanMaterialPortRole.DryBiomassInput,
                        new Vector3(-0.62f, 0.42f, -0.62f),
                        palette);
                    CreateMaterialPort(
                        moduleRoot,
                        part,
                        CaravanMaterialPortRole.MechanicalSource,
                        new Vector3(0.72f, 0.62f, 0.48f),
                        palette);
                    break;
                case CaravanPartKind.ElectricMotor:
                    CreateMaterialPort(
                        moduleRoot,
                        part,
                        CaravanMaterialPortRole.MechanicalSource,
                        new Vector3(0.72f, 0.7f, 0.58f),
                        palette);
                    break;
                case CaravanPartKind.Harvester:
                    CreateMaterialPort(
                        moduleRoot,
                        part,
                        CaravanMaterialPortRole.WetBiomassOutput,
                        new Vector3(1.45f, 0.42f, -0.72f),
                        palette);
                    CreateMaterialPort(
                        moduleRoot,
                        part,
                        CaravanMaterialPortRole.MechanicalConsumer,
                        new Vector3(-1.45f, 0.62f, -0.72f),
                        palette);
                    break;
                case CaravanPartKind.GrassDryer:
                    CreateMaterialPort(
                        moduleRoot,
                        part,
                        CaravanMaterialPortRole.WetBiomassInput,
                        new Vector3(-0.72f, 0.38f, -0.92f),
                        palette);
                    CreateMaterialPort(
                        moduleRoot,
                        part,
                        CaravanMaterialPortRole.DryBiomassOutput,
                        new Vector3(0.72f, 0.38f, -0.92f),
                        palette);
                    break;
                case CaravanPartKind.BiomassStorage:
                    CreateMaterialPort(
                        moduleRoot,
                        part,
                        CaravanMaterialPortRole.DryBiomassInput,
                        new Vector3(-0.72f, 0.42f, -1.02f),
                        palette);
                    CreateMaterialPort(
                        moduleRoot,
                        part,
                        CaravanMaterialPortRole.DryBiomassOutput,
                        new Vector3(0.72f, 0.42f, -1.02f),
                        palette,
                        2);
                    break;
                case CaravanPartKind.Transmission:
                    CreateMaterialPort(
                        moduleRoot,
                        part,
                        CaravanMaterialPortRole.TransmissionInput,
                        new Vector3(0f, 0.48f, -0.72f),
                        palette,
                        2);
                    CreateMaterialPort(
                        moduleRoot,
                        part,
                        CaravanMaterialPortRole.TransmissionOutput,
                        new Vector3(0f, 0.48f, 0.72f),
                        palette,
                        2);
                    break;
                case CaravanPartKind.CouplingRope:
                    CreateMaterialPort(
                        moduleRoot,
                        part,
                        CaravanMaterialPortRole.CouplingEndpoint,
                        new Vector3(0f, 0.42f, -0.62f),
                        palette);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(kind), kind, null);
            }
        }

        private static CaravanMaterialPort CreateMaterialPort(
            Transform moduleRoot,
            CaravanPart part,
            CaravanMaterialPortRole role,
            Vector3 position,
            CaravanPartPalette palette,
            int connectionCapacity = 1)
        {
            var portRoot = new GameObject(role.ToString());
            portRoot.transform.SetParent(moduleRoot, false);
            portRoot.transform.localPosition = position;
            var material = role <= CaravanMaterialPortRole.DryBiomassInput
                ? palette.Biomass
                : palette.Copper;
            Primitive(
                "Material Socket",
                PrimitiveType.Cylinder,
                portRoot.transform,
                Vector3.zero,
                new Vector3(0.14f, 0.17f, 0.14f),
                material).transform.localRotation =
                Quaternion.Euler(90f, 0f, 0f);
            var marker = Primitive(
                "Material Build Marker",
                PrimitiveType.Sphere,
                portRoot.transform,
                new Vector3(0f, 0.31f, 0f),
                new Vector3(0.14f, 0.14f, 0.14f),
                palette.Condition);
            var markerLight = marker.AddComponent<Light>();
            markerLight.type = LightType.Point;
            markerLight.range = 0.75f;
            markerLight.intensity = 0.45f;
            markerLight.shadows = LightShadows.None;
            var port = portRoot.AddComponent<CaravanMaterialPort>();
            port.Configure(
                part,
                role,
                marker,
                connectionCapacity);
            return port;
        }

        private static CaravanControlStation CreateModuleControl(
            Transform moduleRoot,
            Transform visualRoot,
            CaravanPartPalette palette)
        {
            var station = moduleRoot.GetComponent<CaravanControlStation>()
                          ?? moduleRoot.gameObject.AddComponent<CaravanControlStation>();
            var control = new GameObject("Module Control");
            control.transform.SetParent(visualRoot, false);
            control.transform.localPosition = new Vector3(
                0f,
                1.52f,
                -0.54f);
            Primitive(
                "Control Base",
                PrimitiveType.Cylinder,
                control.transform,
                Vector3.zero,
                new Vector3(0.18f, 0.1f, 0.18f),
                palette.DarkMetal);
            var controlVisual = new GameObject("Control Visual");
            controlVisual.transform.SetParent(control.transform, false);
            Primitive(
                "Control Lever",
                PrimitiveType.Cube,
                controlVisual.transform,
                new Vector3(0f, 0.28f, 0f),
                new Vector3(0.08f, 0.54f, 0.08f),
                palette.Copper);
            Primitive(
                "Control Grip",
                PrimitiveType.Sphere,
                controlVisual.transform,
                new Vector3(0f, 0.58f, 0f),
                new Vector3(0.16f, 0.16f, 0.16f),
                palette.Metal);
            var indicator = Primitive(
                "Focus Indicator",
                PrimitiveType.Sphere,
                control.transform,
                new Vector3(0.34f, 0.22f, -0.04f),
                new Vector3(0.1f, 0.1f, 0.1f),
                palette.Condition);
            var light = indicator.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = 0.8f;
            light.intensity = 0.55f;
            light.shadows = LightShadows.None;
            indicator.SetActive(false);
            return station;
        }

        private static void BuildHarvester(Transform root, CaravanPartPalette p)
        {
            var drum = Primitive("Cutting Drum", PrimitiveType.Cylinder, root, new Vector3(0f, 0.52f, 0.45f),
                new Vector3(0.55f, 1.55f, 0.55f), p.DarkMetal);
            drum.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            for (var index = 0; index < 12; index++)
            {
                var angle = index * Mathf.PI * 2f / 12f;
                Primitive($"Cutter {index + 1}", PrimitiveType.Cube, root,
                    new Vector3(0f, 0.52f + Mathf.Sin(angle) * 0.62f, 0.45f + Mathf.Cos(angle) * 0.62f),
                    new Vector3(3.35f, 0.06f, 0.18f), p.Copper)
                    .transform.localRotation = Quaternion.Euler(angle * Mathf.Rad2Deg, 0f, 0f);
            }
            Primitive("Feed Tray", PrimitiveType.Cube, root, new Vector3(0f, 0.35f, -0.5f),
                new Vector3(2.6f, 0.16f, 1.15f), p.Metal);
        }

        private static void BuildDryer(Transform root, CaravanPartPalette p)
        {
            var drum = Primitive("Drying Drum", PrimitiveType.Cylinder, root, new Vector3(0f, 0.82f, 0f),
                new Vector3(0.72f, 1.05f, 0.72f), p.Biomass);
            drum.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            for (var z = -0.75f; z <= 0.75f; z += 0.3f)
            {
                Primitive("Drying Vent", PrimitiveType.Cube, root, new Vector3(0f, 1.28f, z),
                    new Vector3(1.0f, 0.06f, 0.08f), p.DarkMetal);
            }
            Primitive("Water Separator", PrimitiveType.Cylinder, root, new Vector3(0.62f, 0.45f, -0.75f),
                new Vector3(0.18f, 0.4f, 0.18f), p.Water);
        }

        private static void BuildBiomassStorage(Transform root, CaravanPartPalette p)
        {
            Primitive("Storage Hopper", PrimitiveType.Cube, root, new Vector3(0f, 0.92f, 0f),
                new Vector3(1.55f, 1.35f, 2.25f), p.Biomass);
            Primitive("Hopper Funnel", PrimitiveType.Cube, root, new Vector3(0f, 0.3f, 0f),
                new Vector3(0.82f, 0.42f, 1.2f), p.DarkMetal);
            Primitive("Loading Hatch", PrimitiveType.Cube, root, new Vector3(0f, 1.62f, 0f),
                new Vector3(1.15f, 0.1f, 1.55f), p.Metal);
        }

        private static void BuildTransmission(Transform root, CaravanPartPalette p)
        {
            Primitive("Gearbox", PrimitiveType.Cube, root, new Vector3(0f, 0.5f, 0f),
                new Vector3(0.72f, 0.86f, 1.35f), p.DarkMetal);
            for (var index = 0; index < 3; index++)
            {
                var gear = Primitive($"Gear {index + 1}", PrimitiveType.Cylinder, root,
                    new Vector3(0f, 0.55f + index * 0.28f, -0.3f + index * 0.28f),
                    new Vector3(0.24f + index * 0.06f, 0.08f, 0.24f + index * 0.06f), p.Copper);
                gear.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            }
            Primitive("Output Shaft", PrimitiveType.Cylinder, root, new Vector3(0f, 0.5f, 0.85f),
                new Vector3(0.12f, 0.48f, 0.12f), p.Metal)
                .transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        }

        private static void BuildCouplingRope(Transform root, CaravanPartPalette p)
        {
            var drum = Primitive("Rope Drum", PrimitiveType.Cylinder, root, new Vector3(0f, 0.62f, 0f),
                new Vector3(0.52f, 0.62f, 0.52f), p.Biomass);
            drum.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            foreach (var x in new[] { -0.7f, 0.7f })
            {
                Primitive("Drum Flange", PrimitiveType.Cylinder, root, new Vector3(x, 0.62f, 0f),
                    new Vector3(0.68f, 0.08f, 0.68f), p.DarkMetal)
                    .transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            }
            Primitive("Tow Hook", PrimitiveType.Cube, root, new Vector3(0f, 0.28f, -0.62f),
                new Vector3(0.5f, 0.18f, 0.32f), p.Copper);
        }

        private static CaravanStatusDisplay CreateStatusDisplay(
            Transform parent,
            Vector3 localPosition,
            Quaternion localRotation,
            CaravanPartPalette palette)
        {
            var root = new GameObject("Physical Status Gauge");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = localPosition;
            root.transform.localRotation = localRotation;
            Primitive("Gauge Back", PrimitiveType.Cube, root.transform, Vector3.zero,
                new Vector3(0.54f, 0.34f, 0.05f), palette.DarkMetal);
            var dust = Primitive("Dust", PrimitiveType.Cube, root.transform,
                new Vector3(-0.16f, -0.03f, -0.04f),
                new Vector3(0.08f, 0.14f, 0.035f), palette.Dust).transform;
            var condition = Primitive("Condition", PrimitiveType.Cube, root.transform,
                new Vector3(0f, -0.03f, -0.04f),
                new Vector3(0.08f, 0.14f, 0.035f), palette.Condition).transform;
            var load = Primitive("Load", PrimitiveType.Cube, root.transform,
                new Vector3(0.16f, -0.03f, -0.04f),
                new Vector3(0.08f, 0.14f, 0.035f), palette.Load).transform;
            var display = root.AddComponent<CaravanStatusDisplay>();
            display.Configure(dust, condition, load);
            return display;
        }

        private static GameObject Primitive(
            string name,
            PrimitiveType type,
            Transform parent,
            Vector3 position,
            Vector3 scale,
            Material material)
        {
            var item = GameObject.CreatePrimitive(type);
            item.name = name;
            item.transform.SetParent(parent, false);
            item.transform.localPosition = position;
            item.transform.localScale = scale;
            var collider = item.GetComponent<Collider>();
            if (collider != null)
            {
                collider.enabled = false;
            }
            var renderer = item.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = material;
            }
            return item;
        }
    }
}
