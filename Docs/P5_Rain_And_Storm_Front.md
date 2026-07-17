# P5 — Rain and storm-front presentation

## Goal

Make precipitation a visible consequence of the canonical weather field. A storm
must read as one broad front: fair white cloud thickens, becomes water-heavy and
nearly black near the rain core, then brightens as the cloud water is released.

P5 deliberately stops at visual weather. Soil moisture, surface water, vegetation
growth and the wider water cycle belong to a later ecology milestone.

## Simulation contract

`SteppeWeatherModel` remains the only authority and exposes, for every world
coordinate and weather time:

- cloud coverage;
- cloud water;
- rain intensity;
- prevailing wind.

The front has an asymmetric lifecycle along the wind axis:

```text
wind →  white cloud  →  darkening / condensation  →  black rain core  →  drained light cloud
```

Cloud cover has a wider and longer envelope than cloud water. This is what lets a
cloud brighten after raining instead of disappearing immediately. The full field
advects with the prevailing wind and is independent of terrain chunks.

## Presentation

Clouds use a custom URP renderer feature instead of scene geometry. At half screen
resolution it reconstructs one world-space ray per pixel, intersects that ray with
the cloud altitude range, and raymarches a three-dimensional density field. A
second pass composites the premultiplied result only over sky pixels using camera
depth. There is therefore no cloud dome, cloud-box edge, or horizon curtain.

The canonical weather map provides broad coverage, cloud water, and rain. A small
deterministic tileable 3D texture supplies local volume and erosion. Both fields
advect with the canonical wind in world coordinates, so the rendered cloud mass
does not follow or rebuild around the player when the floating origin moves.

Cloud water increases density and self-shadowing; active rain produces the darkest
cores without tinting clear sky. The vertical density profile softens both the base
and top of the layer, while distance extinction dissolves clouds into atmospheric
haze. The initial quality budget is 20 half-resolution view samples and three
light samples per occupied point, without temporal accumulation.

Successive storm centres are farther apart than the complete visible cloud
horizon. Only one wet front can be visible at a time; fair-weather cloud fields
fill the long interval before the next front arrives beyond the horizon.

Rain uses one camera-centred `ParticleSystem`:

- a box emitter follows the observer;
- particles simulate in world space;
- existing drops remain in place while the emitter follows the camera;
- wind supplies horizontal velocity;
- stretched billboards create lightweight streaks;
- emission rate follows `CurrentAtFocus.RainIntensity` with a short visual fade.

There are no rain objects per cloud, terrain chunk or weather-map texel. Visual
cost therefore stays bounded as the streamed horizon grows.

## Acceptance criteria

- A continuous part of every wet front produces rain, not a lone random cloud.
- The darkest and most opaque cloud mass coincides with the rain core.
- Cloud water is lower behind the core while cloud cover is still visible.
- Rain direction agrees with the canonical wind.
- The rain volume follows the flying camera without particles moving with it.
- Weather-map construction remains incremental through `WorldWorkScheduler`.
- Cloud presentation is a screen-space URP pass and creates no visible carrier mesh.
- Cloud density is volumetric and has no geometric edge at the camera horizon.
- No P5 code changes soil, water storage or vegetation state.
