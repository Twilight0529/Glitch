// Contrato común para que el HUD pregunte por un evento de mapa sin saber si está en Lab, Storage o Rupture.
public interface IThemedEventStatusProvider
{
    string ActiveThemedEventLabel { get; }
    string ActiveThemedEventHint { get; }
}
