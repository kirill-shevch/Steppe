# P10 — Vegetation phenology

P10 advances the biomass and green-fraction channels reserved by P7/P8. Rain never colors
grass directly. It first fills surface and root water; accessible root water and warmth
then control vegetation. The two stored values represent three useful quantities without
expanding the persistent record:

```text
total standing mass = Biomass
live mass           = Biomass × GreenFraction
dry standing mass   = Biomass - live mass
```

```text
root water + warmth + fertility
        -> fast greening
        -> slower biomass recovery

drought + damaging heat + frost
        -> fast curing (live becomes dry)
        -> later decomposition and lodging of the dry stand
```

Green fraction changes faster than total biomass so a tall dry stand remains physically
present. Wind, snow load and thaw can lodge dry stems; warm/moist conditions decompose
them. Over a full 15-day season this changes total height and density visibly. The grass
shader recolors living and cured tissue, scales height and selects a stable subset of the
existing instances. Returning growth reveals the same plants in reverse order. Far-terrain
vegetation samples the same field.

Gate:

- wet ground appears before a visibly green response;
- greenness changes faster than height and density;
- hot drought leaves standing golden biomass rather than erasing grass immediately;
- recovery does not reshuffle plant anchors;
- unloaded records retain their phenological history.
