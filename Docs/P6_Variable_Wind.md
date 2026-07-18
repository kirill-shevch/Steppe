# P6 — Variable wind regimes

## Status

Implemented. The atmosphere now has deterministic, smoothly blended wind regimes
derived from canonical simulation time.

## Contract

`SteppeWindRegimeModel` publishes two related flows:

- cloud velocity and its analytically integrated advection;
- surface velocity and its independently integrated advection;
- large-scale gustiness.

Target regimes are deterministic functions of seed, regime index, and season. Velocity
uses a smoothstep blend. Advection uses the exact integral of that curve, so querying a
timestamp directly produces the same cloud position as advancing through every prior
frame.

The weather field is evaluated in material coordinates:

```text
weatherPosition = worldPosition - cloudAdvection(time)
```

The last published weather map is reprojected by the difference between current and map
build advection. Volumetric detail uses the complete current cloud advection. A wind turn
therefore bends the trajectory without rotating or teleporting the existing cloud field.

Grass and far terrain receive surface velocity plus surface advection. Their material gust
pattern keeps a stable prevailing basis while the plant response follows current local
surface direction. A wet front adds a bounded surface gust on its leading edge; this gust
is also stored in the weather map alpha channel for later dust and ecology consumers.

## Validation

- equal seed and timestamp produce bit-identical regime samples;
- velocity is continuous across regime boundaries;
- cloud and surface wind remain related but distinct;
- the complete scalar weather pattern is invariant when translated by integrated cloud
  advection;
- the front leading edge increases surface wind without changing cloud velocity;
- EditMode and PlayMode suites publish and validate the new shader state.
