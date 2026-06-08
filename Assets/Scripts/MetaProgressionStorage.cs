using System.Collections.Generic;
using UnityEngine;

public static class MetaProgressionStorage
{
    // Guarda progresion persistente entre runs: moneda obtenida por score y desbloqueos comprados.
    public struct UnlockDefinition
    {
        public string id;
        public string section;
        public string title;
        public string description;
        public int cost;
        public bool skin;
        public Color bodyColor;
        public Color trailColor;
    }

    public struct RunReward
    {
        public int score;
        public float survivalTime;
        public string levelLabel;
        public int dataEarned;
        public int contractBonusData;
        public int totalData;
    }

    public const string UnlockHazardResistance = "upgrade_hazard_resistance";
    public const string UnlockDisplacementStabilizer = "upgrade_displacement_stabilizer";
    public const string UnlockHazardFirewallCharge = "upgrade_hazard_firewall_charge";
    public const string UnlockFirewallBurstStun = "upgrade_firewall_burst_stun";
    public const string UnlockExtraUpgradeChoice = "upgrade_extra_choice";
    public const string SectionRunUpgrades = "MEJORAS";
    public const string SectionSkins = "COLORES";
    public const string SkinDefault = "skin_default";
    public const string SkinRupturePink = "skin_rupture_pink";
    public const string SkinLabMint = "skin_lab_mint";
    public const string SkinStorageGold = "skin_storage_gold";
    public const string SkinFirewallWhite = "skin_firewall_white";

    private const string DataKey = "Glitch_Meta_Data";
    private const string LastScoreKey = "Glitch_Meta_LastScore";
    private const string LastTimeKey = "Glitch_Meta_LastTime";
    private const string LastLevelKey = "Glitch_Meta_LastLevel";
    private const string LastEarnedKey = "Glitch_Meta_LastEarned";
    private const string LastContractBonusKey = "Glitch_Meta_LastContractBonus";
    private const string SelectedSkinKey = "Glitch_Meta_SelectedSkin";

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
            id = SkinDefault,
            section = SectionSkins,
            title = "Cian Base",
            description = "Color inicial del protocolo GLITCH.",
            cost = 0,
            skin = true,
            bodyColor = new Color(0.28f, 0.88f, 1f, 1f),
            trailColor = new Color(0.42f, 0.92f, 1f, 0.85f)
        },
        new UnlockDefinition
        {
            id = SkinRupturePink,
            section = SectionSkins,
            title = "Rupture Rosa",
            description = "Paleta magenta inspirada en las cadenas de eco.",
            cost = 35,
            skin = true,
            bodyColor = new Color(1f, 0.42f, 0.78f, 1f),
            trailColor = new Color(1f, 0.58f, 0.95f, 0.88f)
        },
        new UnlockDefinition
        {
            id = SkinLabMint,
            section = SectionSkins,
            title = "Lab Menta",
            description = "Paleta fria de contencion y seguridad.",
            cost = 40,
            skin = true,
            bodyColor = new Color(0.48f, 1f, 0.74f, 1f),
            trailColor = new Color(0.58f, 1f, 0.84f, 0.88f)
        },
        new UnlockDefinition
        {
            id = SkinStorageGold,
            section = SectionSkins,
            title = "Storage Dorado",
            description = "Paleta calida de carga industrial.",
            cost = 40,
            skin = true,
            bodyColor = new Color(1f, 0.72f, 0.34f, 1f),
            trailColor = new Color(1f, 0.82f, 0.42f, 0.86f)
        },
        new UnlockDefinition
        {
            id = SkinFirewallWhite,
            section = SectionSkins,
            title = "Firewall Blanco",
            description = "Paleta luminosa para runs de alto contraste.",
            cost = 55,
            skin = true,
            bodyColor = new Color(0.92f, 0.98f, 1f, 1f),
            trailColor = new Color(0.70f, 0.96f, 1f, 0.90f)
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
        totalData = CurrentData
    };

    public static RunReward AwardRun(int score, float survivalTime, string levelLabel, int contractBonusData = 0)
    {
        int safeScore = Mathf.Max(0, score);
        int safeContractBonus = Mathf.Max(0, contractBonusData);
        int earned = CalculateDataReward(safeScore, survivalTime) + safeContractBonus;
        int total = CurrentData + earned;

        PlayerPrefs.SetInt(DataKey, total);
        PlayerPrefs.SetInt(LastScoreKey, safeScore);
        PlayerPrefs.SetFloat(LastTimeKey, Mathf.Max(0f, survivalTime));
        PlayerPrefs.SetString(LastLevelKey, string.IsNullOrWhiteSpace(levelLabel) ? "Unknown" : levelLabel);
        PlayerPrefs.SetInt(LastEarnedKey, earned);
        PlayerPrefs.SetInt(LastContractBonusKey, safeContractBonus);
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
        UnlockDefinition definition;
        if (!TryGetDefinition(GetSelectedSkinId(), out definition) || !definition.skin || !IsUnlocked(definition.id))
        {
            TryGetDefinition(SkinDefault, out definition);
        }

        bodyColor = definition.bodyColor;
        trailColor = definition.trailColor;
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
        PlayerPrefs.DeleteKey(DataKey);
        PlayerPrefs.DeleteKey(LastScoreKey);
        PlayerPrefs.DeleteKey(LastTimeKey);
        PlayerPrefs.DeleteKey(LastLevelKey);
        PlayerPrefs.DeleteKey(LastEarnedKey);
        PlayerPrefs.DeleteKey(LastContractBonusKey);
        PlayerPrefs.DeleteKey(SelectedSkinKey);
        for (int i = 0; i < definitions.Length; i++)
        {
            PlayerPrefs.DeleteKey(GetUnlockKey(definitions[i].id));
        }

        PlayerPrefs.Save();
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
}
