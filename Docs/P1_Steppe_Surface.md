# P1: Steppe surface ecology

## Goal

Turn the P0 heightfield into a readable living surface without introducing weather or player influence. One deterministic surface model drives both the distant ground color and the nearby geometric vegetation.

P1 is deliberately static. Moisture potential describes how the land tends to receive and retain water; it is not current soil moisture. Dynamic rain, drying, seasonal growth, wind animation, tracks, and trampling belong to later milestones.

## Canonical surface fields

For any world coordinate, terrain height, and slope, `SteppeSurfaceGenerator` produces:

- clay content;
- fertility;
- water retention;
- moisture potential;
- vegetation potential;
- climate-normal precipitation and temperature;
- four biome weights and a dominant biome;
- nominal plant height, wind coherence, motion frequency, and dust potential;
- the current prototype ground color.

The fields are continuous, deterministic functions of canonical world coordinates. They do not depend on chunk boundaries, streaming order, camera position, or a player-specific seed.

## Canonical biomes

Climate normals create four large overlapping regimes. A sample stores weights for every regime and exposes the strongest one as its dominant biome; rendering blends the weights so ecotones remain gradual.

1. **Meadow steppe** — cool or moderate and humid, with deep fertile soil, high nearly closed vegetation, tall grass, coherent slow wind waves, and almost no dust potential.
2. **Feather-grass steppe** — the iconic central regime, moderately dry and continental, with silver-green feather grass, substantial but incomplete cover, the longest directional wind waves, and moderate resources.
3. **Dry steppe** — lower precipitation and higher evaporation, short isolated tussocks, wormwood colors, exposed ochre soil, incoherent high-frequency movement, and strong dust potential.
4. **Desert steppe** — effective precipitation around and below the 250 mm regime, dominant exposed ground, sparse low rigid plants, and surface movement expressed primarily through dust.

The biome is a long-lived property of climate and soil. Current rain, drought, heat, frost, and fog will later alter a biome's **state**, never silently convert it to another biome.

Every sample already publishes future animation and weather-response inputs:

- nominal vegetation height and exposed-ground fraction;
- wind coherence and motion frequency;
- dust potential;
- climate-normal temperature and annual precipitation.

## Presentation

- Terrain vertices receive colors sampled from the surface fields.
- Broad soil and vegetation patches therefore remain visible beyond the geometry-grass radius.
- LOD 0 and LOD 1 chunks receive deterministic grass-tuft meshes.
- LOD 2 renders no individual tufts; its terrain color represents the same vegetation potential.
- Grass is currently untextured prototype geometry. Its purpose is to prove density, continuity, and LOD ownership before final vegetation assets and GPU rendering are selected.
- The F3 panel shows both the dominant biome and all four blend weights. The dominant label changes only after another weight becomes largest.
- Debug keys `1` through `4` move the camera to representative meadow, feather-grass, dry, and desert-steppe regions. These are inspection shortcuts, not player navigation.

## Acceptance criteria

- Repeated samples at the same coordinates are identical.
- Nearby samples change continuously rather than at chunk boundaries.
- The nearest ecotone can be reached from the starting region within 30 km.
- Soil, retention, and vegetation form broad readable regions rather than pixel noise.
- Terrain color and grass density are derived from the same surface sample.
- Grass placement is stable after a chunk unloads and reloads.
- Individual vegetation is absent outside its configured LOD radius.
- No P1 system modifies the canonical terrain or stores player-authored changes.

## Deferred

- current soil moisture and surface water;
- rain, evaporation, snow, and freezing;
- seasonal growth and dormancy;
- species distribution;
- wind fields and grass animation;
- trampling, tracks, and vegetation recovery;
- production vegetation assets and a final GPU renderer.
