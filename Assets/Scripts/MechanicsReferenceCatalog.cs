using System.Collections.Generic;

public static class MechanicsReferenceCatalog
{
    public enum Example { Movement, Dash, Parry, Firewall, Powerups, PhaseDash, Breach, Hijack }
    public struct Entry
    {
        public readonly string title, control, description, tutorialKey;
        public readonly Example example;
        public Entry(string title, string control, string description, string key, Example example)
        { this.title = title; this.control = control; this.description = description; tutorialKey = key; this.example = example; }
    }

    public static readonly IReadOnlyList<Entry> Entries = new[]
    {
        new Entry("Movimiento", "WASD / Flechas", "Rodeá los obstáculos y conservá una salida. Moverte en diagonal no aumenta tu velocidad.", "movement", Example.Movement),
        new Entry("Dash", "Shift", "Desplazamiento rápido en la dirección elegida. Esperá la recarga antes de volver a usarlo: el dash normal no atraviesa paredes.", "ghost_dash", Example.Dash),
        new Entry("Parry", "Espacio / E / Clic izquierdo", "Activá una ventana breve para rechazar una amenaza cercana. Un proyectil rechazado vuelve contra la anomalía y un Parry exitoso carga Firewall.", "parry", Example.Parry),
        new Entry("Firewall Burst", "Q / R / Clic derecho", "Al completar la carga, liberá una descarga que empuja y aturde a la anomalía y refleja proyectiles cercanos. Después necesitás volver a cargarla.", "firewall", Example.Firewall),
        new Entry("Datos y powerups", "Recoger al tocarlos", "Los datos suman puntos y carga de Firewall. Los powerups conceden efectos temporales: consultá sus tiempos en el HUD; el escudo absorbe un impacto.", "powerup", Example.Powerups),
        new Entry("Phase Dash", "Shift mientras el powerup esté activo", "La intangibilidad ocurre durante el dash, no mientras caminás. El aviso amarillo del HUD indica que el powerup está por terminar.", "powerup", Example.PhaseDash),
        new Entry("Breach", "Atravesar el portal", "Llegá al portal antes de que el barrido te alcance. Escapás a otro sector con más presión; ignorar la brecha termina la run.", "breach", Example.Breach),
        new Entry("State Hijack", "F para activar la captura", "Un Parry al jefe en un estado compatible puede capturar una habilidad. El HUD muestra su efecto: la captura se consume al usarla, no caduca guardada.", "state_hijack", Example.Hijack)
    };
}
