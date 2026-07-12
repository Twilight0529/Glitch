using UnityEngine;

[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(SpriteRenderer))]
public class ArenaScorePickup : MonoBehaviour
{
    // Este es el dato blanco que flota por la arena. Vive un rato, anima su sprite y premia al jugador al tocarlo.
    [Header("Movimiento y presencia visual")]
    [SerializeField] private bool enableAura = false;
    [SerializeField] private float bobAmplitude = 0.08f;
    [SerializeField] private float bobSpeed = 4.5f;
    [SerializeField] private float pulseSpeed = 5.4f;
    [SerializeField] private float baseScale = 1f;
    [SerializeField] private float auraScale = 1.25f;
    [SerializeField] private float auraPulseSpeed = 3.4f;

    [Header("Efecto al recogerlo")]
    [SerializeField] private int collectSparkCount = 6;
    [SerializeField] private float collectSparkDistance = 0.95f;
    [SerializeField] private float collectSparkLifetime = 0.24f;

    // Estas referencias conectan el pickup con quien lo creo, la partida y sus dos capas visuales.
    private ArenaChaosDirector owner;
    private GameManager gameManager;
    private SpriteRenderer spriteRenderer;
    private SpriteRenderer auraRenderer;
    private GameObject auraObject;

    // Estado propio de esta instancia. Se completa desde Configure apenas aparece en la arena.
    private float lifeTimer;
    private float lifetime;
    private int scoreValue;
    private bool chargedDataCore;
    private float extraFirewallCharge;
    private Vector3 basePosition;

    public int ScoreValue => scoreValue;

    // Los campos gravitatorios mueven el pickup por fuera de su animacion de flotado.
    // También corremos la posición base para que el siguiente frame no lo devuelva al lugar anterior.
    public void ApplyExternalDisplacement(Vector2 delta)
    {
        if (delta.sqrMagnitude <= 0.000001f)
        {
            return;
        }

        basePosition += (Vector3)delta;
    }

    public void Configure(ArenaChaosDirector ownerController, GameManager manager, int points, float lifeSeconds, bool dataCore = false, float firewallChargeBonus = 0f)
    {
        owner = ownerController;
        gameManager = manager;
        scoreValue = Mathf.Max(1, points);
        lifetime = Mathf.Max(0.5f, lifeSeconds);
        chargedDataCore = dataCore;
        extraFirewallCharge = Mathf.Max(0f, firewallChargeBonus);
        if (chargedDataCore)
        {
            enableAura = true;
            baseScale = Mathf.Max(baseScale, 1.24f);
            EnsureAura();
        }

        ApplyVisual();
    }

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        basePosition = transform.position;
        if (enableAura || chargedDataCore)
        {
            EnsureAura();
        }
        ApplyVisual();
    }

    private void Update()
    {
        // Primero revisamos el vencimiento. Si sigue vivo, hacemos el flotado y el pulso visual.
        lifeTimer += Time.deltaTime;
        if (lifeTimer >= lifetime)
        {
            Destroy(gameObject);
            return;
        }

        float bob = Mathf.Sin(Time.time * bobSpeed) * bobAmplitude;
        transform.position = new Vector3(basePosition.x, basePosition.y + bob, basePosition.z);

        float pulse = 0.9f + 0.12f * (0.5f + 0.5f * Mathf.Sin(Time.time * pulseSpeed));
        transform.localScale = Vector3.one * (baseScale * pulse);
        UpdateAura(pulse);
    }

    private void ApplyVisual()
    {
        if (spriteRenderer == null)
        {
            return;
        }

        if (chargedDataCore)
        {
            // El Data Core usa el mismo pickup, pero se ve más grande y brillante porque vale bastante más.
            spriteRenderer.color = GlitchUiPalette.WithAlpha(GlitchUiPalette.Success, 0.98f);
            transform.localScale = Vector3.one * 1.2f;
            return;
        }

        spriteRenderer.color = GlitchUiPalette.WithAlpha(GlitchUiPalette.Information, 0.96f);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other == null)
        {
            return;
        }

        PlayerController player = other.GetComponent<PlayerController>();
        if (player == null)
        {
            return;
        }

        // Repartimos cada recompensa a su dueño: el GameManager recibe score y el jugador carga Firewall.
        if (gameManager != null)
        {
            gameManager.AddScore(scoreValue);
            gameManager.NotifyScorePickupCollected(scoreValue);
        }

        player.AddFirewallChargeFromScore(scoreValue);
        player.AddFirewallCharge(extraFirewallCharge);
        GlitchAudioManager.PlayScorePickup(transform.position);
        SpawnCollectFlash();
        owner?.NotifyScorePickupConsumed(this);
        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        // El director lleva una lista de pickups activos; le avisamos incluso si venció sin ser recogido.
        owner?.NotifyScorePickupDestroyed(this);
    }

    private void EnsureAura()
    {
        if (auraObject != null)
        {
            return;
        }

        // El aura se construye por código para que el pickup no dependa de un prefab adicional.
        auraObject = new GameObject(chargedDataCore ? "DataCoreAura" : "ScoreAura");
        auraObject.transform.SetParent(transform, false);
        auraObject.transform.localPosition = Vector3.zero;
        auraObject.transform.localScale = Vector3.one * Mathf.Max(1f, auraScale);
        auraRenderer = auraObject.AddComponent<SpriteRenderer>();
        auraRenderer.sprite = CircleSpriteProvider.Get();
        auraRenderer.sortingOrder = 11;
        auraRenderer.color = chargedDataCore ? new Color(0.58f, 1f, 0.86f, 0.28f) : new Color(0.82f, 0.95f, 1f, 0.22f);
    }

    private void UpdateAura(float pulse)
    {
        if ((!enableAura && !chargedDataCore) || auraRenderer == null || auraObject == null)
        {
            return;
        }

        float auraPulse = 0.65f + 0.35f * (0.5f + 0.5f * Mathf.Sin(Time.time * auraPulseSpeed));
        Color auraColor = chargedDataCore ? new Color(0.58f, 1f, 0.86f, 1f) : new Color(0.82f, 0.95f, 1f, 1f);
        auraRenderer.color = new Color(auraColor.r, auraColor.g, auraColor.b, 0.06f + auraPulse * (chargedDataCore ? 0.22f : 0.12f));
        auraObject.transform.localScale = Vector3.one * auraScale * (chargedDataCore ? 1.22f : 1f) * Mathf.Lerp(0.88f, 1.12f, auraPulse * pulse);
    }

    private void SpawnCollectFlash()
    {
        // Armamos un destello central y varias chispas radiales. Cada efecto se anima solo y se destruye al terminar.
        GameObject fx = new GameObject("ScoreCollectFx");
        fx.transform.position = transform.position;
        SpriteRenderer fxRenderer = fx.AddComponent<SpriteRenderer>();
        fxRenderer.sprite = CircleSpriteProvider.Get();
        fxRenderer.sortingOrder = 13;
        fxRenderer.color = new Color(1f, 1f, 1f, 0.9f);
        fx.transform.localScale = Vector3.one * 0.34f;
        Destroy(fx, 0.28f);
        fx.AddComponent<ScoreCollectFlash>().Configure(fxRenderer);

        int sparks = Mathf.Max(3, collectSparkCount);
        for (int i = 0; i < sparks; i++)
        {
            float angle = ((Mathf.PI * 2f) * i) / sparks + Random.Range(-0.16f, 0.16f);
            Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            GameObject spark = new GameObject($"ScoreCollectSpark_{i}");
            spark.transform.position = transform.position;
            SpriteRenderer sr = spark.AddComponent<SpriteRenderer>();
            sr.sprite = SquareSpriteProvider.Get();
            sr.color = new Color(0.92f, 0.98f, 1f, 1f);
            sr.sortingOrder = 13;
            spark.transform.localScale = new Vector3(0.12f, 0.05f, 1f);
            spark.AddComponent<ScoreCollectSparkFx>().Configure(sr, dir, Mathf.Max(0.15f, collectSparkDistance), Mathf.Max(0.08f, collectSparkLifetime));
            Destroy(spark, Mathf.Max(0.12f, collectSparkLifetime + 0.08f));
        }
    }
}
