using System;
using UnityEditor;
using UnityEngine;

public static class DefeatBodyLayoutValidation
{
    [MenuItem("GLITCH/Validation/Defeat body layout")]
    public static void Run() => Debug.Log($"Defeat layout: {Validate()} checks passed.");
    public static int Validate()
    {
        int checks = 0;
        foreach (float height in new[] { 474f, 396f, 276f, 216f, 156f })
        {
            var layout = new DefeatBodyLayout(256f, height);
            Check(DefeatBodyLayout.BodyTop + layout.viewportHeight + 16f <= layout.actionsTop + 0.01f, "body stays above actions", ref checks);
            Check(layout.actionsTop + 42f <= height, "buttons remain inside area", ref checks);
            Check(layout.viewportHeight > 0f && layout.contentWidth > 0f, "usable content", ref checks);
        }
        var normal = new DefeatBodyLayout(584f, 474f);
        Check(!normal.needsScroll && normal.contentWidth == 584f, "normal presentation preserved", ref checks);
        var shortWindow = new DefeatBodyLayout(256f, 276f);
        Check(shortWindow.needsScroll && shortWindow.contentWidth == 236f, "short window reserves scrollbar", ref checks);
        Check(normal.metricColumns == 4 && normal.contentHeight == 268f, "wide four-metric row", ref checks);
        Check(shortWindow.metricColumns == 2 && shortWindow.contentHeight == 342f, "narrow two-row metrics", ref checks);
        Check(204f + 64f <= normal.contentHeight, "wide metrics within scroll content", ref checks);
        Check(204f + 74f + 64f <= shortWindow.contentHeight, "last narrow row within scroll content", ref checks);
        return checks;
    }
    private static void Check(bool value, string label, ref int count)
    {
        if (!value) throw new InvalidOperationException("Defeat layout: " + label);
        count++;
    }
}
