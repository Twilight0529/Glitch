// Unity defers Destroy until the end of the frame. Seal a pickup before
// delivering rewards so additional collider callbacks cannot collect it again.
public sealed class PickupClaimGate
{
    private bool closed;

    public bool TryClaim(bool eligible)
    {
        if (closed || !eligible) return false;
        closed = true;
        return true;
    }

    public void Expire() => closed = true;
}
