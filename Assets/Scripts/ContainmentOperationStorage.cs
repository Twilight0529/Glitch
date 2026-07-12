using System.Collections.Generic;
using UnityEngine;

// Persiste el estilo de juego elegido antes de una run.
public static class ContainmentOperationStorage
{
    public struct OperationDefinition
    {
        public string id;
        public string title;
        public string subtitle;
        public string description;
        public string rule;
        public string hudRule;
        public string pressure;
        public string reward;
        public float scoreMultiplier;
        public Color accent;
    }

    public const string NoneId = "class_normal";
    public const string AttackId = "class_attack";
    public const string DefendId = "class_defend";
    public const string AchieverId = "class_achiever";
    public const string ChaosId = "class_chaos";

    // Alias para partidas y logros guardados por versiones anteriores.
    public const string FirewallId = DefendId;
    public const string ExtractionId = AchieverId;
    public const string ContractId = "operation_contract_chain";
    public const string BreachId = AttackId;
    public const string AmbientOverdriveId = ChaosId;
    public const string StorageLogisticsId = "operation_storage_logistics";
    public const string LabSecurityId = "operation_lab_security";
    public const string RuptureEchoId = "operation_rupture_echo";

    private const string SelectedOperationKey = "Glitch_SelectedContainmentOperation";

    private static readonly OperationDefinition[] definitions =
    {
        new OperationDefinition
        {
            id = NoneId,
            title = "NORMAL",
            subtitle = "Equilibrado",
            hudRule = "CLASE NORMAL",
            reward = "Todos los sistemas funcionan normalmente.",
            pressure = "Sin bonificaciones especiales.",
            scoreMultiplier = 1f,
            accent = GlitchUiPalette.Information
        },
        new OperationDefinition
        {
            id = AttackId,
            title = "ATTACK",
            subtitle = "Presión ofensiva",
            hudRule = "CLASE ATTACK",
            reward = "+8% velocidad, +20% carga Firewall y Parry más amplio.",
            pressure = "La anomalía corre 12% más y cambia antes de estado.",
            scoreMultiplier = 1.15f,
            accent = GlitchUiPalette.Danger
        },
        new OperationDefinition
        {
            id = DefendId,
            title = "DEFEND",
            subtitle = "Control defensivo",
            hudRule = "CLASE DEFEND",
            reward = "Parry 22% más rápido, mayor radio y resistencia ambiental.",
            pressure = "Movimiento 10% más lento.",
            scoreMultiplier = 1f,
            accent = GlitchUiPalette.Special
        },
        new OperationDefinition
        {
            id = AchieverId,
            title = "ACHIEVER",
            subtitle = "Máxima puntuación",
            hudRule = "CLASE ACHIEVER",
            reward = "+30% de puntuación.",
            pressure = "No aparecen powerups.",
            scoreMultiplier = 1.30f,
            accent = GlitchUiPalette.Success
        },
        new OperationDefinition
        {
            id = ChaosId,
            title = "CHAOS",
            subtitle = "Arena hiperactiva",
            hudRule = "CLASE CHAOS",
            reward = "+50% puntuación y más datos, powerups y mejoras.",
            pressure = "Todo escala antes: anomalía, estados, contratos, eventos y Breach.",
            scoreMultiplier = 1.50f,
            accent = GlitchUiPalette.Alert
        }
    };

    public static IReadOnlyList<OperationDefinition> Definitions => definitions;
    public static string SelectedOperationId => NormalizeOperationId(PlayerPrefs.GetString(SelectedOperationKey, NoneId));

    public static OperationDefinition SelectedOperation
    {
        get
        {
            return TryGetDefinition(SelectedOperationId, out OperationDefinition operation) ? operation : definitions[0];
        }
    }

    public static void SelectOperation(string id)
    {
        id = NormalizeOperationId(id);
        if (!TryGetDefinition(id, out OperationDefinition operation)) operation = definitions[0];
        PlayerPrefs.SetString(SelectedOperationKey, operation.id);
        PlayerPrefs.Save();
    }

    public static bool IsSelected(string id) => SelectedOperation.id == NormalizeOperationId(id);

    public static void ResetSelection()
    {
        PlayerPrefs.DeleteKey(SelectedOperationKey);
        PlayerPrefs.Save();
    }

    public static bool TryGetDefinition(string id, out OperationDefinition operation)
    {
        id = NormalizeOperationId(id);
        for (int i = 0; i < definitions.Length; i++)
        {
            if (definitions[i].id != id) continue;
            operation = definitions[i];
            return true;
        }
        operation = default;
        return false;
    }

    public static int GetDefinitionIndex(string id)
    {
        id = NormalizeOperationId(id);
        for (int i = 0; i < definitions.Length; i++) if (definitions[i].id == id) return i;
        return 0;
    }

    private static string NormalizeOperationId(string id)
    {
        switch (id)
        {
            case "operation_firewall_protocol": return DefendId;
            case "operation_unstable_extraction": return AchieverId;
            case "operation_breach_runner": return AttackId;
            case "operation_contract_chain": return AchieverId;
            case "operation_ambient_overdrive": return ChaosId;
            case "operation_storage_logistics":
            case "operation_lab_security":
            case "operation_rupture_echo": return ChaosId;
            case "operation_standard_protocol": return NoneId;
            default: return string.IsNullOrWhiteSpace(id) ? NoneId : id;
        }
    }
}
