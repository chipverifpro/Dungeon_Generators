using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.SceneManagement;
public partial class DungeonGenerator : MonoBehaviour
{
    public System.Random rng;

    // 2D assets defined in Unity
    public Tilemap tilemap; // floors
    public Tilemap tilemap_walls;
    public Tilemap tilemap_doors;
    public TileBase floorTile;
    public TileBase wallTile;
    public TileBase doorClosedTile;
    public TileBase doorOpenTile;

    // 3D assets defined in Unity
    public Grid grid;                         // same Grid as the 2D Tilemap
    public GameObject floorPrefab;
    public GameObject rampPrefab;             // oriented to face +Z
    public GameObject cliffPrefab;            // a 1x1x1 pillar you can scale in Y
    public GameObject diagonalWallPrefab;    // thin strip or quad oriented along +Z
    public GameObject triangleFloorPrefab;   // half of a floor tile
    public GameObject doorClosedPrefab;
    public GameObject doorOpenPrefab;
    public Transform root;                    // parent for spawned meshes

    public static readonly Color colorDefault = new(1f, 0.4f, 0.7f, 0.5f); // semi-transparent pink

    // Master list of Rooms makes the current map
    public List<Room> rooms = new(); // Master List of rooms including list of points and metadata


    // These global lists help lookup things quickly
    // These are handled in Room class now.
    //public HashSet<Vector2Int> floor_hash_map = new();
    //public HashSet<Vector2Int> wall_hash_map = new();

    // Future use by Door capability...
    Dictionary<int, Door> doorById; // partner lookup and save/load

    // global perlin seed
    public float perlinSeedX = 0;
    public float perlinSeedY = 0;

    // global variables for return of success and failure results (some functions only)
    [HideInInspector] public bool success;    // global generic return value from various tasks
    [HideInInspector] public string failure;    // global failure description string

    // globally incremented counter across all Agents.
    private int lastIssuedAgentId = 0;      // allows giving agents unique ID's

    // list of directions for neighbor checks
    public Vector2Int[] directions_xy = { Vector2Int.up,
                                   Vector2Int.down,
                                   Vector2Int.left,
                                   Vector2Int.right, };
    //       Vector2Int.up + Vector2Int.left,
    //       Vector2Int.up + Vector2Int.right,
    //       Vector2Int.down + Vector2Int.left,
    //       Vector2Int.down + Vector2Int.right };

    // Sets the agent id to a unique number, and returns that value.
    // Can be called without an agent, and caller must assign the number themselves.
    // ID is used to (a) determine equivalence match, (b) track id of some event in a list.
    public int GetNewAgentId(Agent agent)
    {
        lastIssuedAgentId++;
        if (agent!=null) agent.id = lastIssuedAgentId;
        return lastIssuedAgentId;
    }
}



public partial class DungeonGenerator : MonoBehaviour
{
    // ---- call this from Awake() or keep this whole block in this file ----
    [Header("Auto-Init")]
    [SerializeField] string rootName = "DungeonGenerator";
    [SerializeField] string floorsName = "Floors3D";
    [SerializeField] string wallsName = "Walls3D";
    [SerializeField] string doorsName = "Doors3D";
    [SerializeField] int rngSeed = 0; // 0 = random

    // runtime containers
    Transform floors3D, walls3D, doors3D;

    void Awake_Tilemap()
    {
        InitializeTilemapConnections();
        BuildRuntimeParents();
        InitRng();
        SceneManager.sceneLoaded += OnSceneLoaded_Reconnect;
    }

    void OnDestroy_Tilemap()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded_Reconnect;
    }

    void OnSceneLoaded_Reconnect(Scene s, LoadSceneMode m)
    {
        // Rebind scene objects if the scene changed
        InitializeTilemapConnections();
        BuildRuntimeParents();
    }

    void InitializeTilemapConnections()
    {
        // --- 2D Grid & Tilemaps ---
        if (!grid) grid = FindFirstObjectByType<Grid>(FindObjectsInactive.Include);

        if (!tilemap || !tilemap_walls || !tilemap_doors)
        {
            var tms = FindObjectsByType<Tilemap>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var tm in tms)
            {
                var name = tm.name.ToLowerInvariant();
                if (!tilemap       && (name.Contains("floor") || name.Contains("ground"))) tilemap = tm;
                if (!tilemap_walls && name.Contains("wall"))                             tilemap_walls = tm;
                if (!tilemap_doors && name.Contains("door"))                             tilemap_doors = tm;
            }
        }

        // --- Prefabs (optional Resources fallback) ---
        TryLoadIfNull(ref floorPrefab,         "Prefabs/Terrain/PF_Floor");
        TryLoadIfNull(ref rampPrefab,          "Prefabs/Terrain/PF_Ramp");
        TryLoadIfNull(ref cliffPrefab,         "Prefabs/Terrain/PF_Cliff");
        TryLoadIfNull(ref diagonalWallPrefab,  "Prefabs/Terrain/PF_Diagonal");
        TryLoadIfNull(ref triangleFloorPrefab, "Prefabs/Terrain/PF_Triangle_Floor");
        TryLoadIfNull(ref doorClosedPrefab,    "Prefabs/Terrain/PF_DoorClosed");
        TryLoadIfNull(ref doorOpenPrefab,      "Prefabs/Terrain/PF_DoorOpen");

        TryLoadTileIfNull(ref floorTile,       "Tiles/floorTile");
        TryLoadTileIfNull(ref wallTile,        "Tiles/wallTile");
        TryLoadTileIfNull(ref doorClosedTile,  "Tiles/doorClosedTile");
        TryLoadTileIfNull(ref doorOpenTile,    "Tiles/doorOpenTile");

        // --- Root parent for spawned meshes ---
        if (!root)
        {
            var go = GameObject.Find(rootName);
            if (!go) go = new GameObject(rootName);
            root = go.transform;
        }
        root.gameObject.SetActive(true);
    }

    void BuildRuntimeParents()
    {
        floors3D = EnsureChild(root, floorsName);
        walls3D  = EnsureChild(root, wallsName);
        doors3D  = EnsureChild(root, doorsName);
    }

    void InitRng()
    {
        rng = (rngSeed == 0) ? new System.Random() : new System.Random(rngSeed);
    }

    static void TryLoadIfNull(ref GameObject field, string resourcesPath)
    {
        if (!field)
        {
            Debug.Log($"Loading resource {resourcesPath}");
            var loaded = Resources.Load<GameObject>(resourcesPath);
            if (loaded)
            {
                field = loaded;
                Debug.Log($"Success");
            } else
            {
                Debug.Log($"Failure");
            }
        }
    }

    static void TryLoadTileIfNull(ref TileBase tile, string path)
    {
        if (!tile)
        {
            var loaded = Resources.Load<TileBase>(path);
            if (loaded)
                tile = loaded;
            else
                Debug.LogWarning($"[DungeonGenerator] Tile not found at Resources/{path}");
        }
    }
    static Transform EnsureChild(Transform parent, string childName)
    {
        var t = parent.Find(childName);
        if (!t)
        {
            var go = new GameObject(childName);
            t = go.transform;
            t.SetParent(parent, false);
        }
        return t;
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (!Application.isPlaying)
        {
            // light-touch editor wiring
            if (!grid) grid = FindFirstObjectByType<Grid>(FindObjectsInactive.Include);
            if (root && root.parent != null) root.SetParent(null); // keep root at top level
        }
    }
#endif

    // ----------------- Helpers to place content aligned to Grid -----------------

    // Get world center of a cell (x,y) where y is grid Y (mapped to world Z)
    public Vector3 CellCenter(int x, int y, float unitHeight = 1f, float h = 0f)
    {
        // Use Grid to convert cell → world (assumes XY layout; if your Grid uses XZ, adjust accordingly)
        var cell = new Vector3Int(x, y, 0);
        Vector3 world = grid ? grid.CellToWorld(cell) : new Vector3(x, 0f, y);
        // If your vertical height is separate (e.g., cell.height * unitHeight), pass via 'h'
        return new Vector3(world.x + 0.5f, h * unitHeight, world.z + 0.5f);
    }

    // Place a flat floor tile at (x,y) with given height
    public GameObject PlaceFloor(int x, int y, float height = 0f, float unitHeight = 1f)
    {
        if (!floorPrefab) return null;
        var pos = CellCenter(x, y, unitHeight, height);
        var go = Instantiate(floorPrefab, pos, Quaternion.identity, floors3D);
        go.name = $"Floor_{x}_{y}";
        return go;
    }

    // Place a ramp that rises +unitHeight facing +Z (your note) from (x,y)
    public GameObject PlaceRamp(int x, int y, float baseHeight, bool facePlusZ = true, float unitHeight = 1f)
    {
        if (!rampPrefab) return null;
        var pos = CellCenter(x, y, unitHeight, baseHeight);
        var rot = facePlusZ ? Quaternion.identity : Quaternion.Euler(0f, 180f, 0f);
        var go = Instantiate(rampPrefab, pos, rot, floors3D);
        go.name = $"Ramp_{x}_{y}";
        return go;
    }

    // Place a cliff/pillar centered in the cell; scale in Y to match (height)
    public GameObject PlaceCliff(int x, int y, float heightWorld)
    {
        if (!cliffPrefab) return null;
        var pos = CellCenter(x, y, 1f, 0f);
        var go = Instantiate(cliffPrefab, pos, Quaternion.identity, walls3D);
        go.name = $"Cliff_{x}_{y}";
        var s = go.transform.localScale;
        go.transform.localScale = new Vector3(s.x, heightWorld, s.z);
        return go;
    }

    // Place a diagonal wall running corner-to-corner inside the cell.
    // If NE true: runs SW↔NE (u+v=1). If false: runs NW↔SE (u−v axis).
    public GameObject PlaceDiagonalWall(int x, int y, bool NE_Diagonal, float heightWorld = 2f)
    {
        if (!diagonalWallPrefab) return null;
        var pos = CellCenter(x, y, 1f, 0f);
        // default prefab oriented along +Z; rotate 45° to match diagonal and scale
        float yaw = NE_Diagonal ? 45f : -45f;
        var rot = Quaternion.Euler(0f, yaw, 0f);
        var go = Instantiate(diagonalWallPrefab, pos, rot, walls3D);
        go.name = $"DiagWall_{(NE_Diagonal ? "NE" : "NW")}_{x}_{y}";
        // scale height if the prefab expects it on Y:
        var s = go.transform.localScale;
        go.transform.localScale = new Vector3(s.x, heightWorld, s.z);
        return go;
    }

    // Place a door (closed/open) at a cell edge, oriented by cardinal direction.
    public GameObject PlaceDoor(int x, int y, DirFlags edge, bool open, float height = 0f, float unitHeight = 1f)
    {
        var prefab = open ? doorOpenPrefab : doorClosedPrefab;
        if (!prefab) return null;

        var center = CellCenter(x, y, unitHeight, height);

        // Offset to the edge center and rotate to face across the edge
        Vector3 offset = Vector3.zero;
        float yaw = 0f;
        const float half = 0.5f;

        if (edge.HasFlag(DirFlags.N)) { offset = new Vector3(0f, 0f, +half); yaw = 0f; }
        else if (edge.HasFlag(DirFlags.S)) { offset = new Vector3(0f, 0f, -half); yaw = 180f; }
        else if (edge.HasFlag(DirFlags.E)) { offset = new Vector3(+half, 0f, 0f); yaw = 90f; }
        else if (edge.HasFlag(DirFlags.W)) { offset = new Vector3(-half, 0f, 0f); yaw = -90f; }

        var go = Instantiate(prefab, center + offset, Quaternion.Euler(0f, yaw, 0f), doors3D);
        go.name = $"Door_{edge}_{x}_{y}";
        return go;
    }
}
