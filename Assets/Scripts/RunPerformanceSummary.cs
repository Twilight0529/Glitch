using System;

public struct RunPerformanceSummary
{
    public readonly int parries, breaches, contracts, upgrades, pickups;
    public RunPerformanceSummary(int parries, int breaches, int contracts, int upgrades, int pickups)
    {
        this.parries = Math.Max(0, parries);
        this.breaches = Math.Max(0, breaches);
        this.contracts = Math.Max(0, contracts);
        this.upgrades = Math.Max(0, upgrades);
        this.pickups = Math.Max(0, pickups);
    }

    // A factual highlight, not a new achievement or an inferred combat chain.
    public string Highlight
    {
        get
        {
            if (breaches > 0) return breaches == 1 ? "Una brecha superada" : $"{breaches} brechas superadas";
            if (contracts > 0) return contracts == 1 ? "Un contrato completado" : $"{contracts} contratos completados";
            if (upgrades > 0) return upgrades == 1 ? "Una mejora elegida" : $"{upgrades} mejoras elegidas";
            if (parries > 0) return parries == 1 ? "Un Parry exitoso" : $"{parries} Parrys exitosos";
            if (pickups > 0) return pickups == 1 ? "Un dato recuperado" : $"{pickups} datos recuperados";
            return "Supervivencia";
        }
    }
}
