using UnityEngine;

// Punto único de acceso al kit artístico de la instalación. Cada sector comparte
// resolución, materiales e iluminación, pero conserva sus propios objetos.
// Los colliders permanecen en los objetos originales: estas piezas son presentación.
public static class ArenaArtAssetProvider
{
    public readonly struct VisualKit
    {
        public readonly Sprite Floor;
        public readonly Sprite Boundary;
        public readonly Sprite Obstacle;
        public readonly Sprite RoundedProp;

        public VisualKit(Sprite floor, Sprite boundary, Sprite obstacle, Sprite roundedProp)
        {
            Floor = floor;
            Boundary = boundary;
            Obstacle = obstacle;
            RoundedProp = roundedProp;
        }
    }

    private const string LabFloorPath = "Art/Lab/lab_floor_panels";
    private const string StorageFloorPath = "Art/Storage/storage_floor_v1";
    private const string RuptureFloorPath = "Art/Rupture/rupture_floor_v1";
    private const string CoreFloorPath = "Art/Core/core_floor_v1";
    private const string ArchiveFloorPath = "Art/Archive/archive_floor_v1";
    private const string LabPanelPath = "Art/Lab/lab_obstacle_frame_v2";
    private const string StorageBoundaryPath = "Art/Storage/storage_boundary_v1";
    private const string RuptureBoundaryPath = "Art/Rupture/rupture_boundary_v1";
    private const string CoreBoundaryPath = "Art/Core/core_boundary_v1";
    private const string ArchiveBoundaryPath = "Art/Archive/archive_boundary_v1";
    private const string TerminalPath = "Art/Lab/lab_terminal_v2";
    private const string StoragePanelPath = "Art/Storage/storage_cargo_frame";
    private const string RupturePanelPath = "Art/Rupture/rupture_fragment_frame";
    private const string CorePanelPath = "Art/Core/core_data_frame";
    private const string ArchivePanelPath = "Art/Archive/archive_data_slab_frame";

    private static Sprite labFloor;
    private static Sprite storageFloor;
    private static Sprite ruptureFloor;
    private static Sprite coreFloor;
    private static Sprite archiveFloor;
    private static Sprite labPanel;
    private static Sprite storageBoundary;
    private static Sprite ruptureBoundary;
    private static Sprite coreBoundary;
    private static Sprite archiveBoundary;
    private static Sprite storagePanel;
    private static Sprite rupturePanel;
    private static Sprite corePanel;
    private static Sprite archivePanel;
    private static Sprite terminal;

    public static Sprite Floor => LabFloor;

    private static Sprite LabFloor => labFloor != null
        ? labFloor
        : labFloor = CreateFloorSprite(LabFloorPath, "LabFloorArt");

    private static Sprite StorageFloor => storageFloor != null
        ? storageFloor
        : storageFloor = CreateFloorSprite(StorageFloorPath, "StorageFloorArt");

    private static Sprite RuptureFloor => ruptureFloor != null
        ? ruptureFloor
        : ruptureFloor = CreateFloorSprite(RuptureFloorPath, "RuptureFloorArt");

    private static Sprite CoreFloor => coreFloor != null
        ? coreFloor
        : coreFloor = CreateFloorSprite(CoreFloorPath, "CoreFloorArt");

    private static Sprite ArchiveFloor => archiveFloor != null
        ? archiveFloor
        : archiveFloor = CreateFloorSprite(ArchiveFloorPath, "ArchiveFloorArt");

    public static Sprite LabPanel => labPanel != null
        ? labPanel
        : labPanel = CreatePanelSprite(LabPanelPath, "LabObstaclePanelArt");

    private static Sprite StorageBoundary => storageBoundary != null
        ? storageBoundary
        : storageBoundary = CreatePanelSprite(StorageBoundaryPath, "StorageBoundaryArt");

    private static Sprite RuptureBoundary => ruptureBoundary != null
        ? ruptureBoundary
        : ruptureBoundary = CreatePanelSprite(RuptureBoundaryPath, "RuptureBoundaryArt");

    private static Sprite CoreBoundary => coreBoundary != null
        ? coreBoundary
        : coreBoundary = CreatePanelSprite(CoreBoundaryPath, "CoreBoundaryArt");

    private static Sprite ArchiveBoundary => archiveBoundary != null
        ? archiveBoundary
        : archiveBoundary = CreatePanelSprite(ArchiveBoundaryPath, "ArchiveBoundaryArt");

    public static Sprite Terminal => terminal != null
        ? terminal
        : terminal = CreateSprite(TerminalPath, 1024f, Vector4.zero, "LabTerminalArt");

    public static void Warmup()
    {
        _ = LabFloor;
        _ = StorageFloor;
        _ = RuptureFloor;
        _ = CoreFloor;
        _ = ArchiveFloor;
        _ = LabPanel;
        _ = StorageBoundary;
        _ = RuptureBoundary;
        _ = CoreBoundary;
        _ = ArchiveBoundary;
        _ = StorageCargoPanel;
        _ = RupturePanel;
        _ = CorePanel;
        _ = ArchivePanel;
        _ = Terminal;
        ThemedPropSpriteProvider.Warmup();
    }

    public static VisualKit GetKit(ProceduralArenaGenerator.ArenaTheme theme)
    {
        switch (theme)
        {
            case ProceduralArenaGenerator.ArenaTheme.StorageBay:
                return new VisualKit(StorageFloor, StorageBoundary, StorageCargoPanel, GetRoundedProp(theme));
            case ProceduralArenaGenerator.ArenaTheme.RuptureZone:
                return new VisualKit(RuptureFloor, RuptureBoundary, RupturePanel, GetRoundedProp(theme));
            case ProceduralArenaGenerator.ArenaTheme.DataCore:
                return new VisualKit(CoreFloor, CoreBoundary, CorePanel, GetRoundedProp(theme));
            case ProceduralArenaGenerator.ArenaTheme.NullArchive:
                return new VisualKit(ArchiveFloor, ArchiveBoundary, ArchivePanel, GetRoundedProp(theme));
            default:
                return new VisualKit(LabFloor, LabPanel, LabPanel, GetRoundedProp(theme));
        }
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

    private static Sprite CreateFloorSprite(string resourcePath, string spriteName)
    {
        return CreateSprite(resourcePath, 128f, Vector4.zero, spriteName);
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
