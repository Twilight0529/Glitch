using UnityEngine;

public class LocalVersusManager : MonoBehaviour
{
    // Administra el tiempo, la victoria y la interfaz del duelo local.
    [SerializeField] private float roundDuration = 120f;
    [SerializeField] private Color runnerColor = new Color(0.25f, 0.90f, 1f, 1f);
    [SerializeField] private Color anomalyColor = new Color(1f, 0.28f, 0.48f, 1f);

    private GameManager gameManager;
    private PlayerController runner;
    private EnemyController anomaly;
    private float timeRemaining;
    private GUIStyle headerStyle;
    private GUIStyle labelStyle;
    private GUIStyle smallStyle;
    private EnemyController.AnomalyState lastDisplayedState;
    private float stateBannerTimer;

    public bool IsRoundActive => gameManager != null && gameManager.IsRunActive && !gameManager.IsGameOver && Time.timeScale > 0f;

    public void Configure(GameManager manager, PlayerController player, EnemyController enemy)
    {
        gameManager = manager;
        runner = player;
        timeRemaining = Mathf.Max(20f, roundDuration);

        anomaly = enemy;
        anomaly.enabled = true;
        anomaly.EnableLocalVersusControl(runner);
        lastDisplayedState = anomaly.CurrentState;
        stateBannerTimer = 1.4f;
    }

    private void Update()
    {
        stateBannerTimer = Mathf.Max(0f, stateBannerTimer - Time.unscaledDeltaTime);
        if (gameManager == null || !gameManager.IsRunActive || gameManager.IsGameOver)
        {
            return;
        }

        timeRemaining = Mathf.Max(0f, timeRemaining - Time.deltaTime);
        if (anomaly != null && anomaly.CurrentState != lastDisplayedState)
        {
            lastDisplayedState = anomaly.CurrentState;
            stateBannerTimer = 1.4f;
        }
        gameManager.SetVersusElapsedTime(roundDuration - timeRemaining);
        if (timeRemaining <= 0f)
        {
            FinishRound("CORREDOR", "La contencion resistio hasta agotar el protocolo.");
        }
    }

    private void FinishRound(string winner, string result)
    {
        gameManager?.TriggerVersusGameOver(winner, result);
    }

    private void OnGUI()
    {
        if (gameManager == null || gameManager.IsGameOver || GameMenuController.ShouldHideGameplayHud ||
            SceneTransitionController.IsFading)
        {
            return;
        }

        EnsureStyles();
        if (!gameManager.IsRunActive)
        {
            DrawControlBriefing();
            return;
        }

        float scale = Mathf.Clamp(Mathf.Min(Screen.width / 1280f, Screen.height / 720f), 0.72f, 1.2f);
        float panelWidth = Mathf.Min(540f * scale, Screen.width - 28f);
        Rect panel = new Rect((Screen.width - panelWidth) * 0.5f, 12f * scale, panelWidth, 43f * scale);
        DrawRect(panel, new Color(0.015f, 0.025f, 0.055f, 0.90f));
        DrawRect(new Rect(panel.x, panel.y, panel.width, 2f), Color.Lerp(runnerColor, anomalyColor, 0.5f));

        int seconds = Mathf.CeilToInt(timeRemaining);
        GUI.Label(new Rect(panel.x + 12f, panel.y + 6f, panel.width - 24f, 28f * scale),
            $"DUELO LOCAL  |  {seconds / 60:00}:{seconds % 60:00}", headerStyle);

        DrawPlayerStatusPanels(scale, panel.yMax + 8f * scale);
        DrawStateChangeBanner(scale);
    }

    private void DrawStateChangeBanner(float scale)
    {
        if (anomaly == null || stateBannerTimer <= 0f)
        {
            return;
        }

        float alpha = Mathf.Clamp01(stateBannerTimer / 0.35f);
        float width = Mathf.Min(440f * scale, Screen.width - 36f);
        Rect banner = new Rect((Screen.width - width) * 0.5f, Screen.height * 0.70f, width, 62f * scale);
        DrawRect(banner, new Color(0.035f, 0.015f, 0.055f, 0.82f * alpha));
        DrawRect(new Rect(banner.x, banner.y, banner.width, 2f), new Color(1f, 0.36f, 0.76f, alpha));
        GUI.Label(new Rect(banner.x + 12f, banner.y + 5f, banner.width - 24f, 27f * scale),
            anomaly.CurrentStateLabel, headerStyle);
        GUI.Label(new Rect(banner.x + 12f, banner.y + 31f * scale, banner.width - 24f, 22f * scale),
            GetStateHint(anomaly.CurrentState), smallStyle);
    }

    private void DrawPlayerStatusPanels(float scale, float top)
    {
        float gap = 12f * scale;
        float width = Mathf.Min(350f * scale, (Screen.width - gap * 3f) * 0.5f);
        float height = 148f * scale;
        Rect runnerPanel = new Rect(gap, top, width, height);
        Rect anomalyPanel = new Rect(Screen.width - gap - width, top, width, height);

        DrawRect(runnerPanel, new Color(0.015f, 0.035f, 0.065f, 0.88f));
        DrawRect(anomalyPanel, new Color(0.055f, 0.018f, 0.055f, 0.88f));
        DrawRect(new Rect(runnerPanel.x, runnerPanel.y, runnerPanel.width, 2f), runnerColor);
        DrawRect(new Rect(anomalyPanel.x, anomalyPanel.y, anomalyPanel.width, 2f), anomalyColor);

        GUI.Label(new Rect(runnerPanel.x + 10f, runnerPanel.y + 5f, runnerPanel.width - 20f, 23f * scale),
            "J1 | CORREDOR", headerStyle);
        GUI.Label(new Rect(anomalyPanel.x + 10f, anomalyPanel.y + 5f, anomalyPanel.width - 20f, 23f * scale),
            anomaly != null ? $"J2 | {anomaly.CurrentStateLabel}" : "J2 | ANOMALIA", headerStyle);

        if (runner != null)
        {
            DrawLabeledBar(
                new Rect(runnerPanel.x + 12f, runnerPanel.y + 35f * scale, runnerPanel.width - 24f, 15f * scale),
                "FIREWALL",
                runner.FirewallChargeNormalized,
                runner.IsFirewallBurstReady ? Color.white : runnerColor);
            DrawLabeledBar(
                new Rect(runnerPanel.x + 12f, runnerPanel.y + 57f * scale, runnerPanel.width - 24f, 15f * scale),
                runner.IsParryReady ? "PARRY LISTO" : "PARRY",
                runner.ParryCooldownNormalized,
                new Color(0.50f, 1f, 0.84f, 1f));
            DrawLabeledBar(
                new Rect(runnerPanel.x + 12f, runnerPanel.y + 79f * scale, runnerPanel.width - 24f, 15f * scale),
                runner.IsGhostDashReady ? "DASH LISTO" : "DASH",
                runner.GhostDashCooldownNormalized,
                new Color(0.72f, 0.72f, 1f, 1f));
            string powerup = runner.ActivePowerupLabel == "None" ? "Sin powerup activo" : runner.ActivePowerupLabel;
            GUI.Label(new Rect(runnerPanel.x + 12f, runnerPanel.y + 99f * scale, runnerPanel.width - 24f, 19f * scale),
                powerup, smallStyle);
        }

        if (anomaly != null)
        {
            GUI.Label(new Rect(anomalyPanel.x + 12f, anomalyPanel.y + 29f * scale, anomalyPanel.width - 24f, 22f * scale),
                GetStateHint(anomaly.CurrentState), smallStyle);
            DrawLabeledBar(
                new Rect(anomalyPanel.x + 12f, anomalyPanel.y + 58f * scale, anomalyPanel.width - 24f, 16f * scale),
                "DURACION DEL ESTADO",
                1f - anomaly.LocalVersusStateTimeRemaining / 20f,
                anomalyColor);
            string stateChange = anomaly.CanLocalVersusChangeState
                ? $"CTRL DER: CAMBIAR | AUTO {Mathf.CeilToInt(anomaly.LocalVersusStateTimeRemaining)}s"
                : $"CAMBIO EN {Mathf.CeilToInt(anomaly.LocalVersusManualChangeTimeRemaining)}s | AUTO {Mathf.CeilToInt(anomaly.LocalVersusStateTimeRemaining)}s";
            GUI.Label(new Rect(anomalyPanel.x + 12f, anomalyPanel.y + 84f * scale, anomalyPanel.width - 24f, 19f * scale),
                stateChange, smallStyle);
            GUI.Label(new Rect(anomalyPanel.x + 12f, anomalyPanel.y + 107f * scale, anomalyPanel.width - 24f, 28f * scale),
                "Las habilidades y telegraphs son los del modo normal.", smallStyle);
        }
    }

    private void DrawLabeledBar(Rect rect, string label, float fill, Color color)
    {
        DrawRect(rect, new Color(0.04f, 0.06f, 0.10f, 0.96f));
        DrawRect(new Rect(rect.x, rect.y, rect.width * Mathf.Clamp01(fill), rect.height), new Color(color.r, color.g, color.b, 0.72f));
        GUI.Label(rect, label, smallStyle);
    }

    private void DrawControlBriefing()
    {
        float scale = Mathf.Clamp(Mathf.Min(Screen.width / 1280f, Screen.height / 720f), 0.72f, 1.15f);
        float width = Mathf.Min(720f * scale, Screen.width - 36f);
        Rect panel = new Rect((Screen.width - width) * 0.5f, Screen.height - 150f * scale, width, 122f * scale);
        DrawRect(panel, new Color(0.015f, 0.025f, 0.055f, 0.92f));
        DrawRect(new Rect(panel.x, panel.y, panel.width * 0.5f, 3f), runnerColor);
        DrawRect(new Rect(panel.center.x, panel.y, panel.width * 0.5f, 3f), anomalyColor);

        GUI.Label(new Rect(panel.x + 12f, panel.y + 12f, panel.width * 0.5f - 24f, 26f), "J1 | CORREDOR", headerStyle);
        GUI.Label(new Rect(panel.center.x + 12f, panel.y + 12f, panel.width * 0.5f - 24f, 26f), "J2 | ANOMALIA", headerStyle);
        GUI.Label(new Rect(panel.x + 12f, panel.y + 44f, panel.width * 0.5f - 24f, 54f),
            "WASD mover | ESPACIO parry\nQ firewall | SHIFT dash", labelStyle);
        GUI.Label(new Rect(panel.center.x + 12f, panel.y + 44f, panel.width * 0.5f - 24f, 54f),
            "FLECHAS mover\nCTRL DER cambiar estado cuando se habilite", labelStyle);
    }

    private static string GetStateHint(EnemyController.AnomalyState state)
    {
        switch (state)
        {
            case EnemyController.AnomalyState.SpeedSurge:
                return "Velocidad extrema y menor control al girar.";
            case EnemyController.AnomalyState.Destroyer:
                return "Rompe obstaculos al atravesarlos.";
            case EnemyController.AnomalyState.ExpansionShoot:
                return "Dispara expansiones con telegraph automatico.";
            case EnemyController.AnomalyState.PhaseBlink:
                return "Ejecuta saltos de fase hacia el corredor.";
            case EnemyController.AnomalyState.PincerBarrage:
                return "Genera fuego cruzado desde ambos laterales.";
            case EnemyController.AnomalyState.SignalJam:
                return "Interfiere el espacio alrededor del corredor.";
            case EnemyController.AnomalyState.OrbitBarrage:
                return "Despliega el bombardeo orbital del modo normal.";
            default:
                return "Persecucion directa controlada por J2.";
        }
    }

    private void EnsureStyles()
    {
        if (headerStyle != null)
        {
            return;
        }

        headerStyle = new GUIStyle(GUI.skin.label)
        {
            font = GlobalFontSettings.GetImportantFont(),
            fontSize = 19,
            alignment = TextAnchor.MiddleCenter
        };
        headerStyle.normal.textColor = Color.white;

        labelStyle = new GUIStyle(GUI.skin.label)
        {
            font = GlobalFontSettings.GetSecondaryFont(),
            fontSize = 11,
            alignment = TextAnchor.MiddleCenter
        };
        labelStyle.normal.textColor = new Color(0.78f, 0.86f, 0.96f, 1f);

        smallStyle = new GUIStyle(labelStyle)
        {
            fontSize = 10,
            fontStyle = FontStyle.Bold
        };
        smallStyle.normal.textColor = Color.white;
    }

    private static void DrawRect(Rect rect, Color color)
    {
        Color previous = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = previous;
    }
}
