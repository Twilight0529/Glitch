using UnityEngine;

// Destello corto del pickup: crece, gira un poco y se apaga. No decide premios ni conoce la partida.
public class ScoreCollectFlash : MonoBehaviour
{
    private const float Duration = 0.28f;

    private SpriteRenderer spriteRenderer;
    private float age;

    public void Configure(SpriteRenderer rendererRef)
    {
        spriteRenderer = rendererRef;
    }

    private void Update()
    {
        float progress = AdvanceProgress();
        transform.localScale = Vector3.one * Mathf.Lerp(0.35f, 1.35f, progress);
        transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(0f, 35f, progress));

        if (spriteRenderer != null)
        {
            spriteRenderer.color = new Color(1f, 1f, 1f, 1f - progress);
        }
    }

    private float AdvanceProgress()
    {
        age += Time.deltaTime;
        return Mathf.Clamp01(age / Duration);
    }
}
