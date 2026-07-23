using System;
using UnityEngine;

namespace Steppe.Caravan
{
    [DisallowMultipleComponent]
    public sealed class CaravanModule : MonoBehaviour
    {
        private sealed class RendererState
        {
            public Renderer Renderer;
            public Color BaseColor;
            public MaterialPropertyBlock Properties;
        }

        private RendererState[] rendererStates = Array.Empty<RendererState>();
        private Transform visualRoot;
        private CaravanStatusDisplay statusDisplay;

        public string ModuleId { get; private set; }
        public bool IsMovable { get; private set; }
        public int FootprintWidth { get; private set; } = 1;
        public int FootprintLength { get; private set; } = 1;
        public float MassKilograms { get; private set; } = 50f;
        public Vector3 LocalMassCenter { get; private set; }
        public Vector3 WorldMassCenter => transform.TransformPoint(LocalMassCenter);
        public Transform VisualRoot => visualRoot;
        public CaravanModuleState State { get; } = new CaravanModuleState();

        public void Configure(
            string moduleId,
            Transform moduleVisualRoot,
            bool movable,
            int footprintWidth,
            int footprintLength,
            CaravanStatusDisplay display = null,
            float massKilograms = 50f,
            Vector3 localMassCenter = default)
        {
            ModuleId = string.IsNullOrWhiteSpace(moduleId) ? name : moduleId;
            visualRoot = moduleVisualRoot != null ? moduleVisualRoot : transform;
            IsMovable = movable;
            FootprintWidth = Mathf.Max(1, footprintWidth);
            FootprintLength = Mathf.Max(1, footprintLength);
            MassKilograms = Mathf.Max(0.1f, massKilograms);
            LocalMassCenter = localMassCenter;
            statusDisplay = display;
            CaptureRenderers();
            UpdatePresentation();
        }

        public void Clean(float amount)
        {
            State.Clean(amount);
            UpdatePresentation();
        }

        public void Repair(float amount)
        {
            State.Repair(amount);
            UpdatePresentation();
        }

        public void AccumulateDust(float amount)
        {
            State.AccumulateDust(amount);
            UpdatePresentation();
        }

        public void Damage(float amount)
        {
            State.Damage(amount);
            UpdatePresentation();
        }

        public void SetLoad(float amount)
        {
            State.SetLoad(amount);
            UpdatePresentation();
        }

        public void SetVisualVisible(bool visible)
        {
            if (visualRoot != null)
            {
                visualRoot.gameObject.SetActive(visible);
            }

            foreach (var moduleCollider in GetComponentsInChildren<Collider>(true))
            {
                if (moduleCollider.transform != transform || visualRoot == transform)
                {
                    moduleCollider.enabled = visible;
                }
            }
        }

        public void RefreshRendererCache()
        {
            CaptureRenderers();
            UpdatePresentation();
        }

        private void LateUpdate()
        {
            statusDisplay?.SetValues(State.Dust, State.Integrity, State.Load);
        }

        private void CaptureRenderers()
        {
            var renderers = visualRoot != null
                ? visualRoot.GetComponentsInChildren<Renderer>(true)
                : GetComponentsInChildren<Renderer>(true);
            rendererStates = new RendererState[renderers.Length];
            for (var index = 0; index < renderers.Length; index++)
            {
                var renderer = renderers[index];
                var material = renderer.sharedMaterial;
                var color = Color.white;
                if (material != null)
                {
                    if (material.HasProperty("_BaseColor"))
                    {
                        color = material.GetColor("_BaseColor");
                    }
                    else if (material.HasProperty("_Color"))
                    {
                        color = material.GetColor("_Color");
                    }
                }

                rendererStates[index] = new RendererState
                {
                    Renderer = renderer,
                    BaseColor = color,
                    Properties = new MaterialPropertyBlock()
                };
            }
        }

        private void UpdatePresentation()
        {
            var dustColor = new Color(0.34f, 0.27f, 0.16f, 1f);
            var damageColor = new Color(0.33f, 0.12f, 0.08f, 1f);
            for (var index = 0; index < rendererStates.Length; index++)
            {
                var state = rendererStates[index];
                if (state.Renderer == null)
                {
                    continue;
                }

                var color = Color.Lerp(state.BaseColor, dustColor, State.Dust * 0.58f);
                color = Color.Lerp(color, damageColor, (1f - State.Integrity) * 0.42f);
                state.Renderer.GetPropertyBlock(state.Properties);
                state.Properties.SetColor("_BaseColor", color);
                state.Properties.SetColor("_Color", color);
                state.Renderer.SetPropertyBlock(state.Properties);
            }

            statusDisplay?.SetValues(State.Dust, State.Integrity, State.Load);
        }
    }
}
