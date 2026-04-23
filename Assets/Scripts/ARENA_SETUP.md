# Arena Setup (2D Top-Down)

## One-click scene generation

1. Open `Assets/Scenes/Level1.unity`.
2. In Unity menu, run `Glitch > Generate > Setup Current Level Scene`.
3. Press Play.

This setup creates automatically:
- `GameManager` HUD with `Level Type` (Lab / Storage / Rupture)
- `Player` (WASD, 4-direction movement)
- `Anomaly` (adaptive pathfinding + behavior switching every 5s)
- `ProceduralArenaGenerator` (rule-based themes, static + dynamic obstacles)

## Rule-based themes

Each run can select a theme, each with its own generation logic:
- `Lab`: controlled lanes, long barriers, structured containment geometry
- `Storage`: clustered sectors, mixed footprint obstacles, denser local chokepoints
- `Rupture`: angled fractures, irregular silhouettes, unstable spacing patterns

Global rules always enforced:
- Closed arena, no exits
- Protected movement lanes (avoid visual chaos)
- Spawn safety around player/anomaly
- Obstacle spacing and overlap checks

## Dynamic obstacles

Dynamic obstacles are added with rules (not random noise):
- Sliding blockers (kinematic movement)
- Pulsing blockers (expansion/contraction)

They are placed respecting lane and collision constraints.

## Useful tuning

On `__GeneratedArena` object (`ProceduralArenaGenerator`):
- `arenaWidth` / `arenaHeight`
- `primaryLaneWidth` / `secondaryLaneWidth`
- `minObstacles` / `maxObstacles`
- `minDynamicObstacles` / `maxDynamicObstacles`
- `randomizeThemeEachRun` / `fixedTheme`
- `randomizeSeedEachRun` / `fixedSeed`

## Optional manual regeneration

- In Inspector on `ProceduralArenaGenerator`, use context menu `Generate Arena Now`.

## Design Alignment

- Constant pressure: anomaly never stops chasing.
- Instability: enemy behavior changes every 5s and map theme/layout varies.
- Containment: walls are always sealed and flow lanes remain intentional.
