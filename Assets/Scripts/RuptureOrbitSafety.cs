using System.Collections.Generic;
using UnityEngine;

// Reuses a conservative grid: every accepted swept formation retains one connected
// traversable area. Checking the whole sweep also protects passages between ticks.
public sealed class RuptureOrbitSafety
{
    private readonly ArenaReachabilityGrid grid;
    private readonly Rect outsideArena;

    public RuptureOrbitSafety(Rect arena, float clearance, float cellSize = 0.3f)
    {
        grid = new ArenaReachabilityGrid(arena, clearance, cellSize);
        outsideArena = new Rect(arena.xMin - 10f, arena.yMin - 10f, 0.01f, 0.01f);
    }

    public bool IsConnected(IReadOnlyList<Rect> sweptFootprints)
    {
        return grid.WouldRemainConnected(sweptFootprints, outsideArena);
    }

    // Corners use the box order (--), (+-), (-+), (++). Testing its two axes
    // as well as the player's axes avoids treating empty diamond corners as solids.
    public static bool IntersectsPlayerFootprint(Vector2[] corners, Rect player, float padding)
    {
        if (Separated(corners, player, Vector2.right, padding) ||
            Separated(corners, player, Vector2.up, padding)) return false;
        Vector2 edgeA = corners[1] - corners[0];
        Vector2 edgeB = corners[2] - corners[0];
        return !Separated(corners, player, new Vector2(-edgeA.y, edgeA.x), padding) &&
               !Separated(corners, player, new Vector2(-edgeB.y, edgeB.x), padding);
    }

    private static bool Separated(Vector2[] corners, Rect player, Vector2 axis, float padding)
    {
        if (axis.sqrMagnitude < 0.000001f) return false;
        axis.Normalize();
        float min = float.PositiveInfinity;
        float max = float.NegativeInfinity;
        foreach (Vector2 corner in corners)
        {
            float projection = Vector2.Dot(corner, axis);
            min = Mathf.Min(min, projection);
            max = Mathf.Max(max, projection);
        }
        float playerProjection = Vector2.Dot(player.center, axis);
        float playerRadius = Mathf.Abs(axis.x) * player.width * 0.5f +
            Mathf.Abs(axis.y) * player.height * 0.5f + padding;
        return max < playerProjection - playerRadius || min > playerProjection + playerRadius;
    }
}
