# P13 — Caravan keeper vertical slice

P13 replaces the development rolling sphere with the first playable caravan. The
current handling-test configuration deliberately contains only a wheeled chassis,
one steering wheel and constant test propulsion. The sail is temporarily absent so
steering, ground friction and traversal resistance can be tuned in isolation.

## Playable loop

The player exists as a first-person keeper on a moving deck:

1. enter the physical steering station;
2. turn the wheel while the chassis drives forward at a constant force;
3. leave the station and walk around the moving platform;
4. read chassis state from physical gauges;
5. clean dust or repair damage while looking at the chassis.

There is no autonomous pilot. The wheel retains the last angle set by the player,
but the caravan does not choose a course as the ground changes.

## Controls

| Input | Action |
|---|---|
| `WASD` | walk |
| `Shift` | run |
| `Space` | jump |
| mouse | look |
| `E` | enter or leave the targeted steering station |
| `A` / `D` | turn the active steering wheel |
| hold `C` | clean the targeted module |
| hold `R` | repair the targeted module |
| `B` | enter or leave build mode while nearly stopped |
| left click in build mode | pick up or place a future movable module |
| `R` in build mode | rotate the held module by 90 degrees |
| right click in build mode | return the held module to its previous mount |

The mouse pointer remains a centre-screen world ray. No inventory, status window
or persistent gameplay HUD is introduced.

The front station has a steering wheel. A small lamp appears when the keeper aims
at it and grows while `E` has it engaged. Mouse movement never changes steering:
it is reserved for first-person look outside the engaged station.

## Diegetic state

Every demo module exposes three physical bars:

- orange: accumulated dust;
- green: remaining integrity;
- blue: current mechanical load.

Dust and integrity reduce efficiency gradually instead of switching a module off.
The chassis reads P12 surface resistance for propulsion, longitudinal and lateral
ground friction, speed limit, dust and wear.

## Chassis handling test

The chassis uses the Vehicle Physics Pro Community Edition controller, one Rigidbody,
four `VPWheelCollider` suspension units and a compact body collider. It becomes dynamic
only after the near terrain streamer exposes a physics surface. The caravan root
replaces the old sphere as the canonical focus for terrain, grass, weather, ecology,
tracks and floating-origin shifts.

Every `CaravanModule` contributes its own mass and local mass centre. The demo
chassis contributes 1280 kg for the frame, wheels and steering station. Installing,
removing or moving future modules recalculates the Rigidbody mass and combined
centre of mass.

The VPP engine supplies a constant test throttle and keeps the automatic transmission
in a forward gear. The physical steering wheel sends its `A`/`D` value to VPP steering.
Soil resistance changes the target speed and tyre-friction multiplier. There is no
player throttle, sail force or wind dependence in this temporary configuration.

The keeper is a CharacterController independent of the Rigidbody. While grounded
on a caravan collider, carrier translation and rotation are applied before player
movement. A jump inherits the platform's planar point velocity.

## Build mode

The deck retains its `4 × 8` one-metre mount grid and one-item construction buffer,
but the handling-test configuration contains no movable module. The same grid will
be reused when caravan parts return.

The pure occupancy model does not depend on Unity physics and is covered by EditMode
tests. Future parts can reuse the same `CaravanModule` footprint contract.

## Procedural greybox

`CaravanDemoFactory` creates the demo from a small runtime construction kit:

- tiled deck and frame beams;
- four VPP-driven wheels with procedural tyre visuals;
- steering wheel;
- physical state gauges.

The hierarchy and pivots are intended to survive replacement of the greybox visuals
with authored or generated meshes. Gameplay code addresses modules, pivots and mount
footprints rather than individual renderers.

## Deferred

P13 does not yet implement:

- active sail and sail-trim station;
- water reservoirs, extraction or circulation;
- photovoltaic leaves, batteries or electric motors;
- biomass harvester, dryer, storage, furnace or biofuel engine;
- water pipes and electrical cables;
- ropes between multiple chassis;
- resource costs for repair;
- additional caravan modules or living spaces.

Those systems should extend the existing module state, mount grid, environment
sampler and interaction ray instead of creating a second construction or UI layer.
