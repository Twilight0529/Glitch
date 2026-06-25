using System.Collections.Generic;
using UnityEngine;

public static class ContainmentOperationStorage
{
    // Define reglas pre-run que alteran toda la partida a cambio de mayor puntuacion.
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
            rule = "Todos los sistemas funcionan normalmente.",
            hudRule = "SISTEMAS NORMALES",
            pressure = "Sin restricciones adicionales.",
            reward = "Puntaje x1.00.",
            scoreMultiplier = 1f,
            accent = new Color(0.48f, 0.90f, 1f, 1f)
        },
        new OperationDefinition
        {
            id = FirewallId,
            title = "Sistemas Degradados",
            subtitle = "Defensa limitada",
            description = "La anomalia interfiere con tus herramientas defensivas durante toda la operacion.",
            rule = "El parry tarda 75% mas y Firewall carga 40% menos.",
            hudRule = "PARRY LENTO | FIREWALL -40%",
            pressure = "Cada defensa fallida deja una ventana de peligro mayor.",
            reward = "Puntaje x1.30.",
            scoreMultiplier = 1.30f,
            accent = new Color(1f, 0.74f, 0.42f, 1f)
        },
        new OperationDefinition
        {
            id = ExtractionId,
            title = "Sin Suministros",
            subtitle = "Cero powerups",
            description = "La red de soporte queda desconectada: solo contas con movimiento, parry y tus mejoras instaladas.",
            rule = "No aparecen powerups de velocidad, escudo ni modo compacto.",
            hudRule = "SIN POWERUPS",
            pressure = "No hay rescates temporales ni golpes absorbidos.",
            reward = "Puntaje x1.35.",
            scoreMultiplier = 1.35f,
            accent = new Color(0.54f, 1f, 0.72f, 1f)
        },
        new OperationDefinition
        {
            id = ContractId,
            title = "Nucleo Sin Parches",
            subtitle = "Sin mejoras de run",
            description = "La configuracion inicial queda sellada y no puede modificarse mientras dure la persecucion.",
            rule = "No aparecen selecciones de mejoras durante la run.",
            hudRule = "SIN MEJORAS DE RUN",
            pressure = "Tu kit no escala mientras la anomalia si lo hace.",
            reward = "Puntaje x1.40.",
            scoreMultiplier = 1.40f,
            accent = new Color(0.88f, 0.62f, 1f, 1f)
        },
        new OperationDefinition
        {
            id = BreachId,
            title = "Caceria Acelerada",
            subtitle = "Anomalia sobrecargada",
            description = "El perseguidor recibe mas velocidad y cambia de estrategia con una cadencia mas agresiva.",
            rule = "La anomalia corre 18% mas y sus estados rotan 18% antes.",
            hudRule = "ANOMALIA +18% | ROTACION RAPIDA",
            pressure = "Hay menos tiempo para crear distancia y reconocer patrones.",
            reward = "Puntaje x1.35.",
            scoreMultiplier = 1.35f,
            accent = new Color(1f, 0.42f, 0.78f, 1f)
        },
        new OperationDefinition
        {
            id = AmbientOverdriveId,
            title = "Sobrecarga Ambiental",
            subtitle = "Arena hiperactiva",
            description = "Cada arena fuerza su sistema pasivo: gruas, compuertas, fisuras, gates y campos nulos aparecen con mucha mas presencia.",
            rule = "Los sistemas pasivos de todas las arenas se activan con mucha mas frecuencia.",
            hudRule = "ARENA HIPERACTIVA",
            pressure = "El espacio seguro cambia constantemente durante la persecucion.",
            reward = "Puntaje x1.45.",
            scoreMultiplier = 1.45f,
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
