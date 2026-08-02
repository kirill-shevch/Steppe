using System;
using System.Collections.Generic;
using Steppe.Settings;
using Steppe.World;
using UnityEngine;
using UnityEngine.Rendering;
using VehiclePhysics;

namespace Steppe.Caravan
{
    public sealed class CaravanDemoRig
    {
        internal CaravanDemoRig(
            GameObject root,
            CaravanChassisController chassis,
            CaravanModule chassisModule,
            IReadOnlyList<CaravanModule> equipmentModules,
            CaravanElectricalNetwork electricalNetwork,
            CaravanFluidNetwork fluidNetwork,
            CaravanMaterialNetwork biomassNetwork,
            CaravanMaterialNetwork mechanicalNetwork,
            CaravanResourceSystem resourceSystem,
            CaravanConstructionService construction,
            CaravanMountGrid grid,
            Transform playerSpawn,
            CaravanControlStation steeringStation,
            CaravanControlStation brakeStation)
        {
            Root = root;
            Chassis = chassis;
            ChassisModule = chassisModule;
            EquipmentModules = equipmentModules;
            ElectricalNetwork = electricalNetwork;
            FluidNetwork = fluidNetwork;
            BiomassNetwork = biomassNetwork;
            MechanicalNetwork = mechanicalNetwork;
            ResourceSystem = resourceSystem;
            Construction = construction;
            MountGrid = grid;
            PlayerSpawn = playerSpawn;
            SteeringStation = steeringStation;
            BrakeStation = brakeStation;
        }

        public GameObject Root { get; }
        public CaravanChassisController Chassis { get; }
        public CaravanModule ChassisModule { get; }
        public IReadOnlyList<CaravanModule> EquipmentModules { get; }
        public CaravanElectricalNetwork ElectricalNetwork { get; }
        public CaravanFluidNetwork FluidNetwork { get; }
        public CaravanMaterialNetwork BiomassNetwork { get; }
        public CaravanMaterialNetwork MechanicalNetwork { get; }
        public CaravanResourceSystem ResourceSystem { get; }
        public CaravanConstructionService Construction { get; }
        public CaravanMountGrid MountGrid { get; }
        public Transform PlayerSpawn { get; }
        public CaravanControlStation SteeringStation { get; }
        public CaravanControlStation BrakeStation { get; }

        public void Configure(
            SteppeWorldSettings settings,
            FloatingOriginSystem floatingOrigin,
            CaravanEnvironmentSampler environment)
        {
            Chassis.Configure(settings, floatingOrigin, environment);
            FluidNetwork.SetEnvironment(environment);
            ResourceSystem.SetEnvironment(environment);
            Construction.SetEnvironment(environment);
            SteeringStation.ConfigureSteering(
                Chassis,
                SteeringStation.transform.Find("Control Visual"),
                SteeringStation.transform.Find("Focus Indicator")?.gameObject);
            BrakeStation.ConfigureBrake(
                Chassis,
                BrakeStation.transform.Find("Control Visual"),
                BrakeStation.transform.Find("Focus Indicator")?.gameObject);
            for (var index = 0; index < EquipmentModules.Count; index++)
            {
                Construction.ActivatePlaced(EquipmentModules[index]);
            }
        }
    }

    /// <summary>
    /// Creates an honest procedural greybox from a small shared construction kit.
    /// The generated hierarchy follows the same pivots and sockets that authored
    /// prefabs will use later.
    /// </summary>
    public static class CaravanDemoFactory
    {
        private const int DeckCellsWide = 10;
        private const int DeckCellsLong = 18;
        private const float DeckWidth = DeckCellsWide;
        private const float DeckLength = DeckCellsLong;

        public static CaravanDemoRig Create(
            Vector3 localPosition,
            Quaternion localRotation)
        {
            var materials = new List<Material>();
            var metal = CreateMaterial("Caravan Warm Metal", new Color(0.28f, 0.38f, 0.32f), 0.28f, materials);
            var darkMetal = CreateMaterial("Caravan Dark Metal", new Color(0.10f, 0.14f, 0.14f), 0.42f, materials);
            var deckMaterial = CreateMaterial("Caravan Deck", new Color(0.42f, 0.31f, 0.17f), 0.18f, materials);
            var rubber = CreateMaterial("Caravan Wheel", new Color(0.055f, 0.06f, 0.055f), 0.12f, materials);
            var copper = CreateMaterial("Caravan Copper", new Color(0.58f, 0.31f, 0.16f), 0.52f, materials);
            var solar = CreateMaterial("Caravan Solar Cells", new Color(0.055f, 0.19f, 0.25f), 0.72f, materials);
            var water = CreateMaterial("Caravan Water System", new Color(0.15f, 0.48f, 0.62f), 0.58f, materials);
            var biomass = CreateMaterial("Caravan Biomass", new Color(0.42f, 0.39f, 0.16f), 0.16f, materials);
            var hot = CreateMaterial("Caravan Heat", new Color(0.82f, 0.24f, 0.07f), 0.24f, materials);
            var ochre = CreateMaterial("Caravan Dust Gauge", new Color(0.86f, 0.48f, 0.12f), 0.2f, materials);
            var green = CreateMaterial("Caravan Condition Gauge", new Color(0.18f, 0.78f, 0.38f), 0.2f, materials);
            var blue = CreateMaterial("Caravan Load Gauge", new Color(0.17f, 0.57f, 0.92f), 0.2f, materials);

            var root = CreatePhysicsRoot(localPosition, out var vehicle, out var vehicleInput);
            root.transform.rotation = localRotation;
            var body = root.GetComponent<Rigidbody>();
            body.isKinematic = true;
            var materialOwner = root.AddComponent<CaravanGeneratedMaterialOwner>();
            materialOwner.Configure(materials);
            var chassis = root.AddComponent<CaravanChassisController>();
            var grid = root.AddComponent<CaravanMountGrid>();
            grid.Configure(DeckCellsWide, DeckCellsLong, 1f, 0f);
            CreateChassisCollider(root.transform);

            var chassisVisual = new GameObject("Chassis Visual");
            chassisVisual.transform.SetParent(root.transform, false);
            CreateDeck(chassisVisual.transform, deckMaterial, darkMetal);
            CreateFrame(chassisVisual.transform, metal, darkMetal);

            var deckCollision = new GameObject("Deck Build Surface");
            deckCollision.transform.SetParent(root.transform, false);
            deckCollision.transform.localPosition = new Vector3(0f, -0.12f, 0f);
            var deckCollider = deckCollision.AddComponent<BoxCollider>();
            deckCollider.size = new Vector3(DeckWidth, 0.24f, DeckLength);
            deckCollision.AddComponent<CaravanBuildSurface>();

            var chassisModule = root.AddComponent<CaravanModule>();
            chassisModule.Configure(
                "chassis",
                chassisVisual.transform,
                false,
                DeckCellsWide,
                DeckCellsLong,
                null,
                2600f,
                new Vector3(0f, -0.72f, 0f),
                "starter-chassis");
            chassisModule.State.SetForTests(0.24f, 0.91f, 0f);

            var wheelPositions = new[]
            {
                new Vector3(-5.08f, -0.55f, 6.55f),
                new Vector3(5.08f, -0.55f, 6.55f),
                new Vector3(-5.08f, -0.55f, -6.55f),
                new Vector3(5.08f, -0.55f, -6.55f)
            };
            var physicsWheels = new[]
            {
                vehicle.axles[0].leftWheel,
                vehicle.axles[0].rightWheel,
                vehicle.axles[1].leftWheel,
                vehicle.axles[1].rightWheel
            };
            for (var index = 0; index < wheelPositions.Length; index++)
            {
                CreateWheel(
                    root.transform,
                    index,
                    wheelPositions[index],
                    rubber,
                    darkMetal,
                    physicsWheels[index]);
            }

            var palette = new CaravanPartPalette(
                metal,
                darkMetal,
                copper,
                solar,
                water,
                biomass,
                hot,
                ochre,
                green,
                blue);
            var equipmentModules = CaravanGreyboxPartFactory.CreateInitialParts(grid, palette);
            var electricalNetwork = root.AddComponent<CaravanElectricalNetwork>();
            var electricalPorts = new List<CaravanElectricalPort>();
            for (var index = 0; index < equipmentModules.Count; index++)
            {
                electricalPorts.AddRange(
                    equipmentModules[index].GetComponentsInChildren<CaravanElectricalPort>(
                        true));
            }
            electricalNetwork.Configure(
                chassis,
                electricalPorts,
                copper,
                darkMetal);
            var fluidNetwork = root.AddComponent<CaravanFluidNetwork>();
            fluidNetwork.Configure(water);
            var biomassNetwork = root.AddComponent<CaravanMaterialNetwork>();
            biomassNetwork.Configure(
                CaravanMaterialNetworkKind.Biomass,
                biomass);
            var mechanicalNetwork = root.AddComponent<CaravanMaterialNetwork>();
            mechanicalNetwork.Configure(
                CaravanMaterialNetworkKind.Mechanical,
                copper);
            for (var index = 0; index < equipmentModules.Count; index++)
            {
                biomassNetwork.RegisterModule(equipmentModules[index]);
                mechanicalNetwork.RegisterModule(equipmentModules[index]);
            }
            var resourceSystem = root.AddComponent<CaravanResourceSystem>();
            resourceSystem.Configure(
                chassis,
                biomassNetwork,
                mechanicalNetwork,
                fluidNetwork);
            var construction = root.AddComponent<CaravanConstructionService>();
            construction.Configure(
                palette,
                chassis,
                electricalNetwork,
                fluidNetwork,
                biomassNetwork,
                mechanicalNetwork,
                resourceSystem);

            var steeringStation = CreateControlStation(
                root.transform,
                "Steering Wheel",
                new Vector3(-3.75f, 0.68f, 7.45f),
                Quaternion.Euler(18f, 0f, 0f),
                CaravanControlKind.Steering,
                darkMetal,
                metal,
                green);
            var brakeStation = CreateControlStation(
                root.transform,
                "Brake Lever",
                new Vector3(-2.25f, 0.58f, 7.3f),
                Quaternion.identity,
                CaravanControlKind.Brake,
                darkMetal,
                ochre,
                green);

            var playerSpawn = new GameObject("Player Spawn").transform;
            playerSpawn.SetParent(root.transform, false);
            playerSpawn.localPosition = new Vector3(0f, 0.06f, -5.2f);

            ConfigurePhysics(vehicle, vehicleInput, physicsWheels);
            chassisModule.RefreshRendererCache();
            root.SetActive(true);
            return new CaravanDemoRig(
                root,
                chassis,
                chassisModule,
                equipmentModules,
                electricalNetwork,
                fluidNetwork,
                biomassNetwork,
                mechanicalNetwork,
                resourceSystem,
                construction,
                grid,
                playerSpawn,
                steeringStation,
                brakeStation);
        }

        private static T FindEquipmentComponent<T>(
            IReadOnlyList<CaravanModule> modules)
            where T : Component
        {
            for (var index = 0; index < modules.Count; index++)
            {
                if (modules[index].TryGetComponent<T>(out var component))
                {
                    return component;
                }
            }

            throw new InvalidOperationException(
                $"The initial caravan is missing the required {typeof(T).Name} component.");
        }

        private static GameObject CreateSailModule(
            CaravanMountGrid grid,
            Material sailMaterial,
            Material darkMetal,
            Material metal,
            Material dust,
            Material condition,
            Material load,
            out CaravanSailModule sail,
            out CaravanModule sailModule,
            out CaravanControlStation sailStation)
        {
            var definition = CaravanPartCatalog.Get(CaravanPartKind.Sail);
            var root = new GameObject(definition.DisplayName);
            var visual = new GameObject("Visual");
            visual.transform.SetParent(root.transform, false);
            CreatePrimitive(
                "Mast",
                PrimitiveType.Cylinder,
                visual.transform,
                new Vector3(-0.72f, 2.15f, 0f),
                new Vector3(0.1f, 2.28f, 0.1f),
                darkMetal,
                false);
            var pivot = new GameObject("Sail Pivot");
            pivot.transform.SetParent(visual.transform, false);
            pivot.transform.localPosition = new Vector3(-0.66f, 2.2f, 0f);
            CreatePrimitive(
                "Top Spar",
                PrimitiveType.Cube,
                pivot.transform,
                new Vector3(1.32f, 1.75f, 0f),
                new Vector3(2.82f, 0.08f, 0.08f),
                darkMetal,
                false);
            CreatePrimitive(
                "Bottom Spar",
                PrimitiveType.Cube,
                pivot.transform,
                new Vector3(1.32f, -1.65f, 0f),
                new Vector3(2.82f, 0.08f, 0.08f),
                darkMetal,
                false);
            var cloth = CreateSailCloth(pivot.transform, sailMaterial);
            cloth.localScale = new Vector3(1.2f, 1.25f, 0.04f);
            CreateWindVane(
                visual.transform,
                darkMetal,
                metal,
                sailMaterial,
                dust);

            var collider = root.AddComponent<BoxCollider>();
            collider.center = new Vector3(0.48f, 2.15f, 0f);
            collider.size = new Vector3(2.9f, 4.35f, 0.3f);
            var display = CreateStatusDisplay(
                visual.transform,
                new Vector3(-0.76f, 0.44f, -0.2f),
                Quaternion.Euler(0f, 90f, 0f),
                darkMetal,
                dust,
                condition,
                load);
            sailModule = root.AddComponent<CaravanModule>();
            sailModule.Configure(
                definition.Id,
                visual.transform,
                true,
                definition.FootprintWidth,
                definition.FootprintLength,
                display,
                definition.MassKilograms,
                new Vector3(0.48f, 1.72f, 0f),
                "starter-sail");
            sailModule.State.SetForTests(0.14f, 0.94f, 0f);
            root.AddComponent<CaravanPart>().Configure(
                CaravanPartKind.Sail,
                definition.Capacity,
                42f);
            sail = root.AddComponent<CaravanSailModule>();
            root.AddComponent<CaravanWindVane>();
            sailStation = CreateControlStation(
                root.transform,
                "Sail Trim",
                new Vector3(-0.58f, 0.36f, -0.54f),
                Quaternion.identity,
                CaravanControlKind.SailTrim,
                darkMetal,
                metal,
                dust);

            if (!grid.Register(
                    sailModule,
                    new CaravanGridPlacement(4, 8, 2, 2, 0)))
            {
                UnityEngine.Object.Destroy(root);
                throw new InvalidOperationException("Could not place the initial sail.");
            }

            sailModule.RefreshRendererCache();
            return root;
        }

        private static void CreateWindVane(
            Transform parent,
            Material darkMetal,
            Material metal,
            Material tailMaterial,
            Material arrowMaterial)
        {
            var root = new GameObject("Wind Vane");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = new Vector3(-0.72f, 4.58f, 0f);

            CreatePrimitive(
                "Spindle",
                PrimitiveType.Cylinder,
                root.transform,
                new Vector3(0f, -0.17f, 0f),
                new Vector3(0.055f, 0.24f, 0.055f),
                darkMetal,
                false);
            CreatePrimitive(
                "Bearing",
                PrimitiveType.Sphere,
                root.transform,
                Vector3.zero,
                new Vector3(0.16f, 0.16f, 0.16f),
                metal,
                false);

            var pivot = new GameObject("Vane Pivot");
            pivot.transform.SetParent(root.transform, false);
            CreatePrimitive(
                "Arrow Shaft",
                PrimitiveType.Cube,
                pivot.transform,
                Vector3.zero,
                new Vector3(0.065f, 0.065f, 1.65f),
                metal,
                false);
            var head = CreatePrimitive(
                "Arrow Head",
                PrimitiveType.Cube,
                pivot.transform,
                new Vector3(0f, 0f, 0.86f),
                new Vector3(0.28f, 0.09f, 0.28f),
                arrowMaterial,
                false);
            head.transform.localRotation = Quaternion.Euler(0f, 45f, 0f);
            CreatePrimitive(
                "Tail Vane",
                PrimitiveType.Cube,
                pivot.transform,
                new Vector3(0f, 0.18f, -0.68f),
                new Vector3(0.52f, 0.46f, 0.055f),
                tailMaterial,
                false);
        }

        private static GameObject CreatePhysicsRoot(
            Vector3 localPosition,
            out VPVehicleController vehicle,
            out VPStandardInput vehicleInput)
        {
            var physicsAsset = Resources.Load<VppCaravanPhysicsAsset>(
                "Caravan/VPP Caravan Physics Asset");
            if (physicsAsset == null || physicsAsset.PhysicsPrefab == null)
            {
                throw new InvalidOperationException(
                    "The VPP caravan physics prefab is missing from Resources/Caravan.");
            }

            var staging = new GameObject("VPP Caravan Staging");
            staging.SetActive(false);
            var root = UnityEngine.Object.Instantiate(
                physicsAsset.PhysicsPrefab,
                staging.transform,
                false);
            root.SetActive(false);
            root.transform.SetParent(null, false);
            UnityEngine.Object.Destroy(staging);
            root.name = "Steppe Caravan";
            root.transform.SetPositionAndRotation(localPosition, Quaternion.identity);
            root.transform.localScale = Vector3.one;

            vehicle = root.GetComponent<VPVehicleController>();
            vehicleInput = root.GetComponent<VPStandardInput>();
            if (vehicle == null || vehicleInput == null || vehicle.axles == null
                || vehicle.axles.Length < 2)
            {
                UnityEngine.Object.Destroy(root);
                throw new InvalidOperationException(
                    "The selected VPP prefab does not contain a configured four-wheel vehicle.");
            }

            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                renderer.enabled = false;
            }
            foreach (var source in root.GetComponentsInChildren<AudioSource>(true))
            {
                source.enabled = false;
            }
            foreach (var camera in root.GetComponentsInChildren<Camera>(true))
            {
                camera.enabled = false;
            }
            foreach (var collider in root.GetComponentsInChildren<Collider>(true))
            {
                collider.enabled = false;
            }
            foreach (var behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (behaviour is VPVehicleController
                    || behaviour is VPStandardInput
                    || behaviour is VPWheelCollider)
                {
                    continue;
                }

                behaviour.enabled = false;
            }

            if (root.GetComponent<VPVehicleToolkit>() == null)
            {
                root.AddComponent<VPVehicleToolkit>();
            }

            return root;
        }

        private static void CreateChassisCollider(Transform parent)
        {
            var collision = new GameObject("Chassis Collision");
            collision.transform.SetParent(parent, false);
            collision.transform.localPosition = new Vector3(0f, -0.18f, 0f);
            var collider = collision.AddComponent<BoxCollider>();
            collider.size = new Vector3(9.65f, 0.34f, 17.2f);
        }

        private static void ConfigurePhysics(
            VPVehicleController vehicle,
            VPStandardInput vehicleInput,
            IReadOnlyList<VPWheelCollider> wheels)
        {
            vehicle.steering.maxSteerAngle = 32f;
            vehicle.tireFriction.frictionMultiplier = 0.9f;
            if (vehicle.centerOfMass != null)
            {
                vehicle.centerOfMass.position = vehicle.transform.TransformPoint(
                    new Vector3(0f, -0.62f, 0f));
            }

            vehicleInput.disableSteerInput = true;
            vehicleInput.disableThrottleInput = true;
            vehicleInput.disableBrakeInput = true;
            vehicleInput.disableHandbrakeInput = true;
            vehicleInput.disableClutchInput = true;
            vehicleInput.externalSteer = 0f;
            vehicleInput.externalThrottle = 0f;
            vehicleInput.externalBrake = 1f;
            vehicleInput.externalHandbrake = 0f;

            for (var index = 0; index < wheels.Count; index++)
            {
                var wheel = wheels[index];
                wheel.mass = 110f;
                wheel.radius = 0.72f;
                wheel.center = Vector3.zero;
                wheel.suspensionDistance = 0.48f;
            }
        }

        private static void CreateDeck(Transform parent, Material deck, Material frame)
        {
            for (var z = 0; z < DeckCellsLong; z++)
            {
                for (var x = 0; x < DeckCellsWide; x++)
                {
                    CreatePrimitive(
                        $"Floor {x},{z}",
                        PrimitiveType.Cube,
                        parent,
                        new Vector3(
                            -(DeckCellsWide - 1) * 0.5f + x,
                            -0.055f,
                            -(DeckCellsLong - 1) * 0.5f + z),
                        new Vector3(0.94f, 0.11f, 0.94f),
                        deck,
                        false);
                }
            }

            CreateBeam(parent, "Left Rail", new Vector3(-DeckWidth * 0.5f, -0.2f, 0f),
                new Vector3(0.16f, 0.28f, DeckLength), frame);
            CreateBeam(parent, "Right Rail", new Vector3(DeckWidth * 0.5f, -0.2f, 0f),
                new Vector3(0.16f, 0.28f, DeckLength), frame);
        }

        private static void CreateFrame(Transform parent, Material metal, Material darkMetal)
        {
            CreateBeam(parent, "Front Beam", new Vector3(0f, -0.28f, DeckLength * 0.5f - 0.22f),
                new Vector3(DeckWidth + 0.15f, 0.18f, 0.18f), darkMetal);
            CreateBeam(parent, "Rear Beam", new Vector3(0f, -0.28f, -DeckLength * 0.5f + 0.22f),
                new Vector3(DeckWidth + 0.15f, 0.18f, 0.18f), darkMetal);
            CreateBeam(parent, "Center Spine", new Vector3(0f, -0.31f, 0f),
                new Vector3(0.28f, 0.24f, DeckLength - 0.4f), metal);
            for (var z = -8; z <= 8; z += 2)
            {
                CreateBeam(parent, $"Cross Beam {z}", new Vector3(0f, -0.3f, z),
                    new Vector3(DeckWidth - 0.2f, 0.16f, 0.16f), metal);
            }
        }

        private static void CreateWheel(
            Transform parent,
            int index,
            Vector3 localPosition,
            Material rubber,
            Material hub,
            VPWheelCollider physicsWheel)
        {
            physicsWheel.transform.SetParent(parent, false);
            physicsWheel.transform.localPosition = localPosition;
            physicsWheel.transform.localRotation = Quaternion.identity;

            var suspensionRoot = new GameObject($"Wheel Suspension Visual {index + 1}");
            suspensionRoot.transform.SetParent(physicsWheel.transform, false);
            var visualRoot = new GameObject($"Wheel Spin Visual {index + 1}");
            visualRoot.transform.SetParent(suspensionRoot.transform, false);
            var tyre = CreatePrimitive(
                "Tyre",
                PrimitiveType.Cylinder,
                visualRoot.transform,
                Vector3.zero,
                new Vector3(0.72f, 0.22f, 0.72f),
                rubber,
                false);
            tyre.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            var wheelHub = CreatePrimitive(
                "Hub",
                PrimitiveType.Cylinder,
                visualRoot.transform,
                Vector3.zero,
                new Vector3(0.4f, 0.24f, 0.4f),
                hub,
                false);
            wheelHub.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);

            physicsWheel.suspensionTransform = suspensionRoot.transform;
            physicsWheel.wheelTransform = visualRoot.transform;
            physicsWheel.caliperTransform = null;
        }

        private static CaravanControlStation CreateControlStation(
            Transform parent,
            string name,
            Vector3 localPosition,
            Quaternion localRotation,
            CaravanControlKind kind,
            Material baseMaterial,
            Material controlMaterial,
            Material indicatorMaterial)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.localPosition = localPosition;
            root.transform.localRotation = localRotation;
            CreatePrimitive(
                "Pedestal",
                PrimitiveType.Cylinder,
                root.transform,
                new Vector3(0f, -0.35f, 0f),
                new Vector3(0.12f, 0.35f, 0.12f),
                baseMaterial,
                false);
            if (kind == CaravanControlKind.Steering)
            {
                CreateSteeringWheelVisual(root.transform, baseMaterial, controlMaterial);
            }
            else if (kind == CaravanControlKind.Brake)
            {
                CreateBrakeLeverVisual(root.transform, baseMaterial, controlMaterial);
            }
            else
            {
                CreateSailTrimVisual(root.transform, baseMaterial, controlMaterial);
            }

            var focusIndicator = CreatePrimitive(
                "Focus Indicator",
                PrimitiveType.Sphere,
                root.transform,
                new Vector3(0f, 0.5f, -0.08f),
                new Vector3(0.09f, 0.09f, 0.09f),
                indicatorMaterial,
                false);
            var focusLight = focusIndicator.AddComponent<Light>();
            focusLight.type = LightType.Point;
            focusLight.range = 0.8f;
            focusLight.intensity = 0.55f;
            focusLight.shadows = LightShadows.None;
            focusLight.color = indicatorMaterial.HasProperty("_BaseColor")
                ? indicatorMaterial.GetColor("_BaseColor")
                : indicatorMaterial.color;
            focusIndicator.SetActive(false);
            var collider = root.AddComponent<SphereCollider>();
            collider.radius = kind == CaravanControlKind.Brake
                ? 0.44f
                : 0.62f;
            return root.AddComponent<CaravanControlStation>();
        }

        private static void CreateSteeringWheelVisual(
            Transform parent,
            Material baseMaterial,
            Material controlMaterial)
        {
            var visual = new GameObject("Control Visual");
            visual.transform.SetParent(parent, false);
            CreatePrimitive(
                "Hub",
                PrimitiveType.Cylinder,
                visual.transform,
                Vector3.zero,
                new Vector3(0.1f, 0.075f, 0.1f),
                baseMaterial,
                false).transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

            const int segmentCount = 12;
            const float radius = 0.34f;
            for (var index = 0; index < segmentCount; index++)
            {
                var angle = index * Mathf.PI * 2f / segmentCount;
                var segment = CreatePrimitive(
                    $"Wheel Rim {index + 1}",
                    PrimitiveType.Cube,
                    visual.transform,
                    new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f),
                    new Vector3(0.19f, 0.055f, 0.055f),
                    controlMaterial,
                    false);
                segment.transform.localRotation = Quaternion.Euler(
                    0f,
                    0f,
                    angle * Mathf.Rad2Deg + 90f);
            }

            for (var index = 0; index < 4; index++)
            {
                var angle = index * 45f;
                var spoke = CreatePrimitive(
                    $"Spoke {index + 1}",
                    PrimitiveType.Cube,
                    visual.transform,
                    Vector3.zero,
                    new Vector3(0.59f, 0.035f, 0.035f),
                    baseMaterial,
                    false);
                spoke.transform.localRotation = Quaternion.Euler(0f, 0f, angle);
            }
        }

        private static void CreateSailTrimVisual(
            Transform parent,
            Material baseMaterial,
            Material controlMaterial)
        {
            var visual = new GameObject("Control Visual");
            visual.transform.SetParent(parent, false);
            CreatePrimitive(
                "Winch Drum",
                PrimitiveType.Cylinder,
                visual.transform,
                Vector3.zero,
                new Vector3(0.25f, 0.14f, 0.25f),
                baseMaterial,
                false);
            CreatePrimitive(
                "Trim Arm",
                PrimitiveType.Cube,
                visual.transform,
                new Vector3(0.28f, 0.13f, 0f),
                new Vector3(0.58f, 0.055f, 0.055f),
                controlMaterial,
                false);
            CreatePrimitive(
                "Trim Handle",
                PrimitiveType.Cylinder,
                visual.transform,
                new Vector3(0.56f, 0.24f, 0f),
                new Vector3(0.07f, 0.16f, 0.07f),
                controlMaterial,
                false);
        }

        private static void CreateBrakeLeverVisual(
            Transform parent,
            Material baseMaterial,
            Material controlMaterial)
        {
            var visual = new GameObject("Control Visual");
            visual.transform.SetParent(parent, false);
            CreatePrimitive(
                "Lever Hinge",
                PrimitiveType.Cylinder,
                visual.transform,
                Vector3.zero,
                new Vector3(0.16f, 0.12f, 0.16f),
                baseMaterial,
                false).transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            CreatePrimitive(
                "Lever Shaft",
                PrimitiveType.Cube,
                visual.transform,
                new Vector3(0f, 0.34f, 0f),
                new Vector3(0.075f, 0.68f, 0.075f),
                baseMaterial,
                false);
            CreatePrimitive(
                "Lever Grip",
                PrimitiveType.Cylinder,
                visual.transform,
                new Vector3(0f, 0.7f, 0f),
                new Vector3(0.11f, 0.24f, 0.11f),
                controlMaterial,
                false).transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
        }

        private static CaravanStatusDisplay CreateStatusDisplay(
            Transform parent,
            Vector3 position,
            Quaternion rotation,
            Material back,
            Material dust,
            Material condition,
            Material load)
        {
            var root = new GameObject("Physical Status Gauge");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = position;
            root.transform.localRotation = rotation;
            CreatePrimitive(
                "Gauge Back",
                PrimitiveType.Cube,
                root.transform,
                Vector3.zero,
                new Vector3(0.72f, 0.52f, 0.06f),
                back,
                false);
            var dustBar = CreatePrimitive(
                "Dust",
                PrimitiveType.Cube,
                root.transform,
                new Vector3(-0.22f, -0.05f, -0.045f),
                new Vector3(0.12f, 0.2f, 0.04f),
                dust,
                false).transform;
            var conditionBar = CreatePrimitive(
                "Condition",
                PrimitiveType.Cube,
                root.transform,
                new Vector3(0f, -0.05f, -0.045f),
                new Vector3(0.12f, 0.2f, 0.04f),
                condition,
                false).transform;
            var loadBar = CreatePrimitive(
                "Load",
                PrimitiveType.Cube,
                root.transform,
                new Vector3(0.22f, -0.05f, -0.045f),
                new Vector3(0.12f, 0.2f, 0.04f),
                load,
                false).transform;
            var display = root.AddComponent<CaravanStatusDisplay>();
            display.Configure(dustBar, conditionBar, loadBar);
            return display;
        }

        private static Transform CreateSailCloth(Transform parent, Material material)
        {
            var cloth = new GameObject("Cloth");
            cloth.transform.SetParent(parent, false);
            cloth.transform.localPosition = Vector3.zero;
            var mesh = new Mesh
            {
                name = "Procedural Caravan Sail",
                hideFlags = HideFlags.DontSave
            };
            mesh.vertices = new[]
            {
                new Vector3(0.04f, 1.38f, 0f),
                new Vector3(2.28f, 1.28f, 0f),
                new Vector3(2.32f, -1.22f, 0f),
                new Vector3(0.04f, -1.29f, 0f)
            };
            mesh.uv = new[]
            {
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(1f, 0f),
                new Vector2(0f, 0f)
            };
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            cloth.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = cloth.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.On;
            renderer.receiveShadows = true;
            return cloth.transform;
        }

        private static void CreateBeam(
            Transform parent,
            string name,
            Vector3 position,
            Vector3 scale,
            Material material)
        {
            CreatePrimitive(name, PrimitiveType.Cube, parent, position, scale, material, false);
        }

        private static GameObject CreatePrimitive(
            string name,
            PrimitiveType type,
            Transform parent,
            Vector3 localPosition,
            Vector3 localScale,
            Material material,
            bool keepCollider)
        {
            var item = GameObject.CreatePrimitive(type);
            item.name = name;
            item.transform.SetParent(parent, false);
            item.transform.localPosition = localPosition;
            item.transform.localScale = localScale;
            var collider = item.GetComponent<Collider>();
            if (collider != null && !keepCollider)
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

        private static Material CreateMaterial(
            string name,
            Color color,
            float smoothness,
            ICollection<Material> owner)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit")
                         ?? Shader.Find("Universal Render Pipeline/Simple Lit")
                         ?? Shader.Find("Standard");
            if (shader == null)
            {
                throw new InvalidOperationException("No supported caravan material shader was found.");
            }

            var material = new Material(shader)
            {
                name = name,
                hideFlags = HideFlags.DontSave
            };
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }
            else if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", color);
            }
            if (material.HasProperty("_Smoothness"))
            {
                material.SetFloat("_Smoothness", smoothness);
            }
            owner.Add(material);
            return material;
        }
    }

    internal sealed class CaravanGeneratedMaterialOwner : MonoBehaviour
    {
        private readonly List<Material> materials = new List<Material>();
        private readonly List<PhysicsMaterial> physicsMaterials = new List<PhysicsMaterial>();

        public void Configure(IEnumerable<Material> generatedMaterials)
        {
            materials.Clear();
            materials.AddRange(generatedMaterials);
        }

        public void AddPhysicsMaterial(PhysicsMaterial material)
        {
            if (material != null)
            {
                physicsMaterials.Add(material);
            }
        }

        private void OnDestroy()
        {
            for (var index = 0; index < materials.Count; index++)
            {
                if (materials[index] != null)
                {
                    Destroy(materials[index]);
                }
            }
            materials.Clear();
            for (var index = 0; index < physicsMaterials.Count; index++)
            {
                if (physicsMaterials[index] != null)
                {
                    Destroy(physicsMaterials[index]);
                }
            }
            physicsMaterials.Clear();
        }
    }
}
