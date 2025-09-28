using System;
using Unity.Mathematics;
using UnityEngine;

public partial class Player : MonoBehaviour
{
    void Move_Start()
    {
        var p = transform.position;   // grab object position and set it in variable
        if (useXZPlane) pos2 = World_to_Map(new Vector2(p.x, p.z));
        else            pos2 = World_to_Map(new Vector2(p.x, p.y));

        // Initialize yaw from current rotation
        yawDeg = useXZPlane ? transform.eulerAngles.y - yawCorrection : transform.eulerAngles.z - yawCorrection;
    }

    // called by Input_Update to move in response to inputs.
    void Move_Update(float turn, float thrust)
    {
        // round to nearest .01 to reduce cumulative errors.
        CleanupFloat(ref turn, false);
        CleanupFloat(ref thrust);
        Cleanup(ref pos2);

        if (Math.Abs(turn) > 1e-10f)
        {
            // Rotate the Player
            yawDeg += turn * turnSpeedDegPerSec * Time.deltaTime;
            CleanupFloat(ref yawDeg);

            // commit rotation ALWAYS (even if thrust == 0)
            if (useXZPlane) transform.rotation = Quaternion.Euler(0f, yawDeg + yawCorrection, 0f);
            else transform.rotation = Quaternion.Euler(0f, 0f, yawDeg + yawCorrection);
        }
        else // player not rotating
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
        Cleanup(ref fwd2);

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
        Cleanup(ref pos2);

        if (useXZPlane)
        {
            Vector3 t; // = transform.position; // not necessary, we overwrite this value completely
            Vector2 t_World = Map_to_World(pos2);
            t.x = t_World.x; t.z = t_World.y; // XZ location
            t.y = floorHeight + 1;
            transform.position = t;
            //transform.rotation = Quaternion.Euler(0f, yawDeg, 0f); // rotate around Y for 3D
        }
        else
        {
            Vector3 t; // = transform.position; // not necessary, we overwrite this value completely
            Vector2 t_World = Map_to_World(pos2);
            t.x = t_World.x; t.y = t_World.y; // XY location
            t.z = floorHeight + 1;
            transform.position = t;
            //transform.rotation = Quaternion.Euler(0f, 0f, yawDeg); // rotate around Z for XY
        }
    }

    // ---- Grid constraint solver against DirFlags walls/doors ----
    Vector2 ResolveGridConstraints(Vector2 from, Vector2 to, float r, int maxIters)
    {
        Cleanup(ref from);
        Cleanup(ref to);

        int W = gen.cfg.mapWidth;
        int H = gen.cfg.mapHeight;

        int from_i;
        int from_j;

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
            //if (iter > 0) Debug.Log($"Iteration {iter}: from={from.x},{from.y} to={to.x},{to.y}");    // Debug

            // Recompute cell after bounds clamp
            from_i = Mathf.FloorToInt(from.x);
            from_j = Mathf.FloorToInt(from.y);

            // Define the maximum we allow movement in each direction.
            float cxmin = Mathf.Max(from_i - 1f + r, 0 + r);  
            float cxmax = Mathf.Min(from_i + 2f - r, W - r);  // big enough to get into neighbor cell, but not through it without checking next iteration first.
            float cymin = Mathf.Max(from_j - 1f + r, 0 + r);  // also clamp at world bounds
            float cymax = Mathf.Min(from_j + 2f - r, H - r);

            // debug display:
            //var c = gen.cellGrid[from_i, from_j];                                     // Debug
            //Debug.Log($"pos={from},{from}  Walls={c.walls}, Doors={c.doors}");    // Debug

            // Apply edge block constraints for current 'from'
            if (EdgeBlocked(from_i, from_j, DirFlags.E)) cxmax = Mathf.Min(cxmax, from_i + 1f - r);
            if (EdgeBlocked(from_i, from_j, DirFlags.W)) cxmin = Mathf.Max(cxmin, from_i + 0f + r);
            if (EdgeBlocked(from_i, from_j, DirFlags.N)) cymax = Mathf.Min(cymax, from_j + 1f - r);
            if (EdgeBlocked(from_i, from_j, DirFlags.S)) cymin = Mathf.Max(cymin, from_j + 0f + r);

            //Debug.Log($"p={from_i},{from_j} cxmin/max={cxmin}-{cxmax} cymin/max={cymin}-{cymax}");    // Debug

            Vector2 on_the_way = new Vector2(        // move toward destination (within clamps)
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
            from = on_the_way; // Advance 'from' to the current position for next iteration
            final = on_the_way; // set final position in case we are at last iteration
        }

        Cleanup(ref final);
        return final;    // final is how far we were able to move 'to' within wall limits
    }

    // Is walking in this direction blocked by anything? (walls, closed doors, and end-of-walls)
    bool EdgeBlocked(int i, int j, DirFlags dir)
    {
        var c = gen.cellGrid[i, j];
        bool hasWall = (c.walls & dir) != 0;

        bool hasDoor = (c.doors & dir) != 0;
        bool doorOpen = hasDoor && GetDoorOpenState(i, j, dir);

        DirFlags endWallBlockers = EndOfWallBlockers(pos2, i, j);
        bool blockedByEndWall = (endWallBlockers & dir) != 0;

        //Debug.Log($"EdgeBlocked({i}, {j}, dir={dir} = {wall})");
        if (hasDoor) return !doorOpen; // door present → blocked if closed
        return (hasWall || blockedByEndWall);
    }

    // Prevent walking into the end of a thin wall:
    //   If we are close to an edge of the current cell, then block to the left and right if there is a wall end there.
    //   This also prevents walking off the edge of a door, which is covered as a subset of this check.
    DirFlags EndOfWallBlockers(Vector2 pos2, int i, int j)
    {
        Cell cell_off_grid = new(-1,-1);  // use this for off-grid cells (no walls or doors set)
        cell_off_grid.doors = DirFlags.None;
        cell_off_grid.walls = DirFlags.None;

        Cleanup(ref pos2); // eliminate cumulative errors

        float InGrid_x = pos2.x % 1f;   // WARNING: results in inexact fractions
        float InGrid_y = pos2.y % 1f;
        float One_Minus_Radius = 1f - radius;
        CleanupFloat(ref InGrid_x);
        CleanupFloat(ref InGrid_y); 

        // Which edges of current tile is the player near to?
        bool S_Edge = (InGrid_y < radius);
        bool N_Edge = (InGrid_y > One_Minus_Radius);
        bool W_Edge = (InGrid_x < radius);
        bool E_Edge = (InGrid_x > One_Minus_Radius);
        Debug.Log($"{pos2}->{InGrid_x},{InGrid_y} in {i},{j}  Near Edges: N={N_Edge}, S={S_Edge}, W={W_Edge}, E={E_Edge}.  radius={radius}, 1-radius={One_Minus_Radius}");

        // grab a cell to each side of current cell, using dummy cell when off-grid
        Cell C_South = gen.In(i,j-1) ? gen.cellGrid[i, j - 1] : cell_off_grid;
        Cell C_North = gen.In(i,j+1) ? gen.cellGrid[i, j + 1] : cell_off_grid;
        Cell C_West = gen.In(i-1,j) ? gen.cellGrid[i - 1, j] : cell_off_grid;
        Cell C_East = gen.In(i+1,j) ? gen.cellGrid[i + 1, j] : cell_off_grid;

        // Do we have the end of a wall in this direction? Initialize to no
        bool S_End_Wall = false;
        bool N_End_Wall = false;
        bool W_End_Wall = false;
        bool E_End_Wall = false;

        // If we are by the edge of the cell, look right and left along that same edge for a wall or door.
        if (E_Edge)
        {
            S_End_Wall = ((C_South.walls | C_South.doors) & DirFlags.E) != 0;
            N_End_Wall = ((C_North.walls | C_North.doors) & DirFlags.E) != 0;
        }
        if (W_Edge)
        {
            S_End_Wall = ((C_South.walls | C_South.doors) & DirFlags.W) != 0;
            N_End_Wall = ((C_North.walls | C_North.doors) & DirFlags.W) != 0;
        }
        if (N_Edge)
        {
            W_End_Wall = ((C_West.walls | C_West.doors) & DirFlags.N) != 0;
            E_End_Wall = ((C_East.walls | C_East.doors) & DirFlags.N) != 0;
        }
        if (S_Edge)
        {
            W_End_Wall = ((C_West.walls | C_West.doors) & DirFlags.S) != 0;
            E_End_Wall = ((C_East.walls | C_East.doors) & DirFlags.S) != 0;
        }

        // create DirFlags for all the end walls that would get in our way in that direction.
        DirFlags End_Walls = (N_End_Wall ? DirFlags.N : 0)
                           | (S_End_Wall ? DirFlags.S : 0)
                           | (W_End_Wall ? DirFlags.W : 0)
                           | (E_End_Wall ? DirFlags.E : 0);

        Debug.Log($"End_Walls = {End_Walls}");
        return End_Walls;
    }

    // If we are near a cardinal direction, tweak yaw to go exactly the cardinal direction
    public float SnapToCardinals(float yawDeg, float snapToCardinalDegrees = 10f)
    {
        // Normalize into [0,360)
        yawDeg = Mathf.Repeat(yawDeg, 360f);
        //Debug.Log($"yawDeg = {yawDeg}");

        // Cardinal angles
        float[] cardinals4 = { 0f, 90f, 180f, 270f };
        float[] cardinals8 = { 0f, 45f, 90f, 135f, 180f, 225f, 270f, 315f };
        float[] cardinals;

        // support snapping to diagonals if configured in parameters.
        if (snapEightWay) cardinals = cardinals8;
        else              cardinals = cardinals4;

        foreach (float c in cardinals)
        {
            if (Mathf.Abs(Mathf.DeltaAngle(yawDeg, c)) <= snapToCardinalDegrees)
                return c; // Snap!
        }

        return yawDeg; // leave unchanged if no snap
    }

    // Rounds a number to nearest .01 to eliminate tiny cumulative errors
    // Option to keep the destination within the same integer value
    void CleanupFloat(ref float num, bool same_tile = true)
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
    void Cleanup(ref Vector2 vect, bool same_tile = true)
    {
        CleanupFloat(ref vect.x, same_tile);
        CleanupFloat(ref vect.y, same_tile);
    }

    // apply offset from world coordinates to map coordinates
    Vector2 World_to_Map(Vector2 world_loc)
    {
        Vector2 map_loc;
        map_loc.x = world_loc.x + xCorrection;
        map_loc.y = world_loc.y + yCorrection;
        return map_loc;
    }

    // apply offset from map coordinates to world coordinates
    Vector2 Map_to_World(Vector2 map_loc)
    {
        Vector2 world_loc;
        world_loc.x = map_loc.x - xCorrection;
        world_loc.y = map_loc.y - yCorrection;
        return world_loc;
    }
   
    // ---- Stubs to wire into systems later ----

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
