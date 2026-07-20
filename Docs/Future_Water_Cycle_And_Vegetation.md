# Future task: water cycle and vegetation ecology

## Status

Implemented through P11. P7 stores persistent soil memory, P8 publishes it to the GPU,
P9 derives wind-driven dust, P10 advances biomass and greenness, and P11 adds snowfall,
snow storage, melt and frozen soil. Disk serialization and traversal resistance remain
later milestones.

## Goal

Create one causal chain shared by weather, ground, and vegetation:

```text
atmospheric moisture
        -> water-bearing clouds
        -> precipitation
        -> persistent soil water
        -> plant water access
        -> delayed growth, greening, curing, and dormancy
```

Rain must never recolour grass directly. The ground receives water first; vegetation
responds later according to temperature, soil, and its existing biomass. Weather changes
the state of a biome, never its canonical P1 biome identity.

## Planned simulation contract

`SteppeVegetationStateSystem` maintains a coarse world-anchored field. Each active cell
stores:

- surface soil water;
- deeper retained water;
- live biomass/height potential;
- greenness versus cured dry mass;
- frost or dormancy state;
- last canonical simulation timestamp.

The fixed-step conceptual bucket model is:

```text
soil water += rain * infiltration(retention, slope)
soil water -= evaporation(temperature, daylight, wind, cover)

growth += warm * accessible water * fertility * available capacity
senescence += drought + damaging heat + frost
greenness approaches its moisture/temperature target faster than biomass changes
```

Newly visible cells initialize from P1 climate equilibrium and perform deterministic
coarse catch-up. Unloading a terrain or grass cell must not erase wet ground or reset
plant state.

## Rendering output

A small world-anchored state texture is now sampled by both grass and terrain (P8):

- R: immediate surface wetness;
- G: live biomass relative to the immutable local vegetation capacity;
- B: greenness versus cured material;
- A: dry soil crust.

Snow, compaction and frozen soil use a separate cryosphere map rather than silently
reusing the soil-crust channel.

The implemented gate is: soil darkens during rain, greening follows later, the effect
remains after the cloud passes, and a hot dry interval reverses it gradually. Cold
precipitation accumulates as snow and a thaw releases that stored water.
