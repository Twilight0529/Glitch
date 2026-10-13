using System;
using System.Collections.Generic;
using UnityEngine;

// Conservative navigation snapshot: each free cell fits the player's clearance,
// and cardinal neighbours share a traversable edge (no diagonal corner cutting).
public sealed class ArenaReachabilityGrid
{
    private readonly Rect bounds;
    private readonly int columns;
    private readonly int rows;
    private readonly float cellWidth;
    private readonly float cellHeight;
    private readonly float clearance;
    private readonly bool[] blocked;
    private readonly bool[] reached;
    private readonly int[] queue;
    private int freeCount;

    public ArenaReachabilityGrid(Rect arenaBounds, float playerClearance, float cellSize = 0.3f)
    {
        clearance = Mathf.Max(0f, playerClearance);
        bounds = new Rect(arenaBounds.xMin + clearance, arenaBounds.yMin + clearance,
            Mathf.Max(0.01f, arenaBounds.width - clearance * 2f),
            Mathf.Max(0.01f, arenaBounds.height - clearance * 2f));
        columns = Mathf.Max(1, Mathf.CeilToInt(bounds.width / Mathf.Max(0.1f, cellSize)));
        rows = Mathf.Max(1, Mathf.CeilToInt(bounds.height / Mathf.Max(0.1f, cellSize)));
        cellWidth = bounds.width / columns;
        cellHeight = bounds.height / rows;
        blocked = new bool[columns * rows];
        reached = new bool[blocked.Length];
        queue = new int[blocked.Length];
    }

    public void Build(IReadOnlyList<Rect> obstacles)
    {
        Array.Clear(blocked, 0, blocked.Length);
        Array.Clear(reached, 0, reached.Length);
        freeCount = blocked.Length;
        for (int i = 0; i < obstacles.Count; i++) Block(obstacles[i]);
    }

    // Called before materializing an obstacle, so rejected placements create no GameObjects.
    public bool WouldRemainConnected(IReadOnlyList<Rect> obstacles, Rect candidate, Rect? second = null)
    {
        Build(obstacles);
        Block(candidate);
        if (second.HasValue) Block(second.Value);
        int start = Array.FindIndex(blocked, cell => !cell);
        return start >= 0 && Flood(start) == freeCount;
    }

    public bool FloodFrom(Vector2 origin)
    {
        Array.Clear(reached, 0, reached.Length);
        int start = GetCell(origin);
        return start >= 0 && !blocked[start] && Flood(start) > 0;
    }

    public bool CanReach(Vector2 point)
    {
        int cell = GetCell(point);
        return cell >= 0 && reached[cell];
    }

    private int GetCell(Vector2 point)
    {
        if (!bounds.Contains(point)) return -1;
        int x = Mathf.Min(columns - 1, Mathf.FloorToInt((point.x - bounds.xMin) / cellWidth));
        int y = Mathf.Min(rows - 1, Mathf.FloorToInt((point.y - bounds.yMin) / cellHeight));
        return y * columns + x;
    }

    private void Block(Rect obstacle)
    {
        Rect expanded = new Rect(obstacle.xMin - clearance, obstacle.yMin - clearance,
            obstacle.width + clearance * 2f, obstacle.height + clearance * 2f);
        if (!bounds.Overlaps(expanded)) return;
        int minX = Mathf.Clamp(Mathf.FloorToInt((expanded.xMin - bounds.xMin) / cellWidth), 0, columns - 1);
        int maxX = Mathf.Clamp(Mathf.FloorToInt((expanded.xMax - bounds.xMin) / cellWidth), 0, columns - 1);
        int minY = Mathf.Clamp(Mathf.FloorToInt((expanded.yMin - bounds.yMin) / cellHeight), 0, rows - 1);
        int maxY = Mathf.Clamp(Mathf.FloorToInt((expanded.yMax - bounds.yMin) / cellHeight), 0, rows - 1);
        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                int index = y * columns + x;
                if (blocked[index]) continue;
                blocked[index] = true;
                freeCount--;
            }
        }
    }

    private int Flood(int start)
    {
        int head = 0;
        int tail = 0;
        reached[start] = true;
        queue[tail++] = start;
        while (head < tail)
        {
            int cell = queue[head++];
            int x = cell % columns;
            int y = cell / columns;
            if (x > 0) Enqueue(cell - 1, ref tail);
            if (x + 1 < columns) Enqueue(cell + 1, ref tail);
            if (y > 0) Enqueue(cell - columns, ref tail);
            if (y + 1 < rows) Enqueue(cell + columns, ref tail);
        }
        return tail;
    }

    private void Enqueue(int cell, ref int tail)
    {
        if (blocked[cell] || reached[cell]) return;
        reached[cell] = true;
        queue[tail++] = cell;
    }
}
