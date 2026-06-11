using System.Collections.Generic;
using UnityEngine;

public static class ContainmentOperationStorage
{
    // Define operaciones pre-run: modificadores simples, objetivo y recompensa de datos.
    public struct OperationDefinition
    {
        public string id;
        public string title;
        public string subtitle;
        public string description;
        public string objective;
        public string risk;
        public string reward;
        public int target;
        public int dataReward;
        public float scoreMultiplier;
        public Color accent;
    }

    public const string NoneId = "operation_standard_protocol";
    public const string FirewallId = "operation_firewall_protocol";
    public const string ExtractionId = "operation_unstable_extraction";
    public const string ContractId = "operation_contract_chain";
    public const string BreachId = "operation_breach_runner";
    public const string AmbientOverdriveId = "operation_ambient_overdrive";
    public const string StorageLogisticsId = "operation_storage_logistics";
    public const string LabSecurityId = "operation_lab_security";
    public const string RuptureEchoId = "operation_rupture_echo";

    private const string SelectedOperationKey = "Glitch_SelectedContainmentOperation";

    private static readonly OperationDefinition[] definitions =
    {
        new OperationDefinition
        {
            id = NoneId,
            title = "Protocolo Estandar",
            subtitle = "Run limpia",
            description = "Sin reglas adicionales. Ideal para practicar movimiento, parry y lectura de eventos.",
            objective = "Sobrevive y suma puntos.",
            risk = "Sin modificadores.",
            reward = "Recompensa base.",
            target = 0,
            dataReward = 0,
            scoreMultiplier = 1f,
            accent = new Color(0.48f, 0.90f, 1f, 1f)
        },
        new OperationDefinition
        {
            id = FirewallId,
            title = "Protocolo Firewall",
            subtitle = "Defensa agresiva",
            description = "El parry se vuelve el centro de la run: mas alcance, mas carga y mas recompensa por cada Burst.",
            objective = "Activa 3 Firewall Burst.",
            risk = "Necesitas acercarte al peligro para cargarlo.",
            reward = "+18 Datos si completas la operacion.",
            target = 3,
            dataReward = 18,
            scoreMultiplier = 1f,
            accent = new Color(1f, 0.74f, 0.42f, 1f)
        },
        new OperationDefinition
        {
            id = ExtractionId,
            title = "Extraccion Inestable",
            subtitle = "Riesgo por recursos",
            description = "El mapa se llena de datos por poco tiempo: hay mucho para juntar, pero perseguirlos te expone.",
            objective = "Recolecta 24 pickups de score.",
            risk = "Perseguir datos puede sacarte de posicion.",
            reward = "+16 Datos y +2 puntos extra por pickup.",
            target = 24,
            dataReward = 16,
            scoreMultiplier = 1f,
            accent = new Color(0.54f, 1f, 0.72f, 1f)
        },
        new OperationDefinition
        {
            id = ContractId,
            title = "Cadena de Contratos",
            subtitle = "Objetivos bajo presion",
            description = "Los encargos aparecen mucho mas seguido y con mejor recompensa: jugar sin objetivo deja de convenir.",
            objective = "Completa 3 contratos de run.",
            risk = "Los contratos pueden forzarte a cambiar prioridad.",
            reward = "+22 Datos al completar la cadena.",
            target = 3,
            dataReward = 22,
            scoreMultiplier = 1f,
            accent = new Color(0.88f, 0.62f, 1f, 1f)
        },
        new OperationDefinition
        {
            id = BreachId,
            title = "Mensajero Breach",
            subtitle = "Escape entre arenas",
            description = "Las brechas aparecen antes y pagan mas, pero fuerzan a jugar alrededor del escape.",
            objective = "Escapa por 2 Breach.",
            risk = "Debes llegar vivo al portal durante el barrido.",
            reward = "+28 Datos al cruzar las brechas.",
            target = 2,
            dataReward = 28,
            scoreMultiplier = 1f,
            accent = new Color(1f, 0.42f, 0.78f, 1f)
        },
        new OperationDefinition
        {
            id = AmbientOverdriveId,
            title = "Sobrecarga Ambiental",
            subtitle = "Arena hiperactiva",
            description = "Lab, Storage y Rupture fuerzan sus sistemas pasivos: gruas, compuertas y fisuras aparecen con mucha mas presencia.",
            objective = "Sobrevive 120s con la arena alterada.",
            risk = "Cada zona cambia de forma mas rapido y exige leer el espacio.",
            reward = "+30 Datos al estabilizar la sobrecarga.",
            target = 120,
            dataReward = 30,
            scoreMultiplier = 1f,
            accent = new Color(1f, 0.70f, 0.36f, 1f)
        }
    };

    public static IReadOnlyList<OperationDefinition> Definitions => definitions;
    public static string SelectedOperationId => NormalizeOperationId(PlayerPrefs.GetString(SelectedOperationKey, NoneId));

    public static OperationDefinition SelectedOperation
    {
        get
        {
            if (TryGetDefinition(SelectedOperationId, out OperationDefinition operation))
            {
                return operation;
            }

            return definitions[0];
        }
    }

    public static void SelectOperation(string id)
    {
        id = NormalizeOperationId(id);
        if (!TryGetDefinition(id, out OperationDefinition operation))
        {
            operation = definitions[0];
        }

        PlayerPrefs.SetString(SelectedOperationKey, operation.id);
        PlayerPrefs.Save();
    }

    public static bool IsSelected(string id)
    {
        return SelectedOperation.id == NormalizeOperationId(id);
    }

    public static bool TryGetDefinition(string id, out OperationDefinition operation)
    {
        id = NormalizeOperationId(id);
        for (int i = 0; i < definitions.Length; i++)
        {
            if (definitions[i].id == id)
            {
                operation = definitions[i];
                return true;
            }
        }

        operation = default;
        return false;
    }

    public static int GetDefinitionIndex(string id)
    {
        id = NormalizeOperationId(id);
        for (int i = 0; i < definitions.Length; i++)
        {
            if (definitions[i].id == id)
            {
                return i;
            }
        }

        return 0;
    }

    private static string NormalizeOperationId(string id)
    {
        if (id == StorageLogisticsId || id == LabSecurityId || id == RuptureEchoId)
        {
            return AmbientOverdriveId;
        }

        return string.IsNullOrWhiteSpace(id) ? NoneId : id;
    }
}
