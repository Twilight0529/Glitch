public static class GameplayTickPolicy
{
    // Use timeScale, not just deltaTime: pausing can happen earlier in this frame.
    public static bool CanAdvance(float timeScale, bool transitioning)
    {
        return timeScale > 0f && !float.IsInfinity(timeScale) && !transitioning;
    }
}
