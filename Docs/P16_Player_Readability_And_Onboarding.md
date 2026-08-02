# P16 — Player readability and onboarding

P16 begins the transition from a simulation prototype to a readable caravan game.
It does not add another technical system. Instead, it translates existing state into
player-facing causes and gives the opening minutes a concrete sequence.

## Opening sequence

The starter caravan still contains disconnected photovoltaic leaves, a partially
charged battery and an electric motor. A short objective chain now asks the keeper
to:

1. connect the photovoltaic leaves to the battery;
2. connect the battery to the motor;
3. engage the physical motor throttle;
4. take the steering station and travel 120 metres.

Progress is derived from the authoritative network, throttle and chassis speed. It
does not advance merely because the expected key was pressed.

## Contextual HUD

The gameplay HUD deliberately exposes only:

- the current objective and one actionable instruction;
- a small centre reticle;
- the action available at the aimed station or module;
- transient success or failure feedback;
- an inspector for the aimed module or active control station.

The F3 diagnostic panel remains available for developers and is not used for the
opening sequence.

## Operational vocabulary

Modules translate simulation state into one dominant player-facing state:

- ready;
- working;
- starved;
- blocked;
- full;
- dirty;
- damaged.

The inspector reports a short cause together with the most relevant input and
output. Examples include an unconnected motor, a battery charging or discharging,
photovoltaic leaves facing away from the sun, an unpowered pump, a full harvester
or a dryer without wet biomass.

Dust and integrity remain common maintenance signals. Severe dirt or damage
overrides the process state because it is the keeper's immediate actionable
problem.

## Control modes

The first-person keeper now has explicit free, station and build modes.

- Free mode owns walking, running and jumping.
- Station mode holds the keeper in place and gives `A` / `D` to the physical
  control.
- Build mode stops keeper movement but preserves mouse look for world-space
  placement and connection editing.

Build mode reports why it cannot start while the caravan is moving. Placement,
connection, removal and cancellation now produce explicit feedback.

## Movement feel

Keeper locomotion now includes:

- acceleration and deceleration instead of instantaneous planar speed;
- a short jump input buffer;
- a short coyote window after leaving a surface;
- restrained walking head motion;
- a small landing response proportional to impact speed.

Carrier motion and inherited caravan velocity remain authoritative and continue to
use the existing collision proxy.

## Verification

- Runtime and EditMode assemblies: 0 warnings, 0 errors.
- EditMode: 123 passed, 0 failed.
- PlayMode: 12 passed, 0 failed.

## Next vertical slice

The next milestone should begin after the starter drive is understood:

1. introduce a readable need for water;
2. expose a coarse local weather lead rather than an exact map marker;
3. let the keeper intercept a wet front and extract water;
4. lead from wet grass to harvesting;
5. lead from wet biomass to a warm, windy drying location;
6. finish at one named landmark so the journey has a remembered destination.

The fourteen-part simulation remains available, but new catalogue exposure should
follow player needs instead of presenting the complete technical tree at the start.
