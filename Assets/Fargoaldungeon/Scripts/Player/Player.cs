using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;

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

    public GameObject PackGameObject;   // Assign your parent GameObject in the Inspector
    //public GameObject DogPrefab;        // Optional: prefab to give each agent a visible model

    public Pack pack;                   // pack structure

    [Header("Current Player position")]
    public Agent agent;             // Everything to do with the currently active player
    //public Vector2 pos2;          // XY or XZ (depending on useXZPlane)
    //public float yawDeg;          // facing yaw in degrees (around Z for XY, around Y for XZ)
    //public int floorHeight = 1;   // height of current tile.

    [Header("Unique Agent Parameters")]
    public float baseSpeed = 6.0f;       // W/S movement world units per second
    public float turnSpeedDegPerSec = 180f;     // A/D rotate speed
    [Range(0.1f, 0.49f)]
    public float radius = 0.30f;         // collision radius inside a 1x1 cell
    public Color color1 = Color.black;  // top color
    public Color color2 = Color.white;  // bottom color (or outline)

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

    [HideInInspector]
    public bool camera_refresh_needed = true;   // self-clears after camera updates

    // Tuning internal parameters
    [HideInInspector]
    public bool useXZPlane = false;      // false = XY floor (tilemap), true = XZ floor (3D)
    [HideInInspector]
    public int constraintIters = 3;      // how many passes to resolve against edges

    public void Awake()
    {
        // if references are missing, find them.
        if (!gen)
            gen = FindAnyObjectByType<DungeonGenerator>();
        if (!bottomBanner)
            bottomBanner = FindAnyObjectByType<BottomBanner>();
        //agent = new();
    }

    void Start()
    {
        StartCoroutine(DetermineStartPosition());   // background task waits for generator to complete before choosing starting location
        Move_Start();           // grab initial position from Unity object
                                //agent.trail = GetComponent<BreadcrumbTrail>();
                                //BuildPackObjects(3);    // This exists in Pack class.
        pack.packList.Add(pack.PackLeader); // leader agent needs to be added to the packlist.
        ChangePlayerAgent(pack.PackLeader);
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

        // randomly pick a start location.
        int x = Mathf.FloorToInt(agent.pos2.x);
        int y = Mathf.FloorToInt(agent.pos2.y);
        x = -1; // Debug: Force a move
        y = -1;
        //TODO fix this to use Heightmap instead of cellGrid
        while ((!gen.In(x, y)) || (gen.cellGrid[x, y].room_number < 0))
        {
            // try a new random location
            x = UnityEngine.Random.Range(0, gen.cfg.mapWidth);
            y = UnityEngine.Random.Range(0, gen.cfg.mapHeight);
        }
        agent.pos2.x = x + 0.5f;  // center of cell
        agent.pos2.y = y + 0.5f;
        agent.height = gen.cellGrid[x, y].height + (int)heightCorrection;  // height of current cell floor.
        TransformPosition();    // move the player

        Debug.Log($"Set StartPosition to {agent.pos2.x}, {agent.pos2.y}, {agent.height}");
    }

    // Change which agent the player is controlling...
    void ChangePlayerAgent(Agent new_agent)
    {
        agent.DogPrefab.SetActive(true);    // if prefab was hidden by the first-person camera, bring it back
        agent = new_agent;
        agent.camera_refresh_needed = true;   // camera visibility refresh
        Move_Update(0f, 0f);    // screen refresh
    }

    // Change which agent the player is controlling...
    void ChangePlayerAgentByNum(int new_agent_num)
    {
        for (int i = 0; i < pack.packList.Count; i++)
            if (pack.packList[i].id == new_agent_num)
            {
                ChangePlayerAgent(pack.packList[i]);
            }
    }
}