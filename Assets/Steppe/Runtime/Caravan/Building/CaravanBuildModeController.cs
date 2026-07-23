using System;
using Steppe.Player;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace Steppe.Caravan
{
    [DisallowMultipleComponent]
    public sealed class CaravanBuildModeController : MonoBehaviour
    {
        private Camera viewCamera;
        private CaravanFirstPersonController firstPerson;
        private CaravanChassisController chassis;
        private CaravanMountGrid grid;
        private CaravanModule heldModule;
        private CaravanGridPlacement previousPlacement;
        private GameObject ghost;
        private Material validGhostMaterial;
        private Material invalidGhostMaterial;
        private CaravanGridPlacement candidate;
        private bool candidateValid;
        private int quarterTurns;

        public bool IsActive { get; private set; }
        public CaravanModule HeldModule => heldModule;
        public bool CandidateValid => candidateValid;

        public void Configure(
            Camera camera,
            CaravanFirstPersonController controller,
            CaravanChassisController caravan,
            CaravanMountGrid mountGrid)
        {
            viewCamera = camera != null ? camera : throw new ArgumentNullException(nameof(camera));
            firstPerson = controller != null ? controller : throw new ArgumentNullException(nameof(controller));
            chassis = caravan != null ? caravan : throw new ArgumentNullException(nameof(caravan));
            grid = mountGrid != null ? mountGrid : throw new ArgumentNullException(nameof(mountGrid));
            validGhostMaterial = CreateGhostMaterial(
                "Caravan Valid Placement",
                new Color(0.15f, 0.92f, 0.48f, 0.38f));
            invalidGhostMaterial = CreateGhostMaterial(
                "Caravan Invalid Placement",
                new Color(0.95f, 0.18f, 0.12f, 0.38f));
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            var mouse = Mouse.current;
            if (keyboard == null || viewCamera == null || chassis == null || grid == null)
            {
                return;
            }

            if (keyboard.bKey.wasPressedThisFrame)
            {
                if (IsActive)
                {
                    ExitBuildMode();
                }
                else
                {
                    TryEnterBuildMode();
                }
                return;
            }

            if (!IsActive)
            {
                return;
            }

            if (heldModule == null)
            {
                if (mouse != null && mouse.leftButton.wasPressedThisFrame)
                {
                    TryPickTargetedModule();
                }
                return;
            }

            if (keyboard.rKey.wasPressedThisFrame)
            {
                quarterTurns = (quarterTurns + 1) % 4;
            }

            UpdateGhost();
            if (mouse != null && mouse.rightButton.wasPressedThisFrame)
            {
                CancelHeldModule();
            }
            else if (mouse != null && mouse.leftButton.wasPressedThisFrame && candidateValid)
            {
                PlaceHeldModule();
            }
        }

        public bool TryEnterBuildMode()
        {
            if (chassis == null || chassis.Speed > 0.45f)
            {
                return false;
            }

            IsActive = true;
            return true;
        }

        public bool TryHoldModule(CaravanModule module)
        {
            if (!IsActive
                || heldModule != null
                || module == null
                || !module.IsMovable
                || !grid.Remove(module, out previousPlacement))
            {
                return false;
            }

            heldModule = module;
            quarterTurns = previousPlacement.QuarterTurns;
            ghost = Instantiate(module.VisualRoot.gameObject);
            ghost.name = module.name + " Placement Ghost";
            SetGhostMaterial(validGhostMaterial);
            module.gameObject.SetActive(false);
            chassis.RefreshMassProperties();
            return true;
        }

        public bool TryPlaceHeldModule(CaravanGridPlacement placement)
        {
            if (heldModule == null || !grid.TryPlace(heldModule, placement))
            {
                return false;
            }

            heldModule.gameObject.SetActive(true);
            chassis.RefreshMassProperties();
            ClearGhostAndBuffer();
            return true;
        }

        private void TryPickTargetedModule()
        {
            if (!Physics.Raycast(
                    viewCamera.transform.position,
                    viewCamera.transform.forward,
                    out var hit,
                    8f,
                    ~0,
                    QueryTriggerInteraction.Collide))
            {
                return;
            }

            var module = hit.collider.GetComponentInParent<CaravanModule>();
            if (!TryHoldModule(module))
            {
                return;
            }
            UpdateGhost();
        }

        private void UpdateGhost()
        {
            if (heldModule == null || ghost == null)
            {
                return;
            }

            if (!TryRaycastBuildSurface(out var hit))
            {
                ghost.SetActive(false);
                candidateValid = false;
                return;
            }

            ghost.SetActive(true);
            candidate = grid.PlacementFromWorldPoint(hit.point, heldModule, quarterTurns);
            candidateValid = grid.CanPlace(heldModule, candidate);
            grid.GetWorldPose(candidate, out var position, out var rotation);
            ghost.transform.SetPositionAndRotation(position, rotation);
            SetGhostMaterial(candidateValid ? validGhostMaterial : invalidGhostMaterial);
        }

        private bool TryRaycastBuildSurface(out RaycastHit result)
        {
            var hits = Physics.RaycastAll(
                viewCamera.transform.position,
                viewCamera.transform.forward,
                12f,
                ~0,
                QueryTriggerInteraction.Collide);
            Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));
            for (var index = 0; index < hits.Length; index++)
            {
                if (hits[index].collider.GetComponentInParent<CaravanBuildSurface>() != null)
                {
                    result = hits[index];
                    return true;
                }
            }

            result = default;
            return false;
        }

        private void PlaceHeldModule()
        {
            TryPlaceHeldModule(candidate);
        }

        private void CancelHeldModule()
        {
            if (heldModule == null)
            {
                return;
            }

            grid.TryPlace(heldModule, previousPlacement);
            heldModule.gameObject.SetActive(true);
            chassis.RefreshMassProperties();
            ClearGhostAndBuffer();
        }

        public void ExitBuildMode()
        {
            if (heldModule != null)
            {
                CancelHeldModule();
            }

            IsActive = false;
        }

        private void ClearGhostAndBuffer()
        {
            if (ghost != null)
            {
                Destroy(ghost);
            }

            ghost = null;
            heldModule = null;
            candidateValid = false;
        }

        private void SetGhostMaterial(Material material)
        {
            if (ghost == null || material == null)
            {
                return;
            }

            foreach (var renderer in ghost.GetComponentsInChildren<Renderer>(true))
            {
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
        }

        private static Material CreateGhostMaterial(string name, Color color)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit")
                         ?? Shader.Find("Universal Render Pipeline/Simple Lit")
                         ?? Shader.Find("Standard");
            if (shader == null)
            {
                throw new InvalidOperationException("No supported shader for caravan build ghosts.");
            }

            var material = new Material(shader)
            {
                name = name,
                hideFlags = HideFlags.DontSave,
                renderQueue = (int)RenderQueue.Transparent
            };
            material.SetColor(material.HasProperty("_BaseColor") ? "_BaseColor" : "_Color", color);
            if (material.HasProperty("_Surface"))
            {
                material.SetFloat("_Surface", 1f);
                material.SetFloat("_Blend", 0f);
                material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                material.SetFloat("_ZWrite", 0f);
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            }
            return material;
        }

        private void OnDestroy()
        {
            if (validGhostMaterial != null)
            {
                Destroy(validGhostMaterial);
            }
            if (invalidGhostMaterial != null)
            {
                Destroy(invalidGhostMaterial);
            }
        }
    }
}
