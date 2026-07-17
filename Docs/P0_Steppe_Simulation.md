# P0: Steppe space simulation

## Goal

Prove that Steppe can present one canonical, practically unbounded landscape that remains stable while an observer travels through it. P0 deliberately contains no caravan, weather, vegetation, animals, resource loops, or collision gameplay.

The flying camera is the current point of presence. Its absolute world position drives terrain streaming and is the future center of local weather, vegetation interaction, and encounter simulation.

## Controls

- `WASD`: move
- Mouse: choose view direction
- `Q` / `E`: descend / ascend
- `Shift`: accelerate
- Mouse wheel: change base movement speed
- `Esc`: release pointer
- Left click: capture pointer
- `F3`: toggle diagnostic overlay

The camera intentionally has no collider and does not follow the terrain.

## Runtime layers

1. `WorldPosition` and `ChunkCoordinate` preserve logical coordinates independently of Unity's local float coordinates.
2. `TerrainHeightGenerator` produces the canonical heightfield from seed, generator version, and world coordinates.
3. `TerrainOverrideMap` applies sparse authored corrections without changing the generator everywhere else.
4. `TerrainChunkStreamer` creates concentric LOD rings around the flying camera and recycles chunks that leave the active radius.
5. `FloatingOriginSystem` keeps the camera and active world content near Unity's local origin while retaining absolute logical coordinates.
6. `WorldWorkScheduler` shares one measured main-thread budget between terrain, weather, and vegetation generation so their independent queues cannot stack full quotas in one frame.
7. Presentation consumes the generated data but does not own the simulation state.

Future weather systems must query the camera through `FloatingOriginSystem.LocalToWorld` and place spawned presentation objects under the shared `World Space` root.

## P0 acceptance criteria

- The same seed, generator version, and coordinates produce bit-identical height samples.
- Negative and very large coordinates map to stable chunk coordinates.
- Adjacent chunks share height samples even when rendered at different LODs.
- Moving across chunk boundaries streams new terrain without changing previously defined coordinates.
- Floating-origin shifts do not change the observer's absolute position.
- Sparse author stamps can change one region without making the world player-randomized.
- Loaded chunk count remains bounded by the configured streaming radius.
- The terrain is dominated by broad plains, basins, valleys, and plateaus rather than uniform small-scale noise.

## Remaining P0 validation

- Compare the current mesh-chunk renderer with a Unity Terrain tile spike before committing to the final renderer.
- Profile chunk generation and eliminate visible frame spikes, likely by separating data generation from main-thread mesh upload.
- Add an automated long-distance streaming soak test.
- Establish a first visual reference set and tune macro/meso/micro terrain parameters against it.

## Automated coverage

- EditMode tests verify coordinate flooring, deterministic large-coordinate height sampling, cross-LOD chunk edges, and authored override locality.
- PlayMode smoke tests verify that the runtime bootstrap creates a collision-free flying camera, starts terrain streaming, and preserves absolute coordinates through a floating-origin shift.
