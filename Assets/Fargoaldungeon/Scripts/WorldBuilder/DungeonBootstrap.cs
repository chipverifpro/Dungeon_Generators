using UnityEngine;
using UnityEngine.SceneManagement;

public static class DungeonBootstrap
{
    // Reset static state when the runtime subsystem is reinitialized (important in Editor)
    //[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatic()
    {
        // If you keep static flags, reset them here
        // e.g., _hooked = false;
    }

    //[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void HookSceneEvents()
    {
        // Hook once; safe to add multiple times if you also remove first
        SceneManager.sceneLoaded       -= OnSceneLoaded;
        SceneManager.activeSceneChanged-= OnActiveSceneChanged;

        SceneManager.sceneLoaded       += OnSceneLoaded;        // fires for every scene loaded (Single/Additive)
        SceneManager.activeSceneChanged+= OnActiveSceneChanged; // fires when the active scene switches

        // Optionally run immediately for the first (already-loading) scene:
        //EnsureDungeonGenerator(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        //EnsureDungeonGenerator(scene, mode);
    }

    static void OnActiveSceneChanged(Scene oldScene, Scene newScene)
    {
        // If you need to react specifically when the "active" scene flips
        //EnsureDungeonGenerator(newScene, LoadSceneMode.Single);
    }

    static void EnsureDungeonGenerator(Scene scene, LoadSceneMode mode)
    {
        Scene current = SceneManager.GetActiveScene();
        Debug.Log($"Current scene: {current.name} (index {current.buildIndex})");
        if (current.buildIndex != 1) return;

        // If you want a single persistent generator, keep the first and reuse it
        if (DungeonGenerator.Instance == null)
        {
            // Option A: spawn from a prefab in Resources/Prefabs/DungeonGenerator
            var prefab = Resources.Load<DungeonGenerator>("Prefabs/DungeonGenerator");
            if (prefab != null)
            {
                Object.Instantiate(prefab); // its Awake can call DontDestroyOnLoad
                Debug.Log($"[Bootstrap] Spawned DungeonGenerator from prefab for scene '{scene.name}'.");
            }
            else
            {
                // Option B: create minimal object if no prefab provided
                var go = new GameObject("DungeonGenerator (Ensure Auto)");
                go.AddComponent<DungeonGenerator>(); // inside Awake you can DontDestroyOnLoad
                Debug.Log($"[Bootstrap] Created DungeonGenerator for scene '{scene.name}'.");
            }
        }
        else
        {
            // If you prefer per-scene generators instead of a persistent one, remove the Instance check
            // and instantiate/ensure the scene-local one here (or rebind references).
        }
    }

    static void EnsureDungeonGenerator()
    {
        Scene current = SceneManager.GetActiveScene();
        Debug.Log($"Current scene: {current.name} (index {current.buildIndex})");
        if (current.buildIndex != 1) return;

        if (DungeonGenerator.Instance != null) return;

        // Option A: spawn from a prefab in Resources/Prefabs/DungeonGenerator
        var prefab = Resources.Load<DungeonGenerator>("Prefabs/DungeonGenerator");
        if (prefab != null)
        {
            Object.Instantiate(prefab);
            Debug.Log("[DungeonBootstrap] Spawned DungeonGenerator from Resources.");
            return;
        }

        // Option B: create an empty if no prefab provided
        var go = new GameObject("DungeonGenerator (No Prefab Auto)");
        go.AddComponent<DungeonGenerator>();
        Debug.Log("[DungeonBootstrap] Created DungeonGenerator at runtime.");
    }
}