using System.Collections.Generic;

// Keyboard/mouse reference for the current bindings, not a remapping system.
public static class PauseControlHints
{
    public struct Hint
    {
        public readonly string action;
        public readonly string input;
        public Hint(string action, string input) { this.action = action; this.input = input; }
    }

    private static readonly IReadOnlyList<Hint> solo = new[]
    {
        new Hint("Mover", "WASD / Flechas"),
        new Hint("Dash", "Shift"),
        new Hint("Parry", "Espacio / E / Clic izq."),
        new Hint("Firewall", "Q / R / Clic der."),
        new Hint("State Hijack", "F"),
        new Hint("Pausa", "Esc")
    };
    private static readonly IReadOnlyList<Hint> versus = new[]
    {
        new Hint("J1 · Mover", "WASD"),
        new Hint("J1 · Dash", "Shift"),
        new Hint("J1 · Parry", "Espacio / E"),
        new Hint("J1 · Firewall", "Q / R"),
        new Hint("J2 · Mover", "Flechas"),
        new Hint("J2 · Cambiar estado", "Ctrl der. / Enter"),
        new Hint("Pausa", "Esc")
    };

    public static IReadOnlyList<Hint> KeyboardAndMouse(bool localVersus) => localVersus ? versus : solo;
}
