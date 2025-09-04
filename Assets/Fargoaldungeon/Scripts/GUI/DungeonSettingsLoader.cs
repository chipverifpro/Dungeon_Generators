using System.IO;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

// INCOMPLETE

public static class DungeonSettingsLoader
{
    private const string ResourcePath = "Dungeon/DungeonSettings"; // Assets/Resources/Dungeon/DungeonSettings.asset
    private static string JsonPath => Path.Combine(Application.persistentDataPath, "dungeon_settings_override.json");

    [System.Serializable]
    private class DungeonSettingsDTO
    {
        public int width;
        public int height;
        public bool allowOverlappingRooms;
        public float roomDensity;

        public static DungeonSettingsDTO FromSO(DungeonSettings so) => new DungeonSettingsDTO
        {
            width = so.mapWidth,
            height = so.mapHeight,
        };

        public void ApplyToSO(DungeonSettings so)
        {
            so.mapWidth = width;
            so.mapHeight = height;
        }
    }

    /// Load the settings ScriptableObject (create if missing in Editor), then apply JSON override if present.
    public static DungeonSettings LoadSettings()
    {
        var settings = Resources.Load<DungeonSettings>(ResourcePath);

#if UNITY_EDITOR
        if (settings == null)
        {
            Debug.LogWarning("DungeonSettings.asset not found. Creating default asset in Resources/Dungeon...");
            EnsureResourcesFolders();
            settings = ScriptableObject.CreateInstance<DungeonSettings>();
            AssetDatabase.CreateAsset(settings, "Assets/Resources/Dungeon/DungeonSettings.asset");
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
#endif
        // Apply JSON override if it exists (Editor Play or Player build)
        TryApplyJsonOverride(settings);
        return settings;
    }

    /// Save current settings (Editor: back to asset, Player: to JSON override).
    public static void SaveSettings(DungeonSettings settings)
    {
#if UNITY_EDITOR
        // Persist changes to the .asset even during Play Mode
        EditorUtility.SetDirty(settings);
        AssetDatabase.SaveAssets();
        Debug.Log($"[DungeonSettings] Asset saved.");
#else
        // In builds, write a JSON override
        var dto = DungeonSettingsDTO.FromSO(settings);
        var json = JsonUtility.ToJson(dto, true);
        File.WriteAllText(JsonPath, json);
        // (Optional) log path
        // Debug.Log($"[DungeonSettings] JSON saved to: {JsonPath}");
#endif
    }

    private static void TryApplyJsonOverride(DungeonSettings settings)
    {
        if (!File.Exists(JsonPath)) return;
        try
        {
            var json = File.ReadAllText(JsonPath);
            var dto = JsonUtility.FromJson<DungeonSettingsDTO>(json);
            dto.ApplyToSO(settings);
            // Debug.Log("[DungeonSettings] Applied JSON override.");
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[DungeonSettings] Failed to apply JSON override: {e.Message}");
        }
    }

#if UNITY_EDITOR
    private static void EnsureResourcesFolders()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Resources"))
            AssetDatabase.CreateFolder("Assets", "Resources");
        if (!AssetDatabase.IsValidFolder("Assets/Resources/Dungeon"))
            AssetDatabase.CreateFolder("Assets/Resources", "Dungeon");
    }
#endif
}