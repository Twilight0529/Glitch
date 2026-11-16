using System;
using UnityEditor;
using UnityEngine;

public static class RunPerformanceSummaryValidation
{
    [MenuItem("GLITCH/Validation/Run performance summary")]
    public static void Run() => Debug.Log($"Run summary: {Validate()} checks passed.");
    public static int Validate()
    {
        Check(default(RunPerformanceSummary).Highlight == "Supervivencia", "empty run invents no achievement");
        Check(new RunPerformanceSummary(8, 2, 3, 4, 5).Highlight == "2 brechas superadas", "breach highlight priority");
        Check(new RunPerformanceSummary(8, 0, 1, 4, 5).Highlight == "Un contrato completado", "actual contract highlighted");
        Check(new RunPerformanceSummary(8, 0, 0, 2, 5).Highlight == "2 mejoras elegidas", "chosen upgrades highlighted");
        Check(new RunPerformanceSummary(1, 0, 0, 0, 5).Highlight == "Un Parry exitoso", "parry singular");
        Check(new RunPerformanceSummary(6, 0, 0, 0, 5).parries == 6, "actual parry counter retained");
        Check(new RunPerformanceSummary(0, 0, 0, 0, 3).Highlight == "3 datos recuperados", "pickup fallback");
        var invalid = new RunPerformanceSummary(-1, -1, -1, -1, -1);
        Check(invalid.parries == 0 && invalid.Highlight == "Supervivencia", "negative counters normalized");
        Check(new RunPerformanceSummary(int.MaxValue, 0, 0, 0, 0).Highlight.Contains(int.MaxValue.ToString()), "large count without arithmetic overflow");
        Check(!new RunPerformanceSummary(1, 1, 1, 1, 1).Highlight.Contains("\n"), "one concise highlight");
        return 10;
    }
    private static void Check(bool value, string label)
    {
        if (!value) throw new InvalidOperationException("Run summary: " + label);
    }
}
