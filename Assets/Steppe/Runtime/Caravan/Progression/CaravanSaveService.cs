using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Steppe.Player;
using Steppe.World;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Steppe.Caravan
{
    [Serializable]
    public sealed class CaravanGridCellRecord
    {
        public int x;
        public int z;
    }

    [Serializable]
    public sealed class CaravanModuleRecord
    {
        public string instanceId;
        public CaravanPartKind kind;
        public int x;
        public int z;
        public int quarterTurns;
        public float dust;
        public float integrity = 1f;
        public float load;
        public float storedAmount;
        public float temperatureCelsius = 18f;
    }

    [Serializable]
    public sealed class CaravanConnectionRecord
    {
        public string startPortId;
        public string endPortId;
    }

    [Serializable]
    public sealed class CaravanSaveSnapshot
    {
        public int schemaVersion = CaravanSaveService.CurrentSchemaVersion;
        public CaravanProgressionSnapshot progression;
        public CaravanGridCellRecord[] platformCells =
            Array.Empty<CaravanGridCellRecord>();
        public CaravanModuleRecord[] modules =
            Array.Empty<CaravanModuleRecord>();
        public CaravanConnectionRecord[] electricalConnections =
            Array.Empty<CaravanConnectionRecord>();
        public CaravanConnectionRecord[] fluidConnections =
            Array.Empty<CaravanConnectionRecord>();
        public CaravanConnectionRecord[] biomassConnections =
            Array.Empty<CaravanConnectionRecord>();
        public CaravanConnectionRecord[] mechanicalConnections =
            Array.Empty<CaravanConnectionRecord>();
        public double caravanWorldX;
        public double caravanWorldZ;
        public float caravanWorldY;
        public float rotationX;
        public float rotationY;
        public float rotationZ;
        public float rotationW = 1f;
        public float travelledMetres;
        public bool hasPlayerState;
        public double playerWorldX;
        public double playerWorldZ;
        public float playerWorldY;
        public float playerYaw;
        public float playerPitch;
    }

    /// <summary>
    /// Durable P18 save/load boundary. It serializes progression, the connected
    /// platform shape, placed modules and every player-authored network link.
    /// Restore paths bypass construction payment but use the same factories and
    /// registration code as normal building.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CaravanSaveService : MonoBehaviour
    {
        public const int CurrentSchemaVersion = 1;
        public const string DefaultFileName = "steppe-caravan-save.json";
        public const string DefaultAutosaveFileName =
            "steppe-caravan-autosave.json";
        public const float AutosaveDebounceSeconds = 3f;
        public const float MinimumAutosaveIntervalSeconds = 12f;
        public const float PeriodicAutosaveIntervalSeconds = 90f;

        private FloatingOriginSystem floatingOrigin;
        private CaravanChassisController chassis;
        private CaravanMountGrid grid;
        private CaravanPlatformController platform;
        private CaravanConstructionService construction;
        private CaravanProgressionSystem progression;
        private CaravanElectricalNetwork electrical;
        private CaravanFluidNetwork fluid;
        private CaravanMaterialNetwork biomass;
        private CaravanMaterialNetwork mechanical;
        private CaravanProgressionWorldRig progressionWorld;
        private CaravanProgressionDirector progressionDirector;
        private CaravanBuildModeController buildMode;
        private CaravanFirstPersonController player;
        private float feedbackUntil;
        private float autosaveDueAt;
        private float lastAutosaveAt = float.NegativeInfinity;
        private float nextPeriodicAutosaveAt;
        private bool autosavePending;
        private bool isRestoring;
        private bool subscribedToChanges;

        public string SavePath { get; private set; }
        public string AutosavePath { get; private set; }
        public string FeedbackMessage { get; private set; }
        public bool FeedbackIsError { get; private set; }
        public bool AutosavePending => autosavePending;
        public bool LastLoadWasAutosave { get; private set; }
        public bool LastLoadWasBackup { get; private set; }
        public bool FeedbackVisible =>
            !string.IsNullOrWhiteSpace(FeedbackMessage)
            && UnityEngine.Time.unscaledTime < feedbackUntil;
        public bool HasManualSave => Exists(SavePath);
        public bool HasAutosave => Exists(AutosavePath);
        public bool HasSave => HasManualSave
                               || HasAutosave
                               || Exists(BackupPath(SavePath))
                               || Exists(BackupPath(AutosavePath));

        public void Configure(
            FloatingOriginSystem origin,
            CaravanChassisController caravan,
            CaravanMountGrid mountGrid,
            CaravanPlatformController platformController,
            CaravanConstructionService constructionService,
            CaravanProgressionSystem progressionSystem,
            CaravanElectricalNetwork electricalNetwork,
            CaravanFluidNetwork fluidNetwork,
            CaravanMaterialNetwork biomassNetwork,
            CaravanMaterialNetwork mechanicalNetwork,
            CaravanProgressionWorldRig world,
            CaravanProgressionDirector director,
            CaravanBuildModeController builder,
            string savePath = null,
            string autosavePath = null)
        {
            UnsubscribeFromChanges();
            floatingOrigin = origin != null
                ? origin
                : throw new ArgumentNullException(nameof(origin));
            chassis = caravan != null
                ? caravan
                : throw new ArgumentNullException(nameof(caravan));
            grid = mountGrid != null
                ? mountGrid
                : throw new ArgumentNullException(nameof(mountGrid));
            platform = platformController != null
                ? platformController
                : throw new ArgumentNullException(nameof(platformController));
            construction = constructionService != null
                ? constructionService
                : throw new ArgumentNullException(nameof(constructionService));
            progression = progressionSystem != null
                ? progressionSystem
                : throw new ArgumentNullException(nameof(progressionSystem));
            electrical = electricalNetwork != null
                ? electricalNetwork
                : throw new ArgumentNullException(nameof(electricalNetwork));
            fluid = fluidNetwork != null
                ? fluidNetwork
                : throw new ArgumentNullException(nameof(fluidNetwork));
            biomass = biomassNetwork != null
                ? biomassNetwork
                : throw new ArgumentNullException(nameof(biomassNetwork));
            mechanical = mechanicalNetwork != null
                ? mechanicalNetwork
                : throw new ArgumentNullException(nameof(mechanicalNetwork));
            progressionWorld = world != null
                ? world
                : throw new ArgumentNullException(nameof(world));
            progressionDirector = director != null
                ? director
                : throw new ArgumentNullException(nameof(director));
            buildMode = builder;
            player = GetComponent<CaravanFirstPersonController>();
            SetSavePaths(
                string.IsNullOrWhiteSpace(savePath)
                    ? Path.Combine(
                        Application.persistentDataPath,
                        DefaultFileName)
                    : savePath,
                string.IsNullOrWhiteSpace(autosavePath)
                    ? Path.Combine(
                        Application.persistentDataPath,
                        DefaultAutosaveFileName)
                    : autosavePath);
            SubscribeToChanges();
            ResetAutosaveSchedule();
        }

        public void SetSavePaths(string manualSavePath, string autosaveSavePath)
        {
            if (string.IsNullOrWhiteSpace(manualSavePath))
            {
                throw new ArgumentException(
                    "Manual save path is required.",
                    nameof(manualSavePath));
            }
            if (string.IsNullOrWhiteSpace(autosaveSavePath))
            {
                throw new ArgumentException(
                    "Autosave path is required.",
                    nameof(autosaveSavePath));
            }

            SavePath = Path.GetFullPath(manualSavePath);
            AutosavePath = Path.GetFullPath(autosaveSavePath);
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.f9Key.wasPressedThisFrame)
            {
                if (TryLoad(out var error))
                {
                    var source = LastLoadWasAutosave
                        ? "автосохранение"
                        : "ручное сохранение";
                    var backup = LastLoadWasBackup ? " (резервная копия)" : string.Empty;
                    SetFeedback($"Загружено: {source}{backup}", false);
                    Debug.Log($"Steppe save loaded: {source}{backup}.");
                }
                else
                {
                    SetFeedback($"Не удалось загрузить: {error}", true);
                    Debug.LogError($"Steppe save load failed: {error}");
                }

                // Loading must win over an autosave that became due on the
                // same frame. Otherwise F9 first overwrites the old state and
                // immediately restores that identical snapshot.
                return;
            }

            if (keyboard != null && keyboard.f5Key.wasPressedThisFrame)
            {
                if (TrySave(out var error))
                {
                    SetFeedback("Игра сохранена  •  F9 — загрузить", false);
                    Debug.Log("Steppe manual save written.");
                }
                else
                {
                    SetFeedback($"Не удалось сохранить: {error}", true);
                    Debug.LogError($"Steppe manual save failed: {error}");
                }

                return;
            }

            TickAutosave();
        }

        public CaravanSaveSnapshot CaptureSnapshot()
        {
            EnsureConfigured();
            var worldPosition = floatingOrigin.LocalToWorld(
                chassis.transform.position);
            var rotation = chassis.transform.rotation;
            var hasPlayerState = player != null;
            var playerWorldPosition = hasPlayerState
                ? floatingOrigin.LocalToWorld(player.transform.position)
                : worldPosition;
            return new CaravanSaveSnapshot
            {
                progression = progression.CaptureSnapshot(),
                platformCells = CapturePlatformCells(),
                modules = CaptureModules(),
                electricalConnections = CaptureElectricalConnections(),
                fluidConnections = CaptureFluidConnections(),
                biomassConnections = CaptureMaterialConnections(biomass),
                mechanicalConnections = CaptureMaterialConnections(mechanical),
                caravanWorldX = worldPosition.X,
                caravanWorldY = (float)worldPosition.Y,
                caravanWorldZ = worldPosition.Z,
                travelledMetres = chassis.TravelledMetres,
                rotationX = rotation.x,
                rotationY = rotation.y,
                rotationZ = rotation.z,
                rotationW = rotation.w,
                hasPlayerState = hasPlayerState,
                playerWorldX = playerWorldPosition.X,
                playerWorldY = (float)playerWorldPosition.Y,
                playerWorldZ = playerWorldPosition.Z,
                playerYaw = hasPlayerState ? player.YawDegrees : 0f,
                playerPitch = hasPlayerState ? player.PitchDegrees : 0f
            };
        }

        public bool TrySave(out string error)
        {
            if (!CanCaptureStableState(out error))
            {
                return false;
            }

            if (!TryWriteSnapshot(SavePath, out error))
            {
                return false;
            }

            autosavePending = false;
            ResetAutosaveSchedule();
            return true;
        }

        public bool TryAutosave(out string error)
        {
            if (!CanCaptureStableState(out error))
            {
                return false;
            }

            if (!TryWriteSnapshot(AutosavePath, out error))
            {
                return false;
            }

            autosavePending = false;
            lastAutosaveAt = UnityEngine.Time.unscaledTime;
            ResetAutosaveSchedule();
            return true;
        }

        public bool TryLoad(out string error)
        {
            error = string.Empty;
            var candidates = GetSaveCandidatesNewestFirst();
            if (candidates.Count == 0)
            {
                error = "файл сохранения не найден";
                return false;
            }

            var lastError = string.Empty;
            for (var index = 0; index < candidates.Count; index++)
            {
                var candidate = candidates[index];
                try
                {
                    var json = File.ReadAllText(candidate, Encoding.UTF8);
                    var snapshot =
                        JsonUtility.FromJson<CaravanSaveSnapshot>(json);
                    if (!TryRestoreSnapshot(snapshot, out lastError))
                    {
                        continue;
                    }

                    LastLoadWasAutosave = IsAutosavePath(candidate);
                    LastLoadWasBackup = candidate.EndsWith(
                        ".bak",
                        StringComparison.OrdinalIgnoreCase);
                    error = string.Empty;
                    return true;
                }
                catch (Exception exception)
                {
                    lastError = exception.Message;
                }
            }

            error = string.IsNullOrWhiteSpace(lastError)
                ? "не найдено исправное сохранение"
                : $"не найдено исправное сохранение: {lastError}";
            return false;
        }

        public bool TryRestoreSnapshot(
            CaravanSaveSnapshot snapshot,
            out string error)
        {
            isRestoring = true;
            try
            {
                return TryRestoreSnapshotCore(snapshot, out error);
            }
            finally
            {
                isRestoring = false;
                autosavePending = false;
                ResetAutosaveSchedule();
            }
        }

        private bool TryRestoreSnapshotCore(
            CaravanSaveSnapshot snapshot,
            out string error)
        {
            EnsureConfigured();
            if (!TryValidate(snapshot, out var cells, out error))
            {
                return false;
            }

            buildMode?.ExitBuildMode();
            var body = chassis.Body;
            var wasKinematic = body != null && body.isKinematic;
            if (body != null)
            {
                if (!body.isKinematic)
                {
                    body.linearVelocity = Vector3.zero;
                    body.angularVelocity = Vector3.zero;
                }
                body.isKinematic = true;
            }

            electrical.ClearConnections();
            fluid.ClearConnections();
            biomass.ClearConnections();
            mechanical.ClearConnections();

            var existingParts = chassis.GetComponentsInChildren<CaravanPart>(false);
            for (var index = 0; index < existingParts.Length; index++)
            {
                var module = existingParts[index].GetComponent<CaravanModule>();
                if (module != null && grid.TryGetPlacement(module, out _)
                                   && !construction.TryDestroyPlaced(
                                       module,
                                       grid,
                                       false))
                {
                    error = $"не удалось удалить модуль {module.InstanceId}";
                    RestoreKinematic(body, wasKinematic);
                    return false;
                }
            }

            if (!platform.TryRestoreCells(cells))
            {
                error = "не удалось восстановить форму платформы";
                RestoreKinematic(body, wasKinematic);
                return false;
            }

            progression.RestoreSnapshot(snapshot.progression);
            progressionWorld.RefreshFromProgression();
            for (var index = 0; index < snapshot.modules.Length; index++)
            {
                var record = snapshot.modules[index];
                if (!construction.TryCreateForRestore(
                        record.kind,
                        record.instanceId,
                        out var module))
                {
                    error = $"не удалось создать модуль {record.instanceId}";
                    RestoreKinematic(body, wasKinematic);
                    return false;
                }
                var definition = CaravanPartCatalog.Get(record.kind);
                var placement = new CaravanGridPlacement(
                    record.x,
                    record.z,
                    definition.FootprintWidth,
                    definition.FootprintLength,
                    record.quarterTurns);
                if (!grid.TryPlace(module, placement))
                {
                    construction.DestroyBuffered(module);
                    error = $"не удалось разместить модуль {record.instanceId}";
                    RestoreKinematic(body, wasKinematic);
                    return false;
                }
                module.gameObject.SetActive(true);
                construction.ActivatePlaced(module);
                module.RestoreState(
                    record.dust,
                    record.integrity,
                    record.load);
                var part = module.GetComponent<CaravanPart>();
                part.SetStoredAmount(record.storedAmount);
                part.SetTemperature(record.temperatureCelsius);
            }

            if (!TryRestoreConnections(snapshot, out error))
            {
                RestoreKinematic(body, wasKinematic);
                return false;
            }

            chassis.RefreshMassProperties();
            chassis.NotifyStructureCollidersChanged();
            var localPosition = floatingOrigin.WorldToLocal(
                snapshot.caravanWorldX,
                snapshot.caravanWorldY,
                snapshot.caravanWorldZ);
            var restoredRotation = new Quaternion(
                snapshot.rotationX,
                snapshot.rotationY,
                snapshot.rotationZ,
                snapshot.rotationW).normalized;
            chassis.Teleport(localPosition, restoredRotation);
            chassis.RestoreTravelledMetres(snapshot.travelledMetres);
            if (player != null)
            {
                var playerPosition = snapshot.hasPlayerState
                    ? floatingOrigin.WorldToLocal(
                        snapshot.playerWorldX,
                        snapshot.playerWorldY,
                        snapshot.playerWorldZ)
                    : chassis.transform.TransformPoint(
                        new Vector3(0.55f, 0.08f, 0.15f));
                var playerYaw = snapshot.hasPlayerState
                    ? snapshot.playerYaw
                    : restoredRotation.eulerAngles.y;
                var playerPitch = snapshot.hasPlayerState
                    ? snapshot.playerPitch
                    : 0f;
                player.Teleport(playerPosition, playerYaw, playerPitch);
            }
            Physics.SyncTransforms();
            progressionDirector.RecalculateStage();
            error = string.Empty;
            return true;
        }

        private bool TryValidate(
            CaravanSaveSnapshot snapshot,
            out CaravanGridCell[] cells,
            out string error)
        {
            cells = Array.Empty<CaravanGridCell>();
            error = string.Empty;
            if (snapshot == null)
            {
                error = "пустое сохранение";
                return false;
            }
            if (snapshot.schemaVersion != CurrentSchemaVersion)
            {
                error = $"неподдерживаемая версия {snapshot.schemaVersion}";
                return false;
            }
            if (snapshot.progression == null
                || snapshot.platformCells == null
                || snapshot.modules == null)
            {
                error = "сохранение не содержит обязательных данных";
                return false;
            }

            cells = new CaravanGridCell[snapshot.platformCells.Length];
            for (var index = 0; index < cells.Length; index++)
            {
                cells[index] = new CaravanGridCell(
                    snapshot.platformCells[index].x,
                    snapshot.platformCells[index].z);
            }
            var validationGrid = new CaravanMountGridModel(
                grid.Width,
                grid.Length);
            if (!validationGrid.TryReplacePlatformCells(cells))
            {
                error = "форма платформы разорвана или не содержит базовые клетки";
                return false;
            }

            var ids = new HashSet<string>(StringComparer.Ordinal);
            for (var index = 0; index < snapshot.modules.Length; index++)
            {
                var record = snapshot.modules[index];
                if (record == null
                    || string.IsNullOrWhiteSpace(record.instanceId)
                    || !ids.Add(record.instanceId)
                    || !Enum.IsDefined(typeof(CaravanPartKind), record.kind))
                {
                    error = "сохранение содержит недопустимый идентификатор модуля";
                    return false;
                }
                var definition = CaravanPartCatalog.Get(record.kind);
                var placement = new CaravanGridPlacement(
                    record.x,
                    record.z,
                    definition.FootprintWidth,
                    definition.FootprintLength,
                    record.quarterTurns);
                if (!validationGrid.TryPlace(record, placement))
                {
                    error = $"недопустимое размещение модуля {record.instanceId}";
                    return false;
                }
            }
            return true;
        }

        private CaravanGridCellRecord[] CapturePlatformCells()
        {
            var cells = grid.GetPlatformCells();
            Array.Sort(cells, (left, right) =>
            {
                var zOrder = left.Z.CompareTo(right.Z);
                return zOrder != 0 ? zOrder : left.X.CompareTo(right.X);
            });
            var result = new CaravanGridCellRecord[cells.Length];
            for (var index = 0; index < cells.Length; index++)
            {
                result[index] = new CaravanGridCellRecord
                {
                    x = cells[index].X,
                    z = cells[index].Z
                };
            }
            return result;
        }

        private CaravanModuleRecord[] CaptureModules()
        {
            var parts = chassis.GetComponentsInChildren<CaravanPart>(false);
            var result = new List<CaravanModuleRecord>(parts.Length);
            for (var index = 0; index < parts.Length; index++)
            {
                var part = parts[index];
                var module = part.GetComponent<CaravanModule>();
                if (module == null
                    || !grid.TryGetPlacement(module, out var placement))
                {
                    continue;
                }
                result.Add(new CaravanModuleRecord
                {
                    instanceId = module.InstanceId,
                    kind = part.Kind,
                    x = placement.X,
                    z = placement.Z,
                    quarterTurns = placement.QuarterTurns,
                    dust = module.State.Dust,
                    integrity = module.State.Integrity,
                    load = module.State.Load,
                    storedAmount = part.StoredAmount,
                    temperatureCelsius = part.TemperatureCelsius
                });
            }
            result.Sort((left, right) => string.CompareOrdinal(
                left.instanceId,
                right.instanceId));
            return result.ToArray();
        }

        private CaravanConnectionRecord[] CaptureElectricalConnections()
        {
            var result = new CaravanConnectionRecord[electrical.Cables.Count];
            for (var index = 0; index < result.Length; index++)
            {
                result[index] = Connection(
                    electrical.Cables[index].Start.PortId,
                    electrical.Cables[index].End.PortId);
            }
            return result;
        }

        private CaravanConnectionRecord[] CaptureFluidConnections()
        {
            var result = new CaravanConnectionRecord[fluid.Pipes.Count];
            for (var index = 0; index < result.Length; index++)
            {
                result[index] = Connection(
                    fluid.Pipes[index].Start.PortId,
                    fluid.Pipes[index].End.PortId);
            }
            return result;
        }

        private static CaravanConnectionRecord[] CaptureMaterialConnections(
            CaravanMaterialNetwork network)
        {
            var result = new CaravanConnectionRecord[network.Links.Count];
            for (var index = 0; index < result.Length; index++)
            {
                result[index] = Connection(
                    network.Links[index].Start.PortId,
                    network.Links[index].End.PortId);
            }
            return result;
        }

        private bool TryRestoreConnections(
            CaravanSaveSnapshot snapshot,
            out string error)
        {
            if (!RestoreElectrical(
                    snapshot.electricalConnections
                    ?? Array.Empty<CaravanConnectionRecord>()))
            {
                error = "не удалось восстановить электрическое соединение";
                return false;
            }
            if (!RestoreFluid(
                    snapshot.fluidConnections
                    ?? Array.Empty<CaravanConnectionRecord>()))
            {
                error = "не удалось восстановить жидкостное соединение";
                return false;
            }
            if (!RestoreMaterial(
                    biomass,
                    snapshot.biomassConnections
                    ?? Array.Empty<CaravanConnectionRecord>()))
            {
                error = "не удалось восстановить поток биомассы";
                return false;
            }
            if (!RestoreMaterial(
                    mechanical,
                    snapshot.mechanicalConnections
                    ?? Array.Empty<CaravanConnectionRecord>()))
            {
                error = "не удалось восстановить механическое соединение";
                return false;
            }
            error = string.Empty;
            return true;
        }

        private bool RestoreElectrical(
            IReadOnlyList<CaravanConnectionRecord> records)
        {
            var ports = IndexElectricalPorts();
            for (var index = 0; index < records.Count; index++)
            {
                var record = records[index];
                if (record == null
                    || !ports.TryGetValue(record.startPortId, out var start)
                    || !ports.TryGetValue(record.endPortId, out var end)
                    || !electrical.TryConnect(start, end))
                {
                    return false;
                }
            }
            return true;
        }

        private bool RestoreFluid(IReadOnlyList<CaravanConnectionRecord> records)
        {
            var ports = IndexFluidPorts();
            for (var index = 0; index < records.Count; index++)
            {
                var record = records[index];
                if (record == null
                    || !ports.TryGetValue(record.startPortId, out var start)
                    || !ports.TryGetValue(record.endPortId, out var end)
                    || !fluid.TryConnect(start, end))
                {
                    return false;
                }
            }
            return true;
        }

        private static bool RestoreMaterial(
            CaravanMaterialNetwork network,
            IReadOnlyList<CaravanConnectionRecord> records)
        {
            var ports = new Dictionary<string, CaravanMaterialPort>(
                StringComparer.Ordinal);
            for (var index = 0; index < network.Ports.Count; index++)
            {
                ports[network.Ports[index].PortId] = network.Ports[index];
            }
            for (var index = 0; index < records.Count; index++)
            {
                var record = records[index];
                if (record == null
                    || !ports.TryGetValue(record.startPortId, out var start)
                    || !ports.TryGetValue(record.endPortId, out var end)
                    || !network.TryConnect(start, end))
                {
                    return false;
                }
            }
            return true;
        }

        private Dictionary<string, CaravanElectricalPort> IndexElectricalPorts()
        {
            var result = new Dictionary<string, CaravanElectricalPort>(
                StringComparer.Ordinal);
            for (var index = 0; index < electrical.Ports.Count; index++)
            {
                result[electrical.Ports[index].PortId] = electrical.Ports[index];
            }
            return result;
        }

        private Dictionary<string, CaravanFluidPort> IndexFluidPorts()
        {
            var result = new Dictionary<string, CaravanFluidPort>(
                StringComparer.Ordinal);
            for (var index = 0; index < fluid.Ports.Count; index++)
            {
                result[fluid.Ports[index].PortId] = fluid.Ports[index];
            }
            return result;
        }

        private static CaravanConnectionRecord Connection(
            string start,
            string end)
        {
            return new CaravanConnectionRecord
            {
                startPortId = start,
                endPortId = end
            };
        }

        public void RequestAutosave()
        {
            if (isRestoring || progression == null)
            {
                return;
            }

            var now = UnityEngine.Time.unscaledTime;
            autosavePending = true;
            autosaveDueAt = Mathf.Max(
                now + AutosaveDebounceSeconds,
                lastAutosaveAt + MinimumAutosaveIntervalSeconds);
        }

        private void TickAutosave()
        {
            if (progression == null || isRestoring)
            {
                return;
            }

            var now = UnityEngine.Time.unscaledTime;
            if (now >= nextPeriodicAutosaveAt)
            {
                RequestAutosave();
                nextPeriodicAutosaveAt =
                    now + PeriodicAutosaveIntervalSeconds;
            }
            if (!autosavePending || now < autosaveDueAt)
            {
                return;
            }
            if (!CanCaptureStableState(out _))
            {
                return;
            }

            if (TryAutosave(out var error))
            {
                SetFeedback("Автосохранение", false);
                return;
            }

            autosaveDueAt = now + MinimumAutosaveIntervalSeconds;
            SetFeedback($"Ошибка автосохранения: {error}", true);
        }

        private bool TryWriteSnapshot(string path, out string error)
        {
            error = string.Empty;
            try
            {
                var snapshot = CaptureSnapshot();
                var json = JsonUtility.ToJson(snapshot, true);
                var directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    Directory.CreateDirectory(directory);
                }
                WriteAtomically(path, json);
                return true;
            }
            catch (Exception exception)
            {
                error = exception.Message;
                return false;
            }
        }

        private bool CanCaptureStableState(out string error)
        {
            if (buildMode != null && buildMode.IsActive)
            {
                error = "сначала завершите режим строительства";
                return false;
            }

            error = string.Empty;
            return true;
        }

        private List<string> GetSaveCandidatesNewestFirst()
        {
            var result = new List<string>(4);
            AddCandidate(result, SavePath);
            AddCandidate(result, AutosavePath);
            AddCandidate(result, BackupPath(SavePath));
            AddCandidate(result, BackupPath(AutosavePath));
            result.Sort((left, right) =>
            {
                var timeOrder = File.GetLastWriteTimeUtc(right).CompareTo(
                    File.GetLastWriteTimeUtc(left));
                return timeOrder != 0
                    ? timeOrder
                    : string.CompareOrdinal(left, right);
            });
            return result;
        }

        private static void AddCandidate(ICollection<string> result, string path)
        {
            if (Exists(path) && !result.Contains(path))
            {
                result.Add(path);
            }
        }

        private bool IsAutosavePath(string path)
        {
            return string.Equals(
                       path,
                       AutosavePath,
                       StringComparison.OrdinalIgnoreCase)
                   || string.Equals(
                       path,
                       BackupPath(AutosavePath),
                       StringComparison.OrdinalIgnoreCase);
        }

        private static bool Exists(string path)
        {
            return !string.IsNullOrWhiteSpace(path) && File.Exists(path);
        }

        private static string BackupPath(string path)
        {
            return string.IsNullOrWhiteSpace(path) ? null : path + ".bak";
        }

        private void SubscribeToChanges()
        {
            if (subscribedToChanges)
            {
                return;
            }

            progression.Changed += RequestAutosave;
            grid.Changed += RequestAutosave;
            electrical.ConnectionsChanged += RequestAutosave;
            fluid.ConnectionsChanged += RequestAutosave;
            biomass.ConnectionsChanged += RequestAutosave;
            mechanical.ConnectionsChanged += RequestAutosave;
            subscribedToChanges = true;
        }

        private void UnsubscribeFromChanges()
        {
            if (!subscribedToChanges)
            {
                return;
            }

            progression.Changed -= RequestAutosave;
            grid.Changed -= RequestAutosave;
            electrical.ConnectionsChanged -= RequestAutosave;
            fluid.ConnectionsChanged -= RequestAutosave;
            biomass.ConnectionsChanged -= RequestAutosave;
            mechanical.ConnectionsChanged -= RequestAutosave;
            subscribedToChanges = false;
        }

        private void ResetAutosaveSchedule()
        {
            nextPeriodicAutosaveAt = UnityEngine.Time.unscaledTime
                                     + PeriodicAutosaveIntervalSeconds;
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused)
            {
                FlushAutosave();
            }
        }

        private void OnApplicationQuit()
        {
            FlushAutosave();
        }

        private void OnDestroy()
        {
            UnsubscribeFromChanges();
        }

        private void FlushAutosave()
        {
            if (progression != null
                && !isRestoring
                && CanCaptureStableState(out _))
            {
                TryAutosave(out _);
            }
        }

        private static void WriteAtomically(string path, string contents)
        {
            var temporaryPath = path + ".tmp";
            File.WriteAllText(
                temporaryPath,
                contents,
                new UTF8Encoding(false));
            if (!File.Exists(path))
            {
                File.Move(temporaryPath, path);
                return;
            }
            var backupPath = path + ".bak";
            try
            {
                File.Replace(temporaryPath, path, backupPath, true);
            }
            catch (PlatformNotSupportedException)
            {
                File.Copy(path, backupPath, true);
                File.Copy(temporaryPath, path, true);
                File.Delete(temporaryPath);
            }
        }

        private static void RestoreKinematic(Rigidbody body, bool wasKinematic)
        {
            if (body != null)
            {
                body.isKinematic = wasKinematic;
            }
        }

        private void SetFeedback(string message, bool isError)
        {
            FeedbackMessage = message;
            FeedbackIsError = isError;
            feedbackUntil = UnityEngine.Time.unscaledTime + 4f;
        }

        private void EnsureConfigured()
        {
            if (floatingOrigin == null || chassis == null || grid == null
                || platform == null || construction == null
                || progression == null || electrical == null || fluid == null
                || biomass == null || mechanical == null
                || progressionWorld == null || progressionDirector == null)
            {
                throw new InvalidOperationException(
                    "CaravanSaveService is not configured.");
            }
        }
    }
}
