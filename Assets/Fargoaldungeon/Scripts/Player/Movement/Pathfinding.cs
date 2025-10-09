using UnityEngine;
using System;
using System.Collections.Generic;

public partial class Player : MonoBehaviour
{

}

// GPT produced two versions of the Line-of-Sight algorithm.
// One works on tile coordinates within a Room (RoomLOS).


/// // Suppose you have a RoomCells that implements IRoomCells
/// 
/// How to use:
/// 
/// IRoomCells room = currentRoom;
///
/// bool los = LOS2D.HasLineOfSightInRoom(
///    room,
///    startWorldPosition,
///    endWorldPosition,
///    agentRadius: 0.4f // optional: expands walls by ~one cell if cellSize≈0.5
/// );



/// Minimal API your Room/Heightfield should provide.
public interface IRoomCells
{
    /// World-space origin of cell (0,0).
    Vector3 Origin { get; }
    /// Size of a square cell in world units (XZ).
    float CellSize { get; }
    /// Returns true if (cx,cz) is inside this room's cell bounds.
    bool InBounds(int cx, int cz);
    /// Returns true if (cx,cz) is blocked (wall/obstacle/closed door).
    bool IsBlocked(int cx, int cz);
    /// Optional: room test; LOS only valid if both points are in this same room.
    bool ContainsWorld(Vector3 worldXZ);  // treat y ignored
}


public static class LOS2D
{
    /// Line of sight inside a single room grid on the XZ plane.
    /// - Ignores height (Y).
    /// - Uses supercover Bresenham across cells to test blockage.
    /// - agentRadius expands blockers by ceil(radius / cellSize) cells.
    public static bool HasLineOfSightInRoom(IRoomCells room, Vector3 aWorld, Vector3 bWorld, float agentRadius = 0f)
    {
        // Ensure both endpoints are in this room (caller said "only consider within a single Room").
        if (room == null) return false;
        if (!room.ContainsWorld(aWorld) || !room.ContainsWorld(bWorld)) return false;

        // Convert world → cell coordinates (XZ only)
        float s = room.CellSize;
        Vector2 a2 = new Vector2(aWorld.x, aWorld.z);
        Vector2 b2 = new Vector2(bWorld.x, bWorld.z);

        int ax = WorldToCell(a2.x, room.Origin.x, s);
        int az = WorldToCell(a2.y, room.Origin.z, s);
        int bx = WorldToCell(b2.x, room.Origin.x, s);
        int bz = WorldToCell(b2.y, room.Origin.z, s);

        // Optional: early exit if start or end in a blocked cell (common policy)
        if (!room.InBounds(ax, az) || !room.InBounds(bx, bz)) return false;

        int inflate = Mathf.CeilToInt(agentRadius / Mathf.Max(0.0001f, s));

        // Walk all cells crossed by the segment (supercover → includes edge-adjacent cells)
        foreach (var c in SupercoverLine(ax, az, bx, bz))
        {
            if (!room.InBounds(c.x, c.y)) return false; // treat out-of-bounds as blocked

            if (inflate <= 0)
            {
                if (room.IsBlocked(c.x, c.y)) return false;
            }
            else
            {
                // Check a small square neighborhood for clearance
                for (int dx = -inflate; dx <= inflate; dx++)
                    for (int dz = -inflate; dz <= inflate; dz++)
                    {
                        int nx = c.x + dx, nz = c.y + dz;
                        if (!room.InBounds(nx, nz) || room.IsBlocked(nx, nz))
                            return false;
                    }
            }
        }

        return true;
    }

    // --- Helpers ---

    static int WorldToCell(float w, float origin, float cellSize)
        => Mathf.FloorToInt((w - origin) / cellSize);

    /// Supercover Bresenham in 2D (returns every grid cell the segment touches).
    static IEnumerable<Vector2Int> SupercoverLine(int x0, int y0, int x1, int y1)
    {
        int dx = x1 - x0, dy = y1 - y0;
        int sx = Math.Sign(dx), sy = Math.Sign(dy);
        dx = Math.Abs(dx); dy = Math.Abs(dy);

        int x = x0, y = y0;
        yield return new Vector2Int(x, y);

        if (dx >= dy)
        {
            int err = dx / 2;
            int yStepErr = dx; // threshold for supercover
            int e2;
            while (x != x1)
            {
                x += sx;
                err -= dy;
                e2 = err;
                yield return new Vector2Int(x, y);
                if (e2 < 0) // crossed a row boundary → include adjacent cell
                {
                    y += sy;
                    err += dx;
                    yield return new Vector2Int(x, y);
                }
            }
        }
        else
        {
            int err = dy / 2;
            int xStepErr = dy;
            int e2;
            while (y != y1)
            {
                y += sy;
                err -= dx;
                e2 = err;
                yield return new Vector2Int(x, y);
                if (e2 < 0) // crossed a column boundary → include adjacent cell
                {
                    x += sx;
                    err += dy;
                    yield return new Vector2Int(x, y);
                }
            }
        }
    }
}




/// Usage:
/// // aWorld and bWorld in world XZ? Convert to tile coords first:
/// Vector2Int ToTile(Vector3 world, Vector3 origin, float cellSize) =>
///     new(Mathf.FloorToInt((world.x - origin.x)/cellSize),
///        Mathf.FloorToInt((world.z - origin.z)/cellSize));

// If you already track tile coords:
/// bool los = RoomLOS.HasLineOfSight(currentRoom, startTile, endTile);


public static class RoomLOS
{
    /// <summary>
    /// Returns true if there is line-of-sight between two tile positions within the same Room.
    /// Ignores height. Blocks when the segment crosses a walled edge (unless that edge has a door).
    /// </summary>
    public static bool HasLineOfSight(Room room, Vector2Int aTile, Vector2Int bTile)
    {
        if (room == null) return false;
        if (!room.IsTileInRoom(aTile) || !room.IsTileInRoom(bTile)) return false;

        // Degenerate case
        if (aTile == bTile) return true;

        foreach (var step in SupercoverLine(aTile, bTile))
        {
            // For each transition from prev -> curr, check the shared edge walls.
            if (!step.hasPrev) continue;

            var prev = step.prev;
            var curr = step.curr;

            // Step direction in tile space
            int dx = curr.x - prev.x;
            int dy = curr.y - prev.y;

            // Sanity: both tiles must exist in this room
            if (!room.IsTileInRoom(prev) || !room.IsTileInRoom(curr))
                return false; // treat out of room as blocked

            int iPrev = room.GetCellInRoom(prev);
            int iCurr = room.GetCellInRoom(curr);
            if (iPrev < 0 || iCurr < 0) return false;

            var cellPrev = room.cells[iPrev];
            var cellCurr = room.cells[iCurr];

            // Crossing a vertical edge? (dx != 0)
            if (dx > 0)
            {
                // Crossing from prev to the EAST: check prev.East / curr.West
                if (EdgeBlocked(cellPrev.walls, cellPrev.doors, DirFlags.E) ||
                    EdgeBlocked(cellCurr.walls, cellCurr.doors, DirFlags.W))
                    return false;
            }
            else if (dx < 0)
            {
                // Crossing to the WEST
                if (EdgeBlocked(cellPrev.walls, cellPrev.doors, DirFlags.W) ||
                    EdgeBlocked(cellCurr.walls, cellCurr.doors, DirFlags.E))
                    return false;
            }

            // Crossing a horizontal edge? (dy != 0)
            if (dy > 0)
            {
                // Crossing to the NORTH
                if (EdgeBlocked(cellPrev.walls, cellPrev.doors, DirFlags.N) ||
                    EdgeBlocked(cellCurr.walls, cellCurr.doors, DirFlags.S))
                    return false;
            }
            else if (dy < 0)
            {
                // Crossing to the SOUTH
                if (EdgeBlocked(cellPrev.walls, cellPrev.doors, DirFlags.S) ||
                    EdgeBlocked(cellCurr.walls, cellCurr.doors, DirFlags.N))
                    return false;
            }

            // Diagonal steps (dx != 0 && dy != 0) will trigger both checks above,
            // which correctly handles corner pinches. If you want stricter corner
            // blocking (no "corner cutting"), keep as-is. If you prefer permissive
            // behavior, only block if BOTH orthogonal edges are walls without doors.
        }

        return true;
    }

    /// <summary> True if a wall bit is present and there's NOT a door on that edge. </summary>
    private static bool EdgeBlocked(DirFlags walls, DirFlags doors, DirFlags dir)
    {
        bool hasWall  = (walls & dir) != 0;
        bool hasDoor  = (doors & dir) != 0;
        return hasWall && !hasDoor;
    }

    /// <summary>
    /// Supercover Bresenham: yields every tile the segment touches (including edge-adjacent).
    /// Emits pairs of (prev -> curr) transitions to check shared edges.
    /// </summary>
    private static IEnumerable<(bool hasPrev, Vector2Int prev, Vector2Int curr)> SupercoverLine(Vector2Int a, Vector2Int b)
    {
        int x0 = a.x, y0 = a.y, x1 = b.x, y1 = b.y;
        int dx = Math.Abs(x1 - x0), dy = Math.Abs(y1 - y0);
        int sx = x0 < x1 ? 1 : -1;
        int sy = y0 < y1 ? 1 : -1;

        int x = x0, y = y0;
        //bool first = true;
        Vector2Int prev = new(x, y);
        yield return (false, prev, prev);

        if (dx >= dy)
        {
            int err = dx / 2;
            while (x != x1)
            {
                x += sx;
                err -= dy;
                Vector2Int curr = new(x, y);
                yield return (true, prev, curr);
                prev = curr;

                if (err < 0)
                {
                    y += sy;
                    err += dx;
                    curr = new Vector2Int(x, y);
                    yield return (true, prev, curr);
                    prev = curr;
                }
            }
        }
        else
        {
            int err = dy / 2;
            while (y != y1)
            {
                y += sy;
                err -= dx;
                Vector2Int curr = new(x, y);
                yield return (true, prev, curr);
                prev = curr;

                if (err < 0)
                {
                    x += sx;
                    err += dy;
                    curr = new Vector2Int(x, y);
                    yield return (true, prev, curr);
                    prev = curr;
                }
            }
        }
    }
}
