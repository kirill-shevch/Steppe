# P12 — physical traversal and readable tracks

P12 replaces the prototype fly camera with a canonical rolling sphere. The sphere,
not the camera, is now the focus for terrain, weather, ecology, grass streaming and
floating-origin shifts. The camera is a presentation-only orbit follower.

## Controls

- `WASD` applies rolling force relative to the camera;
- `Shift` adds a short stronger push, but never bypasses surface resistance;
- mouse orbits the camera;
- mouse wheel changes follow distance;
- `1–4` remain development-only biome teleports;
- `F3`, `F5` and `F6` retain the diagnostic panel, pause and time acceleration.

## Authoritative traversal model

The ball samples the same `SurfaceSample` and `SteppeEcoCellState` used by the
terrain and grass shaders. `SteppeTraversalModel` derives six continuous signals:

```text
mud             = liquid water × clay × thawed soil
loose ground    = dryness × sandiness × no crust × exposed surface
soft snow       = snow cover × inverse compaction
frozen firmness = frozen fraction outside soft snow
resistance      = mud + loose ground + soft snow - frozen firmness
sink depth      = visible/tactile depression from mud, snow and loose soil
```

Wet clay therefore slows and visually sinks the ball, loose dry soil reduces its
effective speed, fresh snow produces the deepest sink, and frozen or compacted
ground becomes firm again. Only near-LOD terrain chunks cook mesh colliders.

## Persistent track map

`SteppeTrackSystem` stores sparse cells in canonical world coordinates, independent
of terrain chunks and floating origin. A 512×512 GPU window at 1.5 m per cell covers
768 m around the ball. Its channels are:

| Channel | Meaning | World response |
|---|---|---|
| R | vegetation flattening | blades become short and lie sideways |
| G | snow compression | track becomes darker, bluer and smoother |
| B | soil rut | terrain is visually displaced downward |
| A | wet print | fresh track remains dark and glossy |

Tracks recover on different biological timescales: wet prints fade in hours,
flattened vegetation and snow tracks in days, and soil ruts in weeks. Sparse records
survive streamed chunk unloads for the current session.

## Added direct visual signals

- dry cohesive crust now develops a warped crack network;
- uncrusted dry ground has fine loose-grain variation and remains dust-capable;
- compacted snow is darker, bluer and smoother than fresh snow;
- hot dry air creates a moving mirage band at the horizon;
- humid air, precipitation and wet ground thicken the atmospheric horizon;
- physical speed, sink and the form of the track expose soil state without HUD data.

## Manual acceptance

1. On normal firm ground the ball rolls close to its maximum speed and barely sinks.
2. In rain on clay-rich ground it slows, sinks and leaves a dark rut; dust stops.
3. On a dry loose patch it slows moderately and a strong wind raises dust.
4. On dry crust it rolls well, cracks are visible and dust requires stronger wind.
5. Fresh snow produces the deepest sink and a compact blue-grey trail.
6. Frozen or already compacted snow is visibly harder and faster to cross.
7. Grass remains in stable positions, but is laid down behind the ball.
8. Drive farther than a floating-origin shift and return: the track stays registered
   to the same canonical ground position.

