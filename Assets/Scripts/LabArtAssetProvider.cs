using UnityEngine;

// Punto único de acceso al kit artístico de la instalación. Cada sector comparte
// resolución, materiales e iluminación, pero conserva sus propios objetos.
// Los colliders permanecen en los objetos originales: estas piezas son presentación.
public static class ArenaArtAssetProvider
{
    private const string FloorPath = "Art/Lab/lab_floor_panels";
    private const string LabPanelPath = "Art/Lab/lab_obstacle_frame_v2";
    private const string TerminalPath = "Art/Lab/lab_terminal_v2";
    private const string StoragePanelPath = "Art/Storage/storage_cargo_frame";
    private const string RupturePanelPath = "Art/Rupture/rupture_fragment_frame";
    private const string CorePanelPath = "Art/Core/core_data_frame";
    private const string ArchivePanelPath = "Art/Archive/archive_data_slab_frame";

    private static Sprite floor;
    private static Sprite labPanel;
    private static Sprite storagePanel;
    private static Sprite rupturePanel;
    private static Sprite corePanel;
    private static Sprite archivePanel;
    private static Sprite terminal;

    public static Sprite Floor => floor != null ? floor : floor = CreateSprite(FloorPath, 128f, Vector4.zero, "LabFloorArt");

    public static Sprite SharedWallPanel => LabPanel;

    public static Sprite LabPanel => labPanel != null
        ? labPanel
        : labPanel = CreatePanelSprite(LabPanelPath, "LabObstaclePanelArt");

    public static Sprite Terminal => terminal != null
        ? terminal
        : terminal = CreateSprite(TerminalPath, 1024f, Vector4.zero, "LabTerminalArt");

    public static void Warmup()
    {
        _ = Floor;
        _ = LabPanel;
        _ = StorageCargoPanel;
        _ = RupturePanel;
        _ = CorePanel;
        _ = ArchivePanel;
        _ = Terminal;
        ThemedPropSpriteProvider.Warmup();
    }

    public static Sprite GetObstaclePanel(ProceduralArenaGenerator.ArenaTheme theme)
    {
        switch (theme)
        {
            case ProceduralArenaGenerator.ArenaTheme.StorageBay:
                return StorageCargoPanel;
            case ProceduralArenaGenerator.ArenaTheme.RuptureZone:
                return RupturePanel;
            case ProceduralArenaGenerator.ArenaTheme.DataCore:
                return CorePanel;
            case ProceduralArenaGenerator.ArenaTheme.NullArchive:
                return ArchivePanel;
            default:
                return LabPanel;
        }
    }

    public static Sprite GetRoundedProp(ProceduralArenaGenerator.ArenaTheme theme)
    {
        return ThemedPropSpriteProvider.Get(theme);
    }

    public static Sprite StorageCargoPanel => storagePanel != null
        ? storagePanel
        : storagePanel = CreatePanelSprite(StoragePanelPath, "StorageCargoPanelArt");

    private static Sprite RupturePanel => rupturePanel != null
        ? rupturePanel
        : rupturePanel = CreatePanelSprite(RupturePanelPath, "RuptureFragmentPanelArt");

    private static Sprite CorePanel => corePanel != null
        ? corePanel
        : corePanel = CreatePanelSprite(CorePanelPath, "CoreDataPanelArt");

    private static Sprite ArchivePanel => archivePanel != null
        ? archivePanel
        : archivePanel = CreatePanelSprite(ArchivePanelPath, "ArchiveDataSlabArt");

    private static Sprite CreatePanelSprite(string resourcePath, string spriteName)
    {
        return CreateSprite(resourcePath, 512f, new Vector4(168f, 168f, 168f, 168f), spriteName);
    }

    private static Sprite CreateSprite(string resourcePath, float pixelsPerUnit, Vector4 border, string spriteName)
    {
        Texture2D texture = Resources.Load<Texture2D>(resourcePath);
        if (texture == null)
        {
            Debug.LogWarning($"No se encontró el recurso artístico {resourcePath}; se usará el fallback geométrico.");
            return SquareSpriteProvider.Get();
        }

        Sprite sprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, texture.width, texture.height),
            Vector2.one * 0.5f,
            Mathf.Max(1f, pixelsPerUnit),
            0,
            SpriteMeshType.FullRect,
            border);
        sprite.name = spriteName;
        return sprite;
    }
}
