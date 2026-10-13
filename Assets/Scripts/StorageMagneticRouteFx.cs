using UnityEngine;

// A small chevron on the leading side of moving cargo. Its clock is driven by
// the event controller, so visual direction and mechanical phase stay in sync.
public sealed class StorageMagneticRouteFx : MonoBehaviour
{
    private Transform cargo;
    private Vector2 start;
    private Vector2 destination;
    private float cargoRadius;
    private readonly SpriteRenderer[] wings = new SpriteRenderer[2];

    public void Configure(Transform followCargo, Vector2 originalPosition, Vector2 targetPosition, float radius)
    {
        cargo = followCargo;
        start = originalPosition;
        destination = targetPosition;
        cargoRadius = Mathf.Max(0.15f, radius);
        for (int i = 0; i < wings.Length; i++)
        {
            GameObject part = new GameObject("DirectionWing_" + i);
            part.transform.SetParent(transform, false);
            wings[i] = part.AddComponent<SpriteRenderer>();
            wings[i].sprite = SquareSpriteProvider.Get();
            wings[i].drawMode = SpriteDrawMode.Sliced;
            wings[i].size = new Vector2(0.34f, 0.065f);
            wings[i].sortingOrder = 16;
        }
        ShowDirection(false, true, 0f);
    }

    public void ShowDirection(bool returning, bool warning, float eventTime)
    {
        if (cargo == null)
        {
            gameObject.SetActive(false);
            return;
        }

        Vector2 position = cargo.position;
        Vector2 delta = (returning ? start : destination) - position;
        bool visible = delta.sqrMagnitude >= 0.0225f;
        Vector2 direction = visible ? delta.normalized : Vector2.right;
        Vector2 side = new Vector2(-direction.y, direction.x);
        float pulse = 0.5f + 0.5f * Mathf.Sin(eventTime * Mathf.PI * 4f);
        Vector2 center = position + direction * (cargoRadius + 0.28f + pulse * 0.08f);
        transform.position = new Vector3(center.x, center.y, 0f);
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        Color tint = warning ? GlitchUiPalette.Alert : GlitchUiPalette.Danger;
        tint.a = Mathf.Lerp(0.6f, 0.9f, pulse);
        for (int i = 0; i < wings.Length; i++)
        {
            wings[i].enabled = visible;
            float sign = i == 0 ? 1f : -1f;
            wings[i].transform.localPosition = side * (sign * 0.11f);
            wings[i].transform.localRotation = Quaternion.Euler(0f, 0f, angle - sign * 40f);
            wings[i].color = tint;
        }
    }
}
