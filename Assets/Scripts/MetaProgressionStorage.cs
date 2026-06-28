using System.Collections.Generic;
using UnityEngine;

// Caja fuerte del metajuego: guarda Datos, estadísticas, módulos y la apariencia elegida entre una run y otra.
public static class MetaProgressionStorage
{
    // Guarda progresion persistente entre runs: moneda obtenida por score y desbloqueos comprados.
    public enum SkinPattern
    {
        Core,
        Split,
        Circuit,
        Hazard,
        Firewall,
        Fracture,
        Signal,
        Prism,
        Orbit,
        Containment
    }

    public enum TrailStyle
    {
        Soft,
        Echo,
        Pixels,
        Sparks,
        Pulse
    }

    public struct UnlockDefinition
    {
        public string id;
        public string section;
        public string title;
        public string description;
        public int cost;
        public bool skin;
        public Color bodyColor;
        public Color accentColor;
        public Color trailColor;
        public SkinPattern skinPattern;
        public TrailStyle trailStyle;
    }

    public struct RunReward
    {
        public int score;
        public float survivalTime;
        public string levelLabel;
        public int dataEarned;
        public int contractBonusData;
        public int totalData;
        public bool newBestScore;
        public bool newBestTime;
        public int bestScoreForLevel;
        public float bestTimeForLevel;
        public int totalRuns;
        public int totalScore;
        public float totalSurvivalTime;
        public string performanceGrade;
    }

    public struct CareerStats
    {
        public int totalRuns;
        public int totalScore;
        public float totalSurvivalTime;
        public int bestScore;
        public float bestSurvivalTime;
    }

    public const string UnlockHazardResistance = "upgrade_hazard_resistance";
    public const string UnlockDisplacementStabilizer = "upgrade_displacement_stabilizer";
    public const string UnlockHazardFirewallCharge = "upgrade_hazard_firewall_charge";
    public const string UnlockFirewallBurstStun = "upgrade_firewall_burst_stun";
    public const string UnlockExtraUpgradeChoice = "upgrade_extra_choice";
    public const string UnlockVectorCore = "upgrade_vector_core";
    public const string UnlockEmergencyShield = "upgrade_emergency_shield";
    public const string UnlockParryCapacitor = "upgrade_parry_capacitor";
    public const string SectionRunUpgrades = "MEJORAS";
    public const string SectionSkins = "APARIENCIAS";
    public const string SkinDefault = "skin_default";
    public const string SkinRupturePink = "skin_rupture_pink";
    public const string SkinLabMint = "skin_lab_mint";
    public const string SkinStorageGold = "skin_storage_gold";
    public const string SkinFirewallWhite = "skin_firewall_white";
    public const string SkinBreachBlack = "skin_breach_black";
    public const string SkinSignalRed = "skin_signal_red";
    public const string SkinOverdrivePrism = "skin_overdrive_prism";
    public const string SkinVoidViolet = "skin_void_violet";
    public const string SkinContainmentGold = "skin_containment_gold";

    private const string DataKey = "Glitch_Meta_Data";
    private const string LastScoreKey = "Glitch_Meta_LastScore";
    private const string LastTimeKey = "Glitch_Meta_LastTime";
    private const string LastLevelKey = "Glitch_Meta_LastLevel";
    private const string LastEarnedKey = "Glitch_Meta_LastEarned";
    private const string LastContractBonusKey = "Glitch_Meta_LastContractBonus";
    private const string LastNewBestScoreKey = "Glitch_Meta_LastNewBestScore";
    private const string LastNewBestTimeKey = "Glitch_Meta_LastNewBestTime";
    private const string LastBestScoreKey = "Glitch_Meta_LastBestScore";
    private const string LastBestTimeKey = "Glitch_Meta_LastBestTime";
    private const string LastTotalRunsKey = "Glitch_Meta_LastTotalRuns";
    private const string LastTotalScoreKey = "Glitch_Meta_LastTotalScore";
    private const string LastTotalTimeKey = "Glitch_Meta_LastTotalTime";
    private const string LastGradeKey = "Glitch_Meta_LastGrade";
    private const string TotalRunsKey = "Glitch_Meta_TotalRuns";
    private const string TotalScoreKey = "Glitch_Meta_TotalScore";
    private const string TotalTimeKey = "Glitch_Meta_TotalTime";
    private const string GlobalBestScoreKey = "Glitch_Meta_GlobalBestScore";
    private const string GlobalBestTimeKey = "Glitch_Meta_GlobalBestTime";
    private const string SelectedSkinKey = "Glitch_Meta_SelectedSkin";

    private static readonly string[] knownLevelLabels = { "Lab", "Storage", "Rupture", "Core", "Archive", "Unknown" };

    private static readonly UnlockDefinition[] definitions =
    {
        new UnlockDefinition
        {
            id = UnlockHazardResistance,
            section = SectionRunUpgrades,
            title = "Filtro Ambiental",
            description = "Nueva mejora contra slows de eventos.",
            cost = 45
        },
        new UnlockDefinition
        {
            id = UnlockDisplacementStabilizer,
            section = SectionRunUpgrades,
            title = "Anclaje Inercial",
            description = "Nueva mejora contra empujes de arena.",
            cost = 45
        },
        new UnlockDefinition
        {
            id = UnlockHazardFirewallCharge,
            section = SectionRunUpgrades,
            title = "Reciclaje de Riesgo",
            description = "Nueva mejora: peligro carga Firewall.",
            cost = 60
        },
        new UnlockDefinition
        {
            id = UnlockFirewallBurstStun,
            section = SectionRunUpgrades,
            title = "Corte de Senal",
            description = "Nueva mejora para extender stun del Burst.",
            cost = 70
        },
        new UnlockDefinition
        {
            id = UnlockExtraUpgradeChoice,
            section = SectionRunUpgrades,
            title = "Selector Expandido",
            description = "Una opcion extra en cada seleccion.",
            cost = 95
        },
        new UnlockDefinition
        {
            id = UnlockVectorCore,
            section = SectionRunUpgrades,
            title = "Nucleo Vectorial",
            description = "Nueva mejora hibrida de velocidad y estabilidad.",
            cost = 110
        },
        new UnlockDefinition
        {
            id = UnlockEmergencyShield,
            section = SectionRunUpgrades,
            title = "Escudo de Emergencia",
            description = "Nueva mejora defensiva con escudo inmediato.",
            cost = 125
        },
        new UnlockDefinition
        {
            id = UnlockParryCapacitor,
            section = SectionRunUpgrades,
            title = "Capacitor Parry",
            description = "Nueva mejora avanzada para cargar Firewall.",
            cost = 135
        },
        new UnlockDefinition
        {
            id = SkinDefault,
            section = SectionSkins,
            title = "Cian Base",
            description = "Nucleo limpio con marca central y estela suave.",
            cost = 0,
            skin = true,
            bodyColor = new Color(0.28f, 0.88f, 1f, 1f),
            accentColor = new Color(0.86f, 0.98f, 1f, 1f),
            trailColor = new Color(0.28f, 0.88f, 1f, 0.88f),
            skinPattern = SkinPattern.Core,
            trailStyle = TrailStyle.Soft
        },
        new UnlockDefinition
        {
            id = SkinRupturePink,
            section = SectionSkins,
            title = "Rupture Rosa",
            description = "Traje dividido por una falla luminosa con estela de eco.",
            cost = 35,
            skin = true,
            bodyColor = new Color(1f, 0.42f, 0.78f, 1f),
            accentColor = new Color(0.48f, 0.12f, 0.62f, 1f),
            trailColor = new Color(1f, 0.42f, 0.78f, 0.88f),
            skinPattern = SkinPattern.Split,
            trailStyle = TrailStyle.Echo
        },
        new UnlockDefinition
        {
            id = SkinLabMint,
            section = SectionSkins,
            title = "Lab Menta",
            description = "Circuito de laboratorio animado sobre el cuerpo.",
            cost = 40,
            skin = true,
            bodyColor = new Color(0.48f, 1f, 0.74f, 1f),
            accentColor = new Color(0.06f, 0.42f, 0.38f, 1f),
            trailColor = new Color(0.48f, 1f, 0.74f, 0.88f),
            skinPattern = SkinPattern.Circuit,
            trailStyle = TrailStyle.Pulse
        },
        new UnlockDefinition
        {
            id = SkinStorageGold,
            section = SectionSkins,
            title = "Storage Dorado",
            description = "Franjas industriales de riesgo y particulas pixeladas.",
            cost = 40,
            skin = true,
            bodyColor = new Color(1f, 0.72f, 0.34f, 1f),
            accentColor = new Color(0.22f, 0.14f, 0.05f, 1f),
            trailColor = new Color(1f, 0.72f, 0.34f, 0.88f),
            skinPattern = SkinPattern.Hazard,
            trailStyle = TrailStyle.Pixels
        },
        new UnlockDefinition
        {
            id = SkinFirewallWhite,
            section = SectionSkins,
            title = "Firewall Blanco",
            description = "Armadura perimetral que pulsa al acelerar.",
            cost = 55,
            skin = true,
            bodyColor = new Color(0.92f, 0.98f, 1f, 1f),
            accentColor = new Color(0.18f, 0.76f, 1f, 1f),
            trailColor = new Color(0.92f, 0.98f, 1f, 0.90f),
            skinPattern = SkinPattern.Firewall,
            trailStyle = TrailStyle.Pulse
        },
        new UnlockDefinition
        {
            id = SkinBreachBlack,
            section = SectionSkins,
            title = "Breach Negro",
            description = "Cuerpo fracturado por cortes magenta inestables.",
            cost = 75,
            skin = true,
            bodyColor = new Color(0.08f, 0.06f, 0.11f, 1f),
            accentColor = new Color(1f, 0.38f, 0.78f, 1f),
            trailColor = new Color(0.08f, 0.06f, 0.11f, 0.92f),
            skinPattern = SkinPattern.Fracture,
            trailStyle = TrailStyle.Sparks
        },
        new UnlockDefinition
        {
            id = SkinSignalRed,
            section = SectionSkins,
            title = "Senal Roja",
            description = "Visor de alerta con linea de escaneo movil.",
            cost = 85,
            skin = true,
            bodyColor = new Color(1f, 0.26f, 0.34f, 1f),
            accentColor = new Color(1f, 0.88f, 0.52f, 1f),
            trailColor = new Color(1f, 0.26f, 0.34f, 0.88f),
            skinPattern = SkinPattern.Signal,
            trailStyle = TrailStyle.Sparks
        },
        new UnlockDefinition
        {
            id = SkinOverdrivePrism,
            section = SectionSkins,
            title = "Prisma Overdrive",
            description = "Paneles cromaticos que cambian de fase durante la run.",
            cost = 105,
            skin = true,
            bodyColor = new Color(0.78f, 0.56f, 1f, 1f),
            accentColor = new Color(0.42f, 1f, 0.92f, 1f),
            trailColor = new Color(0.78f, 0.56f, 1f, 0.90f),
            skinPattern = SkinPattern.Prism,
            trailStyle = TrailStyle.Echo
        },
        new UnlockDefinition
        {
            id = SkinVoidViolet,
            section = SectionSkins,
            title = "Vacio Violeta",
            description = "Nodos orbitales giran alrededor de un nucleo profundo.",
            cost = 120,
            skin = true,
            bodyColor = new Color(0.30f, 0.20f, 0.58f, 1f),
            accentColor = new Color(0.64f, 0.92f, 1f, 1f),
            trailColor = new Color(0.30f, 0.20f, 0.58f, 0.88f),
            skinPattern = SkinPattern.Orbit,
            trailStyle = TrailStyle.Echo
        },
        new UnlockDefinition
        {
            id = SkinContainmentGold,
            section = SectionSkins,
            title = "Contencion Oro",
            description = "Traje premium con cierres de contencion y nucleo activo.",
            cost = 150,
            skin = true,
            bodyColor = new Color(1f, 0.86f, 0.42f, 1f),
            accentColor = new Color(1f, 0.98f, 0.76f, 1f),
            trailColor = new Color(1f, 0.86f, 0.42f, 0.92f),
            skinPattern = SkinPattern.Containment,
            trailStyle = TrailStyle.Pulse
        }
    };

    public static IReadOnlyList<UnlockDefinition> UnlockDefinitions => definitions;
    public static int CurrentData => Mathf.Max(0, PlayerPrefs.GetInt(DataKey, 0));

    public static RunReward LastRunReward => new RunReward
    {
        score = Mathf.Max(0, PlayerPrefs.GetInt(LastScoreKey, 0)),
        survivalTime = Mathf.Max(0f, PlayerPrefs.GetFloat(LastTimeKey, 0f)),
        levelLabel = PlayerPrefs.GetString(LastLevelKey, "Unknown"),
        dataEarned = Mathf.Max(0, PlayerPrefs.GetInt(LastEarnedKey, 0)),
        contractBonusData = Mathf.Max(0, PlayerPrefs.GetInt(LastContractBonusKey, 0)),
        totalData = CurrentData,
        newBestScore = PlayerPrefs.GetInt(LastNewBestScoreKey, 0) == 1,
        newBestTime = PlayerPrefs.GetInt(LastNewBestTimeKey, 0) == 1,
        bestScoreForLevel = Mathf.Max(0, PlayerPrefs.GetInt(LastBestScoreKey, 0)),
        bestTimeForLevel = Mathf.Max(0f, PlayerPrefs.GetFloat(LastBestTimeKey, 0f)),
        totalRuns = Mathf.Max(0, PlayerPrefs.GetInt(LastTotalRunsKey, 0)),
        totalScore = Mathf.Max(0, PlayerPrefs.GetInt(LastTotalScoreKey, 0)),
        totalSurvivalTime = Mathf.Max(0f, PlayerPrefs.GetFloat(LastTotalTimeKey, 0f)),
        performanceGrade = PlayerPrefs.GetString(LastGradeKey, "D")
    };

    public static CareerStats Stats => new CareerStats
    {
        totalRuns = Mathf.Max(0, PlayerPrefs.GetInt(TotalRunsKey, 0)),
        totalScore = Mathf.Max(0, PlayerPrefs.GetInt(TotalScoreKey, 0)),
        totalSurvivalTime = Mathf.Max(0f, PlayerPrefs.GetFloat(TotalTimeKey, 0f)),
        bestScore = Mathf.Max(0, PlayerPrefs.GetInt(GlobalBestScoreKey, 0)),
        bestSurvivalTime = Mathf.Max(0f, PlayerPrefs.GetFloat(GlobalBestTimeKey, 0f))
    };

    public static RunReward AwardRun(int score, float survivalTime, string levelLabel, int contractBonusData = 0)
    {
        int safeScore = Mathf.Max(0, score);
        float safeTime = Mathf.Max(0f, survivalTime);
        int safeContractBonus = Mathf.Max(0, contractBonusData);
        int earned = CalculateDataReward(safeScore, safeTime) + safeContractBonus;
        int total = CurrentData + earned;
        string safeLevel = string.IsNullOrWhiteSpace(levelLabel) ? "Unknown" : levelLabel;
        int previousBestScore = Mathf.Max(0, PlayerPrefs.GetInt(GetBestScoreKey(safeLevel), 0));
        float previousBestTime = Mathf.Max(0f, PlayerPrefs.GetFloat(GetBestTimeKey(safeLevel), 0f));
        bool newBestScore = safeScore > previousBestScore;
        bool newBestTime = safeTime > previousBestTime;
        int bestScoreForLevel = newBestScore ? safeScore : previousBestScore;
        float bestTimeForLevel = newBestTime ? safeTime : previousBestTime;
        int totalRuns = Mathf.Max(0, PlayerPrefs.GetInt(TotalRunsKey, 0)) + 1;
        int totalScore = Mathf.Max(0, PlayerPrefs.GetInt(TotalScoreKey, 0)) + safeScore;
        float totalTime = Mathf.Max(0f, PlayerPrefs.GetFloat(TotalTimeKey, 0f)) + safeTime;
        int globalBestScore = Mathf.Max(safeScore, PlayerPrefs.GetInt(GlobalBestScoreKey, 0));
        float globalBestTime = Mathf.Max(safeTime, PlayerPrefs.GetFloat(GlobalBestTimeKey, 0f));
        string grade = CalculatePerformanceGrade(safeScore, safeTime);

        PlayerPrefs.SetInt(DataKey, total);
        PlayerPrefs.SetInt(LastScoreKey, safeScore);
        PlayerPrefs.SetFloat(LastTimeKey, safeTime);
        PlayerPrefs.SetString(LastLevelKey, safeLevel);
        PlayerPrefs.SetInt(LastEarnedKey, earned);
        PlayerPrefs.SetInt(LastContractBonusKey, safeContractBonus);
        PlayerPrefs.SetInt(LastNewBestScoreKey, newBestScore ? 1 : 0);
        PlayerPrefs.SetInt(LastNewBestTimeKey, newBestTime ? 1 : 0);
        PlayerPrefs.SetInt(LastBestScoreKey, bestScoreForLevel);
        PlayerPrefs.SetFloat(LastBestTimeKey, bestTimeForLevel);
        PlayerPrefs.SetInt(LastTotalRunsKey, totalRuns);
        PlayerPrefs.SetInt(LastTotalScoreKey, totalScore);
        PlayerPrefs.SetFloat(LastTotalTimeKey, totalTime);
        PlayerPrefs.SetString(LastGradeKey, grade);
        PlayerPrefs.SetInt(TotalRunsKey, totalRuns);
        PlayerPrefs.SetInt(TotalScoreKey, totalScore);
        PlayerPrefs.SetFloat(TotalTimeKey, totalTime);
        PlayerPrefs.SetInt(GlobalBestScoreKey, globalBestScore);
        PlayerPrefs.SetFloat(GlobalBestTimeKey, globalBestTime);
        PlayerPrefs.SetInt(GetBestScoreKey(safeLevel), bestScoreForLevel);
        PlayerPrefs.SetFloat(GetBestTimeKey(safeLevel), bestTimeForLevel);
        PlayerPrefs.Save();

        return LastRunReward;
    }

    public static void AddData(int amount)
    {
        if (amount == 0)
        {
            return;
        }

        PlayerPrefs.SetInt(DataKey, Mathf.Max(0, CurrentData + amount));
        PlayerPrefs.Save();
    }

    public static void UnlockAll()
    {
        ArenaDiscoveryStorage.UnlockAll();
        for (int i = 0; i < definitions.Length; i++)
        {
            if (definitions[i].cost > 0)
            {
                PlayerPrefs.SetInt(GetUnlockKey(definitions[i].id), 1);
            }
        }

        PlayerPrefs.Save();
    }

    public static int CalculateDataReward(int score, float survivalTime)
    {
        int scoreData = Mathf.FloorToInt(Mathf.Max(0, score) / 18f);
        int survivalBonus = Mathf.FloorToInt(Mathf.Max(0f, survivalTime) / 60f) * 3;
        return Mathf.Max(score > 0 ? 1 : 0, scoreData + survivalBonus);
    }

    public static string GetArenaRecordLabel(string levelLabel)
    {
        string safeLevel = string.IsNullOrWhiteSpace(levelLabel) ? "Unknown" : levelLabel;
        int bestScore = Mathf.Max(0, PlayerPrefs.GetInt(GetBestScoreKey(safeLevel), 0));
        float bestTime = Mathf.Max(0f, PlayerPrefs.GetFloat(GetBestTimeKey(safeLevel), 0f));
        if (bestScore <= 0 && bestTime <= 0f)
        {
            return "Sin registros";
        }

        return $"{bestScore} pts | {bestTime:F1}s";
    }

    public static bool IsUnlocked(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return true;
        }

        UnlockDefinition definition;
        if (TryGetDefinition(id, out definition) && definition.cost <= 0)
        {
            return true;
        }

        return PlayerPrefs.GetInt(GetUnlockKey(id), 0) == 1;
    }

    public static bool IsSelectedSkin(string id)
    {
        return GetSelectedSkinId() == id;
    }

    public static string GetSelectedSkinId()
    {
        return PlayerPrefs.GetString(SelectedSkinKey, SkinDefault);
    }

    public static bool TrySelectSkin(string id)
    {
        UnlockDefinition definition;
        if (!TryGetDefinition(id, out definition) || !definition.skin || !IsUnlocked(id))
        {
            return false;
        }

        PlayerPrefs.SetString(SelectedSkinKey, id);
        PlayerPrefs.Save();
        return true;
    }

    public static bool TryGetSelectedSkinColors(out Color bodyColor, out Color trailColor)
    {
        TryGetSelectedSkin(out UnlockDefinition definition);
        bodyColor = definition.bodyColor;
        trailColor = definition.trailColor;
        return definition.skin;
    }

    public static bool TryGetSelectedSkin(out UnlockDefinition definition)
    {
        if (!TryGetDefinition(GetSelectedSkinId(), out definition) || !definition.skin || !IsUnlocked(definition.id))
        {
            TryGetDefinition(SkinDefault, out definition);
        }

        return definition.skin;
    }

    public static bool TryUnlock(string id)
    {
        UnlockDefinition definition;
        if (!TryGetDefinition(id, out definition) || IsUnlocked(id))
        {
            return false;
        }

        int current = CurrentData;
        if (current < definition.cost)
        {
            return false;
        }

        PlayerPrefs.SetInt(DataKey, current - definition.cost);
        PlayerPrefs.SetInt(GetUnlockKey(id), 1);
        PlayerPrefs.Save();
        return true;
    }

    public static void ResetProgress()
    {
        ArenaDiscoveryStorage.Reset();
        PlayerPrefs.DeleteKey(DataKey);
        PlayerPrefs.DeleteKey(LastScoreKey);
        PlayerPrefs.DeleteKey(LastTimeKey);
        PlayerPrefs.DeleteKey(LastLevelKey);
        PlayerPrefs.DeleteKey(LastEarnedKey);
        PlayerPrefs.DeleteKey(LastContractBonusKey);
        PlayerPrefs.DeleteKey(LastNewBestScoreKey);
        PlayerPrefs.DeleteKey(LastNewBestTimeKey);
        PlayerPrefs.DeleteKey(LastBestScoreKey);
        PlayerPrefs.DeleteKey(LastBestTimeKey);
        PlayerPrefs.DeleteKey(LastTotalRunsKey);
        PlayerPrefs.DeleteKey(LastTotalScoreKey);
        PlayerPrefs.DeleteKey(LastTotalTimeKey);
        PlayerPrefs.DeleteKey(LastGradeKey);
        PlayerPrefs.DeleteKey(TotalRunsKey);
        PlayerPrefs.DeleteKey(TotalScoreKey);
        PlayerPrefs.DeleteKey(TotalTimeKey);
        PlayerPrefs.DeleteKey(GlobalBestScoreKey);
        PlayerPrefs.DeleteKey(GlobalBestTimeKey);
        PlayerPrefs.DeleteKey(SelectedSkinKey);
        for (int i = 0; i < definitions.Length; i++)
        {
            PlayerPrefs.DeleteKey(GetUnlockKey(definitions[i].id));
        }
        for (int i = 0; i < knownLevelLabels.Length; i++)
        {
            PlayerPrefs.DeleteKey(GetBestScoreKey(knownLevelLabels[i]));
            PlayerPrefs.DeleteKey(GetBestTimeKey(knownLevelLabels[i]));
        }

        PlayerPrefs.Save();
    }

    private static string CalculatePerformanceGrade(int score, float survivalTime)
    {
        int safeScore = Mathf.Max(0, score);
        float safeTime = Mathf.Max(0f, survivalTime);
        if (safeScore >= 650 || safeTime >= 210f)
        {
            return "S";
        }
        if (safeScore >= 430 || safeTime >= 150f)
        {
            return "A";
        }
        if (safeScore >= 260 || safeTime >= 95f)
        {
            return "B";
        }
        if (safeScore >= 120 || safeTime >= 45f)
        {
            return "C";
        }

        return "D";
    }

    private static bool TryGetDefinition(string id, out UnlockDefinition definition)
    {
        for (int i = 0; i < definitions.Length; i++)
        {
            if (definitions[i].id == id)
            {
                definition = definitions[i];
                return true;
            }
        }

        definition = default;
        return false;
    }

    private static string GetUnlockKey(string id)
    {
        return $"Glitch_Meta_Unlock_{id}";
    }

    private static string GetBestScoreKey(string levelLabel)
    {
        return $"Glitch_Meta_BestScore_{SanitizeKey(levelLabel)}";
    }

    private static string GetBestTimeKey(string levelLabel)
    {
        return $"Glitch_Meta_BestTime_{SanitizeKey(levelLabel)}";
    }

    private static string SanitizeKey(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return "Unknown";
        }

        return raw.Trim().Replace(" ", string.Empty);
    }
}
