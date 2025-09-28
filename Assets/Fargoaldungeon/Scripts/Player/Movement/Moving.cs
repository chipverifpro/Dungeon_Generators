using System;
using UnityEngine;

public partial class Player : MonoBehaviour
{
    void Move_Start()
    {
        var p = transform.position;   // grab object position and set it in variable
        if (useXZPlane) pos2 = new Vector2(p.x + xCorrection, p.z + yCorrection);
        else pos2 = new Vector2(p.x + xCorrection, p.y + yCorrection);

        // Initialize yaw from current rotation
        yawDeg = useXZPlane ? transform.eulerAngles.y - yawCorrection : transform.eulerAngles.z - yawCorrection;
    }

    // called by Input_Update to move in response to inputs.
    void Move_Update(float turn, float thrust)
    {
        if (Math.Abs(turn) > 1e-10f)
        {
            // Rotate the Player
            yawDeg += turn * turnSpeedDegPerSec * Time.deltaTime;

            // commit rotation ALWAYS (even if thrust == 0)
            if (useXZPlane) transform.rotation = Quaternion.Euler(0f, yawDeg + yawCorrection, 0f);
            else transform.rotation = Quaternion.Euler(0f, 0f, yawDeg + yawCorrection);
        }
        else
        {
            if (Math.Abs(thrust) > 1e-5f)    // only snap if turning=false but moving=true
            {
                yawDeg = SnapToCardinals(yawDeg, snapToCardinalDegrees);
                if (useXZPlane) transform.rotation = Quaternion.Euler(0f, yawDeg + yawCorrection, 0f);
                else transform.rotation = Quaternion.Euler(0f, 0f, yawDeg + yawCorrection);
            }
        }
        // done with turning, next is moving...

        // Forward direction unit vector in 2D plane
        float yawRad = - yawDeg * Mathf.Deg2Rad;
        Vector2 fwd2 = new Vector2(Mathf.Cos(yawRad), Mathf.Sin(yawRad)); // XY forward (or XZ’s X/Z)

        // Desired 2D motion (no strafing here)
        Vector2 desiredDir2 = fwd2 * Mathf.Clamp(thrust, -1f, 1f);
        float speed = baseSpeed * SampleSlopeMultiplier(pos2, desiredDir2);

        // 2) Integrate and resolve against grid edges
        Vector2 p_from = pos2;  // pos2 is the Vector2 of the player location
        Vector2 p_to = p_from + desiredDir2 * speed * Time.deltaTime;
        Vector2 p_new = ResolveGridConstraints(p_from, p_to, radius, constraintIters);

        // 3) Commit position & rotation to Transform
        pos2 = p_new;

        TransformPosition();
    }

    void TransformPosition()
    {
        if (useXZPlane)
        {
            var t = transform.position;
            t.x = pos2.x - xCorrection; t.z = pos2.y - yCorrection; // note: pos2.y -> world Z
            t.y = floorHeight + 1;
            transform.position = t;
            //transform.rotation = Quaternion.Euler(0f, yawDeg, 0f); // rotate around Y for 3D
        }
        else
        {
            var t = transform.position;
            t.x = pos2.x - xCorrection; t.y = pos2.y - yCorrection; // XY floor
            t.z = floorHeight + 1;
            transform.position = t;
            //transform.rotation = Quaternion.Euler(0f, 0f, yawDeg); // rotate around Z for XY
        }
    }
    // ---- Grid constraint solver against DirFlags walls/doors ----
    // TODO: handle condition where we are in a doorway, don't allow moving sideways out of cell
    Vector2 ResolveGridConstraints(Vector2 from, Vector2 to, float r, int maxIters)
    {
        //Vector2 p = from;
        int W = gen.cfg.mapWidth;
        int H = gen.cfg.mapHeight;

        int from_i;
        int from_j;
        int to_i;
        int to_j;

        Vector2 final = from;   // initialize with current position

        // clamp destination to world bounds
        float xmin = r, xmax = W - r;
        float ymin = r, ymax = H - r;
        to.x = Mathf.Clamp(to.x, xmin, xmax);   
        to.y = Mathf.Clamp(to.y, ymin, ymax);

        if ((from - to).sqrMagnitude < 1e-10f)  
                return to; // not moving

        // iterate if we cross into a new tile so we always use correct wall bounds.
        for (int iter = 0; iter < maxIters; iter++)
        {
            //if (iter > 0) Debug.Log($"Iteration {iter}: from={from.x},{from.y} to={to.x},{to.y}");

            // Done already outside loop: Clamp to map bounds first (contracted by radius)
            //xmin = r, xmax = W - r;
            //ymin = r, ymax = H - r;
            //p.x = Mathf.Clamp(p.x, xmin, xmax);
            //p.y = Mathf.Clamp(p.y, ymin, ymax);

            // Recompute cell after bounds clamp
            from_i = Mathf.FloorToInt(from.x);
            from_j = Mathf.FloorToInt(from.y);
            to_i = Mathf.FloorToInt(to.x);
            to_j = Mathf.FloorToInt(to.y);

            // already clamped....
            //if ((uint)i >= (uint)W || (uint)j >= (uint)H) break; // outside, nothing else to do

            // Base contracted cell box
            float cxmin;// = i + r, 
            float cxmax;// = (i + 1) - r;
            float cymin;// = j + r, 
            float cymax;// = (j + 1) - r;

            cxmin = Mathf.Max(from_i - 1f + r, 0 + r);  
            cxmax = Mathf.Min(from_i + 2f - r, W - r);  // big enough to get into neighbor cell, but not through it without checking next iteration first.
            cymin = Mathf.Max(from_j - 1f + r, 0 + r);  // also clamp at world bounds
            cymax = Mathf.Min(from_j + 2f - r, H - r);

            // debug display
            var c = gen.cellGrid[from_i, from_j];
            //Debug.Log($"pos={from_i},{from_j}  Walls={c.walls}, Doors={c.doors}");

            // Apply edge block constraints for current from     // why is j-r the limit, not j+1-r
            if (EdgeBlocked(from_i, from_j, DirFlags.E)) cxmax = Mathf.Min(cxmax, from_i + 1f - r);
            if (EdgeBlocked(from_i, from_j, DirFlags.W)) cxmin = Mathf.Max(cxmin, from_i + 0f + r); //
            if (EdgeBlocked(from_i, from_j, DirFlags.N)) cymax = Mathf.Min(cymax, from_j + 1f - r); //
            if (EdgeBlocked(from_i, from_j, DirFlags.S)) cymin = Mathf.Max(cymin, from_j + 0f + r);

            //Debug.Log($"p={from_i},{from_j} cxmin/max={cxmin}-{cxmax} cymin/max={cymin}-{cymax}");

            Vector2 on_the_way = new Vector2(        // move toward destination within clamps
                Mathf.Clamp(to.x, cxmin, cxmax),
                Mathf.Clamp(to.y, cymin, cymax)
            );

            if ((on_the_way - to).sqrMagnitude < 1e-10f)     // are we at destination?
            {
                final = on_the_way;
                break;              // arrived, no more iterations
            }

            if ((from - to).sqrMagnitude < 1e-10f)     // did we move this iteration?
            {
                final = on_the_way;
                break;      // settled (no change this iteration)
            }
            from = on_the_way; // Advance from to current position for next iteration
            final = on_the_way; // in case we are at last iteration
        }

        return final;    // from has been advanced towards 'to' within wall limits
    }

    bool EdgeBlocked(int i, int j, DirFlags dir)
    {
        var c = gen.cellGrid[i, j];
        bool wall = (c.walls & dir) != 0;

        bool hasDoor = (c.doors & dir) != 0;
        bool doorOpen = hasDoor && GetDoorOpenState(i, j, dir);

        DirFlags doorblockers = InDoorwayBlockers(pos2, i, j);
        bool blockedByDoorway = (doorblockers & dir) != 0;

        //Debug.Log($"EdgeBlocked({i}, {j}, dir={dir} = {wall})");
        if (hasDoor) return !doorOpen; // door present → blocked if closed
        return (wall || blockedByDoorway);
    }

    // If we are in a doorway close to edge of cell that has a door,
    //    then return blockers to right and left of that
    //    preventing exiting the door into the edge of a wall.
    DirFlags InDoorwayBlockers(Vector2 pos2, int i, int j)
    {
        DirFlags blockers = DirFlags.None;
        Cell c = gen.cellGrid[i, j];
        if (true)
        {
            if ((c.doors & DirFlags.S) != DirFlags.None)
                if ((pos2.y % 1f) < radius)
                    blockers |= ((~c.doors) & (DirFlags.E | DirFlags.W));
            if ((c.doors & DirFlags.N) != DirFlags.None)
                if ((pos2.y % 1f) > (1f - radius))
                    blockers |= ((~c.doors) & (DirFlags.E | DirFlags.W));
            if ((c.doors & DirFlags.W) != DirFlags.None)
                if ((pos2.x % 1f) < radius)
                    blockers |= ((~c.doors) & (DirFlags.N | DirFlags.S));
            if ((c.doors & DirFlags.E) != DirFlags.None)
                if ((pos2.x % 1f) > (1f - radius))
                    blockers |= ((~c.doors) & (DirFlags.N | DirFlags.S));
        }
        return blockers;
    }

    public static float SnapToCardinals(float yawDeg, float snapToCardinalDegrees = 10f)
    {
        // Normalize into [0,360)
        yawDeg = Mathf.Repeat(yawDeg, 360f);
        //Debug.Log($"yawDeg = {yawDeg}");

        // Cardinal angles
        float[] cardinals = { 0f, 90f, 180f, 270f };

        foreach (float c in cardinals)
        {
            if (Mathf.Abs(Mathf.DeltaAngle(yawDeg, c)) <= snapToCardinalDegrees)
                return c; // Snap!
        }

        return yawDeg; // leave unchanged if no snap
    }
    // ---- Stubs you can wire into your systems later ----

    // Return whether the door on edge (i,j,dir) is open. For now: consider doors open by default.
    bool GetDoorOpenState(int i, int j, DirFlags dir)
    {
        // TODO: hook into your Door objects on cells
        return true;
    }

    // Use height/tilt/travel_cost for real speed scaling later.
    float SampleSlopeMultiplier(Vector2 pos, Vector2 dir)
    {
        // e.g., read gen.cellGrid[floorX,floorY].travel_cost, tiltFloor, etc.
        // return uphill ? slopeUphillFactor : downhill ? slopeDownhillFactor : 1f;
        return 1f;
    }
}
