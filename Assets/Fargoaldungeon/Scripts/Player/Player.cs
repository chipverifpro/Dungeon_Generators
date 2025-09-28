using System;
using System.Collections;
using UnityEngine;

// TODO list:
//   add movement on diagonal walls
//   DONE add start on map floor tile
//   add height to movement
//   switch to heightmap instead of grid to allow movement with vertical stacking
//   move all params into this file.


public partial class Player : MonoBehaviour
{
    [Header("Refs")]
    public DungeonGenerator gen;         // assign in Inspector (has cellGrid, rooms, etc.)
    public BottomBanner bottomBanner;

    [Header("Current Player position")]
    public Vector2 pos2;          // XY or XZ (depending on useXZPlane)
    public float yawDeg;          // facing yaw in degrees (around Z for XY, around Y for XZ)
    public int floorHeight = 1;   // height of current tile.

    [Header("Unique Agent Parameters")]
    public float baseSpeed = 6.0f;       // W/S movement world units per second
    public float turnSpeedDegPerSec = 180f;     // A/D rotate speed
    [Range(0.1f, 0.49f)]
    public float radius = 0.30f;         // collision radius inside a 1x1 cell

    [Header("Movement")]
    public float snapToCardinalDegrees = 10f;
    public bool snapEightWay = true;            // if false, only snap to 4 cardinal directions
    public float slopeUphillFactor = 0.85f; // (stub) scale speed a bit uphill
    public float slopeDownhillFactor = 1.08f;

    [Header("Player to Walls adjustment")]
    public float xCorrection = 0.5f;
    public float yCorrection = 0.5f;
    public float yawCorrection = 90f;
    public float heightCorrection = 1f;

    // Tuning internal parameters
    [HideInInspector]
    public bool useXZPlane = false;             // false = XY floor (tilemap), true = XZ floor (3D)
    [HideInInspector]
    public int constraintIters = 3;      // how many passes to resolve against edges

    void Awake()
    {
        // if references are missing, find them.
        if (!gen)
            gen = FindAnyObjectByType<DungeonGenerator>();
        if (!bottomBanner)
            bottomBanner = FindAnyObjectByType<BottomBanner>();
    }

    void Start()
    {
        StartCoroutine(DetermineStartPosition());   // background task waits for generator to complete before choosing starting location
        Move_Start();   // grab initial position from Unity object
    }

    void Update()
    {
        Input_Update();  // this is the update for inputs and resulting movement
        // Input_Update will call Move_Update with the appropriate parameters.
    }

    public IEnumerator DetermineStartPosition()
    {
        // wait until build completes
        yield return null;
        yield return new WaitUntil(() => gen.buildComplete);

        // start with default location, and if not a valid floor, randomly pick a new one.
        int x = Mathf.FloorToInt(pos2.x);
        int y = Mathf.FloorToInt(pos2.y);
        x = -1; // Debug: Force a move
        y = -1;
        //TODO fix this to use Heightmap instead of cellGrid
        while ((!gen.In(x, y)) || (gen.cellGrid[x, y].room_number < 0))
        {
            // try a new random location
            x = UnityEngine.Random.Range(0, gen.cfg.mapWidth);
            y = UnityEngine.Random.Range(0, gen.cfg.mapHeight);
        }
        pos2.x = x + 0.5f;  // center of cell
        pos2.y = y + 0.5f;
        floorHeight = gen.cellGrid[x, y].height + (int)heightCorrection;  // height of current cell floor.
        TransformPosition();    // move the player

        Debug.Log($"Set StartPosition to {pos2.x}, {pos2.y}, {floorHeight}");
    }
}