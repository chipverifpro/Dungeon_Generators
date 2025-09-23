using UnityEngine;

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

    Vector2 posXY;                       // working XY pose (Z comes from transform)


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
        if (!gen)
            gen = FindAnyObjectByType<DungeonGenerator>();
    }

}