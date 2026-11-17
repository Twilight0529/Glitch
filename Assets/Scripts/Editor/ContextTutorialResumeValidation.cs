using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class ContextTutorialResumeValidation
{
    [MenuItem("GLITCH/Validation/Tutorial continuation after defeat")]
    public static void Run() => Debug.Log($"Tutorial continuation: {Validate()} checks passed.");

    public static int Validate()
    {
        int checks = 0;
        var completed = new HashSet<string>();
        var handled = new HashSet<string>();
        Check(Offer("movement", true, completed, handled), "first movement opens", ref checks);
        Check(completed.Count == 0, "opening never persists completion", ref checks);
        completed.Add("movement"); // The player actually completed this panel, then died.
        handled.Clear(); // A new run resets only temporary bookkeeping.
        Check(!Offer("movement", true, completed, handled), "completed movement does not repeat", ref checks);
        Check(handled.Contains("movement"), "loaded completion populates the per-run cache", ref checks);
        Check(Offer("interface", true, completed, handled), "next unseen panel remains eligible after defeat", ref checks);
        Check(!Offer("interface", true, completed, handled), "same run does not duplicate an opened panel", ref checks);
        handled.Clear(); // Interrupted before completing the interface tutorial.
        Check(Offer("interface", true, completed, handled), "interrupted panel is offered next run", ref checks);
        Check(Offer("parry", true, completed, handled) && Offer("ghost_dash", true, completed, handled), "later mechanic panels are independent", ref checks);
        Check(Offer("arena_event_v2_rupture_spin", true, completed, handled), "unseen map event remains eligible", ref checks);
        Check(!Offer("breach", false, completed, handled) && !completed.Contains("breach"), "disabled tutorial is not completed", ref checks);
        handled.Clear();
        Check(Offer("breach", true, completed, handled), "disabled panel can appear in a later enabled run", ref checks);
        Check(completed.Count == 1 && completed.Contains("movement"), "no unseen progress was written or old completion erased", ref checks);
        return checks;
    }

    private static bool Offer(string key, bool enabled, HashSet<string> completed, HashSet<string> handled)
    {
        return ContextTutorialRunGate.TryOffer(enabled, completed.Contains(key) || handled.Contains(key), () => handled.Add(key));
    }
    private static void Check(bool value, string label, ref int count)
    {
        if (!value) throw new InvalidOperationException("Tutorial continuation: " + label);
        count++;
    }
}
