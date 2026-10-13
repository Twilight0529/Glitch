using UnityEngine;

// Lenguaje visual de la anomalía. Este catálogo describe cómo se presenta cada
// estado, pero nunca cambia duración, velocidad, daño ni selección de gameplay.
public static class EnemyStatePresentationCatalog
{
    public enum Glyph
    {
        None,
        Pursuit,
        Split,
        Expansion,
        Speed,
        Weave,
        Destroyer,
        PhaseBlink,
        Pincer,
        SignalJam,
        Orbit,
        Replay,
        Lattice,
        Desync,
        Recompile,
        Possession,
        Contract,
        Adaptive,
        Tether,
        Blindspot
    }

    public readonly struct Profile
    {
        public readonly Glyph glyph;
        public readonly int tier;
        public readonly float signatureSeconds;
        public readonly float rhythm;
        public readonly Vector2 introSilhouette;
        public readonly float trailStrength;
        public readonly float cameraStrength;
        public readonly float ornamentPulse;

        public Profile(
            Glyph glyph,
            int tier,
            float signatureSeconds,
            float rhythm,
            Vector2 introSilhouette,
            float trailStrength,
            float cameraStrength,
            float ornamentPulse)
        {
            this.glyph = glyph;
            this.tier = tier;
            this.signatureSeconds = signatureSeconds;
            this.rhythm = rhythm;
            this.introSilhouette = introSilhouette;
            this.trailStrength = trailStrength;
            this.cameraStrength = cameraStrength;
            this.ornamentPulse = ornamentPulse;
        }

        public bool ShowsSignature => glyph != Glyph.None && glyph != Glyph.Pursuit;
        public bool HasAttackPreparation => glyph == Glyph.Expansion || glyph == Glyph.PhaseBlink
            || glyph == Glyph.Pincer || glyph == Glyph.SignalJam || glyph == Glyph.Orbit
            || glyph == Glyph.Recompile;
    }

    public static Profile Get(EnemyController.AnomalyState state)
    {
        switch (state)
        {
            case EnemyController.AnomalyState.Split:
                return P(Glyph.Split, 1, 0.62f, 7.8f, 1.15f, 0.82f, 0.10f, 0.92f, 0.80f);
            case EnemyController.AnomalyState.ExpansionShoot:
                return P(Glyph.Expansion, 1, 0.62f, 8.7f, 0.88f, 1.16f, 0.08f, 0.94f, 0.92f);
            case EnemyController.AnomalyState.SpeedSurge:
                return P(Glyph.Speed, 1, 0.56f, 11.4f, 1.18f, 0.80f, 0.90f, 0.92f, 0.72f);
            case EnemyController.AnomalyState.WeaveHunter:
                return P(Glyph.Weave, 1, 0.54f, 10.2f, 1.08f, 0.91f, 0.46f, 0.88f, 0.78f);
            case EnemyController.AnomalyState.Destroyer:
                return P(Glyph.Destroyer, 1, 0.64f, 7.2f, 1.20f, 0.78f, 0.18f, 1.00f, 0.96f);
            case EnemyController.AnomalyState.PhaseBlink:
                return P(Glyph.PhaseBlink, 2, 0.50f, 12.8f, 0.78f, 1.18f, 0.88f, 1.00f, 0.88f);
            case EnemyController.AnomalyState.PincerBarrage:
                return P(Glyph.Pincer, 2, 0.52f, 9.4f, 0.86f, 1.14f, 0.10f, 1.04f, 0.96f);
            case EnemyController.AnomalyState.SignalJam:
                return P(Glyph.SignalJam, 2, 0.46f, 15.2f, 1.08f, 0.90f, 0.34f, 0.92f, 1.15f);
            case EnemyController.AnomalyState.OrbitBarrage:
                return P(Glyph.Orbit, 2, 0.50f, 10.8f, 0.92f, 1.12f, 0.12f, 1.04f, 1.06f);
            case EnemyController.AnomalyState.ReplayPredator:
                return P(Glyph.Replay, 2, 0.47f, 8.2f, 1.12f, 0.88f, 0.72f, 1.00f, 0.88f);
            case EnemyController.AnomalyState.ChecksumLattice:
                return P(Glyph.Lattice, 2, 0.48f, 12.2f, 0.90f, 1.12f, 0.08f, 1.02f, 1.08f);
            case EnemyController.AnomalyState.InputDesync:
                return P(Glyph.Desync, 2, 0.43f, 16.5f, 1.14f, 0.86f, 0.58f, 0.98f, 1.16f);
            case EnemyController.AnomalyState.MapRecompile:
                return P(Glyph.Recompile, 2, 0.50f, 11.8f, 0.88f, 1.14f, 0.14f, 1.06f, 1.10f);
            case EnemyController.AnomalyState.SignalPossession:
                return P(Glyph.Possession, 2, 0.47f, 9.8f, 1.12f, 0.90f, 0.24f, 1.02f, 1.02f);
            case EnemyController.AnomalyState.PhaseContract:
                return P(Glyph.Contract, 2, 0.48f, 12.4f, 0.84f, 0.84f, 0.28f, 1.05f, 1.08f);
            case EnemyController.AnomalyState.AdaptiveCountermeasure:
                return P(Glyph.Adaptive, 3, 0.37f, 13.8f, 1.16f, 0.84f, 0.30f, 1.14f, 1.18f);
            case EnemyController.AnomalyState.SignalTether:
                return P(Glyph.Tether, 3, 0.35f, 12.6f, 1.18f, 0.82f, 0.36f, 1.12f, 1.12f);
            case EnemyController.AnomalyState.BlindspotProtocol:
                return P(Glyph.Blindspot, 3, 0.32f, 17.2f, 1.16f, 0.80f, 0.76f, 1.10f, 1.16f);
            default:
                return P(Glyph.Pursuit, 0, 0f, 6.8f, 1f, 1f, 0f, 0.48f, 0.55f);
        }
    }

    private static Profile P(
        Glyph glyph,
        int tier,
        float seconds,
        float rhythm,
        float silhouetteX,
        float silhouetteY,
        float trail,
        float camera,
        float ornament)
    {
        return new Profile(
            glyph,
            tier,
            seconds,
            rhythm,
            new Vector2(silhouetteX, silhouetteY),
            trail,
            camera,
            ornament);
    }
}
