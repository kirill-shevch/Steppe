using System;
using Steppe.Simulation;
using Steppe.UnitySimulation;
using Steppe.World;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Steppe.Rendering
{
    public enum SteppeVisualizationKind
    {
        State,
        Flux,
        VectorProcess
    }

    /// <summary>
    /// Publishes the complete finite-world state to rendering. The natural map is
    /// always active; the F2 atlas is an explicit visual contract for every scalar
    /// state, balance flux, and vector process in the simulation catalogs.
    /// </summary>
    [DefaultExecutionOrder(360)]
    [DisallowMultipleComponent]
    public sealed class SteppeSimulationVisualization : MonoBehaviour
    {
        private static readonly int NaturalMapId =
            Shader.PropertyToID("_SteppeSimulationNaturalMap");
        private static readonly int DiagnosticMapId =
            Shader.PropertyToID("_SteppeSimulationDiagnosticMap");
        private static readonly int MapBoundsId =
            Shader.PropertyToID("_SteppeSimulationMapBounds");
        private static readonly int NaturalReadyId =
            Shader.PropertyToID("_SteppeSimulationNaturalReady");
        private static readonly int DiagnosticStrengthId =
            Shader.PropertyToID("_SteppeDiagnosticOverlayStrength");

        private static readonly Color32[] FluxPalette =
        {
            new Color32(39, 78, 121, 255),
            new Color32(92, 155, 183, 255),
            new Color32(224, 224, 197, 255),
            new Color32(224, 145, 68, 255),
            new Color32(152, 48, 33, 255),
        };

        private SteppeSimulationHost host;
        private FloatingOriginSystem floatingOrigin;
        private Transform focus;
        private Texture2D naturalMap;
        private Texture2D diagnosticMap;
        private Texture2D legendTexture;
        private Color32[] naturalPixels;
        private Color32[] diagnosticPixels;
        private SteppeSimulationSnapshot lastNaturalSnapshot;
        private SteppeSimulationSnapshot lastDiagnosticSnapshot;
        private SteppeVisualizationKind lastDiagnosticKind;
        private int lastDiagnosticIndex = -1;
        private float nextNaturalRefresh;
        private float nextDiagnosticRefresh;
        private int selectionIndex;
        private bool visible;
        private bool worldOverlay = true;
        private GUIStyle titleStyle;
        private GUIStyle bodyStyle;
        private GUIStyle smallStyle;

        public int StateCount => StateCatalog.All.Count;
        public int FluxCount => FluxCatalog.All.Count;
        public int VectorProcessCount => VectorProcessCatalog.All.Count;
        public bool IsVisible => visible;
        public bool WorldOverlayEnabled => worldOverlay;
        public SteppeVisualizationKind Kind { get; private set; } = SteppeVisualizationKind.State;
        public Texture2D NaturalMap => naturalMap;
        public Texture2D DiagnosticMap => diagnosticMap;

        public void Configure(
            SteppeSimulationHost simulationHost,
            FloatingOriginSystem origin,
            Transform focusTransform)
        {
            host = simulationHost != null
                ? simulationHost
                : throw new ArgumentNullException(nameof(simulationHost));
            floatingOrigin = origin != null
                ? origin
                : throw new ArgumentNullException(nameof(origin));
            focus = focusTransform != null
                ? focusTransform
                : throw new ArgumentNullException(nameof(focusTransform));
            selectionIndex = FindStateIndex(SimulationLayer.AirTemperature);
            Shader.SetGlobalFloat(NaturalReadyId, 0f);
            Shader.SetGlobalFloat(DiagnosticStrengthId, 0f);
        }

        private void Update()
        {
            HandleInput();
            var latest = host != null ? host.LatestSnapshot : null;
            if (latest == null)
            {
                return;
            }

            EnsureTextures(latest);
            if (latest != lastNaturalSnapshot || UnityEngine.Time.unscaledTime >= nextNaturalRefresh)
            {
                RebuildNaturalMap(host.PreviousSnapshot ?? latest, latest, host.MacroInterpolationAlpha);
                lastNaturalSnapshot = latest;
                nextNaturalRefresh = UnityEngine.Time.unscaledTime + 0.2f;
            }

            var diagnosticChanged = latest != lastDiagnosticSnapshot
                                    || Kind != lastDiagnosticKind
                                    || selectionIndex != lastDiagnosticIndex;
            if (visible
                && (diagnosticChanged || UnityEngine.Time.unscaledTime >= nextDiagnosticRefresh))
            {
                RebuildDiagnosticMap(host.PreviousSnapshot ?? latest, latest, host.MacroInterpolationAlpha);
                lastDiagnosticSnapshot = latest;
                lastDiagnosticKind = Kind;
                lastDiagnosticIndex = selectionIndex;
                nextDiagnosticRefresh = UnityEngine.Time.unscaledTime + 0.12f;
            }

            Shader.SetGlobalFloat(
                DiagnosticStrengthId,
                visible && worldOverlay ? 0.72f : 0f);
        }

        public void SetSelection(SteppeVisualizationKind kind, int index)
        {
            Kind = kind;
            selectionIndex = Wrap(index, SelectionCount);
        }

        public void SetVisible(bool value)
        {
            visible = value;
        }

        private void HandleInput()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            if (keyboard.f2Key.wasPressedThisFrame)
            {
                visible = !visible;
            }

            if (!visible)
            {
                return;
            }

            if (keyboard.tabKey.wasPressedThisFrame)
            {
                Kind = (SteppeVisualizationKind)(((int)Kind + 1) % 3);
                selectionIndex = 0;
            }

            if (keyboard.leftBracketKey.wasPressedThisFrame)
            {
                selectionIndex = Wrap(selectionIndex - 1, SelectionCount);
            }
            else if (keyboard.rightBracketKey.wasPressedThisFrame)
            {
                selectionIndex = Wrap(selectionIndex + 1, SelectionCount);
            }

            if (keyboard.oKey.wasPressedThisFrame)
            {
                worldOverlay = !worldOverlay;
            }
        }

        private int SelectionCount
        {
            get
            {
                switch (Kind)
                {
                    case SteppeVisualizationKind.State:
                        return StateCatalog.All.Count;
                    case SteppeVisualizationKind.Flux:
                        return FluxCatalog.All.Count;
                    default:
                        return VectorProcessCatalog.All.Count;
                }
            }
        }

        private void EnsureTextures(SteppeSimulationSnapshot snapshot)
        {
            var width = snapshot.Summary.Width;
            var height = snapshot.Summary.Height;
            if (naturalMap == null || naturalMap.width != width || naturalMap.height != height)
            {
                DestroyTexture(naturalMap);
                DestroyTexture(diagnosticMap);
                naturalMap = CreateMap("Steppe natural state map", width, height);
                diagnosticMap = CreateMap("Steppe diagnostic field map", width, height);
                naturalPixels = new Color32[width * height];
                diagnosticPixels = new Color32[width * height];
                Shader.SetGlobalTexture(NaturalMapId, naturalMap);
                Shader.SetGlobalTexture(DiagnosticMapId, diagnosticMap);
            }

            var coordinates = snapshot.Coordinates;
            Shader.SetGlobalVector(MapBoundsId, new Vector4(
                (float)coordinates.MinimumWorldX,
                (float)coordinates.MinimumWorldZ,
                1f / (coordinates.Width * coordinates.CellSizeMeters),
                1f / (coordinates.Height * coordinates.CellSizeMeters)));
        }

        private void RebuildNaturalMap(
            SteppeSimulationSnapshot previous,
            SteppeSimulationSnapshot latest,
            float blend)
        {
            if (!TryLayerPair(previous, latest, SimulationLayer.SurfaceWater, out var surface0, out var surface1)
                || !TryLayerPair(previous, latest, SimulationLayer.RootWater, out var root0, out var root1)
                || !TryLayerPair(previous, latest, SimulationLayer.BurnScar, out var scar0, out var scar1)
                || !TryLayerPair(previous, latest, SimulationLayer.FireIntensity, out var fire0, out var fire1)
                || !TryLayerPair(previous, latest, SimulationLayer.SoilCompaction, out var compact0, out var compact1))
            {
                Shader.SetGlobalFloat(NaturalReadyId, 0f);
                return;
            }

            for (var index = 0; index < naturalPixels.Length; index++)
            {
                var surface = Mathf.LerpUnclamped(surface0.Values[index], surface1.Values[index], blend);
                var root = Mathf.LerpUnclamped(root0.Values[index], root1.Values[index], blend);
                var scar = Mathf.LerpUnclamped(scar0.Values[index], scar1.Values[index], blend);
                var fire = Mathf.LerpUnclamped(fire0.Values[index], fire1.Values[index], blend);
                var compaction = Mathf.LerpUnclamped(compact0.Values[index], compact1.Values[index], blend);
                var liquidAvailability = Mathf.Max(
                    Mathf.Clamp01(surface / 24f),
                    Mathf.Clamp01(root / 180f) * 0.86f);
                naturalPixels[index] = new Color(
                    1f - liquidAvailability,
                    Mathf.Clamp01(scar),
                    Mathf.Sqrt(Mathf.Clamp01(fire)),
                    Mathf.Clamp01(compaction));
            }

            naturalMap.SetPixels32(naturalPixels);
            naturalMap.Apply(false, false);
            Shader.SetGlobalFloat(NaturalReadyId, 1f);
        }

        private void RebuildDiagnosticMap(
            SteppeSimulationSnapshot previous,
            SteppeSimulationSnapshot latest,
            float blend)
        {
            switch (Kind)
            {
                case SteppeVisualizationKind.State:
                    BuildStateMap(previous, latest, blend);
                    break;
                case SteppeVisualizationKind.Flux:
                    BuildFluxMap(latest);
                    break;
                default:
                    BuildVectorMap(latest);
                    break;
            }

            diagnosticMap.SetPixels32(diagnosticPixels);
            diagnosticMap.Apply(false, false);
            RebuildLegend();
        }

        private void BuildStateMap(
            SteppeSimulationSnapshot previous,
            SteppeSimulationSnapshot latest,
            float blend)
        {
            var descriptor = StateCatalog.All[selectionIndex];
            if (!TryLayerPair(previous, latest, descriptor.Id, out var from, out var to))
            {
                ClearDiagnostic();
                return;
            }

            var palette = ParsePalette(descriptor.Palette);
            for (var index = 0; index < diagnosticPixels.Length; index++)
            {
                var value = Mathf.LerpUnclamped(from.Values[index], to.Values[index], blend);
                diagnosticPixels[index] = EvaluateStateColor(descriptor, value, palette);
            }
        }

        private void BuildFluxMap(SteppeSimulationSnapshot latest)
        {
            var descriptor = FluxCatalog.All[selectionIndex];
            if (!latest.TryGetFlux(descriptor.Id, out var flux))
            {
                ClearDiagnostic();
                return;
            }

            var maximum = RobustAbsoluteMaximum(flux.Values);
            for (var index = 0; index < diagnosticPixels.Length; index++)
            {
                var normalized = descriptor.Signed
                    ? 0.5f + 0.5f * Mathf.Clamp(flux.Values[index] / maximum, -1f, 1f)
                    : Mathf.Clamp01(flux.Values[index] / maximum);
                diagnosticPixels[index] = PaletteColor(FluxPalette, normalized, 222);
            }
        }

        private void BuildVectorMap(SteppeSimulationSnapshot latest)
        {
            var descriptor = VectorProcessCatalog.All[selectionIndex];
            if (!latest.TryGetVectorProcess(descriptor.Process, out var process))
            {
                ClearDiagnostic();
                return;
            }

            var maximum = Mathf.Max(0.000001f, process.MaximumMagnitude);
            for (var index = 0; index < diagnosticPixels.Length; index++)
            {
                var angle = Mathf.Atan2(process.VectorY[index], process.VectorX[index]);
                var hue = Mathf.Repeat(angle / (Mathf.PI * 2f) + 1f, 1f);
                var strength = Mathf.Sqrt(Mathf.Clamp01(process.Magnitude[index] / maximum));
                var color = Color.HSVToRGB(hue, 0.78f, Mathf.Lerp(0.18f, 1f, strength));
                color.a = 0.87f;
                diagnosticPixels[index] = color;
            }
        }

        private void ClearDiagnostic()
        {
            for (var index = 0; index < diagnosticPixels.Length; index++)
            {
                diagnosticPixels[index] = new Color32(0, 0, 0, 0);
            }
        }

        private void OnGUI()
        {
            if (!visible || host == null || host.LatestSnapshot == null || diagnosticMap == null)
            {
                return;
            }

            EnsureStyles();
            const float width = 410f;
            const float height = 548f;
            var area = new Rect(Screen.width - width - 14f, 14f, width, height);
            GUI.Box(area, GUIContent.none);
            GUILayout.BeginArea(new Rect(area.x + 12f, area.y + 10f, width - 24f, height - 20f));
            GUILayout.Label("АТЛАС СИМУЛЯЦИИ СТЕПИ", titleStyle);
            GUILayout.Label("F2 закрыть  •  Tab состояние/поток/вектор  •  [ ] листать  •  O поле на земле", smallStyle);
            GUILayout.Space(5f);

            var title = SelectedTitle;
            GUILayout.Label($"{KindLabel}  {selectionIndex + 1}/{SelectionCount}: {title}", titleStyle);
            GUILayout.Label(SelectedDescription, bodyStyle, GUILayout.Height(48f));
            GUILayout.Label(SelectedMeasurement, smallStyle);
            GUILayout.Space(4f);

            var mapRect = GUILayoutUtility.GetRect(330f, 330f, GUILayout.ExpandWidth(true));
            GUI.DrawTexture(mapRect, diagnosticMap, ScaleMode.StretchToFill, false);
            GUI.Box(mapRect, GUIContent.none);
            DrawVectorArrows(mapRect, host.LatestSnapshot);
            DrawFocusMarker(mapRect, host.LatestSnapshot);
            GUILayout.Space(4f);

            if (legendTexture != null)
            {
                var legendRect = GUILayoutUtility.GetRect(40f, 18f, GUILayout.ExpandWidth(true));
                GUI.DrawTexture(legendRect, legendTexture, ScaleMode.StretchToFill, false);
            }

            GUILayout.Label(SelectedScaleLabel, smallStyle);
            GUILayout.Label($"В точке камеры: {SelectedFocusValue}", bodyStyle);
            GUILayout.Label(worldOverlay
                ? "Цвет поля наложен на поверхность мира."
                : "Наложение на мир выключено; карта остаётся активной.", smallStyle);
            GUILayout.EndArea();
        }

        private string KindLabel => Kind == SteppeVisualizationKind.State
            ? "СОСТОЯНИЕ"
            : Kind == SteppeVisualizationKind.Flux ? "ПОТОК ЗА ШАГ" : "ВЕКТОРНЫЙ ПРОЦЕСС";

        private string SelectedTitle
        {
            get
            {
                switch (Kind)
                {
                    case SteppeVisualizationKind.State:
                        return StateCatalog.All[selectionIndex].Title;
                    case SteppeVisualizationKind.Flux:
                        return FluxCatalog.All[selectionIndex].Title;
                    default:
                        return VectorProcessCatalog.All[selectionIndex].Title;
                }
            }
        }

        private string SelectedDescription
        {
            get
            {
                switch (Kind)
                {
                    case SteppeVisualizationKind.State:
                        return StateCatalog.All[selectionIndex].Description;
                    case SteppeVisualizationKind.Flux:
                        return FluxCatalog.All[selectionIndex].Description;
                    default:
                        return VectorProcessCatalog.All[selectionIndex].Description;
                }
            }
        }

        private string SelectedMeasurement
        {
            get
            {
                switch (Kind)
                {
                    case SteppeVisualizationKind.State:
                        var state = StateCatalog.All[selectionIndex];
                        return $"Группа: {state.GroupTitle}  •  единица: {state.Unit}";
                    case SteppeVisualizationKind.Flux:
                        var flux = FluxCatalog.All[selectionIndex];
                        return $"Группа: {flux.GroupTitle}  •  единица: {flux.Unit}";
                    default:
                        var vector = VectorProcessCatalog.All[selectionIndex];
                        return $"Тип: {vector.Measure}  •  единица: {vector.Unit}";
                }
            }
        }

        private string SelectedScaleLabel
        {
            get
            {
                if (Kind == SteppeVisualizationKind.State)
                {
                    var descriptor = StateCatalog.All[selectionIndex];
                    return $"Шкала: {descriptor.ScaleMinimum:G4} … {descriptor.ScaleMaximum:G4} {descriptor.Unit} ({descriptor.Scale})";
                }

                if (Kind == SteppeVisualizationKind.Flux
                    && host.LatestSnapshot.TryGetFlux(FluxCatalog.All[selectionIndex].Id, out var flux))
                {
                    return $"Активно ячеек: {flux.ActiveCellCount}  •  |сумма|: {flux.AbsoluteTotal:G5} {flux.Unit}";
                }

                if (Kind == SteppeVisualizationKind.VectorProcess
                    && host.LatestSnapshot.TryGetVectorProcess(
                        VectorProcessCatalog.All[selectionIndex].Process,
                        out var vector))
                {
                    return $"Среднее: {vector.MeanMagnitude:G4}  •  максимум: {vector.MaximumMagnitude:G4} {vector.Unit}";
                }

                return string.Empty;
            }
        }

        private string SelectedFocusValue
        {
            get
            {
                if (focus == null || floatingOrigin == null)
                {
                    return "нет данных";
                }

                var latest = host.LatestSnapshot;
                var world = floatingOrigin.LocalToWorld(focus.position);
                if (!latest.Coordinates.TryWorldToCell(world.X, world.Z, out var x, out var y))
                {
                    return "вне конечного мира";
                }

                var index = y * latest.Summary.Width + x;
                switch (Kind)
                {
                    case SteppeVisualizationKind.State:
                        var stateDescriptor = StateCatalog.All[selectionIndex];
                        return latest.TryGetLayer(stateDescriptor.Id, out var state)
                            ? $"{state.Values[index]:F3} {stateDescriptor.Unit}"
                            : "нет данных";
                    case SteppeVisualizationKind.Flux:
                        var fluxDescriptor = FluxCatalog.All[selectionIndex];
                        return latest.TryGetFlux(fluxDescriptor.Id, out var flux)
                            ? $"{flux.Values[index]:G5} {fluxDescriptor.Unit}"
                            : "нет данных";
                    default:
                        var vectorDescriptor = VectorProcessCatalog.All[selectionIndex];
                        return latest.TryGetVectorProcess(vectorDescriptor.Process, out var vector)
                            ? $"{vector.Magnitude[index]:G4} {vectorDescriptor.Unit}, ({vector.VectorX[index]:G3}; {vector.VectorY[index]:G3})"
                            : "нет данных";
                }
            }
        }

        private void DrawFocusMarker(Rect mapRect, SteppeSimulationSnapshot snapshot)
        {
            var world = floatingOrigin.LocalToWorld(focus.position);
            var coordinates = snapshot.Coordinates;
            var u = (float)((world.X - coordinates.MinimumWorldX)
                            / (coordinates.MaximumWorldX - coordinates.MinimumWorldX));
            var v = (float)((world.Z - coordinates.MinimumWorldZ)
                            / (coordinates.MaximumWorldZ - coordinates.MinimumWorldZ));
            if (u < 0f || u > 1f || v < 0f || v > 1f)
            {
                return;
            }

            var marker = new Rect(
                mapRect.x + u * mapRect.width - 4f,
                mapRect.y + (1f - v) * mapRect.height - 4f,
                8f,
                8f);
            var old = GUI.color;
            GUI.color = Color.white;
            GUI.DrawTexture(marker, Texture2D.whiteTexture);
            GUI.color = old;
        }

        private void DrawVectorArrows(Rect mapRect, SteppeSimulationSnapshot snapshot)
        {
            float[] vectorX = null;
            float[] vectorY = null;
            float[] magnitude = null;
            float maximum = 1f;
            int width;
            int height;

            if (Kind == SteppeVisualizationKind.VectorProcess
                && snapshot.TryGetVectorProcess(
                    VectorProcessCatalog.All[selectionIndex].Process,
                    out var process))
            {
                vectorX = process.VectorX;
                vectorY = process.VectorY;
                magnitude = process.Magnitude;
                maximum = Mathf.Max(0.000001f, process.MaximumMagnitude);
                width = process.Width;
                height = process.Height;
            }
            else if (Kind == SteppeVisualizationKind.State
                     && snapshot.TryGetLayer(StateCatalog.All[selectionIndex].Id, out var layer)
                     && layer.VectorX != null
                     && layer.VectorY != null)
            {
                vectorX = layer.VectorX;
                vectorY = layer.VectorY;
                magnitude = layer.Values;
                maximum = Mathf.Max(0.000001f, layer.Maximum);
                width = layer.Width;
                height = layer.Height;
            }
            else
            {
                return;
            }

            var step = Mathf.Max(1, width / 12);
            var previousColor = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, 0.86f);
            for (var y = step / 2; y < height; y += step)
            {
                for (var x = step / 2; x < width; x += step)
                {
                    var index = y * width + x;
                    var length = Mathf.Sqrt(vectorX[index] * vectorX[index] + vectorY[index] * vectorY[index]);
                    if (length < 0.000001f)
                    {
                        continue;
                    }

                    var normalized = magnitude != null
                        ? Mathf.Clamp01(magnitude[index] / maximum)
                        : Mathf.Clamp01(length / maximum);
                    var lineLength = Mathf.Lerp(4f, mapRect.width / 18f, Mathf.Sqrt(normalized));
                    var centre = new Vector2(
                        mapRect.x + (x + 0.5f) / width * mapRect.width,
                        mapRect.y + (1f - (y + 0.5f) / height) * mapRect.height);
                    var angle = Mathf.Atan2(-vectorY[index], vectorX[index]) * Mathf.Rad2Deg;
                    var oldMatrix = GUI.matrix;
                    GUIUtility.RotateAroundPivot(angle, centre);
                    GUI.DrawTexture(
                        new Rect(centre.x - 1f, centre.y - 1f, lineLength, 2f),
                        Texture2D.whiteTexture);
                    GUI.DrawTexture(
                        new Rect(centre.x + lineLength - 4f, centre.y - 3f, 4f, 6f),
                        Texture2D.whiteTexture);
                    GUI.matrix = oldMatrix;
                }
            }

            GUI.color = previousColor;
        }

        private void RebuildLegend()
        {
            if (legendTexture == null)
            {
                legendTexture = new Texture2D(256, 1, TextureFormat.RGBA32, false, true)
                {
                    name = "Steppe simulation legend",
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear,
                    hideFlags = HideFlags.DontSave,
                };
            }

            var pixels = new Color32[256];
            Color32[] palette;
            if (Kind == SteppeVisualizationKind.State)
            {
                palette = ParsePalette(StateCatalog.All[selectionIndex].Palette);
            }
            else
            {
                palette = FluxPalette;
            }

            for (var index = 0; index < pixels.Length; index++)
            {
                pixels[index] = PaletteColor(palette, index / 255f, 255);
            }

            legendTexture.SetPixels32(pixels);
            legendTexture.Apply(false, false);
        }

        private static Color32 EvaluateStateColor(
            StateDescriptor descriptor,
            float value,
            Color32[] palette)
        {
            float normalized;
            switch (descriptor.Scale)
            {
                case StateScale.Logarithmic:
                    var range = Mathf.Max(0.000001f, descriptor.ScaleMaximum - descriptor.ScaleMinimum);
                    normalized = Mathf.Log(1f + Mathf.Max(0f, value - descriptor.ScaleMinimum))
                                 / Mathf.Log(1f + range);
                    break;
                case StateScale.Cyclic:
                    normalized = Mathf.Repeat(
                        (value - descriptor.ScaleMinimum)
                        / Mathf.Max(0.000001f, descriptor.ScaleMaximum - descriptor.ScaleMinimum),
                        1f);
                    break;
                case StateScale.Categorical:
                    normalized = Mathf.Repeat(value * 0.61803398875f, 1f);
                    break;
                default:
                    normalized = Mathf.InverseLerp(
                        descriptor.ScaleMinimum,
                        descriptor.ScaleMaximum,
                        value);
                    break;
            }

            return PaletteColor(palette, normalized, 222);
        }

        private static Color32[] ParsePalette(string[] values)
        {
            var result = new Color32[values.Length];
            for (var index = 0; index < values.Length; index++)
            {
                result[index] = ColorUtility.TryParseHtmlString(values[index], out var color)
                    ? color
                    : Color.magenta;
            }

            return result;
        }

        private static Color32 PaletteColor(Color32[] palette, float value, byte alpha)
        {
            value = Mathf.Clamp01(value);
            var scaled = value * (palette.Length - 1);
            var lower = Mathf.FloorToInt(scaled);
            var upper = Mathf.Min(palette.Length - 1, lower + 1);
            var color = Color.Lerp((Color)palette[lower], (Color)palette[upper], scaled - lower);
            color.a = alpha / 255f;
            return color;
        }

        private static float RobustAbsoluteMaximum(float[] values)
        {
            var maximum = 0f;
            for (var index = 0; index < values.Length; index++)
            {
                maximum = Mathf.Max(maximum, Mathf.Abs(values[index]));
            }

            return Mathf.Max(0.000001f, maximum);
        }

        private static bool TryLayerPair(
            SteppeSimulationSnapshot previous,
            SteppeSimulationSnapshot latest,
            SimulationLayer layer,
            out LayerSnapshot from,
            out LayerSnapshot to)
        {
            from = null;
            to = null;
            return previous.TryGetLayer(layer, out from)
                   && latest.TryGetLayer(layer, out to);
        }

        private static Texture2D CreateMap(string mapName, int width, int height)
        {
            return new Texture2D(width, height, TextureFormat.RGBA32, false, true)
            {
                name = mapName,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.DontSave,
            };
        }

        private void EnsureStyles()
        {
            if (titleStyle != null)
            {
                return;
            }

            titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontStyle = FontStyle.Bold,
                fontSize = 14,
                wordWrap = true,
            };
            bodyStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                wordWrap = true,
            };
            smallStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 10,
                wordWrap = true,
                normal = { textColor = new Color(0.8f, 0.84f, 0.82f) },
            };
        }

        private static int FindStateIndex(SimulationLayer layer)
        {
            for (var index = 0; index < StateCatalog.All.Count; index++)
            {
                if (StateCatalog.All[index].Id == layer)
                {
                    return index;
                }
            }

            return 0;
        }

        private static int Wrap(int value, int count)
        {
            return count <= 0 ? 0 : (value % count + count) % count;
        }

        private static void DestroyTexture(Texture texture)
        {
            if (texture == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(texture);
            }
            else
            {
                DestroyImmediate(texture);
            }
        }

        private void OnDestroy()
        {
            Shader.SetGlobalFloat(NaturalReadyId, 0f);
            Shader.SetGlobalFloat(DiagnosticStrengthId, 0f);
            Shader.SetGlobalTexture(NaturalMapId, null);
            Shader.SetGlobalTexture(DiagnosticMapId, null);
            DestroyTexture(naturalMap);
            DestroyTexture(diagnosticMap);
            DestroyTexture(legendTexture);
        }
    }
}
