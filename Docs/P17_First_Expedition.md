# P17 — First expedition

> **Historical implementation note:** P18 defines a new progression opening that
> must precede and eventually reframe this expedition.

P17 turns the opening tutorial into a short journey driven by the existing
simulation. The player is not sent to fixed resource pickups. Instead, the caravan
must be reconfigured to exploit three temporary opportunities in the steppe:
water, grass and drying weather.

## Route

After the starter drive, the keeper is asked to:

1. install a reservoir, pump and radiator;
2. close the fluid loop and connect the pump to the battery distribution port;
3. set the pump to extraction and find rain-soaked or water-retaining ground;
4. collect 30 litres of water;
5. install a harvester, dryer and biomass storage, then connect the material
   chain;
6. power the harvester, set its working control and find a productive grassland;
7. harvest 4 kilograms of wet biomass;
8. find warm, dry wind and produce 2 kilograms of dry biomass;
9. return to the named landmark, **Wind Tower — First Ridge**.

Every transition is checked against authoritative module, network, resource,
weather, ecology and position state. Pressing the expected control without
creating the required physical state does not advance the expedition.

## Opportunity navigation

The HUD periodically samples broad environmental fields around the caravan. It
shows a coarse direction sector and distance band for:

- wet fronts and terrain likely to retain water;
- productive grassland;
- warm, dry and windy terrain suitable for drying.

This is intentionally not an exact waypoint. The lead gets the player into the
right region; local rain, soil moisture, vegetation and wind still determine
whether the machinery can work.

## Landmark

The first ridge tower is generated as a visible world object with a wind vane,
turning rotor and pulsing amber beacon. Its name appears in the return objective,
giving the expedition a remembered place instead of only a completion counter.

## Completion criteria

- Water gained after reaching the extraction stage: 30 L.
- Wet biomass harvested after reaching grass: 4 kg.
- Dry biomass stored after reaching drying weather: 2 kg.
- Caravan distance from the first ridge tower: at most 55 m.

## Verification

- EditMode: 124 passed, 0 failed.
- PlayMode: 12 passed, 0 failed.

## Next tuning pass

The next pass should be performed in the running prototype and focus on travel
rhythm: visibility of the tower, scan distance, objective wording, resource
thresholds and how often the weather creates a useful choice between nearby
opportunities.
