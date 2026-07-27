# P15 — Caravan refactor and test coverage

## Scope

The caravan test contract covers all 14 catalogue parts, all communication
layers, their pure simulation models, construction registration and the
complete resource chain. This is behavioural coverage; no percentage is
claimed without Unity's optional line-coverage package.

## Module matrix

| Part | Unit/model coverage | Runtime integration coverage |
| --- | --- | --- |
| Sail | apparent wind, broadside/edge-on force, force cap | constructed with the full catalogue |
| Photovoltaic leaves | incidence, cloud shadow, low-sun projection | solar → battery electrical component |
| Battery | capacity, charge/discharge limits and efficiencies | dynamic storage registration and cable isolation |
| Water reservoir | capacity, payload mass and temperature | closed-loop cooling, freezing and external heating |
| Dual-mode pump | control dead zone, extraction/circulation/off, mixed power | electrical and mechanical drive in the water loop |
| Radiator | opening, damage efficiency, cooling limits | wind-assisted closed-loop cooling |
| Biofurnace | requested fuel, useful heat and damage derating | biomass fuel consumption and water-loop heat |
| Biofuel engine | coupling, throttle, fuel power and waste heat | storage → engine → transmission drive chain |
| Electric motor | throttle, electrical clamp and mechanical efficiency | solar/battery drive and disconnected consumer |
| Harvester | capacity, wet-mixture conservation and hybrid power | ecology extraction and wet-biomass transfer |
| Grass dryer | active/passive drying and overflow conservation | wet input → dry output → recovered water |
| Biomass storage | accept/supply capacity and matter conservation | dryer input, furnace/engine output and repair material |
| Transmission | gear multipliers and engagement | engine source → transmission → mechanical consumers |
| Coupling rope | slack, tension, exact break limit, repair and symmetric link | material link conductivity after rope failure |

## Communication matrix

- Electrical: exhaustive symmetric role matrix, port capacity, duplicate
  rejection, connected-component balancing, open-cable isolation and build-mode
  removal/reconnection.
- Fluids: exhaustive role matrix, exact loop topology, powered circulation,
  open pipe, frozen water, heat/cooling and build-mode removal/reconnection.
- Biomass: exhaustive wet/dry role matrix, direct routing, transfer activity,
  fuel consumers and build-mode removal/reconnection.
- Mechanical: exhaustive source/transmission/consumer/coupling matrix,
  multi-link paths, broken links and build-mode removal/reconnection.

## Current verified suite

- EditMode: 121 passed, 0 failed.
- PlayMode: 12 passed, 0 failed.
- Runtime, EditMode and PlayMode assemblies: 0 warnings, 0 errors.

The main Unity editor has asset auto-refresh disabled during implementation.
Refresh it with `Ctrl+R` before interactive testing.
