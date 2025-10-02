using System;
using UnityEngine;


//[RequireComponent(typeof(BreadcrumbTrail))]
public class Agent : MonoBehaviour
{
    // ==============================================================
    // An Agent is a character.  Specific types inherit these behaviors
    //   PlayerAgent is a member of the player's party.
    //   NPCAgent is a non-player-charactger (friendly / neutral / hostile).

    //public String name;   // already inherited from MonoBehavior
    //public bool enabled;  // already inherited from MonoBehavior

    public int id;                          // unique id number

    // controls map movement:
    public float baseSpeed = 6.0f;          // W/S movement world units per second
    public float turnSpeedDegPerSec = 180f; // A/D rotate speed
    public float radius = 0.30f;            // collision radius inside a 1x1 cell

    // current status
    public Vector3 pos3;// => new() { x=pos2.x, y=pos2.y, z=height};
    public Vector2 pos2;
    public int height;
    public float yawDeg;

    // next crumb in trail we are following
    public Crumb next_crumb;

    // add other properties...
    public Color color1;// = Color.black;  // top color
    public Color color2;// = Color.white;  // bottom color (or outline)
    public int healthPoints;
    public String racialType;           // Humanoid, Shifter, Animal, Monster
    public String race;                 // Human, Elf, Werewolf, Cat, Mimic, etc.
    public BreadcrumbTrail trail;       // 
    public bool trailLeader = false;    //
    public bool trailFollower = false;  //

    protected virtual void Awake()
    {
        trail = GetComponent<BreadcrumbTrail>();
    }

    protected virtual void Start()
    {

    }

    protected virtual void Update()
    {
        // Leave crumbs
        trail.RecordIfNeeded();
    }
}
