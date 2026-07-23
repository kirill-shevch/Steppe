using System;
using System.Collections.Generic;
using Steppe.Settings;
using Steppe.World;
using UnityEngine;
using UnityEngine.Rendering;

namespace Steppe.Caravan
{
    public sealed class CaravanDemoRig
    {
        internal CaravanDemoRig(
            GameObject root,
            CaravanChassisController chassis,
            CaravanModule chassisModule,
            CaravanSailModule sail,
            CaravanModule sailModule,
            CaravanMountGrid grid,
            Transform playerSpawn,
            WheelCollider[] wheelColliders,
            Transform[] wheelVisuals,
            CaravanControlStation steeringStation,
            CaravanControlStation sailStation)
        {
            Root = root;
            Chassis = chassis;
            ChassisModule = chassisModule;
            Sail = sail;
            SailModule = sailModule;
            MountGrid = grid;
            PlayerSpawn = playerSpawn;
            WheelColliders = wheelColliders;
            WheelVisuals = wheelVisuals;
            SteeringStation = steeringStation;
            SailStation = sailStation;
        }

        public GameObject Root { get; }
        public CaravanChassisController Chassis { get; }
        public CaravanModule ChassisModule { get; }
        public CaravanSailModule Sail { get; }
        public CaravanModule SailModule { get; }
        public CaravanMountGrid MountGrid { get; }
        public Transform PlayerSpawn { get; }
        public WheelCollider[] WheelColliders { get; }
        public Transform[] WheelVisuals { get; }
        public CaravanControlStation SteeringStation { get; }
        public CaravanControlStation SailStation { get; }

        public void Configure(
            SteppeWorldSettings settings,
            FloatingOriginSystem floatingOrigin,
            CaravanEnvironmentSampler environment)
        {
            Chassis.Configure(settings, floatingOrigin, environment, WheelColliders, WheelVisuals);
            Sail.Configure(Chassis.Body, environment, Sail.transform.Find("Visual/Sail Pivot"), Sail.transform.Find("Visual/Sail Pivot/Cloth"));
            SteeringStation.ConfigureSteering(
                Chassis,
                SteeringStation.transform.Find("Control Visual"),
                SteeringStation.transform.Find("Focus Indicator")?.gameObject);
            SailStation.ConfigureSail(
                Sail,
                SailStation.transform.Find("Control Visual"),
                SailStation.transform.Find("Focus Indicator")?.gameObject);
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
            var sailMaterial = CreateMaterial("Caravan Sail", new Color(0.73f, 0.69f, 0.47f), 0.08f, materials);
            sailMaterial.doubleSidedGI = true;
            if (sailMaterial.HasProperty("_Cull"))
            {
                sailMaterial.SetFloat("_Cull", (float)CullMode.Off);
            }
            var ochre = CreateMaterial("Caravan Dust Gauge", new Color(0.86f, 0.48f, 0.12f), 0.2f, materials);
            var green = CreateMaterial("Caravan Condition Gauge", new Color(0.18f, 0.78f, 0.38f), 0.2f, materials);
            var blue = CreateMaterial("Caravan Load Gauge", new Color(0.17f, 0.57f, 0.92f), 0.2f, materials);

            var root = new GameObject("Steppe Caravan");
            root.transform.position = localPosition;
            var body = root.AddComponent<Rigidbody>();
            body.isKinematic = true;
            var materialOwner = root.AddComponent<CaravanGeneratedMaterialOwner>();
            materialOwner.Configure(materials);
            var chassis = root.AddComponent<CaravanChassisController>();
            var grid = root.AddComponent<CaravanMountGrid>();
            grid.Configure(4, 8, 1f, 0f);

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

            var wheels = new WheelCollider[4];
            var wheelVisuals = new Transform[4];
            var wheelPositions = new[]
            {
                new Vector3(-2.08f, -0.48f, 2.72f),
                new Vector3(2.08f, -0.48f, 2.72f),
                new Vector3(-2.08f, -0.48f, -2.72f),
                new Vector3(2.08f, -0.48f, -2.72f)
            };
            for (var index = 0; index < wheelPositions.Length; index++)
            {
                CreateWheel(
                    root.transform,
                    index,
                    wheelPositions[index],
                    rubber,
                    darkMetal,
                    out wheels[index],
                    out wheelVisuals[index]);
            }

            var sailRoot = new GameObject("Sail Module");
            var sailVisual = new GameObject("Visual");
            sailVisual.transform.SetParent(sailRoot.transform, false);
            CreatePrimitive(
                "Mast",
                PrimitiveType.Cylinder,
                sailVisual.transform,
                new Vector3(-0.72f, 1.72f, 0f),
                new Vector3(0.075f, 1.88f, 0.075f),
                darkMetal,
                false);
            var sailPivot = new GameObject("Sail Pivot");
            sailPivot.transform.SetParent(sailVisual.transform, false);
            sailPivot.transform.localPosition = new Vector3(-0.66f, 1.72f, 0f);
            CreatePrimitive(
                "Top Spar",
                PrimitiveType.Cube,
                sailPivot.transform,
                new Vector3(1.08f, 1.42f, 0f),
                new Vector3(2.35f, 0.07f, 0.07f),
                darkMetal,
                false);
            CreatePrimitive(
                "Bottom Spar",
                PrimitiveType.Cube,
                sailPivot.transform,
                new Vector3(1.08f, -1.32f, 0f),
                new Vector3(2.35f, 0.07f, 0.07f),
                darkMetal,
                false);
            var cloth = CreateSailCloth(sailPivot.transform, sailMaterial);

            var sailCollider = sailRoot.AddComponent<BoxCollider>();
            sailCollider.center = new Vector3(0.36f, 1.78f, 0f);
            sailCollider.size = new Vector3(2.55f, 3.45f, 0.24f);
            var sailDisplay = CreateStatusDisplay(
                sailVisual.transform,
                new Vector3(-0.74f, 0.42f, -0.18f),
                Quaternion.Euler(0f, 90f, 0f),
                darkMetal,
                ochre,
                green,
                blue);
            var sailModule = sailRoot.AddComponent<CaravanModule>();
            sailModule.Configure(
                "sail",
                sailVisual.transform,
                true,
                2,
                2,
                sailDisplay,
                90f,
                new Vector3(0.65f, 1.45f, 0f));
            sailModule.State.SetForTests(0.52f, 0.78f, 0f);
            var sail = sailRoot.AddComponent<CaravanSailModule>();
            var initialPlacement = new CaravanGridPlacement(1, 3, 2, 2, 0);
            if (!grid.Register(sailModule, initialPlacement))
            {
                throw new InvalidOperationException("Could not place the initial demo sail.");
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
            var sailStation = CreateControlStation(
                sailRoot.transform,
                "Sail Trim",
                new Vector3(-0.56f, 0.34f, -0.48f),
                Quaternion.identity,
                CaravanControlKind.SailTrim,
                darkMetal,
                metal,
                ochre);

            var playerSpawn = new GameObject("Player Spawn").transform;
            playerSpawn.SetParent(root.transform, false);
            playerSpawn.localPosition = new Vector3(0f, 0.06f, -2.55f);

            sailModule.RefreshRendererCache();
            chassisModule.RefreshRendererCache();
            return new CaravanDemoRig(
                root,
                chassis,
                chassisModule,
                sail,
                sailModule,
                grid,
                playerSpawn,
                wheels,
                wheelVisuals,
                steeringStation,
                sailStation);
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
            out WheelCollider wheel,
            out Transform visual)
        {
            var colliderObject = new GameObject($"Wheel Collider {index + 1}");
            colliderObject.transform.SetParent(parent, false);
            colliderObject.transform.localPosition = localPosition;
            wheel = colliderObject.AddComponent<WheelCollider>();
            wheel.radius = 0.58f;
            wheel.mass = 32f;
            wheel.suspensionDistance = 0.18f;
            wheel.wheelDampingRate = 0.28f;
            var spring = wheel.suspensionSpring;
            spring.spring = 38000f;
            spring.damper = 6500f;
            spring.targetPosition = 0.52f;
            wheel.suspensionSpring = spring;
            wheel.ConfigureVehicleSubsteps(5f, 8, 12);
            var forward = wheel.forwardFriction;
            forward.extremumSlip = 0.42f;
            forward.extremumValue = 1f;
            forward.asymptoteSlip = 0.86f;
            forward.asymptoteValue = 0.72f;
            forward.stiffness = 1.2f;
            wheel.forwardFriction = forward;
            var sideways = wheel.sidewaysFriction;
            sideways.extremumSlip = 0.24f;
            sideways.extremumValue = 1f;
            sideways.asymptoteSlip = 0.58f;
            sideways.asymptoteValue = 0.78f;
            sideways.stiffness = 1.55f;
            wheel.sidewaysFriction = sideways;

            var visualRoot = new GameObject($"Wheel Visual {index + 1}");
            visualRoot.transform.SetParent(parent, false);
            visualRoot.transform.localPosition = localPosition;
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
            visual = visualRoot.transform;
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
                UnityEngine.Object.Destroy(collider);
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

        public void Configure(IEnumerable<Material> generatedMaterials)
        {
            materials.Clear();
            materials.AddRange(generatedMaterials);
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
        }
    }
}
