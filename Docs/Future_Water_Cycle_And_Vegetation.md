# Future task: water cycle and vegetation ecology

## Status

Deferred. This task is explicitly separate from P3 cloud presentation and P4 grass
rendering/wind response. Current clouds may contain water and report rain intensity,
but they do not yet participate in a persistent water cycle and do not change plants.

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

A small world-anchored state texture will eventually be sampled by both grass and terrain:

- R: immediate surface wetness;
- G: live biomass/height;
- B: greenness versus cured material;
- A: frost/dormancy.

The future gate is: soil darkens during rain, greening follows later, the effect remains
after the cloud passes, and a hot dry interval reverses it gradually.
