# P13 — Caravan keeper vertical slice

P13 replaces the development rolling sphere with the first playable caravan. The
slice deliberately contains only a wheeled chassis, one sail and two physical
control stations. Water, electricity, biomass and communication networks remain
deferred, but the player-facing verbs and the extension points for later modules
are present.

## Playable loop

The player exists as a first-person keeper on a moving deck:

1. read the wind from the steppe and the sail;
2. use the steering or sail-trim station;
3. walk around the moving platform;
4. read module state from physical gauges;
5. clean dust or repair damage while looking at a module;
6. stop the caravan and reposition the sail in build mode.

There is no autonomous pilot. Steering and trim retain the last values set by the
player, but the caravan does not choose new values as weather or ground changes.

## Controls

| Input | Action |
|---|---|
| `WASD` | walk |
| `Shift` | run |
| `Space` | jump |
| mouse | look |
| `E` | enter or leave the targeted steering/trim station |
| `A` / `D` or horizontal mouse movement | adjust the active station |
| hold `C` | clean the targeted module |
| hold `R` | repair the targeted module |
| `B` | enter or leave build mode while nearly stopped |
| left click in build mode | pick up the sail or place its ghost |
| `R` in build mode | rotate the held module by 90 degrees |
| right click in build mode | return the held module to its previous mount |

The mouse pointer remains a centre-screen world ray. No inventory, status window
or persistent gameplay HUD is introduced.

The two control stations are physically distinct. The front station has a steering
wheel; the station beside the mast is a sail-trim winch. A small lamp appears when
the keeper aims at a station and grows while `E` has it engaged. Steering only
changes wheel angle and therefore becomes effective after the sail starts moving
the chassis.

## Diegetic state

Every demo module exposes three physical bars:

- orange: accumulated dust;
- green: remaining integrity;
- blue: current mechanical load.

Dust and integrity reduce efficiency gradually instead of switching a module off.
The sail accumulates dust from the authoritative P9 dust field and can take slow
overload damage in strong apparent wind. The chassis reads P12 surface resistance
for wheel grip, rolling loss, speed limit, dust and wear.

## Chassis and sail

The chassis uses a Rigidbody and four WheelColliders. It becomes dynamic only after
the near terrain streamer exposes a physics surface. The caravan root replaces the
old sphere as the canonical focus for terrain, grass, weather, ecology, tracks and
floating-origin shifts.

Every `CaravanModule` contributes its own mass and local mass centre. The demo
chassis contributes 1280 kg (frame, wheels and control stations), while the sail
module contributes 90 kg above the deck. Installing, removing or moving a module
recalculates the Rigidbody mass and combined centre of mass. The initial assembly
therefore weighs 1370 kg and keeps its centre of mass below deck level.

The sail computes apparent wind from the current P6 surface wind minus caravan
velocity. Manual trim rotates the sail and changes its aerodynamic force. That force
is applied at the mast rather than at the centre of mass, so poor trim and gusts can
also create yaw while the chassis remains stable against rollover.

The keeper is a CharacterController independent of the Rigidbody. While grounded
on a caravan collider, carrier translation and rotation are applied before player
movement. A jump inherits the platform's planar point velocity.

## Build mode

The deck is a `4 × 8` one-metre mount grid. Build mode uses one temporary item
buffer:

1. selecting the sail removes its occupied cells and hides the live module;
2. a transparent copy follows the targeted deck cell;
3. green means the rotated footprint is free; red means it is invalid;
4. placement registers the new footprint and restores the live module;
5. cancellation restores the previous placement.

The pure occupancy model does not depend on Unity physics and is covered by EditMode
tests. Future parts can reuse the same `CaravanModule` footprint contract.

## Procedural greybox

`CaravanDemoFactory` creates the demo from a small runtime construction kit:

- tiled deck and frame beams;
- four wheels with correct rotation pivots;
- mast, spars and a double-sided procedural sail;
- steering and trim controls;
- physical state gauges.

The hierarchy and pivots are intended to survive replacement of the greybox visuals
with authored or generated meshes. Gameplay code addresses modules, pivots and mount
footprints rather than individual renderers.

## Deferred

P13 does not yet implement:

- water reservoirs, extraction or circulation;
- photovoltaic leaves, batteries or electric motors;
- biomass harvester, dryer, storage, furnace or biofuel engine;
- water pipes and electrical cables;
- ropes between multiple chassis;
- resource costs for repair;
- additional caravan modules or living spaces.

Those systems should extend the existing module state, mount grid, environment
sampler and interaction ray instead of creating a second construction or UI layer.
