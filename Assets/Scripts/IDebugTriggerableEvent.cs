public interface IDebugTriggerableEvent
{
    string DebugEventLabel { get; }
    bool TriggerForDebug();
}
