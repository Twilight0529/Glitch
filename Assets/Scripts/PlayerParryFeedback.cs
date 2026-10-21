using UnityEngine;

// A single reusable, non-interactive label. It reports facts, never guesses
// timing errors, and cannot extend the parry window or consume input events.
[DisallowMultipleComponent]
public sealed class PlayerParryFeedback : MonoBehaviour
{
    public enum Reason { NoContact, Cooldown, NoCharges }

    private const float DisplayDuration = 0.8f;
    private const float MinimumInterval = 0.45f;
    private PlayerController owner;
    private Camera worldCamera;
    private GUIStyle labelStyle;
    private string message;
    private float remaining;
    private float nextAllowedTime;

    public bool Show(PlayerController player, Reason reason)
    {
        if (player == null || Time.timeScale <= 0f || player.IsInDeathSequence || player.IsInBreachConsumption ||
            Time.time < nextAllowedTime || remaining > 0f)
        {
            return false;
        }
        owner = player;
        message = reason == Reason.NoCharges ? "Parry: sin cargas"
            : reason == Reason.Cooldown ? "Parry: recargando"
            : "Parry: sin contacto";
        remaining = DisplayDuration;
        nextAllowedTime = Time.time + MinimumInterval;
        return true;
    }

    public void Clear()
    {
        remaining = 0f;
        nextAllowedTime = 0f;
    }

    private void OnDisable()
    {
        Clear();
    }

    private void Update()
    {
        if (owner == null || owner.IsInDeathSequence || owner.IsInBreachConsumption || !owner.isActiveAndEnabled)
        {
            Clear();
            return;
        }
        remaining = Mathf.Max(0f, remaining - Time.deltaTime);
    }

    private void OnGUI()
    {
        if (Event.current.type != EventType.Repaint || remaining <= 0f || Time.timeScale <= 0f ||
            owner == null || owner.IsInDeathSequence || owner.IsInBreachConsumption || !owner.isActiveAndEnabled)
        {
            return;
        }
        if (worldCamera == null)
        {
            worldCamera = Camera.main;
        }
        if (worldCamera == null)
        {
            return;
        }
        Vector3 screenPoint = worldCamera.WorldToScreenPoint(transform.position + Vector3.up * 0.9f);
        if (screenPoint.z <= 0f)
        {
            return;
        }
        if (labelStyle == null)
        {
            labelStyle = new GUIStyle(GUI.skin.label)
            {
                font = GlobalFontSettings.GetSecondaryFont(),
                fontSize = 17,
                alignment = TextAnchor.MiddleCenter,
                richText = false
            };
        }
        float width = Mathf.Min(labelStyle.CalcSize(new GUIContent(message)).x + 18f, Mathf.Max(1f, Screen.width - 16f));
        Rect rect = new Rect(
            Mathf.Clamp(screenPoint.x - width * 0.5f, 8f, Mathf.Max(8f, Screen.width - width - 8f)),
            Mathf.Clamp(Screen.height - screenPoint.y - 24f, 8f, Mathf.Max(8f, Screen.height - 34f)), width, 26f);
        Color oldColor = GUI.color;
        float alpha = Mathf.Clamp01(remaining / 0.18f);
        GUI.color = GlitchUiPalette.WithAlpha(GlitchUiPalette.Surface, alpha * 0.94f);
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = GlitchUiPalette.WithAlpha(GlitchUiPalette.Alert, alpha);
        GUI.DrawTexture(new Rect(rect.x, rect.yMax - 2f, rect.width, 2f), Texture2D.whiteTexture);
        labelStyle.normal.textColor = GlitchUiPalette.Alert;
        GUI.color = new Color(1f, 1f, 1f, alpha);
        GUI.Label(rect, message, labelStyle);
        GUI.color = oldColor;
    }
}
