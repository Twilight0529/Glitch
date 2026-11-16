using System;

public struct DefeatBodyLayout
{
    public const float BodyTop = 66f;
    public const float ContentHeight = 268f;
    public readonly float actionsTop;
    public readonly float viewportHeight;
    public readonly float contentWidth;
    public readonly bool needsScroll;

    public DefeatBodyLayout(float areaWidth, float areaHeight)
    {
        actionsTop = areaHeight - 48f;
        viewportHeight = Math.Max(1f, actionsTop - BodyTop - 16f);
        needsScroll = viewportHeight < ContentHeight;
        contentWidth = Math.Max(1f, areaWidth - (needsScroll ? 20f : 0f));
    }
}
