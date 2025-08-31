using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using UnityEditor.Rendering;
using UnityEngine;

public class HeightMap3DBuilder : MonoBehaviour
{
    public DungeonGenerator generator;
    public Grid grid;                         // same Grid as the 2D Tilemap
    public float unitHeight = 0.1f;             // world Y per step
    public GameObject floorPrefab;
    public GameObject rampPrefab;             // oriented to face +Z
    public GameObject cliffPrefab;            // a 1x1x1 pillar you can scale in Y
    public Transform root;                    // parent for spawned meshes
    public bool onlyPerimeterWalls = true;    // skip deep interior walls

    public GameObject diagonalWallPrefab;    // thin strip or quad oriented along +Z
    public bool useDiagonalCorners = true;
    public bool skipOrthogonalWhenDiagonal = true;
    public int perimeterWallSteps = 30; // height of perimeter faces in steps

    [HideInInspector] public const byte WALL = 1;
    [HideInInspector] public const byte FLOOR = 2;
    [HideInInspector] public const byte RAMP = 3;
    [HideInInspector] public const byte UNKNOWN = 99;


    // If your ramp mesh "forward" is +Z, map directions to rotations:
    static readonly Vector2Int[] Dir4 = { new(0, 1), new(1, 0), new(0, -1), new(-1, 0) };
    static Quaternion RotFromDir(Vector2Int d)
    {
        if (d == new Vector2Int(0, 1)) return Quaternion.Euler(0, 0, 0);   // face +Z
        if (d == new Vector2Int(1, 0)) return Quaternion.Euler(0, 90, 0);
        if (d == new Vector2Int(0, -1)) return Quaternion.Euler(0, 180, 0);
        return Quaternion.Euler(0, 270, 0); // (-1,0)
    }

    // 45° yaw helpers
    static readonly Quaternion Yaw45 = Quaternion.Euler(0, -45, 0);
    static readonly Quaternion Yaw135 = Quaternion.Euler(0, -135, 0);
    static readonly Quaternion Yaw225 = Quaternion.Euler(0, -225, 0);
    static readonly Quaternion Yaw315 = Quaternion.Euler(0, -315, 0);

    static Vector3 CornerOffset(bool east, bool north, Vector3 cell)
    {
        // Don't offset, leaving wall diagonally across the center of the tile.
        float ox = (east ? +1f : -1f) * (cell.x * 0f);
        float oz = (north ? +1f : -1f) * (cell.y * 0f); // grid.y maps to world Z

        // Offset from tile center toward a corner (¼ cell each axis)
        //float ox = (east  ? +1f : -1f) * (cell.x * 0.25f);
        //float oz = (north ? +1f : -1f) * (cell.y * 0.25f); // grid.y maps to world Z
        return new Vector3(ox, 0f, oz);
    }

    static float DiagonalInsideLength(Vector3 cell)
    {
        // Lenght of strip across the center of the tile (corner to corner):
        float hx = cell.x * 1f;
        float hz = cell.y * 1f;
        // Length of a strip across the tile on a 45° diagonal (midpoint to midpoint):
        //float hx = cell.x * 0.5f;
        //float hz = cell.y * 0.5f;
        return Mathf.Sqrt(hx * hx + hz * hz);
    }

    // if root exists, destroy all 3D objects under it.
    public void Destroy3D()
    {
        if (root == null) return;
        for (int i = root.childCount - 1; i >= 0; i--)
            Destroy(root.GetChild(i).gameObject);
    }

    // TODO: make a version that does Build from Rooms list
    //  --This would allow rooms above/below other rooms
    // 3D Build routine from map and heights.  Places prefabs in correct places.
    //   Includes floors, walls, ramps, cliffs
    public IEnumerator Build3DFromRooms(TimeTask tm = null)
    {
        bool local_tm = false;
        if (tm == null) { tm = TimeManager.Instance.BeginTask("Build3DFromRooms"); local_tm = true; }
        try
        {
            Vector3 mid = new();
            Vector3 world = new();
            Vector3 nWorld = new();

            if (root == null) root = new GameObject("Terrain3D").transform; // TODO: get existing game object?
                                                                            // Clear old objects
            Destroy3D();

            //int w = map.GetLength(0), hi = map.GetLength(1);
            Vector3 cell = grid.cellSize;

            for (int room_number = 0; room_number < generator.rooms.Count; room_number++)
            {
                if (tm.IfYield()) yield return null;
                string room_name = generator.rooms[room_number].name;
                int num_tiles = generator.rooms[room_number].tiles.Count;
                for (int tile_number = 0; tile_number < num_tiles; tile_number++)
                {
                    if((tile_number % 500) == 0) if (tm.IfYield()) yield return null;
                    Vector2Int pos = generator.rooms[room_number].tiles[tile_number];
                    int x = pos.x;
                    int z = pos.y;
                    int ySteps = generator.rooms[room_number].heights[tile_number];
                    bool isFloor = true;
                    Color colorFloor = generator.rooms[room_number].colorFloor;
                    //bool isWall = false; //unused

                    // NOT RELEVANT: THIS IS DEFINITELY A FLOOR
                    // Optionally skip walls that are not adjacent to floor (visual cleanliness/perf)
                    //if (!isFloor && onlyPerimeterWalls && HasAdjacentTileFromRoom(room_number, pos, WALL)) continue;

                    // Base world position of this tile center
                    world = grid.CellToWorld(new Vector3Int(x, z, 0));

                    // If your Grid's tile anchor isn't centered, you may want to offset by cell * 0.5f
                    // world += new Vector3(cell.x * 0.5f, 0, cell.y * 0.5f); // uncomment if needed

                    // -------- diagonal corner smoothing (before orthogonal perimeter faces) --------
                    bool suppressN = false, suppressE = false, suppressS = false, suppressW = false;

                    if (useDiagonalCorners && isFloor && diagonalWallPrefab != null)
                    {
                        bool N = IsTileFromRoom(room_number, pos + Dir4[0], WALL);
                        bool E = IsTileFromRoom(room_number, pos + Dir4[1], WALL);
                        bool S = IsTileFromRoom(room_number, pos + Dir4[2], WALL);
                        bool W = IsTileFromRoom(room_number, pos + Dir4[3], WALL);

                        // if zero or one sides are walls, then nothing will happen here, so skip extra calculations
                        // if three sides are walls, don't replace with diagonals and leave as three walls (yucky X arrangement)
                        int num_walls = (N ? 1 : 0) + (S ? 1 : 0) + (E ? 1 : 0) + (W ? 1 : 0);

                        if (num_walls == 2)  // must have exactly two walls to use diagonal wall
                        {
                            float floorY = ySteps * unitHeight;
                            float wallH = Mathf.Max(1, perimeterWallSteps) * unitHeight;
                            float diagLen = DiagonalInsideLength(cell);
                            Vector3 baseY = new Vector3(0f, floorY + wallH * 0.5f, 0f);

                            // NE corner (N & E)
                            if (N && E /* && NE */)
                            {
                                var t = Instantiate(diagonalWallPrefab,
                                    world + CornerOffset(east: true, north: true, cell) + baseY,
                                    Yaw45, root);
                                t.transform.localScale = new Vector3(cell.x * 0.1f, wallH, diagLen);
                                if (skipOrthogonalWhenDiagonal) { suppressN = true; suppressE = true; }
                            }
                            // NW corner (N & W)
                            if (N && W /* && NW */)
                            {
                                var t = Instantiate(diagonalWallPrefab,
                                    world + CornerOffset(east: false, north: true, cell) + baseY,
                                    Yaw315, root);
                                t.transform.localScale = new Vector3(cell.x * 0.1f, wallH, diagLen);
                                if (skipOrthogonalWhenDiagonal) { suppressN = true; suppressW = true; }
                            }
                            // SE corner (S & E)
                            if (S && E /* && SE */)
                            {
                                var t = Instantiate(diagonalWallPrefab,
                                    world + CornerOffset(east: true, north: false, cell) + baseY,
                                    Yaw135, root);
                                t.transform.localScale = new Vector3(cell.x * 0.1f, wallH, diagLen);
                                if (skipOrthogonalWhenDiagonal) { suppressS = true; suppressE = true; }
                            }
                            // SW corner (S & W)
                            if (S && W /* && SW */)
                            {
                                var t = Instantiate(diagonalWallPrefab,
                                    world + CornerOffset(east: false, north: false, cell) + baseY,
                                    Yaw225, root);
                                t.transform.localScale = new Vector3(cell.x * 0.1f, wallH, diagLen);
                                if (skipOrthogonalWhenDiagonal) { suppressS = true; suppressW = true; }
                            }
                        }
                    }
                    // -------- end diagonal corner smoothing --------

                    // Place floor at its height (Y is up)
                    if (isFloor && floorPrefab != null)
                    {
                        var f = Instantiate(floorPrefab, world + new Vector3(0, ySteps * unitHeight, 0), Quaternion.identity, root);
                        f.name = room_name;
                        f.transform.localScale = new Vector3(cell.x, 1f, cell.y); // thickness 1; adjust as needed
                        var renderer = f.GetComponent<MeshRenderer>();
                        if (renderer != null)
                            renderer.material.color = colorFloor;
                    }

                    // Compare with 4 neighbors and add perimeter walls or ramps/cliffs
                    for (int i = 0; i < 4; i++)
                    {
                        Vector2Int d = Dir4[i];
                        int nx = x + d.x;
                        int nz = z + d.y;
                        //if (nx < 0 || nz < 0 || nx >= w || nz >= hi) continue; // off-map

                        bool nIsFloor = IsTileFromRoom(room_number, new Vector2Int(nx, nz), FLOOR);
                        bool nIsWall = IsTileFromRoom(room_number, new Vector2Int(nx, nz), WALL);

                        // If current is FLOOR and neighbor is WALL => perimeter face (unless diagonal suppressed)
                        if (isFloor && nIsWall && cliffPrefab != null)
                        {
                            // Respect suppress flags for the matching direction
                            if ((d.x == 0 && d.y == 1 && /*N*/ suppressN) ||
                                (d.x == 1 && d.y == 0 && /*E*/ suppressE) ||
                                (d.x == 0 && d.y == -1 && /*S*/ suppressS) ||
                                (d.x == -1 && d.y == 0 && /*W*/ suppressW))
                            {
                                // skip orthogonal face; diagonal already placed
                            }
                            else
                            {
                                // location of the neighboring cell
                                nWorld = grid.CellToWorld(new Vector3Int(nx, nz, 0));
                                // midpoint between the two cells
                                mid = 0.5f * (world + nWorld);

                                int floorSteps = GetHeightFromRoom(room_number, pos);
                                float ht = Mathf.Max(1, perimeterWallSteps) * unitHeight;
                                float baseY = floorSteps * unitHeight;

                                var face = Instantiate(cliffPrefab,
                                    mid + new Vector3(0, baseY + 0.5f * ht, 0),
                                    RotFromDir(new Vector2Int(nx - x, nz - z)),
                                    root);

                                face.transform.localScale = new Vector3(cell.x, ht, cell.y * 0.1f);
                            }
                        }

                        // Only consider transitions between walkable tiles, or visualize room->void edges as cliffs if you prefer
                        if (!(isFloor && nIsFloor)) continue;

                        int nySteps = GetHeightFromRoom(room_number, new Vector2Int(nx, nz));
                        int diff = nySteps - ySteps;
                        if (diff == 0) continue;

                        // Place transition geometry centered between the two tiles (for walls only)
                        nWorld = grid.CellToWorld(new Vector3Int(nx, nz, 0));
                        mid = (world + nWorld) * 0.5f;

                        if (Mathf.Abs(diff) == 1 && rampPrefab != null)
                        {
                            // Ramp spans from lower to higher tile
                            bool up = diff > 0;
                            if (up) continue; // don't create two ramps, one from each side, instead pick 'down'
                                              // Place ramp slightly biased toward lower side so the top aligns cleanly
                            int lower = up ? ySteps : nySteps;
                            var rot = RotFromDir(d * (up ? 1 : -1)); // face uphill
                                                                     //var ramp = Instantiate(rampPrefab, mid + new Vector3(0, (lower + 1.0f) * unitHeight, 0), rot, root);
                            var ramp = Instantiate(rampPrefab, nWorld + new Vector3(0, (lower + 1.0f) * unitHeight, 0), rot, root);
                            ramp.transform.localScale = new Vector3(cell.x, unitHeight, cell.y); // length matches cell, height equals one step
                        }
                        else if (Mathf.Abs(diff) >= 2 && cliffPrefab != null)
                        {
                            bool up = diff > 0;
                            if (up) continue; // don't create two walls, one from each side, instead pick 'down'
                                              // Vertical face for a bigger step; center vertically between heights
                            int minStep = Mathf.Min(ySteps, nySteps);
                            float heightWorld = Mathf.Abs(diff) * unitHeight;
                            var face = Instantiate(cliffPrefab, mid + new Vector3(0, (minStep * unitHeight) + heightWorld * 0.5f, 0), RotFromDir(d), root);
                            // Scale so its Y matches the height difference; X/Z to cell dimensions
                            face.transform.localScale = new Vector3(cell.x, heightWorld, cell.y * 0.1f); // thin face; adjust thickness
                        }
                    }
                }
                //if (tm.IfYield()) yield return null;
            }
        }
        finally { if (local_tm) tm.End(); }
    }


    // floor neighbor check (not including diagonals)
    bool HasFloorNeighbor(byte[,] map, int x, int z)
    {
        int w = map.GetLength(0), h = map.GetLength(1);
        if (z + 1 < h && map[x, z + 1] == FLOOR) return true;
        if (x + 1 < w && map[x + 1, z] == FLOOR) return true;
        if (z - 1 >= 0 && map[x, z - 1] == FLOOR) return true;
        if (x - 1 >= 0 && map[x - 1, z] == FLOOR) return true;
        return false;
    }

    int GetHeightFromRoom(int room_number, Vector2Int pos)
    {
        for (int i = 0; i < generator.rooms[room_number].tiles.Count; i++)
        {
            if (generator.rooms[room_number].tiles[i] == pos)
                return generator.rooms[room_number].heights[i];
        }
        return 999;
    }
    byte GetTileFromRoom(int room_number, Vector2Int pos)
    {  
        if (generator.rooms[room_number].wall_hash_room.Contains(pos))
            return FLOOR;
        if (generator.rooms[room_number].floor_hash_room.Contains(pos))
            return WALL;
        return UNKNOWN;
    }

    bool IsTileFromRoom(int room_number, Vector2Int pos, byte tile_type)
    {
        // TODO: get hash_room and hash_walls from room_number
        if (tile_type == FLOOR)
            if (generator.rooms[room_number].floor_hash_room.Contains(pos))
                return true;
        if (tile_type == WALL)
            if (generator.rooms[room_number].wall_hash_room.Contains(pos))
                return true;
        return false;
    }

    bool HasAdjacentTileFromRoom(int room_number, Vector2Int pos, byte tile_type)
    {
        HashSet<Vector2Int> hash_room = new();
        Vector2Int npos;
        // TODO: get hash_room(room_number) and hash_walls(room_number)
        
        for (int dir = 0; dir < 4; dir++) // look for a floor in 4 directions
            {
                npos = pos + Dir4[dir];
                if (tile_type == FLOOR)
                    if (generator.rooms[room_number].floor_hash_room.Contains(npos))
                        return true;
                if (tile_type == WALL)
                    if (generator.rooms[room_number].wall_hash_room.Contains(npos))
                        return true;
            }
        return false;
    }
}