# Final visual-readability audit

## Player-facing rule

The shipped simulation has no ecology or weather HUD. Every authoritative parameter used
for traversal or prediction must change an observable property of the world. The F3 panel
is retained only as development ground truth: validate with it, hide it, then confirm the
same state can be inferred from the scene.

## Parameter-to-signal matrix

| Authoritative parameter | Intended natural signal | Current strength |
|---|---|---|
| biome weights / climate precipitation | continuous cover, stature, soil and grass palette | strong |
| climate temperature | seasonal phenology, precipitation phase, frost, snow, melt and moving horizon mirage | strong |
| vegetation potential | grass density/height and exposed-ground fraction | strong |
| fertility | maximum biomass and recovery speed | medium, visible over time |
| clay content | soil hue, retained wetness, mud resistance/ball sink and dry crack structure | strong at ground level |
| water retention | duration of root moisture and greenness after rain | medium, temporal |
| exposed ground / dust potential | available dust source area and emission strength | strong after P9 tuning |
| wind direction | grass lean/waves, dust streams, rain/snow inclination | strong |
| wind speed | bend amplitude, wave speed, dust threshold and precipitation angle | strong |
| gust intensity | travelling grass band and short dust pulse before a front | strong |
| cloud wind | cloud advection independent of surface motion | strong |
| cloud coverage / water | light loss, cloud density/dark base | strong |
| precipitation intensity | rain/snow density, immediate soil or snow input | strong |
| surface water | dark smooth soil, wet highlight, zero dust | strong |
| root water | delayed greenness and biomass recovery | medium, intentionally delayed |
| dry crust | pale cracked surface, firm rolling and raised dust threshold | strong |
| biomass | stable grass subset, height and far-cover strength | strong |
| green fraction | living green versus cured gold tissue | strong |
| snow water | white cover, loaded grass, no dust | strong |
| snow compaction | slower melt, reduced grass load, firm rolling and a dark blue smooth crust | strong |
| frozen fraction | frost tint, stiff grass, slow infiltration | medium |
| traversal resistance | ball acceleration, maximum speed and rolling decay | strong and tactile |
| mud / loose soil / snow sink | visible ball depth and distinct track form | strong at player scale |
| recent passage | laid grass, compact snow, depressed soil and wet print | strong |

## Required final procedure

1. Add debug-only overrides that can sweep each parameter through low, middle and high
   values while holding the others fixed. Overrides must never enter save data or gameplay.
2. Record the same camera path for every sweep with F3 visible as ground truth.
3. Repeat with F3 hidden. A reviewer must identify direction, relative strength and recent
   history without seeing a number.
4. Test confusable pairs: wet clay versus dry dark soil, hard crust versus loose dust,
   root moisture versus active rain, snow versus frost, calm desert versus wet desert.
5. Test at ground level, middle distance and horizon; at noon, dusk and night; in every
   biome; before and after a floating-origin shift.
6. Any gameplay-relevant parameter that cannot be identified receives a stronger visual,
   motion or audio signal before the HUD is removed from development builds.

## Remaining presentation polish (not simulation blockers)

- audio driven by the same wind, rain, dust and frozen-surface state;
- more authored material detail for mud clods, granular sand and fractured clay;
- screen-space refraction can later strengthen the current horizon mirage.
