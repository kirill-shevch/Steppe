using System;
using Steppe.Player;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace Steppe.Caravan
{
    public enum CaravanBuildInteractionMode
    {
        Modules = 0,
        Communications = 1,
        Electrical = Communications,
        Fluids = 2,
        Biomass = 3,
        Mechanical = 4
    }

    [DisallowMultipleComponent]
    public sealed class CaravanBuildModeController : MonoBehaviour
    {
        private Camera viewCamera;
        private CaravanFirstPersonController firstPerson;
        private CaravanChassisController chassis;
        private CaravanMountGrid grid;
        private CaravanElectricalNetwork electricalNetwork;
        private CaravanFluidNetwork fluidNetwork;
        private CaravanMaterialNetwork biomassNetwork;
        private CaravanMaterialNetwork mechanicalNetwork;
        private CaravanConstructionService construction;
        private CaravanModule heldModule;
        private CaravanElectricalPort selectedCommunicationPort;
        private CaravanElectricalPort focusedCommunicationPort;
        private CaravanFluidPort selectedFluidPort;
        private CaravanFluidPort focusedFluidPort;
        private CaravanMaterialPort selectedMaterialPort;
        private CaravanMaterialPort focusedMaterialPort;
        private CaravanGridPlacement previousPlacement;
        private GameObject ghost;
        private LineRenderer communicationPreview;
        private Material validGhostMaterial;
        private Material invalidGhostMaterial;
        private CaravanGridPlacement candidate;
        private bool candidateValid;
        private bool heldModuleIsNew;
        private int quarterTurns;
        private int selectedConstructionIndex;

        public bool IsActive { get; private set; }
        public CaravanModule HeldModule => heldModule;
        public bool CandidateValid => candidateValid;
        public CaravanBuildInteractionMode InteractionMode { get; private set; } =
            CaravanBuildInteractionMode.Modules;
        public bool IsCommunicationMode =>
            InteractionMode != CaravanBuildInteractionMode.Modules;
        public bool IsElectricalMode =>
            InteractionMode == CaravanBuildInteractionMode.Electrical;
        public bool IsFluidMode =>
            InteractionMode == CaravanBuildInteractionMode.Fluids;
        public bool IsBiomassMode =>
            InteractionMode == CaravanBuildInteractionMode.Biomass;
        public bool IsMechanicalMode =>
            InteractionMode == CaravanBuildInteractionMode.Mechanical;
        public bool IsMaterialMode => IsBiomassMode || IsMechanicalMode;
        public CaravanElectricalPort SelectedCommunicationPort =>
            selectedCommunicationPort;
        public CaravanFluidPort SelectedFluidPort => selectedFluidPort;
        public CaravanMaterialPort SelectedMaterialPort => selectedMaterialPort;
        public CaravanPartKind SelectedConstructionKind =>
            CaravanConstructionService.AvailablePartKinds[selectedConstructionIndex];

        public void Configure(
            Camera camera,
            CaravanFirstPersonController controller,
            CaravanChassisController caravan,
            CaravanMountGrid mountGrid,
            CaravanElectricalNetwork network,
            CaravanFluidNetwork fluids,
            CaravanMaterialNetwork biomass,
            CaravanMaterialNetwork mechanical,
            CaravanConstructionService constructionService)
        {
            viewCamera = camera != null ? camera : throw new ArgumentNullException(nameof(camera));
            firstPerson = controller != null ? controller : throw new ArgumentNullException(nameof(controller));
            chassis = caravan != null ? caravan : throw new ArgumentNullException(nameof(caravan));
            grid = mountGrid != null ? mountGrid : throw new ArgumentNullException(nameof(mountGrid));
            electricalNetwork = network != null
                ? network
                : throw new ArgumentNullException(nameof(network));
            fluidNetwork = fluids != null
                ? fluids
                : throw new ArgumentNullException(nameof(fluids));
            biomassNetwork = biomass != null
                ? biomass
                : throw new ArgumentNullException(nameof(biomass));
            mechanicalNetwork = mechanical != null
                ? mechanical
                : throw new ArgumentNullException(nameof(mechanical));
            construction = constructionService != null
                ? constructionService
                : throw new ArgumentNullException(nameof(constructionService));
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
                if (keyboard.qKey.wasPressedThisFrame)
                {
                    CycleConstructionSelection(-1);
                }
                else if (keyboard.eKey.wasPressedThisFrame)
                {
                    CycleConstructionSelection(1);
                }
                else if (keyboard.fKey.wasPressedThisFrame)
                {
                    TryCreateSelectedModule();
                }

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
            selectedFluidPort = null;
            focusedFluidPort = null;
            selectedMaterialPort = null;
            focusedMaterialPort = null;
            RefreshCommunicationPresentation(null, null);
            return true;
        }

        public bool ToggleCommunicationMode()
        {
            var next = InteractionMode switch
            {
                CaravanBuildInteractionMode.Modules =>
                    CaravanBuildInteractionMode.Electrical,
                CaravanBuildInteractionMode.Electrical =>
                    CaravanBuildInteractionMode.Fluids,
                CaravanBuildInteractionMode.Fluids =>
                    CaravanBuildInteractionMode.Biomass,
                CaravanBuildInteractionMode.Biomass =>
                    CaravanBuildInteractionMode.Mechanical,
                _ => CaravanBuildInteractionMode.Modules
            };
            return SetInteractionMode(next);
        }

        public bool SetInteractionMode(CaravanBuildInteractionMode mode)
        {
            if (!IsActive
                || (mode == CaravanBuildInteractionMode.Electrical
                    && electricalNetwork == null)
                || (mode == CaravanBuildInteractionMode.Fluids
                    && fluidNetwork == null)
                || (mode == CaravanBuildInteractionMode.Biomass
                    && biomassNetwork == null)
                || (mode == CaravanBuildInteractionMode.Mechanical
                    && mechanicalNetwork == null))
            {
                return false;
            }

            if (heldModule != null)
            {
                CancelHeldModule();
            }
            selectedCommunicationPort = null;
            focusedCommunicationPort = null;
            selectedFluidPort = null;
            focusedFluidPort = null;
            selectedMaterialPort = null;
            focusedMaterialPort = null;
            InteractionMode = mode;
            RefreshCommunicationPresentation(null, null);
            UpdateCommunicationPreview(null, null);
            return true;
        }

        public bool TrySelectCommunicationPort(CaravanElectricalPort port)
        {
            if (!IsActive
                || !IsElectricalMode
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
                RefreshCommunicationPresentation(port, null);
                UpdateCommunicationPreview(port, null);
                return true;
            }

            if (selectedCommunicationPort == port)
            {
                CancelCommunicationSelection();
                return true;
            }

            if (!electricalNetwork.TryConnect(selectedCommunicationPort, port))
            {
                RefreshCommunicationPresentation(port, null);
                UpdateCommunicationPreview(port, null);
                return false;
            }

            selectedCommunicationPort = null;
            RefreshCommunicationPresentation(port, null);
            UpdateCommunicationPreview(port, null);
            return true;
        }

        public int RemoveCommunicationConnections(CaravanElectricalPort port)
        {
            if (!IsActive
                || !IsElectricalMode
                || electricalNetwork == null)
            {
                return 0;
            }

            var removed = electricalNetwork.DisconnectPort(port);
            selectedCommunicationPort = null;
            RefreshCommunicationPresentation(port, null);
            UpdateCommunicationPreview(port, null);
            return removed;
        }

        public bool TrySelectFluidPort(CaravanFluidPort port)
        {
            if (!IsActive
                || !IsFluidMode
                || fluidNetwork == null
                || port == null)
            {
                return false;
            }

            if (selectedFluidPort == null)
            {
                if (port.IsAtCapacity || !ContainsFluidPort(port))
                {
                    return false;
                }

                selectedFluidPort = port;
                RefreshCommunicationPresentation(null, port);
                UpdateCommunicationPreview(null, port);
                return true;
            }

            if (selectedFluidPort == port)
            {
                CancelCommunicationSelection();
                return true;
            }

            if (!fluidNetwork.TryConnect(selectedFluidPort, port))
            {
                RefreshCommunicationPresentation(null, port);
                UpdateCommunicationPreview(null, port);
                return false;
            }

            selectedFluidPort = null;
            RefreshCommunicationPresentation(null, port);
            UpdateCommunicationPreview(null, port);
            return true;
        }

        public int RemoveFluidConnections(CaravanFluidPort port)
        {
            if (!IsActive || !IsFluidMode || fluidNetwork == null)
            {
                return 0;
            }

            var removed = fluidNetwork.DisconnectPort(port);
            selectedFluidPort = null;
            RefreshCommunicationPresentation(null, port);
            UpdateCommunicationPreview(null, port);
            return removed;
        }

        public bool TrySelectMaterialPort(CaravanMaterialPort port)
        {
            var network = ActiveMaterialNetwork;
            if (!IsActive
                || !IsMaterialMode
                || network == null
                || port == null)
            {
                return false;
            }

            focusedMaterialPort = port;
            if (selectedMaterialPort == null)
            {
                if (port.IsAtCapacity || !ContainsMaterialPort(network, port))
                {
                    return false;
                }

                selectedMaterialPort = port;
                RefreshCommunicationPresentation(null, null);
                UpdateCommunicationPreview(null, null);
                return true;
            }

            if (selectedMaterialPort == port)
            {
                CancelCommunicationSelection();
                return true;
            }

            if (!network.TryConnect(selectedMaterialPort, port))
            {
                RefreshCommunicationPresentation(null, null);
                UpdateCommunicationPreview(null, null);
                return false;
            }

            selectedMaterialPort = null;
            RefreshCommunicationPresentation(null, null);
            UpdateCommunicationPreview(null, null);
            return true;
        }

        public int RemoveMaterialConnections(CaravanMaterialPort port)
        {
            var network = ActiveMaterialNetwork;
            if (!IsActive || !IsMaterialMode || network == null)
            {
                return 0;
            }

            var removed = network.DisconnectPort(port);
            selectedMaterialPort = null;
            focusedMaterialPort = port;
            RefreshCommunicationPresentation(null, null);
            UpdateCommunicationPreview(null, null);
            return removed;
        }

        private CaravanMaterialNetwork ActiveMaterialNetwork =>
            IsBiomassMode
                ? biomassNetwork
                : IsMechanicalMode
                    ? mechanicalNetwork
                    : null;

        public void CycleConstructionSelection(int delta)
        {
            var count = CaravanConstructionService.AvailablePartKinds.Count;
            if (count <= 0)
            {
                selectedConstructionIndex = 0;
                return;
            }

            selectedConstructionIndex =
                (selectedConstructionIndex + delta % count + count) % count;
        }

        public bool TryCreateSelectedModule()
        {
            return TryCreateModule(SelectedConstructionKind);
        }

        public bool TryCreateModule(CaravanPartKind kind)
        {
            if (!IsActive
                || IsCommunicationMode
                || heldModule != null
                || construction == null
                || !construction.TryCreateBuffered(kind, out var module))
            {
                return false;
            }

            heldModule = module;
            heldModuleIsNew = true;
            previousPlacement = default;
            quarterTurns = 0;
            ghost = Instantiate(module.VisualRoot.gameObject);
            ghost.name = module.name + " Placement Ghost";
            SetGhostMaterial(validGhostMaterial);
            UpdateGhost();
            return true;
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
            heldModuleIsNew = false;
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

            var placedModule = heldModule;
            var wasNew = heldModuleIsNew;
            placedModule.gameObject.SetActive(true);
            if (wasNew)
            {
                construction.ActivatePlaced(placedModule);
            }
            else
            {
                chassis.RefreshMassProperties();
            }
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

            if (heldModuleIsNew)
            {
                construction.DestroyBuffered(heldModule);
            }
            else
            {
                grid.TryPlace(heldModule, previousPlacement);
                heldModule.gameObject.SetActive(true);
                chassis.RefreshMassProperties();
            }
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
            selectedFluidPort = null;
            focusedFluidPort = null;
            selectedMaterialPort = null;
            focusedMaterialPort = null;
            InteractionMode = CaravanBuildInteractionMode.Modules;
            RefreshCommunicationPresentation(null, null);
            UpdateCommunicationPreview(null, null);
            IsActive = false;
        }

        private void UpdateCommunicationMode(Mouse mouse)
        {
            focusedCommunicationPort = IsElectricalMode
                ? FindTargetedElectricalPort()
                : null;
            focusedFluidPort = IsFluidMode
                ? FindTargetedFluidPort()
                : null;
            focusedMaterialPort = IsMaterialMode
                ? FindTargetedMaterialPort()
                : null;
            RefreshCommunicationPresentation(
                focusedCommunicationPort,
                focusedFluidPort);
            UpdateCommunicationPreview(
                focusedCommunicationPort,
                focusedFluidPort);
            if (mouse == null)
            {
                return;
            }

            if (mouse.rightButton.wasPressedThisFrame)
            {
                if (selectedCommunicationPort != null
                    || selectedFluidPort != null
                    || selectedMaterialPort != null)
                {
                    CancelCommunicationSelection();
                }
                else if (IsElectricalMode && focusedCommunicationPort != null)
                {
                    RemoveCommunicationConnections(focusedCommunicationPort);
                }
                else if (IsFluidMode && focusedFluidPort != null)
                {
                    RemoveFluidConnections(focusedFluidPort);
                }
                else if (IsMaterialMode && focusedMaterialPort != null)
                {
                    RemoveMaterialConnections(focusedMaterialPort);
                }
            }
            else if (mouse.leftButton.wasPressedThisFrame)
            {
                if (IsElectricalMode && focusedCommunicationPort != null)
                {
                    TrySelectCommunicationPort(focusedCommunicationPort);
                }
                else if (IsFluidMode && focusedFluidPort != null)
                {
                    TrySelectFluidPort(focusedFluidPort);
                }
                else if (IsMaterialMode && focusedMaterialPort != null)
                {
                    TrySelectMaterialPort(focusedMaterialPort);
                }
            }
        }

        private CaravanElectricalPort FindTargetedElectricalPort()
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
            return FindClosestPort(
                module != null
                    ? module.GetComponentsInChildren<CaravanElectricalPort>(true)
                    : null,
                hit.point);
        }

        private CaravanFluidPort FindTargetedFluidPort()
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

            var directPort = hit.collider.GetComponentInParent<CaravanFluidPort>();
            if (directPort != null)
            {
                return directPort;
            }

            var module = hit.collider.GetComponentInParent<CaravanModule>();
            return FindClosestPort(
                module != null
                    ? module.GetComponentsInChildren<CaravanFluidPort>(true)
                    : null,
                hit.point);
        }

        private CaravanMaterialPort FindTargetedMaterialPort()
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

            var directPort =
                hit.collider.GetComponentInParent<CaravanMaterialPort>();
            if (directPort != null
                && directPort.NetworkKind
                == ActiveMaterialNetwork?.Kind)
            {
                return directPort;
            }

            var module = hit.collider.GetComponentInParent<CaravanModule>();
            var candidates = module != null
                ? module.GetComponentsInChildren<CaravanMaterialPort>(true)
                : null;
            if (candidates == null)
            {
                return null;
            }

            var matching = new System.Collections.Generic.List<CaravanMaterialPort>();
            for (var index = 0; index < candidates.Length; index++)
            {
                if (candidates[index].NetworkKind
                    == ActiveMaterialNetwork?.Kind)
                {
                    matching.Add(candidates[index]);
                }
            }
            return FindClosestPort(matching.ToArray(), hit.point);
        }

        private static TPort FindClosestPort<TPort>(
            TPort[] candidates,
            Vector3 point)
            where TPort : Component
        {
            if (candidates == null || candidates.Length == 0)
            {
                return null;
            }

            var closest = candidates[0];
            var closestDistance = Vector3.SqrMagnitude(
                closest.transform.position - point);
            for (var index = 1; index < candidates.Length; index++)
            {
                var distance = Vector3.SqrMagnitude(
                    candidates[index].transform.position - point);
                if (distance < closestDistance)
                {
                    closest = candidates[index];
                    closestDistance = distance;
                }
            }

            return closest;
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

        private bool ContainsFluidPort(CaravanFluidPort port)
        {
            for (var index = 0; index < fluidNetwork.Ports.Count; index++)
            {
                if (fluidNetwork.Ports[index] == port)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ContainsMaterialPort(
            CaravanMaterialNetwork network,
            CaravanMaterialPort port)
        {
            for (var index = 0; index < network.Ports.Count; index++)
            {
                if (network.Ports[index] == port)
                {
                    return true;
                }
            }
            return false;
        }

        private void CancelCommunicationSelection()
        {
            selectedCommunicationPort = null;
            selectedFluidPort = null;
            selectedMaterialPort = null;
            RefreshCommunicationPresentation(
                focusedCommunicationPort,
                focusedFluidPort);
            UpdateCommunicationPreview(
                focusedCommunicationPort,
                focusedFluidPort);
        }

        private void RefreshCommunicationPresentation(
            CaravanElectricalPort focusedElectrical,
            CaravanFluidPort focusedFluid)
        {
            if (electricalNetwork != null)
            {
                for (var index = 0; index < electricalNetwork.Ports.Count; index++)
                {
                    var port = electricalNetwork.Ports[index];
                    var selected = port == selectedCommunicationPort;
                    var compatible = selectedCommunicationPort == null
                        ? !port.IsAtCapacity
                        : selected || electricalNetwork.CanConnect(
                            selectedCommunicationPort,
                            port);
                    port.SetBuildPresentation(
                        IsActive && IsElectricalMode,
                        selected,
                        compatible,
                        port == focusedElectrical);
                }
            }

            if (fluidNetwork == null)
            {
                return;
            }

            for (var index = 0; index < fluidNetwork.Ports.Count; index++)
            {
                var port = fluidNetwork.Ports[index];
                var selected = port == selectedFluidPort;
                var compatible = selectedFluidPort == null
                    ? !port.IsAtCapacity
                    : selected || fluidNetwork.CanConnect(selectedFluidPort, port);
                port.SetBuildPresentation(
                    IsActive && IsFluidMode,
                    selected,
                    compatible,
                    port == focusedFluid);
            }

            RefreshMaterialNetworkPresentation(
                biomassNetwork,
                IsBiomassMode);
            RefreshMaterialNetworkPresentation(
                mechanicalNetwork,
                IsMechanicalMode);
        }

        private void RefreshMaterialNetworkPresentation(
            CaravanMaterialNetwork network,
            bool layerVisible)
        {
            if (network == null)
            {
                return;
            }

            for (var index = 0; index < network.Ports.Count; index++)
            {
                var port = network.Ports[index];
                var selected = port == selectedMaterialPort;
                var compatible = selectedMaterialPort == null
                    ? !port.IsAtCapacity
                    : selected || network.CanConnect(
                        selectedMaterialPort,
                        port);
                port.SetBuildPresentation(
                    IsActive && layerVisible,
                    selected,
                    compatible,
                    port == focusedMaterialPort);
            }
        }

        private void UpdateCommunicationPreview(
            CaravanElectricalPort focusedElectrical,
            CaravanFluidPort focusedFluid)
        {
            if (communicationPreview == null)
            {
                return;
            }

            var selectedTransform = IsElectricalMode
                ? selectedCommunicationPort?.transform
                : IsFluidMode
                    ? selectedFluidPort?.transform
                    : selectedMaterialPort?.transform;
            var focusedTransform = IsElectricalMode
                ? focusedElectrical?.transform
                : IsFluidMode
                    ? focusedFluid?.transform
                    : focusedMaterialPort?.transform;
            var visible = IsActive && IsCommunicationMode
                          && selectedTransform != null;
            communicationPreview.enabled = visible;
            if (!visible)
            {
                return;
            }

            var validTarget = IsElectricalMode
                ? focusedElectrical != null
                  && electricalNetwork.CanConnect(
                      selectedCommunicationPort,
                      focusedElectrical)
                : IsFluidMode
                    ? focusedFluid != null
                      && fluidNetwork.CanConnect(
                          selectedFluidPort,
                          focusedFluid)
                    : focusedMaterialPort != null
                      && ActiveMaterialNetwork != null
                      && ActiveMaterialNetwork.CanConnect(
                          selectedMaterialPort,
                          focusedMaterialPort);
            var ray = new Ray(viewCamera.transform.position, viewCamera.transform.forward);
            var end = ray.GetPoint(6f);
            if (focusedTransform != null)
            {
                end = focusedTransform.position;
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
            var start = selectedTransform.position;
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
            heldModuleIsNew = false;
            candidateValid = false;
        }

        private void OnGUI()
        {
            if (!IsActive)
            {
                return;
            }

            GUILayout.BeginArea(new Rect(18f, 18f, 390f, 126f), GUI.skin.box);
            GUILayout.Label($"Caravan construction — {InteractionMode}");
            if (InteractionMode == CaravanBuildInteractionMode.Modules)
            {
                var definition = CaravanPartCatalog.Get(SelectedConstructionKind);
                GUILayout.Label($"Blueprint: {definition.DisplayName}");
                GUILayout.Label(heldModule == null
                    ? "Q/E select  •  F construct  •  LMB move"
                    : "R rotate  •  LMB place  •  RMB cancel");
            }
            else if (IsElectricalMode)
            {
                GUILayout.Label("LMB connect power ports  •  RMB cancel/remove");
            }
            else if (IsFluidMode)
            {
                GUILayout.Label("LMB connect fluid ports  •  RMB cancel/remove");
            }
            else if (IsBiomassMode)
            {
                GUILayout.Label("LMB connect biomass ports  •  RMB cancel/remove");
            }
            else
            {
                GUILayout.Label("LMB connect drive ports  •  RMB cancel/remove");
            }
            GUILayout.Label("Tab changes layer  •  B exits");
            GUILayout.EndArea();
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
