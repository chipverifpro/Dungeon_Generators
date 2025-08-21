using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using System.Collections;
using System.Linq;

/* TODO list...
-- Simplex Noise
-- Round world
-- DONE: Nested Perlin / Stacked Perlin
-- DONE: Filter small wall areas
-- Presets of interesting dungeons - menu or random selection
-- Adding extra corridors to break up tree
-- Dirty MapArray
-- 3D walls with flythrough
-- More tile types: stairs, doors, traps
-- Fix early regeneration button (abort in-progress)
-- Fix pulldown after recompile
-- Don't show build progress option
-- Code cleanup for organization and optimization
-- Enforce minimum width room connectivity
 */

// Master Dungeon Generation Class...
public class DungeonGenerator : MonoBehaviour
{
    public DungeonSettings cfg; // Configurable settings for project

    // References to game components (set in Unity Inspector)
    public HeightMap3DBuilder heightBuilder;
    public Tilemap tilemap;
    public TileBase floorTile;
    public TileBase wallTile;
    // ---------------------------------------------------------------
    // Different ways to store the map: room(list of points) and mapArray (grid of bytes)
    public List<Room> rooms = new(); // Master List of rooms including list of points and metadata

    public List<RectInt> room_rects = new(); // List of RectInt rooms for ScatterRooms
    public List<int> room_rects_heights = new(); // List of heights for each room rectangle

    public byte[,] map; // each byte represents one of the below constants
    public int[,] mapHeights; // 2D array to store height information for each tile
    public bool mapStale = true; // Flag to indicate if map needs to be regenerated from rooms
    [HideInInspector] public byte WALL = 1;
    [HideInInspector] public byte FLOOR = 2;
    [HideInInspector] public byte RAMP = 3;
    // Additional tile types to be defined here

    public List<Color> room_rects_color = new();

    // Reference to CellularAutomata component for variables and methods there
    private CellularAutomata ca;

    // ------------------------------------- //
    // Start is called by Unity before the first frame update
    public void Start()
    {
        // Initialize randomizer
        if (cfg.randomizeSeed) cfg.seed = UnityEngine.Random.Range(0, 10000);
        UnityEngine.Random.InitState(cfg.seed);

        // Get reference to CellularAutomata component
        ca = GetComponent<CellularAutomata>();
        if (ca == null)
        {
            Debug.LogError("CellularAutomata component not found. Please add it to the DungeonGenerator GameObject.");
            Application.Quit();
        }

        // Start the fun...
        Debug.Log("DungeonGenerator started with seed: " + cfg.seed);

        StartCoroutine(RegenerateDungeon());
    }

    // RegenerateDungeon is the main coroutine that handles dungeon generation.
    // It orchestrates the various steps involved in creating the dungeon layout.
    // Step 0: Select settings
    // Step 1: Initialize the dungeon
    // Step 2: Place rooms (ScatterRooms or CellularAutomata)
    // Step 3: Convert rooms to a list of floor tiles (ConvertRectToRoomPoints or findRoomTiles for CA)
    // Step 4: Combine overlapping rooms (MergeOverlappingRooms)
    // Step 5: Connect rooms by corridors (DrawCorridors)

    // Other routines:
    //  Draw Map by Rooms
    //  Draw Walls

    public IEnumerator RegenerateDungeon()
    {
        room_rects = new List<RectInt>(); // Clear the list of room rectangles
        heightBuilder.Destroy3D();
        yield return null; // Start on a fresh screen render frame
        BottomBanner.Show("Generating dungeon...");

        // Step 0: Select settings
        switch (cfg.RoomAlgorithm) // Change this to select different algorithms
        {
            case DungeonSettings.DungeonAlgorithm_e.Scatter_Overlap:
                cfg.allowOverlappingRooms = true;
                cfg.useCellularAutomata = false;
                break;
            case DungeonSettings.DungeonAlgorithm_e.Scatter_NoOverlap:
                cfg.allowOverlappingRooms = false;
                cfg.useCellularAutomata = false;
                break;
            case DungeonSettings.DungeonAlgorithm_e.CellularAutomata:
                cfg.useCellularAutomata = true;
                cfg.usePerlin = false; // Disable Perlin for CA
                break;
            case DungeonSettings.DungeonAlgorithm_e.CellularAutomataPerlin:
                cfg.useCellularAutomata = true;
                cfg.usePerlin = true; // Enable Perlin for CA
                break;
        }

        BottomBanner.Show("Generating dungeon...");

        // ===== Step 1. Initialize the dungeon
        tilemap.ClearAllTiles();
        rooms.Clear();
        map = new byte[cfg.mapWidth, cfg.mapHeight];
        FillVoidToWalls(map);
        yield return new WaitForSeconds(cfg.stepDelay);

        // ===== Step 2. Place rooms
        if (cfg.useCellularAutomata) // Cellular Automata generation
        {
            BottomBanner.Show("Cellular Automata cavern generation itterating...");
            yield return StartCoroutine(ca.RunCaveGeneration());
            //DrawMapByRooms(rooms);
            yield return StartCoroutine(DrawWalls());
        }
        else // Scatter rooms
        {
            BottomBanner.Show("Scattering rooms...");
            yield return StartCoroutine(ScatterRooms());
            Debug.Log("ScatterRooms done, room_rects.Count = " + room_rects.Count);
            DrawMapByRects(room_rects, room_rects_color);
            yield return StartCoroutine(DrawWalls());
        }

        //DrawMapByRooms(rooms);
        //ca.ColorCodeRooms(rooms);
        //yield return StartCoroutine(DrawWalls());
        yield return new WaitForSeconds(cfg.stepDelay);

        // Step 3: Combine overlapping rooms
        BottomBanner.Show("Locate Discrete rooms...");
        if (cfg.useCellularAutomata) // locate rooms from cellular automata
        {
            BottomBanner.Show("Remove tiny rocks...");
            yield return StartCoroutine(ca.RemoveTinyRocksCoroutine());

            BottomBanner.Show("Remove tiny rooms...");
            yield return StartCoroutine(ca.RemoveTinyRoomsCoroutine());

            // For Cellular Automata, find rooms from the map
            yield return StartCoroutine(ca.FindRoomsCoroutine(map));
            rooms = new List<Room>(ca.return_rooms); // Get the rooms found by CA


        }
        else // locate rooms from scattered rooms
        {
            rooms = ConvertAllRectToRooms(room_rects, room_rects_color, SetTile: true);
            DrawMapByRooms(rooms);
            yield return StartCoroutine(DrawWalls());
            yield return new WaitForSeconds(cfg.stepDelay);
            // Step 4: Merge overlapping rooms
            BottomBanner.Show("Merging Overlapping Rooms...");
            rooms = MergeOverlappingRooms(rooms, considerAdjacency: true, eightWay: true);
            DrawMapByRooms(rooms);
            yield return StartCoroutine(DrawWalls());
            yield return new WaitForSeconds(cfg.stepDelay);
        }

        DrawMapByRooms(rooms);
        //ca.ColorCodeRooms(rooms);
        yield return StartCoroutine(DrawWalls());

        // Step 5: Connect rooms with corridors
        BottomBanner.Show("Connecting Rooms with Corridors...");
        yield return StartCoroutine(ca.ConnectRoomsByCorridors(rooms));

        DrawMapByRooms(rooms);
        //ca.ColorCodeRooms(rooms);
        yield return StartCoroutine(DrawWalls());
        yield return new WaitForSeconds(cfg.stepDelay * 5f);

        BottomBanner.Show("Height Map Build...");

        // Example: raise room centers by 1 step
        mapHeights = new int[cfg.mapWidth, cfg.mapHeight];
        foreach (var room in rooms)
        {
            var c = room.GetCenter();
            mapHeights[c.x, c.y] = 1;
        }
        // If Build should be called on an instance:
        FillVoidToWalls(map);
        heightBuilder.Build(map, mapHeights);
        // If Build should be static, change its definition to 'public static void Build(...)' in HeightMap3DBuilder.

        BottomBanner.ShowFor("Dungeon generation complete!", 5f);

    }

    // Scatter rooms performs the main room placement for Rectangular or Oval rooms
    IEnumerator ScatterRooms()
    {
        //List<Vector2Int> roomPoints = new List<Vector2Int>();
        tilemap.ClearAllTiles();
        room_rects.Clear(); // Clear the list of room rectangles
        room_rects_color.Clear(); // Clear the list of colors for room rectangles
        //rooms.Clear();
        BottomBanner.Show($"Scattering {cfg.roomsMax} Rooms...");
        for (int i = 0; room_rects.Count < cfg.roomsMax && i < cfg.roomAttempts; i++)
        {
            int w = UnityEngine.Random.Range(cfg.minRoomSize, cfg.maxRoomSize + 1);
            int h = UnityEngine.Random.Range(cfg.minRoomSize, cfg.maxRoomSize + 1);
            int x = UnityEngine.Random.Range(1, cfg.mapWidth - w - 1);
            int y = UnityEngine.Random.Range(1, cfg.mapHeight - h - 1);
            RectInt newRoom = new(x, y, w, h);

            // Check if the new room overlaps with existing rooms
            bool overlaps = false;
            foreach (var r in room_rects)
            {
                RectInt big_r = new(r.xMin - 1, r.yMin - 1, r.width + 2, r.height + 2);
                if (newRoom.Overlaps(big_r))
                {
                    overlaps = true;
                }
            }

            if (!overlaps || cfg.allowOverlappingRooms)
            {
                var newColor = UnityEngine.Random.ColorHSV(0f, 1f, 0.6f, 1f, 0.6f, 1f);
                room_rects.Add(newRoom);
                room_rects_color.Add(newColor);
                DrawRect(newRoom, newColor);
                //                roomPoints = ConvertRectToRoomPoints(newRoom, SetTile: true);
                //                rooms.Add(new Room(roomPoints));
                //                rooms[rooms.Count - 1].Name = "Room " + rooms.Count;
                Debug.Log("Created " + room_rects.Count() + " room_rects");
                yield return new WaitForSeconds(cfg.stepDelay / 3f);
            }
        }
        Debug.Log("room_rects.Count = " + room_rects.Count);
        //BottomBanner.Show($"DONE: Scattered {room_rects.Count} Rooms");
        //yield return null; // Ensure all tiles are set before proceeding
        //yield return StartCoroutine(ConnectRoomsByCorridors());
        yield return new WaitForSeconds(cfg.stepDelay); // Pause to see what happened
    }

    List<Room> ConvertAllRectToRooms(List<RectInt> room_rects, List<Color> room_rects_color, bool SetTile)
    {
        List<Vector2Int> PointsList;
        List<Room> rooms = new List<Room>();
        Debug.Log("Converting " + room_rects.Count + " Rects to Rooms...");
        for (int i = 0; i < room_rects.Count; i++)
        {
            var room_rect = room_rects[i];
            var room_rect_color = room_rects_color[i];
            PointsList = ConvertRectToRoomPoints(room_rect, room_rect_color, false/*SetTile*/);
            Room room = new Room(PointsList);
            room.isCorridor = false; // Default to false, can be set later if needed
            room.Name = "Room " + (rooms.Count + 1);
            //room.colorFloor = UnityEngine.Random.ColorHSV(0f, 1f, 0.6f, 1f, 0.6f, 1f); // Bright Random
            room.setColorFloor(room_rect_color);
            rooms.Add(room);
            DrawMapByRooms(rooms);
            //StartCoroutine(WaitForSeconds(cfg.stepDelay/3f)); // Pause to see what happened
        }
        return rooms;
    }

    // ConvertRectToRoomPoints generates a list of points within the
    //  given room rectangle or oval.
    // As a side effect, it can also set the corresponding tiles in the tilemap.
    List<Vector2Int> ConvertRectToRoomPoints(RectInt room_rect, Color room_rect_color, bool SetTile)
    {
        //BottomBanner.Show($"Measuring rooms...");
        List<Vector2Int> roomPoints = new List<Vector2Int>();
        for (int x = room_rect.xMin; x < room_rect.xMax; x++)
        {
            for (int y = room_rect.yMin; y < room_rect.yMax; y++)
            {
                if (IsPointInRoomRectOrOval(new Vector2Int(x, y), room_rect))
                {
                    roomPoints.Add(new Vector2Int(x, y));
                    if (SetTile)
                    {
                        tilemap.SetTile(new Vector3Int(x, y, 0), floorTile);
                        tilemap.SetTileFlags(new Vector3Int(x, y, 0), TileFlags.None); // Allow color changes
                        tilemap.SetColor(new Vector3Int(x, y, 0), room_rect_color); // Set default color
                    }
                }
            }
        }
        return roomPoints;
    }

    public void DrawMapByRects(List<RectInt> room_rects, List<Color> colors)
    {
        for (int i = 0; i < room_rects.Count; i++)
        {
            var room_rect = room_rects[i];
            var color = colors[i];
            Debug.Log("Drawing Rect " + room_rect);
            DrawRect(room_rect, color);
        }
    }

    public void DrawRect(RectInt room_rect, Color tempcolor)
    {
        Debug.Log("Drawing Rect ");
        //Color tempcolor = UnityEngine.Random.ColorHSV(0f, 1f, 0.6f, 1f, 0.6f, 1f); // Bright Random
        for (int x = room_rect.xMin; x < room_rect.xMax; x++)
        {
            for (int y = room_rect.yMin; y < room_rect.yMax; y++)
            {
                if (IsPointInRoomRectOrOval(new Vector2Int(x, y), room_rect))
                {
                    tilemap.SetTile(new Vector3Int(x, y, 0), floorTile);
                    tilemap.SetTileFlags(new Vector3Int(x, y, 0), TileFlags.None); // Allow color changes
                    tilemap.SetColor(new Vector3Int(x, y, 0), tempcolor);

                    map[x, y] = FLOOR;
                }
            }
        }
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
                tilemap.SetTileFlags(new Vector3Int(point.x, point.y, 0), TileFlags.None); // Allow color changes
                tilemap.SetColor(new Vector3Int(point.x, point.y, 0), room.colorFloor); // Set room color
            }
            //ca.ColorCodeOneRoom(room);
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
                BottomBanner.Show("Drawing orthogonal corridors...");
                path = GridedLine(start, end);
                break;
            case DungeonSettings.TunnelsAlgorithm_e.TunnelsStraight:
                BottomBanner.Show("Drawing straight corridors...");
                path = BresenhamLine(start.x, start.y, end.x, end.y);
                break;
            case DungeonSettings.TunnelsAlgorithm_e.TunnelsOrganic:
                BottomBanner.Show("Drawing organic corridors...");
                path = OrganicLine(start, end);
                break;
            case DungeonSettings.TunnelsAlgorithm_e.TunnelsCurved:
                BottomBanner.Show("Drawing curved corridors...");
                path = GetComponent<BezierDraw>().DrawBezierCorridor(start, end);
                break;
            default:
                BottomBanner.Show("Drawing Bresenham corridors...");
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

                    map[tilePos.x, tilePos.y] = FLOOR; //Floor
                }
            }
        }
        return hashPath.ToList();
    }

    // Grided Line algorithm: creates an orthogonal line between two points.
    // Randomly starts with either x or y direction and makes just one turn.
    public List<Vector2Int> GridedLine(Vector2Int from, Vector2Int to)
    {
        List<Vector2Int> line = new List<Vector2Int>();
        Vector2Int current = from;
        bool xFirst = UnityEngine.Random.Range(0, 2) == 0;

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
        // Move in y direction
        while (current.y != to.y)
        {
            //tilemap.SetTile(new Vector3Int(current.x, current.y, 0), floorTile);
            line.Add(new Vector2Int(current.x, current.y));
            current.y += current.y < to.y ? 1 : -1;
        }
        // Move in x direction
        while (current.x != to.x)
        {
            //tilemap.SetTile(new Vector3Int(current.x, current.y, 0), floorTile);
            line.Add(new Vector2Int(current.x, current.y));
            current.x += current.x < to.x ? 1 : -1;
        }
        return line;
    }

    // Bresenham's Line algorithm: creates a straight line between two points.
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

    // Noisy Bresenham's Line algorithm: creates a straight line between two points with added jiggle noise.
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

            // Add UnityEngine.Random noise to the decision
            float noise = UnityEngine.Random.Range(-1f, 1f) * noiseStrength;

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

    // Organic Line algorithm: creates a wiggly line between two points.
    // Tends to first do a 45 degree diagonal and then switches to horizontal or vertical.
    // Not too bad for short lines as the jagginess is good.
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
            if (UnityEngine.Random.value < cfg.jitterChance)
            {
                if (UnityEngine.Random.value < 0.5f)
                    //dx = 0; // favor y
                    dx = UnityEngine.Random.value < 0.5f ? -1 : 1; // favor y
                else
                    //dy = 0; // favor x
                    dy = UnityEngine.Random.value < 0.5f ? -1 : 1; // favor y
            }

            current += new Vector2Int(dx, dy);
        }

        path.Add(end);
        return path;
    }

    public IEnumerator DrawWalls()  // from tilemap
    {
        BoundsInt bounds = tilemap.cellBounds;
        //BottomBanner.Show("Drawing walls...");
        for (int x = bounds.xMin - 1; x <= bounds.xMax + 1; x++)
        {
            for (int y = bounds.yMin - 1; y <= bounds.yMax + 1; y++)
            {
                Vector3Int pos = new(x, y, 0);
                if (tilemap.GetTile(pos) == floorTile)
                    continue;                       // Skip floor tiles
                if (HasFloorNeighbor(pos))
                    tilemap.SetTile(pos, wallTile); // add wall tile
                else
                    tilemap.SetTile(pos, null);     // Remove wall if no floor neighbor
            }
            //yield return null;  // slow things down
        }
        yield return null; // Final yield to ensure all tiles are set
    }

    // Check if a tile at position pos has a neighboring floor tile within the specified radius
    bool HasFloorNeighbor(Vector3Int pos, int radius = 1)
    {
        if (tilemap == null || floorTile == null) return false; // Safety check

        // Check all neighbors within the specified radius
        NeighborCache.Shape shape = cfg.neighborShape;
        bool includeDiagonals = cfg.includeDiagonals;
        var neighbors = NeighborCache.Get(radius, shape, borderOnly: true, includeDiagonals);

        foreach (var offset in neighbors)
        {
            Vector3Int neighborPos = pos + offset;
            if (tilemap.GetTile(neighborPos) == floorTile)
            {
                return true; // Found a floor tile neighbor
            }
        }
        return false; // No floor tile neighbors found
    }

    bool IsPointInRoomRectOrOval(Vector2Int point, RectInt room_rect)
    {
        if (cfg.ovalRooms == false)
        {
            // rectangular room check
            return point.x >= room_rect.xMin && point.x < room_rect.xMax && point.y >= room_rect.yMin && point.y < room_rect.yMax;
        }
        else
        {
            // Check if the point is within the ellipse defined by the room
            float centerX = room_rect.xMin + room_rect.width / 2f;
            float centerY = room_rect.yMin + room_rect.height / 2f;
            float radiusX = room_rect.width / 2f;
            float radiusY = room_rect.height / 2f;

            return Mathf.Pow((point.x - centerX) / radiusX, 2) + Mathf.Pow((point.y - centerY) / radiusY, 2) <= 1;
        }
    }

    //} // End class DungeonGenerator



    // ================================================= //

    //public static class RoomMergeUtil
    //{
    // Simple Union-Find/Disjoint Set (DSU=Disjoint Set Union)
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
    /// <param name="rooms">Input rooms (each has List<Vector2Int> tiles)</param>
    /// <param name="considerAdjacency">If true, merge when tiles are neighbors (4- or 8-connected)</param>
    /// <param name="eightWay">If adjacency is considered, choose 4-way or 8-way</param>
    public static List<Room> MergeOverlappingRooms(List<Room> rooms, bool considerAdjacency = false, bool eightWay = true)
    {
        //BottomBanner.Show("Merging overlapping rooms...");
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
            r.setColorFloor(highlight: true);
            merged.Add(r);
        }

        // (Optional) sort by size descending like your existing code
        merged.Sort((a, b) => b.Size.CompareTo(a.Size));
        return merged;
    }

    public void FillVoidToWalls(byte[,] map)
    {
        for (var y = 0; y < cfg.mapHeight; y++)
            for (var x = 0; x < cfg.mapWidth; x++)
            {
                if (map[x, y] == 0) map[x,y] = WALL;
            }
    }

}