public sealed class RunExitConfirmationState
{
    public enum Action { None, Restart, ReturnToMenu }
    public Action Pending { get; private set; }

    public bool Request(Action action)
    {
        if (action == Action.None || Pending != Action.None) return false;
        Pending = action;
        return true;
    }

    public void Cancel() => Pending = Action.None;

    public bool TryConfirm(out Action action)
    {
        action = Pending;
        Pending = Action.None;
        return action != Action.None;
    }
}
