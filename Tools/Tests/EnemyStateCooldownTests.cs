using System;

// Pruebas independientes de Unity: compilar con Assets/Scripts/EnemyStateCooldowns.cs.
public static class EnemyStateCooldownTests
{
    private static int passed;

    private static void Check(bool condition, string description)
    {
        if (!condition) throw new Exception(description);
        passed++;
    }

    public static int Main()
    {
        var memory = new EnemyStateCooldowns();
        Check(!memory.IsCoolingDown(1, 0f, 10f), "Un estado nunca usado debe estar disponible.");
        memory.RecordExit(1, 10f);
        Check(memory.IsCoolingDown(1, 10f, 10f), "La salida inicia la espera inmediatamente.");
        Check(memory.IsCoolingDown(1, 19.99f, 10f), "No puede volver durante la ventana, incluso tras BasePursuit.");
        Check(!memory.IsCoolingDown(1, 20f, 10f), "Vuelve a estar disponible exactamente al cumplir la espera.");
        memory.RecordExit(2, 18f);
        Check(!memory.IsCoolingDown(1, 20f, 10f) && memory.IsCoolingDown(2, 20f, 10f),
            "La salida de otro estado no reemplaza ni extiende el historial anterior.");
        memory.RecordExit(1, 21f);
        Check(memory.IsCoolingDown(1, 30f, 10f) && !memory.IsCoolingDown(1, 31f, 10f),
            "Una salida posterior renueva la ventana de ese estado.");
        Check(!memory.IsCoolingDown(1, 21f, 0f), "Una espera deshabilitada no bloquea estados.");
        Check(memory.IsCoolingDown(2, 20f, 10f) && memory.IsCoolingDown(2, 20f, 10f),
            "Consultar con el reloj pausado no consume la espera.");
        memory.Clear();
        Check(!memory.IsCoolingDown(1, 21f, 10f) && !memory.IsCoolingDown(2, 21f, 10f),
            "La memoria no persiste al iniciar otra run.");
        Console.WriteLine("EnemyStateCooldowns: " + passed + " pruebas aprobadas.");
        return 0;
    }
}
