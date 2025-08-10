using System.Collections;
using UnityEngine;
using UnityEngine.Tilemaps;
using System.Collections.Generic;

public class Room
{
    public List<Vector2Int> tiles = new List<Vector2Int>();
    public int Size => tiles.Count;
    public string Name = "";

    public Room(List<Vector2Int> initialTiles)
    {
        tiles = initialTiles;
    }
    public Room() { }

    public RectInt GetBounds()
    {
        if (tiles.Count == 0) return new RectInt();

        int minX = int.MaxValue, minY = int.MaxValue;
        int maxX = int.MinValue, maxY = int.MinValue;

        foreach (var t in tiles)
        {
            if (t.x < minX) minX = t.x;
            if (t.y < minY) minY = t.y;
            if (t.x > maxX) maxX = t.x;
            if (t.y > maxY) maxY = t.y;
        }

        return new RectInt(minX, minY, maxX - minX + 1, maxY - minY + 1);
    }

    public Vector2Int GetCenter()
    {
        if (tiles.Count == 0) return Vector2Int.zero;

        int sumX = 0, sumY = 0;
        foreach (var t in tiles)
        {
            sumX += t.x;
            sumY += t.y;
        }

        return new Vector2Int(sumX / tiles.Count, sumY / tiles.Count);
    }

    public Vector2Int GetClosestPointInRoom(Vector2Int point)
    {
        Vector2Int center = point;
        int min_distance = 9999;
        int cur_distance = 9999;
        Vector2Int closest_point = Vector2Int.zero;

        if (tiles.Count == 0) return Vector2Int.zero;

        foreach (var t in tiles)
        {
            cur_distance = (t - center).sqrMagnitude;
            if (cur_distance < min_distance)
            {
                min_distance = cur_distance;
                closest_point = t;
            }
        }

        return closest_point;
    }
}
public class CellularAutomata : MonoBehaviour
{
    public DungeonSettings cfg; // Reference to the DungeonSettings ScriptableObject

    public DungeonGenerator generator;

    public Tilemap tilemap;
    public TileBase wallTile;
    public TileBase floorTile;

    public bool[,] map;
    private System.Random rng;

    // Incremental draw cache: 0 = no tile, 1 = floorTile, 2 = wallTile
    private bool _hasPrevVisual;
    private byte[,] _prevKind;

    public IEnumerator RunCaveGeneration()
    {
        map = new bool[cfg.mapWidth, cfg.mapHeight];
        RandomFillMap(map);

        // Draw initial map
        DrawMap();
        yield return new WaitForSeconds(cfg.stepDelay);

        for (int step = 0; step < cfg.totalSteps; step++)
        {
            map = RunSimulationStep(map);
            DrawMap();
            yield return new WaitForSeconds(cfg.stepDelay);
        }

        Debug.Log("Cave generation complete.");

        FindRooms(map);
        //DrawMap();
    }

    public bool[,] RandomFillMap(bool[,] map)
    {
        rng = new System.Random();

        float seedX = Random.Range(0f, 100f);
        float seedY = Random.Range(0f, 100f);
        for (int x = 0; x < cfg.mapWidth; x++)
            for (int y = 0; y < cfg.mapHeight; y++)
            {
                int borderDistance = Mathf.Min(x, y, cfg.mapWidth - x - 1, cfg.mapHeight - y - 1);
                if (borderDistance == 1)
                    map[x, y] = true; // Set border tile to wall
                else if (borderDistance <= cfg.softBorderSize)
                    // Setting a wide random border makes edges less sharp
                    map[x, y] = rng.Next(0, 100) < cfg.fillPercent;
                else
                    if (cfg.usePerlin && rng.Next(0, 100) < (100 - cfg.noiseOverlay))
                {
                    float noise = Mathf.PerlinNoise((x + seedX) * cfg.perlinScale, (y + seedY) * cfg.perlinScale);
                    map[x, y] = noise > cfg.perlinThreshold;
                }
                else
                {
                    map[x, y] = rng.Next(0, 100) < cfg.fillPercent;
                }
            }
        return map;
    }

    bool[,] RunSimulationStep(bool[,] oldMap)
    {
        bool[,] newMap = new bool[cfg.mapWidth, cfg.mapHeight];

        for (int x = 0; x < cfg.mapWidth; x++)
            for (int y = 0; y < cfg.mapHeight; y++)
            {
                int walls = CountWallNeighbors(oldMap, x, y);
                if (oldMap[x, y])
                    newMap[x, y] = walls >= 3;
                else
                    newMap[x, y] = walls > 4;
            }

        return newMap;
    }

    int CountWallNeighbors(bool[,] map, int x, int y)
    {
        int count = 0;
        for (int nx = x - 1; nx <= x + 1; nx++)
            for (int ny = y - 1; ny <= y + 1; ny++)
            {
                if (nx == x && ny == y) continue;
                if (nx < 0 || ny < 0 || nx >= cfg.mapWidth || ny >= cfg.mapHeight)
                    count++;
                else if (map[nx, ny])
                    count++;
            }

        return count;
    }

    void DrawMap()
    {
        int w = cfg.mapWidth;
        int h = cfg.mapHeight;

        // Helper to decide what should be drawn in this cell right now
        // 0 = draw nothing, 1 = draw floorTile, 2 = draw wallTile
        byte DesiredKindAt(int x, int y)
        {
            // Your existing visibility rule:
            // place a tile if it's floor OR a wall that touches floor
            bool isWall = map[x, y];
            bool place = !isWall || HasFloorNeighbor(new Vector3Int(x, y, 0));
            if (!place) return 0;
            return (byte)(isWall ? 2 : 1);
        }

        // First draw or size changed: do a bulk build and snapshot
        if (!_hasPrevVisual || _prevKind == null ||
            _prevKind.GetLength(0) != w || _prevKind.GetLength(1) != h)
        {
            tilemap.ClearAllTiles();

            var positions = new List<Vector3Int>(w * h);
            var tiles     = new List<TileBase>(w * h);

            _prevKind = new byte[w, h];

            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                byte k = DesiredKindAt(x, y);
                _prevKind[x, y] = k;

                if (k != 0)
                {
                    positions.Add(new Vector3Int(x, y, 0));
                    tiles.Add(k == 1 ? floorTile : wallTile);
                }
            }

            if (positions.Count > 0)
                tilemap.SetTiles(positions.ToArray(), tiles.ToArray());

            _hasPrevVisual = true;
            return;
        }

        // Incremental diff: only touch cells whose visual kind changed
        var changedPos   = new List<Vector3Int>(256);
        var changedTiles = new List<TileBase>(256);

        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                byte curr = DesiredKindAt(x, y);
                if (_prevKind[x, y] != curr)
                {
                    changedPos.Add(new Vector3Int(x, y, 0));
                    // null removes the tile when curr == 0
                    changedTiles.Add(curr == 0 ? null : (curr == 1 ? floorTile : wallTile));
                    // inside the diff loop, after changedTiles.Add(...):
                    _prevKind[x, y] = curr;
                }
            /*if (curr == 0)*/ tilemap.SetColor(new Vector3Int(x, y, 0), Color.white);
            }

        if (changedPos.Count > 0)
            tilemap.SetTiles(changedPos.ToArray(), changedTiles.ToArray());
    }

    void DrawMap_old()
    {
        tilemap.ClearAllTiles();

        for (int x = 0; x < cfg.mapWidth; x++)
            for (int y = 0; y < cfg.mapHeight; y++)
            {
                Vector3Int pos = new Vector3Int(x, y, 0);
                if (map[x, y] == false || HasFloorNeighbor(pos))
                {
                    tilemap.SetTile(pos, map[x, y] ? wallTile : floorTile);
                }
            }
    }

    bool HasFloorNeighbor(Vector3Int pos)
    {
        foreach (Vector3Int dir in Directions())
        {
            if (pos.x + dir.x < 0 || pos.y + dir.y < 0 ||
                pos.x + dir.x >= cfg.mapWidth || pos.y + dir.y >= cfg.mapHeight)
                continue; // Out of bounds
            if (map[pos.x + dir.x, pos.y + dir.y] == false)
                return true;
        }
        return false;
    }
    public List<Vector3Int> Directions() => new()
    {
        new Vector3Int(1, 0, 0),
        new Vector3Int(-1, 0, 0),
        new Vector3Int(0, 1, 0),
        new Vector3Int(0, -1, 0),
        new Vector3Int(-1, -1, 0),
        new Vector3Int(-1, 1, 0),
        new Vector3Int(1, 1, 0),
        new Vector3Int(1, -1, 0)

    };

    public List<Room> FindRooms(bool[,] map)
    {
        Vector2Int close_i = Vector2Int.zero;
        Vector2Int close_j = Vector2Int.zero;
        int width = map.GetLength(0);
        int height = map.GetLength(1);
        bool[,] visited = new bool[width, height];
        List<Room> rooms = new List<Room>();

        Vector2Int[] directions = {
            Vector2Int.up, Vector2Int.down,
            Vector2Int.left, Vector2Int.right
        };

        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
            {
                if (!visited[x, y] && !map[x, y]) // Floor and unvisited
                {
                    Room newRoom = new Room();
                    Queue<Vector2Int> queue = new Queue<Vector2Int>();
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
                                !visited[nx, ny] && !map[nx, ny])
                            {
                                queue.Enqueue(new Vector2Int(nx, ny));
                                visited[nx, ny] = true;
                            }
                        }
                    }

                    rooms.Add(newRoom);
                }
            }

        rooms.Sort((a, b) => b.Size.CompareTo(a.Size)); // Descending
        rooms = RemoveTinyRooms(rooms);
        ColorCodeRooms(rooms);

        while (rooms.Count > 1)
        {
            List<Vector2Int> corridor_points = new List<Vector2Int>();
            Vector2Int closestPair = FindTwoClosestRooms(rooms);
            int i = closestPair.x;
            int j = closestPair.y;
            // Closest points between rooom i and j.
            close_i = rooms[i].GetClosestPointInRoom(rooms[i].GetCenter());
            close_j = rooms[j].GetClosestPointInRoom(close_i);
            close_i = rooms[i].GetClosestPointInRoom(close_j);
            
            // 1) Carve the corridor (your existing visual/path)
            corridor_points = generator.DrawCorridor(close_i, close_j);

            // TODO: Change DrawCorridor to return the corridor tiles

            // 2) Compute the cells along the corridor and mark them as floor in the map
            /*var corridorTiles = GetLineTiles(close_zero, close_i);
            foreach (var p in corridorTiles)
            {
                if (p.x >= 0 && p.y >= 0 && p.x < cfg.mapWidth && p.y < cfg.mapHeight)
                    map[p.x, p.y] = false; // corridor is floor
            }
*/
            // 3) Merge this room into the main room and remove it from the list
            MergeRooms(rooms[i], rooms[j], corridor_points);
            rooms.RemoveAt(j);
            //i--; // adjust index after removal
        }
        DrawMap();


        return rooms;
    }

    public Vector2Int FindTwoClosestRooms (List<Room> rooms)
    {
        if (rooms.Count < 2) return Vector2Int.zero;

        Vector2Int closestPair = Vector2Int.zero;
        float minDistance = float.MaxValue;

        for (int i = 0; i < rooms.Count - 1; i++)
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
  
    public void ColorCodeRooms(List<Room> rooms)
    {
        foreach (Room room in rooms)
        {
            Color color = Random.ColorHSV(0f, 1f, 0.6f, 1f, 0.6f, 1f); // Bright, visible colors

            foreach (Vector2Int tilePos in room.tiles)
            {
                Vector3Int pos = new Vector3Int(tilePos.x, tilePos.y, 0);

                tilemap.SetColor(pos, color);
            }
        }
    }

    List<Room> RemoveTinyRooms(List<Room> rooms)
    {
        bool Done = false;
        while (!Done)
        {
            Done = true; // Reset for each pass
            foreach (Room room in rooms)
            {
                if (room.Size < cfg.MinimumRoomSize) // Arbitrary threshold for tiny rooms
                {
                    foreach (Vector2Int tilePos in room.tiles)
                    {
                        Vector3Int pos = new Vector3Int(tilePos.x, tilePos.y, 0);
                        //tilemap.SetTile(pos, wallTile); // Remove room tile
                        map[tilePos.x, tilePos.y] = true; // Mark as wall
                    }
                    rooms.Remove(room);
                    Debug.Log($"Removed tiny room of size {room.Size} at bounds {room.GetBounds()}");
                    DrawMap(); // Redraw the map after removing tiny rooms
                    Done = false;
                    break; // Exit loop since we modified the list
                }
                else
                {
                    Debug.Log($"Keeping room of size {room.Size} at bounds {room.GetBounds()}");
                }
            }
        }
        return rooms;
    }
    
    void MergeRooms(Room keep, Room merge, List<Vector2Int> corridor)
    {
        var combined = new HashSet<Vector2Int>(keep.tiles);
        foreach (var t in merge.tiles) combined.Add(t);
        if (corridor != null)
            foreach (var c in corridor) combined.Add(c);

        keep.tiles = new List<Vector2Int>(combined);
    }
}