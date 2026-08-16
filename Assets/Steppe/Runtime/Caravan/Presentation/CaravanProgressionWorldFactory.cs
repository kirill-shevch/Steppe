using System;
using System.Collections.Generic;
using Steppe.Terrain;
using Steppe.World;
using UnityEngine;

namespace Steppe.Caravan
{
    public sealed class CaravanProgressionWorldRig
    {
        internal CaravanProgressionWorldRig(
            CaravanSalvageWreck tutorialWreck,
            CaravanRecipeSite powerStation,
            IReadOnlyList<CaravanSalvageWreck> wrecks,
            IReadOnlyList<CaravanRecipeSite> recipeSites)
        {
            TutorialWreck = tutorialWreck;
            PowerStation = powerStation;
            Wrecks = wrecks;
            RecipeSites = recipeSites;
        }

        public CaravanSalvageWreck TutorialWreck { get; }
        public CaravanRecipeSite PowerStation { get; }
        public IReadOnlyList<CaravanSalvageWreck> Wrecks { get; }
        public IReadOnlyList<CaravanRecipeSite> RecipeSites { get; }

        public void RefreshFromProgression()
        {
            for (var index = 0; index < Wrecks.Count; index++)
            {
                Wrecks[index]?.RefreshFromProgression();
            }
            for (var index = 0; index < RecipeSites.Count; index++)
            {
                RecipeSites[index]?.RefreshFromProgression();
            }
        }
    }

    internal static class CaravanProgressionWorldFactory
    {
        public static CaravanProgressionWorldRig Create(
            Transform worldSpaceRoot,
            FloatingOriginSystem floatingOrigin,
            TerrainHeightGenerator terrain,
            CaravanProgressionSystem progression,
            double startWorldX,
            double startWorldZ,
            Quaternion startingRotation)
        {
            if (worldSpaceRoot == null)
            {
                throw new ArgumentNullException(nameof(worldSpaceRoot));
            }
            if (floatingOrigin == null)
            {
                throw new ArgumentNullException(nameof(floatingOrigin));
            }
            if (terrain == null)
            {
                throw new ArgumentNullException(nameof(terrain));
            }
            if (progression == null)
            {
                throw new ArgumentNullException(nameof(progression));
            }

            var group = new GameObject("P18 Progression Landmarks");
            group.transform.SetParent(worldSpaceRoot, false);
            var materials = new List<Material>(8);
            var rust = Material(
                "P18 Rusted Steel",
                new Color(0.34f, 0.19f, 0.10f),
                0.35f,
                materials);
            var dark = Material(
                "P18 Charred Metal",
                new Color(0.08f, 0.10f, 0.09f),
                0.55f,
                materials);
            var electric = Material(
                "P18 Electrical Landmark",
                new Color(0.12f, 0.42f, 0.58f),
                0.42f,
                materials);
            var water = Material(
                "P18 Water Landmark",
                new Color(0.14f, 0.48f, 0.55f),
                0.28f,
                materials);
            var farm = Material(
                "P18 Farm Landmark",
                new Color(0.43f, 0.39f, 0.14f),
                0.12f,
                materials);
            var workshop = Material(
                "P18 Workshop Landmark",
                new Color(0.48f, 0.27f, 0.11f),
                0.38f,
                materials);
            var salvageGlow = EmissiveMaterial(
                "P18 Uncollected Salvage Glow",
                new Color(1f, 0.38f, 0.08f),
                1.35f,
                materials);
            var recipeGlow = EmissiveMaterial(
                "P18 Uncollected Recipe Glow",
                new Color(0.12f, 0.68f, 1f),
                1.45f,
                materials);
            group.AddComponent<SteppeLandmarkMaterialOwner>().Configure(materials);

            var forward = Vector3.ProjectOnPlane(
                startingRotation * Vector3.forward,
                Vector3.up).normalized;
            if (forward.sqrMagnitude < 0.5f)
            {
                forward = Vector3.forward;
            }
            var right = Vector3.Cross(Vector3.up, forward).normalized;

            // Landmarks stay roughly 2.4 km apart, but knowledge is intentionally
            // rarer than material. After the first power station, three salvage
            // caravans separate each recipe site so discoveries create buildable
            // options instead of an unaffordable catalogue backlog.
            var wrecks = new List<CaravanSalvageWreck>(10);
            var tutorialWreck = CreateWreck(
                group.transform,
                floatingOrigin,
                terrain,
                progression,
                "wreck-tutorial-01",
                Offset(startWorldX, startWorldZ, forward * 60f + right * 12f),
                rust,
                dark,
                salvageGlow,
                new[]
                {
                    Amount(CaravanConstructionResourceKind.StructuralMaterial, 24),
                    Amount(CaravanConstructionResourceKind.MechanicalParts, 4),
                    Amount(CaravanConstructionResourceKind.Fabric, 4)
                });
            wrecks.Add(tutorialWreck);
            wrecks.Add(CreateWreck(
                group.transform,
                floatingOrigin,
                terrain,
                progression,
                "wreck-electrical-02",
                Offset(startWorldX, startWorldZ, forward * 4800f - right * 180f),
                rust,
                electric,
                salvageGlow,
                new[]
                {
                    Amount(CaravanConstructionResourceKind.StructuralMaterial, 28),
                    Amount(CaravanConstructionResourceKind.MechanicalParts, 16),
                    Amount(CaravanConstructionResourceKind.ElectricalParts, 28)
                }));
            wrecks.Add(CreateWreck(
                group.transform,
                floatingOrigin,
                terrain,
                progression,
                "wreck-electrical-03",
                Offset(startWorldX, startWorldZ, forward * 7200f - right * 260f),
                rust,
                electric,
                salvageGlow,
                new[]
                {
                    Amount(CaravanConstructionResourceKind.StructuralMaterial, 32),
                    Amount(CaravanConstructionResourceKind.MechanicalParts, 18),
                    Amount(CaravanConstructionResourceKind.ElectricalParts, 12),
                    Amount(CaravanConstructionResourceKind.Fabric, 6)
                }));
            wrecks.Add(CreateWreck(
                group.transform,
                floatingOrigin,
                terrain,
                progression,
                "wreck-electrical-04",
                Offset(startWorldX, startWorldZ, forward * 9600f + right * 120f),
                rust,
                electric,
                salvageGlow,
                new[]
                {
                    Amount(CaravanConstructionResourceKind.StructuralMaterial, 30),
                    Amount(CaravanConstructionResourceKind.MechanicalParts, 18),
                    Amount(CaravanConstructionResourceKind.ElectricalParts, 20),
                    Amount(CaravanConstructionResourceKind.Fabric, 4)
                }));
            wrecks.Add(CreateWreck(
                group.transform,
                floatingOrigin,
                terrain,
                progression,
                "wreck-biomass-04",
                Offset(startWorldX, startWorldZ, forward * 14400f - right * 220f),
                rust,
                farm,
                salvageGlow,
                new[]
                {
                    Amount(CaravanConstructionResourceKind.StructuralMaterial, 34),
                    Amount(CaravanConstructionResourceKind.MechanicalParts, 18),
                    Amount(CaravanConstructionResourceKind.ElectricalParts, 8),
                    Amount(CaravanConstructionResourceKind.Fabric, 26)
                }));
            wrecks.Add(CreateWreck(
                group.transform,
                floatingOrigin,
                terrain,
                progression,
                "wreck-water-05",
                Offset(startWorldX, startWorldZ, forward * 16800f + right * 160f),
                rust,
                water,
                salvageGlow,
                new[]
                {
                    Amount(CaravanConstructionResourceKind.StructuralMaterial, 30),
                    Amount(CaravanConstructionResourceKind.MechanicalParts, 18),
                    Amount(CaravanConstructionResourceKind.ElectricalParts, 8),
                    Amount(CaravanConstructionResourceKind.Fabric, 10)
                }));
            wrecks.Add(CreateWreck(
                group.transform,
                floatingOrigin,
                terrain,
                progression,
                "wreck-biomass-06",
                Offset(startWorldX, startWorldZ, forward * 19200f - right * 120f),
                rust,
                farm,
                salvageGlow,
                new[]
                {
                    Amount(CaravanConstructionResourceKind.StructuralMaterial, 34),
                    Amount(CaravanConstructionResourceKind.MechanicalParts, 16),
                    Amount(CaravanConstructionResourceKind.ElectricalParts, 5),
                    Amount(CaravanConstructionResourceKind.Fabric, 28)
                }));
            wrecks.Add(CreateWreck(
                group.transform,
                floatingOrigin,
                terrain,
                progression,
                "wreck-biomass-07",
                Offset(startWorldX, startWorldZ, forward * 24000f + right * 180f),
                rust,
                farm,
                salvageGlow,
                new[]
                {
                    Amount(CaravanConstructionResourceKind.StructuralMaterial, 38),
                    Amount(CaravanConstructionResourceKind.MechanicalParts, 20),
                    Amount(CaravanConstructionResourceKind.ElectricalParts, 7),
                    Amount(CaravanConstructionResourceKind.Fabric, 30)
                }));
            wrecks.Add(CreateWreck(
                group.transform,
                floatingOrigin,
                terrain,
                progression,
                "wreck-mechanical-05",
                Offset(startWorldX, startWorldZ, forward * 26400f - right * 120f),
                rust,
                workshop,
                salvageGlow,
                new[]
                {
                    Amount(CaravanConstructionResourceKind.StructuralMaterial, 36),
                    Amount(CaravanConstructionResourceKind.MechanicalParts, 34),
                    Amount(CaravanConstructionResourceKind.ElectricalParts, 8),
                    Amount(CaravanConstructionResourceKind.Fabric, 18)
                }));
            wrecks.Add(CreateWreck(
                group.transform,
                floatingOrigin,
                terrain,
                progression,
                "wreck-mechanical-08",
                Offset(startWorldX, startWorldZ, forward * 28800f + right * 130f),
                rust,
                workshop,
                salvageGlow,
                new[]
                {
                    Amount(CaravanConstructionResourceKind.StructuralMaterial, 42),
                    Amount(CaravanConstructionResourceKind.MechanicalParts, 38),
                    Amount(CaravanConstructionResourceKind.ElectricalParts, 12),
                    Amount(CaravanConstructionResourceKind.Fabric, 16)
                }));

            var sites = new List<CaravanRecipeSite>(4);
            var powerStation = CreateRecipeSite(
                group.transform,
                floatingOrigin,
                terrain,
                progression,
                "site-power-01",
                CaravanRecipeSiteKind.PowerStation,
                Offset(startWorldX, startWorldZ, forward * 2400f + right * 140f),
                dark,
                electric,
                recipeGlow);
            sites.Add(powerStation);
            sites.Add(CreateRecipeSite(
                group.transform,
                floatingOrigin,
                terrain,
                progression,
                "site-water-01",
                CaravanRecipeSiteKind.WaterFacility,
                Offset(startWorldX, startWorldZ, forward * 12000f + right * 220f),
                dark,
                water,
                recipeGlow));
            sites.Add(CreateRecipeSite(
                group.transform,
                floatingOrigin,
                terrain,
                progression,
                "site-farm-01",
                CaravanRecipeSiteKind.Farm,
                Offset(startWorldX, startWorldZ, forward * 21600f + right * 300f),
                dark,
                farm,
                recipeGlow));
            sites.Add(CreateRecipeSite(
                group.transform,
                floatingOrigin,
                terrain,
                progression,
                "site-workshop-01",
                CaravanRecipeSiteKind.TransportWorkshop,
                Offset(startWorldX, startWorldZ, forward * 31200f + right * 180f),
                dark,
                workshop,
                recipeGlow));

            return new CaravanProgressionWorldRig(
                tutorialWreck,
                powerStation,
                wrecks,
                sites);
        }

        private static CaravanSalvageWreck CreateWreck(
            Transform parent,
            FloatingOriginSystem floatingOrigin,
            TerrainHeightGenerator terrain,
            CaravanProgressionSystem progression,
            string id,
            Vector2 worldPosition,
            Material bodyMaterial,
            Material accentMaterial,
            Material discoveryMaterial,
            IReadOnlyList<CaravanResourceAmount> yield)
        {
            var root = new GameObject("Разрушенный караван");
            root.transform.SetParent(parent, false);
            var ground = terrain.SampleHeight(worldPosition.x, worldPosition.y);
            root.transform.position = floatingOrigin.WorldToLocal(
                worldPosition.x,
                ground + 0.35,
                worldPosition.y);
            root.transform.rotation = Quaternion.Euler(4f, id.GetHashCode() % 360, 7f);

            var intact = new GameObject("Intact Salvage").transform;
            intact.SetParent(root.transform, false);
            Primitive("Broken Deck", PrimitiveType.Cube, intact,
                new Vector3(0f, 0.38f, 0f), new Vector3(3.4f, 0.28f, 5.2f), bodyMaterial);
            Primitive("Bent Frame", PrimitiveType.Cube, intact,
                new Vector3(0.7f, 1.0f, -0.4f), new Vector3(0.24f, 1.6f, 3.8f), accentMaterial)
                .localRotation = Quaternion.Euler(0f, 0f, 18f);
            for (var index = 0; index < 3; index++)
            {
                var wheel = Primitive(
                    $"Broken Wheel {index + 1}",
                    PrimitiveType.Cylinder,
                    intact,
                    new Vector3(index == 0 ? -1.65f : 1.65f, 0.12f, -1.5f + index * 1.5f),
                    new Vector3(0.62f, 0.24f, 0.62f),
                    accentMaterial);
                wheel.localRotation = Quaternion.Euler(0f, 0f, 90f);
            }

            var depleted = new GameObject("Depleted Salvage").transform;
            depleted.SetParent(root.transform, false);
            Primitive("Bare Frame", PrimitiveType.Cube, depleted,
                new Vector3(0f, 0.12f, 0f), new Vector3(2.8f, 0.12f, 4.4f), bodyMaterial);
            depleted.gameObject.SetActive(false);

            var discoveryGlow = CreateWreckDiscoveryGlow(
                root.transform,
                discoveryMaterial);

            var collider = root.AddComponent<BoxCollider>();
            collider.center = new Vector3(0f, 0.75f, 0f);
            collider.size = new Vector3(4.2f, 1.8f, 6f);
            var wreck = root.AddComponent<CaravanSalvageWreck>();
            wreck.Configure(
                id,
                progression,
                yield,
                intact,
                depleted,
                uncollectedGlow: discoveryGlow);
            return wreck;
        }

        private static CaravanRecipeSite CreateRecipeSite(
            Transform parent,
            FloatingOriginSystem floatingOrigin,
            TerrainHeightGenerator terrain,
            CaravanProgressionSystem progression,
            string id,
            CaravanRecipeSiteKind kind,
            Vector2 worldPosition,
            Material structureMaterial,
            Material accentMaterial,
            Material discoveryMaterial)
        {
            var root = new GameObject(CaravanRecipeSite.SiteName(kind));
            root.transform.SetParent(parent, false);
            var ground = terrain.SampleHeight(worldPosition.x, worldPosition.y);
            root.transform.position = floatingOrigin.WorldToLocal(
                worldPosition.x,
                ground,
                worldPosition.y);

            Primitive("Collapsed Hall", PrimitiveType.Cube, root.transform,
                new Vector3(0f, 2.1f, 0f), new Vector3(9f, 4.2f, 6.5f), structureMaterial)
                .localRotation = Quaternion.Euler(0f, 8f, -5f);
            Primitive("Landmark Tower", PrimitiveType.Cube, root.transform,
                new Vector3(-2.8f, 7.2f, 1.2f), new Vector3(1.6f, 10.5f, 1.6f), accentMaterial)
                .localRotation = Quaternion.Euler(0f, 0f, -7f);
            Primitive("Broken Roof", PrimitiveType.Cube, root.transform,
                new Vector3(1.4f, 4.6f, 0f), new Vector3(7.5f, 0.32f, 5.8f), accentMaterial)
                .localRotation = Quaternion.Euler(0f, 0f, 14f);

            var archive = Primitive(
                "Сохранившиеся чертежи",
                PrimitiveType.Cube,
                root.transform,
                new Vector3(0f, 1.05f, -3.55f),
                new Vector3(1.5f, 1.8f, 0.7f),
                accentMaterial);
            var archiveCollider = archive.gameObject.AddComponent<BoxCollider>();
            archiveCollider.size = Vector3.one;
            var discoveryGlow = CreateRecipeDiscoveryGlow(
                root.transform,
                discoveryMaterial);
            var site = root.AddComponent<CaravanRecipeSite>();
            site.Configure(id, kind, progression, discoveryGlow);
            return site;
        }

        private static Transform CreateWreckDiscoveryGlow(
            Transform parent,
            Material material)
        {
            var glow = new GameObject("Uncollected Salvage Glow").transform;
            glow.SetParent(parent, false);
            Primitive(
                "Left Salvage Trace",
                PrimitiveType.Cube,
                glow,
                new Vector3(-1.54f, 0.6f, 0f),
                new Vector3(0.07f, 0.07f, 4.5f),
                material);
            Primitive(
                "Right Salvage Trace",
                PrimitiveType.Cube,
                glow,
                new Vector3(1.54f, 0.6f, 0f),
                new Vector3(0.07f, 0.07f, 4.5f),
                material);
            AddDiscoveryLight(
                glow,
                new Vector3(0f, 1.1f, 0f),
                new Color(1f, 0.38f, 0.08f),
                0.7f,
                10f);
            return glow;
        }

        private static Transform CreateRecipeDiscoveryGlow(
            Transform parent,
            Material material)
        {
            var glow = new GameObject("Uncollected Recipe Glow").transform;
            glow.SetParent(parent, false);
            for (var index = 0; index < 3; index++)
            {
                Primitive(
                    $"Recipe Signal {index + 1}",
                    PrimitiveType.Cube,
                    glow,
                    new Vector3(-2.8f, 4.8f + index * 2.3f, 0.36f),
                    new Vector3(0.78f, 0.13f, 0.06f),
                    material);
            }
            Primitive(
                "Archive Signal",
                PrimitiveType.Cube,
                glow,
                new Vector3(0f, 1.3f, -3.93f),
                new Vector3(1.0f, 0.12f, 0.05f),
                material);
            AddDiscoveryLight(
                glow,
                new Vector3(0f, 1.5f, -3.7f),
                new Color(0.12f, 0.68f, 1f),
                0.85f,
                12f);
            return glow;
        }

        private static void AddDiscoveryLight(
            Transform parent,
            Vector3 localPosition,
            Color color,
            float intensity,
            float range)
        {
            var lightObject = new GameObject("Soft Discovery Light");
            lightObject.transform.SetParent(parent, false);
            lightObject.transform.localPosition = localPosition;
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.intensity = intensity;
            light.range = range;
            light.shadows = LightShadows.None;
        }

        private static Transform Primitive(
            string name,
            PrimitiveType type,
            Transform parent,
            Vector3 localPosition,
            Vector3 localScale,
            Material material)
        {
            var gameObject = GameObject.CreatePrimitive(type);
            gameObject.name = name;
            gameObject.transform.SetParent(parent, false);
            gameObject.transform.localPosition = localPosition;
            gameObject.transform.localScale = localScale;
            gameObject.GetComponent<Renderer>().sharedMaterial = material;
            var collider = gameObject.GetComponent<Collider>();
            if (collider != null)
            {
                UnityEngine.Object.Destroy(collider);
            }
            return gameObject.transform;
        }

        private static Material Material(
            string name,
            Color color,
            float metallic,
            ICollection<Material> owner)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit")
                         ?? Shader.Find("Universal Render Pipeline/Simple Lit")
                         ?? Shader.Find("Standard");
            var material = new Material(shader)
            {
                name = name,
                hideFlags = HideFlags.DontSave
            };
            material.SetColor(
                material.HasProperty("_BaseColor") ? "_BaseColor" : "_Color",
                color);
            if (material.HasProperty("_Metallic"))
            {
                material.SetFloat("_Metallic", metallic);
            }
            owner.Add(material);
            return material;
        }

        private static Material EmissiveMaterial(
            string name,
            Color color,
            float emissionStrength,
            ICollection<Material> owner)
        {
            var material = Material(name, color * 0.38f, 0f, owner);
            if (material.HasProperty("_EmissionColor"))
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", color * emissionStrength);
                material.globalIlluminationFlags =
                    MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            return material;
        }

        private static CaravanResourceAmount Amount(
            CaravanConstructionResourceKind kind,
            int amount)
        {
            return new CaravanResourceAmount(kind, amount);
        }

        private static Vector2 Offset(
            double startWorldX,
            double startWorldZ,
            Vector3 offset)
        {
            return new Vector2(
                (float)(startWorldX + offset.x),
                (float)(startWorldZ + offset.z));
        }
    }
}
