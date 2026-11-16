using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class MechanicsReferenceValidation
{
    [MenuItem("GLITCH/Validation/Mechanics reference")]
    public static void Run() => Debug.Log($"Mechanics reference: {Validate()} checks passed.");
    public static int Validate()
    {
        int checks = 0;
        var entries = MechanicsReferenceCatalog.Entries;
        var examples = new HashSet<MechanicsReferenceCatalog.Example>();
        foreach (var entry in entries)
        {
            Check(!string.IsNullOrWhiteSpace(entry.title) && !string.IsNullOrWhiteSpace(entry.control) &&
                !string.IsNullOrWhiteSpace(entry.tutorialKey) && entry.description.Length > 20 && entry.description.Length < 240,
                "complete bounded entry", ref checks);
            examples.Add(entry.example);
        }
        Check(entries.Count == 8 && examples.Count == 8, "eight distinct examples", ref checks);
        Check(entries[2].control.Contains("Clic izquierdo"), "mouse parry listed", ref checks);
        Check(entries[3].control.Contains("Clic derecho"), "mouse firewall listed", ref checks);
        Check(entries[5].description.Contains("durante el dash") && entries[5].description.Contains("caminás"), "phase dash is not permanent walking intangibility", ref checks);
        Check(entries[7].description.Contains("no caduca"), "stored hijack has no invented expiry", ref checks);
        return checks;
    }
    private static void Check(bool value, string label, ref int count)
    {
        if (!value) throw new InvalidOperationException("Mechanics reference: " + label);
        count++;
    }
}
