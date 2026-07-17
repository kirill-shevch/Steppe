# P2: World time and clear-sky seasons

## Goal

Give the autonomous steppe a long temporal rhythm before weather is introduced. P2 owns canonical world time, astronomical sunlight, a clear-sky temperature baseline, and presentation of the day-night cycle.

P2 does not create clouds, fronts, precipitation, current soil moisture, or seasonal plant state. Those systems will consume P2 time and climate data in later milestones.

## Time composition

- The underlying `x1` rate keeps one game day at 20 real minutes.
- The build starts at `x1`; the optional `x100` debug rate makes one game day last about 12 real seconds.
- The temporary test year contains 12 game days: each season lasts approximately 3 game days or 36 real seconds.
- The starting point is spring, day 2, at 08:00.
- The clock uses accumulated double-precision simulation seconds and is independent of frame rate.
- `F5` pauses or resumes world time.
- `F6` cycles debug multipliers `x1`, `x10`, and `x100` without changing the canonical clock model.

The 12-day year remains a temporary validation setting. Normal play starts at `x1`,
while `x10` and `x100` only expose slow changes during testing. The production cadence
will return to long seasons intended to be inhabited across multiple sessions.

## Astronomy

`SteppeAstronomy` derives solar declination, direction, elevation, and daylight from time of year, time of day, and the configured latitude. The prototype latitude is 48 degrees north.

The resulting sun state drives:

- directional-light rotation, intensity, and color;
- night, dawn, and daylight fog color;
- fog density and ambient intensity;
- global shader values for absolute day, year phase, and daylight.

## Clear-sky climate

`SteppeClimateModel` combines the surface's climate-normal temperature with:

- a seasonal wave whose warmest period lags the summer solstice;
- a diurnal wave whose warmest hour is approximately 15:00.

This is a baseline air temperature. Future weather systems will add air-mass offsets, clouds, fronts, rain cooling, and other transient effects. Time changes the state of a biome but never its canonical biome weights.

## Acceptance criteria

- Equal elapsed simulation time always produces the same clock state.
- Day and year boundaries wrap without discontinuities.
- At the prototype latitude the sun is above the horizon at noon and below it at midnight.
- The afternoon is warmer than the pre-dawn period under an otherwise identical clear sky.
- Seasonal and daily temperature changes do not change the canonical biome.
- Lighting and fog follow the clock without rebuilding terrain chunks.
- P2 runtime systems are created automatically by the prototype bootstrap.

## Deferred to P3 and P4

- pressure zones, wind vectors, clouds, fronts, rain, and snow;
- cloud influence on sunlight and daily temperature range;
- current surface moisture, puddles, mud, and evaporation;
- biome-specific greening, flowering, drying, frost, and dormancy;
- wind-driven vegetation waves and dust.
