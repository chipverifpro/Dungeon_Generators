using UnityEngine;
using UnityEngine.Tilemaps;
using System.Collections.Generic;
using System;
using System.Collections;

[Flags]
public enum DirFlags : byte
{
    None = 0,
    N = 1 << 0,
    E = 1 << 1,
    S = 1 << 2,
    W = 1 << 3,

    NE = 1 << 4,
    SE = 1 << 5,
    SW = 1 << 6,
    NW = 1 << 7,
    //NE = N | E,
    //SE = S | E,
    //SW = S | W,
    //NW = N | W,
    //All = N | E | S | W,
}

public static class DirFlagsEx  // extension functions for the DirFlags enum
{
    // ---- Private cached arrays (no per-call allocations) ----
    private static readonly DirFlags[] kCardinals = { DirFlags.N, DirFlags.E, DirFlags.S, DirFlags.W };
    private static readonly DirFlags[] kDiagonals = { DirFlags.NE, DirFlags.SE, DirFlags.SW, DirFlags.NW };
    private static readonly DirFlags[] kAll8 = { DirFlags.N, DirFlags.NE, DirFlags.E, DirFlags.SE,
                                                       DirFlags.S, DirFlags.SW, DirFlags.W, DirFlags.NW };

    // Fast, allocation-free accessors (iterate directly).
    public static DirFlags[] AllCardinals => kCardinals;
    public static DirFlags[] AllDiagonals => kDiagonals;
    public static DirFlags[] All8 => kAll8;

    // ---- Classification ----
    public static bool IsCardinal(this DirFlags dir)
        => dir == DirFlags.N || dir == DirFlags.E || dir == DirFlags.S || dir == DirFlags.W;

    public static bool IsDiagonal(this DirFlags dir)
        => dir == DirFlags.NE || dir == DirFlags.NW || dir == DirFlags.SE || dir == DirFlags.SW;

    public static DirFlags Opposite(this DirFlags dir)
    {
        switch (dir)
        {
            case DirFlags.N: return DirFlags.S;
            case DirFlags.S: return DirFlags.N;
            case DirFlags.E: return DirFlags.W;
            case DirFlags.W: return DirFlags.E;
            case DirFlags.NE: return DirFlags.SW;
            case DirFlags.SE: return DirFlags.NW;
            case DirFlags.SW: return DirFlags.NE;
            case DirFlags.NW: return DirFlags.SE;
            default: return DirFlags.None;
        }
    }

    // ---- Conversions ----
    public static Vector2Int ToVector2Int(this DirFlags dir)
    {
        switch (dir)
        {
            case DirFlags.N: return new Vector2Int(0, -1);
            case DirFlags.E: return new Vector2Int(-1, 0);
            case DirFlags.S: return new Vector2Int(0, 1);
            case DirFlags.W: return new Vector2Int(1, 0);

            case DirFlags.NE: return new Vector2Int(-1, -1);
            case DirFlags.SE: return new Vector2Int(-1, 1);
            case DirFlags.SW: return new Vector2Int(1, 1);
            case DirFlags.NW: return new Vector2Int(1, -1);
        }
        return Vector2Int.zero;
    }

    public static DirFlags FromVector2Int(Vector2Int v)
    {
        int x = Mathf.Clamp(v.x, -1, 1);
        int y = Mathf.Clamp(v.y, -1, 1);

        if (x == 0 && y == 1) return DirFlags.S;
        if (x == 1 && y == 0) return DirFlags.W;
        if (x == 0 && y == -1) return DirFlags.N;
        if (x == -1 && y == 0) return DirFlags.E;

        if (x == 1 && y == 1) return DirFlags.SW;
        if (x == 1 && y == -1) return DirFlags.NW;
        if (x == -1 && y == -1) return DirFlags.NE;
        if (x == -1 && y == 1) return DirFlags.SE;

        return DirFlags.None;
    }

    //CountBits: This uses Brian Kernighan’s algorithm (v &= v-1) to strip one bit
    //           per loop → very fast for small bitfields like a byte.
    public static int Count(this DirFlags dir)
    {
        byte v = (byte)dir;
        int count = 0;
        while (v != 0)
        {
            v &= (byte)(v - 1); // clear the lowest set bit
            count++;
        }
        return count;
    }
}

/* Examples of things to do with DirFlags and DirFlagsEx:
--------------------------------------------- Example 1
DirFlags dir = DirFlags.N | DirFlags.E;

if ((dir & DirFlags.NE) == DirFlags.NE)
    Debug.Log("Exactly Northeast!");

if (dir.HasFlag(DirFlags.N))
    Debug.Log("Includes North");

-------------------------------------------- Example 2
DirFlags dir = DirFlags.NE;

if (dir.IsDiagonal())
    Debug.Log("Going diagonal!");

Vector2Int step = dir.ToVector2Int();
Debug.Log($"Step = {step}"); // (1,1)

DirFlags back = dir.Opposite();
Debug.Log($"Opposite of {dir} = {back}");

-------------------------------------------- Example 3
Vector2Int v = new Vector2Int(-1, 1);
DirFlags dir = DirFlagsEx.FromVector2Int(v);   // <- uses Ex

Debug.Log(dir);  // outputs "NW"

-------------------------------------------- Example 4
// Loop 8 directions (N,E,S,W,NE,SE,SW,NW)
Vector2Int here = new Vector2Int(10,2);
Vector2Int neighbor;

foreach (var dir in DirFlagsEx.All8())      // <- uses Ex
{
    Vector2Int neighbor = here + dir.ToVector2Int();
    // Check neighbors in map
}

-------------------------------------------- Example 5
DirFlags d = DirFlags.N | DirFlags.E | DirFlags.S;

int bits = d.CountBits();

Debug.Log($"{d} has {bits} bits set."); 
// Output: "N, E, S has 3 bits set."

*/


// ===================== Cell class ===================
public class Cell       // one cell in a Room
{
    public Vector2Int pos;          // x,y
    public int height;              // z
    public int room_number;         // Based on index into global "rooms" list
    public int type;                // Floor, Solid Stone, Water, etc.
    public DirFlags walls = DirFlags.None;  // walls: N-E-S-W bit field
    public DirFlags doors = DirFlags.None;  // doors: N-E-S-W bit field
    public Color colorFloor = new(1f, 0.4f, 0.7f, 0.5f); // default semi-transparent pink

    public float travel_cost = 1f;  // examples: 1 = open floor, 2 = rough terrain, 0.75 = road

    // Delegates for behaviors (see notes below)
    public Action<Cell> OnView;     // function triggered when viewed
    public Action<Cell> OnStep;     // function triggered when stepped on

    // Constructors:
    public Cell(int x, int y, int z)
    {
        this.pos.x = x;
        this.pos.y = y;
        this.height = z;
    }

    public Cell(int x, int y)
    {
        this.pos.x = x;
        this.pos.y = y;
    }

    public Cell(Vector2Int pos)
    {
        this.pos = pos;
    }

    // shortcuts to read access variations
    public Vector3Int pos3d => new Vector3Int(pos.x, pos.y, height);
    public int x => pos.x;
    public int y => pos.y;
    public int z => height;

    // Helpers to trigger delegates safely
    public void TriggerView() { OnView?.Invoke(this); }
    public void TriggerStep() { OnStep?.Invoke(this); }

    // Example of assigning a functiion to the delegates:
    //   Cell trapCell = new Cell(2, 3);
    //   trapCell.OnStep = (c) => Debug.Log($"Ouch! Trap triggered at {c.x},{c.y}!");
    //   trapCell.OnView = (c) => Debug.Log($"You see a suspicious floor tile at {c.x},{c.y}...");

    // Example of calling the delegates:
    //   currentCell.TriggerView();
    //   currentCell.TriggerStep();
}

// ========================== Room class =================================
public class Room
{
    // == Properties of the room
    public int my_room_number = -1; // Uniquely identifies this room based on global "rooms" list
    public String name = "";

    // Tile-by-tile list of everything about a cell: floors/walls/doors/etc
    public List<Cell> cells = new();

    // NOTE: The above structure will replace these fields below.
    //public List<Vector2Int> tiles = new();
    //public List<Vector2Int> walls = new();
    public List<int> heights = new(); // Heights for each tile in the room, used for 3D generation

    public List<Door> doors = new();  // Details of every door in this room

    public int Size => cells.Count;     // OLD used tiles, NEW will use cells
    public int Last => cells.Count - 1; // Handy index for editing a newly added cell.
    public Color colorFloor = new(1f, 0.4f, 0.7f, 0.5f); // semi-transparent pink; // Color for the whole room, cell may override this
    public List<int> neighbors = new(); // List of neighboring rooms by index into global "rooms" list
    public bool isCorridor = false;     // Indicate if this room was generated as a corridor

    // OLD style: quick lookup in multiple lists for floors, walls, heights.
    // HashSets allow fast check whether room contains something at a position.
    // See the functions below: bool IsTileInRoom(pos), bool IsWallInRoom(pos)
    // Dictionaries are also based on a hash but returns a value at that position.
    // int GetHeightInRoom_OLD(pos)
    //public HashSet<Vector2Int> floor_hash_room = new();
    //public HashSet<Vector2Int> wall_hash_room = new();
    //public Dictionary<Vector2Int, int> heights_lookup_room = new();

    // NEW style: After migrating to using class Cell instead of separate lists.
    // GetCellInRoom(pos) returns the index into this room's "cells" list.
    // on not finding the cell, function returns -1.
    public Dictionary<Vector2Int, int> cell_dictionary_room = new();


    // == constructors...
    public Room() { }

    // NEW
    public Room(List<Vector2Int> initialTileList, List<int> initialHeightsList)
    {
        //List<Vector2Int> pos_list = new List<Vector2Int>(initialTileList);
        //List<int> heights = new List<int>(initialHeightsList);

        cells = new List<Cell>();
        for (int i = 0; i < initialTileList.Count; i++)
        {
            cells.Add(new Cell(initialTileList[i].x, initialTileList[i].y, initialHeightsList[i]));
        }
    }

    // UNUSED
    public Room(List<Vector2Int> initialTileList)
    {

        cells = new List<Cell>();
        for (int i = 0; i < initialTileList.Count; i++)
        {
            cells.Add(new Cell(initialTileList[i].x, initialTileList[i].y, 0));
        }
    }

    // UNUSED
    public Room(List<Cell> initialCells)
    {
        // Note: not deep copy
        this.cells = new(initialCells);
    }

    // UNUSED
    // copy constructor - buggy - not deep copy
    public Room(Room other)
    {
        cells = new List<Cell>(other.cells);
        doors = new List<Door>(other.doors);
        name = other.name;
        colorFloor = other.colorFloor;
        isCorridor = other.isCorridor;

        cells = other.cells;
        // TODO: check what other parameters need copying...
    }

    // NEW
    public bool IsTileInRoom(Vector2Int pos)
    {
        int cell_num = GetCellInRoom(pos);
        return (cell_num >= 0);
    }

    // NEW
    public int GetCellInRoom(Vector2Int pos)
    {
        //if (cell_dictionary_room.Count != cells.Count)
        if (cell_dictionary_room.Count == 0)
        {
            Debug.Log($"Building cell_dictionary_room.");
            // Build dictionary once and keep it.
            //   Auto-regenerates if "cells" list length changes.
            //   Note that you must manually call ResetCellDictionary()
            //   yourself if you modify the pos value in any cell, but keep
            //   the list the same length.
            cell_dictionary_room = new(cells.Count);
            int cell_number = 0;
            for (int i = 0; i < cells.Count; i++)
            {
                if (cell_dictionary_room.TryAdd(cells[i].pos, cell_number))
                    cell_number++;
            }
        }
        // Here is the actual function.  Everything above was calculating the cache.
        return cell_dictionary_room.TryGetValue(pos, out var v) ? v : -1;
    }

    // NEW
    public void ResetCellDictionary()
    {
        Debug.Log($"Clearing cell_dictionary_room.");
        cell_dictionary_room = new();   // will force list to be regenerated next time it is used.
    }

    // NEW
    // simple helper lookup function for height.
    // Other fields could be done the same way.
    public int GetHeightInRoom(Vector2Int pos)
    {
        int index = GetCellInRoom(pos);
        //Debug.Log($"GetHeightInRoom: index = {index}, cells.Count = {cells.Count}");
        if (index >= 0) return cells[index].height;
        else return 999; // not found
    }


    // ==================== Color Helper functions...

    //setColorFloor sets all floors of a room to a color.

    // Set the color for the floor tiles in this room many ways...
    // room.setColorFloor(Color.white);        // White
    // room.setColorFloor(rgb: "#FF0000FF"); // Red
    // room.setColorFloor();                   // Bright Random
    // room.setColorFloor(highlight: false);   // Dark   Random
    // room.setColorFloor(highlight: true);    // Bright Random
    public Color setColorFloor(Color? color = null, bool highlight = true, string rgba = "")
    {
        colorFloor = getColor(color: color, highlight: highlight, rgba: rgba);
        return colorFloor;
    }

    //getColor is a simple helper to generate a Color based on various ways to specify a color
    // (see setColorFloor for examples)
    public Color getColor(Color? color = null, bool highlight = true, string rgba = "")
    {
        Color colorrgba; // temp
        Color return_color = Color.white;

        if (color != null)
            return_color = (Color)color;
        else if ((!string.IsNullOrEmpty(rgba)) && (ColorUtility.TryParseHtmlString(rgba, out colorrgba)))
            colorFloor = colorrgba;
        else if (highlight)
            return_color = UnityEngine.Random.ColorHSV(0f, 1f, 0.6f, 1f, 0.6f, 1f);   // Bright Random
        else // highlight == false
            return_color = UnityEngine.Random.ColorHSV(0f, 1f, 0.6f, 1f, 0.1f, 0.4f); // Dark Random

        return return_color;
    }

}

public partial class DungeonGenerator : MonoBehaviour
{
    // =======================================================
    // helper routines for Rooms

    // NEW Draw: room -> 2D tiles
    public void DrawMapFromRoomsList(List<Room> rooms)
    {
        tilemap.ClearAllTiles();

        foreach (Room room in rooms)
        {
            foreach (Cell cell in room.cells)
            {
                Vector3Int pos = new Vector3Int(cell.x, cell.y, 0);
                tilemap.SetTile(pos, floorTile);
                tilemap.SetTileFlags(pos, TileFlags.None); // Allow color changes
                tilemap.SetColor(pos, cell.colorFloor);
            }
        }
    }

    // NEW Apply this function to a room to give subtle ripples to the floor heights.
    public Room AddPerlinToFloorHeights(Room room)
    {
        if (cfg.perlinFloorHeights == 0) return room;

        int perlin_floor;
        float seedX = UnityEngine.Random.Range(0f, 9999f);
        float seedY = UnityEngine.Random.Range(0f, 9999f);
        foreach (Cell cell in room.cells)
        {
            perlin_floor = (int)(Mathf.PerlinNoise((cell.x + seedX) * cfg.perlinFloorWavelength,
                                                   (cell.y + seedY) * cfg.perlinFloorWavelength)
                                                   * cfg.perlinFloorHeights);
            cell.height += perlin_floor;
        }
        return room;
    }

    // UNCHANGED
    public int GetHeightOfLocationFromOneRoom(Room room, Vector2Int pos)
    {
        //Debug.Log($"room.tiles = {room.tiles.Count}; room.heights = {room.heights.Count}");
        int height = room.GetHeightInRoom(pos);
        if (height != 999) return height; // found it

        //Debug.Log("location not found in room");
        return 999;
    }

    // UNCHANGED
    public int GetHeightOfLocationFromAllRooms(List<Room> rooms, Vector2Int pos)
    {
        int height;
        foreach (var room in rooms)
        {
            height = room.GetHeightInRoom(pos);
            if (height != 999) return height; // found it
        }
        //Debug.Log("location not found in rooms");
        return 999;
    }

    // NEW
    public IEnumerator BuildWallsAroundFloorsInRooms(TimeTask tm = null)
    {
        bool local_tm = false;
        if (tm == null) { tm = TimeManager.Instance.BeginTask("Build3DFromRooms"); local_tm = true; }
        try
        {
            int room_number = 0;
            foreach (Room room in rooms)
            {
                foreach (var cell in room.cells)
                {
                    foreach (var dir in directions_xy)
                    {
                        if (!IsTileInNeighborhood(room_number, room.neighbors, cell.pos + dir))
                        //if (!room.IsTileInRoom(cell.pos+dir))
                        {
                            // No neighboring floor seen in direction dir,
                            // so OR that bit into the wall flags for this cell...
                            cell.walls |= DirFlagsEx.FromVector2Int(dir);

                        }

                        // Display as debug...  SLOW
                        Vector3Int pos3d = new Vector3Int(cell.x, cell.y, 0);
                        tilemap.SetTile(pos3d, wallTile);
                        tilemap.SetTileFlags(pos3d, TileFlags.None);
                        tilemap.SetColor(pos3d, Color.red);
                    }

                    //Debug.Log($"cell.walls({cell.x},{cell.y} = {cell.walls})");
                    if (tm.IfYield()) yield return null;
                }
                room_number++;
                //Debug.Log($"BuildWallsAroundFloorsInRooms room {room_number} of {rooms.Count}");
            }
            //Debug.Log($"BuildWallsAroundFloorsInRooms DONE");
        }
        finally { if (local_tm) tm.End(); }
    }

    // UNUSED NEW
    // MoveRoom will shift a room in x,y,and z(height) directions.
    // If allow_collision = false, room doesn't move when it collides with another room.
    // TODO: check for collision.  Also allow rotation, scaling, growing?
    public bool MoveRoom(int room_number, Vector3Int transpose_vector, bool allow_collision = true)
    {
        List<Cell> new_cells = new();
        //List<Vector2Int> new_floors = new();
        //List<int> new_heights = new();
        List<Door> new_doors = new();
        int collisions = 0;

        for (int tile_number = 0; tile_number < rooms[room_number].cells.Count; tile_number++)
        {
            Vector2Int new_floor = (rooms[room_number].cells[tile_number].pos + new Vector2Int(transpose_vector.x, transpose_vector.y));
            int new_height = (rooms[room_number].heights[tile_number] + transpose_vector.z);
            new_cells.Add(new Cell(new_floor.x, new_floor.y, new_height));
            // TODO: Check for collisions to other rooms
        }
        if (collisions == 0 || allow_collision)
        {
            rooms[room_number].cells = new_cells;
            //rooms[room_number].tiles = new_floors;
            //rooms[room_number].heights = new_heights;
            rooms[room_number].doors = new_doors;
            return true; // true = no collisions or ignore them
        }
        else
        {
            return false; // false = collided so don't update room
        }

    }


    // UNCHANGED
    // create a complete list of all rooms connected, ignoring duplicates
    public List<int> get_union_of_connected_room_indexes(int start_room_number, bool everything = true)
    {
        bool added = true;
        List<int> rooms_to_connect = new();
        rooms_to_connect.Add(start_room_number);
        rooms_to_connect.AddRange(rooms[start_room_number].neighbors);

        // if everything, include all neighboring rooms of neighbors
        // if !everything, only include direct neighbors
        if (!everything) return rooms_to_connect;

        // create a complete list of all rooms connected, ignoring duplicates
        // keep going over the list until no more to add
        while (added == true)
        {
            added = false;

            for (int i = 0; i < rooms_to_connect.Count; i++)
            {
                for (int j = 0; j < rooms[rooms_to_connect[i]].neighbors.Count; j++)
                {
                    if (!rooms_to_connect.Contains(rooms[rooms_to_connect[i]].neighbors[j]))
                    {
                        rooms_to_connect.Add(rooms[rooms_to_connect[i]].neighbors[j]);
                        added = true;
                    }
                }
            }
        }
        return rooms_to_connect;
    }

    // NEW
    public List<Vector2Int> get_union_of_connected_room_cells(int start_room_number, bool everything = true)
    {
        List<Vector2Int> union_of_cells = new();
        // create a complete list of all rooms connected, ignoring duplicates
        List<int> rooms_to_connect = get_union_of_connected_room_indexes(start_room_number, everything);

        // add tiles from all connected rooms to the list (union of cells)
        for (int i = 0; i < rooms_to_connect.Count; i++)
        {
            foreach (Cell cell in rooms[rooms_to_connect[i]].cells)
                union_of_cells.Add(cell.pos);
        }

        //Debug.Log("get_union_of_connected_room_cells(" + start_room_number + ") -> length " + union_of_cells.Count + " END");
        return union_of_cells;
    }

    // Neighborhood searches...

    // UNCHANGED
    public int GetHeightInNeighborhood(int room_number, Vector2Int pos)
    {
        int ht = rooms[room_number].GetHeightInRoom(pos);
        if (ht != 999) return ht;
        List<int> myneighbors = rooms[room_number].neighbors;
        for (int i = 0; i < myneighbors.Count; i++)
        {
            ht = rooms[myneighbors[i]].GetHeightInRoom(pos);
            if (ht != 999) return ht;
        }
        return ht;
    }

    // UNCHANGED.
    public bool IsTileInNeighborhood(int room_number, List<int> room_neighbors, Vector2Int pos)
    {
        //Debug.Log($"IsTileInNeighborhood: room_neighbors.Count={room_neighbors.Count} pos = {pos.x},{pos.y}");
        bool isit = rooms[room_number].IsTileInRoom(pos);
        if (isit) return isit;
        //List<int> myneighbors = rooms[room_number].neighbors;
        for (int i = 0; i < room_neighbors.Count; i++)
        {
            isit = rooms[room_neighbors[i]].IsTileInRoom(pos);
            if (isit) break;
        }
        //Debug.Log($"isit = {isit}, in room {room_number}");
        return isit;
    }

}