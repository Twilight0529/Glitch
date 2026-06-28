using System.Collections.Generic;
using UnityEngine;

// Catálogo y memoria de logros. Recibe progreso, desbloquea una sola vez y entrega su recompensa persistente.
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
    public const string SurviveFiveMinutesId = "achievement_survive_5min";
    public const string SurviveEightMinutesId = "achievement_survive_8min";
    public const string FirstFirewallBurstId = "achievement_first_firewall_burst";
    public const string FirewallBurstTenId = "achievement_firewall_burst_10";
    public const string FirewallBurstTwentyFiveId = "achievement_firewall_burst_25";
    public const string FirewallBurstFiftyId = "achievement_firewall_burst_50";
    public const string ParryFiveId = "achievement_parry_5";
    public const string ParryTwentyFiveId = "achievement_parry_25";
    public const string ParrySeventyFiveId = "achievement_parry_75";
    public const string ParryOneHundredFiftyId = "achievement_parry_150";
    public const string PickupsTwentyFiveId = "achievement_pickups_25";
    public const string PickupsOneHundredId = "achievement_pickups_100";
    public const string PickupsTwoHundredFiftyId = "achievement_pickups_250";
    public const string PickupsFiveHundredId = "achievement_pickups_500";
    public const string ContractsFiveId = "achievement_contracts_5";
    public const string ContractsFifteenId = "achievement_contracts_15";
    public const string ContractsThirtyId = "achievement_contracts_30";
    public const string BreachFirstId = "achievement_breach_1";
    public const string BreachThreeId = "achievement_breach_3";
    public const string BreachSevenId = "achievement_breach_7";
    public const string OperationFirewallId = "achievement_operation_firewall";
    public const string OperationExtractionId = "achievement_operation_extraction";
    public const string OperationContractId = "achievement_operation_contract";
    public const string OperationBreachId = "achievement_operation_breach";
    public const string OperationAmbientId = "achievement_operation_ambient_overdrive";
    public const string OperationsThreeId = "achievement_operations_3";
    public const string OperationsTenId = "achievement_operations_10";
    public const string LabSurviveNinetyId = "achievement_lab_survive_90";
    public const string LabSurviveThreeMinutesId = "achievement_lab_survive_180";
    public const string StorageSurviveNinetyId = "achievement_storage_survive_90";
    public const string StorageSurviveThreeMinutesId = "achievement_storage_survive_180";
    public const string RuptureSurviveNinetyId = "achievement_rupture_survive_90";
    public const string RuptureSurviveThreeMinutesId = "achievement_rupture_survive_180";
    public const string GradeAId = "achievement_grade_a";
    public const string GradeSId = "achievement_grade_s";
    public const string RuptureEchoTrapId = "achievement_rupture_echo_trap";

    public const string CounterParries = "parries";
    public const string CounterPickups = "pickups";
    public const string CounterFirewallBursts = "firewall_bursts";
    public const string CounterContracts = "contracts";
    public const string CounterBreaches = "breaches";
    public const string CounterOperations = "operations";

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
            id = SurviveFiveMinutesId,
            title = "Zona Estable",
            description = "Sobrevive 300 segundos en una misma run.",
            progressLabel = "Segundos sobrevividos",
            target = 300,
            dataReward = 28
        },
        new AchievementDefinition
        {
            id = SurviveEightMinutesId,
            title = "Contencion Larga",
            description = "Sobrevive 480 segundos en una misma run.",
            progressLabel = "Segundos sobrevividos",
            target = 480,
            dataReward = 48
        },
        new AchievementDefinition
        {
            id = LabSurviveNinetyId,
            title = "Lab Bajo Control",
            description = "Sobrevive 90 segundos en Lab.",
            progressLabel = "Segundos en Lab",
            target = 90,
            dataReward = 12
        },
        new AchievementDefinition
        {
            id = LabSurviveThreeMinutesId,
            title = "Lab Sellado",
            description = "Sobrevive 180 segundos acumulados en Lab durante una run.",
            progressLabel = "Segundos en Lab",
            target = 180,
            dataReward = 24
        },
        new AchievementDefinition
        {
            id = StorageSurviveNinetyId,
            title = "Logistica Hostil",
            description = "Sobrevive 90 segundos en Storage.",
            progressLabel = "Segundos en Storage",
            target = 90,
            dataReward = 12
        },
        new AchievementDefinition
        {
            id = StorageSurviveThreeMinutesId,
            title = "Turno Industrial",
            description = "Sobrevive 180 segundos acumulados en Storage durante una run.",
            progressLabel = "Segundos en Storage",
            target = 180,
            dataReward = 24
        },
        new AchievementDefinition
        {
            id = RuptureSurviveNinetyId,
            title = "Eco Persistente",
            description = "Sobrevive 90 segundos en Rupture.",
            progressLabel = "Segundos en Rupture",
            target = 90,
            dataReward = 12
        },
        new AchievementDefinition
        {
            id = RuptureSurviveThreeMinutesId,
            title = "Ruptura Sostenida",
            description = "Sobrevive 180 segundos acumulados en Rupture durante una run.",
            progressLabel = "Segundos en Rupture",
            target = 180,
            dataReward = 24
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
            id = FirewallBurstTenId,
            title = "Descarga Recurrente",
            description = "Activa 10 Firewall Burst entre todas tus runs.",
            progressLabel = "Bursts activados",
            target = 10,
            dataReward = 14
        },
        new AchievementDefinition
        {
            id = FirewallBurstTwentyFiveId,
            title = "Cortafuegos Maestro",
            description = "Activa 25 Firewall Burst entre todas tus runs.",
            progressLabel = "Bursts activados",
            target = 25,
            dataReward = 22
        },
        new AchievementDefinition
        {
            id = FirewallBurstFiftyId,
            title = "Nucleo Firewall",
            description = "Activa 50 Firewall Burst entre todas tus runs.",
            progressLabel = "Bursts activados",
            target = 50,
            dataReward = 42
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
            id = ParryTwentyFiveId,
            title = "Pulso Estable",
            description = "Conecta 25 parries exitosos entre todas tus runs.",
            progressLabel = "Parries exitosos",
            target = 25,
            dataReward = 16
        },
        new AchievementDefinition
        {
            id = ParrySeventyFiveId,
            title = "Reflejo de Contencion",
            description = "Conecta 75 parries exitosos entre todas tus runs.",
            progressLabel = "Parries exitosos",
            target = 75,
            dataReward = 28
        },
        new AchievementDefinition
        {
            id = ParryOneHundredFiftyId,
            title = "Parry Absoluto",
            description = "Conecta 150 parries exitosos entre todas tus runs.",
            progressLabel = "Parries exitosos",
            target = 150,
            dataReward = 46
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
            id = PickupsOneHundredId,
            title = "Barrido de Datos",
            description = "Junta 100 pickups de score entre todas tus runs.",
            progressLabel = "Pickups recolectados",
            target = 100,
            dataReward = 18
        },
        new AchievementDefinition
        {
            id = PickupsTwoHundredFiftyId,
            title = "Archivista Glitch",
            description = "Junta 250 pickups de score entre todas tus runs.",
            progressLabel = "Pickups recolectados",
            target = 250,
            dataReward = 32
        },
        new AchievementDefinition
        {
            id = PickupsFiveHundredId,
            title = "Banco de Datos",
            description = "Junta 500 pickups de score entre todas tus runs.",
            progressLabel = "Pickups recolectados",
            target = 500,
            dataReward = 58
        },
        new AchievementDefinition
        {
            id = ContractsFiveId,
            title = "Operador Constante",
            description = "Completa 5 contratos de contencion entre todas tus runs.",
            progressLabel = "Contratos completados",
            target = 5,
            dataReward = 16
        },
        new AchievementDefinition
        {
            id = ContractsFifteenId,
            title = "Supervisor de Campo",
            description = "Completa 15 contratos de contencion entre todas tus runs.",
            progressLabel = "Contratos completados",
            target = 15,
            dataReward = 30
        },
        new AchievementDefinition
        {
            id = ContractsThirtyId,
            title = "Director de Campo",
            description = "Completa 30 contratos de contencion entre todas tus runs.",
            progressLabel = "Contratos completados",
            target = 30,
            dataReward = 52
        },
        new AchievementDefinition
        {
            id = BreachFirstId,
            title = "Primer Salto",
            description = "Escapa por una brecha por primera vez.",
            progressLabel = "Breaches cruzadas",
            target = 1,
            dataReward = 12
        },
        new AchievementDefinition
        {
            id = BreachThreeId,
            title = "Mensajero de Ruptura",
            description = "Escapa por 3 brechas entre todas tus runs.",
            progressLabel = "Breaches cruzadas",
            target = 3,
            dataReward = 24
        },
        new AchievementDefinition
        {
            id = BreachSevenId,
            title = "Ruta de Brechas",
            description = "Escapa por 7 brechas entre todas tus runs.",
            progressLabel = "Breaches cruzadas",
            target = 7,
            dataReward = 46
        },
        new AchievementDefinition
        {
            id = OperationFirewallId,
            title = "Sistema Comprometido",
            description = "Juega una run con Sistemas Degradados.",
            progressLabel = "Regla utilizada",
            target = 1,
            dataReward = 10
        },
        new AchievementDefinition
        {
            id = OperationExtractionId,
            title = "Con Lo Puesto",
            description = "Juega una run sin suministros.",
            progressLabel = "Regla utilizada",
            target = 1,
            dataReward = 10
        },
        new AchievementDefinition
        {
            id = OperationContractId,
            title = "Version de Fabrica",
            description = "Juega una run sin mejoras temporales.",
            progressLabel = "Regla utilizada",
            target = 1,
            dataReward = 10
        },
        new AchievementDefinition
        {
            id = OperationBreachId,
            title = "Sin Respiro",
            description = "Juega una run con Caceria Acelerada.",
            progressLabel = "Regla utilizada",
            target = 1,
            dataReward = 12
        },
        new AchievementDefinition
        {
            id = OperationAmbientId,
            title = "Operacion: Sobrecarga",
            description = "Juega una run con Sobrecarga Ambiental.",
            progressLabel = "Regla utilizada",
            target = 1,
            dataReward = 16
        },
        new AchievementDefinition
        {
            id = OperationsThreeId,
            title = "Operador Versatil",
            description = "Juega 3 runs con reglas de contencion.",
            progressLabel = "Reglas utilizadas",
            target = 3,
            dataReward = 26
        },
        new AchievementDefinition
        {
            id = OperationsTenId,
            title = "Jefe de Protocolo",
            description = "Juega 10 runs con reglas de contencion.",
            progressLabel = "Reglas utilizadas",
            target = 10,
            dataReward = 64
        },
        new AchievementDefinition
        {
            id = GradeAId,
            title = "Rendimiento A",
            description = "Termina una run con rendimiento A o superior.",
            progressLabel = "Rango alcanzado",
            target = 1,
            dataReward = 18
        },
        new AchievementDefinition
        {
            id = GradeSId,
            title = "Rendimiento S",
            description = "Termina una run con rendimiento S.",
            progressLabel = "Rango alcanzado",
            target = 1,
            dataReward = 34
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
        AddCounter(counterId, amount);
        return TryUnlockCounterAchievement(counterId, achievementId, out achievement);
    }

    public static int AddCounter(string counterId, int amount)
    {
        if (string.IsNullOrWhiteSpace(counterId) || amount <= 0)
        {
            return GetCounter(counterId);
        }

        int current = Mathf.Max(0, GetCounter(counterId) + amount);
        PlayerPrefs.SetInt(GetCounterKey(counterId), current);
        PlayerPrefs.Save();
        return current;
    }

    public static bool TryUnlockCounterAchievement(string counterId, string achievementId, out AchievementDefinition achievement)
    {
        if (!TryGetDefinition(achievementId, out achievement))
        {
            return false;
        }

        if (GetCounter(counterId) < Mathf.Max(1, achievement.target))
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
            case SurviveFiveMinutesId:
            case SurviveEightMinutesId:
            case LabSurviveNinetyId:
            case LabSurviveThreeMinutesId:
            case StorageSurviveNinetyId:
            case StorageSurviveThreeMinutesId:
            case RuptureSurviveNinetyId:
            case RuptureSurviveThreeMinutesId:
                return Mathf.Clamp(Mathf.FloorToInt(currentRunSurvivalTime), 0, Mathf.Max(1, achievement.target));
            case ParryFiveId:
            case ParryTwentyFiveId:
            case ParrySeventyFiveId:
            case ParryOneHundredFiftyId:
                return Mathf.Clamp(GetCounter(CounterParries), 0, Mathf.Max(1, achievement.target));
            case PickupsTwentyFiveId:
            case PickupsOneHundredId:
            case PickupsTwoHundredFiftyId:
            case PickupsFiveHundredId:
                return Mathf.Clamp(GetCounter(CounterPickups), 0, Mathf.Max(1, achievement.target));
            case FirstFirewallBurstId:
            case FirewallBurstTenId:
            case FirewallBurstTwentyFiveId:
            case FirewallBurstFiftyId:
                return Mathf.Clamp(GetCounter(CounterFirewallBursts), 0, Mathf.Max(1, achievement.target));
            case FirstContractId:
            case ContractsFiveId:
            case ContractsFifteenId:
            case ContractsThirtyId:
                return Mathf.Clamp(GetCounter(CounterContracts), 0, Mathf.Max(1, achievement.target));
            case BreachFirstId:
            case BreachThreeId:
            case BreachSevenId:
                return Mathf.Clamp(GetCounter(CounterBreaches), 0, Mathf.Max(1, achievement.target));
            case OperationsThreeId:
            case OperationsTenId:
                return Mathf.Clamp(GetCounter(CounterOperations), 0, Mathf.Max(1, achievement.target));
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

        PlayerPrefs.SetInt(GetCounterKey(CounterParries), 150);
        PlayerPrefs.SetInt(GetCounterKey(CounterPickups), 500);
        PlayerPrefs.SetInt(GetCounterKey(CounterFirewallBursts), 50);
        PlayerPrefs.SetInt(GetCounterKey(CounterContracts), 30);
        PlayerPrefs.SetInt(GetCounterKey(CounterBreaches), 7);
        PlayerPrefs.SetInt(GetCounterKey(CounterOperations), 10);
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
        PlayerPrefs.DeleteKey(GetCounterKey(CounterFirewallBursts));
        PlayerPrefs.DeleteKey(GetCounterKey(CounterContracts));
        PlayerPrefs.DeleteKey(GetCounterKey(CounterBreaches));
        PlayerPrefs.DeleteKey(GetCounterKey(CounterOperations));
        PlayerPrefs.Save();
    }

    public static int GetCounter(string counterId)
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
