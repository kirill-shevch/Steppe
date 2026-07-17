using System;

namespace Steppe.Surface
{
    public enum SteppeBiome
    {
        Meadow,
        FeatherGrass,
        Dry,
        Desert
    }

    public readonly struct BiomeWeights
    {
        public BiomeWeights(double meadow, double featherGrass, double dry, double desert)
        {
            var total = meadow + featherGrass + dry + desert;
            if (total <= double.Epsilon)
            {
                Meadow = 0.0;
                FeatherGrass = 1.0;
                Dry = 0.0;
                Desert = 0.0;
                return;
            }

            Meadow = meadow / total;
            FeatherGrass = featherGrass / total;
            Dry = dry / total;
            Desert = desert / total;
        }

        public double Meadow { get; }
        public double FeatherGrass { get; }
        public double Dry { get; }
        public double Desert { get; }

        public SteppeBiome Dominant
        {
            get
            {
                var dominant = SteppeBiome.Meadow;
                var maximum = Meadow;
                if (FeatherGrass > maximum)
                {
                    dominant = SteppeBiome.FeatherGrass;
                    maximum = FeatherGrass;
                }

                if (Dry > maximum)
                {
                    dominant = SteppeBiome.Dry;
                    maximum = Dry;
                }

                if (Desert > maximum)
                {
                    dominant = SteppeBiome.Desert;
                }

                return dominant;
            }
        }

        public double Blend(double meadow, double featherGrass, double dry, double desert)
        {
            return Meadow * meadow + FeatherGrass * featherGrass + Dry * dry + Desert * desert;
        }
    }
}
