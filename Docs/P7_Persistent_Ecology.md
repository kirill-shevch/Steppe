# P7 — Persistent ecology grid

P7 introduces the first dynamic memory of the steppe. It is deliberately a CPU-only
milestone: terrain and grass still render their static surface samples. P8 now publishes
this state to a world-anchored GPU texture.

## Canonical grid

- Cell size: 128 m by default.
- The grid uses absolute double-precision world coordinates and floor division.
- It is independent of 512 m terrain chunks, 64 m grass cells, and floating-origin shifts.
- Active coverage includes the complete streamed terrain horizon plus one terrain-chunk margin.
- Records are sparse and remain stored when their visual chunks or active horizon unload.

Persistence in this milestone means persistence during the running world session.
Save-file serialization is intentionally deferred until the state contract has survived
the vegetation and snow milestones.

## State contract

Each `SteppeEcoCellState` stores normalized values:

| Field | Meaning in P7 |
|---|---|
| `SurfaceWater` | Recent rain and shallow ponding; responds over hours |
| `RootWater` | Retained plant-available water; responds over days |
| `SurfaceCrust` | Dry coherent surface that rain softens and dry clay rebuilds |
| `Biomass` | Initialized from static vegetation potential; dynamic growth comes later |
| `GreenFraction` | Initialized from climate and water; phenology comes later |
| `SnowWater` | Reserved for the snow-water milestone |
| `SnowCompaction` | Reserved for wind and physical traversal effects |
| `FrozenFraction` | Reserved for freeze/thaw and soil resistance |
| `LastSimulationSeconds` | Canonical timestamp used for deterministic catch-up |

No visual color is stored. Appearance and future movement resistance are derived from
the physical state.

## Water balance

Every completed 30 game-minute interval is sampled at its midpoint:

1. `SteppeWeatherModel` supplies rain and surface wind at the cell centre.
2. `SteppeClimateModel` supplies local air temperature.
3. `SteppeAstronomy` supplies daylight.
4. Rain enters `SurfaceWater` immediately.
5. Soil texture and saturation limit infiltration into `RootWater`.
6. Temperature, daylight, wind, bare ground and vegetation affect evaporation and root loss.
7. Rain weakens the dry crust; exposed dry clay slowly rebuilds it.

Overflow is currently removed as local runoff. Rivers and lateral cell-to-cell water
transport are outside the flat-steppe scope.

## Scheduling and catch-up

`SteppeEcologySystem` is an `IWorldWorkSource` and shares the existing main-thread budget
with weather, grass and terrain. One scheduler operation advances at most one cell by one
fixed interval. The focus cell is queued first.

Newly visited cells start from the immutable surface equilibrium and replay six game hours
of canonical weather by default. Previously visited cells retain their exact earlier state
and replay every missing fixed step when they return to the active horizon. Accelerated
time may therefore create visible simulation lag instead of causing a frame spike; the F3
overlay exposes that lag and the active/stored/queued counts.

## Deliberate limits

- P7 itself has no rendering output; the state map and wet-ground shader are delivered in P8.
- Biomass and greenness do not evolve yet.
- No snow, freezing, dust emission or traversal resistance yet.
- No pruning or disk serialization yet.

These constraints keep P7 responsible for one thing: making atmospheric events leave a
deterministic, inspectable and streaming-independent history in the ground.
