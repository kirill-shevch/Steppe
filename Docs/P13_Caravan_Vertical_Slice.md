# P13 — Caravan keeper vertical slice

P13 replaces the development rolling sphere with the first playable modular caravan.
The current configuration is deliberately minimal: an enlarged wheeled deck, a
physical steering wheel and the three components of a player-wired electrical
circuit: photovoltaic leaves, a battery and an electric motor. All other equipment
is absent from the starting caravan.

## Playable loop

The player exists as a first-person keeper on a moving deck:

1. set electric power with the motor throttle;
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
| `E` | enter or leave the targeted physical control station |
| `A` / `D` | adjust the active physical control |
| hold `C` | clean the targeted module |
| hold `R` | repair the targeted module |
| `B` | enter or leave build mode while nearly stopped |
| `Tab` in build mode | switch between module placement and communications |
| left click in module mode | pick up or place a movable module |
| `R` in build mode | rotate the held module by 90 degrees |
| right click in module mode | return the held module to its previous mount |
| two left clicks in communications mode | connect the selected compatible ports |
| right click in communications mode | cancel selection or remove cables from the targeted port |

The mouse pointer remains a centre-screen world ray. No inventory, status window
or persistent gameplay HUD is introduced.

The front station has a steering wheel and the electric motor has a physical
throttle lever. A small lamp appears when the keeper
aims at a station and grows while `E` has it engaged. Mouse movement never changes
a control.

## Diegetic state

Every demo module exposes three physical bars:

- orange: accumulated dust;
- green: remaining integrity;
- blue: current mechanical load.

Dust and integrity reduce efficiency gradually instead of switching a module off.
The chassis reads P12 surface resistance for tyre friction, dust and wear. The three
installed electrical modules expose generation, charge and motor load physically.

## Chassis and propulsion

The chassis uses the Vehicle Physics Pro Community Edition controller, one Rigidbody,
four `VPWheelCollider` suspension units and a compact body collider. It becomes dynamic
only after the near terrain streamer exposes a physics surface. The caravan root
replaces the old sphere as the canonical focus for terrain, grass, weather, ecology,
tracks and floating-origin shifts.

Every `CaravanModule` contributes its own mass and local mass centre. The enlarged
chassis contributes 2600 kg before equipment. Installing, removing or moving parts
recalculates the Rigidbody mass and combined centre of mass.

The VPP engine has no default throttle and starts in neutral. The physical steering
wheel sends its `A`/`D` value to VPP steering, while soil resistance changes the
tyre-friction multiplier. Electric throttle is limited by the power actually
delivered to the motor, so an empty battery or an unplugged motor produces no
powered traction.

## Electrical circuit

The photovoltaic leaves, battery and electric motor expose physical electrical
ports. Two cable pairs form a shared DC chain: panel to battery and battery to motor.
The cables are visual and logical only; they have no colliders or rigid bodies and
follow modules when the keeper moves them on the mount grid.

Cables are not created automatically. The keeper enters build mode with `B`, switches
to communications with `Tab`, then clicks the source and destination modules. Port
markers are blue when available, yellow when selected, green for a compatible aimed
target and red when the connection is invalid or the port is full. A temporary line
previews the route before the second click.

The panel supplies the active motor load first. Surplus generation charges the
120 kWh battery up to its charge-power limit; a generation deficit discharges the
battery up to its output-power limit. Charge and discharge efficiency, spilled
generation and unmet demand are all preserved in the network state. The battery
window shows state of charge, and the motor shaft spins in proportion to delivered
mechanical power.

The keeper is a CharacterController independent of the Rigidbody. While grounded
on a caravan collider, carrier translation and rotation are applied before player
movement. A jump inherits the platform's planar point velocity.

## Build mode

The deck uses a `10 × 18` one-metre mount grid and one-item construction buffer.
Each of the three installed electrical parts can be picked up, previewed as a
transparent ghost, rotated and placed in another free footprint.

The same build mode owns communication editing. A generator or consumer accepts one
cable, while the battery storage port accepts two. Only generator-to-storage and
storage-to-consumer links are valid. Right-clicking an unselected connected module
removes all cables attached to its electrical port.

The pure occupancy model does not depend on Unity physics and is covered by EditMode
tests. Future parts can reuse the same `CaravanModule` footprint contract.

## Procedural greybox

`CaravanDemoFactory` creates the demo from a small runtime construction kit:

- tiled deck and frame beams;
- four VPP-driven wheels with procedural tyre visuals;
- physical steering wheel and electric throttle;
- photovoltaic leaves, battery and electric motor;
- paired electrical cables and visible connection terminals;
- physical state gauges.

The hierarchy and pivots are intended to survive replacement of the greybox visuals
with authored or generated meshes. Gameplay code addresses modules, pivots and mount
footprints rather than individual renderers.

## Deferred

P13 does not yet implement:

- water extraction, circulation or heat transfer;
- biomass harvesting, drying or combustion;
- sail and wind propulsion in the starting loadout;
- water pipes;
- physical ropes between multiple chassis;
- resource costs for repair;
- additional caravan modules or living spaces.

Those systems should extend the existing module state, mount grid, environment
sampler and interaction ray instead of creating a second construction or UI layer.
