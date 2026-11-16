using System;

public struct DefeatBodyLayout
{
    public const float BodyTop = 66f;
    public readonly float contentHeight;
    public readonly int metricColumns;
    public readonly float actionsTop;
    public readonly float viewportHeight;
    public readonly float contentWidth;
    public readonly bool needsScroll;

    public DefeatBodyLayout(float areaWidth, float areaHeight)
    {
        actionsTop = areaHeight - 48f;
        viewportHeight = Math.Max(1f, actionsTop - BodyTop - 16f);
        metricColumns = areaWidth < 420f ? 2 : 4;
        contentHeight = metricColumns == 2 ? 342f : 268f;
        needsScroll = viewportHeight < contentHeight;
        contentWidth = Math.Max(1f, areaWidth - (needsScroll ? 20f : 0f));
    }
}
