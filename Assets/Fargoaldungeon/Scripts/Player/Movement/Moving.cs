using UnityEngine;
using System;

public partial class Player : MonoBehaviour
{
    /*
    public void StepFreeButGridConstrained(
    ref Pose2 pose,
    Vector2 desiredDirWorld,   // normalized or not
    float dt,
    AgentParams agent)
    {
        if (desiredDirWorld.sqrMagnitude < 1e-6f) return;

        // 1) move intent
        float speed = agent.baseSpeed * SampleSlopeMultiplier(pose.p, desiredDirWorld);
        Vector2 d = Vector2.ClampMagnitude(desiredDirWorld, 1f) * speed * dt;
        Vector2 p0 = pose.p;
        Vector2 p1 = p0 + d;

        // 2) resolve against grid edges (current + up to 2 neighbor hops)
        p1 = ResolveGridConstraints(p0, p1, agent.radius, maxIters: 3);

        // 3) commit
        pose.p = p1;
        if (d.sqrMagnitude > 1e-6f) pose.yaw = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg; // optional: face motion
    }
    */

    void Start()
    {
        Input_Start();
        //Vector3 p = transform.position;
        //posXY = new Vector2(p.x, p.y); // XY is the floor plane in your project
    }

    void Update()
    {
        Input_Update();
        /*
        if (gen == null || gen.cellGrid == null) return;

        // 1) gather keyboard input -> desired world direction (XY)
        Vector2 input = GetMovementInput();     // in Inputs.cs

        // 2) slope/cost scaling (stubbed: returns 1f now)
        float speed = baseSpeed * SampleSlopeMultiplier(posXY, input);

        // 3) integrate
        Vector2 p0 = posXY;
        Vector2 p1 = p0 + input * speed * Time.deltaTime;

        // 4) resolve against grid walls/doors (no physics)
        p1 = ResolveGridConstraints(p0, p1, radius, constraintIters);

        // 5) commit to transform (XY plane). Keep original Z.
        posXY = p1;
        var t = transform.position;
        t.x = posXY.x; t.z = posXY.y;
        transform.position = t;
        
        if (faceMoveDirection && input.sqrMagnitude > 0.0001f)
        {
            float yawDeg = Mathf.Atan2(input.y, input.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0f, yawDeg - 180f, 0f); // if your model faces +X in XY plane
        }
        BottomBanner.Show($"x = {posXY.x}, y = {posXY.y}");
        */
    }

    // ---- Grid constraint solver against DirFlags walls/doors ----
    Vector2 ResolveGridConstraints(Vector2 from, Vector2 to, float r, int maxIters)
    {
        Vector2 p = to;
        int W = gen.cfg.mapWidth;
        int H = gen.cfg.mapHeight;

        for (int iter = 0; iter < maxIters; iter++)
        {
            int i = Mathf.FloorToInt(p.x);
            int j = Mathf.FloorToInt(p.y);

            // Clamp to map bounds first (contracted by radius)
            float xmin = r, xmax = W - r;
            float ymin = r, ymax = H - r;
            p.x = Mathf.Clamp(p.x, xmin, xmax);
            p.y = Mathf.Clamp(p.y, ymin, ymax);

            // Recompute cell after bounds clamp
            i = Mathf.FloorToInt(p.x);
            j = Mathf.FloorToInt(p.y);
            if ((uint)i >= (uint)W || (uint)j >= (uint)H) break; // outside, nothing else to do

            // Base contracted cell box
            float cxmin = i + r, cxmax = (i + 1) - r;
            float cymin = j + r, cymax = (j + 1) - r;

            cxmin = i-1 + r; cxmax = i+2 - r;   // big enough to get into neighbor cell, but not through it without checking next iteration first.
            cymin = j-1 + r; cymax = j+2 - r;

            // debug display
            var c = gen.cellGrid[i, j];
            Debug.Log($"pos={i},{j}  Walls={c.walls}, Doors={c.doors}");

            // Apply edge block constraints     // why is j-r the limit, not j+1-r
            if (EdgeBlocked(i, j, DirFlags.N)) cymax = Mathf.Min(cymax, j + 0 - r);
            if (EdgeBlocked(i, j, DirFlags.S)) cymin = Mathf.Max(cymin, j + 0 + r);
            if (EdgeBlocked(i, j, DirFlags.E)) cxmax = Mathf.Min(cxmax, i + 0 - r);
            if (EdgeBlocked(i, j, DirFlags.W)) cxmin = Mathf.Max(cxmin, i + 0 + r);

            Debug.Log($"p={p.x},{p.y} cxmin/max={cxmin}-{cxmax} cymin/max={cymin}-{cymax}");
            Vector2 corrected = new Vector2(
                Mathf.Clamp(p.x, cxmin, cxmax),
                Mathf.Clamp(p.y, cymin, cymax)
            );

            if ((corrected - p).sqrMagnitude < 1e-10f)
                break; // settled
            p = corrected;
            
        }

        return p;
    }

    bool EdgeBlocked(int i, int j, DirFlags dir)
    {
        var c = gen.cellGrid[i, j];
        bool wall = (c.walls & dir) != 0;

        bool hasDoor = (c.doors & dir) != 0;
        bool doorOpen = hasDoor && GetDoorOpenState(i, j, dir);

        Debug.Log($"EdgeBlocked({i}, {j}, dir={dir} = {wall})");
        if (hasDoor) return !doorOpen; // door present → blocked if closed
        return wall;
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
