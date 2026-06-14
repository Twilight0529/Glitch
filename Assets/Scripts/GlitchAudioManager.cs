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
    [SerializeField] private string menuMusicResourcePath = "Audio/Music/glitchstairs";
    [SerializeField] private float menuMusicVolume = 0.34f;
    [SerializeField] private string authoredMusicResourcePath = "Audio/Music/a_flawless_getaway_loop";
    [SerializeField] private float authoredMusicVolume = 0.48f;
    [SerializeField] private float musicFoundationVolume = 0.02f;
    [SerializeField] private float musicPulseVolume = 0.04f;
    [SerializeField] private float musicArpVolume = 0.015f;
    [SerializeField] private float musicPressureVolume = 0.07f;
    [SerializeField] private int sfxSourceCount = 12;

    private readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
    private AudioSource[] sfxSources;
    private AudioSource ambienceSource;
    private AudioSource tensionSource;
    private AudioSource menuMusicSource;
    private AudioSource authoredMusicSource;
    private AudioSource musicFoundationSource;
    private AudioSource musicPulseSource;
    private AudioSource musicArpSource;
    private AudioSource musicPressureSource;
    private int sfxCursor;
    private int sampleRate;
    private float musicIntensityBoost;
    private float musicBreachHoldTimer;
    private AudioContext audioContext = AudioContext.None;
    private PlayerController player;
    private EnemyController enemy;
    private GameManager gameManager;

    private enum AudioContext
    {
        None,
        MainMenu,
        Gameplay
    }

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
    public static void PlayGhostDash(Vector3 position) => Play("ghost_dash", 0.58f, 1f, position);
    public static void PlayParryStart(Vector3 position) => Play("parry_start", 0.52f, 1f, position);
    public static void PlayParrySuccess(Vector3 position) => Play("parry_success", 0.78f, 1f, position);
    public static void PlayProjectileReflect(Vector3 position) => Play("projectile_reflect", 0.48f, 1f, position);
    public static void PlayFirewallReady(Vector3 position) => Play("firewall_ready", 0.58f, 1f, position);
    public static void PlayFirewallBurst(Vector3 position)
    {
        BoostMusic(0.28f, 1.1f);
        Play("firewall_burst", 0.84f, 1f, position);
    }

    public static void PlayPlayerDeath(Vector3 position) => Play("player_death", 0.92f, 1f, position);
    public static void PlayUpgradeOpen() => Play("upgrade_open", 0.58f, 1f, Vector3.zero);
    public static void PlayUpgradeSelect() => Play("upgrade_select", 0.64f, 1f, Vector3.zero);
    public static void PlayEnemyParried(Vector3 position) => Play("enemy_parried", 0.72f, 1f, position);
    public static void PlayMenuHover() => Play("menu_hover", 0.28f, 1f, Vector3.zero);
    public static void PlayMenuConfirm() => Play("menu_confirm", 0.62f, 1f, Vector3.zero);
    public static void PlayMenuBack() => Play("menu_back", 0.46f, 1f, Vector3.zero);
    public static void PlayMenuToggle() => Play("menu_toggle", 0.44f, 1f, Vector3.zero);
    public static void PlayPauseOpen() => Play("pause_open", 0.54f, 1f, Vector3.zero);
    public static void PlayPauseClose() => Play("pause_close", 0.42f, 1f, Vector3.zero);
    public static void PlayRankingSubmit() => Play("ranking_submit", 0.58f, 1f, Vector3.zero);
    public static void PlayCountdownTick(int remaining) => Play("countdown_tick", 0.74f, Mathf.Lerp(1.12f, 0.92f, Mathf.Clamp01((remaining - 1f) / 3f)), Vector3.zero);
    public static void PlayCountdownGo() => Play("countdown_go", 0.88f, 1f, Vector3.zero);
    public static void PlayBreachWarning(Vector3 position)
    {
        BoostMusic(0.26f, 1.6f);
        Play("breach_warning", 0.78f, 1f, position);
    }

    public static void PlayBreachSweep(Vector3 position)
    {
        BoostMusic(0.55f, 2.4f);
        Play("breach_sweep", 0.88f, 1f, position);
    }

    public static void PlayBreachEnter(Vector3 position)
    {
        BoostMusic(0.42f, 1.4f);
        Play("breach_enter", 0.86f, 1f, position);
    }

    public static void PlayBreachTransition(Vector3 position)
    {
        BoostMusic(0.62f, 2.8f);
        Play("breach_transition", 0.9f, 1f, position);
    }

    public static void PlayBreachArrival(Vector3 position)
    {
        BoostMusic(0.34f, 1.6f);
        Play("breach_arrival", 0.76f, 1f, position);
    }

    public static void PlayBreachEnemyReentry(Vector3 position)
    {
        BoostMusic(0.48f, 1.8f);
        Play("breach_enemy_reentry", 0.82f, 1f, position);
    }

    public static void PlayBreachFail(Vector3 position)
    {
        BoostMusic(0.74f, 2.2f);
        Play("breach_fail", 0.94f, 1f, position);
    }

    public static void PlayLabSecurityScan(Vector3 position) => Play("lab_security_scan", 0.52f, 1f, position);
    public static void PlayLabGateLock(Vector3 position) => Play("lab_gate_lock", 0.66f, 1f, position);
    public static void PlayLabGateRelease(Vector3 position) => Play("lab_gate_release", 0.46f, 1f, position);
    public static void PlayStorageCraneStart(Vector3 position) => Play("storage_crane_start", 0.52f, 1f, position);
    public static void PlayStorageCargoImpact(Vector3 position) => Play("storage_cargo_impact", 0.62f, 1f, position);
    public static void PlayStorageCargoRemove(Vector3 position) => Play("storage_cargo_remove", 0.48f, 1f, position);
    public static void PlayRuptureRiftOpen(Vector3 position)
    {
        BoostMusic(0.12f, 0.9f);
        Play("rupture_rift_open", 0.58f, 1f, position);
    }
    public static void PlayRuptureFragmentMaterialize(Vector3 position) => Play("rupture_fragment_materialize", 0.54f, 1f, position);
    public static void PlayRuptureFragmentDissolve(Vector3 position) => Play("rupture_fragment_dissolve", 0.50f, 1f, position);

    public static void PlayEnemyState(EnemyController.AnomalyState state, Vector3 position)
    {
        BoostMusic(IsMajorState(state) ? 0.34f : 0.16f, IsMajorState(state) ? 2.2f : 0.8f);

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

    private static void BoostMusic(float amount, float holdSeconds)
    {
        Ensure();
        instance.musicIntensityBoost = Mathf.Max(instance.musicIntensityBoost, Mathf.Clamp01(amount));
        instance.musicBreachHoldTimer = Mathf.Max(instance.musicBreachHoldTimer, Mathf.Max(0f, holdSeconds));
    }

    private static bool IsMajorState(EnemyController.AnomalyState state)
    {
        return state == EnemyController.AnomalyState.Split ||
               state == EnemyController.AnomalyState.ExpansionShoot ||
               state == EnemyController.AnomalyState.SpeedSurge ||
               state == EnemyController.AnomalyState.Destroyer;
    }

    private static void Play(string clipName, float volume, float pitch, Vector3 position)
    {
        Ensure();
        instance.PlayInternal(clipName, volume, pitch, position);
    }

    public static void EnterMainMenu()
    {
        Ensure();
        instance.audioContext = AudioContext.MainMenu;
        instance.player = null;
        instance.enemy = null;
        instance.gameManager = null;
        instance.musicIntensityBoost = 0f;
        instance.musicBreachHoldTimer = 0f;
    }

    public static void EnterGameplay()
    {
        Ensure();
        instance.audioContext = AudioContext.Gameplay;
        instance.musicIntensityBoost = 0f;
        instance.musicBreachHoldTimer = 0f;
        instance.RefreshReferences();
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

        ambienceSource = CreateLoopSource("AmbientLoop", "ambient_loop");
        tensionSource = CreateLoopSource("ThreatLoop", "threat_loop");
        menuMusicSource = CreateResourceLoopSource("MenuMusic", menuMusicResourcePath);
        authoredMusicSource = CreateAuthoredMusicSource();
        musicFoundationSource = CreateLoopSource("MusicFoundation", "music_foundation");
        musicPulseSource = CreateLoopSource("MusicPulse", "music_pulse");
        musicArpSource = CreateLoopSource("MusicArp", "music_arp");
        musicPressureSource = CreateLoopSource("MusicPressure", "music_pressure");

        double loopStartTime = AudioSettings.dspTime + 0.05;
        PlayLoopSource(ambienceSource, loopStartTime);
        PlayLoopSource(tensionSource, loopStartTime);
        PlayLoopSource(menuMusicSource, loopStartTime);
        PlayLoopSource(authoredMusicSource, loopStartTime);
        PlayLoopSource(musicFoundationSource, loopStartTime);
        PlayLoopSource(musicPulseSource, loopStartTime);
        PlayLoopSource(musicArpSource, loopStartTime);
        PlayLoopSource(musicPressureSource, loopStartTime);
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

    private AudioSource CreateLoopSource(string sourceName, string clipName)
    {
        AudioSource source = CreateSource(sourceName);
        source.loop = true;
        source.clip = GetClip(clipName);
        source.volume = 0f;
        return source;
    }

    private AudioSource CreateAuthoredMusicSource()
    {
        return CreateResourceLoopSource("AuthoredMusic", authoredMusicResourcePath);
    }

    private AudioSource CreateResourceLoopSource(string sourceName, string resourcePath)
    {
        AudioClip clip = Resources.Load<AudioClip>(resourcePath);
        if (clip == null)
        {
            return null;
        }

        AudioSource source = CreateSource(sourceName);
        source.loop = true;
        source.clip = clip;
        source.volume = 0f;
        source.priority = 32;
        return source;
    }

    private static void PlayLoopSource(AudioSource source, double startTime)
    {
        if (source == null)
        {
            return;
        }

        source.timeSamples = 0;
        source.PlayScheduled(startTime);
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
        float dt = Time.unscaledDeltaTime;
        bool menuActive = audioContext == AudioContext.MainMenu;
        bool gameplayContext = audioContext == AudioContext.Gameplay;
        bool active = gameplayContext && gameManager != null && gameManager.IsRunActive && !gameManager.IsGameOver;
        bool gameplaySceneLive = gameplayContext && gameManager != null && !gameManager.IsGameOver;
        float threat = 0f;
        bool breachLure = false;
        if (active && player != null && enemy != null)
        {
            float distance = Vector2.Distance(player.GetPosition(), enemy.GetCurrentPosition());
            threat = 1f - Mathf.Clamp01((distance - 2f) / 10f);
            breachLure = enemy.IsBreachLureActive();
            if (breachLure)
            {
                threat *= 0.45f;
            }
        }

        musicIntensityBoost = Mathf.MoveTowards(musicIntensityBoost, 0f, dt * 0.42f);
        musicBreachHoldTimer = Mathf.Max(0f, musicBreachHoldTimer - dt);

        float progression = active && gameManager != null ? Mathf.Clamp01(gameManager.SurvivalTime / 150f) : 0f;
        float breachPressure = musicBreachHoldTimer > 0f || breachLure ? 0.34f : 0f;
        float musicIntensity = active
            ? Mathf.Clamp01(threat * 0.68f + progression * 0.24f + musicIntensityBoost + breachPressure)
            : 0f;

        float ambientTarget = active ? ambienceVolume : menuActive ? ambienceVolume * 0.42f : ambienceVolume * 0.18f;
        float tensionTarget = active ? tensionVolume * Mathf.SmoothStep(0f, 1f, threat) : 0f;
        float musicSetting = UserSettings.GetMusicVolume();
        MoveLoop(ambienceSource, ambientTarget * musicSetting, Mathf.Lerp(0.92f, 1.04f, threat), 0.25f, 0.15f);
        MoveLoop(tensionSource, tensionTarget * musicSetting, Mathf.Lerp(0.82f, 1.18f, threat), 0.45f, 0.3f);

        MoveLoop(menuMusicSource, (menuActive ? menuMusicVolume : 0f) * musicSetting, menuActive ? 1f : 0.96f, 0.28f, 0.1f);
        MoveLoop(authoredMusicSource, (gameplaySceneLive ? authoredMusicVolume : 0f) * musicSetting, 1f, 0.22f, 0.1f);
        MoveLoop(musicFoundationSource, (active ? musicFoundationVolume : 0f) * musicSetting, 1f, 0.28f, 0.12f);
        MoveLoop(musicPulseSource, (active ? musicPulseVolume * Smooth01(0.12f, 0.78f, musicIntensity) : 0f) * musicSetting, Mathf.Lerp(0.98f, 1.04f, musicIntensity), 0.36f, 0.16f);
        MoveLoop(musicArpSource, (active ? musicArpVolume * Smooth01(0.30f, 0.92f, musicIntensity) : 0f) * musicSetting, Mathf.Lerp(0.96f, 1.08f, musicIntensity), 0.34f, 0.22f);
        MoveLoop(musicPressureSource, (active ? musicPressureVolume * Smooth01(0.52f, 1f, musicIntensity) : 0f) * musicSetting, Mathf.Lerp(0.9f, 1.12f, musicIntensity), 0.5f, 0.28f);
    }

    private static void MoveLoop(AudioSource source, float targetVolume, float targetPitch, float volumeRate, float pitchRate)
    {
        if (source == null)
        {
            return;
        }

        float dt = Time.unscaledDeltaTime;
        source.volume = Mathf.MoveTowards(source.volume, Mathf.Max(0f, targetVolume), dt * Mathf.Max(0.01f, volumeRate));
        source.pitch = Mathf.MoveTowards(source.pitch, Mathf.Clamp(targetPitch, 0.45f, 1.75f), dt * Mathf.Max(0.01f, pitchRate));
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
        source.volume = Mathf.Clamp01(volume * sfxVolume * UserSettings.GetSfxVolume());
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
            case "music_foundation":
                return CreateClip(clipName, 8f, MusicFoundation);
            case "music_pulse":
                return CreateClip(clipName, 8f, MusicPulse);
            case "music_arp":
                return CreateClip(clipName, 8f, MusicArp);
            case "music_pressure":
                return CreateClip(clipName, 8f, MusicPressure);
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
            case "firewall_ready":
                return CreateClip(clipName, 0.48f, FirewallReady);
            case "firewall_burst":
                return CreateClip(clipName, 0.72f, FirewallBurst);
            case "shield_break":
                return CreateClip(clipName, 0.46f, ShieldBreak);
            case "ghost_dash":
                return CreateClip(clipName, 0.24f, GhostDash);
            case "player_death":
                return CreateClip(clipName, 0.9f, PlayerDeath);
            case "upgrade_open":
                return CreateClip(clipName, 0.54f, UpgradeOpen);
            case "upgrade_select":
                return CreateClip(clipName, 0.38f, UpgradeSelect);
            case "enemy_parried":
                return CreateClip(clipName, 0.34f, EnemyParried);
            case "menu_hover":
                return CreateClip(clipName, 0.10f, MenuHover);
            case "menu_confirm":
                return CreateClip(clipName, 0.32f, MenuConfirm);
            case "menu_back":
                return CreateClip(clipName, 0.26f, MenuBack);
            case "menu_toggle":
                return CreateClip(clipName, 0.22f, MenuToggle);
            case "pause_open":
                return CreateClip(clipName, 0.28f, PauseOpen);
            case "pause_close":
                return CreateClip(clipName, 0.20f, PauseClose);
            case "ranking_submit":
                return CreateClip(clipName, 0.42f, RankingSubmit);
            case "countdown_tick":
                return CreateClip(clipName, 0.36f, CountdownTick);
            case "countdown_go":
                return CreateClip(clipName, 0.62f, CountdownGo);
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
            case "lab_security_scan":
                return CreateClip(clipName, 0.82f, LabSecurityScan);
            case "lab_gate_lock":
                return CreateClip(clipName, 0.46f, LabGateLock);
            case "lab_gate_release":
                return CreateClip(clipName, 0.36f, LabGateRelease);
            case "storage_crane_start":
                return CreateClip(clipName, 0.78f, StorageCraneStart);
            case "storage_cargo_impact":
                return CreateClip(clipName, 0.44f, StorageCargoImpact);
            case "storage_cargo_remove":
                return CreateClip(clipName, 0.48f, StorageCargoRemove);
            case "rupture_rift_open":
                return CreateClip(clipName, 0.92f, RuptureRiftOpen);
            case "rupture_fragment_materialize":
                return CreateClip(clipName, 0.44f, RuptureFragmentMaterialize);
            case "rupture_fragment_dissolve":
                return CreateClip(clipName, 0.52f, RuptureFragmentDissolve);
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

    private static float MusicFoundation(float t, int i)
    {
        float kickSidechain = BeatDecay(t, 0.5f, 0.16f, 0f);
        float drone = Sine(55f, t) * 0.16f + Sine(82.5f, t) * 0.07f + Sine(110f, t) * 0.045f;
        float pulse = Sine(55f, t) * 0.18f * kickSidechain;
        float scan = Sine(220f, t) * 0.025f * Gate(t, 2f, 0.55f);
        return SoftClip(drone + pulse + scan);
    }

    private static float MusicPulse(float t, int i)
    {
        float kickEnv = BeatDecay(t, 0.5f, 0.075f, 0f);
        float kickPhase = Mathf.Repeat(t, 0.5f) / 0.5f;
        float kick = Sine(Mathf.Lerp(92f, 42f, Mathf.Clamp01(kickPhase * 3.2f)), t) * 0.46f * kickEnv;
        float snare = Noise(i, 101.4f) * 0.19f * BeatDecay(t, 1f, 0.045f, 0.5f);
        float hat = Noise(i, 103.8f) * 0.06f * Gate(t, 8f, 0.10f);
        float click = Square(1200f, t) * 0.035f * Gate(t, 4f, 0.08f);
        return SoftClip(kick + snare + hat + click);
    }

    private static float MusicArp(float t, int i)
    {
        float stepLength = 0.25f;
        int step = Mathf.FloorToInt(Mathf.Repeat(t, 8f) / stepLength);
        float local = Mathf.Repeat(t, stepLength);
        float attack = Mathf.Clamp01(local / 0.012f);
        float env = Mathf.SmoothStep(0f, 1f, attack) * Mathf.Exp(-local * 11f);
        float f = ArpFrequency(step);
        float tone = Sine(f, t) * 0.24f + Crush(Square(f * 0.5f, t), 6) * 0.09f;
        float alias = Sine(f * 2f, t) * 0.04f * Gate(t, 16f, 0.34f);
        return SoftClip((tone + alias + Noise(i, 108.2f) * 0.025f) * env);
    }

    private static float MusicPressure(float t, int i)
    {
        float rise = 0.4f + 0.6f * Mathf.Pow(0.5f + 0.5f * Sine(0.25f, t), 2f);
        float glitchHat = Noise(i, 112.9f) * 0.15f * Gate(t, 12f, 0.16f);
        float modem = Sine(680f + Mathf.Sin(t * 9f) * 180f, t) * 0.07f * Gate(t, 3f, 0.42f);
        float shimmer = Crush(Sine(1360f, t), 5) * 0.045f * Gate(t, 6f, 0.24f);
        return SoftClip((glitchHat + modem + shimmer) * rise);
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

    private static float FirewallReady(float t, int i)
    {
        float d = 0.48f;
        float e = Envelope(t, d, 0.006f, 0.16f);
        float stepA = Sine(520f, t) * Gate(t, 9f, 0.32f);
        float stepB = Sine(780f, t) * Gate(t + 0.04f, 9f, 0.32f);
        float shimmer = Crush(Sine(1560f + Mathf.Sin(t * 18f) * 120f, t), 7) * 0.10f;
        return SoftClip((stepA * 0.18f + stepB * 0.16f + shimmer + Noise(i, 18.5f) * 0.025f) * e);
    }

    private static float FirewallBurst(float t, int i)
    {
        float d = 0.72f;
        float hit = Mathf.Exp(-t * 12f);
        float e = Envelope(t, d, 0.004f, 0.24f);
        float punch = Sine(Mathf.Lerp(96f, 54f, Mathf.Clamp01(t / d)), t) * 0.56f * hit;
        float sweep = Sine(Mathf.Lerp(360f, 1340f, Mathf.Clamp01(t / d)), t) * 0.22f * e;
        float staticWall = Noise(i, 19.7f) * 0.24f * Mathf.Exp(-t * 5.2f) * Gate(t, 22f, 0.42f);
        float bitCrush = Crush(Sine(760f + Mathf.Sin(t * 30f) * 180f, t), 5) * 0.12f * e;
        return SoftClip(punch + sweep + staticWall + bitCrush);
    }

    private static float ShieldBreak(float t, int i)
    {
        float d = 0.46f;
        float crack = Mathf.Exp(-t * 16f);
        float tail = Envelope(t, d, 0.002f, 0.22f);
        return SoftClip(Sine(130f, t) * 0.34f * crack + Noise(i, 20.3f) * 0.28f * crack + Sine(780f, t) * 0.12f * tail);
    }

    private static float GhostDash(float t, int i)
    {
        float d = 0.24f;
        float e = Envelope(t, d, 0.003f, 0.09f);
        float sweep = Sine(Mathf.Lerp(1380f, 420f, Mathf.Clamp01(t / d)), t) * 0.24f;
        float air = Noise(i, 23.6f) * 0.16f * Mathf.Exp(-t * 13f);
        float phaseClick = Crush(Sine(760f + Mathf.Sin(t * 54f) * 220f, t), 6) * 0.10f * Gate(t, 18f, 0.28f);
        float body = Sine(96f, t) * 0.22f * Mathf.Exp(-t * 18f);
        return SoftClip((sweep + phaseClick) * e + air + body);
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

    private static float MenuHover(float t, int i)
    {
        float d = 0.10f;
        float e = Envelope(t, d, 0.002f, 0.045f);
        float f = Mathf.Lerp(820f, 1180f, t / d);
        return SoftClip((Sine(f, t) * 0.18f + Crush(Sine(f * 0.5f, t), 5) * 0.08f + Noise(i, 44.1f) * 0.025f) * e);
    }

    private static float MenuConfirm(float t, int i)
    {
        float d = 0.32f;
        float e = Envelope(t, d, 0.006f, 0.12f);
        float chirpA = Sine(Mathf.Lerp(380f, 740f, Mathf.Clamp01(t / d)), t) * 0.25f;
        float chirpB = Sine(Mathf.Lerp(760f, 1180f, Mathf.Clamp01((t - 0.06f) / d)), t) * 0.15f * Gate(t + 0.02f, 7f, 0.55f);
        return SoftClip((chirpA + chirpB + Noise(i, 45.7f) * 0.035f) * e);
    }

    private static float MenuBack(float t, int i)
    {
        float d = 0.26f;
        float e = Envelope(t, d, 0.004f, 0.11f);
        float f = Mathf.Lerp(680f, 260f, t / d);
        return SoftClip((Sine(f, t) * 0.22f + Square(f * 0.5f, t) * 0.06f + Noise(i, 47.3f) * 0.035f) * e);
    }

    private static float MenuToggle(float t, int i)
    {
        float d = 0.22f;
        float e = Envelope(t, d, 0.004f, 0.08f);
        float f = t < d * 0.5f ? 420f : 640f;
        return SoftClip((Sine(f, t) * 0.18f + Sine(f * 1.5f, t) * 0.08f + Noise(i, 48.6f) * 0.03f) * e);
    }

    private static float PauseOpen(float t, int i)
    {
        float d = 0.28f;
        float e = Envelope(t, d, 0.006f, 0.13f);
        float f = Mathf.Lerp(180f, 520f, Mathf.SmoothStep(0f, 1f, t / d));
        return SoftClip((Sine(f, t) * 0.25f + Crush(Square(f * 0.25f, t), 6) * 0.08f + Noise(i, 50.4f) * 0.045f) * e);
    }

    private static float PauseClose(float t, int i)
    {
        float d = 0.20f;
        float e = Envelope(t, d, 0.004f, 0.08f);
        float f = Mathf.Lerp(520f, 220f, t / d);
        return SoftClip((Sine(f, t) * 0.20f + Noise(i, 52.4f) * 0.025f) * e);
    }

    private static float RankingSubmit(float t, int i)
    {
        float d = 0.42f;
        float e = Envelope(t, d, 0.008f, 0.18f);
        float arp = Sine(440f, t) * Gate(t, 10f, 0.26f) + Sine(660f, t) * Gate(t + 0.04f, 10f, 0.26f) + Sine(880f, t) * Gate(t + 0.08f, 10f, 0.26f);
        return SoftClip((arp * 0.2f + Sine(110f, t) * 0.14f * Mathf.Exp(-t * 8f) + Noise(i, 53.8f) * 0.025f) * e);
    }

    private static float CountdownTick(float t, int i)
    {
        float d = 0.36f;
        float hit = Mathf.Exp(-t * 18f);
        float ring = Envelope(t, d, 0.002f, 0.17f);
        float tone = Sine(Mathf.Lerp(520f, 940f, Mathf.Clamp01(t / d)), t) * 0.20f * ring;
        float low = Sine(78f, t) * 0.34f * hit;
        float bit = Noise(i, 57.2f) * 0.08f * Gate(t, 18f, 0.32f) * ring;
        return SoftClip(low + tone + bit);
    }

    private static float CountdownGo(float t, int i)
    {
        float d = 0.62f;
        float e = Envelope(t, d, 0.006f, 0.22f);
        float punch = Sine(92f, t) * 0.50f * Mathf.Exp(-t * 8f);
        float rise = Sine(Mathf.Lerp(360f, 1480f, Mathf.Clamp01(t / d)), t) * 0.24f * e;
        float staticSnap = Noise(i, 58.9f) * 0.18f * Mathf.Exp(-t * 12f);
        return SoftClip(punch + rise + staticSnap);
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

    private static float LabSecurityScan(float t, int i)
    {
        float d = 0.82f;
        float e = Envelope(t, d, 0.018f, 0.18f);
        float scan = Sine(Mathf.Lerp(340f, 980f, Mathf.Clamp01(t / d)), t) * 0.18f;
        float alarm = (Sine(620f, t) * 0.22f + Sine(930f, t) * 0.08f) * Gate(t, 7.5f, 0.34f);
        float relay = Noise(i, 121.4f) * 0.045f * Gate(t, 24f, 0.18f);
        return SoftClip((scan + alarm + relay) * e);
    }

    private static float LabGateLock(float t, int i)
    {
        float d = 0.46f;
        float hit = Mathf.Exp(-t * 12f);
        float servo = Sine(Mathf.Lerp(180f, 74f, Mathf.Clamp01(t / d)), t) * 0.38f;
        float clampTone = Sine(520f, t) * 0.13f * Envelope(t, d, 0.003f, 0.18f);
        float metal = Noise(i, 124.8f) * 0.20f * hit;
        return SoftClip(servo * Envelope(t, d, 0.01f, 0.14f) + clampTone + metal);
    }

    private static float LabGateRelease(float t, int i)
    {
        float d = 0.36f;
        float e = Envelope(t, d, 0.006f, 0.12f);
        float servo = Sine(Mathf.Lerp(240f, 560f, Mathf.Clamp01(t / d)), t) * 0.20f;
        float vent = Noise(i, 127.3f) * 0.10f * Mathf.Exp(-t * 5f);
        return SoftClip((servo + vent + Sine(96f, t) * 0.12f * Mathf.Exp(-t * 8f)) * e);
    }

    private static float StorageCraneStart(float t, int i)
    {
        float d = 0.78f;
        float e = Envelope(t, d, 0.02f, 0.22f);
        float motor = Sine(Mathf.Lerp(58f, 116f, Mathf.Clamp01(t / d)), t) * 0.34f;
        float chain = Noise(i, 131.2f) * 0.13f * Gate(t, 18f, 0.28f);
        float beep = Sine(740f, t) * 0.10f * Gate(t, 3.6f, 0.18f);
        return SoftClip((motor + chain + beep) * e);
    }

    private static float StorageCargoImpact(float t, int i)
    {
        float d = 0.44f;
        float hit = Mathf.Exp(-t * 16f);
        float thud = Sine(Mathf.Lerp(82f, 42f, Mathf.Clamp01(t / d)), t) * 0.58f * hit;
        float metal = Noise(i, 134.7f) * 0.26f * hit + Square(166f, t) * 0.08f * hit;
        float tail = Sine(280f, t) * 0.08f * Envelope(t, d, 0.002f, 0.22f);
        return SoftClip(thud + metal + tail);
    }

    private static float StorageCargoRemove(float t, int i)
    {
        float d = 0.48f;
        float e = Envelope(t, d, 0.012f, 0.16f);
        float lift = Sine(Mathf.Lerp(120f, 340f, Mathf.Clamp01(t / d)), t) * 0.22f;
        float ratchet = Noise(i, 137.6f) * 0.10f * Gate(t, 20f, 0.22f);
        return SoftClip((lift + ratchet + Sine(650f, t) * 0.06f * Gate(t, 5f, 0.25f)) * e);
    }

    private static float RuptureRiftOpen(float t, int i)
    {
        float d = 0.92f;
        float e = Envelope(t, d, 0.02f, 0.26f);
        float tear = Sine(Mathf.Lerp(920f, 180f, Mathf.Clamp01(t / d)), t) * 0.20f * Gate(t, 13f, 0.48f);
        float voidBass = Sine(Mathf.Lerp(44f, 70f, Mathf.Clamp01(t / d)), t) * 0.36f;
        float staticRip = Noise(i, 142.5f) * Mathf.Lerp(0.05f, 0.22f, Mathf.Clamp01(t / d)) * Gate(t, 31f, 0.44f);
        return SoftClip((tear + voidBass + staticRip) * e);
    }

    private static float RuptureFragmentMaterialize(float t, int i)
    {
        float d = 0.44f;
        float e = Envelope(t, d, 0.004f, 0.14f);
        float snap = Sine(Mathf.Lerp(1480f, 520f, Mathf.Clamp01(t / d)), t) * 0.20f;
        float pixel = Crush(Sine(760f + Mathf.Sin(t * 38f) * 240f, t), 5) * 0.16f * Gate(t, 26f, 0.42f);
        return SoftClip((snap + pixel + Noise(i, 146.2f) * 0.11f) * e);
    }

    private static float RuptureFragmentDissolve(float t, int i)
    {
        float d = 0.52f;
        float e = Envelope(t, d, 0.008f, 0.18f);
        float crumble = Noise(i, 149.9f) * Mathf.Lerp(0.18f, 0.05f, Mathf.Clamp01(t / d)) * Gate(t, 34f, 0.62f);
        float fall = Sine(Mathf.Lerp(620f, 90f, Mathf.Clamp01(t / d)), t) * 0.18f;
        return SoftClip((crumble + fall + Crush(Sine(310f, t), 4) * 0.08f) * e);
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

    private static float BeatDecay(float t, float interval, float decay, float offset)
    {
        float phase = Mathf.Repeat(t - offset, Mathf.Max(0.001f, interval));
        return Mathf.Exp(-phase / Mathf.Max(0.001f, decay));
    }

    private static float Smooth01(float edge0, float edge1, float value)
    {
        if (edge1 <= edge0)
        {
            return value >= edge1 ? 1f : 0f;
        }

        return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(edge0, edge1, value));
    }

    private static float ArpFrequency(int step)
    {
        switch (step % 16)
        {
            case 0:
                return 220f;
            case 1:
                return 277.5f;
            case 2:
                return 330f;
            case 3:
                return 440f;
            case 4:
                return 247.5f;
            case 5:
                return 330f;
            case 6:
                return 415f;
            case 7:
                return 555f;
            case 8:
                return 196f;
            case 9:
                return 247.5f;
            case 10:
                return 330f;
            case 11:
                return 392f;
            case 12:
                return 207.5f;
            case 13:
                return 277.5f;
            case 14:
                return 370f;
            default:
                return 440f;
        }
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
