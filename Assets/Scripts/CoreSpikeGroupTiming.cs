using System;

public enum CoreSpikePhase { Dormant, Warning, Active, Finished }

// Two warned waves share the existing event budget; warning the second wave costs active time.
public static class CoreSpikeGroupTiming
{
    public static CoreSpikePhase GetPhase(float elapsed, int group, int groupCount,
        float warningSeconds, float activeSeconds)
    {
        float initialWarning = Math.Max(0.2f, warningSeconds);
        float activeBudget = Math.Max(1f, activeSeconds);
        if (groupCount <= 1)
        {
            if (elapsed < 0f) return CoreSpikePhase.Dormant;
            if (elapsed < initialWarning) return CoreSpikePhase.Warning;
            return elapsed < initialWarning + activeBudget ? CoreSpikePhase.Active : CoreSpikePhase.Finished;
        }

        float secondWarning = Math.Min(initialWarning, activeBudget * 0.4f);
        float waveActive = (activeBudget - secondWarning) * 0.5f;
        float start = group <= 0 ? 0f : initialWarning + waveActive;
        float warning = group <= 0 ? initialWarning : secondWarning;
        if (elapsed < start) return CoreSpikePhase.Dormant;
        if (elapsed < start + warning) return CoreSpikePhase.Warning;
        return elapsed < start + warning + waveActive ? CoreSpikePhase.Active : CoreSpikePhase.Finished;
    }
}
