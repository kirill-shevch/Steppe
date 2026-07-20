# P11 — Snow and freezing

P11 treats the weather model's precipitation intensity as water flux and selects its phase
from current canonical air temperature. Rain and snow presentations therefore cannot run
at full strength simultaneously.

Cold precipitation enters `SnowWater`. Vegetation captures snow while exposed windy cells
are scoured. Existing snow compacts slowly, sublimates, insulates the soil and completely
suppresses dust. Warm daylight melts it into surface water, after which normal infiltration
feeds the root layer. Frozen soil admits less water and makes grass visibly stiffer.

A second world-anchored RGBA texture keeps the cryosphere contract separate from P8:

| Channel | Meaning |
|---|---|
| R | snow water |
| G | snow compaction |
| B | frozen soil fraction |
| A | reserved |

Terrain blends to snow with sun sparkle and a weak frost tint. Grass catches snow at the
tips, shortens under load and bends less while frozen. One camera-local snowfall volume
shows wind direction through drifting flakes.

Gate:

- sub-zero precipitation produces snowfall and stored snow, not wet soil;
- snow persists after the cloud leaves and suppresses dust;
- wind visibly inclines flakes and reduces accumulation on exposed cells;
- thaw reduces snow, wets the ground and later increases root water;
- snow/frost remain continuous across chunk and floating-origin shifts.
