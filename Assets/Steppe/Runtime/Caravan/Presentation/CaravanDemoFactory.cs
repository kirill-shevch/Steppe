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
            CaravanMountGrid grid,
            Transform playerSpawn,
            CaravanControlStation steeringStation)
        {
            Root = root;
            Chassis = chassis;
            ChassisModule = chassisModule;
            MountGrid = grid;
            PlayerSpawn = playerSpawn;
            SteeringStation = steeringStation;
        }

        public GameObject Root { get; }
        public CaravanChassisController Chassis { get; }
        public CaravanModule ChassisModule { get; }
        public CaravanMountGrid MountGrid { get; }
        public Transform PlayerSpawn { get; }
        public CaravanControlStation SteeringStation { get; }

        public void Configure(
            SteppeWorldSettings settings,
            FloatingOriginSystem floatingOrigin,
            CaravanEnvironmentSampler environment)
        {
            Chassis.Configure(settings, floatingOrigin, environment);
            SteeringStation.ConfigureSteering(
                Chassis,
                SteeringStation.transform.Find("Control Visual"),
                SteeringStation.transform.Find("Focus Indicator")?.gameObject);
        }
    }

    /// <summary>
    /// Creates an honest procedural greybox from a small shared construction kit.
    /// The generated hierarchy follows the same pivots and sockets that authored
    /// prefabs will use later.
    /// </summary>
    public static class CaravanDemoFactory
    {
        private const float DeckWidth = 4f;
        private const float DeckLength = 8f;

        public static CaravanDemoRig Create(Vector3 localPosition)
        {
            var materials = new List<Material>();
            var metal = CreateMaterial("Caravan Warm Metal", new Color(0.28f, 0.38f, 0.32f), 0.28f, materials);
            var darkMetal = CreateMaterial("Caravan Dark Metal", new Color(0.10f, 0.14f, 0.14f), 0.42f, materials);
            var deckMaterial = CreateMaterial("Caravan Deck", new Color(0.42f, 0.31f, 0.17f), 0.18f, materials);
            var rubber = CreateMaterial("Caravan Wheel", new Color(0.055f, 0.06f, 0.055f), 0.12f, materials);
            var ochre = CreateMaterial("Caravan Dust Gauge", new Color(0.86f, 0.48f, 0.12f), 0.2f, materials);
            var green = CreateMaterial("Caravan Condition Gauge", new Color(0.18f, 0.78f, 0.38f), 0.2f, materials);
            var blue = CreateMaterial("Caravan Load Gauge", new Color(0.17f, 0.57f, 0.92f), 0.2f, materials);

            var root = CreatePhysicsRoot(localPosition, out var vehicle, out var vehicleInput);
            var body = root.GetComponent<Rigidbody>();
            body.isKinematic = true;
            var materialOwner = root.AddComponent<CaravanGeneratedMaterialOwner>();
            materialOwner.Configure(materials);
            var chassis = root.AddComponent<CaravanChassisController>();
            var grid = root.AddComponent<CaravanMountGrid>();
            grid.Configure(4, 8, 1f, 0f);
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

            var chassisDisplay = CreateStatusDisplay(
                chassisVisual.transform,
                new Vector3(-1.45f, 0.62f, 2.55f),
                Quaternion.Euler(8f, 0f, 0f),
                darkMetal,
                ochre,
                green,
                blue);
            var chassisModule = root.AddComponent<CaravanModule>();
            chassisModule.Configure(
                "chassis",
                chassisVisual.transform,
                false,
                4,
                8,
                chassisDisplay,
                1280f,
                new Vector3(0f, -0.62f, 0f));
            chassisModule.State.SetForTests(0.24f, 0.91f, 0f);

            var wheelPositions = new[]
            {
                new Vector3(-2.08f, -0.48f, 2.72f),
                new Vector3(2.08f, -0.48f, 2.72f),
                new Vector3(-2.08f, -0.48f, -2.72f),
                new Vector3(2.08f, -0.48f, -2.72f)
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

            var steeringStation = CreateControlStation(
                root.transform,
                "Steering Wheel",
                new Vector3(0f, 0.68f, 2.92f),
                Quaternion.Euler(18f, 0f, 0f),
                CaravanControlKind.Steering,
                darkMetal,
                metal,
                green);

            var playerSpawn = new GameObject("Player Spawn").transform;
            playerSpawn.SetParent(root.transform, false);
            playerSpawn.localPosition = new Vector3(0f, 0.06f, -2.55f);

            ConfigurePhysics(vehicle, vehicleInput, physicsWheels);
            chassisModule.RefreshRendererCache();
            root.SetActive(true);
            return new CaravanDemoRig(
                root,
                chassis,
                chassisModule,
                grid,
                playerSpawn,
                steeringStation);
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
            collision.transform.localPosition = new Vector3(0f, -0.15f, 0f);
            var collider = collision.AddComponent<BoxCollider>();
            collider.size = new Vector3(3.82f, 0.34f, 7.2f);
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
                wheel.mass = 75f;
                wheel.radius = 0.58f;
                wheel.center = Vector3.zero;
                wheel.suspensionDistance = 0.38f;
            }
        }

        private static void CreateDeck(Transform parent, Material deck, Material frame)
        {
            for (var z = 0; z < 8; z++)
            {
                for (var x = 0; x < 4; x++)
                {
                    CreatePrimitive(
                        $"Floor {x},{z}",
                        PrimitiveType.Cube,
                        parent,
                        new Vector3(-1.5f + x, -0.055f, -3.5f + z),
                        new Vector3(0.94f, 0.11f, 0.94f),
                        deck,
                        false);
                }
            }

            CreateBeam(parent, "Left Rail", new Vector3(-2f, -0.2f, 0f), new Vector3(0.16f, 0.28f, 8f), frame);
            CreateBeam(parent, "Right Rail", new Vector3(2f, -0.2f, 0f), new Vector3(0.16f, 0.28f, 8f), frame);
        }

        private static void CreateFrame(Transform parent, Material metal, Material darkMetal)
        {
            CreateBeam(parent, "Front Beam", new Vector3(0f, -0.28f, 3.78f), new Vector3(4.15f, 0.18f, 0.18f), darkMetal);
            CreateBeam(parent, "Rear Beam", new Vector3(0f, -0.28f, -3.78f), new Vector3(4.15f, 0.18f, 0.18f), darkMetal);
            CreateBeam(parent, "Center Spine", new Vector3(0f, -0.31f, 0f), new Vector3(0.22f, 0.22f, 7.6f), metal);
            for (var z = -3; z <= 3; z += 2)
            {
                CreateBeam(parent, $"Cross Beam {z}", new Vector3(0f, -0.3f, z), new Vector3(3.9f, 0.16f, 0.16f), metal);
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
                new Vector3(0.58f, 0.18f, 0.58f),
                rubber,
                false);
            tyre.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            var wheelHub = CreatePrimitive(
                "Hub",
                PrimitiveType.Cylinder,
                visualRoot.transform,
                Vector3.zero,
                new Vector3(0.32f, 0.2f, 0.32f),
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
            collider.radius = 0.62f;
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
