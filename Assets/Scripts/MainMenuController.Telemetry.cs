using UnityEngine;

public partial class MainMenuController
{
    private bool showTelemetry;
    private Vector2 telemetryScroll;

    private void DrawTelemetryMenu()
    {
        Rect panel = DrawHubShell("DATOS DE JUEGO", GlitchUiPalette.Information, 490f);
        Rect viewport = new Rect(panel.x + 26f, panel.y + 110f, panel.width - 52f, Mathf.Max(50f, panel.height - 180f));
        float width = viewport.width - 22f;
        var text = new GUIStyle(paragraphStyle) { wordWrap = true, fontSize = 17 };
        string intro = "Opcional: compartir resultados, duración y causas de muerte para mejorar GLITCH.";
        string privacy = "Sin nombre del ranking ni cuenta de Google. Usa un identificador aleatorio para reconocer sesiones del mismo juego.";
        string footer = "Al desactivarlo se borra la cola pendiente, no los datos ya enviados.";
        bool allowed = UserSettings.GetShareGameplayTelemetry();
        string status = !GoogleSheetsTelemetryService.IsConfigured ? "Conexión del desarrollador sin configurar."
            : !allowed ? "Envío desactivado." : GoogleSheetsTelemetryService.Status;
        float wrap = Mathf.Max(40f, width - 20f);
        float introHeight = text.CalcHeight(new GUIContent(intro), wrap);
        float toggleY = introHeight + 18f;
        float privacyY = toggleY + 58f;
        float privacyHeight = text.CalcHeight(new GUIContent(privacy), wrap);
        float statusY = privacyY + privacyHeight + 20f;
        float statusHeight = text.CalcHeight(new GUIContent(status), wrap);
        float footerY = statusY + statusHeight + 20f;
        float footerHeight = text.CalcHeight(new GUIContent(footer), wrap);
        Rect content = new Rect(0, 0, width, footerY + footerHeight + 16f);
        telemetryScroll = GUI.BeginScrollView(viewport, telemetryScroll, content);
        GUI.Label(new Rect(10, 4, wrap, introHeight), intro, text);
        bool next = DrawOptionToggle(content, toggleY, "Compartir partidas", allowed);
        if (next != allowed) UserSettings.SetShareGameplayTelemetry(next);
        GUI.Label(new Rect(10, privacyY, wrap, privacyHeight), privacy, text);
        GUI.Label(new Rect(10, statusY, wrap, statusHeight), status, text);
        GUI.Label(new Rect(10, footerY, wrap, footerHeight), footer, text);
        GUI.EndScrollView();
        DrawHubBackButton(panel, () => showTelemetry = false);
    }
}
