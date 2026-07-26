using System;

namespace Steppe.Caravan
{
    /// <summary>
    /// Small common state shared by every caravan part in the first vertical slice.
    /// Resource networks will extend the inputs later; maintenance can already use the
    /// same dust, integrity and load contract.
    /// </summary>
    [Serializable]
    public sealed class CaravanModuleState
    {
        private float dust;
        private float integrity = 1f;
        private float load;

        public float Dust => dust;
        public float Integrity => integrity;
        public float Load => load;
        public float Efficiency => Clamp01((0.42f + integrity * 0.58f) * (1f - dust * 0.36f));

        public void SetLoad(float value)
        {
            load = Clamp01(value);
        }

        public void AccumulateDust(float amount)
        {
            dust = Clamp01(dust + Math.Max(0f, amount));
        }

        public void Damage(float amount)
        {
            integrity = Clamp01(integrity - Math.Max(0f, amount));
        }

        public void Clean(float amount)
        {
            dust = Clamp01(dust - Math.Max(0f, amount));
        }

        public void Repair(float amount)
        {
            integrity = Clamp01(integrity + Math.Max(0f, amount));
        }

        public void SetForTests(float newDust, float newIntegrity, float newLoad = 0f)
        {
            dust = Clamp01(newDust);
            integrity = Clamp01(newIntegrity);
            load = Clamp01(newLoad);
        }

        private static float Clamp01(float value)
        {
            return Math.Max(0f, Math.Min(1f, value));
        }
    }
}
