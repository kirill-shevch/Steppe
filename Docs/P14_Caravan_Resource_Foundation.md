# P14 — Complete caravan technical modules

> **Historical implementation note:** P18 preserves these technical modules but
> supersedes their starting availability with recipe-driven progression.

P14 turns the fixed P13 electrical demonstration into a constructible technical
caravan. The starting loadout remains deliberately minimal: photovoltaic leaves,
one battery and one electric motor, initially disconnected.

## Constructible modules

The build catalogue contains all fourteen planned technical modules:

- sail;
- photovoltaic leaves;
- battery;
- water reservoir;
- dual-mode pump;
- radiator;
- biofurnace;
- biofuel engine;
- electric motor;
- biomass harvester;
- grass dryer;
- dry-biomass storage;
- transmission;
- coupling rope.

Every module has a stable catalogue id, footprint, dry mass, capacity, procedural
greybox, status display and runtime instance id. Modules enter the live networks and
the chassis mass calculation only after successful placement.

## Manual communication layers

`Tab` cycles five build layers:

1. module placement;
2. electrical wiring;
3. fluid piping;
4. biomass routing;
5. mechanical drive routing.

Two left clicks connect compatible ports. Right click cancels a pending connection
or removes the targeted link. Cables, pipes and material or drive links are visual
and logical only: none has a collider or Rigidbody.

Electrical graphs support multiple generators, batteries and consumers. Power is
balanced independently inside each connected component. Solar generation serves
loads and charges storage; batteries cover deficits within their charge, discharge
and efficiency limits.

The fluid graph supports a closed reservoir–pump–radiator loop plus thermal taps for
the sail, photovoltaic leaves, furnace and biofuel engine. Flow depends on pump
power, water quantity and freezing. The radiator exchanges heat with ambient air
and benefits from wind. Furnace and engine waste heat enter the connected loop.

The biomass graph implements the complete material chain:

`harvester → wet biomass → dryer → dry storage → furnace / biofuel engine`

Harvesting subtracts biomass from the persistent ecology cell. Drying conserves dry
matter and recovered water. Stored dry biomass fuels combustion and is also consumed
by manual repair.

The mechanical graph connects electric or biofuel sources to a transmission and
then to the pump or harvester. A biofuel engine only drives the chassis after it is
coupled to a transmission. An unconnected transmission has no effect. Its manual
ratio trades road speed for tractive effort.

Coupling-rope endpoints can be linked across different rigidbody platforms. The rope
is slack below its configured length, applies equal and opposite tension above it,
damages itself at overload and stops transmitting force after breaking.

## Manual controls

The technical modules do not run an autonomous controller. Physical stations retain
the values set by the keeper:

- steering;
- electric drive throttle;
- sail trim;
- photovoltaic orientation;
- pump mode: extraction, off or circulation;
- radiator opening;
- furnace intensity;
- biofuel throttle;
- harvester power;
- dryer power;
- transmission ratio.

Dust, integrity and load affect module efficiency. Water and harvested material add
dynamic payload mass to the chassis.

## Current limitations

- Construction currently has no material purchase cost, dismantling or recycling.
- Placements, resource quantities and connection endpoints are not serialized yet.
- The demo starts with one chassis; the coupling force is ready for a second
  rigidbody platform, but that second platform is not spawned by the prototype.
- The fluid network currently models one primary reservoir, pump and radiator loop.
- Junction boxes and arbitrary electrical buses are not represented as separate
  buildable parts.
