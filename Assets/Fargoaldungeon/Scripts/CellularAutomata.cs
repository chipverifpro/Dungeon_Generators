using System.Collections;
using UnityEngine;
using UnityEngine.Tilemaps;
using System.Collections.Generic;
using System;
using UnityEditor.MemoryProfiler;

public class Room
{
    // == Properties of the room
    public int my_room_number = -1; // Uniquely identifies this room
    public String name = "";
    public List<Vector2Int> tiles = new();
    public List<Vector2Int> walls = new();
    public List<int> heights = new(); // Heights for each tile in the room, used for 3D generation
    public int Size => tiles.Count;
    public string Name = "";
    public Color colorFloor = Color.white;
    public List<int> neighbors = new(); // List of neighboring rooms by index
    public bool isCorridor = false; // Indicate if this room is a corridor

    // HashSets contain tiles or walls for this room or room + immediate neighbors.
    public HashSet<Vector2Int> floor_hash_room = new();
    public HashSet<Vector2Int> wall_hash_room = new();
    public HashSet<Vector2Int> floor_hash_neighborhood = new();
    public HashSet<Vector2Int> wall_hash_neighborhood = new();

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
        Name = other.Name;
        colorFloor = other.colorFloor;
        isCorridor = other.isCorridor;
    }

    // == Helper functions...
    public RectInt GetBounds2D() // Ignores height
    {
        BoundsInt bounds = GetBounds3D();
        return new RectInt(bounds.xMin, bounds.yMin, bounds.size.x, bounds.size.y);
    }

    public BoundsInt GetBounds3D()
    {
        if (tiles.Count == 0) return new BoundsInt();

        int minX = int.MaxValue, minY = int.MaxValue, minZ = int.MaxValue;
        int maxX = int.MinValue, maxY = int.MinValue, maxZ = int.MinValue;

        foreach (var t in tiles)
        {
            if (t.x < minX) minX = t.x;
            if (t.y < minY) minY = t.y;
            if (t.x > maxX) maxX = t.x;
            if (t.y > maxY) maxY = t.y;
        }
        foreach (var h in heights)
        {
            if (h < minZ) minZ = h;
            if (h > maxZ) maxZ = h;
        }

        return new BoundsInt(new Vector3Int(minX, minY, minZ), new Vector3Int(maxX - minX + 1, maxY - minY + 1, maxZ - minZ + 1));
    }

    /*
    public Vector2Int GetCenter()
    {
        RectInt bounds = GetBounds2D();
        return new Vector2Int(bounds.xMin + bounds.width / 2, bounds.yMin + bounds.height / 2);
    }

    // Get the closest floor tile location in this room to a given target location
    public Vector2Int GetClosestPointInRoom(Vector2Int target)
    {
        int min_distance = int.MaxValue;
        int cur_distance = int.MaxValue;
        Vector2Int closest_point = Vector2Int.zero;

        if (tiles.Count == 0) return Vector2Int.zero;

        foreach (var t in tiles)
        {
            cur_distance = (t - target).sqrMagnitude;
            if (cur_distance < min_distance)
            {
                min_distance = cur_distance;
                closest_point = t;
            }
        }

        return closest_point;
    }
    */

    // Set the color for the floor tiles in this room many ways...
    // room.setColorFloor(Color.white);        // White
    // room.setColorFloor(rgb: "#FF0000FF"); // Red
    // room.setColorFloor();                   // Bright Random
    // room.setColorFloor(highlight: false);   // Dark   Random
    // room.setColorFloor(highlight: true);    // Bright Random
    public Color setColorFloor(Color? color = null, bool highlight = true, string rgba = "")
    {
        Color color_floor = getColor(color: color, highlight: highlight, rgba: rgba);
        return color_floor;
    }

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
}

// ==================================================================

public class CellularAutomata : MonoBehaviour
{
    public DungeonSettings cfg; // Reference to the DungeonSettings ScriptableObject

    public DungeonGenerator generator;

    public Tilemap tilemap;
    public TileBase wallTile;
    public TileBase floorTile;

    //public byte[,] map;
    private byte WALL;
    private byte FLOOR;

    private System.Random rng;
    [HideInInspector] public bool success;    // global generic return value from various tasks
    

    //public List<Room> return_rooms = new List<Room>();

    public void Start()
    {
        WALL = DungeonGenerator.WALL;
        FLOOR = DungeonGenerator.FLOOR;
    }
    public IEnumerator RunCaveGeneration(TimeTask tm = null)
    {
        bool local_tm = false;
        if (tm == null) { tm = TimeManager.Instance.BeginTask("RunCaveGeneration"); local_tm = true; }
        try
        {
            generator.map = new byte[cfg.mapWidth, cfg.mapHeight];
            RandomFillMap(generator.map);

            // Draw initial map
            DrawMapFromByteArray();
            yield return tm.YieldOrDelay(cfg.stepDelay);

            for (int step = 0; step < cfg.CellularGrowthSteps; step++)
            {
                generator.map = RunSimulationStep(generator.map);
                DrawMapFromByteArray();
                yield return tm.YieldOrDelay(cfg.stepDelay);
            }
        }
        finally { if (local_tm) tm.End(); }
    }

    public byte[,] RandomFillMap(byte[,] map)
    {
        rng = new System.Random();

        float seedX = UnityEngine.Random.Range(0f, 10000f);
        float seedY = UnityEngine.Random.Range(0f, 10000f);
        for (int x = 0; x < cfg.mapWidth; x++)
            for (int y = 0; y < cfg.mapHeight; y++)
            {
                if (!generator.IsPointInWorld(new Vector2Int(x, y)))
                {
                    map[x, y] = WALL;
                    continue;
                }
                int borderDistance = Mathf.Min(x, y, cfg.mapWidth - x - 1, cfg.mapHeight - y - 1);
                if (borderDistance == 1)
                    map[x, y] = WALL; // Set hard border tile to wall
                else if (borderDistance <= cfg.softBorderSize)
                    // Setting a wide random border makes square world edges less sharp
                    map[x, y] = rng.Next(0, 100) < cfg.cellularFillPercent ? WALL : FLOOR;
                else
                    if (cfg.usePerlin)
                {
                    float perlin1 = Mathf.PerlinNoise((x + seedX) * cfg.perlinWavelength, (y + seedY) * cfg.perlinWavelength);
                    float perlin2 = Mathf.PerlinNoise((x - seedX) * cfg.perlin2Wavelength, (y - seedY) * cfg.perlin2Wavelength);
                    float noise = (perlin1 + perlin2) / 2f; // Combine two noise layers
                    map[x, y] = noise > cfg.perlinThreshold ? WALL : FLOOR;
                }
                else // Non Perlin noise
                {
                    map[x, y] = rng.Next(0, 100) < cfg.cellularFillPercent ? WALL : FLOOR;
                }
            }
        return map;
    }

    byte[,] RunSimulationStep(byte[,] oldMap)
    {
        byte[,] newMap = new byte[cfg.mapWidth, cfg.mapHeight];

        for (int x = 0; x < cfg.mapWidth; x++)
            for (int y = 0; y < cfg.mapHeight; y++)
            {
                int walls = CountWallNeighbors(oldMap, x, y);
                if (oldMap[x, y] == WALL)
                    newMap[x, y] = walls >= 3 ? WALL : FLOOR;
                else
                    newMap[x, y] = walls > 4 ? WALL : FLOOR;
            }

        return newMap;
    }

    int CountWallNeighbors(byte[,] map, int x, int y)
    {
        int count = 0;
        for (int nx = x - 1; nx <= x + 1; nx++)
            for (int ny = y - 1; ny <= y + 1; ny++)
            {
                if (nx == x && ny == y) continue;
                if (nx < 0 || ny < 0 || nx >= cfg.mapWidth || ny >= cfg.mapHeight)
                    count++;
                else if (map[nx, ny] == WALL)
                    count++;
            }

        return count;
    }

    // DrawMapFromByteArray() is used only by cellular automata during iterations
    void DrawMapFromByteArray()
    {
        tilemap.ClearAllTiles();

        for (int x = 0; x < cfg.mapWidth; x++)
            for (int y = 0; y < cfg.mapHeight; y++)
            {
                Vector3Int pos = new Vector3Int(x, y, 0);
                if (generator.map[x, y] == FLOOR)
                {
                    tilemap.SetTile(pos, floorTile);
                    tilemap.SetTileFlags(pos, TileFlags.None);
                    tilemap.SetColor(pos, Color.white);
                }
                else // WALL
                {
                    if (HasFloorNeighbor(pos))
                    {
                        tilemap.SetTile(pos, wallTile);
                        tilemap.SetTileFlags(pos, TileFlags.None);
                        tilemap.SetColor(pos, Color.white);
                    }
                    else
                    {
                        tilemap.SetTile(pos, null); // optional: don't draw deep interior walls
                    }
                }

            }
    }

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

    bool HasFloorNeighbor(Vector3Int pos)
    {
        for (int x = -cfg.wallThickness; x <= cfg.wallThickness; x++)
            for (int y = -cfg.wallThickness; y <= cfg.wallThickness; y++)
            //foreach (Vector3Int dir in Directions())
            {
                Vector3Int dir = new Vector3Int(x, y, 0);
                if (dir.x == 0 && dir.y == 0) continue; // Skip self
                if (pos.x + dir.x < 0 || pos.y + dir.y < 0 ||
                            pos.x + dir.x >= cfg.mapWidth || pos.y + dir.y >= cfg.mapHeight)
                    continue; // Out of bounds
                if (generator.map[pos.x + dir.x, pos.y + dir.y] == FLOOR)
                    return true;
            }
        return false;
    }

    public IEnumerator FindRoomsCoroutine(byte[,] map, TimeTask tm)
    {
        bool local_tm = false;
        if (tm == null) { tm = TimeManager.Instance.BeginTask("FindRoomsCoroutine"); local_tm = true; }
        try
        {
            BottomBanner.Show("Finding rooms...");
            int this_room_height = 0;
            int width = map.GetLength(0);
            int height = map.GetLength(1);
            bool[,] visited = new bool[width, height];
            List<Room> rooms = new List<Room>();

            Vector2Int[] directions = {
            Vector2Int.up, Vector2Int.down,
            Vector2Int.left, Vector2Int.right
        };

            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    if (!visited[x, y] && (map[x, y] == FLOOR)) // Floor and unvisited
                    {
                        Room newRoom = new Room();
                        Queue<Vector2Int> queue = new Queue<Vector2Int>(16);
                        queue.Enqueue(new Vector2Int(x, y));
                        visited[x, y] = true;

                        while (queue.Count > 0)
                        {
                            var pos = queue.Dequeue();
                            newRoom.tiles.Add(pos);
                            newRoom.heights.Add(this_room_height);

                            foreach (var dir in directions)
                            {
                                int nx = pos.x + dir.x;
                                int ny = pos.y + dir.y;

                                if (nx >= 0 && ny >= 0 && nx < width && ny < height &&
                                    !visited[nx, ny] && (map[nx, ny] == FLOOR))
                                {
                                    queue.Enqueue(new Vector2Int(nx, ny));
                                    visited[nx, ny] = true;
                                }
                                // Yield periodically to keep UI responsive during big rooms
                                if ((newRoom.tiles.Count & 0x1FFF) == 0) // every ~8192 tiles
                                    yield return tm.YieldOrDelay(cfg.stepDelay / 3);
                            }
                        }

                        newRoom.my_room_number = rooms.Count;
                        newRoom.Name = $"Room {newRoom.my_room_number} ({newRoom.tiles.Count} tiles)";
                        newRoom.setColorFloor(highlight: true);
                        rooms.Add(newRoom);

                        this_room_height++;  // change for the next room to be found
                                             //Debug.Log($"Found room: {newRoom.Name} at {x}, {y}");
                    }
                }
                //Debug.Log($"Processed row {x} of {width}");
                if (tm.IfYield()) yield return null; // Yield to allow UI updates
            }
            //BottomBanner.Show($"Sorting {rooms.Count} rooms by size...");
            rooms.Sort((a, b) => b.Size.CompareTo(a.Size)); // Descending
            Debug.Log($"Finished room sorting.");
            //rooms = RemoveTinyRooms(rooms);
            generator.DrawMapByRooms(rooms);
            //yield return StartCoroutine(RemoveTinyRoomsCoroutine(tm:null));
            //rooms = new List<Room>(return_rooms);
            //ColorCodeRooms(rooms);

            //return rooms;
            //return_rooms = rooms;
            generator.rooms = rooms;
        }
        finally { if (local_tm) tm.End(); }
    }

    // Generic cluster finder: find connected components whose cells equal `target` (FLOOR or WALL)
    // Uses 4-way adjacency like FindRoomsCoroutine did.
    public IEnumerator FindClustersCoroutine(byte[,] map, byte target, List<Room> outRooms, TimeTask tm = null)
    {
        bool local_tm = false;
        if (tm == null) { tm = TimeManager.Instance.BeginTask("FindClustersCoroutine"); local_tm = true; }
        try
        {
            outRooms.Clear();
            int width = map.GetLength(0);
            int height = map.GetLength(1);
            bool[,] visited = new bool[width, height];

            Vector2Int[] directions = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };

            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    if (!visited[x, y] && map[x, y] == target)
                    {
                        Room cluster = new Room();
                        Queue<Vector2Int> q = new Queue<Vector2Int>(16);
                        q.Enqueue(new Vector2Int(x, y));
                        visited[x, y] = true;

                        while (q.Count > 0)
                        {
                            var p = q.Dequeue();
                            cluster.tiles.Add(p);

                            foreach (var d in directions)
                            {
                                int nx = p.x + d.x;
                                int ny = p.y + d.y;
                                if (nx >= 0 && ny >= 0 && nx < width && ny < height &&
                                    !visited[nx, ny] && map[nx, ny] == target)
                                {
                                    q.Enqueue(new Vector2Int(nx, ny));
                                    visited[nx, ny] = true;
                                }
                            }

                            // Periodic yield to keep UI responsive on large clusters
                            if ((cluster.tiles.Count & 0x1FFF) == 0)
                                if (tm.IfYield()) yield return null;
                        }

                        cluster.Name = $"Cluster {outRooms.Count + 1} ({cluster.tiles.Count} tiles)";
                        outRooms.Add(cluster);
                        if (tm.IfYield()) yield return null; // let UI breathe between clusters
                    }
                }
                // optional progress log
                // Debug.Log($"Cluster finder processed col {x} of {width}");
            }
        }
        finally { if (local_tm) tm.End(); }
    }

    // Remove clusters smaller than cfg.MinimumRoomSize by repainting them to `replacement` (FLOOR or WALL)
    public IEnumerator RemoveTinyClustersCoroutine(List<Room> clusters, int minimumSize, byte replacement, TileBase replacementTile = null, TimeTask tm = null)
    {
        bool local_tm = false;
        if (tm == null) { tm = TimeManager.Instance.BeginTask("RemoveTinyClustersCoroutine"); local_tm = true; }
        try
        {
            bool Done = false;
            while (!Done)
            {
                Done = true;
                for (int i = 0; i < clusters.Count; i++)
                {
                    var room = clusters[i];
                    if (room.Size < minimumSize)
                    {
                        foreach (var t in room.tiles)
                        {
                            var pos = new Vector3Int(t.x, t.y, 0);
                            generator.map[t.x, t.y] = replacement; // flip to replacement
                                                                   // Clear visuals;
                            if (replacementTile != null)
                                tilemap.SetTile(pos, replacementTile);
                            else
                                ClearTileAndNeighborWalls(tilemap, pos);
                        }
                        clusters.RemoveAt(i);
                        Done = false;
                        if (tm.IfYield()) yield return null; // UI breathe
                        break;
                    }
                }
            }
            if (tm.IfYield()) yield return null;
        }
        finally { if (local_tm) tm.End(); }
    }

    public IEnumerator RemoveTinyRoomsCoroutine(TimeTask tm = null)
    {
        bool local_tm = false;
        if (tm == null) { tm = TimeManager.Instance.BeginTask("RemoveTinyRoomsCoroutine"); local_tm = true; }
        try
        {
            // 1) Find Floor clusters
            generator.rooms = new List<Room>();
            yield return StartCoroutine(FindClustersCoroutine(generator.map, FLOOR, generator.rooms, tm: null));
            // 2) Remove the tiny ones by turning them into WALL
            yield return StartCoroutine(RemoveTinyClustersCoroutine(generator.rooms, cfg.MinimumRoomSize, WALL, null, tm: null));
            // 3) Redraw (floor/wall visuals updated by DrawMapFromByteArray)
            //DrawMapFromByteArray();
            if (tm.IfYield()) yield return null;
        }
        finally { if (local_tm) tm.End(); }
    }

    public IEnumerator RemoveTinyRocksCoroutine(TimeTask tm = null)
    {
        bool local_tm = false;
        if (tm == null) { tm = TimeManager.Instance.BeginTask("RemoveTinyRocksCoroutine"); local_tm = true; }
        try
        {
            // 1) Find WALL clusters
            var islands = new List<Room>(128);
            yield return StartCoroutine(FindClustersCoroutine(generator.map, WALL, islands, tm: null));
            // 2) Remove the tiny ones by turning them into FLOOR
            yield return StartCoroutine(RemoveTinyClustersCoroutine(islands, cfg.MinimumRockSize, FLOOR, floorTile, tm: null));
            // 3) Redraw (floor/wall visuals updated by DrawMapFromByteArray)
            //DrawMapFromByteArray();
            if (tm.IfYield()) yield return null;
        }
        finally { if (local_tm) tm.End(); }
    }

    public Room SetRoomToHeight(Room room, int setHeight)
    {
        for (int i = 0; i < room.heights.Count; i++)
        {
            room.heights[i] = setHeight;
        }
        return room;
    }

    public int GetHeightOfLocationFromOneRoom(Room room, Vector2Int pos)
    {
        for (int i = 0; i < room.Size; i++)
        {
            if (room.tiles[i] == pos)
            {
                return room.heights[i];
            }
        }
        //Debug.Log("location not found in room");
        return int.MaxValue; // not found
    }

    public int GetHeightOfLocationFromAllRooms(List<Room> rooms, Vector2Int pos)
    {
        int height;
        foreach (var room in rooms)
        {
            height = GetHeightOfLocationFromOneRoom(room, pos);
            if (height != int.MaxValue) return height; // found it
        }
        Debug.Log("location not found in rooms");
        return 0; //int.MaxValue;
    }

    public String ListOfIntToString(List<int> ilist, bool do_sort = true)
    {
        String result = "List: ";
        if (do_sort) ilist.Sort();
        foreach (int i in ilist)
        {
            result = result + i + ",";
        }
        return result;
    }

    // replaced local rooms list by indexes to global rooms list
    public IEnumerator ConnectRoomsByCorridors(TimeTask tm = null)
    {
        bool local_tm = false;
        if (tm == null) { tm = TimeManager.Instance.BeginTask("ConnectRoomsByCorridors"); local_tm = true; }
        try
        {
            List<int> unconnected_rooms = new(); // start with all, and then remove as rooms are merged
            Vector2Int center_i = Vector2Int.zero;
            Vector2Int close_i = Vector2Int.zero;
            Vector2Int close_j = Vector2Int.zero;
            int connection_room_i = -1, connection_room_j = -1;

            BottomBanner.Show($"Connecting {generator.rooms.Count} rooms by corridors...");

            // initialize unconnected rooms to include all room indexes
            for (int room_no = 0; room_no < generator.rooms.Count; room_no++)
                unconnected_rooms.Add(room_no);

            while (unconnected_rooms.Count > 1)
            {
                //Debug.Log("unconnected_rooms = " + ListOfIntToString(unconnected_rooms));
                //Debug.Log("Room "+ +" neighbor_rooms = " + ListOfIntToString(unconnected_rooms));
                for (int xx = 0; xx < generator.rooms.Count; xx++)
                {
                    List<int> all_connected_to_xx = get_union_of_connected_room_indexes(xx);
                    Debug.Log("NEIGHBORS of Room " + xx + "; neighbors  = " + ListOfIntToString(generator.rooms[xx].neighbors));
                    Debug.Log("NEIGHBORS of Room " + xx + "; all_connected_to_xx  = " + ListOfIntToString(all_connected_to_xx));
                }

                // Find two closest rooms (i and j),
                // and a point in each close to the other (close_i, close_j)
                // (not guaranteed to be THE closest, but good enough for room connections)
                Vector2Int closestPair = FindTwoClosestRooms(unconnected_rooms);
                if (closestPair == Vector2Int.zero)
                {
                    Debug.Log("Problem? No pairs found but unconnected_rooms.Count = " + unconnected_rooms.Count);
                    break; // no pairs found, exit loop
                }
                int i = closestPair.x;
                int j = closestPair.y;
                List<Vector2Int> all_tiles_i = get_union_of_connected_room_cells(i);
                List<Vector2Int> all_tiles_j = get_union_of_connected_room_cells(j);

                // Closest points between rooom i and room j.
                center_i = GetCenterOfTiles(all_tiles_i);
                close_j = GetClosestPointInTilesList(all_tiles_j, center_i);
                close_i = GetClosestPointInTilesList(all_tiles_i, close_j);

                connection_room_i = -1;  // initial assumptions to be adjusted in for loop
                connection_room_j = -1;
                for (int rn = 0; rn < generator.rooms.Count; rn++)
                {
                    if (generator.rooms[rn].tiles.Contains(close_i))
                    {
                        connection_room_i = rn;
                    }
                    if (generator.rooms[rn].tiles.Contains(close_j))
                    {
                        connection_room_j = rn;
                    }
                }
                if (connection_room_i == -1)
                {
                    Debug.Log("ERROR: No connection_room_i(" + i + ") found");
                    //yield return tm.YieldOrDelay(5f);
                }
                if (connection_room_j == -1)
                {
                    Debug.Log("ERROR: No connection_room_j(" + j + ") found");
                    //yield return tm.YieldOrDelay(5f);
                }
                // find height of each corridor endpoint, limiting search to specific room
                int height_i = GetHeightOfLocationFromOneRoom(generator.rooms[connection_room_i], close_i);
                int height_j = GetHeightOfLocationFromOneRoom(generator.rooms[connection_room_j], close_j);

                // Carve the corridor and create a new room of it
                Room corridorRoom = generator.DrawCorridorSloped(close_i, close_j, height_i, height_j);
                corridorRoom.isCorridor = true; // Mark as corridor
                corridorRoom.Name = $"Corridor {connection_room_i}-{connection_room_j}";
                corridorRoom.setColorFloor(highlight: false); // Set corridor color
                corridorRoom.neighbors = new();

                // connect the two rooms and the new corridor via connected_rooms lists
                corridorRoom.neighbors.Add(connection_room_i);
                corridorRoom.neighbors.Add(connection_room_j);
                int corridor_room_no = generator.rooms.Count;
                corridorRoom.my_room_number = corridor_room_no;
                generator.rooms.Add(corridorRoom); // add new corridor room to the master list
                generator.rooms[connection_room_i].neighbors.Add(corridor_room_no);
                generator.rooms[connection_room_j].neighbors.Add(corridor_room_no);

                // Remove second room (j) from unconnected rooms list
                for (var index = 0; index < unconnected_rooms.Count; index++)
                {
                    if (unconnected_rooms[index] == j)
                    {
                        unconnected_rooms.RemoveAt(index);
                        break; // found it, done removing j from unconnected rooms list
                    }
                }

                DrawMapFromRoomsList(generator.rooms);
                generator.DrawWalls();
                //yield return tm.YieldOrDelay(cfg.stepDelay / 3);
                if (tm.IfYield()) yield return null;
            }
            //DrawMapFromRoomsList(connected_rooms);
            //yield return StartCoroutine(generator.DrawWalls());
            if (tm.IfYield()) yield return null;
        }
        finally { if (local_tm) tm.End(); }
    }

    // TODO: not very efficient
    public Vector2Int FindTwoClosestRooms(List<int> unconnected_rooms)
    {
        if (unconnected_rooms.Count < 2) return Vector2Int.zero; // not enough rooms

        Vector2Int closestPair = Vector2Int.zero;
        float minDistance = float.MaxValue;

        for (int i = 0; i < generator.rooms.Count; i++)
        {
            if (!unconnected_rooms.Contains(i)) continue;  // i is not a unique room

            List<Vector2Int> room_cells_i = get_union_of_connected_room_cells(i);
            Vector2Int center_i = GetCenterOfTiles(room_cells_i);

            for (int j = i + 1; j < generator.rooms.Count; j++)
            {
                if (!unconnected_rooms.Contains(j)) continue;  // j is not a unique room

                List<Vector2Int> room_cells_j = get_union_of_connected_room_cells(j);
                Vector2Int center_j = GetCenterOfTiles(room_cells_j);

                float distance = Vector2Int.Distance(center_i, center_j);

                if (distance < minDistance)
                {
                    minDistance = distance;
                    closestPair = new Vector2Int(i, j);
                }
            }
        }

        return closestPair;
    }

    public Vector2Int GetCenterOfTiles(List<Vector2Int> tiles)
    {
        if (tiles.Count == 0) return new Vector2Int(0, 0);

        int minX = int.MaxValue, minY = int.MaxValue;
        int maxX = int.MinValue, maxY = int.MinValue;

        foreach (var t in tiles)
        {
            if (t.x < minX) minX = t.x;
            if (t.y < minY) minY = t.y;
            if (t.x > maxX) maxX = t.x;
            if (t.y > maxY) maxY = t.y;
        }

        return new Vector2Int(((minX + maxX) / 2), (minY + maxY) / 2);
    }

    // create a complete list of all rooms connected, ignoring duplicates
    List<int> get_union_of_connected_room_indexes(int start_room_number, bool everything = true)
    {
        bool added = true;
        List<int> rooms_to_connect = new();
        rooms_to_connect.Add(start_room_number);
        rooms_to_connect.AddRange(generator.rooms[start_room_number].neighbors);

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
                for (int j = 0; j < generator.rooms[rooms_to_connect[i]].neighbors.Count; j++)
                {
                    if (!rooms_to_connect.Contains(generator.rooms[rooms_to_connect[i]].neighbors[j]))
                    {
                        rooms_to_connect.Add(generator.rooms[rooms_to_connect[i]].neighbors[j]);
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
            union_of_cells.AddRange(generator.rooms[rooms_to_connect[i]].tiles);
        }

        //Debug.Log("get_union_of_connected_room_cells(" + start_room_number + ") -> length " + union_of_cells.Count + " END");
        return union_of_cells;
    }

    // Get the closest floor tile location in this room to a given target location
    public Vector2Int GetClosestPointInTilesList(List<Vector2Int> tile_list, Vector2Int target)
    {
        int min_distance = int.MaxValue;
        int cur_distance = int.MaxValue;
        Vector2Int closest_point = Vector2Int.zero;

        if (tile_list.Count == 0) return Vector2Int.zero;

        foreach (var t in tile_list)
        {
            cur_distance = (t - target).sqrMagnitude;
            if (cur_distance < min_distance)
            {
                min_distance = cur_distance;
                closest_point = t;
            }
        }

        return closest_point;
    }

    // Obsolete?  This really combined the room contents, which
    // we no longer do, we instead maintain a list of neighbors.
    void MergeRooms(Room keep, Room merge, List<Vector2Int> corridor)
    {
        var combined = new HashSet<Vector2Int>(keep.tiles);
        foreach (var t in merge.tiles) combined.Add(t);
        foreach (var c in corridor) combined.Add(c);

        keep.tiles = new List<Vector2Int>(combined);
    }

    void ClearTileAndNeighborWalls(Tilemap tilemap, Vector3Int cellPos)
    {
        // Square filled radius 2
        var squareR2 = NeighborCache.Get(2, NeighborCache.Shape.Square, borderOnly: false, includeDiagonals: true);
        //Debug.Log($"Clearing tile at {cellPos} and its neighbors with square radius 2");

        foreach (var offset in squareR2)
        {
            var neighbor = cellPos + offset;
            if (tilemap.GetTile(neighbor) == wallTile)
                tilemap.SetTile(neighbor, null);
        }

        tilemap.SetTile(cellPos, null); // Clear the main tile
    }

    Vector2Int[] directions_xy = { Vector2Int.up,
                                   Vector2Int.down,
                                   Vector2Int.left,
                                   Vector2Int.right,
                                   Vector2Int.up + Vector2Int.left,
                                   Vector2Int.up + Vector2Int.right,
                                   Vector2Int.down + Vector2Int.left,
                                   Vector2Int.down + Vector2Int.right };

    void BuildWallListsFromRooms()
    {
        for (var room_number = 0; room_number < generator.rooms.Count; room_number++)
        {
            List<Vector2Int> connected_floor_tiles = get_union_of_connected_room_cells(room_number, false);
            generator.rooms[room_number].walls = new();
            foreach (var pos in generator.rooms[room_number].tiles)
            {
                foreach (var dir in directions_xy)
                {
                    if (!connected_floor_tiles.Contains(pos + dir))
                    {
                        generator.rooms[room_number].walls.Add(pos + dir);
                        // Do we need to keep height for walls?
                        // No, they only are drawn as neighbors of a floor
                        // which already has a height.
                    }
                }
            }
        }

    }

    public Color getColor(Color? color = null, bool highlight = true, string rgba = "")
    {
        Color colorrgba = new(); //temp
        Color return_color = Color.white;

        if (color != null)
            return_color = (Color)color;
        else if ((!string.IsNullOrEmpty(rgba)) && (ColorUtility.TryParseHtmlString(rgba, out colorrgba)))
            return_color = colorrgba;
        else if (highlight)
            return_color = UnityEngine.Random.ColorHSV(0f, 1f, 0.6f, 1f, 0.6f, 1f);   // Bright Random
        else // highlight == false
            return_color = UnityEngine.Random.ColorHSV(0f, 1f, 0.6f, 1f, 0.1f, 0.4f); // Dark Random

        return return_color;
    }

}