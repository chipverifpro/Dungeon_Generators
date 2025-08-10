using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using System.Collections;
using System.Linq;

public class DungeonGenerator : MonoBehaviour
{
    public DungeonSettings cfg; // Configurable settings for project
    public Tilemap tilemap;
    public TileBase floorTile;
    public TileBase wallTile;

    List<RectInt> rect_rooms = new();   // being deprecated probably
    List<Room> rooms = new(); // List of rooms for Cellular Automata
    public float stepDelay = 0.2f;  // adjustable delay

    public void Start()
    {
        if (cfg.randomizeSeed)
        {
            cfg.seed = Random.Range(0, 10000);
        }
        Random.InitState(cfg.seed);
        RegenerateDungeon();
    }

    public void RegenerateDungeon()
    {
        StopAllCoroutines();
        switch (cfg.RoomAlgorithm) // Change this to select different algorithms
        {
            case DungeonSettings.DungeonAlgorithm_e.Scatter_Overlap:
                cfg.allowOverlappingRooms = true;
                StartCoroutine(ScatterRooms());
                break;
            case DungeonSettings.DungeonAlgorithm_e.Scatter_NoOverlap:
                cfg.allowOverlappingRooms = false;
                StartCoroutine(ScatterRooms());
                break;
            case DungeonSettings.DungeonAlgorithm_e.CellularAutomata:
                CellularAutomata ca = GetComponent<CellularAutomata>();
                if (ca != null)
                {
                    StartCoroutine(ca.RunCaveGeneration());
                }
                break;
        }
    }

    IEnumerator ScatterRooms()
    {
        List<Vector2Int> roomPoints = new List<Vector2Int>();
        List<Vector2Int> corridorPoints = new List<Vector2Int>();
        tilemap.ClearAllTiles();
        rect_rooms.Clear();

        for (int i = 0; rect_rooms.Count < cfg.roomsMax && i < cfg.roomAttempts; i++)
        {
            int w = Random.Range(cfg.minRoomSize, cfg.maxRoomSize + 1);
            int h = Random.Range(cfg.minRoomSize, cfg.maxRoomSize + 1);
            int x = Random.Range(1, cfg.mapWidth - w - 1);
            int y = Random.Range(1, cfg.mapHeight - h - 1);
            RectInt newRoom = new(x, y, w, h);
            if (rect_rooms.Count == 0)
            {
                // First room, no need to check for overlaps
                rect_rooms.Add(newRoom);
                
                roomPoints = DrawRoom(newRoom);
                rooms.Add(new Room(roomPoints));
                rooms[rooms.Count - 1].Name = "First Room";
                yield return new WaitForSeconds(cfg.stepDelay);
                continue;
            }

            // Check if the new room overlaps with existing rooms
            bool overlaps = false;
            foreach (var r in rect_rooms)
            {
                RectInt big_r = new(r.xMin - 1, r.yMin - 1, r.width + 2, r.height + 2);
                if (newRoom.Overlaps(big_r))
                {
                    overlaps = true;
                    //break;
                }
            }

            if (!overlaps || cfg.allowOverlappingRooms)
            {
                rect_rooms.Add(newRoom);
                roomPoints = DrawRoom(newRoom);
                rooms.Add(new Room(roomPoints));
                rooms[rooms.Count - 1].Name = "Room " + rooms.Count;
                yield return new WaitForSeconds(cfg.stepDelay);
            }
        }
        Debug.Log("rooms.Count = " + rect_rooms.Count);
        // Draw all the corridors between rooms
        for (var i = 1; i < rect_rooms.Count; i++)
        {
            Vector2Int PointA = PointInRoom(rect_rooms[i - 1]);
            Vector2Int PointB = PointInRoom(rect_rooms[i]);
            corridorPoints = DrawCorridor(PointA, PointB);
            rooms.Add(new Room(corridorPoints));
            rooms[rooms.Count - 1].Name = "Corridor from " + (i - 1) + " to " + i;
            yield return new WaitForSeconds(cfg.stepDelay);

        }
        // connect first and last room
        Vector2Int lastPoint = PointInRoom(rect_rooms[rect_rooms.Count - 1]);
        Vector2Int firstPoint = PointInRoom(rect_rooms[0]);
        corridorPoints = DrawCorridor(lastPoint, firstPoint);
        rooms.Add(new Room(corridorPoints));
        rooms[rooms.Count - 1].Name = "Corridor from " + (rect_rooms.Count - 1) + " to " + 0;

        yield return new WaitForSeconds(cfg.stepDelay);

        // Draw walls around the dungeon
        DrawWalls();
    }
    List<Vector2Int> DrawRoom(RectInt room)
    {
        List<Vector2Int> roomPoints = new List<Vector2Int>();
        for (int x = room.xMin; x < room.xMax; x++)
        {
            for (int y = room.yMin; y < room.yMax; y++)
            {
                if (IsPointInRoom(new Vector2Int(x, y), room))
                {
                    tilemap.SetTile(new Vector3Int(x, y, 0), floorTile);
                    roomPoints.Add(new Vector2Int(x, y));
                }
            }
        }
        return roomPoints;
    }

    public void DrawMapByRooms(List<Room> rooms)
    {
        tilemap.ClearAllTiles();
        foreach (var room in rooms)
        {
            foreach (var point in room.tiles)
            {
                tilemap.SetTile(new Vector3Int(point.x, point.y, 0), floorTile);
            }
        }
    }
    
    public List<Vector2Int> DrawCorridor(Vector2Int start, Vector2Int end)
    {
        List<Vector2Int> path;
        HashSet<Vector2Int> hashPath = new HashSet<Vector2Int>();

        switch (cfg.TunnelsAlgorithm)
        {
            case DungeonSettings.TunnelsAlgorithm_e.TunnelsOrthographic:
                path = GridedLine(start, end);
                break;
            case DungeonSettings.TunnelsAlgorithm_e.TunnelsStraight:
                path = BresenhamLine(start.x, start.y, end.x, end.y);
                break;
            case DungeonSettings.TunnelsAlgorithm_e.TunnelsOrganic:
                path = OrganicLine(start, end);
                break;
            case DungeonSettings.TunnelsAlgorithm_e.TunnelsCurved:
                path = GetComponent<BezierDraw>().DrawBezierCorridor(start, end);
                break;
            default:
                path = BresenhamLine(start.x, start.y, end.x, end.y);
                break;
        }

        Debug.Log("Drawing corridor length " + path.Count + " from " + start + " to " + end + " width " + cfg.corridorWidth + " using " + cfg.TunnelsAlgorithm);

        int brush_neg = -cfg.corridorWidth / 2;
        int brush_pos = brush_neg + cfg.corridorWidth;

        foreach (Vector2Int point in path)
        {
            // Square brush around each line point
            for (int dx = brush_neg; dx < brush_pos; dx++)
            {
                for (int dy = brush_neg; dy < brush_pos; dy++)
                {
                    Vector3Int tilePos = new Vector3Int(point.x + dx, point.y + dy, 0);
                    tilemap.SetTile(tilePos, floorTile);
                    if (cfg.RoomAlgorithm == DungeonSettings.DungeonAlgorithm_e.CellularAutomata)
                    {
                        CellularAutomata ca = GetComponent<CellularAutomata>();
                        if (ca != null)
                        {
                            ca.map[tilePos.x, tilePos.y] = false;
                            hashPath.Add(new Vector2Int(tilePos.x, tilePos.y));
                        }
                    }
                }
            }
        }
        return hashPath.ToList();
    }

    public List<Vector2Int> GridedLine(Vector2Int from, Vector2Int to)
    {
        List<Vector2Int> line = new List<Vector2Int>();
        Vector2Int current = from;
        bool xFirst = Random.Range(0, 2) == 0;

        if (xFirst)
        {
            // Move in x direction first
            while (current.x != to.x)
            {
                //tilemap.SetTile(new Vector3Int(current.x, current.y, 0), floorTile);
                line.Add(new Vector2Int(current.x, current.y));
                current.x += current.x < to.x ? 1 : -1;
            }
        }

        while (current.y != to.y)
        {
            //tilemap.SetTile(new Vector3Int(current.x, current.y, 0), floorTile);
            line.Add(new Vector2Int(current.x, current.y));
            current.y += current.y < to.y ? 1 : -1;
        }
        while (current.x != to.x)
        {
            //tilemap.SetTile(new Vector3Int(current.x, current.y, 0), floorTile);
            line.Add(new Vector2Int(current.x, current.y));
            current.x += current.x < to.x ? 1 : -1;
        }
        return line;
    }

    public List<Vector2Int> BresenhamLine(int x0, int y0, int x1, int y1)
    {
        List<Vector2Int> line = new List<Vector2Int>();

        int dx = Mathf.Abs(x1 - x0);
        int dy = Mathf.Abs(y1 - y0);
        int sx = x0 < x1 ? 1 : -1;
        int sy = y0 < y1 ? 1 : -1;
        int err = dx - dy;

        while (true)
        {
            line.Add(new Vector2Int(x0, y0));
            if (x0 == x1 && y0 == y1) break;

            int e2 = 2 * err;
            if (e2 > -dy) { err -= dy; x0 += sx; }
            if (e2 < dx) { err += dx; y0 += sy; }
        }

        return line;
    }

    public List<Vector2Int> NoisyBresenham(Vector2Int start, Vector2Int end)
    {
        List<Vector2Int> path = new List<Vector2Int>();
        float noiseStrength = cfg.jitterChance;
        int x0 = start.x;
        int y0 = start.y;
        int x1 = end.x;
        int y1 = end.y;

        int dx = Mathf.Abs(x1 - x0);
        int dy = Mathf.Abs(y1 - y0);
        int sx = x0 < x1 ? 1 : -1;
        int sy = y0 < y1 ? 1 : -1;
        int err = dx - dy;

        while (true)
        {
            path.Add(new Vector2Int(x0, y0));
            if (x0 == x1 && y0 == y1) break;

            int e2 = 2 * err;

            // Add random noise to the decision
            float noise = Random.Range(-1f, 1f) * noiseStrength;

            if (e2 + noise > -dy)
            {
                err -= dy;
                x0 += sx;
            }

            if (e2 + noise < dx)
            {
                err += dx;
                y0 += sy;
            }

            // Always force at least one step to avoid infinite loop
            if (x0 == path[path.Count - 1].x && y0 == path[path.Count - 1].y)
            {
                if (dx > dy)
                    x0 += sx;
                else
                    y0 += sy;
            }
        }

        return path;
    }

    public List<Vector2Int> OrganicLine(Vector2Int start, Vector2Int end)
    {
        List<Vector2Int> path = new List<Vector2Int>();
        Vector2Int current = start;

        while (current != end)
        {
            path.Add(current);

            Vector2Int direction = end - current;
            int dx = Mathf.Clamp(direction.x, -1, 1);
            int dy = Mathf.Clamp(direction.y, -1, 1);

            // Introduce a slight chance to “wiggle”
            if (Random.value < cfg.jitterChance)
            {
                if (Random.value < 0.5f)
                    //dx = 0; // favor y
                    dx = Random.value < 0.5f ? -1 : 1; // favor y
                else
                    //dy = 0; // favor x
                    dy = Random.value < 0.5f ? -1 : 1; // favor y
            }

            current += new Vector2Int(dx, dy);
        }

        path.Add(end);
        return path;
    }

    void DrawWalls()
    {
        BoundsInt bounds = tilemap.cellBounds;

        for (int x = bounds.xMin - 1; x <= bounds.xMax + 1; x++)
        {
            for (int y = bounds.yMin - 1; y <= bounds.yMax + 1; y++)
            {
                Vector3Int pos = new(x, y, 0);
                if (cfg.RoomAlgorithm == DungeonSettings.DungeonAlgorithm_e.CellularAutomata)
                {

                    if (/*(tilemap.GetTile(pos) == null) &&*/ (HasFloorNeighbor(pos)))
                    {
                        tilemap.SetTile(pos, wallTile);
                    }
                } else {
                    if (tilemap.GetTile(pos) == null && HasFloorNeighbor(pos))
                    {
                        tilemap.SetTile(pos, wallTile);
                    }
                }
            }
        }
    }

    bool HasFloorNeighbor(Vector3Int pos)
    {
        foreach (Vector3Int dir in Directions())
        {
            if (tilemap.GetTile(pos + dir) == floorTile)
                return true;
        }
        return false;
    }

    Vector2Int RoomCenter(RectInt room) => new(room.xMin + room.width / 2, room.yMin + room.height / 2);

    Vector2Int PointInRoom(RectInt room)
    {
        Vector2Int RoomPoint = new(Random.Range(room.xMin, room.xMin + room.width), Random.Range(room.yMin, room.yMin + room.height));
        while (!IsPointInRoom(RoomPoint, room))
        {
            RoomPoint = new(Random.Range(room.xMin, room.xMin + room.width), Random.Range(room.yMin, room.yMin + room.height));
        }
        return RoomPoint;
    }

    bool IsPointInRoom(Vector2Int point, RectInt room)
    {
        if (cfg.ovalRooms==false) {
            return point.x >= room.xMin && point.x < room.xMax && point.y >= room.yMin && point.y < room.yMax;
        } else {
            // Check if the point is within the ellipse defined by the room
            float centerX = room.xMin + room.width / 2f;
            float centerY = room.yMin + room.height / 2f;
            float radiusX = room.width / 2f;
            float radiusY = room.height / 2f;

            return Mathf.Pow((point.x - centerX) / radiusX, 2) + Mathf.Pow((point.y - centerY) / radiusY, 2) <= 1;
        }
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

}

