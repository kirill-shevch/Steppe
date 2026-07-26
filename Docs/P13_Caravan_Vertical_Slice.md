# P13 — Caravan keeper vertical slice

P13 replaces the development rolling sphere with the first playable modular caravan.
The current configuration has an enlarged wheeled deck, a physical steering wheel,
a wind-driven sail and greybox versions of every planned technical part. Water,
electricity and biomass networks remain deferred.

## Playable loop

The player exists as a first-person keeper on a moving deck:

1. trim the sail for the current wind;
2. enter the physical steering station and choose a course;
3. walk around the moving deck;
4. read part state from physical gauges;
5. clean, repair or reposition individual parts.

There is no autonomous pilot. The wheel retains the last angle set by the player,
but the caravan does not choose a course as the ground changes.

## Controls

| Input | Action |
|---|---|
| `WASD` | walk |
| `Shift` | run |
| `Space` | jump |
| mouse | look |
| `E` | enter or leave the targeted steering or sail-trim station |
| `A` / `D` | adjust the active physical control |
| hold `C` | clean the targeted module |
| hold `R` | repair the targeted module |
| `B` | enter or leave build mode while nearly stopped |
| left click in build mode | pick up or place a movable module |
| `R` in build mode | rotate the held module by 90 degrees |
| right click in build mode | return the held module to its previous mount |

The mouse pointer remains a centre-screen world ray. No inventory, status window
or persistent gameplay HUD is introduced.

The front station has a steering wheel; the sail carries its own trim winch. A small
lamp appears when the keeper aims at either station and grows while `E` has it
engaged. Mouse movement never changes a control.

## Diegetic state

Every demo module exposes three physical bars:

- orange: accumulated dust;
- green: remaining integrity;
- blue: current mechanical load.

Dust and integrity reduce efficiency gradually instead of switching a module off.
The chassis reads P12 surface resistance for tyre friction, dust and wear. The sail
reads the authoritative surface wind and accumulates load, dust and damage.

## Chassis and propulsion

The chassis uses the Vehicle Physics Pro Community Edition controller, one Rigidbody,
four `VPWheelCollider` suspension units and a compact body collider. It becomes dynamic
only after the near terrain streamer exposes a physics surface. The caravan root
replaces the old sphere as the canonical focus for terrain, grass, weather, ecology,
tracks and floating-origin shifts.

Every `CaravanModule` contributes its own mass and local mass centre. The enlarged
chassis contributes 2600 kg before equipment. Installing, removing or moving parts
recalculates the Rigidbody mass and combined centre of mass.

The VPP engine has no default throttle and starts in neutral. The chassis free-rolls
until the sail applies wind force. The physical steering wheel sends its `A`/`D`
value to VPP steering, while soil resistance changes the tyre-friction multiplier.

The keeper is a CharacterController independent of the Rigidbody. While grounded
on a caravan collider, carrier translation and rotation are applied before player
movement. A jump inherits the platform's planar point velocity.

## Build mode

The deck uses a `10 × 18` one-metre mount grid and one-item construction buffer.
Every technical part, including the sail, can be picked up, previewed as a transparent
ghost, rotated and placed in another free footprint.

The pure occupancy model does not depend on Unity physics and is covered by EditMode
tests. Future parts can reuse the same `CaravanModule` footprint contract.

## Procedural greybox

`CaravanDemoFactory` creates the demo from a small runtime construction kit:

- tiled deck and frame beams;
- four VPP-driven wheels with procedural tyre visuals;
- steering wheel and sail-trim winch;
- deforming mast-and-cloth sail;
- photovoltaic leaves, battery, reservoir, pump and radiator;
- biofurnace, biofuel engine, electric motor and transmission;
- harvester, grass dryer, biomass storage and coupling rope;
- physical state gauges.

The hierarchy and pivots are intended to survive replacement of the greybox visuals
with authored or generated meshes. Gameplay code addresses modules, pivots and mount
footprints rather than individual renderers.

## Deferred

P13 does not yet implement:

- water extraction, circulation or heat transfer;
- electricity production, storage or consumption;
- biomass harvesting, drying or combustion;
- water pipes and electrical cables;
- physical ropes between multiple chassis;
- resource costs for repair;
- additional caravan modules or living spaces.

Those systems should extend the existing module state, mount grid, environment
sampler and interaction ray instead of creating a second construction or UI layer.
