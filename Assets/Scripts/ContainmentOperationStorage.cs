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
            description = "Juega alrededor del parry: cada Burst importa y la run premia convertir presion en control.",
            objective = "Activa 2 Firewall Burst.",
            risk = "Necesitas acercarte al peligro para cargarlo.",
            reward = "+12 Datos si completas la operacion.",
            target = 2,
            dataReward = 12,
            scoreMultiplier = 1f,
            accent = new Color(1f, 0.74f, 0.42f, 1f)
        },
        new OperationDefinition
        {
            id = ExtractionId,
            title = "Extraccion Inestable",
            subtitle = "Riesgo por recursos",
            description = "Los pickups de score valen mas, pero la operacion te empuja a moverte por toda la arena.",
            objective = "Recolecta 18 pickups de score.",
            risk = "Perseguir datos puede sacarte de posicion.",
            reward = "+10 Datos y +1 punto extra por pickup.",
            target = 18,
            dataReward = 10,
            scoreMultiplier = 1f,
            accent = new Color(0.54f, 1f, 0.72f, 1f)
        },
        new OperationDefinition
        {
            id = ContractId,
            title = "Cadena de Contratos",
            subtitle = "Objetivos bajo presion",
            description = "Convierte la run en una sucesion de encargos cortos con recompensa extra al encadenarlos.",
            objective = "Completa 2 contratos de run.",
            risk = "Los contratos pueden forzarte a cambiar prioridad.",
            reward = "+14 Datos al completar la cadena.",
            target = 2,
            dataReward = 14,
            scoreMultiplier = 1f,
            accent = new Color(0.88f, 0.62f, 1f, 1f)
        },
        new OperationDefinition
        {
            id = BreachId,
            title = "Mensajero Breach",
            subtitle = "Escape entre arenas",
            description = "La prioridad es sobrevivir hasta una brecha y cruzarla. Premia entender rutas y transiciones.",
            objective = "Escapa por 1 Breach.",
            risk = "Debes llegar vivo al portal durante el barrido.",
            reward = "+16 Datos al cruzar la brecha.",
            target = 1,
            dataReward = 16,
            scoreMultiplier = 1f,
            accent = new Color(1f, 0.42f, 0.78f, 1f)
        }
    };

    public static IReadOnlyList<OperationDefinition> Definitions => definitions;
    public static string SelectedOperationId => PlayerPrefs.GetString(SelectedOperationKey, NoneId);

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
        if (!TryGetDefinition(id, out OperationDefinition operation))
        {
            operation = definitions[0];
        }

        PlayerPrefs.SetString(SelectedOperationKey, operation.id);
        PlayerPrefs.Save();
    }

    public static bool IsSelected(string id)
    {
        return SelectedOperation.id == id;
    }

    public static bool TryGetDefinition(string id, out OperationDefinition operation)
    {
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
        for (int i = 0; i < definitions.Length; i++)
        {
            if (definitions[i].id == id)
            {
                return i;
            }
        }

        return 0;
    }
}
