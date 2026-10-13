using System;

// Uses the run's gameplay clock, so pausing cannot consume arrival protection.
public sealed class SectorArrivalWindow
{
    private float until;

    public bool IsActive(float now) => now < until;

    public void Extend(float now, float duration)
    {
        until = Math.Max(until, now + Math.Max(0f, duration));
    }

    public void Clear() => until = 0f;
}
