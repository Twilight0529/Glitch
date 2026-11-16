using UnityEngine;

public partial class MainMenuController
{
    private bool showMechanicsReference;
    private int mechanicsReferenceIndex;
    private Vector2 mechanicsReferenceScroll;
    private Vector2 archiveHubScroll;

    private void DrawMechanicsReference()
    {
        Rect panel = DrawHubShell("GUIA DE MECANICAS", GlitchUiPalette.Information, 560f);
        var entries = MechanicsReferenceCatalog.Entries;
        mechanicsReferenceIndex = Mathf.Clamp(mechanicsReferenceIndex, 0, entries.Count - 1);
        var entry = entries[mechanicsReferenceIndex];
        Rect viewport = new Rect(panel.x + 26f, panel.y + 110f, panel.width - 52f, Mathf.Max(40f, panel.height - 216f));
        float width = viewport.width - 22f;
        mechanicsReferenceScroll = GUI.BeginScrollView(viewport, mechanicsReferenceScroll, new Rect(0f, 0f, width, 300f));
        GUI.Label(new Rect(0f, 0f, width, 34f), entry.title, panelTitleStyle);
        GUIStyle text = new GUIStyle(paragraphStyle) { wordWrap = true, fontSize = 17, alignment = TextAnchor.UpperLeft };
        GUI.Label(new Rect(0f, 42f, width, 88f), entry.description, text);
        DrawReferenceExample(new Rect(0f, 136f, width, 116f), entry.example);
        text.normal.textColor = GlitchUiPalette.Information;
        GUI.Label(new Rect(0f, 262f, width, 38f), entry.control, text);
        GUI.EndScrollView();
        float navY = panel.yMax - 94f;
        if (DrawAnimatedMenuButton(new Rect(panel.x + 26f, navY, 110f, 34f), "Anterior")) ChangeReference(-1);
        if (DrawAnimatedMenuButton(new Rect(panel.xMax - 136f, navY, 110f, 34f), "Siguiente")) ChangeReference(1);
        string progress = $"{mechanicsReferenceIndex + 1}/{entries.Count}";
        if (UserSettings.HasSeenContextTutorial(entry.tutorialKey)) progress += " · Visto";
        GUI.Label(new Rect(panel.center.x - 70f, navY, 140f, 34f), progress,
            new GUIStyle(paragraphStyle) { alignment = TextAnchor.MiddleCenter });
        DrawHubBackButton(panel, () => showMechanicsReference = false);
    }

    private void ChangeReference(int direction)
    {
        int count = MechanicsReferenceCatalog.Entries.Count;
        mechanicsReferenceIndex = (mechanicsReferenceIndex + direction + count) % count;
        mechanicsReferenceScroll = Vector2.zero;
        GlitchAudioManager.PlayMenuConfirm();
    }

    private static void DrawReferenceExample(Rect rect, MechanicsReferenceCatalog.Example example)
    {
        DrawSolidRect(rect, GlitchUiPalette.Surface);
        GUI.BeginGroup(rect);
        float w = rect.width;
        float t = Mathf.Repeat(Time.unscaledTime * 0.55f * UserSettings.GetMenuMotion(), 1f);
        float x = Mathf.Lerp(w * 0.18f, w * 0.78f, t);
        Color info = GlitchUiPalette.Information;
        if (example == MechanicsReferenceCatalog.Example.Parry)
        {
            ReferenceSprite(new Rect(w * 0.20f, 47f, 22f, 22f), SquareSpriteProvider.Get(), info);
            float p = t < 0.5f ? 1f - t * 2f : (t - 0.5f) * 2f;
            ReferenceSprite(new Rect(Mathf.Lerp(w * 0.29f, w * 0.78f, p), 52f, 12f, 12f), CircleSpriteProvider.Get(),
                t < 0.5f ? GlitchUiPalette.Danger : GlitchUiPalette.Success);
            ReferenceSprite(new Rect(w * 0.20f - 9f, 38f, 40f, 40f), ContainmentRingSpriteProvider.Get(), GlitchUiPalette.Special);
        }
        else if (example == MechanicsReferenceCatalog.Example.Firewall)
        {
            ReferenceSprite(new Rect(w * 0.5f - 11f, 47f, 22f, 22f), SquareSpriteProvider.Get(), info);
            float size = Mathf.Lerp(24f, 106f, t);
            ReferenceSprite(new Rect(w * 0.5f - size / 2f, 58f - size / 2f, size, size), ContainmentRingSpriteProvider.Get(),
                GlitchUiPalette.WithAlpha(GlitchUiPalette.Alert, 1f - t));
        }
        else if (example == MechanicsReferenceCatalog.Example.Powerups)
        {
            Sprite[] icons = { ShieldSpriteProvider.Get(), LightningSpriteProvider.Get(), CompactSpriteProvider.Get(), PhaseDashSpriteProvider.Get() };
            Color[] colors = { GlitchUiPalette.Special, info, GlitchUiPalette.Success, info };
            for (int i = 0; i < icons.Length; i++)
                ReferenceSprite(new Rect(w * (i + 1) / 5f - 18f, 40f, 36f, 36f), icons[i], colors[i]);
        }
        else if (example == MechanicsReferenceCatalog.Example.Hijack)
        {
            ReferenceSprite(new Rect(w * 0.18f, 40f, 36f, 36f), CompactSlimeSpriteProvider.Get(), GlitchUiPalette.Danger);
            ReferenceSprite(new Rect(Mathf.Lerp(w * 0.32f, w * 0.65f, t), 45f, 26f, 26f),
                AnomalyStateGlyphSpriteProvider.Get(EnemyStatePresentationCatalog.Glyph.Orbit), GlitchUiPalette.Special);
            ReferenceSprite(new Rect(w * 0.76f, 47f, 22f, 22f), SquareSpriteProvider.Get(), info);
        }
        else
        {
            if (example == MechanicsReferenceCatalog.Example.PhaseDash)
                DrawSolidRect(new Rect(w * 0.47f, 18f, 16f, 80f), GlitchUiPalette.Neutral);
            if (example == MechanicsReferenceCatalog.Example.Breach)
            {
                DrawSolidRect(new Rect(w * (0.06f + t * 0.50f), 12f, 5f, 92f), GlitchUiPalette.Danger);
                ReferenceSprite(new Rect(w * 0.78f, 28f, 48f, 60f), ContainmentRingSpriteProvider.Get(), GlitchUiPalette.Special);
            }
            if (example == MechanicsReferenceCatalog.Example.Dash || example == MechanicsReferenceCatalog.Example.PhaseDash)
                for (int i = 1; i <= 3; i++)
                    ReferenceSprite(new Rect(x - i * 12f, 49f, 18f, 18f), SquareSpriteProvider.Get(), GlitchUiPalette.WithAlpha(info, 0.3f / i));
            Color body = info;
            if (example == MechanicsReferenceCatalog.Example.PhaseDash && Mathf.Abs(x - w * 0.47f) < 24f) body.a = 0.40f;
            ReferenceSprite(new Rect(x, 47f, 22f, 22f), SquareSpriteProvider.Get(), body);
        }
        GUI.EndGroup();
    }

    private static void ReferenceSprite(Rect rect, Sprite sprite, Color color)
    {
        Color old = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(rect, sprite.texture, ScaleMode.ScaleToFit);
        GUI.color = old;
    }
}
