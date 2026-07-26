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
        private readonly struct PartSpec
        {
            public PartSpec(
                string id,
                string displayName,
                int width,
                int length,
                float mass,
                float capacity)
            {
                Id = id;
                DisplayName = displayName;
                Width = width;
                Length = length;
                Mass = mass;
                Capacity = capacity;
            }

            public string Id { get; }
            public string DisplayName { get; }
            public int Width { get; }
            public int Length { get; }
            public float Mass { get; }
            public float Capacity { get; }
        }

        public static IReadOnlyList<CaravanModule> CreateInitialParts(
            CaravanMountGrid grid,
            CaravanPartPalette palette)
        {
            var modules = new List<CaravanModule>
            {
                Create(grid, palette, CaravanPartKind.BiomassStorage, new CaravanGridPlacement(0, 0, 2, 3, 0)),
                Create(grid, palette, CaravanPartKind.GrassDryer, new CaravanGridPlacement(2, 0, 2, 3, 0)),
                Create(grid, palette, CaravanPartKind.WaterReservoir, new CaravanGridPlacement(6, 0, 2, 3, 0)),
                Create(grid, palette, CaravanPartKind.Biofurnace, new CaravanGridPlacement(8, 0, 2, 2, 0)),
                Create(grid, palette, CaravanPartKind.Battery, new CaravanGridPlacement(0, 4, 2, 2, 0)),
                Create(grid, palette, CaravanPartKind.DualModePump, new CaravanGridPlacement(2, 4, 1, 2, 0)),
                Create(grid, palette, CaravanPartKind.Radiator, new CaravanGridPlacement(3, 4, 2, 2, 0)),
                Create(grid, palette, CaravanPartKind.BiofuelEngine, new CaravanGridPlacement(5, 4, 2, 2, 0)),
                Create(grid, palette, CaravanPartKind.ElectricMotor, new CaravanGridPlacement(7, 4, 2, 2, 0)),
                Create(grid, palette, CaravanPartKind.Transmission, new CaravanGridPlacement(9, 4, 1, 2, 0)),
                Create(grid, palette, CaravanPartKind.PhotovoltaicLeaves, new CaravanGridPlacement(0, 8, 3, 3, 0)),
                Create(grid, palette, CaravanPartKind.Harvester, new CaravanGridPlacement(3, 16, 4, 2, 0)),
                Create(grid, palette, CaravanPartKind.CouplingRope, new CaravanGridPlacement(8, 16, 2, 1, 0))
            };

            return modules;
        }

        private static CaravanModule Create(
            CaravanMountGrid grid,
            CaravanPartPalette palette,
            CaravanPartKind kind,
            CaravanGridPlacement placement)
        {
            var spec = GetSpec(kind);
            var root = new GameObject(spec.DisplayName);
            var visual = new GameObject("Visual");
            visual.transform.SetParent(root.transform, false);
            BuildVisual(kind, visual.transform, palette);

            var collider = root.AddComponent<BoxCollider>();
            collider.center = new Vector3(0f, 0.72f, 0f);
            collider.size = new Vector3(
                Mathf.Max(0.72f, spec.Width * 0.82f),
                1.45f,
                Mathf.Max(0.72f, spec.Length * 0.82f));

            var display = CreateStatusDisplay(
                visual.transform,
                new Vector3(
                    -spec.Width * 0.24f,
                    0.72f,
                    -spec.Length * 0.34f),
                Quaternion.Euler(12f, 0f, 0f),
                palette);
            var module = root.AddComponent<CaravanModule>();
            module.Configure(
                spec.Id,
                visual.transform,
                true,
                spec.Width,
                spec.Length,
                display,
                spec.Mass,
                new Vector3(0f, 0.62f, 0f));
            module.State.SetForTests(0.08f, 0.96f, 0f);
            var part = root.AddComponent<CaravanPart>();
            part.Configure(kind, spec.Capacity, spec.Capacity * 0.35f);
            if (kind == CaravanPartKind.PhotovoltaicLeaves)
            {
                root.AddComponent<CaravanPhotovoltaicModule>();
            }

            if (!grid.Register(module, placement))
            {
                UnityEngine.Object.Destroy(root);
                throw new InvalidOperationException(
                    $"Could not place initial caravan part '{spec.DisplayName}'.");
            }

            module.RefreshRendererCache();
            return module;
        }

        private static PartSpec GetSpec(CaravanPartKind kind)
        {
            return kind switch
            {
                CaravanPartKind.PhotovoltaicLeaves => new PartSpec(
                    "photovoltaic-leaves", "Photovoltaic Leaves", 3, 3, 180f, 18f),
                CaravanPartKind.Battery => new PartSpec(
                    "battery", "Electric Battery", 2, 2, 310f, 120f),
                CaravanPartKind.WaterReservoir => new PartSpec(
                    "water-reservoir", "Water Reservoir", 2, 3, 820f, 900f),
                CaravanPartKind.DualModePump => new PartSpec(
                    "dual-mode-pump", "Dual-mode Pump", 1, 2, 125f, 24f),
                CaravanPartKind.Radiator => new PartSpec(
                    "radiator", "Wind Radiator", 2, 2, 175f, 32f),
                CaravanPartKind.Biofurnace => new PartSpec(
                    "biofurnace", "Biofurnace", 2, 2, 285f, 80f),
                CaravanPartKind.BiofuelEngine => new PartSpec(
                    "biofuel-engine", "Biofuel Engine", 2, 2, 430f, 55f),
                CaravanPartKind.ElectricMotor => new PartSpec(
                    "electric-motor", "Electric Motor", 2, 2, 255f, 55f),
                CaravanPartKind.Harvester => new PartSpec(
                    "harvester", "Biomass Harvester", 4, 2, 380f, 90f),
                CaravanPartKind.GrassDryer => new PartSpec(
                    "grass-dryer", "Grass Dryer", 2, 3, 315f, 240f),
                CaravanPartKind.BiomassStorage => new PartSpec(
                    "biomass-storage", "Dry Biomass Storage", 2, 3, 270f, 600f),
                CaravanPartKind.Transmission => new PartSpec(
                    "transmission", "Transmission", 1, 2, 190f, 8f),
                CaravanPartKind.CouplingRope => new PartSpec(
                    "coupling-rope", "Coupling Rope", 2, 1, 95f, 24f),
                _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
            };
        }

        private static void BuildVisual(
            CaravanPartKind kind,
            Transform root,
            CaravanPartPalette palette)
        {
            switch (kind)
            {
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
