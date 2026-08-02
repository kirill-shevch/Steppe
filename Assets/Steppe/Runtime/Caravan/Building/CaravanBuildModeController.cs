using System;
using Steppe.Player;
using Steppe.Presentation;
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
        private CaravanPlatformController platform;
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
        private GameObject platformGhost;
        private LineRenderer communicationPreview;
        private Material validGhostMaterial;
        private Material invalidGhostMaterial;
        private CaravanGridPlacement candidate;
        private bool candidateValid;
        private CaravanGridCell platformCandidate;
        private CaravanGridCell[] platformCandidateLine =
            Array.Empty<CaravanGridCell>();
        private bool platformCandidateValid;
        private bool platformExpansionMode;
        private bool heldModuleIsNew;
        private int quarterTurns;
        private int selectedConstructionIndex;
        private float feedbackUntil;
        private string feedbackMessage;
        private bool feedbackIsError;
        private GUIStyle buildDescriptionStyle;

        public bool IsActive { get; private set; }
        public CaravanModule HeldModule => heldModule;
        public bool CandidateValid => platformExpansionMode
            ? platformCandidateValid
            : candidateValid;
        public bool IsPlatformExpansionMode => platformExpansionMode;
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
            construction != null && construction.KnownPartCount > 0
                ? construction.GetKnownPartKind(selectedConstructionIndex)
                : CaravanPartKind.Sail;
        public string FeedbackMessage => feedbackMessage;
        public bool FeedbackIsError => feedbackIsError;
        public bool FeedbackVisible =>
            !string.IsNullOrWhiteSpace(feedbackMessage)
            && UnityEngine.Time.unscaledTime < feedbackUntil;

        public void Configure(
            Camera camera,
            CaravanFirstPersonController controller,
            CaravanChassisController caravan,
            CaravanMountGrid mountGrid,
            CaravanPlatformController platformController,
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
            platform = platformController != null
                ? platformController
                : throw new ArgumentNullException(nameof(platformController));
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
                CancelPlatformExpansion();
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
                if (platformExpansionMode)
                {
                    UpdatePlatformExpansion();
                    if (keyboard.gKey.wasPressedThisFrame
                        || (mouse != null
                            && mouse.rightButton.wasPressedThisFrame))
                    {
                        CancelPlatformExpansion();
                    }
                    else if (mouse != null
                             && mouse.leftButton.wasPressedThisFrame
                             && platformCandidateValid)
                    {
                        TryExpandPlatformCell(
                            platformCandidate.X,
                            platformCandidate.Z);
                    }
                    return;
                }

                if (keyboard.gKey.wasPressedThisFrame)
                {
                    BeginPlatformExpansion();
                    return;
                }
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
                else if ((mouse != null
                          && mouse.rightButton.wasPressedThisFrame)
                         || keyboard.xKey.wasPressedThisFrame
                         || keyboard.deleteKey.wasPressedThisFrame)
                {
                    TryRemoveTargetedModule();
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
            if (chassis == null)
            {
                SetFeedback("Строительство пока недоступно", true);
                return false;
            }
            if (chassis.Speed > 0.45f)
            {
                SetFeedback(
                    $"Сначала остановите караван — сейчас {chassis.Speed:F1} м/с",
                    true);
                return false;
            }

            IsActive = true;
            firstPerson.SetBuildControl(true);
            InteractionMode = CaravanBuildInteractionMode.Modules;
            selectedCommunicationPort = null;
            focusedCommunicationPort = null;
            selectedFluidPort = null;
            focusedFluidPort = null;
            selectedMaterialPort = null;
            focusedMaterialPort = null;
            RefreshCommunicationPresentation(null, null);
            SetFeedback("Караван зафиксирован. Выберите модуль или слой сети.", false);
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
            CancelPlatformExpansion();
            selectedCommunicationPort = null;
            focusedCommunicationPort = null;
            selectedFluidPort = null;
            focusedFluidPort = null;
            selectedMaterialPort = null;
            focusedMaterialPort = null;
            InteractionMode = mode;
            RefreshCommunicationPresentation(null, null);
            UpdateCommunicationPreview(null, null);
            SetFeedback(
                mode switch
                {
                    CaravanBuildInteractionMode.Modules => "Монтаж модулей",
                    CaravanBuildInteractionMode.Electrical => "Электрическая сеть",
                    CaravanBuildInteractionMode.Fluids => "Жидкостный контур",
                    CaravanBuildInteractionMode.Biomass => "Поток биомассы",
                    _ => "Механический привод"
                },
                false);
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
                if (!ContainsElectricalPort(port))
                {
                    SetFeedback("Этот электрический порт недоступен", true);
                    return false;
                }
                if (port.IsAtCapacity)
                {
                    SetFeedback(
                        $"Нет свободных контактов: {port.ConnectedCableCount}/{port.MaximumConnections}",
                        true);
                    return false;
                }

                selectedCommunicationPort = port;
                SetFeedback("Выберите совместимый второй порт", false);
                RefreshCommunicationPresentation(port, null);
                UpdateCommunicationPreview(port, null);
                return true;
            }

            if (selectedCommunicationPort == port)
            {
                CancelCommunicationSelection();
                return true;
            }

            if (port.IsAtCapacity)
            {
                SetFeedback(
                    $"Нет свободных контактов: {port.ConnectedCableCount}/{port.MaximumConnections}",
                    true);
                RefreshCommunicationPresentation(port, null);
                UpdateCommunicationPreview(port, null);
                return false;
            }

            if (!electricalNetwork.TryConnect(selectedCommunicationPort, port))
            {
                SetFeedback("Эти электрические порты несовместимы", true);
                RefreshCommunicationPresentation(port, null);
                UpdateCommunicationPreview(port, null);
                return false;
            }

            selectedCommunicationPort = null;
            SetFeedback("Электрическое соединение создано", false);
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
            SetFeedback(
                removed > 0
                    ? "Электрическое соединение удалено"
                    : "У этого порта нет соединений",
                removed == 0);
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
                    SetFeedback("Этот жидкостный порт уже занят", true);
                    return false;
                }

                selectedFluidPort = port;
                SetFeedback("Выберите совместимый второй порт", false);
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
                SetFeedback("Эти жидкостные порты несовместимы", true);
                RefreshCommunicationPresentation(null, port);
                UpdateCommunicationPreview(null, port);
                return false;
            }

            selectedFluidPort = null;
            SetFeedback("Труба проложена", false);
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
            SetFeedback(
                removed > 0 ? "Труба удалена" : "У этого порта нет труб",
                removed == 0);
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
                    SetFeedback("Этот порт уже занят", true);
                    return false;
                }

                selectedMaterialPort = port;
                SetFeedback("Выберите совместимый второй порт", false);
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
                SetFeedback("Эти порты несовместимы в текущем слое", true);
                RefreshCommunicationPresentation(null, null);
                UpdateCommunicationPreview(null, null);
                return false;
            }

            selectedMaterialPort = null;
            SetFeedback("Соединение создано", false);
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
            SetFeedback(
                removed > 0 ? "Соединение удалено" : "У этого порта нет связей",
                removed == 0);
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
            var count = construction != null
                ? construction.KnownPartCount
                : 0;
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
                if (IsActive && construction != null)
                {
                    SetFeedback(
                        $"{CaravanPartCatalog.Get(kind).DisplayName}: {construction.GetConstructionStatus(kind)}",
                        true);
                }
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
            SetFeedback($"Создано в буфере: {module.name}", false);
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
            SetFeedback($"Перемещение: {module.name}", false);
            return true;
        }

        public bool TryPlaceHeldModule(CaravanGridPlacement placement)
        {
            if (heldModule == null || !grid.CanPlace(heldModule, placement))
            {
                return false;
            }

            var placedModule = heldModule;
            var wasNew = heldModuleIsNew;
            var paid = false;
            if (wasNew)
            {
                paid = construction.TryPayFor(placedModule);
                if (!paid)
                {
                    SetFeedback("Не хватает ресурсов для строительства", true);
                    return false;
                }
            }
            if (!grid.TryPlace(heldModule, placement))
            {
                if (paid)
                {
                    construction.Refund(placedModule);
                }
                return false;
            }
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
            SetFeedback(
                wasNew
                    ? $"Установлено: {placedModule.name}"
                    : $"Перемещено: {placedModule.name}",
                false);
            return true;
        }

        public bool BeginPlatformExpansion()
        {
            if (!IsActive
                || IsCommunicationMode
                || heldModule != null
                || platform == null)
            {
                return false;
            }

            platformExpansionMode = true;
            if (platformGhost == null)
            {
                platformGhost = GameObject.CreatePrimitive(PrimitiveType.Cube);
                platformGhost.name = "Platform Expansion Ghost";
                var collider = platformGhost.GetComponent<Collider>();
                if (collider != null)
                {
                    Destroy(collider);
                }
            }
            platformGhost.SetActive(false);
            SetFeedback(
                "Расширение платформы: выберите внешний край для целой линии",
                false);
            return true;
        }

        public bool TryExpandPlatformCell(int x, int z)
        {
            var lineLength = platform != null
                             && platform.TryGetExpansionLine(x, z, out var line)
                ? line.Length
                : 0;
            var cost = platform != null
                ? platform.FormatExpansionCost(x, z)
                : string.Empty;
            if (!IsActive
                || !platformExpansionMode
                || platform == null
                || !platform.TryExpand(x, z))
            {
                SetFeedback(
                    lineLength > 0
                        ? $"Не хватает ресурсов на линию: {cost}"
                        : "Выберите свободный внешний край платформы",
                    true);
                return false;
            }

            SetFeedback(
                $"Построена линия: {lineLength} клеток  •  колёса перенесены на край",
                false);
            UpdatePlatformExpansion();
            return true;
        }

        private void UpdatePlatformExpansion()
        {
            if (!platformExpansionMode || platformGhost == null)
            {
                return;
            }

            var plane = new Plane(grid.transform.up, grid.transform.position);
            var ray = new Ray(
                viewCamera.transform.position,
                viewCamera.transform.forward);
            if (!plane.Raycast(ray, out var distance)
                || distance < 0f
                || distance > 12f)
            {
                platformCandidateValid = false;
                platformGhost.SetActive(false);
                return;
            }

            platformCandidate = grid.CellFromWorldPoint(ray.GetPoint(distance));
            var hasLine = platform.TryGetExpansionLine(
                platformCandidate.X,
                platformCandidate.Z,
                out platformCandidateLine);
            platformCandidateValid = hasLine
                                     && platform.CanExpand(
                                         platformCandidate.X,
                                         platformCandidate.Z);
            var first = hasLine
                ? platformCandidateLine[0]
                : platformCandidate;
            var last = hasLine
                ? platformCandidateLine[platformCandidateLine.Length - 1]
                : platformCandidate;
            grid.GetWorldCellPose(
                first.X,
                first.Z,
                out var firstPosition,
                out var rotation);
            grid.GetWorldCellPose(
                last.X,
                last.Z,
                out var lastPosition,
                out _);
            platformGhost.transform.SetPositionAndRotation(
                (firstPosition + lastPosition) * 0.5f
                - grid.transform.up * 0.055f,
                rotation);
            var cellWidth = Mathf.Abs(last.X - first.X) + 1;
            var cellLength = Mathf.Abs(last.Z - first.Z) + 1;
            platformGhost.transform.localScale = new Vector3(
                cellWidth * grid.CellSize - grid.CellSize * 0.06f,
                0.11f,
                cellLength * grid.CellSize - grid.CellSize * 0.06f);
            platformGhost.SetActive(true);
            foreach (var renderer in
                     platformGhost.GetComponentsInChildren<Renderer>(true))
            {
                renderer.sharedMaterial = platformCandidateValid
                    ? validGhostMaterial
                    : invalidGhostMaterial;
            }
        }

        private void CancelPlatformExpansion()
        {
            platformExpansionMode = false;
            platformCandidateValid = false;
            platformCandidateLine = Array.Empty<CaravanGridCell>();
            if (platformGhost != null)
            {
                platformGhost.SetActive(false);
            }
        }

        public bool TryRemoveModule(CaravanModule module)
        {
            if (!IsActive
                || IsCommunicationMode
                || heldModule != null
                || construction == null
                || module == null)
            {
                return false;
            }
            if (!module.IsMovable)
            {
                SetFeedback("Этот элемент нельзя разобрать", true);
                return false;
            }

            var moduleName = module.name;
            var part = module.GetComponent<CaravanPart>();
            var returned = part != null
                ? construction.FormatCost(part.Kind)
                : string.Empty;
            if (!construction.TryDestroyPlaced(module, grid))
            {
                SetFeedback("Не удалось разобрать выбранный модуль", true);
                return false;
            }

            SetFeedback(
                string.IsNullOrWhiteSpace(returned)
                    ? $"Разобрано: {moduleName}"
                    : $"Разобрано: {moduleName}  •  в ящик: {returned}",
                false);
            return true;
        }

        private void TryPickTargetedModule()
        {
            var module = FindTargetedModule();
            if (module == null)
            {
                return;
            }

            if (!TryHoldModule(module))
            {
                return;
            }
            UpdateGhost();
        }

        private void TryRemoveTargetedModule()
        {
            var module = FindTargetedModule();
            if (module == null)
            {
                SetFeedback("Наведитесь на модуль, который нужно разобрать", true);
                return;
            }

            TryRemoveModule(module);
        }

        private CaravanModule FindTargetedModule()
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

            return hit.collider.GetComponentInParent<CaravanModule>();
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
            SetFeedback("Действие отменено", false);
        }

        public void ExitBuildMode()
        {
            CancelPlatformExpansion();
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
            firstPerson?.SetBuildControl(false);
            SetFeedback("Строительство завершено", false);
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

            var previousMatrix = SteppeGuiScale.Begin();
            try
            {
                buildDescriptionStyle ??= new GUIStyle(GUI.skin.label)
                {
                    wordWrap = true
                };
                GUILayout.BeginArea(
                    new Rect(18f, 18f, 430f, 190f),
                    GUI.skin.box);
                GUILayout.Label(
                    $"Строительство каравана — {InteractionModeName()}");
                if (InteractionMode == CaravanBuildInteractionMode.Modules)
                {
                    if (platformExpansionMode)
                    {
                        GUILayout.Label("Расширение платформы");
                        if (platformCandidateLine.Length > 0)
                        {
                            GUILayout.Label(
                                $"Линия: {platformCandidateLine.Length} клеток");
                            GUILayout.Label(
                                $"Цена: {platform.FormatExpansionCost(platformCandidate.X, platformCandidate.Z)}");
                        }
                        else
                        {
                            GUILayout.Label("Выберите внешний край платформы");
                        }
                        GUILayout.Label("ЛКМ — построить  •  ПКМ / G — отменить");
                        GUILayout.Label("B — выйти");
                        GUILayout.EndArea();
                        return;
                    }
                    var definition = CaravanPartCatalog.Get(
                        SelectedConstructionKind);
                    GUILayout.Label($"Модуль: {definition.DisplayName}");
                    GUILayout.Label(
                        $"{construction.GetConstructionStatus(SelectedConstructionKind)}  •  {construction.FormatCost(SelectedConstructionKind)}");
                    GUILayout.Label(
                        definition.Description,
                        buildDescriptionStyle);
                    GUILayout.Label(heldModule == null
                        ? "Q/E — выбрать  •  F — создать  •  G — расширить платформу  •  ЛКМ — переставить"
                        : "R — повернуть  •  ЛКМ — установить  •  ПКМ — отменить");
                    if (heldModule == null)
                    {
                        GUILayout.Label(
                            "ПКМ / X / Delete — разобрать модуль под прицелом");
                    }
                }
                else if (IsElectricalMode)
                {
                    GUILayout.Label("ЛКМ — соединить электрические порты  •  ПКМ — отменить или удалить");
                }
                else if (IsFluidMode)
                {
                    GUILayout.Label("ЛКМ — соединить жидкостные порты  •  ПКМ — отменить или удалить");
                }
                else if (IsBiomassMode)
                {
                    GUILayout.Label("ЛКМ — соединить порты биомассы  •  ПКМ — отменить или удалить");
                }
                else
                {
                    GUILayout.Label("ЛКМ — соединить механические порты  •  ПКМ — отменить или удалить");
                }
                GUILayout.Label("Tab — сменить слой  •  B — выйти");
                GUILayout.EndArea();
            }
            finally
            {
                SteppeGuiScale.End(previousMatrix);
            }
        }

        private string InteractionModeName()
        {
            return InteractionMode switch
            {
                CaravanBuildInteractionMode.Modules => "модули",
                CaravanBuildInteractionMode.Communications => "электричество",
                CaravanBuildInteractionMode.Fluids => "жидкости",
                CaravanBuildInteractionMode.Biomass => "биомасса",
                CaravanBuildInteractionMode.Mechanical => "механика",
                _ => "неизвестный слой"
            };
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
            if (platformGhost != null)
            {
                Destroy(platformGhost);
            }
            if (validGhostMaterial != null)
            {
                Destroy(validGhostMaterial);
            }
            if (invalidGhostMaterial != null)
            {
                Destroy(invalidGhostMaterial);
            }
        }

        private void OnDisable()
        {
            if (IsActive)
            {
                ExitBuildMode();
            }
        }

        private void SetFeedback(
            string message,
            bool isError,
            float duration = 2.2f)
        {
            feedbackMessage = message;
            feedbackIsError = isError;
            feedbackUntil = UnityEngine.Time.unscaledTime
                            + Mathf.Max(0.1f, duration);
        }
    }
}
