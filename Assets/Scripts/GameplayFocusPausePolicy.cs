// Only an advancing run may be paused by an operating-system focus change.
// Returning to the foreground intentionally does not resume the game.
public static class GameplayFocusPausePolicy
{
    public static bool ShouldPause(bool foreground, bool playing, bool runActive,
        bool gameOver, bool transitioning, bool advancing)
    {
        return !foreground && playing && runActive && !gameOver && !transitioning && advancing;
    }
}
