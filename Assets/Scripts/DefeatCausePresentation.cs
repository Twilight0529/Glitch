public static class DefeatCausePresentation
{
    public static string Label(string source)
    {
        switch (source)
        {
            case "anomaly_contact": return "CONTACTO CON LA ANOMALÍA";
            case "split_clone_contact": return "CLON DE SPLIT";
            case "anomaly_projectile": return "PROYECTIL DE LA ANOMALÍA";
            case "replay_predator_ghost": return "ECO DE REPLAY PREDATOR";
            case "core_spikes": return "ESPINAS DE CORE";
            case "breach_sweep": return "BARRIDO DE BREACH";
            default: return string.Empty;
        }
    }
}
