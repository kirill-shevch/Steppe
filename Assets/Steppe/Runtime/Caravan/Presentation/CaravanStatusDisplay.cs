using UnityEngine;

namespace Steppe.Caravan
{
    /// <summary>
    /// Three diegetic bars: ochre dust, green integrity and blue mechanical load.
    /// The display is physical scene geometry and does not create screen-space UI.
    /// </summary>
    public sealed class CaravanStatusDisplay : MonoBehaviour
    {
        private Transform dustBar;
        private Transform integrityBar;
        private Transform loadBar;

        public void Configure(Transform dust, Transform integrity, Transform load)
        {
            dustBar = dust;
            integrityBar = integrity;
            loadBar = load;
        }

        public void SetValues(float dust, float integrity, float load)
        {
            SetBar(dustBar, dust);
            SetBar(integrityBar, integrity);
            SetBar(loadBar, load);
        }

        private static void SetBar(Transform bar, float value)
        {
            if (bar == null)
            {
                return;
            }

            var normalized = Mathf.Clamp01(value);
            var scale = bar.localScale;
            scale.y = Mathf.Lerp(0.03f, 0.42f, normalized);
            bar.localScale = scale;
            var position = bar.localPosition;
            position.y = -0.21f + scale.y * 0.5f;
            bar.localPosition = position;
        }
    }
}
