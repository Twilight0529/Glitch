// Emits once on unavailable -> ready, never on the initial sample.
public sealed class ReadinessTransition
{
    private bool known;
    private bool wasReady;

    public bool Observe(bool ready)
    {
        bool becameReady = known && !wasReady && ready;
        known = true;
        wasReady = ready;
        return becameReady;
    }

    public void Reset()
    {
        known = false;
        wasReady = false;
    }
}
