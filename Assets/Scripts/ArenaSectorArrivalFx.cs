using UnityEngine;

public class ArenaSectorArrivalFx : MonoBehaviour
{
    // Presenta el nuevo sector, su regla principal y el aumento de presion antes de reanudar la persecucion.
    private readonly SpriteRenderer[] rings = new SpriteRenderer[4];
    private readonly SpriteRenderer[] pressureTicks = new SpriteRenderer[8];
    private readonly SpriteRenderer[] scanLines = new SpriteRenderer[9];
    private SpriteRenderer wash;
    private SpriteRenderer core;
    private SpriteRenderer titlePlate;
    private TextMesh sectorText;
    private TextMesh themeText;
    private TextMesh ruleText;
    private TextMesh pressureText;
    private Color accent = Color.cyan;
    private Vector2 arenaSize = new Vector2(32f, 18f);
    private float duration = 2.2f;
    private float age;
    private int sectorLevel = 1;

    public void Configure(
        ProceduralArenaGenerator arena,
        int level,
        Color color,
        float seconds,
        string themeLabel,
        string themeRule,
        float pressureMultiplier)
    {
        if (arena != null)
        {
            arenaSize = new Vector2(arena.ArenaWidth, arena.ArenaHeight);
        }

        sectorLevel = Mathf.Max(1, level);
        accent = color;
        duration = Mathf.Max(0.8f, seconds);
        CreateVisuals();
        ConfigureText(themeLabel, themeRule, pressureMultiplier);
    }

    private void Update()
    {
        age += Time.deltaTime;
        float t = Mathf.Clamp01(age / duration);
        float enter = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / 0.22f));
        float exit = 1f - Mathf.SmoothStep(0.68f, 1f, t);
        float visibility = enter * exit;
        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 8f);

        wash.color = new Color(accent.r, accent.g, accent.b, visibility * Mathf.Lerp(0.035f, 0.085f, pulse));
        core.transform.rotation = Quaternion.Euler(0f, 0f, Time.time * 95f);
        core.transform.localScale = Vector3.one * Mathf.Lerp(0.65f, 1.05f, pulse);
        core.color = new Color(1f, 1f, 1f, visibility * Mathf.Lerp(0.28f, 0.65f, pulse));
        titlePlate.color = new Color(0.01f, 0.02f, 0.03f, visibility * 0.78f);
        UpdateTextAlpha(visibility);

        for (int i = 0; i < rings.Length; i++)
        {
            float phase = Mathf.Repeat(t * 1.15f + i / (float)rings.Length, 1f);
            rings[i].transform.localScale = Vector3.one * Mathf.Lerp(0.8f, 5.8f, phase);
            rings[i].color = new Color(accent.r, accent.g, accent.b, visibility * Mathf.Lerp(0.34f, 0f, phase));
        }

        for (int i = 0; i < pressureTicks.Length; i++)
        {
            bool enabledTick = i < Mathf.Min(pressureTicks.Length, sectorLevel);
            float angle = i / (float)pressureTicks.Length * Mathf.PI * 2f;
            Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            pressureTicks[i].transform.localPosition = direction * 2.1f;
            pressureTicks[i].transform.rotation = Quaternion.Euler(0f, 0f, angle * Mathf.Rad2Deg);
            pressureTicks[i].color = enabledTick
                ? new Color(accent.r, accent.g, accent.b, visibility * Mathf.Lerp(0.55f, 0.95f, pulse))
                : new Color(accent.r, accent.g, accent.b, visibility * 0.10f);
        }

        for (int i = 0; i < scanLines.Length; i++)
        {
            float linePhase = Mathf.Repeat(t * 1.5f + i / (float)scanLines.Length, 1f);
            float y = Mathf.Lerp(-arenaSize.y * 0.48f, arenaSize.y * 0.48f, linePhase);
            scanLines[i].transform.localPosition = new Vector3(0f, y, 0f);
            scanLines[i].size = new Vector2(arenaSize.x * Mathf.Lerp(0.25f, 0.92f, pulse), 0.045f);
            scanLines[i].color = new Color(accent.r, accent.g, accent.b, visibility * Mathf.Lerp(0.18f, 0f, linePhase));
        }

        if (age >= duration)
        {
            Destroy(gameObject);
        }
    }

    private void CreateVisuals()
    {
        wash = CreateRenderer("SectorArrivalWash", 28);
        wash.size = arenaSize + Vector2.one * 2f;

        core = CreateRenderer("SectorArrivalCore", 33);
        core.sprite = SquareSpriteProvider.Get();
        core.size = Vector2.one * 1.1f;

        titlePlate = CreateRenderer("SectorArrivalTitlePlate", 36);
        titlePlate.size = new Vector2(Mathf.Min(13.5f, arenaSize.x * 0.58f), 4.4f);

        sectorText = CreateText("SectorArrivalSector", 40, 0.10f, new Vector3(0f, 1.35f, 0f));
        themeText = CreateText("SectorArrivalTheme", 41, 0.18f, new Vector3(0f, 0.48f, 0f));
        ruleText = CreateText("SectorArrivalRule", 40, 0.075f, new Vector3(0f, -0.55f, 0f));
        pressureText = CreateText("SectorArrivalPressure", 40, 0.075f, new Vector3(0f, -1.42f, 0f));

        for (int i = 0; i < rings.Length; i++)
        {
            rings[i] = CreateRenderer($"SectorArrivalRing_{i}", 31);
            rings[i].sprite = CircleSpriteProvider.Get();
            rings[i].size = Vector2.one;
        }

        for (int i = 0; i < pressureTicks.Length; i++)
        {
            pressureTicks[i] = CreateRenderer($"SectorPressureTick_{i}", 34);
            pressureTicks[i].size = new Vector2(0.65f, 0.10f);
        }

        for (int i = 0; i < scanLines.Length; i++)
        {
            scanLines[i] = CreateRenderer($"SectorScanLine_{i}", 30);
        }
    }

    private void ConfigureText(string themeLabel, string themeRule, float pressureMultiplier)
    {
        sectorText.text = $"SECTOR {sectorLevel}";
        themeText.text = string.IsNullOrWhiteSpace(themeLabel) ? "UNKNOWN" : themeLabel.ToUpperInvariant();
        ruleText.text = FormatRule(themeRule);
        pressureText.text = $"PRESION DE SISTEMA x{Mathf.Max(1f, pressureMultiplier):0.00}";
    }

    private static string FormatRule(string themeRule)
    {
        if (string.IsNullOrWhiteSpace(themeRule))
        {
            return "Nueva regla de contencion";
        }

        int separator = themeRule.IndexOf(':');
        return separator > 0 && separator < themeRule.Length - 1
            ? themeRule.Substring(0, separator + 1) + "\n" + themeRule.Substring(separator + 1).Trim()
            : themeRule;
    }

    private TextMesh CreateText(string objectName, int sortingOrder, float characterSize, Vector3 localPosition)
    {
        GameObject child = new GameObject(objectName);
        child.transform.SetParent(transform, false);
        child.transform.localPosition = localPosition;

        TextMesh text = child.AddComponent<TextMesh>();
        text.font = GlobalFontSettings.GetImportantFont();
        text.fontSize = 48;
        text.characterSize = characterSize;
        text.anchor = TextAnchor.MiddleCenter;
        text.alignment = TextAlignment.Center;
        text.color = Color.clear;

        MeshRenderer renderer = child.GetComponent<MeshRenderer>();
        if (renderer != null)
        {
            renderer.sortingOrder = sortingOrder;
            if (text.font != null)
            {
                renderer.sharedMaterial = text.font.material;
            }
        }

        return text;
    }

    private void UpdateTextAlpha(float visibility)
    {
        SetTextColor(sectorText, new Color(accent.r, accent.g, accent.b, visibility * 0.92f));
        SetTextColor(themeText, new Color(1f, 1f, 1f, visibility));
        SetTextColor(ruleText, new Color(0.86f, 0.94f, 0.92f, visibility * 0.94f));
        SetTextColor(pressureText, new Color(accent.r, accent.g, accent.b, visibility * 0.86f));
    }

    private static void SetTextColor(TextMesh text, Color color)
    {
        if (text != null)
        {
            text.color = color;
        }
    }

    private SpriteRenderer CreateRenderer(string objectName, int sortingOrder)
    {
        GameObject child = new GameObject(objectName);
        child.transform.SetParent(transform, false);
        SpriteRenderer renderer = child.AddComponent<SpriteRenderer>();
        renderer.sprite = SquareSpriteProvider.Get();
        renderer.drawMode = SpriteDrawMode.Sliced;
        renderer.sortingOrder = sortingOrder;
        renderer.color = Color.clear;
        return renderer;
    }
}
