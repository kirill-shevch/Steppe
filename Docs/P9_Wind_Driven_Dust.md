# P9 — Wind-driven dust

P9 turns the immutable P1 `DustPotential` into a current surface process. Dust is not a
biome decal and is not stored in ecology records. It is derived from:

```text
soil dust potential
× exposed ground
× dry loose surface
× wind above the local threshold
× no rain
× no snow
```

Water, vegetation, dry crust and snow all raise the threshold or bind particles. A dry
desert cell can therefore remain clear during calm weather, while a storm gust lifts low
streams immediately before rain and then suppresses them when precipitation arrives.

`SteppeDustPresentation` owns one camera-local particle system. Candidate sources are
quantized in canonical world coordinates and read P7 cell state, so emission comes from
stable ground patches without creating one GameObject per chunk. Existing particles are
shifted when the floating origin moves.

The debug overlay reports current emission, displayed response, threshold wind speed,
dryness, looseness and particle count. The player-facing signal is the low wind-aligned
stream itself; the overlay is validation instrumentation only.

Gate:

- no dust below threshold even in desert steppe;
- visible low streams on dry loose ground during a strong gust;
- streams follow surface wind, not cloud wind;
- wet ground, active rain, crust and snow independently suppress emission;
- dust sources and existing particles remain continuous across chunk and origin shifts.
