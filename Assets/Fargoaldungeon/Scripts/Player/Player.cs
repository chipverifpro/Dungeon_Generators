using System;
using System.Collections;
using UnityEngine;

// TODO list:
//   add movement on diagonal walls
//   add start on map floor tile
//   add height to movement
//   switch to heightmap instead of grid to allow movement with vertical stacking
//   move all params into this file.


public partial class Player : MonoBehaviour
{
    [Header("Refs")]
    public DungeonGenerator gen;         // assign in Inspector (has cellGrid, rooms, etc.)

    [Header("Movement")]
    public float baseSpeed = 6.0f;       // world units per second
    [Range(0.1f, 0.49f)]
    public float radius = 0.30f;         // collision radius inside a 1x1 cell
    public bool faceMoveDirection = true;

    [Header("Tuning")]
    public int constraintIters = 3;      // how many passes to resolve against edges
    public float slopeUphillFactor = 0.85f; // (stub) scale speed a bit uphill
    public float slopeDownhillFactor = 1.08f;

    //Vector2 posXY;                       // working XY pose (Z comes from transform)
    int floorHeight = 1;

    public struct Pose2             // location and direction of player
    {
        public Vector2 p;           // 2D location of player
        public float height;
        public float yaw;
    }

    public struct AgentParams       // tracks characteristics of player
    {
        public float radius;
        public float baseSpeed;
    }


    void Awake()
    {
        if (!gen)   // if DungeonGenerator is missing, find it.
            gen = FindAnyObjectByType<DungeonGenerator>();
    }

    void Start()
    {
        StartCoroutine(DetermineStartPosition());
        Input_Start();
    }

    void Update()
    {
        Input_Update();  // this is the update for inputs and resulting movement
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