using System;
using System.Collections.Generic;
using System.Data.Common;
using UnityEngine;


[System.Serializable]
public class Crumb
{
    public Vector3 position;        // point creator was at
    public float yawDeg;       // angle player wasw at: helps followers turn?
    public List<int> whichFollowersArrived;
}

[DisallowMultipleComponent]
public class BreadcrumbTrail : MonoBehaviour
{
    [Header("Breadcrumb Trail (for leader)")]
    [Tooltip("Drop a new crumb when we've moved at least this far since last drop.")]
    public float dropDistance = 0.5f;

    [Tooltip("Hard cap on stored crumbs (acts as ring buffer ceiling).")]
    public int maxCrumbs = 256;

    public Agent leader;                // who is making the trail
    public List<Agent> followers;       // who is following the trail (in order)
    public int numFollowers => followers.Count;     // shortcut


    public List<Crumb> crumbs = new List<Crumb>(256);
    private Vector3 lastDropPos;
    private bool hasAny = false;

    void Awake()
    {
        hasAny = false;
        if (followers == null) followers = new();
    }

    void Update()
    {
        RecordIfNeeded();
    }

    /// Call once per frame by the owner to record position if moved enough.
    /// Can be forced in the case of a sharp turn that we want included.
    public void RecordIfNeeded(bool forceDrop = false)
    {
        if (numFollowers == 0) return;

        if (!hasAny)
        {
            AddCrumb();
            lastDropPos = leader.pos3;
            hasAny = true;
            return;
        }

        if (forceDrop && (leader.pos3 != lastDropPos))
        {
            AddCrumb();
            lastDropPos = leader.pos3;
            return;
        }

        if ((leader.pos3 - lastDropPos).sqrMagnitude >= dropDistance * dropDistance)
        {
            AddCrumb();
            lastDropPos = leader.pos3;
        }
    }

    private void AddCrumb()
    {
        if (crumbs == null) crumbs = new();

        if (crumbs.Count >= maxCrumbs)
        {
            // Drop oldest when full
            crumbs.RemoveAt(0);
        }
        Crumb new_crumb = new() { position = leader.pos3, yawDeg = leader.yawDeg };
        crumbs.Add(new_crumb);
    }

    /// Returns the newest crumb if any; else returns current transform position.
    public Vector3 GetLatestPositionFallback()
    {
        if (crumbs.Count > 0) return crumbs[crumbs.Count - 1].position;
        return leader.pos3;
    }

    public void AddFollower(Agent agent)
    {
        FindFollowerIndex(agent, addIfNotFollowing: true); // if not found, aadds missing follower
    }

    public void RemoveFollower(Agent agent)
    {
        int index = FindFollowerIndex(agent, addIfNotFollowing: false);
        if (index >= 0)
        {
            followers.RemoveAt(index);
            if (followers.Count == 0)   // if nobody left, clear the crumbs trail
            {
                crumbs.Clear();
                hasAny = false;
            }
        }
    }

    public int FindFollowerIndex(Agent agent, bool addIfNotFollowing = true)
    {
        int eater_index;
        int eater_id = agent.id;

        if (followers == null) followers = new();

        for (eater_index = 0; eater_index < numFollowers; eater_index++)
        {
            if (eater_id == followers[eater_index].id)
            {
                break;
            }
        }
        if (eater_index == numFollowers)
        {
            // eater not found
            if (addIfNotFollowing)
            {
                // add the follower.
                followers.Add(agent);
            }
            else
            {
                // or, return not found
                return -1;
            }
        }
        return eater_index;
    }

    public Vector3 GetNextCrumb(Agent agent)
    {
        int eater_index;
        int crumb_index;

        eater_index = FindFollowerIndex(agent);

        // scan through the crumb list to find the first one that the eater has not eaten
        for (crumb_index = 0; crumb_index < crumbs.Count; crumb_index++)
        {
            ///if (crumbs[crumb_index].position == lastEaten[eater_index])
            if (!crumbs[crumb_index].whichFollowersArrived.Contains(eater_index))
            {
                // we located the last crumb this follower ate.  Now give follower the next one    
                agent.next_crumb = crumbs[crumb_index];
                // update the crumb to know it was eaten.
                crumbs[crumb_index].whichFollowersArrived.Add(eater_index);
                // if every follower has eaten here, remove crumb from trail.
                if (crumbs[crumb_index].whichFollowersArrived.Count == followers.Count)
                {
                    crumbs.RemoveAt(crumb_index);
                }
                // return that position
                return agent.next_crumb.position;
            }
        }
        // not found, so create a temporary crumb at current leader position
        // does not add to trail.
        Crumb new_crumb = new()
        {
            position = leader.pos3,
            yawDeg = leader.yawDeg,
            whichFollowersArrived = new() { eater_index }
        };
        agent.next_crumb = new_crumb;
        return agent.next_crumb.position;
    }
}
