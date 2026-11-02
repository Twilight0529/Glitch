using System;

[Serializable]
public sealed class RunEndContext
{
    public string source = "unknown";
    public string arena = "unknown";
    public string enemyState = "unknown";
    public float playerX;
    public float playerY;
    public float gameplaySeconds;
    [NonSerialized] private bool captured;

    // Keep the first accepted cause, before the defeat cinematic changes the world.
    public bool TryCapture(string cause, string arenaLabel, string state, float x, float y, float seconds)
    {
        if (captured) return false;
        captured = true;
        source = Clean(cause);
        arena = Clean(arenaLabel);
        enemyState = Clean(state);
        playerX = Finite(x);
        playerY = Finite(y);
        gameplaySeconds = Math.Max(0f, Finite(seconds));
        return true;
    }

    private static float Finite(float value) => float.IsNaN(value) || float.IsInfinity(value) ? 0f : value;
    private static string Clean(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "unknown";
        value = value.Trim();
        return value.Length <= 64 ? value : value.Substring(0, 64);
    }
}
