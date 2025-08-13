using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using System.Collections;
using System.Linq;


public class DungeonGenerator : MonoBehaviour
{
    public DungeonSettings cfg; // Configurable settings for project

    // References to game components (set in Unity Inspector)
    public Tilemap tilemap;
    public TileBase floorTile;
    public TileBase wallTile;

    // Different ways to store the map: rect_rooms, rooms, and mapArray
    public List<RectInt> rect_rooms = new(); // List of rooms as RectInt
    public List<Room> rooms = new(); // List of rooms includikng list of points and metadata

    public byte[,] mapArray; // each byte represents one of the below constants
    private const byte WALL = 0;
    private const byte FLOOR = 1;
    // Additional tile types to be defined here

    // Reference to CellularAutomata component for variables and methods there
    private CellularAutomata ca;
    public List<Room> caRooms;

    // Directions for neighbor search
    private static readonly Vector3Int[] Directions =
    {
        new Vector3Int(-1, 1, 0), // Up-Left
        new Vector3Int(0, 1, 0),  // Up
        new Vector3Int(1, 1, 0),  // Up-Right
        new Vector3Int(-1, 0, 0), // Left
        new Vector3Int(1, 0, 0),  // Right
        new Vector3Int(-1, -1, 0),// Down-Left
        new Vector3Int(0, -1, 0), // Down
        new Vector3Int(1, -1, 0)  // Down-Right
    };


    public void Start()
    {
        if (cfg.randomizeSeed)
        {
            cfg.seed = Random.Range(0, 10000);
        }
        Random.InitState(cfg.seed);
        Debug.Log("DungeonGenerator started with seed: " + cfg.seed);
        StopAllCoroutines();
        StartCoroutine(RegenerateDungeon());
    }

    public IEnumerator RegenerateDungeon()
    {
        yield return new WaitForSeconds(cfg.stepDelay);
        ca = GetComponent<CellularAutomata>();
        if (ca == null)
        {
            Debug.LogError("CellularAutomata component not found. Please add it to the DungeonGenerator GameObject.");
            yield break;
        }

        yield return null; // Wait for the end of the frame to ensure all previous operations are complete
        switch (cfg.RoomAlgorithm) // Change this to select different algorithms
        {
            case DungeonSettings.DungeonAlgorithm_e.Scatter_Overlap:
                cfg.allowOverlappingRooms = true;
                yield return StartCoroutine(ScatterRooms());
                //yield return StartCoroutine(ca.ConnectRoomsByCorridors(rooms));
                break;
            case DungeonSettings.DungeonAlgorithm_e.Scatter_NoOverlap:
                cfg.allowOverlappingRooms = false;
                yield return StartCoroutine(ScatterRooms());
                //yield return StartCoroutine(ca.ConnectRoomsByCorridors(rooms));
                break;
            case DungeonSettings.DungeonAlgorithm_e.CellularAutomata:
                rect_rooms = new(); // don't leave leftovers
                if (ca != null)
                {
                    cfg.usePerlin = false; // Disable Perlin for CA
                    tilemap.ClearAllTiles();
                    rect_rooms.Clear();
                    rooms.Clear();
                    yield return StartCoroutine(ca.RunCaveGeneration());
                    //rooms = ca.FindRooms(ca.map);
                    yield return StartCoroutine(ca.FindRoomsCoroutine(ca.map));
                    rooms = ca.return_rooms; // Get the rooms found by CA
                    Debug.Log($"Done FindRoomsCoroutine, rooms.Count = {rooms.Count}");
                    //yield return StartCoroutine(ca.ConnectRoomsByCorridors(rooms));
                    //ca.DrawMapFromRoomsList(rooms);
                    //DrawMapByRooms(rooms);
                    //yield return StartCoroutine(DrawWalls());
                    //ca.ColorCodeRooms(rooms);
                    //yield break;
                }
                break;
            case DungeonSettings.DungeonAlgorithm_e.CellularAutomataPerlin:
                rect_rooms = new(); // don't leave leftovers

                if (ca != null)
                {
                    cfg.usePerlin = true; // Enable Perlin for CA
                    tilemap.ClearAllTiles();
                    rect_rooms.Clear();
                    rooms.Clear();
                    yield return StartCoroutine(ca.RunCaveGeneration());
                    //rooms = ca.FindRooms(ca.map);
                    yield return StartCoroutine(ca.FindRoomsCoroutine(ca.map));
                    rooms = new List<Room>(ca.return_rooms); // Get the rooms found by CA
                    Debug.Log($"Done FindRoomsCoroutine, rooms.Count = {rooms.Count}");
                    //yield return StartCoroutine(ca.ConnectRoomsByCorridors(rooms));
                    //ca.DrawMapFromRoomsList(rooms);
                    //DrawMapByRooms(rooms);
                    //yield return StartCoroutine(DrawWalls());
                    //ca.ColorCodeRooms(rooms);
                    //yield break;
                }
                break;
        }
        yield return new WaitForSeconds(cfg.stepDelay);
        //rooms = FindRoomsByList(rooms); // Ensure rooms are found after generation
        //rooms = RoomMergeUtil.MergeOverlappingRooms(rooms, considerAdjacency: true, eightWay: false);
        DrawMapByRooms(rooms);
        ca.ColorCodeRooms(rooms);
        yield return new WaitForSeconds(cfg.stepDelay);
        yield return StartCoroutine(DrawWalls());
        yield return new WaitForSeconds(cfg.stepDelay);
        //yield break;
        // Draw the corridors between rooms
        yield return StartCoroutine(ca.ConnectRoomsByCorridors(rooms));
        //yield return StartCoroutine(ConnectRoomsByCorridors());
        DrawMapByRooms(rooms);
        // Draw walls around the dungeon
        yield return StartCoroutine(DrawWalls());
        //yield return StartCoroutine(DrawWalls());
        ca.ColorCodeRooms(rooms);
    }

    List<Room> FindRoomsByList(List<Room> rooms)
    {
        Debug.Log("Finding rooms by list...");
        HashSet<Vector2Int> HashMap = new HashSet<Vector2Int>();
        HashSet<int> found_matches = new HashSet<int>();
        Debug.Log($"Initial room count: {rooms.Count}");
        for (int r = 0; r < rooms.Count; r++)
        {
            for (int t = 0; t < rooms[r].tiles.Count; t++)
            {
                Vector2Int tile = rooms[r].tiles[t];
                // Check if the tile is already in the HashMap...
                HashMap.Add(tile);
                if (true)
                {
                    // Tile in room {r} exists in HashMap, indicating a match to an already explored room
                    // determine which rooms {rc} match and merge them
                    for (int rc = 0; rc < r; rc++)
                    {
                        if (rooms[rc].tiles.Contains(tile) && !found_matches.Contains(rc))
                        {
                            // Merge rooms
                            Debug.Log($"Queueing merge room rc={rc}({rooms[rc].tiles.Count}) into room r={r}({rooms[r].tiles.Count})");
                            found_matches.Add(rc);
                            //continue;
                        } // end Contains
                    } // end rc
                } // end !HashMap.Add
            } // end t
            print(found_matches.Count + " matches found to room r=" + r);
            //print($"Contents of found_matches: {found_matches.ToString()}");
            for (int rc = r - 1; rc >= 0; rc--)
            {
                if (found_matches.Contains(rc))
                {
                    Debug.Log($"Merging and removing rooms rc={rc} r={r} from list");
                    MergeRooms(rooms[rc], rooms[r]);
                    rooms.RemoveAt(r);
                    found_matches.Remove(rc);
                } // end found_matches
            } // end rc
            Debug.Log($"found_matches.count {found_matches.Count} should be zero");
            found_matches.Clear();
            Debug.Log($"room count after merging: {rooms.Count}");
        } // end r
        Debug.Log($"Final room count: {rooms.Count}");
        // DONE.  HashMap now contains all unique tiles from all rooms
        // AND any duplicates have been merged into the first matching room

        return rooms;

    }

    public void MergeRooms(Room keep, Room merge)
    {
        Debug.Log($"Merging room {merge.Name}({merge.tiles.Count}) into {keep.Name}({keep.tiles.Count})");
        var combined = new HashSet<Vector2Int>(keep.tiles);
        foreach (var t in merge.tiles) combined.Add(t);
        keep.tiles = new List<Vector2Int>(combined);
    }

    IEnumerator ScatterRooms()
    {
        List<Vector2Int> roomPoints = new List<Vector2Int>();

        tilemap.ClearAllTiles();
        rect_rooms.Clear();
        rooms.Clear();

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
                Debug.Log("Created " + rooms[rooms.Count - 1].Name + " size: " + rooms[rooms.Count - 1].Size);
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
                Debug.Log("Created " + rooms[rooms.Count - 1].Name + " size: " + rooms[rooms.Count - 1].Size);
                yield return new WaitForSeconds(cfg.stepDelay);
            }
        }
        Debug.Log("rooms.Count = " + rect_rooms.Count);
        yield return null; // Ensure all tiles are set before proceeding
        //yield return StartCoroutine(ConnectRoomsByCorridors());
    }

    IEnumerator ConnectRoomsByCorridors()
    {
        List<Vector2Int> corridorPoints = new List<Vector2Int>();
        Debug.Log("Connecting rooms...");
        if (rect_rooms.Count == 0)
        {
            Debug.LogWarning("No rooms to connect. Exiting ConnectRoomsByCorridors.");
            yield break;
        }
        if (rect_rooms.Count > 1)
        {
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
            /*
            Vector2Int lastPoint = PointInRoom(rect_rooms[rect_rooms.Count - 1]);
            Vector2Int firstPoint = PointInRoom(rect_rooms[0]);
            corridorPoints = DrawCorridor(lastPoint, firstPoint);
            rooms.Add(new Room(corridorPoints));
            rooms[rooms.Count - 1].Name = "Corridor from " + (rect_rooms.Count - 1) + " to " + 0;
            yield return new WaitForSeconds(cfg.stepDelay);
            */
        }
        // Draw walls around the dungeon
        yield return StartCoroutine(DrawWalls());
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
        Debug.Log("Drawing Map by " + rooms.Count + " rooms...");
        tilemap.ClearAllTiles();
        foreach (var room in rooms)
        {
            Debug.Log("Drawing " + room.Name + " size: " + room.tiles.Count);
            foreach (var point in room.tiles)
            {
                tilemap.SetTile(new Vector3Int(point.x, point.y, 0), floorTile);
            }
            ca.ColorCodeOneRoom(room);
        }
    }

    public List<Vector2Int> DrawCorridor(Vector2Int start, Vector2Int end)
    {
        CellularAutomata ca = GetComponent<CellularAutomata>();
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
                    if (tilePos.x < 0 || tilePos.x >= cfg.mapWidth || tilePos.y < 0 || tilePos.y >= cfg.mapHeight)
                    {
                        continue; // Skip out-of-bounds tiles
                    }
                    tilemap.SetTile(tilePos, floorTile);
                    hashPath.Add(new Vector2Int(tilePos.x, tilePos.y));

                    if (cfg.RoomAlgorithm == DungeonSettings.DungeonAlgorithm_e.CellularAutomata)
                    {
                        ca.map[tilePos.x, tilePos.y] = true; //Floor
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


    public IEnumerator DrawWalls()
    {
        BoundsInt bounds = tilemap.cellBounds;

        for (int x = bounds.xMin - 1; x <= bounds.xMax + 1; x++)
        {
            for (int y = bounds.yMin - 1; y <= bounds.yMax + 1; y++)
            {
                Vector3Int pos = new(x, y, 0);
                //if (cfg.RoomAlgorithm == DungeonSettings.DungeonAlgorithm_e.CellularAutomata)
                //{
                if (tilemap.GetTile(pos) == floorTile)
                    continue; // Skip floor tiles
                if (HasFloorNeighbor(pos))
                    tilemap.SetTile(pos, wallTile);
                else
                    tilemap.SetTile(pos, null); // Remove wall if no floor neighbor
                //}
                /*else
                {
                    if (tilemap.GetTile(pos) == null && HasFloorNeighbor(pos))
                    {
                        tilemap.SetTile(pos, wallTile);
                    }
                }*/
            }
            //yield return null;
        }
        yield return null; // Final yield to ensure all tiles are set
    }

    bool HasFloorNeighbor(Vector3Int pos)
    {
        for (int x = -cfg.wallThickness; x <= cfg.wallThickness; x++)
            for (int y = -cfg.wallThickness; y <= cfg.wallThickness; y++)
            {
                Vector3Int dir = new Vector3Int(x, y, 0);
                if (dir.x == 0 && dir.y == 0) continue; // Skip self
                if (pos.x + dir.x < 0 || pos.y + dir.y < 0 ||
                    pos.x + dir.x >= cfg.mapWidth || pos.y + dir.y >= cfg.mapHeight)
                    continue; // Out of bounds

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
        if (cfg.ovalRooms == false)
        {
            return point.x >= room.xMin && point.x < room.xMax && point.y >= room.yMin && point.y < room.yMax;
        }
        else
        {
            // Check if the point is within the ellipse defined by the room
            float centerX = room.xMin + room.width / 2f;
            float centerY = room.yMin + room.height / 2f;
            float radiusX = room.width / 2f;
            float radiusY = room.height / 2f;

            return Mathf.Pow((point.x - centerX) / radiusX, 2) + Mathf.Pow((point.y - centerY) / radiusY, 2) <= 1;
        }
    }

    byte[,] FillMapArrayFromRooms(List<Room> rooms)
    {
        byte[,] mapArray = new byte[tilemap.size.x, tilemap.size.y];
        foreach (var room in rooms)
        {
            foreach (var tile in room.tiles)
            {
                mapArray[(int)tile.x, (int)tile.y] = FLOOR;
            }
        }
        return mapArray;
    }

    public void FillTilesFromRooms()
    {
        tilemap.ClearAllTiles();

        // Fill the tilemap with the floor tiles from each room
        foreach (var room in rooms)
        {
            foreach (var point in room.tiles)
            {
                tilemap.SetTile(new Vector3Int(point.x, point.y, 0), floorTile);
            }
        }
        // Fill the tilemap with walls around the rooms
        for (int y = 0; y < tilemap.size.y; y++)
        {
            for (int x = 0; x < tilemap.size.x; x++)
            {
                Vector3Int pos = new Vector3Int(x, y, 0);
                if (tilemap.GetTile(pos) == null && HasFloorNeighbor(pos))
                {
                    tilemap.SetTile(pos, wallTile);
                }
            }
        }
    }

    public void FillTilesFromMapArray(byte[,] mapArray)
    {
        tilemap.ClearAllTiles();
        for (int x = 0; x < tilemap.size.x; x++)
            for (int y = 0; y < tilemap.size.y; y++)
            {
                switch (mapArray[x, y])
                {
                    case WALL:
                        tilemap.SetTile(new Vector3Int(x, y, 0), wallTile);
                        break;
                    case FLOOR:
                        tilemap.SetTile(new Vector3Int(x, y, 0), floorTile);
                        break;
                    default:
                        // Optionally handle other cases or leave empty
                        break;
                }
            }
    }

    byte[,] FillMapArrayFromTiles()
    {
        byte[,] mapArray = new byte[tilemap.size.x, tilemap.size.y];
        for (int x = 0; x < tilemap.size.x; x++)
        {
            for (int y = 0; y < tilemap.size.y; y++)
            {
                if (tilemap.GetTile(new Vector3Int(x, y, 0)) == floorTile)
                {
                    mapArray[x, y] = FLOOR;
                }
                else
                {
                    mapArray[x, y] = WALL;
                }
            }
        }
        return mapArray;
    }

}





public static class RoomMergeUtil
{
    // Simple Union-Find/Disjoint Set
    class DSU
    {
        int[] parent;
        int[] rank;

        public DSU(int n)
        {
            parent = new int[n];
            rank = new int[n];
            for (int i = 0; i < n; i++) parent[i] = i;
        }

        public int Find(int x)
        {
            if (parent[x] != x) parent[x] = Find(parent[x]);
            return parent[x];
        }

        public void Union(int a, int b)
        {
            a = Find(a); b = Find(b);
            if (a == b) return;
            if (rank[a] < rank[b]) parent[a] = b;
            else if (rank[a] > rank[b]) parent[b] = a;
            else { parent[b] = a; rank[a]++; }
        }
    }

    /// <summary>
    /// Merge rooms that overlap (share at least one tile).
    /// If considerAdjacency is true, rooms that touch by edge/corner are merged too.
    /// </summary>
    /// <param name="rooms">Input rooms (each has List&lt;Vector2Int&gt; tiles)</param>
    /// <param name="considerAdjacency">If true, merge when tiles are neighbors (4- or 8-connected)</param>
    /// <param name="eightWay">If adjacency is considered, choose 4-way or 8-way</param>
    public static List<Room> MergeOverlappingRooms(List<Room> rooms, bool considerAdjacency = false, bool eightWay = true)
    {
        if (rooms == null || rooms.Count == 0) return new List<Room>();

        var dsu = new DSU(rooms.Count);
        var owner = new Dictionary<Vector2Int, int>(1024);

        // Optional neighbor offsets for adjacency merging
        Vector2Int[] n4 = new[]
        {
            new Vector2Int( 1, 0),
            new Vector2Int(-1, 0),
            new Vector2Int( 0, 1),
            new Vector2Int( 0,-1)
        };
        Vector2Int[] n8 = new[]
        {
            new Vector2Int( 1, 0), new Vector2Int(-1, 0), new Vector2Int(0, 1), new Vector2Int(0,-1),
            new Vector2Int( 1, 1), new Vector2Int( 1,-1), new Vector2Int(-1, 1), new Vector2Int(-1,-1)
        };
        var neighbors = eightWay ? n8 : n4;

        // 1) Scan all tiles, union rooms that share tiles (overlap)
        for (int i = 0; i < rooms.Count; i++)
        {
            var tiles = rooms[i].tiles;
            for (int k = 0; k < tiles.Count; k++)
            {
                var t = tiles[k];

                if (!owner.TryGetValue(t, out int j))
                {
                    owner[t] = i; // first time we see this tile, claim it
                }
                else
                {
                    // tile already owned by room j => overlap with i
                    dsu.Union(i, j);
                }

                // 2) Optional: adjacency-based merging (touching rooms)
                if (considerAdjacency)
                {
                    foreach (var d in neighbors)
                    {
                        var n = t + d;
                        if (owner.TryGetValue(n, out int kOwner))
                            dsu.Union(i, kOwner);
                    }
                }
            }
        }

        // 3) Fold tiles into their root groups
        var grouped = new Dictionary<int, HashSet<Vector2Int>>();
        for (int i = 0; i < rooms.Count; i++)
        {
            int root = dsu.Find(i);
            if (!grouped.TryGetValue(root, out var set))
            {
                set = new HashSet<Vector2Int>();
                grouped[root] = set;
            }
            foreach (var t in rooms[i].tiles)
                set.Add(t);
        }

        // 4) Emit merged rooms (preserving your Room type)
        var merged = new List<Room>(grouped.Count);
        foreach (var kv in grouped)
        {
            var r = new Room();
            r.tiles = new List<Vector2Int>(kv.Value);
            merged.Add(r);
        }

        // (Optional) sort by size descending like your existing code
        merged.Sort((a, b) => b.Size.CompareTo(a.Size));
        return merged;
    }
}