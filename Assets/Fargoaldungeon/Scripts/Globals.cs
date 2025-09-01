using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class Globals : MonoBehaviour
{
    // Reference to each C# class is maintained here
    public CellularAutomata ca;
    public RoomScatterAlgorithms scatter;
    public RoomGrowthAlgorithms growth;
    public Tilemap2D tm2d;
    public RoomCorridors corridors;
    public Room roomclass;
    public Globals global;
    public Door doorclass;
    public DungeonGenerator generator;
    public DungeonSettings cfg;
    public BottomBanner bottomBanner;
    public TimeManager timeManager;
    public HeightMap3DBuilder heightMap3D;

    private System.Random rng;

    // 2D assets defined in Unity
    public Tilemap tilemap;
    public TileBase floorTile;
    public TileBase wallTile;
    public TileBase doorClosedTile;
    public TileBase doorOpenTile;

    // 3D assets defined in Unity
    public Grid grid;                         // same Grid as the 2D Tilemap
    public GameObject floorPrefab;
    public GameObject rampPrefab;             // oriented to face +Z
    public GameObject cliffPrefab;            // a 1x1x1 pillar you can scale in Y
    public GameObject doorClosedPrefab;
    public GameObject doorOpenPrefab;
    public Transform root;                    // parent for spawned meshes

    public GameObject diagonalWallPrefab;    // thin strip or quad oriented along +Z

    // Master list of Rooms makes the current map
    public List<Room> rooms = new(); // Master List of rooms including list of points and metadata


    // These global lists help lookup things quickly 
    public HashSet<Vector2Int> floor_hash_map = new();
    public HashSet<Vector2Int> wall_hash_map = new();
    Dictionary<int, Door> doorById; // partner lookup and save/load

    private void Awake()
    {
        // initialize references
    }
}
