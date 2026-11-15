public sealed class GameplayInputGate
{
    private int blockedFrame = -1;

    public void BlockFrame(int frame) => blockedFrame = frame;

    public bool Allows(float timeScale, bool transitioning, int frame)
    {
        return timeScale > 0f && !float.IsInfinity(timeScale) && !transitioning && frame != blockedFrame;
    }
}
