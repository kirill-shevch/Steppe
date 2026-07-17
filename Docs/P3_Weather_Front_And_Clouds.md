# P3: Advected weather front and lightweight clouds

## Goal

Add the first transient actor to the autonomous steppe: a broad wet weather front that exists independently of terrain chunks, moves with the prevailing wind, carries visible cloud water, and produces a rain-intensity field. P3 makes the front readable from the horizon without yet simulating pressure, soil moisture, individual rain drops, or cloud-volume traversal.

The prototype must establish a durable separation:

```text
canonical weather model
        |
        +-- CPU sample: navigation, debug, future rain and soil
        |
        +-- 128 x 128 weather map: coverage / cloud water / rain
                         |
                         +-- one GPU cloud deck over the streamed horizon
```

Clouds are not spawned per terrain chunk. Terrain streaming can rebuild or discard geometry without creating, destroying, or changing the weather.

## Research decision

Guerrilla's *Horizon Zero Dawn* cloud system uses a large weather map whose channels describe coverage, precipitation, and cloud type, then renders a separate cloud volume around the player. The published presentation reports a roughly 35 km cloud range and shows that the expensive volumetric renderer needed low-resolution sampling and temporal reconstruction to reach its performance target. This validates the data split, but not a full ray marcher as the first URP implementation: [The Real-time Volumetric Cloudscapes of Horizon Zero Dawn](https://advances.realtimerendering.com/s2015/The%20Real-time%20Volumetric%20Cloudscapes%20of%20Horizon%20-%20Zero%20Dawn%20-%20ARTR.pdf).

NVIDIA's reference material describes two older families of solutions. Impostors can represent many discrete objects cheaply but remain fill-rate and overdraw sensitive; slice-based volume rendering combines a coarse volume with procedural high-frequency detail and animation. Both are useful future backends, but individual impostors conflict with Steppe's continuous front field: [True Impostors](https://developer.nvidia.com/gpugems/gpugems3/part-iv-image-effects/chapter-21-true-impostors), [Volume Rendering Techniques](https://developer.nvidia.com/gpugems/gpugems/part-vi-beyond-triangles/chapter-39-volume-rendering-techniques).

P3 therefore uses one low-poly, camera-centred cloud deck and one transparent URP material. It is deliberately replaceable. If flatness becomes the main visual limitation, the next renderer can consume the same weather map through a low-resolution URP full-screen pass; Unity documents that extension path through renderer features and full-screen passes: [URP full-screen pass](https://docs.unity3d.com/Packages/com.unity.render-pipelines.universal@15.0/manual/renderer-features/renderer-feature-full-screen-pass.html).

## Weather model

- A continuing sequence of long wet bands travels south. Successive front centres are currently 8.2 km apart. A persistent, lighter fair-weather cloud field bridges their edges, so the sky changes continuously rather than exposing a world-wide clear band.
- The prevailing wind is a world-space vector, currently 8 m/s.
- The complete cloud pattern is sampled in advected coordinates, so coverage, water, and rain move together without cloud objects.
- Multi-scale deterministic noise bends the front and breaks its cloud cover into coherent masses.
- A domain-warped mixture of smooth value noise and tileable cellular noise prevents the flat prototype deck from revealing square noise cells.
- The tileable detail field owns cloud topology. Coverage changes its opacity, while water and rain change density and colour; a passing front never rebuilds the silhouette.
- Normalized cloud water controls optical darkness.
- Rain intensity appears only after cloud water crosses the configured threshold.
- Clear-air regions may contain sparse fair-weather cover, but cannot rain.

The P2 seasonal debug clock currently starts at `x100`. P3 weather uses the same debug multiplier: `F6` cycles x100, x1, and x10 for both the seasons and cloud advection, while `F5` pauses both. At x100 an 8 m/s front crosses 800 metres every real second, deliberately exposing the simulation for validation. Production cadence will return to a slow, inhabitable rate after accelerated testing.

## Weather map and performance budget

- Resolution: 128 x 128 RGBA32 with mip maps.
- World coverage: at least 24 x 24 km and always larger than the streamed terrain diameter.
- Channels: red = coverage, green = cloud water, blue = rain intensity.
- Update interval: 0.6 real seconds, or after the observer moves by half a texel. Generation is budgeted at 16 rows per frame, so a refresh cannot become a single 16,384-sample CPU spike.
- Between map publications the shader continuously advects the last field by `wind * elapsed time`; slow cloud motion therefore remains smooth without increasing CPU update frequency.
- Semantic-map advection and detail-noise advection use separate clocks. The map advances from its publication timestamp, while cloud detail uses absolute weather time modulo its tile size; publishing a new map can therefore never reset the visible cloud shape.
- Rendering: one mesh, one material, one transparent draw submission; no cloud GameObjects per chunk.
- The cloud deck is a gently lifted disc with a fading outer ring, 11 km in radius and 1.35 km above the canonical datum.
- The semantic weather map has no mip chain: averaging its channels at the horizon would spread a front over the whole sky. A separate 128 x 128 tileable detail texture supplies cloud edges and relief; the cloud pass samples its authored level explicitly because automatic grazing-angle mip selection would reduce it to a uniform grey value.
- The cloud pass does not consume the terrain's exponential ground fog. At front distances that fog converges to a uniform fog colour and would erase cloud contrast entirely; the cloud disc uses its own outer-ring atmospheric fade instead.

## Visual contract

- Sparse, water-poor clouds are bright and relatively translucent.
- As water increases, the same cloud becomes grey and more opaque.
- Rain intensity further darkens saturated cores; rain clouds are a wet state of the same continuous field, not a separate cloud species.
- The cloud pattern travels in exactly the prevailing wind direction.
- The deck follows the floating origin and map centre, but its field is sampled from double-precision canonical world coordinates. Recentring cannot make the weather jump.

The first cloud renderer is a representation of the system, not the final cloud aesthetic. It intentionally prioritizes horizon coverage, causality, and profiling over close-up volume.

## Acceptance criteria

- A sample translated by `wind * time` preserves coverage, cloud water, rain, and front distance.
- Wet front samples cross the rain threshold; dry samples do not produce rain.
- Weather-map coverage exceeds the full loaded-terrain diameter.
- The runtime creates one `SteppeWeatherSystem` and one `SteppeCloudLayer`.
- Flying across chunk boundaries never spawns cloud objects or ties weather lifetime to chunks.
- F3 reports wind, coverage, cloud water, and rain at the observer.

## Deferred

- low-resolution volumetric ray marching and temporal reconstruction;
- cloud thickness, towers, anvils, and traversal from above;
- multiple interacting air masses and pressure-derived winds;
- local rain particles, wet ground, puddles, mud, runoff, and delayed vegetation response;
- cloud shadows and cloud-dependent suppression of the clear-sky diurnal temperature range;
- snow and phase changes.
