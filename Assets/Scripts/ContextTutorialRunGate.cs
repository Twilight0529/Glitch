using System;

public static class ContextTutorialRunGate
{
    // Remember handled offers locally even when completion was loaded from disk.
    // This never writes completion: only the confirmed tutorial close may do that.
    public static bool TryOffer(bool enabled, bool alreadyHandled, Action rememberForThisRun)
    {
        rememberForThisRun();
        return enabled && !alreadyHandled;
    }
}
