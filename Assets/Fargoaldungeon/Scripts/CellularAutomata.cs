using System.Collections;
using UnityEngine;
using UnityEngine.Tilemaps;
using System.Collections.Generic;
using UnityEngine.PlayerLoop;


public class Room
{
    // == Properties of the room
    public List<Vector2Int> tiles = new List<Vector2Int>();
    public List<int> heights = new List<int>(); // Heights for each tile in the room, used for 3D generation
    public int Size => tiles.Count;
    public string Name = "";
    public Color colorFloor = Color.white;
    public List<Room> neighbors = new List<Room>(); // List of neighboring rooms
    public bool isCorridor = false; // Indicate if this room is a corridor

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
    public RectInt GetBounds() // Ignores height
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

    public Vector2Int GetCenter()
    {
        RectInt bounds = GetBounds();
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

    // Set the color for the floor tiles in this room many ways...
    // room.setColorFloor(Color.white);        // White
    // room.setColorFloor(rgb: "#FF0000FF"); // Red
    // room.setColorFloor();                   // Bright Random
    // room.setColorFloor(highlight: false);   // Dark   Random
    // room.setColorFloor(highlight: true);    // Bright Random
    public Color setColorFloor(Color? color = null, bool highlight = true, string rgba = "")
    {
        Color colorrgba = new(); //temp

        if (color != null)
            colorFloor = (Color)color;
        else if ((!string.IsNullOrEmpty(rgba)) && (ColorUtility.TryParseHtmlString(rgba, out colorrgba)))
            colorFloor = colorrgba;
        else if (highlight)
            colorFloor = Random.ColorHSV(0f, 1f, 0.6f, 1f, 0.6f, 1f);   // Bright Random
        else // highlight == false
            colorFloor = Random.ColorHSV(0f, 1f, 0.6f, 1f, 0.1f, 0.4f); // Dark Random

        return colorFloor;
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

    //public List<Room> return_rooms = new List<Room>();

    public void Start()
    {
        WALL = generator.WALL;
        FLOOR = generator.FLOOR;
    }
    public IEnumerator RunCaveGeneration()
    {
        generator.map = new byte[cfg.mapWidth, cfg.mapHeight];
        RandomFillMap(generator.map);

        // Draw initial map
        DrawMapFromByteArray();
        yield return new WaitForSeconds(cfg.stepDelay);

        for (int step = 0; step < cfg.totalSteps; step++)
        {
            generator.map = RunSimulationStep(generator.map);
            DrawMapFromByteArray();
            yield return new WaitForSeconds(cfg.stepDelay);
        }
    }

    public byte[,] RandomFillMap(byte[,] map)
    {
        rng = new System.Random();

        float seedX = Random.Range(0f, 10000f);
        float seedY = Random.Range(0f, 10000f);
        for (int x = 0; x < cfg.mapWidth; x++)
            for (int y = 0; y < cfg.mapHeight; y++)
            {
                int borderDistance = Mathf.Min(x, y, cfg.mapWidth - x - 1, cfg.mapHeight - y - 1);
                if (borderDistance == 1)
                    map[x, y] = WALL; // Set border tile to wall
                else if (borderDistance <= cfg.softBorderSize)
                    // Setting a wide random border makes edges less sharp
                    map[x, y] = rng.Next(0, 100) < cfg.fillPercent ? WALL : FLOOR;
                else
                    if (cfg.usePerlin && rng.Next(0, 100) < (100 - cfg.noiseOverlay))
                {
                    float perlin1 = Mathf.PerlinNoise((x + seedX) * cfg.perlinScale, (y + seedY) * cfg.perlinScale);
                    float perlin2 = Mathf.PerlinNoise((x - seedX) * cfg.perlin2Scale, (y - seedY) * cfg.perlin2Scale);
                    float noise = (perlin1 + perlin2) / 2f; // Combine two noise layers
                    map[x, y] = noise > cfg.perlinThreshold ? WALL : FLOOR;
                }
                else
                {
                    map[x, y] = rng.Next(0, 100) < cfg.fillPercent ? WALL : FLOOR;
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

    public IEnumerator FindRoomsCoroutine(byte[,] map)
    {
        BottomBanner.Show("Finding rooms...");
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
                                yield return null;
                        }
                    }
                    newRoom.Name = $"Room {rooms.Count + 1} ({newRoom.tiles.Count} tiles)";
                    newRoom.setColorFloor(highlight: true);
                    rooms.Add(newRoom);
                    //Debug.Log($"Found room: {newRoom.Name} at {x}, {y}");
                }
            }
            Debug.Log($"Processed row {x} of {width}");
            yield return null; // Yield to allow UI updates
        }
        //BottomBanner.Show($"Sorting {rooms.Count} rooms by size...");
        rooms.Sort((a, b) => b.Size.CompareTo(a.Size)); // Descending
        Debug.Log($"Finished room sorting.");
        //rooms = RemoveTinyRooms(rooms);
        generator.DrawMapByRooms(rooms);
        yield return StartCoroutine(RemoveTinyRoomsCoroutine());
        //rooms = new List<Room>(return_rooms);
        //ColorCodeRooms(rooms);

        //return rooms;
        //return_rooms = rooms;
    }

    // Generic cluster finder: find connected components whose cells equal `target` (FLOOR or WALL)
    // Uses 4-way adjacency like FindRoomsCoroutine did.
    public IEnumerator FindClustersCoroutine(byte[,] map, byte target, List<Room> outRooms)
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
                            yield return null;
                    }

                    cluster.Name = $"Cluster {outRooms.Count + 1} ({cluster.tiles.Count} tiles)";
                    outRooms.Add(cluster);
                    yield return null; // let UI breathe between clusters
                }
            }
            // optional progress log
            // Debug.Log($"Cluster finder processed col {x} of {width}");
        }
    }

    // Remove clusters smaller than cfg.MinimumRoomSize by repainting them to `replacement` (FLOOR or WALL)
    public IEnumerator RemoveTinyClustersCoroutine(List<Room> clusters, int minimumSize, byte replacement, TileBase replacementTile = null)
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
                    yield return null; // UI breathe
                    break;
                }
            }
        }
        yield return null;
    }

    public IEnumerator RemoveTinyRoomsCoroutine()
    {
        // 1) Find Floor clusters
        generator.rooms = new List<Room>();
        yield return StartCoroutine(FindClustersCoroutine(generator.map, FLOOR, generator.rooms));
        // 2) Remove the tiny ones by turning them into WALL
        yield return StartCoroutine(RemoveTinyClustersCoroutine(generator.rooms, cfg.MinimumRoomSize, WALL, null));
        // 3) Redraw (floor/wall visuals updated by DrawMapFromByteArray)
        //DrawMapFromByteArray();
    }

    public IEnumerator RemoveTinyRocksCoroutine()
    {
        // 1) Find WALL clusters
        var islands = new List<Room>(128);
        yield return StartCoroutine(FindClustersCoroutine(generator.map, WALL, islands));
        // 2) Remove the tiny ones by turning them into FLOOR
        yield return StartCoroutine(RemoveTinyClustersCoroutine(islands, cfg.MinimumRockSize, FLOOR, floorTile));
        // 3) Redraw (floor/wall visuals updated by DrawMapFromByteArray)
        //DrawMapFromByteArray();
    }


    public IEnumerator ConnectRoomsByCorridors(List<Room> master_list_of_rooms)
    {
        List<Room> connected_rooms = new(master_list_of_rooms);
        Vector2Int close_i = Vector2Int.zero;
        Vector2Int close_j = Vector2Int.zero;
        BottomBanner.Show($"Connecting {connected_rooms.Count} rooms by corridors...");
        while (connected_rooms.Count > 1)
        {
            List<Vector2Int> corridor_points = new List<Vector2Int>();
            Vector2Int closestPair = FindTwoClosestRooms(connected_rooms);
            if (closestPair == Vector2Int.zero)
            {
                Debug.Log("No more pairs of rooms to connect.");
                break; // No pairs found, exit loop
            }
            int i = closestPair.x;
            int j = closestPair.y;
            // Closest points between rooom i and j.
            close_i = connected_rooms[i].GetClosestPointInRoom(connected_rooms[i].GetCenter());
            close_j = connected_rooms[j].GetClosestPointInRoom(close_i);
            close_i = connected_rooms[i].GetClosestPointInRoom(close_j);

            // 1) Carve the corridor (your existing visual/path)
            corridor_points = generator.DrawCorridor(close_i, close_j);
            Room corridorRoom = new Room(corridor_points);
            //ColorCodeOneRoom(connected_rooms[i], highlight: false);
            //ColorCodeOneRoom(connected_rooms[j], highlight: false);
            //ColorCodeOneRoom(corridorRoom, highlight: false);
            corridorRoom.isCorridor = true; // Mark as corridor
            corridorRoom.Name = $"Corridor {i}-{j}";
            corridorRoom.setColorFloor(highlight: false); // Set corridor color
            corridorRoom.neighbors.Add(connected_rooms[i]);
            corridorRoom.neighbors.Add(connected_rooms[j]);
            connected_rooms[i].neighbors.Add(corridorRoom);
            connected_rooms[j].neighbors.Add(corridorRoom);

            // 2) Compute the cells along the corridor and mark them as floor in the map
            /*var corridorTiles = GetLineTiles(close_zero, close_i);
            foreach (var p in corridorTiles)
            {
                if (p.x >= 0 && p.y >= 0 && p.x < cfg.mapWidth && p.y < cfg.mapHeight)
                    map[p.x, p.y] = false; // corridor is floor
            }
*/
            // 3) Merge this room into the main room and remove it from the list
            //BottomBanner.Show($"Merging rooms {i}({rooms[i].tiles.Count}) and {j}({rooms[j].tiles.Count}) and Corridor({corridor_points.Count}) tiles");
            MergeRooms(connected_rooms[i], connected_rooms[j], corridor_points);
            generator.rooms.Add(corridorRoom); // Add corridor to the generator's room list
            //BottomBanner.Show($"Merged room size: {connected_rooms[i].tiles.Count} tiles");
            connected_rooms.RemoveAt(j);
            DrawMapFromRoomsList(generator.rooms);
            yield return StartCoroutine(generator.DrawWalls());
            yield return new WaitForSeconds(cfg.stepDelay / 3f);
        }
        //DrawMapFromRoomsList(connected_rooms);
        yield return null;
    }

    public Vector2Int FindTwoClosestRooms(List<Room> rooms)
    {
        if (rooms.Count < 2) return Vector2Int.zero;

        Vector2Int closestPair = Vector2Int.zero;
        float minDistance = float.MaxValue;

        for (int i = 0; i < rooms.Count; i++)
        {
            for (int j = i + 1; j < rooms.Count; j++)
            {
                Vector2Int centerA = rooms[i].GetCenter();
                Vector2Int centerB = rooms[j].GetCenter();
                float distance = Vector2Int.Distance(centerA, centerB);

                if (distance < minDistance)
                {
                    minDistance = distance;
                    closestPair = new Vector2Int(i, j);
                }
            }
        }

        return closestPair;
    }
      
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

}