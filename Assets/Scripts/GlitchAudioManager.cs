using System;
using System.Collections.Generic;
using UnityEngine;

public class GlitchAudioManager : MonoBehaviour
{
    // Sintetiza una paleta sonora procedural para mantener una identidad glitch sin depender de assets externos.
    private const float TwoPi = Mathf.PI * 2f;
    private static GlitchAudioManager instance;

    [SerializeField] private float sfxVolume = 0.82f;
    [SerializeField] private float ambienceVolume = 0.18f;
    [SerializeField] private float tensionVolume = 0.22f;
    [SerializeField] private int sfxSourceCount = 12;

    private readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
    private AudioSource[] sfxSources;
    private AudioSource ambienceSource;
    private AudioSource tensionSource;
    private int sfxCursor;
    private int sampleRate;
    private PlayerController player;
    private EnemyController enemy;
    private GameManager gameManager;

    public static void Ensure()
    {
        if (instance != null)
        {
            return;
        }

        GameObject go = new GameObject("GlitchAudioManager");
        instance = go.AddComponent<GlitchAudioManager>();
        DontDestroyOnLoad(go);
    }

    public static void PlayScorePickup(Vector3 position) => Play("score_pickup", 0.42f, 1f, position);
    public static void PlayPowerupSpawn(Vector3 position) => Play("powerup_spawn", 0.36f, 1f, position);

    public static void PlayPowerupCollected(ArenaPowerupPickup.PickupKind kind, Vector3 position)
    {
        Play(kind == ArenaPowerupPickup.PickupKind.Shield ? "powerup_shield" : "powerup_speed", 0.72f, 1f, position);
    }

    public static void PlayShieldBreak(Vector3 position) => Play("shield_break", 0.72f, 1f, position);
    public static void PlayParryStart(Vector3 position) => Play("parry_start", 0.52f, 1f, position);
    public static void PlayParrySuccess(Vector3 position) => Play("parry_success", 0.78f, 1f, position);
    public static void PlayProjectileReflect(Vector3 position) => Play("projectile_reflect", 0.48f, 1f, position);
    public static void PlayPlayerDeath(Vector3 position) => Play("player_death", 0.92f, 1f, position);
    public static void PlayUpgradeOpen() => Play("upgrade_open", 0.58f, 1f, Vector3.zero);
    public static void PlayUpgradeSelect() => Play("upgrade_select", 0.64f, 1f, Vector3.zero);
    public static void PlayEnemyParried(Vector3 position) => Play("enemy_parried", 0.72f, 1f, position);
    public static void PlayBreachWarning(Vector3 position) => Play("breach_warning", 0.78f, 1f, position);
    public static void PlayBreachSweep(Vector3 position) => Play("breach_sweep", 0.88f, 1f, position);
    public static void PlayBreachEnter(Vector3 position) => Play("breach_enter", 0.86f, 1f, position);
    public static void PlayBreachTransition(Vector3 position) => Play("breach_transition", 0.9f, 1f, position);
    public static void PlayBreachArrival(Vector3 position) => Play("breach_arrival", 0.76f, 1f, position);
    public static void PlayBreachEnemyReentry(Vector3 position) => Play("breach_enemy_reentry", 0.82f, 1f, position);
    public static void PlayBreachFail(Vector3 position) => Play("breach_fail", 0.94f, 1f, position);

    public static void PlayEnemyState(EnemyController.AnomalyState state, Vector3 position)
    {
        switch (state)
        {
            case EnemyController.AnomalyState.Split:
                Play("state_split", 0.62f, 1f, position);
                break;
            case EnemyController.AnomalyState.ExpansionShoot:
                Play("state_expansion", 0.64f, 1f, position);
                break;
            case EnemyController.AnomalyState.SpeedSurge:
                Play("state_speed", 0.68f, 1f, position);
                break;
            case EnemyController.AnomalyState.WeaveHunter:
                Play("state_weave", 0.6f, 1f, position);
                break;
            case EnemyController.AnomalyState.Destroyer:
                Play("state_destroyer", 0.76f, 1f, position);
                break;
            default:
                Play("state_minor", 0.42f, 1f, position);
                break;
        }
    }

    private static void Play(string clipName, float volume, float pitch, Vector3 position)
    {
        Ensure();
        instance.PlayInternal(clipName, volume, pitch, position);
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
        sampleRate = Mathf.Max(22050, AudioSettings.outputSampleRate);
        CreateSources();
        StartAmbience();
    }

    private void Update()
    {
        AudioListener.volume = UserSettings.GetMasterVolume();
        RefreshReferences();
        UpdateReactiveAmbience();
    }

    private void CreateSources()
    {
        int count = Mathf.Max(4, sfxSourceCount);
        sfxSources = new AudioSource[count];
        for (int i = 0; i < count; i++)
        {
            sfxSources[i] = CreateSource($"Sfx_{i}");
        }

        ambienceSource = CreateSource("AmbientLoop");
        ambienceSource.loop = true;
        ambienceSource.clip = GetClip("ambient_loop");
        ambienceSource.volume = 0f;
        ambienceSource.Play();

        tensionSource = CreateSource("ThreatLoop");
        tensionSource.loop = true;
        tensionSource.clip = GetClip("threat_loop");
        tensionSource.volume = 0f;
        tensionSource.Play();
    }

    private AudioSource CreateSource(string sourceName)
    {
        GameObject sourceGo = new GameObject(sourceName);
        sourceGo.transform.SetParent(transform, false);
        AudioSource source = sourceGo.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        source.dopplerLevel = 0f;
        source.ignoreListenerPause = true;
        return source;
    }

    private void StartAmbience()
    {
        RefreshReferences();
        UpdateReactiveAmbience();
    }

    private void RefreshReferences()
    {
        if (player == null)
        {
            player = FindAnyObjectByType<PlayerController>();
        }
        if (enemy == null)
        {
            enemy = FindAnyObjectByType<EnemyController>();
        }
        if (gameManager == null)
        {
            gameManager = FindAnyObjectByType<GameManager>();
        }
    }

    private void UpdateReactiveAmbience()
    {
        bool active = gameManager != null && gameManager.IsRunActive && !gameManager.IsGameOver;
        float threat = 0f;
        if (active && player != null && enemy != null)
        {
            float distance = Vector2.Distance(player.GetPosition(), enemy.GetCurrentPosition());
            threat = 1f - Mathf.Clamp01((distance - 2f) / 10f);
            if (enemy.IsBreachLureActive())
            {
                threat *= 0.45f;
            }
        }

        float ambientTarget = active ? ambienceVolume : ambienceVolume * 0.35f;
        float tensionTarget = active ? tensionVolume * Mathf.SmoothStep(0f, 1f, threat) : 0f;
        if (ambienceSource != null)
        {
            ambienceSource.volume = Mathf.MoveTowards(ambienceSource.volume, ambientTarget, Time.unscaledDeltaTime * 0.25f);
            ambienceSource.pitch = Mathf.MoveTowards(ambienceSource.pitch, Mathf.Lerp(0.92f, 1.04f, threat), Time.unscaledDeltaTime * 0.15f);
        }
        if (tensionSource != null)
        {
            tensionSource.volume = Mathf.MoveTowards(tensionSource.volume, tensionTarget, Time.unscaledDeltaTime * 0.45f);
            tensionSource.pitch = Mathf.MoveTowards(tensionSource.pitch, Mathf.Lerp(0.82f, 1.18f, threat), Time.unscaledDeltaTime * 0.3f);
        }
    }

    private void PlayInternal(string clipName, float volume, float pitch, Vector3 position)
    {
        if (sfxSources == null || sfxSources.Length == 0)
        {
            CreateSources();
        }

        AudioClip clip = GetClip(clipName);
        if (clip == null)
        {
            return;
        }

        AudioSource source = sfxSources[sfxCursor % sfxSources.Length];
        sfxCursor++;
        source.Stop();
        source.clip = clip;
        source.pitch = Mathf.Clamp(pitch + UnityEngine.Random.Range(-0.025f, 0.025f), 0.45f, 1.75f);
        source.panStereo = ComputePan(position);
        source.volume = Mathf.Clamp01(volume * sfxVolume);
        source.Play();
    }

    private float ComputePan(Vector3 position)
    {
        Camera cam = Camera.main;
        if (cam == null || position == Vector3.zero)
        {
            return 0f;
        }

        Vector3 viewport = cam.WorldToViewportPoint(position);
        if (viewport.z < -0.01f)
        {
            return 0f;
        }

        return Mathf.Clamp((viewport.x - 0.5f) * 1.6f, -0.65f, 0.65f);
    }

    private AudioClip GetClip(string clipName)
    {
        if (clips.TryGetValue(clipName, out AudioClip clip) && clip != null)
        {
            return clip;
        }

        clip = BuildClip(clipName);
        clips[clipName] = clip;
        return clip;
    }

    private AudioClip BuildClip(string clipName)
    {
        switch (clipName)
        {
            case "ambient_loop":
                return CreateClip(clipName, 5.2f, AmbientLoop);
            case "threat_loop":
                return CreateClip(clipName, 2.8f, ThreatLoop);
            case "score_pickup":
                return CreateClip(clipName, 0.18f, ScorePickup);
            case "powerup_spawn":
                return CreateClip(clipName, 0.42f, PowerupSpawn);
            case "powerup_speed":
                return CreateClip(clipName, 0.48f, PowerupSpeed);
            case "powerup_shield":
                return CreateClip(clipName, 0.58f, PowerupShield);
            case "parry_start":
                return CreateClip(clipName, 0.24f, ParryStart);
            case "parry_success":
                return CreateClip(clipName, 0.42f, ParrySuccess);
            case "projectile_reflect":
                return CreateClip(clipName, 0.22f, ProjectileReflect);
            case "shield_break":
                return CreateClip(clipName, 0.46f, ShieldBreak);
            case "player_death":
                return CreateClip(clipName, 0.9f, PlayerDeath);
            case "upgrade_open":
                return CreateClip(clipName, 0.54f, UpgradeOpen);
            case "upgrade_select":
                return CreateClip(clipName, 0.38f, UpgradeSelect);
            case "enemy_parried":
                return CreateClip(clipName, 0.34f, EnemyParried);
            case "state_split":
                return CreateClip(clipName, 0.48f, StateSplit);
            case "state_expansion":
                return CreateClip(clipName, 0.5f, StateExpansion);
            case "state_speed":
                return CreateClip(clipName, 0.44f, StateSpeed);
            case "state_weave":
                return CreateClip(clipName, 0.48f, StateWeave);
            case "state_destroyer":
                return CreateClip(clipName, 0.62f, StateDestroyer);
            case "state_minor":
                return CreateClip(clipName, 0.25f, StateMinor);
            case "breach_warning":
                return CreateClip(clipName, 1.2f, BreachWarning);
            case "breach_sweep":
                return CreateClip(clipName, 1.35f, BreachSweep);
            case "breach_enter":
                return CreateClip(clipName, 0.72f, BreachEnter);
            case "breach_transition":
                return CreateClip(clipName, 1.45f, BreachTransition);
            case "breach_arrival":
                return CreateClip(clipName, 1.45f, BreachArrival);
            case "breach_enemy_reentry":
                return CreateClip(clipName, 0.82f, BreachEnemyReentry);
            case "breach_fail":
                return CreateClip(clipName, 0.9f, BreachFail);
            default:
                return CreateClip(clipName, 0.2f, StateMinor);
        }
    }

    private AudioClip CreateClip(string clipName, float duration, Func<float, int, float> generator)
    {
        int samples = Mathf.Max(1, Mathf.CeilToInt(sampleRate * Mathf.Max(0.02f, duration)));
        float[] data = new float[samples];
        for (int i = 0; i < samples; i++)
        {
            float t = i / (float)sampleRate;
            data[i] = Mathf.Clamp(generator(t, i), -0.95f, 0.95f);
        }

        AudioClip clip = AudioClip.Create(clipName, samples, 1, sampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    private static float AmbientLoop(float t, int i)
    {
        float hum = Sine(48f, t) * 0.15f + Sine(96.5f, t) * 0.06f + Sine(143f, t) * 0.025f;
        float staticBed = Noise(i, 3.1f) * 0.018f;
        float scan = Sine(310f + Mathf.Sin(t * 0.7f) * 16f, t) * (0.012f + 0.012f * Mathf.PerlinNoise(t * 0.4f, 2.2f));
        return SoftClip(hum + staticBed + scan);
    }

    private static float ThreatLoop(float t, int i)
    {
        float pulse = Mathf.Pow(0.5f + 0.5f * Sine(1.6f, t), 3f);
        float bass = Sine(41f, t) * 0.28f * pulse;
        float grit = Square(82f, t) * 0.05f * pulse + Noise(i, 7.7f) * 0.03f * pulse;
        float warning = Sine(520f + 80f * pulse, t) * 0.035f * Gate(t, 7f, 0.28f);
        return SoftClip(bass + grit + warning);
    }

    private static float ScorePickup(float t, int i)
    {
        float d = 0.18f;
        float e = Envelope(t, d, 0.006f, 0.08f);
        float f = Mathf.Lerp(880f, 1620f, t / d);
        return (Sine(f, t) * 0.45f + Square(f * 1.5f, t) * 0.08f + Noise(i, 1.2f) * 0.04f) * e;
    }

    private static float PowerupSpawn(float t, int i)
    {
        float d = 0.42f;
        float e = Envelope(t, d, 0.02f, 0.18f);
        float f = Mathf.Lerp(260f, 720f, Mathf.SmoothStep(0f, 1f, t / d));
        return SoftClip((Sine(f, t) * 0.28f + Sine(f * 2.01f, t) * 0.12f + Noise(i, 4.4f) * 0.035f) * e);
    }

    private static float PowerupSpeed(float t, int i)
    {
        float d = 0.48f;
        float e = Envelope(t, d, 0.01f, 0.14f);
        float f = Mathf.Lerp(340f, 1580f, Mathf.Pow(t / d, 0.7f));
        float bit = Crush(Sine(f * 1.02f, t), 9) * 0.18f * Gate(t, 24f, 0.35f);
        return SoftClip((Sine(f, t) * 0.32f + bit + Noise(i, 9.1f) * 0.03f) * e);
    }

    private static float PowerupShield(float t, int i)
    {
        float d = 0.58f;
        float e = Envelope(t, d, 0.02f, 0.22f);
        float chord = Sine(220f, t) * 0.24f + Sine(330f, t) * 0.18f + Sine(550f, t) * 0.08f;
        float shimmer = Sine(980f + 45f * Mathf.Sin(t * 18f), t) * 0.08f * Gate(t, 18f, 0.5f);
        return SoftClip((chord + shimmer + Noise(i, 11.2f) * 0.018f) * e);
    }

    private static float ParryStart(float t, int i)
    {
        float d = 0.24f;
        float e = Envelope(t, d, 0.006f, 0.08f);
        float f = Mathf.Lerp(690f, 420f, t / d);
        return SoftClip((Sine(f, t) * 0.32f + Square(f * 0.5f, t) * 0.12f + Noise(i, 5.5f) * 0.06f) * e);
    }

    private static float ParrySuccess(float t, int i)
    {
        float d = 0.42f;
        float hit = Mathf.Exp(-t * 18f);
        float ring = Envelope(t, d, 0.002f, 0.2f);
        float zap = Sine(Mathf.Lerp(1400f, 520f, t / d), t) * 0.24f * ring;
        float thump = Sine(76f, t) * 0.55f * hit;
        return SoftClip(thump + zap + Noise(i, 14.7f) * 0.22f * hit);
    }

    private static float ProjectileReflect(float t, int i)
    {
        float d = 0.22f;
        float e = Envelope(t, d, 0.003f, 0.07f);
        float f = Mathf.Lerp(1200f, 2100f, t / d);
        return SoftClip((Sine(f, t) * 0.24f + Crush(Sine(f * 0.5f, t), 6) * 0.12f + Noise(i, 17.2f) * 0.08f) * e);
    }

    private static float ShieldBreak(float t, int i)
    {
        float d = 0.46f;
        float crack = Mathf.Exp(-t * 16f);
        float tail = Envelope(t, d, 0.002f, 0.22f);
        return SoftClip(Sine(130f, t) * 0.34f * crack + Noise(i, 20.3f) * 0.28f * crack + Sine(780f, t) * 0.12f * tail);
    }

    private static float PlayerDeath(float t, int i)
    {
        float d = 0.9f;
        float rise = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / 0.22f));
        float fall = 1f - Mathf.SmoothStep(0.42f, 1f, t / d);
        float f = Mathf.Lerp(96f, 34f, Mathf.Clamp01(t / d));
        float core = Sine(f, t) * 0.56f * rise * fall;
        float staticBurst = Noise(i, 31.4f) * 0.34f * Mathf.Exp(-Mathf.Max(0f, t - 0.25f) * 4.8f);
        float alarm = Sine(510f + Mathf.Sin(t * 38f) * 90f, t) * 0.12f * fall;
        return SoftClip(core + staticBurst + alarm);
    }

    private static float UpgradeOpen(float t, int i)
    {
        float d = 0.54f;
        float e = Envelope(t, d, 0.03f, 0.18f);
        float f = Mathf.Lerp(180f, 620f, Mathf.SmoothStep(0f, 1f, t / d));
        return SoftClip((Sine(f, t) * 0.23f + Sine(f * 1.5f, t) * 0.12f + Noise(i, 38.2f) * 0.025f) * e);
    }

    private static float UpgradeSelect(float t, int i)
    {
        float d = 0.38f;
        float e = Envelope(t, d, 0.008f, 0.15f);
        float arp = Sine(520f, t) * Gate(t, 12f, 0.22f) + Sine(780f, t) * Gate(t + 0.03f, 12f, 0.22f);
        return SoftClip((arp * 0.22f + Sine(120f, t) * 0.2f * Mathf.Exp(-t * 9f)) * e);
    }

    private static float EnemyParried(float t, int i)
    {
        float d = 0.34f;
        float e = Envelope(t, d, 0.001f, 0.14f);
        return SoftClip((Sine(Mathf.Lerp(180f, 70f, t / d), t) * 0.45f + Noise(i, 41.1f) * 0.18f + Square(95f, t) * 0.12f) * e);
    }

    private static float StateMinor(float t, int i)
    {
        float d = 0.25f;
        return (Sine(420f + Mathf.Sin(t * 35f) * 40f, t) * 0.18f + Noise(i, 43f) * 0.04f) * Envelope(t, d, 0.01f, 0.08f);
    }

    private static float StateSplit(float t, int i)
    {
        float d = 0.48f;
        float e = Envelope(t, d, 0.01f, 0.14f);
        float left = Sine(360f, t) * Gate(t, 7f, 0.34f);
        float right = Sine(620f, t + 0.035f) * Gate(t + 0.055f, 7f, 0.34f);
        return SoftClip((left + right + Noise(i, 49.2f) * 0.08f) * 0.28f * e);
    }

    private static float StateExpansion(float t, int i)
    {
        float d = 0.5f;
        float e = Envelope(t, d, 0.018f, 0.16f);
        float f = Mathf.Lerp(420f, 1180f, Mathf.Clamp01(t / d));
        return SoftClip((Sine(f, t) * 0.22f + Square(f * 0.5f, t) * 0.1f + Noise(i, 51.8f) * 0.05f) * e);
    }

    private static float StateSpeed(float t, int i)
    {
        float d = 0.44f;
        float e = Envelope(t, d, 0.005f, 0.12f);
        float f = Mathf.Lerp(520f, 1760f, Mathf.Pow(t / d, 0.55f));
        return SoftClip((Sine(f, t) * 0.22f + Crush(Square(f * 0.25f, t), 4) * 0.18f + Noise(i, 56.5f) * 0.08f) * e);
    }

    private static float StateWeave(float t, int i)
    {
        float d = 0.48f;
        float e = Envelope(t, d, 0.02f, 0.18f);
        float f = 380f + Mathf.Sin(t * 25f) * 150f;
        return SoftClip((Sine(f, t) * 0.22f + Sine(f * 1.52f, t) * 0.11f + Noise(i, 59.9f) * 0.04f) * e);
    }

    private static float StateDestroyer(float t, int i)
    {
        float d = 0.62f;
        float hit = Mathf.Exp(-t * 7f);
        float metal = Noise(i, 61.6f) * 0.16f * Envelope(t, d, 0.002f, 0.35f);
        return SoftClip(Sine(52f, t) * 0.62f * hit + Square(104f, t) * 0.15f * hit + metal);
    }

    private static float BreachWarning(float t, int i)
    {
        float d = 1.2f;
        float e = Envelope(t, d, 0.04f, 0.2f);
        float alarmGate = Gate(t, 3.2f, 0.42f);
        float alarm = Sine(188f, t) * 0.35f * alarmGate + Sine(376f, t) * 0.12f * alarmGate;
        float staticTick = Noise(i, 70.7f) * 0.06f * Gate(t, 18f, 0.18f);
        return SoftClip((alarm + staticTick) * e);
    }

    private static float BreachSweep(float t, int i)
    {
        float d = 1.35f;
        float e = Envelope(t, d, 0.02f, 0.28f);
        float sweep = Sine(Mathf.Lerp(90f, 36f, t / d), t) * 0.46f;
        float tearing = Noise(i, 74.1f) * Mathf.Lerp(0.07f, 0.28f, t / d) * Gate(t, 28f, 0.38f);
        float edge = Sine(Mathf.Lerp(900f, 260f, t / d), t) * 0.12f * Gate(t, 11f, 0.5f);
        return SoftClip((sweep + tearing + edge) * e);
    }

    private static float BreachEnter(float t, int i)
    {
        float d = 0.72f;
        float e = Envelope(t, d, 0.005f, 0.26f);
        float suck = Sine(Mathf.Lerp(520f, 70f, t / d), t) * 0.34f;
        float pop = Sine(65f, t) * 0.42f * Mathf.Exp(-t * 9f);
        return SoftClip((suck + pop + Noise(i, 78.5f) * 0.13f * Gate(t, 23f, 0.4f)) * e);
    }

    private static float BreachTransition(float t, int i)
    {
        float d = 1.45f;
        float center = Mathf.Sin(Mathf.Clamp01(t / d) * Mathf.PI);
        float bass = Sine(44f, t) * 0.5f * center;
        float storm = Noise(i, 83.8f) * 0.25f * center * Gate(t, 33f, 0.55f);
        float scan = Sine(720f + Mathf.Sin(t * 20f) * 220f, t) * 0.12f * center;
        return SoftClip(bass + storm + scan);
    }

    private static float BreachArrival(float t, int i)
    {
        float d = 1.45f;
        float e = Envelope(t, d, 0.04f, 0.42f);
        float shimmer = Sine(Mathf.Lerp(260f, 780f, Mathf.SmoothStep(0f, 1f, t / d)), t) * 0.22f;
        float phase = Sine(1040f + Mathf.Sin(t * 16f) * 120f, t) * 0.08f * Gate(t, 16f, 0.5f);
        return SoftClip((shimmer + phase + Noise(i, 88.2f) * 0.035f) * e);
    }

    private static float BreachEnemyReentry(float t, int i)
    {
        float d = 0.82f;
        float e = Envelope(t, d, 0.02f, 0.2f);
        float growl = Sine(Mathf.Lerp(64f, 118f, t / d), t) * 0.48f;
        float serrated = Crush(Square(236f, t), 6) * 0.14f * Gate(t, 14f, 0.38f);
        return SoftClip((growl + serrated + Noise(i, 91.7f) * 0.08f) * e);
    }

    private static float BreachFail(float t, int i)
    {
        float d = 0.9f;
        float e = Envelope(t, d, 0.005f, 0.24f);
        float collapse = Sine(Mathf.Lerp(130f, 28f, t / d), t) * 0.58f;
        float dissolve = Noise(i, 96.6f) * Mathf.Lerp(0.16f, 0.34f, t / d);
        return SoftClip((collapse + dissolve) * e);
    }

    private static float Envelope(float t, float duration, float attack, float release)
    {
        float a = attack <= 0f ? 1f : Mathf.Clamp01(t / attack);
        float r = release <= 0f ? 1f : Mathf.Clamp01((duration - t) / release);
        return Mathf.SmoothStep(0f, 1f, a) * Mathf.SmoothStep(0f, 1f, r);
    }

    private static float Sine(float frequency, float t)
    {
        return Mathf.Sin(TwoPi * frequency * t);
    }

    private static float Square(float frequency, float t)
    {
        return Sine(frequency, t) >= 0f ? 1f : -1f;
    }

    private static float Noise(int sample, float seed)
    {
        float n = Mathf.Sin(sample * 12.9898f + seed * 78.233f) * 43758.5453f;
        return (n - Mathf.Floor(n)) * 2f - 1f;
    }

    private static float Gate(float t, float rate, float duty)
    {
        return Mathf.Repeat(t * rate, 1f) <= Mathf.Clamp01(duty) ? 1f : 0f;
    }

    private static float Crush(float value, int steps)
    {
        float s = Mathf.Max(2, steps);
        return Mathf.Round(value * s) / s;
    }

    private static float SoftClip(float value)
    {
        return value / (1f + Mathf.Abs(value));
    }
}
