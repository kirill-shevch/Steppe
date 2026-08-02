using UnityEngine;

namespace Steppe.Presentation
{
    /// <summary>
    /// Keeps immediate-mode UI at a stable percentage of the screen by drawing it
    /// on a virtual 1920x1080 canvas. The shorter screen axis determines the scale,
    /// so ultrawide displays gain horizontal room without oversized text.
    /// </summary>
    public static class SteppeGuiScale
    {
        public const float ReferenceWidth = 1920f;
        public const float ReferenceHeight = 1080f;
        public const float MinimumScale = 0.75f;
        public const float MaximumScale = 4f;

        public static float Scale => CalculateScale(Screen.width, Screen.height);
        public static float Width => Screen.width / Scale;
        public static float Height => Screen.height / Scale;

        public static float CalculateScale(int pixelWidth, int pixelHeight)
        {
            var widthScale = Mathf.Max(1, pixelWidth) / ReferenceWidth;
            var heightScale = Mathf.Max(1, pixelHeight) / ReferenceHeight;
            return Mathf.Clamp(
                Mathf.Min(widthScale, heightScale),
                MinimumScale,
                MaximumScale);
        }

        public static Matrix4x4 Begin()
        {
            var previous = GUI.matrix;
            var scale = Scale;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            return previous;
        }

        public static void End(Matrix4x4 previous)
        {
            GUI.matrix = previous;
        }
    }
}
