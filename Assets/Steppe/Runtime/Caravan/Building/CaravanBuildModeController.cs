using System;
using Steppe.Player;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace Steppe.Caravan
{
    public enum CaravanBuildInteractionMode
    {
        Modules,
        Communications
    }

    [DisallowMultipleComponent]
    public sealed class CaravanBuildModeController : MonoBehaviour
    {
        private Camera viewCamera;
        private CaravanFirstPersonController firstPerson;
        private CaravanChassisController chassis;
        private CaravanMountGrid grid;
        private CaravanElectricalNetwork electricalNetwork;
        private CaravanModule heldModule;
        private CaravanElectricalPort selectedCommunicationPort;
        private CaravanElectricalPort focusedCommunicationPort;
        private CaravanGridPlacement previousPlacement;
        private GameObject ghost;
        private LineRenderer communicationPreview;
        private Material validGhostMaterial;
        private Material invalidGhostMaterial;
        private CaravanGridPlacement candidate;
        private bool candidateValid;
        private int quarterTurns;

        public bool IsActive { get; private set; }
        public CaravanModule HeldModule => heldModule;
        public bool CandidateValid => candidateValid;
        public CaravanBuildInteractionMode InteractionMode { get; private set; } =
            CaravanBuildInteractionMode.Modules;
        public bool IsCommunicationMode =>
            InteractionMode == CaravanBuildInteractionMode.Communications;
        public CaravanElectricalPort SelectedCommunicationPort =>
            selectedCommunicationPort;

        public void Configure(
            Camera camera,
            CaravanFirstPersonController controller,
            CaravanChassisController caravan,
            CaravanMountGrid mountGrid,
            CaravanElectricalNetwork network)
        {
            viewCamera = camera != null ? camera : throw new ArgumentNullException(nameof(camera));
            firstPerson = controller != null ? controller : throw new ArgumentNullException(nameof(controller));
            chassis = caravan != null ? caravan : throw new ArgumentNullException(nameof(caravan));
            grid = mountGrid != null ? mountGrid : throw new ArgumentNullException(nameof(mountGrid));
            electricalNetwork = network != null
                ? network
                : throw new ArgumentNullException(nameof(network));
            validGhostMaterial = CreateGhostMaterial(
                "Caravan Valid Placement",
                new Color(0.15f, 0.92f, 0.48f, 0.38f));
            invalidGhostMaterial = CreateGhostMaterial(
                "Caravan Invalid Placement",
                new Color(0.95f, 0.18f, 0.12f, 0.38f));
            communicationPreview = CreateCommunicationPreview();
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

            if (keyboard.tabKey.wasPressedThisFrame)
            {
                ToggleCommunicationMode();
                return;
            }

            if (IsCommunicationMode)
            {
                UpdateCommunicationMode(mouse);
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
            InteractionMode = CaravanBuildInteractionMode.Modules;
            selectedCommunicationPort = null;
            focusedCommunicationPort = null;
            RefreshCommunicationPresentation(null);
            return true;
        }

        public bool ToggleCommunicationMode()
        {
            return SetInteractionMode(IsCommunicationMode
                ? CaravanBuildInteractionMode.Modules
                : CaravanBuildInteractionMode.Communications);
        }

        public bool SetInteractionMode(CaravanBuildInteractionMode mode)
        {
            if (!IsActive
                || (mode == CaravanBuildInteractionMode.Communications
                    && electricalNetwork == null))
            {
                return false;
            }

            if (heldModule != null)
            {
                CancelHeldModule();
            }
            selectedCommunicationPort = null;
            focusedCommunicationPort = null;
            InteractionMode = mode;
            RefreshCommunicationPresentation(null);
            UpdateCommunicationPreview(null);
            return true;
        }

        public bool TrySelectCommunicationPort(CaravanElectricalPort port)
        {
            if (!IsActive
                || !IsCommunicationMode
                || electricalNetwork == null
                || port == null)
            {
                return false;
            }

            if (selectedCommunicationPort == null)
            {
                if (port.IsAtCapacity || !ContainsElectricalPort(port))
                {
                    return false;
                }

                selectedCommunicationPort = port;
                RefreshCommunicationPresentation(port);
                UpdateCommunicationPreview(port);
                return true;
            }

            if (selectedCommunicationPort == port)
            {
                CancelCommunicationSelection();
                return true;
            }

            if (!electricalNetwork.TryConnect(selectedCommunicationPort, port))
            {
                RefreshCommunicationPresentation(port);
                UpdateCommunicationPreview(port);
                return false;
            }

            selectedCommunicationPort = null;
            RefreshCommunicationPresentation(port);
            UpdateCommunicationPreview(port);
            return true;
        }

        public int RemoveCommunicationConnections(CaravanElectricalPort port)
        {
            if (!IsActive
                || !IsCommunicationMode
                || electricalNetwork == null)
            {
                return 0;
            }

            var removed = electricalNetwork.DisconnectPort(port);
            selectedCommunicationPort = null;
            RefreshCommunicationPresentation(port);
            UpdateCommunicationPreview(port);
            return removed;
        }

        public bool TryHoldModule(CaravanModule module)
        {
            if (!IsActive
                || IsCommunicationMode
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
                    CaravanFirstPersonController.WorldQueryMask,
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
                CaravanFirstPersonController.WorldQueryMask,
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

            selectedCommunicationPort = null;
            focusedCommunicationPort = null;
            InteractionMode = CaravanBuildInteractionMode.Modules;
            RefreshCommunicationPresentation(null);
            UpdateCommunicationPreview(null);
            IsActive = false;
        }

        private void UpdateCommunicationMode(Mouse mouse)
        {
            focusedCommunicationPort = FindTargetedCommunicationPort();
            RefreshCommunicationPresentation(focusedCommunicationPort);
            UpdateCommunicationPreview(focusedCommunicationPort);
            if (mouse == null)
            {
                return;
            }

            if (mouse.rightButton.wasPressedThisFrame)
            {
                if (selectedCommunicationPort != null)
                {
                    CancelCommunicationSelection();
                }
                else if (focusedCommunicationPort != null)
                {
                    RemoveCommunicationConnections(focusedCommunicationPort);
                }
            }
            else if (mouse.leftButton.wasPressedThisFrame
                     && focusedCommunicationPort != null)
            {
                TrySelectCommunicationPort(focusedCommunicationPort);
            }
        }

        private CaravanElectricalPort FindTargetedCommunicationPort()
        {
            if (!Physics.Raycast(
                    viewCamera.transform.position,
                    viewCamera.transform.forward,
                    out var hit,
                    8f,
                    CaravanFirstPersonController.WorldQueryMask,
                    QueryTriggerInteraction.Collide))
            {
                return null;
            }

            var directPort = hit.collider.GetComponentInParent<CaravanElectricalPort>();
            if (directPort != null)
            {
                return directPort;
            }

            var module = hit.collider.GetComponentInParent<CaravanModule>();
            return module != null
                ? module.GetComponentInChildren<CaravanElectricalPort>(true)
                : null;
        }

        private bool ContainsElectricalPort(CaravanElectricalPort port)
        {
            for (var index = 0; index < electricalNetwork.Ports.Count; index++)
            {
                if (electricalNetwork.Ports[index] == port)
                {
                    return true;
                }
            }

            return false;
        }

        private void CancelCommunicationSelection()
        {
            selectedCommunicationPort = null;
            RefreshCommunicationPresentation(focusedCommunicationPort);
            UpdateCommunicationPreview(focusedCommunicationPort);
        }

        private void RefreshCommunicationPresentation(CaravanElectricalPort focusedPort)
        {
            if (electricalNetwork == null)
            {
                return;
            }

            for (var index = 0; index < electricalNetwork.Ports.Count; index++)
            {
                var port = electricalNetwork.Ports[index];
                var selected = port == selectedCommunicationPort;
                var compatible = selectedCommunicationPort == null
                    ? !port.IsAtCapacity
                    : selected || electricalNetwork.CanConnect(selectedCommunicationPort, port);
                port.SetBuildPresentation(
                    IsActive && IsCommunicationMode,
                    selected,
                    compatible,
                    port == focusedPort);
            }
        }

        private void UpdateCommunicationPreview(CaravanElectricalPort focusedPort)
        {
            if (communicationPreview == null)
            {
                return;
            }

            var visible = IsActive
                          && IsCommunicationMode
                          && selectedCommunicationPort != null;
            communicationPreview.enabled = visible;
            if (!visible)
            {
                return;
            }

            var validTarget = focusedPort != null
                              && electricalNetwork.CanConnect(
                                  selectedCommunicationPort,
                                  focusedPort);
            var ray = new Ray(viewCamera.transform.position, viewCamera.transform.forward);
            var end = ray.GetPoint(6f);
            if (focusedPort != null)
            {
                end = focusedPort.transform.position;
            }
            else if (Physics.Raycast(
                         ray,
                         out var hit,
                         8f,
                         CaravanFirstPersonController.WorldQueryMask,
                         QueryTriggerInteraction.Collide))
            {
                end = hit.point;
            }

            communicationPreview.sharedMaterial =
                validTarget ? validGhostMaterial : invalidGhostMaterial;
            var start = selectedCommunicationPort.transform.position;
            var up = chassis.transform.up * 0.28f;
            communicationPreview.SetPosition(0, start);
            communicationPreview.SetPosition(1, start + up);
            communicationPreview.SetPosition(2, end + up);
            communicationPreview.SetPosition(3, end);
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

        private LineRenderer CreateCommunicationPreview()
        {
            var previewObject = new GameObject("Communication Cable Preview");
            previewObject.transform.SetParent(transform, false);
            var line = previewObject.AddComponent<LineRenderer>();
            line.sharedMaterial = invalidGhostMaterial;
            line.useWorldSpace = true;
            line.positionCount = 4;
            line.widthMultiplier = 0.07f;
            line.numCapVertices = 4;
            line.numCornerVertices = 3;
            line.textureMode = LineTextureMode.Stretch;
            line.alignment = LineAlignment.View;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.enabled = false;
            return line;
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
