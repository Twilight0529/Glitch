using System.Collections.Generic;

// Memoria de salida de estados; el reloj lo aporta el controlador para respetar las pausas.
public sealed class EnemyStateCooldowns
{
    private readonly Dictionary<int, float> exitedAt = new Dictionary<int, float>();

    public void RecordExit(int stateId, float now)
    {
        exitedAt[stateId] = now;
    }

    public bool IsCoolingDown(int stateId, float now, float cooldown)
    {
        return cooldown > 0f && exitedAt.TryGetValue(stateId, out float exitTime) &&
            now - exitTime < cooldown;
    }

    public void Clear()
    {
        exitedAt.Clear();
    }
}
