using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

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

    // global variables for return of success and failure results (some functions only)
    [HideInInspector] public bool success;    // global generic return value from various tasks
    [HideInInspector] public string failure;    // global failure description string
    
    // list of directions for neighbor checks
    public Vector2Int[] directions_xy = { Vector2Int.up,
                                   Vector2Int.down,
                                   Vector2Int.left,
                                   Vector2Int.right, };
                            //       Vector2Int.up + Vector2Int.left,
                            //       Vector2Int.up + Vector2Int.right,
                            //       Vector2Int.down + Vector2Int.left,
                            //       Vector2Int.down + Vector2Int.right };
                            
    private void Awake()
    {
        // initialize references
    }
}
