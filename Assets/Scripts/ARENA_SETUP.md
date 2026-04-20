# Arena Setup (2D Top-Down)

1. Open `Assets/Scenes/Level1.unity`.
2. Create an empty `GameManager` object and attach `GameManager.cs`.
3. Create Player:
   - GameObject name: `Player`
   - Components: `SpriteRenderer`, `Rigidbody2D` (Dynamic), `CircleCollider2D`, `PlayerController.cs`
4. Create Enemy:
   - GameObject name: `Anomaly`
   - Components: `SpriteRenderer`, `Rigidbody2D` (Dynamic), `CircleCollider2D`, `EnemyController.cs`
   - In `EnemyController`, drag references for `Player` and `GameManager`.
5. Build containment arena:
   - Create 4 wall objects (top, bottom, left, right)
   - Add `BoxCollider2D` to each wall
   - Keep walls static (no Rigidbody2D needed)
6. Add static obstacles:
   - Create obstacle objects inside arena
   - Add `BoxCollider2D` or `PolygonCollider2D`
   - Keep obstacles static
7. Optional physics sanity:
   - On Player and Enemy Rigidbody2D: Collision Detection = Continuous
8. Play and test:
   - Move with WASD
   - Enemy should switch behavior every 5 seconds
   - Contact with enemy triggers game over

## Design Alignment

- Constant pressure: enemy velocity is always driven toward a target.
- Instability: behavior pattern rotates every 5 seconds.
- Containment: closed colliders and static obstacles prevent escape routes.
