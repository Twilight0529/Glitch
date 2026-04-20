# Arena Setup (2D Top-Down)

## One-click scene generation

1. Open `Assets/Scenes/Level1.unity`.
2. In Unity menu, run `Glitch > Generate > Setup Current Level Scene`.
3. Press Play.

This setup creates automatically:
- `GameManager`
- `Player` (WASD, 4-direction movement)
- `Anomaly` (3 behavior patterns, switching every 5s)
- `ProceduralArenaGenerator` (closed walls + procedural static obstacles)

## Procedural behavior

- Every run generates a new obstacle layout.
- The arena remains closed (containment preserved).
- Obstacles avoid immediate spawn overlap around player/anomaly.

## Useful tuning

On `__GeneratedArena` object (`ProceduralArenaGenerator`):
- `minObstacles` / `maxObstacles`
- `obstacleSizeMin` / `obstacleSizeMax`
- `spawnSafeRadius`
- `randomizeSeedEachRun`
- `fixedSeed` (disable randomize to reproduce one map)

## Optional manual regeneration

- In Inspector on `ProceduralArenaGenerator`, use context menu `Generate Arena Now`.

## Design Alignment

- Constant pressure: anomaly never stops chasing.
- Instability: anomaly behavior changes every 5 seconds and terrain changes each run.
- Containment: walls always close the arena, no escape route.
