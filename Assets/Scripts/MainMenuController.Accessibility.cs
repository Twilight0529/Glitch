using UnityEngine;

public partial class MainMenuController
{
    private bool showAccessibility;
    private Vector2 accessibilityScroll;

    private void DrawAccessibilityMenu()
    {
        Rect panel = DrawHubShell("ACCESIBILIDAD", GlitchUiPalette.Information, 490f);
        Rect viewport = new Rect(panel.x + 26f, panel.y + 112f, panel.width - 52f, Mathf.Max(60f, panel.height - 178f));
        accessibilityScroll = GUI.BeginScrollView(viewport, accessibilityScroll, new Rect(0f, 0f, viewport.width - 22f, 260f));
        Rect content = new Rect(0f, 0f, viewport.width - 22f, 260f);
        float camera = UserSettings.GetCameraImpact();
        float flash = UserSettings.GetFlashIntensity();
        float motion = UserSettings.GetMenuMotion();
        float newCamera = DrawOptionSlider(content, 12f, "Impactos de cámara", camera, 0f, 1f, true);
        float newFlash = DrawOptionSlider(content, 80f, "Destellos de pantalla", flash, 0f, 1f, true);
        float newMotion = DrawOptionSlider(content, 148f, "Movimiento decorativo", motion, UserSettings.MinMenuMotion, UserSettings.MaxMenuMotion);
        if (!Mathf.Approximately(camera, newCamera)) UserSettings.SetCameraImpact(newCamera);
        if (!Mathf.Approximately(flash, newFlash)) UserSettings.SetFlashIntensity(newFlash);
        if (!Mathf.Approximately(motion, newMotion))
        {
            UserSettings.SetMenuMotion(newMotion);
            menuMotionIntensity = newMotion;
        }
        GUI.Label(new Rect(14f, 214f, content.width - 28f, 46f), "Los avisos y las formas de peligro permanecen visibles.",
            new GUIStyle(paragraphStyle) { wordWrap = true, fontSize = 16 });
        GUI.EndScrollView();
        DrawHubBackButton(panel, () => showAccessibility = false);
    }
}
