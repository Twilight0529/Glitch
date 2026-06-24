public static class LocalVersusModeStorage
{
    // Mantiene el modo elegido al cambiar del menu principal a la escena de juego.
    public enum GameMode
    {
        SinglePlayer,
        LocalVersus
    }

    public static GameMode SelectedMode { get; private set; } = GameMode.SinglePlayer;
    public static bool IsLocalVersus => SelectedMode == GameMode.LocalVersus;

    public static void SelectSinglePlayer()
    {
        SelectedMode = GameMode.SinglePlayer;
    }

    public static void SelectLocalVersus()
    {
        SelectedMode = GameMode.LocalVersus;
    }
}
