using System.Collections;
using UnityEngine;
using UnityEngine.Tilemaps;
using System.Collections.Generic;

public class Room
{
    public List<Vector2Int> tiles = new List<Vector2Int>();
    public int Size => tiles.Count;

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
    [Header("Map Settings")]
    public int width = 150;
    public int height = 150;
    [Range(0, 100)] public int fillPercent = 45;
    public int totalSteps = 5;
    public float stepDelay = 0.3f;

    [Header("Perlin Noise Settings")]
    public bool usePerlin = true;
    public float noiseScale = 0.1f;
    public float noiseThreshold = 0.5f;

    [Header("Tiles & Tilemap")]
    public int MinimumRoomSize = 100; // Threshold for tiny rooms
    public int BorderSize = 5; // Size of the border around the map

    public DungeonGenerator generator;

    public Tilemap tilemap;
    public TileBase wallTile;
    public TileBase floorTile;

    public bool[,] map;
    private System.Random rng;


    public IEnumerator RunCaveGeneration()
    {
        map = new bool[width, height];
        RandomFillMap(map);

        // Draw initial map
        DrawMap();
        yield return new WaitForSeconds(stepDelay);

        for (int step = 0; step < totalSteps; step++)
        {
            map = RunSimulationStep(map);
            DrawMap();
            yield return new WaitForSeconds(stepDelay);
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
        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
            {
                int borderDistance = Mathf.Min(x, y, width - x - 1, height - y - 1);
                if (borderDistance == 1)
                    map[x, y] = true; // Set border tile to wall
                else if (borderDistance <= BorderSize)
                    // Setting a wide random border makes edges less sharp
                    map[x, y] = rng.Next(0, 100) < fillPercent;
                else
                    if (usePerlin && rng.Next(0, 100) < 60)
                    {
                        float noise = Mathf.PerlinNoise((x + seedX) * noiseScale, (y + seedY) * noiseScale);
                        map[x, y] = noise > noiseThreshold;
                    }
                else
                {
                    map[x, y] = rng.Next(0, 100) < fillPercent;
                }
            }
        return map;
    }

    bool[,] RunSimulationStep(bool[,] oldMap)
    {
        bool[,] newMap = new bool[width, height];

        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
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
                if (nx < 0 || ny < 0 || nx >= width || ny >= height)
                    count++;
                else if (map[nx, ny])
                    count++;
            }

        return count;
    }

    void DrawMap()
    {
        tilemap.ClearAllTiles();

        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
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
                pos.x + dir.x >= width || pos.y + dir.y >= height)
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
        Vector2Int close_zero = Vector2Int.zero;
        Vector2Int close_i = Vector2Int.zero;
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

        for (int i = 1; i < rooms.Count; i++)
        {
            // Connect each room to the first room, finding the closest points
            close_zero = rooms[0].GetClosestPointInRoom(rooms[i].GetCenter());
            close_i = rooms[i].GetClosestPointInRoom(close_zero);

            generator.DrawCorridor(close_zero, close_i);
        }

        foreach (var room in rooms)
        {
            Debug.Log($"Room size: {room.Size}, Bounds: {room.GetBounds()}");
        }
        DrawMap();
        ColorCodeRooms(rooms);

        return rooms;
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
                if (room.Size < MinimumRoomSize) // Arbitrary threshold for tiny rooms
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
}