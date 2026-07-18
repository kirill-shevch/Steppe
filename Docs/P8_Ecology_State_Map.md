# P8 — World-anchored ecology state map

P8 makes the persistent P7 soil state visible without rebuilding terrain meshes or grass
instance buffers. One CPU-authored texture is sampled by both renderers in canonical world
coordinates.

## Texture contract

The default map is 128 by 128 RGBA32 texels. One texel covers one 128 m ecology cell, so
the texture spans 16.384 km per side.

| Channel | Meaning |
|---|---|
| R | Immediate surface water |
| G | Biomass relative to the immutable vegetation capacity |
| B | Green fraction, reserved for the phenology milestone |
| A | Dry surface crust |

The neutral value is `(0, 1, 0.5, 0)`: dry ground, unchanged grass density and height,
neutral unused greenness, and no extra crust presentation. Missing cells therefore preserve
the P1/P4 appearance rather than becoming empty or black.

Relative biomass avoids applying the biome's static vegetation potential twice. P4 already
uses that potential while placing stable grass candidates. Future biomass changes can now
select a stable subset and scale height without regenerating those candidates.

## World anchoring

The map origin is an absolute ecology-cell coordinate. Shaders reconstruct map UVs from:

```text
local render position
+ floating-origin canonical modulo
- state-map canonical modulo origin
```

The modulo period is 65,536 m, larger than the complete state map. Positive modular
subtraction preserves sampling across both floating-origin shifts and period boundaries.
The map recentres only when the player approaches its safe inner margin, not on every
terrain or grass chunk transition.

## Updates

Changed cells update a CPU pixel buffer immediately. The complete 64 KiB texture upload is
batched to at most four times per real second. Recentering rebuilds and publishes the map
atomically so shaders never combine a new origin with old pixels.

The map is a presentation cache, not ecological truth. P7 records remain authoritative and
persist after leaving the texture window.

## Terrain response

Surface water darkens mostly exposed soil and adds a directional wet highlight. The effect
is continuous across terrain LOD seams because every chunk samples the same canonical map.
Dry crust slightly lightens exposed ground and is suppressed by wetness.

## Grass response

The grass vertex shader samples relative biomass at each canonical root position. It scales
height and compares the existing stable random threshold against biomass, so future loss and
recovery reveal the same plants in reverse order. In P8 biomass remains at equilibrium, so
this connection is visually neutral. Rain does not directly recolour grass.

## Deferred

- Dynamic greening, growth and curing.
- Snow/frost channels and presentation.
- Dust emission from dry, uncrusted pixels.
- Disk serialization of authoritative ecology records.
