# Arena Setup (2D Top-Down)

## One-click scene generation

1. Open `Assets/Scenes/Level1.unity`.
2. In Unity menu, run `Glitch > Generate > Setup Current Level Scene`.
3. Press Play.

This setup creates automatically:
- `GameManager`
- `Player` (WASD, 4-direction movement)
- `Anomaly` (adaptive pathfinding, switching behavior every 5s)
- `ProceduralArenaGenerator` (closed walls + procedural obstacles + thematic details)

## Rule-based procedural themes

Each run can select one arena theme with explicit layout rules:
- `ContainmentLab`: orthogonal blocks with protected crossing lanes.
- `StorageBay`: clustered structures by quadrant with mixed shapes.
- `RuptureZone`: diagonal fractures and irregular formations.

Global rules always enforced:
- Closed arena and no escape.
- Reserved circulation lanes (avoid visual clutter chaos).
- Spawn safety around player and anomaly.
- Obstacle separation gap to keep readable movement paths.

## Useful tuning

On `__GeneratedArena` object (`ProceduralArenaGenerator`):
- `arenaWidth` / `arenaHeight`
- `primaryLaneWidth` / `secondaryLaneWidth`
- `minObstacles` / `maxObstacles`
- `randomizeThemeEachRun` / `fixedTheme`
- `randomizeSeedEachRun` / `fixedSeed`

## Optional manual regeneration

- In Inspector on `ProceduralArenaGenerator`, use context menu `Generate Arena Now`.

## Design Alignment

- Constant pressure: anomaly never stops chasing.
- Instability: anomaly behavior changes every 5 seconds and map style varies by theme.
- Containment: walls always close the arena and core circulation remains intentionally structured.
