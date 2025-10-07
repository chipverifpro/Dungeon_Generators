using System;
using System.Collections;
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

    DungeonSettings cfg;

    public int id;                          // unique id number

    // controls map movement:
    public float baseSpeed = 6.0f;          // W/S movement world units per second
    public float turnSpeedDegPerSec = 180f; // A/D rotate speed
    public float radius = 0.30f;            // collision radius inside a 1x1 cell

    // current status
    //public Vector3 pos3;// => new() { x=pos2.x, y=pos2.y, z=height};
    public Vector2 pos2;
    public int height;
    public float yawDeg;

    public GameObject DogPrefab;        // Optional: prefab to give each agent a visible model

    public Animator anim;

    // next crumb in trail we are following
    public Crumb next_crumb;

    // add other properties...
    public Color color1;// = Color.black;  // top color
    public Color color2;// = Color.white;  // bottom color (or outline)
    public int healthPoints;
    public String racialType;           // Humanoid, Shifter, Animal, Monster
    public String race;                 // Human, Elf, Werewolf, Cat, Mimic, etc.
    public Pack pack;
    public BreadcrumbTrail trail;       // 
    public bool trailLeader = false;    //
    public bool trailFollower = false;  //
    public bool camera_refresh_needed = true;   // one-time request for vcam to refresh visibility settings

    [Header("Player to Walls adjustment")]
    public float xCorrection = 0.5f;
    public float yCorrection = 0.5f;
    public float yawCorrection = 90f;
    public float heightCorrection = 1f;
    
    // Tuning internal parameters
    
    public bool useXZPlane = true;      // false = XY floor (tilemap), true = XZ floor (3D)

    protected virtual void Awake()
    {
        //trail = GetComponent<BreadcrumbTrail>();
    }

    protected virtual void Start()
    {

        StartCoroutine(CycleAnimations());
    }


    protected virtual void Update()
    {
        if (trailLeader) // Leave crumbs
        {
            trail.RecordIfNeeded();
        }
        if (trailFollower)
        {
            FollowTrail();
        }
    }

    IEnumerator CycleAnimations()
    {
        yield return new WaitForSeconds(.5f);

        while (true)
        {
            anim.SetInteger("AnimationID", UnityEngine.Random.Range(0,15));
            yield return new WaitForSeconds(2);
        }
    }

    void FollowTrail()
    {
        Vector3 originalPos;
        Vector3 agentPos3;
        Vector3 loc_clamped;

        //Vector3 next_crumb_pos;
        float dist_to_next_crumb;
        Vector3 agent_ahead_pos;
        float dist_to_next_agent;
        Vector3 target_pos;
        float dist_to_target;
        float move_credit;

        originalPos = new(pos2.x, pos2.y, height);

        // if we have no valid destination, see if we can get a new crumb, else abort
        if (next_crumb.valid == false)
        {
            next_crumb = trail.GetNextCrumb(this);
            if (next_crumb.valid == false) return; // no trail to follow
        }

        // calculate the move_credit;
        move_credit = baseSpeed * Time.deltaTime;
        //Debug.Log($"Player {name} following trail towards {next_crumb.position}, move_credit={move_credit}");

        //loop until move_credit is gone
        while (move_credit > 0.0001)
        {
            //Debug.Log($"Player {name} following trail towards {next_crumb.position}, move_credit={move_credit}");

            agentPos3 = new(pos2.x, pos2.y, height);
            dist_to_next_crumb = Mathf.Sqrt((agentPos3 - next_crumb.position).sqrMagnitude);
            if (dist_to_next_crumb < .0001)
            {
                // arrived, get next crumb
                next_crumb = trail.GetNextCrumb(this);
                if (next_crumb.valid == false) return;  // arrived at last available crumb.
                dist_to_next_crumb = Mathf.Sqrt((agentPos3 - next_crumb.position).sqrMagnitude);

                if (dist_to_next_crumb < .01) return; // we are also at the next crumb, stop for now.???? not supposed to happen.
            }

            agent_ahead_pos = GetPositionOfAgentBeforeMe(id);
            dist_to_next_agent = Mathf.Sqrt((agent_ahead_pos - agentPos3).sqrMagnitude);
            if (dist_to_next_agent <= 3 * radius)
            {
                // bumped into agent ahead, stop here.
                return;
            }

            // we have two limits, choose the closer one.
            if (dist_to_next_crumb < dist_to_next_agent)
            {
                dist_to_target = dist_to_next_crumb;
                target_pos = next_crumb.position;
            }
            else
            {
                dist_to_target = dist_to_next_agent;
                target_pos = agent_ahead_pos;
            }

            // move up to target, maximum move is move_credit.

            // do the move.  Travel dist towards target position
            if (move_credit < dist_to_target)
            {
                // cannot go all the way, so travel by move_credit distance
                loc_clamped = LerpVector3(agentPos3, target_pos, move_credit / dist_to_target);
                pos2.x = loc_clamped.x;
                pos2.y = loc_clamped.y;
                height = (int)loc_clamped.z;  // TODO: make height a float
                move_credit = 0;
            }
            else
            {
                // we have enough move_credit to go all the way.  Do it and repeat the loop.
                pos2.x = target_pos.x;
                pos2.y = target_pos.y;
                height = (int)target_pos.z;    // TODO: make height a float
                move_credit -= dist_to_target;    // continue while loop getting next crumb
            }
            Vector3 final_dest_pos = new(pos2.x, pos2.y, height);
            Vector3 unit_vector = (final_dest_pos - originalPos).normalized;
            yawDeg = Mathf.Atan2(unit_vector.x, unit_vector.y) * Mathf.Rad2Deg - yawCorrection;

            TransformPosition(this);    // move the agent object to it's new location.
        }
    }

    // Apply Lerp to all 3 dimensions of a vector.
    // Moves character t percent of the way along the vector.
    Vector3 LerpVector3(Vector3 a, Vector3 b, float t)
    {
        Vector3 result;
        result.x = Mathf.Lerp(a.x, b.x, t);
        result.y = Mathf.Lerp(a.y, b.y, t);
        result.z = Mathf.Lerp(a.z, b.z, t);
        return result;
    }

    Vector3 GetPositionOfAgentBeforeMe(int my_id)
    {
        Vector3 ahead_pos = new();
        //int ahead_id = -1;
        int i;

        for (i = 1; i < pack.packList.Count; i++)
        {
            if (pack.packList[i].id == my_id)
                break;
            //ahead_id = pack.packList[i].id;
        }
        ahead_pos.x = pack.packList[i - 1].pos2.x;
        ahead_pos.y = pack.packList[i - 1].pos2.y;
        ahead_pos.z = pack.packList[i - 1].height;
        return ahead_pos;
    }

    // Convert from map location to world location and apply that to the agent's ojbect
    public void TransformPosition(Agent agent)
    {
        Cleanup(ref agent.pos2);

        if (useXZPlane)
        {
            Vector3 t; // = transform.position; // not necessary, we overwrite this value completely
            Vector2 t_World = Map_to_World(agent.pos2);
            t.x = t_World.x; t.z = t_World.y; // XZ location
            t.y = agent.height + 1;
            agent.transform.position = t;
            //pack.player.transform.position = t;
            agent.transform.rotation = Quaternion.Euler(0f, agent.yawDeg + yawCorrection, 0f); // rotate around Y for 3D
            //pack.player.transform.rotation = Quaternion.Euler(0f, agent.yawDeg + yawCorrection, 0f); // rotate around Y for 3D
            if (pack.player.agent == agent)
            {
                pack.player.transform.position = t;
                pack.player.transform.rotation = Quaternion.Euler(0f, agent.yawDeg + yawCorrection, 0f); // rotate around Y for 3D
            }
        }
        else
        {
            Vector3 t; // = transform.position; // not necessary, we overwrite this value completely
            Vector2 t_World = Map_to_World(agent.pos2);
            t.x = t_World.x; t.y = t_World.y; // XY location
            t.z = agent.height + 1;
            agent.transform.position = t;
            //pack.player.transform.position = t;
            agent.transform.rotation = Quaternion.Euler(0f, 0f, agent.yawDeg + yawCorrection); // rotate around Z for XY
            if (pack.player.agent == agent)
            {
                pack.player.transform.position = t;
                pack.player.transform.rotation = Quaternion.Euler(0f, 0f, agent.yawDeg + yawCorrection); // rotate around Y for 3D
            }                                                                                       //pack.player.transform.rotation = Quaternion.Euler(0f, 0f, agent.yawDeg + yawCorrection); // rotate around Z for XY

        }
    }

    // apply offset from map coordinates to world coordinates
    public Vector2 Map_to_World(Vector2 map_loc)
    {
        Vector2 world_loc;
        world_loc.x = map_loc.x - xCorrection;
        world_loc.y = map_loc.y - yCorrection;
        return world_loc;
    }

    // Rounds a number to nearest .01 to eliminate tiny cumulative errors
    // Option to keep the destination within the same integer value
    public void CleanupFloat(ref float num, bool same_tile = true)
    {
        float new_num;
        new_num = Mathf.Round(num * 100f) / 100f;   // round to 0.01

        if (same_tile)      // prevent going into different tile
        {
            float tile_num = Mathf.Floor(num); // tile to stay in

            new_num = Mathf.Clamp(new_num, tile_num, tile_num + 0.99f);
        }
        num = new_num;  // update the ref to the cleaned up num
    }

    // Rounds Vector2 x,y to nearest .01 to eliminate tiny cumulative errors
    public void Cleanup(ref Vector2 vect, bool same_tile = true)
    {
        CleanupFloat(ref vect.x, same_tile);
        CleanupFloat(ref vect.y, same_tile);
    }
}
