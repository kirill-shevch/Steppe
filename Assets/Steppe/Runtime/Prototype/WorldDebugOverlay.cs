using Steppe.Player;
using Steppe.Settings;
using Steppe.Terrain;
using Steppe.World;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Steppe.Prototype
{
    public sealed class WorldDebugOverlay : MonoBehaviour
    {
        private SteppeWorldSettings settings;
        private FloatingOriginSystem floatingOrigin;
        private TerrainChunkStreamer chunkStreamer;
        private FlyCameraController cameraController;
        private Transform focus;
        private bool visible = true;

        public void Configure(
            SteppeWorldSettings worldSettings,
            FloatingOriginSystem origin,
            TerrainChunkStreamer streamer,
            FlyCameraController controller,
            Transform focusTransform)
        {
            settings = worldSettings;
            floatingOrigin = origin;
            chunkStreamer = streamer;
            cameraController = controller;
            focus = focusTransform;
        }

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.f3Key.wasPressedThisFrame)
            {
                visible = !visible;
            }
        }

        private void OnGUI()
        {
            if (!visible || settings == null || floatingOrigin == null || chunkStreamer == null || focus == null)
            {
                return;
            }

            var worldPosition = floatingOrigin.LocalToWorld(focus.position);
            chunkStreamer.GetLodCounts(out var near, out var middle, out var far);

            const float width = 430f;
            const float height = 178f;
            var area = new Rect(12f, 12f, width, height);
            GUI.Box(area, GUIContent.none);

            GUILayout.BeginArea(new Rect(area.x + 12f, area.y + 10f, width - 24f, height - 20f));
            GUILayout.Label("STEPPE — P0 SPACE");
            GUILayout.Label($"World XZ: {worldPosition.X:F1}, {worldPosition.Z:F1} m    Altitude: {worldPosition.Y:F1} m");
            GUILayout.Label($"Chunk: {chunkStreamer.CenterCoordinate}    Origin XZ: {floatingOrigin.OriginX:F0}, {floatingOrigin.OriginZ:F0}");
            GUILayout.Label($"Chunks: {chunkStreamer.LoadedCount} loaded / {chunkStreamer.PendingCount} queued    LOD: {near}/{middle}/{far}");
            GUILayout.Label($"Seed: {settings.WorldSeed}    Generator: v{settings.GeneratorVersion}    Speed: {cameraController.CurrentMoveSpeed:F0} m/s");
            GUILayout.Space(4f);
            GUILayout.Label("WASD move · mouse look · Q/E down/up · Shift boost · wheel speed");
            GUILayout.Label("Esc releases mouse · click captures · F3 toggles this panel");
            GUILayout.EndArea();
        }
    }
}
