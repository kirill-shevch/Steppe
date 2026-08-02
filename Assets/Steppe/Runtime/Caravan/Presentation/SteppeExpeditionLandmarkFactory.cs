using System;
using System.Collections.Generic;
using Steppe.Terrain;
using Steppe.Weather;
using Steppe.World;
using UnityEngine;

namespace Steppe.Caravan
{
    internal static class SteppeExpeditionLandmarkFactory
    {
        public static Transform CreateFirstRidgeTower(
            Transform worldSpaceRoot,
            FloatingOriginSystem floatingOrigin,
            SteppeWeatherSystem weather,
            TerrainHeightGenerator terrain,
            double worldX,
            double worldZ)
        {
            if (worldSpaceRoot == null)
            {
                throw new ArgumentNullException(nameof(worldSpaceRoot));
            }

            var root = new GameObject(
                "Ветровая башня — Первый гребень");
            root.transform.SetParent(worldSpaceRoot, false);
            var ground = terrain.SampleHeight(worldX, worldZ);
            root.transform.position = floatingOrigin.WorldToLocal(
                worldX,
                ground,
                worldZ);

            var materials = new List<Material>(4);
            var darkMetal = Material(
                "First Ridge Dark Metal",
                new Color(0.12f, 0.16f, 0.15f),
                0.55f,
                materials);
            var paleMetal = Material(
                "First Ridge Pale Metal",
                new Color(0.58f, 0.55f, 0.42f),
                0.35f,
                materials);
            var cloth = Material(
                "First Ridge Signal Cloth",
                new Color(0.82f, 0.33f, 0.12f),
                0.08f,
                materials);
            var beacon = Material(
                "First Ridge Beacon",
                new Color(1f, 0.63f, 0.18f),
                0.1f,
                materials);
            var owner = root.AddComponent<SteppeLandmarkMaterialOwner>();
            owner.Configure(materials);

            var legs = new[]
            {
                new Vector3(-2.2f, 7.5f, -2.2f),
                new Vector3(2.2f, 7.5f, -2.2f),
                new Vector3(-2.2f, 7.5f, 2.2f),
                new Vector3(2.2f, 7.5f, 2.2f)
            };
            for (var index = 0; index < legs.Length; index++)
            {
                var leg = Primitive(
                    $"Tower Leg {index + 1}",
                    PrimitiveType.Cube,
                    root.transform,
                    legs[index],
                    new Vector3(0.28f, 15f, 0.28f),
                    darkMetal);
                leg.localRotation = Quaternion.Euler(
                    index < 2 ? -7f : 7f,
                    0f,
                    index % 2 == 0 ? -7f : 7f);
            }

            for (var level = 0; level < 4; level++)
            {
                var y = 3f + level * 3.4f;
                Primitive(
                    $"Cross Beam X {level}",
                    PrimitiveType.Cube,
                    root.transform,
                    new Vector3(0f, y, 0f),
                    new Vector3(4.8f - level * 0.45f, 0.16f, 0.16f),
                    paleMetal);
                Primitive(
                    $"Cross Beam Z {level}",
                    PrimitiveType.Cube,
                    root.transform,
                    new Vector3(0f, y + 0.12f, 0f),
                    new Vector3(0.16f, 0.16f, 4.8f - level * 0.45f),
                    paleMetal);
            }

            var platform = Primitive(
                "Observation Platform",
                PrimitiveType.Cylinder,
                root.transform,
                new Vector3(0f, 15.2f, 0f),
                new Vector3(2.3f, 0.18f, 2.3f),
                darkMetal);
            platform.localRotation = Quaternion.identity;

            var vane = new GameObject("Wind Vane").transform;
            vane.SetParent(root.transform, false);
            vane.localPosition = new Vector3(0f, 17.6f, 0f);
            Primitive(
                "Vane Mast",
                PrimitiveType.Cylinder,
                vane,
                new Vector3(0f, -1.15f, 0f),
                new Vector3(0.12f, 1.2f, 0.12f),
                darkMetal);
            Primitive(
                "Vane Arrow",
                PrimitiveType.Cube,
                vane,
                new Vector3(0f, 0f, 1.05f),
                new Vector3(0.16f, 0.16f, 2.4f),
                paleMetal);
            Primitive(
                "Vane Tail",
                PrimitiveType.Cube,
                vane,
                new Vector3(0f, 0.4f, -1.15f),
                new Vector3(0.08f, 0.85f, 1.1f),
                cloth);

            var rotor = new GameObject("Wind Rotor").transform;
            rotor.SetParent(vane, false);
            rotor.localPosition = new Vector3(0f, 0f, 2.25f);
            rotor.localRotation = Quaternion.Euler(0f, 90f, 0f);
            Primitive(
                "Rotor Hub",
                PrimitiveType.Sphere,
                rotor,
                Vector3.zero,
                Vector3.one * 0.34f,
                paleMetal);
            for (var bladeIndex = 0; bladeIndex < 4; bladeIndex++)
            {
                var blade = Primitive(
                    $"Rotor Blade {bladeIndex + 1}",
                    PrimitiveType.Cube,
                    rotor,
                    new Vector3(0f, 0.9f, 0f),
                    new Vector3(0.14f, 1.6f, 0.36f),
                    cloth);
                blade.localRotation = Quaternion.Euler(
                    0f,
                    0f,
                    bladeIndex * 90f);
                blade.localPosition =
                    blade.localRotation * new Vector3(0f, 0.9f, 0f);
            }

            var beaconObject = Primitive(
                "Amber Beacon",
                PrimitiveType.Sphere,
                root.transform,
                new Vector3(0f, 16.1f, 0f),
                Vector3.one * 0.42f,
                beacon);
            var light = beaconObject.gameObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.5f, 0.12f);
            light.range = 22f;
            light.intensity = 2.4f;
            light.shadows = LightShadows.None;

            var presentation =
                root.AddComponent<SteppeWindLandmarkPresentation>();
            presentation.Configure(
                floatingOrigin,
                weather,
                vane,
                rotor,
                light);
            return root.transform;
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
            var collider = gameObject.GetComponent<Collider>();
            if (collider != null)
            {
                UnityEngine.Object.Destroy(collider);
            }
            gameObject.GetComponent<Renderer>().sharedMaterial = material;
            return gameObject.transform;
        }

        private static Material Material(
            string name,
            Color color,
            float metallic,
            ICollection<Material> owner)
        {
            var shader = Shader.Find(
                             "Universal Render Pipeline/Lit")
                         ?? Shader.Find(
                             "Universal Render Pipeline/Simple Lit")
                         ?? Shader.Find("Standard");
            var material = new Material(shader)
            {
                name = name,
                hideFlags = HideFlags.DontSave
            };
            material.SetColor(
                material.HasProperty("_BaseColor")
                    ? "_BaseColor"
                    : "_Color",
                color);
            if (material.HasProperty("_Metallic"))
            {
                material.SetFloat("_Metallic", metallic);
            }
            if (material.HasProperty("_Smoothness"))
            {
                material.SetFloat("_Smoothness", 0.42f);
            }
            owner.Add(material);
            return material;
        }
    }

    public sealed class SteppeWindLandmarkPresentation : MonoBehaviour
    {
        private FloatingOriginSystem floatingOrigin;
        private SteppeWeatherSystem weather;
        private Transform vane;
        private Transform rotor;
        private Light beacon;
        private float rotorAngle;

        public void Configure(
            FloatingOriginSystem origin,
            SteppeWeatherSystem weatherSystem,
            Transform vanePivot,
            Transform rotorPivot,
            Light beaconLight)
        {
            floatingOrigin = origin;
            weather = weatherSystem;
            vane = vanePivot;
            rotor = rotorPivot;
            beacon = beaconLight;
        }

        private void Update()
        {
            if (floatingOrigin == null
                || weather == null
                || vane == null
                || rotor == null)
            {
                return;
            }

            var world = floatingOrigin.LocalToWorld(transform.position);
            var sample = weather.Sample(world.X, world.Z);
            var wind = new Vector3(
                sample.SurfaceWind.x,
                0f,
                sample.SurfaceWind.y);
            if (wind.sqrMagnitude > 0.01f)
            {
                var target = Quaternion.LookRotation(-wind.normalized);
                vane.rotation = Quaternion.Slerp(
                    vane.rotation,
                    target,
                    1f - Mathf.Exp(-UnityEngine.Time.deltaTime * 2.2f));
            }

            rotorAngle = Mathf.Repeat(
                rotorAngle
                + wind.magnitude * UnityEngine.Time.deltaTime * 28f,
                360f);
            rotor.localRotation =
                Quaternion.Euler(rotorAngle, 90f, 0f);
            if (beacon != null)
            {
                beacon.intensity =
                    1.9f
                    + Mathf.Sin(UnityEngine.Time.unscaledTime * 2.3f)
                    * 0.55f;
            }
        }
    }

    internal sealed class SteppeLandmarkMaterialOwner : MonoBehaviour
    {
        private IReadOnlyList<Material> materials;

        public void Configure(IReadOnlyList<Material> ownedMaterials)
        {
            materials = ownedMaterials;
        }

        private void OnDestroy()
        {
            if (materials == null)
            {
                return;
            }
            for (var index = 0; index < materials.Count; index++)
            {
                if (materials[index] != null)
                {
                    Destroy(materials[index]);
                }
            }
        }
    }
}
