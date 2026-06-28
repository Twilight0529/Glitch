using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// Adapta el modo normal al duelo local: congela la introducción, separa controles y presenta el resultado de ambos lados.
public class LocalVersusManager : MonoBehaviour
{
    // Administra el tiempo, la victoria y la presentacion bilateral del duelo local.
    private enum AlertKind
    {
        State,
        Event,
        Critical
    }

    [SerializeField] private float roundDuration = 120f;
    [SerializeField] private Color runnerColor = new Color(0.25f, 0.90f, 1f, 1f);
    [SerializeField] private Color anomalyColor = new Color(1f, 0.28f, 0.48f, 1f);
    [SerializeField] private Color eventColor = new Color(1f, 0.76f, 0.28f, 1f);
    [SerializeField, Range(0.10f, 0.24f)] private float sideViewportFraction = 0.17f;

    private GameManager gameManager;
    private PlayerController runner;
    private EnemyController anomaly;
    private float timeRemaining;
    private EnemyController.AnomalyState lastDisplayedState;
    private string lastEventKey = string.Empty;
    private bool thirtySecondWarningShown;
    private bool tenSecondWarningShown;
    private AlertKind alertKind;
    private string alertTitle = string.Empty;
    private string alertSubtitle = string.Empty;
    private Color alertColor;
    private float alertTimer;
    private const float AlertDuration = 2.4f;
    private bool runnerReady;
    private bool anomalyReady;
    private bool introductionComplete;
    private float introAge;
    private float introClosingTimer;
    private Camera gameplayCamera;
    private Rect originalCameraRect;
    private float originalOrthographicSize;
    private bool cameraLayoutApplied;
    private bool gameplayInputsReleased;
    private ProceduralArenaGenerator arenaGenerator;

    private GUIStyle titleStyle;
    private GUIStyle sectionStyle;
    private GUIStyle valueStyle;
    private GUIStyle compactStyle;
    private GUIStyle alertTitleStyle;
    private GUIStyle alertBodyStyle;

    public bool IsRoundActive =>
        gameManager != null && gameManager.IsRunActive && !gameManager.IsGameOver && Time.timeScale > 0f;
    public bool IntroductionComplete => introductionComplete;

    public void Configure(GameManager manager, PlayerController player, EnemyController enemy)
    {
        gameManager = manager;
        runner = player;
        anomaly = enemy;
        timeRemaining = Mathf.Max(20f, roundDuration);

        anomaly.enabled = true;
        anomaly.EnableLocalVersusControl(runner);
        lastDisplayedState = anomaly.CurrentState;
        Time.timeScale = 0f;
        PlayerController.SetTutorialInputLocked(true);
        ApplyReservedCameraLayout();
    }

    private void Update()
    {
        if (!introductionComplete)
        {
            Time.timeScale = 0f;
            PlayerController.SetTutorialInputLocked(true);
            UpdateIntroduction();
            return;
        }

        if (gameManager == null || !gameManager.IsRunActive)
        {
            Time.timeScale = 0f;
            PlayerController.SetTutorialInputLocked(true);
            return;
        }

        if (!gameplayInputsReleased)
        {
            gameplayInputsReleased = true;
            PlayerController.SetTutorialInputLocked(false);
        }

        alertTimer = Mathf.Max(0f, alertTimer - Time.unscaledDeltaTime);
        if (gameManager.IsGameOver)
        {
            return;
        }

        timeRemaining = Mathf.Max(0f, timeRemaining - Time.deltaTime);
        TrackStateAlerts();
        TrackEventAlerts();
        TrackTimeAlerts();

        gameManager.SetVersusElapsedTime(roundDuration - timeRemaining);
        if (timeRemaining <= 0f)
        {
            gameManager.TriggerVersusGameOver("CORREDOR", "La contencion resistio hasta agotar el protocolo.");
        }
    }

    private void OnDestroy()
    {
        Time.timeScale = 1f;
        PlayerController.SetTutorialInputLocked(false);
        RestoreCameraLayout();
    }

    private void UpdateIntroduction()
    {
        introAge += Time.unscaledDeltaTime;
        if (introClosingTimer > 0f)
        {
            introClosingTimer -= Time.unscaledDeltaTime;
            if (introClosingTimer <= 0f)
            {
                introductionComplete = true;
                ShowAlert(AlertKind.State, anomaly.CurrentStateLabel, GetStateHint(anomaly.CurrentState), anomalyColor);
            }
            return;
        }

        if (WasRunnerReadyPressed())
        {
            runnerReady = true;
        }
        if (WasAnomalyReadyPressed())
        {
            anomalyReady = true;
        }
        if (runnerReady && anomalyReady)
        {
            introClosingTimer = 0.72f;
        }
    }

    private void TrackStateAlerts()
    {
        if (anomaly == null || anomaly.CurrentState == lastDisplayedState)
        {
            return;
        }

        lastDisplayedState = anomaly.CurrentState;
        ShowAlert(AlertKind.State, anomaly.CurrentStateLabel, GetStateHint(anomaly.CurrentState), anomalyColor);
    }

    private void TrackEventAlerts()
    {
        string label = gameManager.CurrentMapEventLabel;
        string title = NormalizeEventKey(label);
        string key = NormalizeEventIdentity(title);
        if (string.IsNullOrWhiteSpace(key) || string.Equals(key, "Nominal", System.StringComparison.OrdinalIgnoreCase))
        {
            lastEventKey = string.Empty;
            return;
        }
        if (string.Equals(key, lastEventKey, System.StringComparison.Ordinal))
        {
            return;
        }

        lastEventKey = key;
        string hint = gameManager.CurrentMapEventHint;
        if (string.IsNullOrWhiteSpace(hint))
        {
            hint = "La arena acaba de cambiar sus reglas.";
        }
        ShowAlert(AlertKind.Event, title, hint, eventColor);
    }

    private void TrackTimeAlerts()
    {
        if (!thirtySecondWarningShown && timeRemaining <= 30f)
        {
            thirtySecondWarningShown = true;
            ShowAlert(AlertKind.Critical, "FASE CRITICA", "Quedan 30 segundos de contencion.", eventColor);
        }
        if (!tenSecondWarningShown && timeRemaining <= 10f)
        {
            tenSecondWarningShown = true;
            ShowAlert(AlertKind.Critical, "ULTIMOS 10 SEGUNDOS", "La anomalia debe cerrar la persecucion.", anomalyColor);
        }
    }

    private void ShowAlert(AlertKind kind, string title, string subtitle, Color color)
    {
        alertKind = kind;
        alertTitle = string.IsNullOrWhiteSpace(title) ? "ALERTA" : title.ToUpperInvariant();
        alertSubtitle = subtitle ?? string.Empty;
        alertColor = color;
        alertTimer = AlertDuration;
    }

    private void OnGUI()
    {
        if (gameManager == null || gameManager.IsGameOver || GameMenuController.ShouldHideGameplayHud ||
            SceneTransitionController.IsFading)
        {
            return;
        }

        EnsureStyles();
        float scale = GetScale();
        UpdateStyleScale(scale);
        if (!introductionComplete)
        {
            DrawIntroduction(scale);
            return;
        }
        if (!gameManager.IsRunActive)
        {
            DrawPreRoundFrame(scale);
            return;
        }

        DrawArenaFrame(scale);
        DrawRunnerRail(scale);
        DrawAnomalyRail(scale);
        DrawCenterClock(scale);
        DrawActiveEventChip(scale);
        DrawAlert(scale);
    }

    private void DrawRunnerRail(float scale)
    {
        Rect panel = GetSidePanel(scale, true);
        DrawSidePanel(panel, runnerColor, true);
        if (panel.width < 90f * scale)
        {
            return;
        }

        float pad = 16f * scale;
        float y = panel.y + 20f * scale;

        GUI.Label(new Rect(panel.x + pad, y, panel.width - pad * 2f, 27f * scale), "J1", sectionStyle);
        y += 24f * scale;
        GUI.Label(new Rect(panel.x + pad, y, panel.width - pad * 2f, 40f * scale), "CORREDOR", titleStyle);
        y += 52f * scale;
        DrawDivider(panel.x + pad, y, panel.width - pad * 2f, runnerColor);
        y += 18f * scale;

        DrawResource(panel, ref y, "FIREWALL", runner != null ? runner.FirewallChargeNormalized : 0f,
            runner != null && runner.IsFirewallBurstReady ? Color.white : runnerColor, scale);
        DrawVersusParryResource(panel, ref y, scale);
        DrawResource(panel, ref y, "GHOST DASH", runner != null ? runner.GhostDashCooldownNormalized : 0f,
            new Color(0.68f, 0.72f, 1f, 1f), scale);

        y += 8f * scale;
        GUI.Label(new Rect(panel.x + pad, y, panel.width - pad * 2f, 20f * scale), "ESTADO", sectionStyle);
        y += 22f * scale;
        string powerup = runner == null || runner.ActivePowerupLabel == "None"
            ? "SIN MODIFICADORES"
            : runner.ActivePowerupLabel.ToUpperInvariant();
        DrawStatusChip(new Rect(panel.x + pad, y, panel.width - pad * 2f, 30f * scale), powerup, runnerColor);

        DrawControlFooter(panel, "WASD  |  ESPACIO  |  Q  |  SHIFT", runnerColor, scale);
    }

    private void DrawAnomalyRail(float scale)
    {
        Rect panel = GetSidePanel(scale, false);
        DrawSidePanel(panel, anomalyColor, false);
        if (panel.width < 90f * scale)
        {
            return;
        }

        float pad = 16f * scale;
        float y = panel.y + 20f * scale;

        GUI.Label(new Rect(panel.x + pad, y, panel.width - pad * 2f, 27f * scale), "J2", sectionStyle);
        y += 24f * scale;
        GUI.Label(new Rect(panel.x + pad, y, panel.width - pad * 2f, 40f * scale), "ANOMALIA", titleStyle);
        y += 52f * scale;
        DrawDivider(panel.x + pad, y, panel.width - pad * 2f, anomalyColor);
        y += 18f * scale;

        string state = anomaly != null ? anomaly.CurrentStateLabel.ToUpperInvariant() : "SIN ESTADO";
        GUI.Label(new Rect(panel.x + pad, y, panel.width - pad * 2f, 20f * scale), "ESTADO ACTIVO", sectionStyle);
        y += 23f * scale;
        GUI.Label(new Rect(panel.x + pad, y, panel.width - pad * 2f, 35f * scale), state, valueStyle);
        y += 43f * scale;
        GUI.Label(new Rect(panel.x + pad, y, panel.width - pad * 2f, 45f * scale),
            anomaly != null ? GetStateHint(anomaly.CurrentState) : string.Empty, compactStyle);
        y += 55f * scale;

        float stateFill = anomaly != null
            ? 1f - anomaly.LocalVersusStateTimeRemaining / 20f
            : 0f;
        DrawResource(panel, ref y, "CICLO DE ESTADO", stateFill, anomalyColor, scale);
        string change = anomaly != null && anomaly.CanLocalVersusChangeState
            ? "CAMBIO DISPONIBLE"
            : $"CAMBIO EN {Mathf.CeilToInt(anomaly != null ? anomaly.LocalVersusManualChangeTimeRemaining : 0f)}s";
        DrawStatusChip(new Rect(panel.x + pad, y, panel.width - pad * 2f, 30f * scale), change, anomalyColor);

        DrawControlFooter(panel, "FLECHAS  |  CTRL DER / ENTER", anomalyColor, scale);
    }

    private void DrawCenterClock(float scale)
    {
        float width = Mathf.Min(260f * scale, Screen.width * 0.28f);
        Rect clock = new Rect((Screen.width - width) * 0.5f, 12f * scale, width, 54f * scale);
        DrawRect(clock, new Color(0.012f, 0.022f, 0.048f, 0.93f));
        DrawRect(new Rect(clock.x, clock.y, clock.width * 0.5f, 2f * scale), runnerColor);
        DrawRect(new Rect(clock.center.x, clock.y, clock.width * 0.5f, 2f * scale), anomalyColor);
        DrawCornerBrackets(clock, Color.Lerp(runnerColor, anomalyColor, 0.5f), scale);

        int seconds = Mathf.CeilToInt(timeRemaining);
        string time = $"{seconds / 60:00}:{seconds % 60:00}";
        GUI.Label(new Rect(clock.x, clock.y + 3f * scale, clock.width, 17f * scale), "DUELO LOCAL", sectionStyle);
        GUI.Label(new Rect(clock.x, clock.y + 18f * scale, clock.width, 32f * scale), time, valueStyle);
    }

    private void DrawActiveEventChip(float scale)
    {
        string label = gameManager.CurrentMapEventLabel;
        if (string.IsNullOrWhiteSpace(label) || string.Equals(label, "Nominal", System.StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        float width = Mathf.Min(390f * scale, Screen.width * 0.42f);
        Rect chip = new Rect((Screen.width - width) * 0.5f, 73f * scale, width, 30f * scale);
        DrawRect(chip, new Color(0.09f, 0.055f, 0.018f, 0.88f));
        DrawRect(new Rect(chip.x, chip.y, 4f * scale, chip.height), eventColor);
        GUI.Label(chip, label.ToUpperInvariant(), compactStyle);
    }

    private void DrawAlert(float scale)
    {
        if (alertTimer <= 0f)
        {
            return;
        }

        float age = AlertDuration - alertTimer;
        float enter = Smooth01(age / 0.25f);
        float exit = Smooth01(alertTimer / 0.35f);
        float alpha = Mathf.Min(enter, exit);
        float width = Mathf.Min(520f * scale, Screen.width * 0.52f);
        float height = 84f * scale;
        float slide = (1f - enter) * 42f * scale;
        Rect panel = new Rect((Screen.width - width) * 0.5f, Screen.height * 0.69f + slide, width, height);

        Color fill = alertKind == AlertKind.Event
            ? new Color(0.07f, 0.045f, 0.015f, 0.91f * alpha)
            : new Color(0.035f, 0.015f, 0.045f, 0.91f * alpha);
        DrawRect(panel, fill);
        DrawRect(new Rect(panel.x, panel.y, panel.width, 3f * scale),
            new Color(alertColor.r, alertColor.g, alertColor.b, alpha));
        DrawRect(new Rect(panel.x, panel.yMax - 2f * scale, panel.width, 2f * scale),
            new Color(alertColor.r, alertColor.g, alertColor.b, alpha * 0.45f));
        DrawCornerBrackets(panel, new Color(alertColor.r, alertColor.g, alertColor.b, alpha), scale);

        float scanX = panel.x + Mathf.Repeat(Time.unscaledTime * 380f, panel.width + 24f * scale) - 12f * scale;
        DrawRect(new Rect(scanX, panel.y, 8f * scale, panel.height),
            new Color(alertColor.r, alertColor.g, alertColor.b, 0.10f * alpha));

        Color previous = GUI.color;
        GUI.color = new Color(1f, 1f, 1f, alpha);
        string prefix = alertKind == AlertKind.State ? "CAMBIO DE ESTADO" :
            alertKind == AlertKind.Event ? "EVENTO DE ARENA" : "ALERTA";
        GUI.Label(new Rect(panel.x + 18f * scale, panel.y + 8f * scale, panel.width - 36f * scale, 18f * scale),
            prefix, sectionStyle);
        GUI.Label(new Rect(panel.x + 18f * scale, panel.y + 24f * scale, panel.width - 36f * scale, 31f * scale),
            alertTitle, alertTitleStyle);
        GUI.Label(new Rect(panel.x + 18f * scale, panel.y + 55f * scale, panel.width - 36f * scale, 21f * scale),
            alertSubtitle, alertBodyStyle);
        GUI.color = previous;
    }

    private void DrawArenaFrame(float scale)
    {
        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 3.2f);
        float railWidth = 3f * scale;
        DrawRect(new Rect(0f, 0f, railWidth, Screen.height),
            new Color(runnerColor.r, runnerColor.g, runnerColor.b, 0.30f + pulse * 0.16f));
        DrawRect(new Rect(Screen.width - railWidth, 0f, railWidth, Screen.height),
            new Color(anomalyColor.r, anomalyColor.g, anomalyColor.b, 0.30f + pulse * 0.16f));

        for (int i = 0; i < 9; i++)
        {
            float y = Mathf.Repeat(Time.unscaledTime * (22f + i * 2f) + i * Screen.height / 9f, Screen.height);
            DrawRect(new Rect(5f * scale, y, Mathf.Lerp(12f, 34f, (i % 3) / 2f) * scale, 1f * scale),
                new Color(runnerColor.r, runnerColor.g, runnerColor.b, 0.16f));
            float width = Mathf.Lerp(12f, 34f, ((i + 1) % 3) / 2f) * scale;
            DrawRect(new Rect(Screen.width - 5f * scale - width, Screen.height - y, width, 1f * scale),
                new Color(anomalyColor.r, anomalyColor.g, anomalyColor.b, 0.16f));
        }
    }

    private void DrawSidePanel(Rect panel, Color accent, bool left)
    {
        DrawRect(panel, new Color(0.012f, 0.022f, 0.046f, 0.90f));
        DrawRect(new Rect(left ? panel.xMax - 3f : panel.x, panel.y, 3f, panel.height),
            new Color(accent.r, accent.g, accent.b, 0.72f));
        DrawCornerBrackets(panel, accent, GetScale());

        float sweep = Mathf.Repeat(Time.unscaledTime * 58f, panel.height + 20f) - 10f;
        DrawRect(new Rect(panel.x + 3f, panel.y + sweep, panel.width - 6f, 2f),
            new Color(accent.r, accent.g, accent.b, 0.07f));
    }

    private void DrawResource(Rect panel, ref float y, string label, float fill, Color color, float scale)
    {
        float pad = 16f * scale;
        GUI.Label(new Rect(panel.x + pad, y, panel.width - pad * 2f, 18f * scale), label, compactStyle);
        y += 19f * scale;
        Rect bar = new Rect(panel.x + pad, y, panel.width - pad * 2f, 11f * scale);
        DrawRect(bar, new Color(0.04f, 0.07f, 0.12f, 0.96f));
        DrawRect(new Rect(bar.x, bar.y, bar.width * Mathf.Clamp01(fill), bar.height),
            new Color(color.r, color.g, color.b, 0.82f));
        DrawRect(new Rect(bar.x + Mathf.Repeat(Time.unscaledTime * 46f, Mathf.Max(1f, bar.width)), bar.y, 2f * scale, bar.height),
            new Color(1f, 1f, 1f, 0.24f));
        y += 22f * scale;
    }

    private void DrawVersusParryResource(Rect panel, ref float y, float scale)
    {
        float pad = 16f * scale;
        int charges = runner != null ? runner.LocalVersusParryCharges : 0;
        int maximum = runner != null ? runner.LocalVersusParryChargesMax : 3;
        string label = charges > 0
            ? $"PARRY  {charges}/{maximum}"
            : $"PARRY  RECARGA {Mathf.CeilToInt(runner != null ? runner.LocalVersusParryRechargeRemaining : 0f)}s";
        GUI.Label(new Rect(panel.x + pad, y, panel.width - pad * 2f, 18f * scale), label, compactStyle);
        y += 19f * scale;

        float gap = 4f * scale;
        float totalWidth = panel.width - pad * 2f;
        float segmentWidth = (totalWidth - gap * (maximum - 1)) / Mathf.Max(1, maximum);
        for (int i = 0; i < maximum; i++)
        {
            Rect segment = new Rect(panel.x + pad + i * (segmentWidth + gap), y, segmentWidth, 11f * scale);
            bool loaded = i < charges;
            DrawRect(segment, loaded
                ? new Color(0.48f, 1f, 0.82f, 0.86f)
                : new Color(0.04f, 0.07f, 0.12f, 0.96f));
            if (!loaded && charges <= 0)
            {
                float fill = runner != null ? runner.LocalVersusParryRechargeNormalized : 0f;
                DrawRect(new Rect(segment.x, segment.y, segment.width * fill, segment.height),
                    new Color(0.48f, 1f, 0.82f, 0.28f));
            }
        }
        y += 22f * scale;
    }

    private void DrawStatusChip(Rect rect, string label, Color accent)
    {
        DrawRect(rect, new Color(accent.r, accent.g, accent.b, 0.13f));
        DrawRect(new Rect(rect.x, rect.y, 3f, rect.height), new Color(accent.r, accent.g, accent.b, 0.72f));
        GUI.Label(rect, label, compactStyle);
    }

    private void DrawControlFooter(Rect panel, string controls, Color accent, float scale)
    {
        Rect footer = new Rect(panel.x + 12f * scale, panel.yMax - 45f * scale, panel.width - 24f * scale, 30f * scale);
        DrawRect(footer, new Color(accent.r, accent.g, accent.b, 0.10f));
        GUI.Label(footer, controls, compactStyle);
    }

    private void DrawIntroduction(float scale)
    {
        float closeProgress = introClosingTimer > 0f ? 1f - introClosingTimer / 0.72f : 0f;
        float alpha = 1f - Smooth01(closeProgress);
        float entrance = Smooth01(introAge / 0.45f);
        DrawRect(new Rect(0f, 0f, Screen.width, Screen.height), new Color(0.004f, 0.008f, 0.018f, 0.72f * alpha));

        float usableX = Screen.width * sideViewportFraction;
        float usableWidth = Screen.width * (1f - sideViewportFraction * 2f);
        float width = Mathf.Min(920f * scale, usableWidth - 24f * scale);
        float height = Mathf.Min(570f * scale, Screen.height - 42f * scale);
        float slide = (1f - entrance) * 55f * scale;
        Rect panel = new Rect(usableX + (usableWidth - width) * 0.5f, (Screen.height - height) * 0.5f + slide, width, height);

        Color previous = GUI.color;
        GUI.color = new Color(1f, 1f, 1f, alpha);
        DrawRect(panel, new Color(0.012f, 0.022f, 0.048f, 0.96f));
        DrawRect(new Rect(panel.x, panel.y, panel.width * 0.5f, 3f * scale), runnerColor);
        DrawRect(new Rect(panel.center.x, panel.y, panel.width * 0.5f, 3f * scale), anomalyColor);
        DrawCornerBrackets(panel, Color.Lerp(runnerColor, anomalyColor, 0.5f), scale);
        DrawIntroScanlines(panel, scale);

        GUI.Label(new Rect(panel.x + 24f * scale, panel.y + 18f * scale, panel.width - 48f * scale, 42f * scale),
            "PROTOCOLO 1V1 LOCAL", alertTitleStyle);
        GUI.Label(new Rect(panel.x + 34f * scale, panel.y + 61f * scale, panel.width - 68f * scale, 40f * scale),
            "El corredor debe sobrevivir 2 minutos. La anomalia debe alcanzarlo antes de que termine el protocolo.",
            alertBodyStyle);

        float cardsY = panel.y + 118f * scale;
        float gap = 18f * scale;
        float cardWidth = (panel.width - 54f * scale - gap) * 0.5f;
        float cardHeight = 250f * scale;
        Rect runnerCard = new Rect(panel.x + 27f * scale, cardsY, cardWidth, cardHeight);
        Rect anomalyCard = new Rect(runnerCard.xMax + gap, cardsY, cardWidth, cardHeight);
        DrawIntroPlayerCard(runnerCard, true, scale);
        DrawIntroPlayerCard(anomalyCard, false, scale);

        Rect rule = new Rect(panel.x + 27f * scale, runnerCard.yMax + 16f * scale, panel.width - 54f * scale, 76f * scale);
        DrawRect(rule, new Color(0.055f, 0.045f, 0.075f, 0.92f));
        DrawRect(new Rect(rule.x, rule.y, 4f * scale, rule.height), eventColor);
        GUI.Label(new Rect(rule.x + 14f * scale, rule.y + 7f * scale, rule.width - 28f * scale, 20f * scale),
            "ESTADOS Y EVENTOS", sectionStyle);
        GUI.Label(new Rect(rule.x + 14f * scale, rule.y + 27f * scale, rule.width - 28f * scale, 42f * scale),
            "J2 puede cambiar de estado luego de 10s; a los 20s cambia automaticamente. Los eventos de arena afectan a ambos.",
            compactStyle);

        string sync = runnerReady && anomalyReady
            ? "SINCRONIZANDO JUGADORES..."
            : "AMBOS JUGADORES DEBEN CONFIRMAR";
        GUI.Label(new Rect(panel.x + 24f * scale, panel.yMax - 43f * scale, panel.width - 48f * scale, 25f * scale),
            sync, sectionStyle);
        GUI.color = previous;
    }

    private void DrawIntroPlayerCard(Rect card, bool isRunner, float scale)
    {
        Color accent = isRunner ? runnerColor : anomalyColor;
        bool ready = isRunner ? runnerReady : anomalyReady;
        DrawRect(card, new Color(accent.r, accent.g, accent.b, ready ? 0.16f : 0.075f));
        DrawRect(new Rect(card.x, card.y, card.width, 2f * scale), accent);
        DrawCornerBrackets(card, accent, scale);

        string player = isRunner ? "J1 | CORREDOR" : "J2 | ANOMALIA";
        string objective = isRunner ? "SOBREVIVIR" : "CAPTURAR";
        GUI.Label(new Rect(card.x + 12f * scale, card.y + 12f * scale, card.width - 24f * scale, 22f * scale),
            player, sectionStyle);
        GUI.Label(new Rect(card.x + 12f * scale, card.y + 36f * scale, card.width - 24f * scale, 34f * scale),
            objective, titleStyle);

        float y = card.y + 78f * scale;
        if (isRunner)
        {
            DrawIntroControlLine(card, ref y, "WASD", "Moverse y combinar direcciones", accent, scale);
            DrawIntroControlLine(card, ref y, "ESPACIO", "Parry cuando la anomalia esta cerca", accent, scale);
            DrawIntroControlLine(card, ref y, "Q", "Firewall cuando la barra esta llena", accent, scale);
            DrawIntroControlLine(card, ref y, "SHIFT", "Ghost Dash para reposicionarse", accent, scale);
        }
        else
        {
            DrawIntroControlLine(card, ref y, "FLECHAS", "Controlar directamente la anomalia", accent, scale);
            DrawIntroControlLine(card, ref y, "CTRL DER", "Solicitar otro estado desde los 10s", accent, scale);
            DrawIntroControlLine(card, ref y, "AUTOMATICO", "El estado cambia al cumplir 20s", accent, scale);
            DrawIntroControlLine(card, ref y, "HABILIDADES", "Se activan como en el modo normal", accent, scale);
        }

        float footerY = card.yMax - 39f * scale;
        DrawRect(new Rect(card.x + 14f * scale, footerY - 7f * scale, card.width - 28f * scale, 1f * scale),
            new Color(accent.r, accent.g, accent.b, 0.34f));
        Rect readyRect = new Rect(card.x + 14f * scale, footerY, card.width - 28f * scale, 27f * scale);
        DrawRect(readyRect, new Color(accent.r, accent.g, accent.b, ready ? 0.42f : 0.13f));
        string readyText = ready
            ? "LISTO"
            : isRunner ? "PULSA ESPACIO PARA CONFIRMAR" : "PULSA ENTER PARA CONFIRMAR";
        GUI.Label(readyRect, readyText, compactStyle);
    }

    private void DrawIntroControlLine(Rect card, ref float y, string key, string description, Color accent, float scale)
    {
        Rect keyRect = new Rect(card.x + 15f * scale, y, 86f * scale, 22f * scale);
        DrawRect(keyRect, new Color(accent.r, accent.g, accent.b, 0.21f));
        GUI.Label(keyRect, key, compactStyle);
        GUI.Label(new Rect(keyRect.xMax + 10f * scale, y, card.xMax - keyRect.xMax - 25f * scale, 23f * scale),
            description, compactStyle);
        y += 27f * scale;
    }

    private void DrawIntroScanlines(Rect panel, float scale)
    {
        for (int i = 0; i < 7; i++)
        {
            float y = panel.y + Mathf.Repeat(Time.unscaledTime * (18f + i * 2f) + i * panel.height / 7f, panel.height);
            DrawRect(new Rect(panel.x + 4f * scale, y, panel.width - 8f * scale, 1f),
                new Color(0.55f, 0.78f, 1f, 0.035f));
        }
    }

    private void DrawPreRoundFrame(float scale)
    {
        DrawArenaFrame(scale);
        DrawRunnerRail(scale);
        DrawAnomalyRail(scale);
    }

    private Rect GetSidePanel(float scale, bool left)
    {
        float margin = 12f * scale;
        GetArenaScreenBounds(out float arenaLeft, out float arenaRight);
        float available = left ? arenaLeft : Screen.width - arenaRight;
        float width = Mathf.Clamp(available - margin * 2f, 0f, 260f * scale);
        float x = left ? margin : Screen.width - margin - width;
        return new Rect(x, margin, width, Screen.height - margin * 2f);
    }

    private void ApplyReservedCameraLayout()
    {
        gameplayCamera = Camera.main;
        if (gameplayCamera == null || cameraLayoutApplied)
        {
            return;
        }

        originalCameraRect = gameplayCamera.rect;
        originalOrthographicSize = gameplayCamera.orthographicSize;
        gameplayCamera.rect = new Rect(0f, 0f, 1f, 1f);
        arenaGenerator = FindAnyObjectByType<ProceduralArenaGenerator>();
        cameraLayoutApplied = true;
    }

    private void RestoreCameraLayout()
    {
        if (!cameraLayoutApplied || gameplayCamera == null)
        {
            return;
        }

        gameplayCamera.rect = originalCameraRect;
        if (gameplayCamera.orthographic)
        {
            gameplayCamera.orthographicSize = originalOrthographicSize;
        }
        cameraLayoutApplied = false;
    }

    private void GetArenaScreenBounds(out float left, out float right)
    {
        left = Screen.width * Mathf.Clamp(sideViewportFraction, 0.10f, 0.24f);
        right = Screen.width - left;
        if (gameplayCamera == null)
        {
            gameplayCamera = Camera.main;
        }
        if (arenaGenerator == null)
        {
            arenaGenerator = FindAnyObjectByType<ProceduralArenaGenerator>();
        }
        if (gameplayCamera == null || arenaGenerator == null)
        {
            return;
        }

        Vector2 center = arenaGenerator.transform.position;
        float halfWidth = arenaGenerator.ArenaWidth * 0.5f;
        Vector3 leftPoint = gameplayCamera.WorldToScreenPoint(new Vector3(center.x - halfWidth, center.y, 0f));
        Vector3 rightPoint = gameplayCamera.WorldToScreenPoint(new Vector3(center.x + halfWidth, center.y, 0f));
        left = Mathf.Clamp(Mathf.Min(leftPoint.x, rightPoint.x), 0f, Screen.width);
        right = Mathf.Clamp(Mathf.Max(leftPoint.x, rightPoint.x), 0f, Screen.width);
    }

    private static bool WasRunnerReadyPressed()
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.spaceKey.wasPressedThisFrame)
        {
            return true;
        }
        if (Gamepad.all.Count > 0 && Gamepad.all[0].buttonSouth.wasPressedThisFrame)
        {
            return true;
        }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetKeyDown(KeyCode.Space);
#else
        return false;
#endif
    }

    private static bool WasAnomalyReadyPressed()
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame))
        {
            return true;
        }
        if (Gamepad.all.Count > 1 && Gamepad.all[1].buttonSouth.wasPressedThisFrame)
        {
            return true;
        }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter);
#else
        return false;
#endif
    }

    private static string NormalizeEventKey(string label)
    {
        if (string.IsNullOrWhiteSpace(label))
        {
            return string.Empty;
        }

        string trimmed = label.Trim();
        int separator = trimmed.IndexOf('|');
        return separator >= 0 ? trimmed.Substring(0, separator).Trim() : trimmed;
    }

    private static string NormalizeEventIdentity(string label)
    {
        if (string.IsNullOrWhiteSpace(label))
        {
            return string.Empty;
        }

        char[] characters = label.ToCharArray();
        for (int i = 0; i < characters.Length; i++)
        {
            if (char.IsDigit(characters[i]))
            {
                characters[i] = '#';
            }
        }

        return new string(characters);
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
                return "Despliega el bombardeo orbital.";
            default:
                return "Persecucion directa controlada por J2.";
        }
    }

    private void EnsureStyles()
    {
        if (titleStyle != null)
        {
            return;
        }

        Font important = GlobalFontSettings.GetImportantFont();
        Font secondary = GlobalFontSettings.GetSecondaryFont();
        titleStyle = CreateStyle(important, 24, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white, false);
        sectionStyle = CreateStyle(secondary, 11, FontStyle.Bold, TextAnchor.MiddleCenter,
            new Color(0.72f, 0.82f, 0.94f, 0.92f), false);
        valueStyle = CreateStyle(important, 20, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white, false);
        compactStyle = CreateStyle(secondary, 10, FontStyle.Bold, TextAnchor.MiddleCenter,
            new Color(0.86f, 0.92f, 1f, 0.96f), true);
        alertTitleStyle = CreateStyle(important, 23, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white, false);
        alertBodyStyle = CreateStyle(secondary, 11, FontStyle.Bold, TextAnchor.MiddleCenter,
            new Color(0.90f, 0.94f, 1f, 0.94f), true);
    }

    private void UpdateStyleScale(float scale)
    {
        titleStyle.fontSize = Mathf.Max(16, Mathf.RoundToInt(24f * scale));
        sectionStyle.fontSize = Mathf.Max(9, Mathf.RoundToInt(11f * scale));
        valueStyle.fontSize = Mathf.Max(14, Mathf.RoundToInt(20f * scale));
        compactStyle.fontSize = Mathf.Max(8, Mathf.RoundToInt(10f * scale));
        alertTitleStyle.fontSize = Mathf.Max(16, Mathf.RoundToInt(23f * scale));
        alertBodyStyle.fontSize = Mathf.Max(9, Mathf.RoundToInt(11f * scale));
    }

    private static GUIStyle CreateStyle(
        Font font,
        int size,
        FontStyle fontStyle,
        TextAnchor alignment,
        Color color,
        bool wrap)
    {
        GUIStyle style = new GUIStyle(GUI.skin.label)
        {
            font = font,
            fontSize = size,
            fontStyle = fontStyle,
            alignment = alignment,
            wordWrap = wrap,
            clipping = TextClipping.Clip
        };
        style.normal.textColor = color;
        return style;
    }

    private static void DrawDivider(float x, float y, float width, Color color)
    {
        DrawRect(new Rect(x, y, width, 1f), new Color(color.r, color.g, color.b, 0.42f));
    }

    private static void DrawCornerBrackets(Rect rect, Color color, float scale)
    {
        float length = 16f * scale;
        float thickness = Mathf.Max(1f, 2f * scale);
        Color c = new Color(color.r, color.g, color.b, color.a * 0.72f);
        DrawRect(new Rect(rect.x + 6f, rect.y + 6f, length, thickness), c);
        DrawRect(new Rect(rect.x + 6f, rect.y + 6f, thickness, length), c);
        DrawRect(new Rect(rect.xMax - 6f - length, rect.y + 6f, length, thickness), c);
        DrawRect(new Rect(rect.xMax - 6f - thickness, rect.y + 6f, thickness, length), c);
        DrawRect(new Rect(rect.x + 6f, rect.yMax - 6f - thickness, length, thickness), c);
        DrawRect(new Rect(rect.x + 6f, rect.yMax - 6f - length, thickness, length), c);
        DrawRect(new Rect(rect.xMax - 6f - length, rect.yMax - 6f - thickness, length, thickness), c);
        DrawRect(new Rect(rect.xMax - 6f - thickness, rect.yMax - 6f - length, thickness, length), c);
    }

    private static float GetScale()
    {
        return Mathf.Clamp(Mathf.Min(Screen.width / 1280f, Screen.height / 720f), 0.70f, 1.15f);
    }

    private static float Smooth01(float value)
    {
        value = Mathf.Clamp01(value);
        return value * value * (3f - 2f * value);
    }

    private static void DrawRect(Rect rect, Color color)
    {
        Color previous = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = previous;
    }
}
