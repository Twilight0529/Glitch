# Arena Setup (2D Top-Down)

## One-click scene generation

1. Open `Assets/Scenes/Level1.unity`.
2. In Unity menu, run `Glitch > Generate > Setup Current Level Scene`.
3. Press Play.

This generator creates automatically:
- `GameManager` with survival timer/game over/difficulty ramp
- `Player` (WASD, 4-direction movement)
- `Anomaly` (3 behavior patterns, switching every 5s)
- Arena walls (closed containment)
- Static blocking obstacles
- Enemy references wired to `PlayerController` + `GameManager`

## Manual fallback (if needed)

1. Create an empty `GameManager` object and attach `GameManager.cs`.
2. Create `Player` with: `SpriteRenderer`, `Rigidbody2D` (Dynamic), `CircleCollider2D`, `PlayerController.cs`.
3. Create `Anomaly` with: `SpriteRenderer`, `Rigidbody2D` (Dynamic), `CircleCollider2D`, `EnemyController.cs`.
4. In `EnemyController`, drag references for `Player` and `GameManager`.
5. Create 4 walls with `BoxCollider2D` to close the map.
6. Create static obstacles with `BoxCollider2D` or `PolygonCollider2D`.

## Design Alignment

- Constant pressure: enemy velocity is always driven toward a target.
- Instability: behavior pattern rotates every 5 seconds.
- Containment: closed colliders and static obstacles prevent escape routes.
