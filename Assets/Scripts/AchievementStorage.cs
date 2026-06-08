using System.Collections.Generic;
using UnityEngine;

public static class AchievementStorage
{
    // Guarda logros persistentes y contadores acumulados para dar objetivos de largo plazo.
    public struct AchievementDefinition
    {
        public string id;
        public string title;
        public string description;
        public string progressLabel;
        public int target;
        public int dataReward;
    }

    public const string SectionAchievements = "LOGROS";
    public const string FirstContractId = "achievement_first_contract";
    public const string SurviveThreeMinutesId = "achievement_survive_3min";
    public const string FirstFirewallBurstId = "achievement_first_firewall_burst";
    public const string ParryFiveId = "achievement_parry_5";
    public const string PickupsTwentyFiveId = "achievement_pickups_25";
    public const string RuptureEchoTrapId = "achievement_rupture_echo_trap";

    public const string CounterParries = "parries";
    public const string CounterPickups = "pickups";

    private const string KeyPrefix = "Glitch_Achievement_";
    private const string CounterPrefix = "Glitch_AchievementCounter_";

    private static readonly AchievementDefinition[] definitions =
    {
        new AchievementDefinition
        {
            id = FirstContractId,
            title = "Primer Protocolo",
            description = "Completa un contrato de contencion durante una run.",
            progressLabel = "Contratos completados",
            target = 1,
            dataReward = 8
        },
        new AchievementDefinition
        {
            id = SurviveThreeMinutesId,
            title = "Tres Minutos",
            description = "Sobrevive 180 segundos en una misma run.",
            progressLabel = "Segundos sobrevividos",
            target = 180,
            dataReward = 12
        },
        new AchievementDefinition
        {
            id = FirstFirewallBurstId,
            title = "Firewall Operativo",
            description = "Activa un Firewall Burst por primera vez.",
            progressLabel = "Bursts activados",
            target = 1,
            dataReward = 8
        },
        new AchievementDefinition
        {
            id = ParryFiveId,
            title = "Mano Firme",
            description = "Conecta 5 parries exitosos en total.",
            progressLabel = "Parries exitosos",
            target = 5,
            dataReward = 10
        },
        new AchievementDefinition
        {
            id = PickupsTwentyFiveId,
            title = "Recolector",
            description = "Junta 25 pickups de score entre todas tus runs.",
            progressLabel = "Pickups recolectados",
            target = 25,
            dataReward = 10
        },
        new AchievementDefinition
        {
            id = RuptureEchoTrapId,
            title = "Cadena Perfecta",
            description = "Atrapa al jefe o a un clon con una cadena de ecos en Rupture.",
            progressLabel = "Cadenas efectivas",
            target = 1,
            dataReward = 14
        }
    };

    public static IReadOnlyList<AchievementDefinition> Definitions => definitions;

    public static int CompletedCount
    {
        get
        {
            int count = 0;
            for (int i = 0; i < definitions.Length; i++)
            {
                if (IsUnlocked(definitions[i].id))
                {
                    count++;
                }
            }

            return count;
        }
    }

    public static bool TryUnlock(string id, out AchievementDefinition achievement)
    {
        if (!TryGetDefinition(id, out achievement) || IsUnlocked(id))
        {
            return false;
        }

        PlayerPrefs.SetInt(GetAchievementKey(id), 1);
        MetaProgressionStorage.AddData(Mathf.Max(0, achievement.dataReward));
        PlayerPrefs.Save();
        return true;
    }

    public static bool AddCounterAndTryUnlock(
        string counterId,
        int amount,
        int target,
        string achievementId,
        out AchievementDefinition achievement)
    {
        int safeTarget = Mathf.Max(1, target);
        int current = Mathf.Clamp(GetCounter(counterId) + Mathf.Max(0, amount), 0, safeTarget);
        PlayerPrefs.SetInt(GetCounterKey(counterId), current);
        PlayerPrefs.Save();

        if (current < safeTarget)
        {
            achievement = default;
            return false;
        }

        return TryUnlock(achievementId, out achievement);
    }

    public static bool IsUnlocked(string id)
    {
        return !string.IsNullOrWhiteSpace(id) && PlayerPrefs.GetInt(GetAchievementKey(id), 0) == 1;
    }

    public static int GetProgress(AchievementDefinition achievement, float currentRunSurvivalTime)
    {
        if (IsUnlocked(achievement.id))
        {
            return Mathf.Max(1, achievement.target);
        }

        switch (achievement.id)
        {
            case SurviveThreeMinutesId:
                return Mathf.Clamp(Mathf.FloorToInt(currentRunSurvivalTime), 0, Mathf.Max(1, achievement.target));
            case ParryFiveId:
                return Mathf.Clamp(GetCounter(CounterParries), 0, Mathf.Max(1, achievement.target));
            case PickupsTwentyFiveId:
                return Mathf.Clamp(GetCounter(CounterPickups), 0, Mathf.Max(1, achievement.target));
            default:
                return 0;
        }
    }

    public static void UnlockAll()
    {
        for (int i = 0; i < definitions.Length; i++)
        {
            PlayerPrefs.SetInt(GetAchievementKey(definitions[i].id), 1);
        }

        PlayerPrefs.SetInt(GetCounterKey(CounterParries), 5);
        PlayerPrefs.SetInt(GetCounterKey(CounterPickups), 25);
        PlayerPrefs.Save();
    }

    public static void ResetAchievements()
    {
        for (int i = 0; i < definitions.Length; i++)
        {
            PlayerPrefs.DeleteKey(GetAchievementKey(definitions[i].id));
        }

        PlayerPrefs.DeleteKey(GetCounterKey(CounterParries));
        PlayerPrefs.DeleteKey(GetCounterKey(CounterPickups));
        PlayerPrefs.Save();
    }

    private static int GetCounter(string counterId)
    {
        return string.IsNullOrWhiteSpace(counterId)
            ? 0
            : Mathf.Max(0, PlayerPrefs.GetInt(GetCounterKey(counterId), 0));
    }

    private static bool TryGetDefinition(string id, out AchievementDefinition achievement)
    {
        for (int i = 0; i < definitions.Length; i++)
        {
            if (definitions[i].id == id)
            {
                achievement = definitions[i];
                return true;
            }
        }

        achievement = default;
        return false;
    }

    private static string GetAchievementKey(string id)
    {
        return $"{KeyPrefix}{id}";
    }

    private static string GetCounterKey(string id)
    {
        return $"{CounterPrefix}{id}";
    }
}
