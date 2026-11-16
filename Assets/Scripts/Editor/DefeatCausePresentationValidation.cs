using System;
using UnityEditor;
using UnityEngine;

public static class DefeatCausePresentationValidation
{
    [MenuItem("GLITCH/Validation/Defeat cause labels")]
    public static void Run() => Debug.Log($"Defeat cause: {Validate()} checks passed.");
    public static int Validate()
    {
        string[] sources = { "anomaly_contact", "split_clone_contact", "anomaly_projectile", "replay_predator_ghost", "core_spikes", "breach_sweep" };
        foreach (string source in sources)
        {
            string label = DefeatCausePresentation.Label(source);
            if (string.IsNullOrWhiteSpace(label) || label.Contains("\n")) throw new InvalidOperationException("Missing single-line cause: " + source);
        }
        Check(DefeatCausePresentation.Label("unknown") == "", "unknown does not invent a killer");
        Check(DefeatCausePresentation.Label(null) == "", "missing cause keeps default title");
        Check(DefeatCausePresentation.Label("restart") == "", "abandonment is not a death cause");
        Check(DefeatCausePresentation.Label("core_spikes") != DefeatCausePresentation.Label("anomaly_contact"), "map danger distinguished from contact");
        return 10;
    }
    private static void Check(bool value, string label)
    {
        if (!value) throw new InvalidOperationException("Defeat cause: " + label);
    }
}
