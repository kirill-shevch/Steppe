# P18 — Caravan progression, recipes and salvage

## Status and precedence

This document defines the target game progression that turns the existing caravan
systems into an exploration and construction loop. It supersedes the starting
loadout, deck size and full-catalogue availability described in P13–P17. Those
documents remain a record of the implemented simulation prototype.

This is a game-design specification, not an implementation plan. Exact costs,
resource yields, timings, storage limits and world-generation density remain tuning
parameters unless stated otherwise.

## Player fantasy and design pillars

The player is the keeper of a tiny wind-driven caravan crossing a landscape full of
lost infrastructure. Progress does not come from experience points or a character
level. It comes from two things found in the world:

- **knowledge** — recipes recovered from themed ruined structures;
- **materials** — construction resources salvaged from ruined caravans.

The caravan is both the player's vehicle and their long-term progression artifact.
Every newly discovered recipe creates a new possibility, while every platform tile
and constructed module makes that possibility physically visible on the caravan.

The intended rhythm is:

`travel → discover → stop → explore on foot → recover knowledge or materials → return → expand or build → travel farther`

## Canonical starting state

The player starts on a `4 × 5` platform: twenty one-metre construction cells. The
basic sail occupies the centered rear end of the deck, while the steering station,
brake and resource crate share the front row. The platform remains compact enough for the
first expansion to be useful and later growth to be easy to read at a glance.

The starting caravan contains only:

| Element | Starting role |
|---|---|
| Steering station | Sets the caravan's direction and retains its last setting. |
| Brake control | Slows the caravan and holds it still for a safe exit and construction. |
| Basic sail | The only starting source of propulsion; the keeper sets its trim manually. |
| Resource crate | Receives salvaged construction resources and supplies construction. |

The chassis, wheels and twenty platform cells are the base vehicle rather than
catalogue modules. There is no starting photovoltaic array, battery, electric motor
or other technical equipment.

The recipes for the steering station, brake, basic sail and resource crate are core
knowledge and are available from the start. This prevents loss of a starter part
from permanently blocking a run. All technical module recipes begin locked.

The opening world seed must provide usable wind and a reachable tutorial wreck. A
calm start must never leave the sail-only caravan unable to begin the game.

## Progression model

Progress has three connected axes.

### Knowledge progression

A recipe permanently adds an element to the construction catalogue. Recipes are
not consumable and do not have to be found again when a constructed module is lost.
There is no abstract research currency and no recipe purchase screen.

Ruined structures contain thematic recipe sets rather than random individual
blueprints. Searching a new type of structure therefore produces a meaningful new
technical capability and a predictable reason to explore visible landmarks.

### Material progression

Ruined caravans are the primary source of construction resources. The player must
approach them on foot and dismantle them. When dismantling completes, the full yield
is transferred directly to resource storage on the player's caravan. The player
does not pick up loose resource objects or carry stacks by hand.

Construction consumes resources from caravan storage. Knowing a recipe is necessary
but does not make construction free.

Newly constructed storage modules begin empty. Building an accumulator, water tank
or any later storage vessel creates capacity but never grants free charge, water,
fuel or another operational payload. Loading an existing save restores the payload
that was actually stored in that specific module.

Dismantling a player-built module returns its full construction cost to the resource
crate. This is a reconfiguration tool rather than a punitive sale: the player can
immediately rebuild the same module elsewhere without another salvage trip. Stored
operational payloads are not converted into construction resources by this action.

Operational resources remain distinct from construction resources. Water and wet or
dry biomass continue to flow through their technical networks; they do not become
generic construction currency merely because the caravan can store them elsewhere.

### Spatial progression

Platform expansion is always known and never requires a discovered recipe. The player
selects an outer edge and constructs one complete row or column, preserving a solid
rectangular deck. The price is the per-cell structural cost multiplied by the number
of cells in that line; payment and placement are atomic.

After a line is constructed, the four physical caravan wheels move to the new outer
corners. The deck therefore remains visibly and physically supported as its footprint
grows. Individual isolated cells cannot be purchased through build mode.

Expansion gives the player room for newly unlocked machinery but also adds dry mass
and changes the caravan's handling. A larger deck is useful capacity, not a free
stat upgrade. Removing tiles and structural-failure rules are outside this design
slice and must not be assumed by the first implementation.

## Recipe sources and catalogue growth

For the current technical catalogue, the world uses the following discovery map:

| Ruined structure | Recipes recovered | Capability introduced |
|---|---|---|
| Broken power station | Photovoltaic leaves, battery, electric motor | Generate, store and use electrical power for propulsion. |
| Abandoned water facility | Water reservoir, dual-mode pump, radiator | Extract, move and thermally manage water. |
| Ruined farm or biofuel works | Biomass harvester, grass dryer, dry-biomass storage, biofurnace, biofuel engine | Harvest biomass, process it and turn it into heat or propulsion. |
| Collapsed workshop or transport yard | Transmission, coupling rope | Route mechanical work and pull additional platforms. |

The basic sail belongs to the starting set. The new resource crate, steering station,
brake and platform expansion are core caravan elements rather than landmark rewards.

A searched site grants its entire recipe set and becomes depleted. Discovering
another site of a type whose recipes are already known does not duplicate recipes.
Such duplicate-site rewards are not defined in this slice; world generation should
avoid presenting a mandatory progression landmark with no new reward.

Future module families should follow the same rule: their recipes belong to a ruin
whose former purpose explains the technology found there. Recipe placement must be
legible from the landmark silhouette and environmental storytelling rather than
feeling like a random loot table.

Beyond the nearby tutorial wreck and the first power station, three salvage caravans
appear between each pair of recipe sites along the intended travel corridor. Their
target spacing remains one meaningful discovery per five to ten minutes at the
caravan's expected 4–8 m/s travel speed. Tall silhouettes,
fog and lateral offsets should make the next destination readable on the horizon
without revealing several later rewards at once.

An uncollected wreck carries a restrained warm emissive trace, while an unsearched
recipe landmark carries a restrained cool signal. Both remain readable by day and
night and switch off immediately after their one-time reward is collected. They are
navigation affordances rather than permanent landmark lighting.

## New game elements

### Resource crate

The resource crate is a physical module on the caravan and the player-facing access
point for construction inventory.

- Salvage is deposited into caravan storage when dismantling completes.
- Construction reads and spends resources directly from caravan storage.
- Aiming at or opening the crate shows every stored construction resource and amount.
- The first progression slice uses one starting crate and guarantees that a complete
  wreck yield fits in it.
- Multiple crates, hard capacity limits, overflow behaviour and cargo mass are later
  balance decisions. Until those rules are approved, salvage must not be silently
  destroyed because of capacity.

The automatic transfer is an explicit abstraction. It keeps the game focused on
finding, dismantling and rebuilding rather than repeated trips carrying individual
pieces between a wreck and the caravan.

### Ruined caravan

A ruined caravan is a persistent, one-use salvage target found in the open landscape.
Its silhouette must be distinguishable from a recipe-bearing ruined building.
Different wreck compositions may yield different material mixes, and the visible
parts of a wreck should broadly explain its yield.

A wreck has these states:

1. **untouched** — readable from a distance and available for dismantling;
2. **being dismantled** — shows progress and the expected resource yield;
3. **depleted** — its useful parts are removed and it cannot pay out again.

The first tutorial wreck must contain enough structural material for at least one
starting-width platform line so the player experiences the complete
material-to-expansion loop.

### Recipe-bearing ruined structure

A ruined structure is an explorable landmark with a searchable knowledge point such
as a control cabinet, archive, workbench or surviving plans. It grants recipes, not
the bulk construction payout associated with a caravan wreck.

Before searching, the interaction identifies the ruin's theme without listing every
reward. After searching, the game names each newly learned recipe, updates the build
catalogue and marks the structure as searched. Recipe progress and searched state
must persist across save and load.

### Platform line

A platform cell remains the unit used to calculate construction cost, mass and module
placement. The player purchases cells only as a complete edge line through the
dedicated **Expand platform** option. The preview shows the whole row or column, its
cell count, total resource cost and resulting wheel footprint before confirmation.

## Player actions and rules

Input bindings are intentionally expressed as named actions here. The dismantle
action needs a dedicated binding during implementation and must not conflict with
the existing repair and build-rotation inputs.

| Action | Context and result |
|---|---|
| Steer | At the steering station, adjust and retain the caravan's heading. |
| Trim sail | At the sail control, trade wind angle for propulsion. |
| Apply or release brake | At the brake control, slow the caravan and hold it for stopping, disembarking and building. |
| Leave and board caravan | Move between the mobile platform and the landscape as the keeper. |
| Inspect target | Show whether a ruin holds recipes, a wreck can be dismantled or a crate contains resources. |
| Search structure | At its knowledge point, complete a short interaction and permanently learn its recipe set. |
| Dismantle wreck | While on foot and in range, hold the dismantle action until progress completes. |
| Inspect storage | Read construction-resource amounts from the physical resource crate. |
| Open build mode | While the caravan is safely stopped, browse known recipes and platform expansion. |
| Expand platform | Preview and purchase a complete outer row or column without a recipe; pay once per included cell. |
| Construct module | Select a known recipe, preview placement and consume its listed resources on confirmation. |
| Reposition module | Move an already constructed movable module without repurchasing it. |
| Dismantle owned module | Remove a movable module and return its complete construction cost to the resource crate. |
| Connect systems | Use the existing electrical, fluid, biomass and mechanical layers after the relevant modules are built. |

Dismantling is interrupted if the player leaves interaction range. Resources are
awarded once, atomically, when the action completes; cancelling cannot duplicate or
partially lose the yield. The wreck's depleted state must be saved.

The build catalogue shows only learned recipes and communicates two states:

- **known but unaffordable** — recipe learned, with missing resources listed;
- **buildable** — recipe learned, resources available and a valid placement possible.

Unknown recipes do not appear as silhouettes, placeholder names or selectable entries.
Newly discovered recipes become visible in the catalogue immediately. Platform
expansion remains a separate core action and is never part of recipe selection.

## Construction resources

The resource taxonomy is a content and balance decision, but the system should
support multiple typed resources rather than one universal scrap counter. The
minimum provisional families are:

- structural material for platform cells and frames;
- mechanical parts for moving machinery;
- electrical parts for power modules;
- fabric or fibre for sails, ropes and flexible components.

Exact names, stack sizes, recipes, yields and whether a fifth specialist family is
needed will be decided before implementation data is authored. Wreck visuals and
ruin themes should make each material source plausible.

## First playable progression beat

The new opening replaces the P16 electrical-start tutorial and precedes any larger
expedition:

1. use the sail and steering station to approach a visible ruined caravan;
2. use the brake to stop at a safe distance;
3. leave the platform and dismantle the wreck;
4. return and inspect the resources now present in the crate;
5. enter build mode and add one complete platform line;
6. travel to a visible ruined power station;
7. search it and learn the electrical recipe set;
8. salvage enough additional wrecks to construct a first electrical chain;
9. place and connect photovoltaic leaves, a battery and an electric motor;
10. use the new propulsion to reach a more distant class of ruin.

This sequence teaches the complete long-term loop with concrete physical actions.
Objectives advance from authoritative state: a wreck is depleted, resources exist
in storage, a platform line is placed, recipes are known and modules are actually connected.
Pressing the expected input without producing the state does not count.

## Feedback and readability

World objects need a consistent visual language:

- large themed buildings promise **knowledge**;
- broken caravans in the field promise **materials**;
- the player's physical crate represents **owned resources**;
- the construction catalogue represents **known possibilities**;
- the growing platform represents **realized progress**.

Recipe discovery produces a short notification listing newly unlocked elements.
Salvage completion shows the transferred resource amounts and identifies the crate as
their destination. Failed construction explains whether the blocker is an unknown
recipe, missing resources, occupied cells or unsafe caravan movement.

No permanent general-purpose inventory HUD is required. Contextual prompts, discovery
notifications, the crate inspector and build catalogue are sufficient for this slice.

The steering-wheel inspector doubles as the caravan's navigation dashboard. Alongside
load, dust and integrity it shows speed, ambient temperature, persistent odometer,
canonical X/Z coordinates and altitude, plus day, year and season.

## Persistence contract

The following progression state survives save and load:

- learned recipe ids;
- searched recipe-site ids;
- depleted wreck ids;
- construction resources stored by the caravan;
- platform shape and occupied cells;
- constructed module instances, positions and rotations;
- existing communication-layer connections.
- total caravan distance travelled.

These are player achievements, not transient scene setup. Re-entering an area must
not restore a paid-out wreck or revoke a learned recipe.

## Design boundaries for the first slice

This milestone does not introduce traders, research points, manual hauling, random
recipe drops, character levels, platform-line recipes, structural collapse or
resource loss on death. Those systems require separate design decisions and are not
implied by this progression loop.

The next step after approval of this document is implementation planning: compare
these rules with the current P13–P17 systems, identify reusable parts, define data
and save contracts, and split the work into independently testable milestones.

## Prototype implementation status — 2026-08-02

The first playable vertical slice is implemented in the runtime prototype:

- the caravan starts on a `4 × 5` platform; steering, brake and crate occupy the
  front row, while the sail is centered on the rear edge;
- ruined caravans support hold-to-dismantle, one-time typed salvage and depleted visuals;
- uncollected wrecks and unsearched recipe sites use distinct soft emissive signals
  that turn off when their reward is collected;
- four themed ruin classes unlock their complete recipe families once;
- the construction catalogue gates modules by recipe and typed resource cost;
- the construction catalogue contains learned recipes only;
- newly constructed storage modules begin empty while saved contents restore normally;
- dismantling an owned module refunds its full recipe cost to the resource crate;
- platform expansion purchases complete edge lines, charges per cell and moves the
  physical VPP wheels to the new outer corners;
- after the first power station, three resource wrecks appear between each recipe
  ruin; individual discoveries remain roughly 2.4 km apart;
- the opening objective chain leads through wreck, storage, expansion, power-station
  discovery and construction of a connected electrical drive;
- recipe, resource, searched-site, depleted-wreck and storage-inspection state has a
  tested snapshot contract;
- `F5` writes a versioned JSON save and `F9` restores it; the save includes the
  caravan's world pose, connected platform shape, placed module IDs and rotations,
  generic module condition and storage, plus all four connection layers;
- automatic checkpoints use a separate slot after progression, construction or
  connection changes, plus a periodic 90-second checkpoint for operational state;
- automatic writes are debounced for 3 seconds, limited to one every 12 seconds and
  deferred while build mode is active so a held module cannot disappear from a save;
- pausing or quitting flushes a checkpoint whenever the caravan is in a stable state;
- `F9` tries manual saves, autosaves and their backups newest-first, skipping a
  malformed candidate and reporting which kind was restored;
- save replacement is atomic where supported and retains a `.bak` copy of each
  slot's previous file. The default paths are
  `Application.persistentDataPath/steppe-caravan-save.json` and
  `Application.persistentDataPath/steppe-caravan-autosave.json`.

The provisional costs, yields, landmark geometry and distances are greybox tuning data.
Multiple profiles, schema migration beyond version validation and specialized
persistence for partially processed wet biomass are later technical milestones.
