using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public partial class DungeonGenerator : MonoBehaviour
{

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
    public GameObject diagonalWallPrefab;    // thin strip or quad oriented along +Z
    public GameObject doorClosedPrefab;
    public GameObject doorOpenPrefab;
    public Transform root;                    // parent for spawned meshes



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
