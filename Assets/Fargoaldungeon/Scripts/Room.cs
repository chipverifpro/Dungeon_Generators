using System.Collections;
using UnityEngine;
using UnityEngine.Tilemaps;
using System.Collections.Generic;
using System;
using UnityEditor.MemoryProfiler;
using System.Threading;

public class Room
{
    // == Properties of the room
    public int my_room_number = -1; // Uniquely identifies this room
    public String name = "";  // use inherited Object.name

    // Tile-by-tile lists of floors/walls/doors/etc
    public List<Vector2Int> tiles = new();
    public List<Vector2Int> walls = new();
    public List<Door> doors = new List<Door>();

    public List<int> heights = new(); // Heights for each tile in the room, used for 3D generation
    public int Size => tiles.Count;
    public Color colorFloor = Color.white;
    public List<int> neighbors = new(); // List of neighboring rooms by index
    public bool isCorridor = false; // Indicate if this room is a corridor

    // HashSets contain tiles or walls for this room or room + immediate neighbors.
    public HashSet<Vector2Int> floor_hash_room = new();
    public HashSet<Vector2Int> wall_hash_room = new();
    public Dictionary<Vector2Int, int> heights_lookup_room = new();


    // == constructors...
    public Room() { }

    public Room(List<Vector2Int> initialTileList, List<int> initialHeightsList)
    {
        tiles = new List<Vector2Int>(initialTileList);
        heights = new List<int>(initialHeightsList);
    }
    public Room(List<Vector2Int> initialTileList)
    {
        tiles = initialTileList;
        heights = new List<int>(initialTileList.Count); // Initialize heights list with the same count
    }

    // copy constructor
    public Room(Room other)
    {
        tiles = new List<Vector2Int>(other.tiles);
        heights = new List<int>(other.heights);
        doors = new List<Door>(other.doors);
        name = other.name;
        colorFloor = other.colorFloor;
        isCorridor = other.isCorridor;
        // TODO: check to see if other parameters need copying
    }

    public int GetHeightInRoom(Vector2Int pos)
    {
        if (heights_lookup_room.Count == 0)
        {
            // Build once and keep it.
            heights_lookup_room = new(Size);
            for (int i = 0; i < tiles.Count; i++)
                heights_lookup_room[tiles[i]] = heights[i];
        }
        return heights_lookup_room.TryGetValue(pos, out var v) ? v : 999;
    }

    public bool IsTileInRoom(Vector2Int pos)
    {
        if (floor_hash_room.Count == 0)
        {
            // Build once and keep it.
            floor_hash_room = new(Size);
            for (int i = 0; i < tiles.Count; i++)
                floor_hash_room.Add(tiles[i]);
        }
        return floor_hash_room.Contains(pos);
    } 
    
    public bool IsWallInRoom(Vector2Int pos)
    {
        if (wall_hash_room.Count == 0)
        {
            // Build once and keep it.
            wall_hash_room = new(walls.Count);
            for (int i = 0; i < walls.Count; i++)
                wall_hash_room.Add(walls[i]);
        }
        return wall_hash_room.Contains(pos);
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
        Color colorrgba = new(); //temp
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

    // =======================================================
    // helper routines for Rooms
}

public partial class DungeonGenerator : MonoBehaviour
{
    public void DrawMapFromRoomsList(List<Room> rooms)
    {
        tilemap.ClearAllTiles();

        foreach (Room room in rooms)
        {
            foreach (Vector2Int tilePos in room.tiles)
            {
                Vector3Int pos = new Vector3Int(tilePos.x, tilePos.y, 0);
                tilemap.SetTile(pos, floorTile);
                tilemap.SetTileFlags(pos, TileFlags.None); // Allow color changes
                tilemap.SetColor(pos, room.colorFloor);
            }
        }
    }

    // TODO: add option to give floor a gentle perlin ripple
    public Room AddPerlinToFloorHeights(Room room)
    {
        Vector2Int pos;
        int perlin_floor;
        float seedX = UnityEngine.Random.Range(0f,9999f);
        float seedY = UnityEngine.Random.Range(0f,9999f);
        for (int i = 0; i < room.heights.Count; i++)
        {
            if (cfg.perlinFloorHeights > 0)
            {
                pos = room.tiles[i];
                perlin_floor = (int)(Mathf.PerlinNoise((pos.x + seedX) * cfg.perlinWavelength, (pos.y + seedY) * cfg.perlinWavelength) * cfg.perlinFloorHeights);
                room.heights[i] += perlin_floor;
                Debug.Log($"Perlin floor room[{i}].pos[{pos.x}, {pos.y}] += {perlin_floor}");
            }
        }
        return room;
    }

    public int GetHeightOfLocationFromOneRoom(Room room, Vector2Int pos)
    {
        //Debug.Log($"room.tiles = {room.tiles.Count}; room.heights = {room.heights.Count}");
        int height = room.GetHeightInRoom(pos);
        if (height != 999) return height; // found it

        Debug.Log("location not found in rooms");
        return 999;
    }

    public int GetHeightOfLocationFromAllRooms(List<Room> rooms, Vector2Int pos)
    {
        int height;
        foreach (var room in rooms)
        {
            height = room.GetHeightInRoom(pos);
            if (height != 999) return height; // found it
        }
        Debug.Log("location not found in rooms");
        return 999;
    }

    public void BuildWallListsFromRooms()
    {
        for (var room_number = 0; room_number < rooms.Count; room_number++)
        {
            //Debug.Log($"Before Building Walls for room {room_number} = {rooms[room_number].walls.Count}, num_heights = {rooms[room_number].heights.Count}");
            rooms[room_number].walls = new();
            foreach (var pos in rooms[room_number].tiles)
            {
                foreach (var dir in directions_xy)
                {
                    if (!IsTileInNeighborhood(room_number,pos + dir))
                    {
                        rooms[room_number].walls.Add(pos + dir);
                        // Do we need to keep height for walls?
                        // No, they only are drawn as neighbors of a floor
                        // which already has a height.
                    }
                }
            }
            //Debug.Log($"After Building Walls for room {room_number} = {rooms[room_number].walls.Count} height={rooms[room_number].heights[0]}");
        }
    }

    // UNUSED
    // Obsolete?  This really combined the room contents, which
    // we no longer do, we instead maintain a list of neighbors.
    // Maybe useful later?
    public void MergeRooms(Room keep, Room merge, List<Vector2Int> corridor)
    {
        var combined = new HashSet<Vector2Int>(keep.tiles);
        foreach (var t in merge.tiles) combined.Add(t);
        foreach (var c in corridor) combined.Add(c);

        keep.tiles = new List<Vector2Int>(combined);
    }

    // UNUSED
    // MoveRoom will shift a room in x,y,and z(height) directions.
    // If allow_collision = false, room doesn't move when it collides with another room.
    // TODO: check for collision.  Also allow rotation, scaling, growing?
    public bool MoveRoom(int room_number, Vector3Int transpose_vector, bool allow_collision = true)
    {
        List<Vector2Int> new_floors = new();
        List<int> new_heights = new();
        List<Door> new_doors = new();
        int collisions = 0;

        for (int tile_number = 0; tile_number < rooms[room_number].tiles.Count; tile_number++)
        {
            new_floors.Add(rooms[room_number].tiles[tile_number] + new Vector2Int(transpose_vector.x, transpose_vector.y));
            new_heights.Add(rooms[room_number].heights[tile_number] + transpose_vector.z);
            // TODO: Check for collisions to other rooms
        }
        if (collisions == 0 || allow_collision)
        {
            rooms[room_number].tiles = new_floors;
            rooms[room_number].heights = new_heights;
            rooms[room_number].doors = new_doors;
            return true; // true = no collisions or ignore them
        }
        else
        {
            return false; // false = collided so don't update room
        }

    }


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

    public List<Vector2Int> get_union_of_connected_room_cells(int start_room_number, bool everything = true)
    {
        List<Vector2Int> union_of_cells = new();
        // create a complete list of all rooms connected, ignoring duplicates
        List<int> rooms_to_connect = get_union_of_connected_room_indexes(start_room_number, everything);

        // add tiles from all connected rooms to the list (union of cells)
        for (int i = 0; i < rooms_to_connect.Count; i++)
        {
            union_of_cells.AddRange(rooms[rooms_to_connect[i]].tiles);
        }

        //Debug.Log("get_union_of_connected_room_cells(" + start_room_number + ") -> length " + union_of_cells.Count + " END");
        return union_of_cells;
    }

    // Neighborhood searches...

    public int GetHeightInNeighborhood(int room_number, Vector2Int pos)
    {
        int ht = rooms[room_number].GetHeightInRoom(pos);
        List<int> myneighbors = rooms[room_number].neighbors;
        for (int i = 0; (ht == 999) && (i < myneighbors.Count); i++)
            ht = rooms[myneighbors[i]].GetHeightInRoom(pos);
        return ht;
    }

    public bool IsTileInNeighborhood(int room_number, Vector2Int pos)
    {
        bool isit = rooms[room_number].IsTileInRoom(pos);
        List<int> myneighbors = rooms[room_number].neighbors;
        for (int i = 0; (isit == false) && (i < myneighbors.Count); i++)
            isit = rooms[myneighbors[i]].IsTileInRoom(pos);
        return isit;
    }

    public bool IsWallInNeighborhood(int room_number, Vector2Int pos)
    {
        bool isit = rooms[room_number].IsWallInRoom(pos);
        List<int> myneighbors = rooms[room_number].neighbors;
        for (int i = 0; (isit == false) && (i < myneighbors.Count); i++)
            isit = rooms[myneighbors[i]].IsWallInRoom(pos);
        return isit;
    }

}