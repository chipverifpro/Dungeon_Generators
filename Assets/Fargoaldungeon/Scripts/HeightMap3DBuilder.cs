using System.Collections;
using System.Collections.Generic;
using System.Data;
using Unity.Collections;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.UIElements;

public partial class DungeonGenerator : MonoBehaviour
{
    public float unitHeight = 0.1f;             // world Y per step
    public bool useDiagonalCorners = true;
    public bool skipOrthogonalWhenDiagonal = true;
    public int perimeterWallSteps = 30; // height of perimeter faces in steps

    public Dictionary<Vector2Int, int> idx;
    
/*    public Grid grid;                         // same Grid as the 2D Tilemap

                        public GameObject floorPrefab;
                        public GameObject rampPrefab;             // oriented to face +Z
                        public GameObject cliffPrefab;            // a 1x1x1 pillar you can scale in Y
                        public Transform root;                    // parent for spawned meshes

                        public GameObject diagonalWallPrefab;    // thin strip or quad oriented along +Z


                        [HideInInspector] public const byte WALL = 1;
                        [HideInInspector] public const byte FLOOR = 2;
                        [HideInInspector] public const byte RAMP = 3;
                        [HideInInspector] public const byte UNKNOWN = 99;
                    */

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

    // 3D Build routine from rooms list.  Places prefabs in correct places.
    //   Includes floors, walls, ramps, cliffs
    public IEnumerator Build3DFromRooms(TimeTask tm = null)
    {
        bool local_tm = false;
        if (tm == null) { tm = TimeManager.Instance.BeginTask("Build3DFromRooms"); local_tm = true; }
        try
        {
            if (root == null) root = new GameObject("Terrain3D").transform; // TODO: get existing game object?
                                                                            
            Destroy3D(); // Clear old objects

            for (int room_number = 0; room_number < rooms.Count; room_number++)
            {
                yield return StartCoroutine(Build3DFromOneRoom(room_number, tm: null));
                //if (tm.IfYield()) yield return null;
            }
        }
        finally { if (local_tm) tm.End(); }
    }
    
    public IEnumerator Build3DFromOneRoom(int room_number, TimeTask tm = null)
    {
        bool local_tm = false;
        if (tm == null) { tm = TimeManager.Instance.BeginTask("Build3DFromRooms"); local_tm = true; }
        try
        {
            Vector3 mid = new();
            Vector3 world = new();
            Vector3 nWorld = new();
            Vector3 cell = grid.cellSize;
            bool use_triangle_floor = false;
            int triangle_floor_dir = 0;
            
            // Cache once:
            var floorMR = (floorPrefab != null) ? floorPrefab.GetComponent<MeshRenderer>() : null;
            var wallMR  = (cliffPrefab != null) ? cliffPrefab.GetComponent<MeshRenderer>() : null;
            var triangleFloorMR = (triangleFloorPrefab != null) ? triangleFloorPrefab.GetComponent<MeshRenderer>() : null;

            //if (tm.IfYield()) yield return null;
            string room_name = rooms[room_number].name;
            int num_tiles = rooms[room_number].tiles.Count;
            for (int tile_number = 0; tile_number < num_tiles; tile_number++)
            {
                if ((tile_number % 500) == 0) if (tm.IfYield()) yield return null;
                Vector2Int pos = rooms[room_number].tiles[tile_number];
                int x = pos.x;
                int z = pos.y;
                int ySteps = rooms[room_number].heights[tile_number];
                bool isFloor = true;
                use_triangle_floor = false;
                Color colorFloor = rooms[room_number].colorFloor;
                //bool isWall = false; //unused

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
                        use_triangle_floor = true;
                        triangle_floor_dir = 0;
                        // NE corner (N & E)
                        if (N && E /* && NE */)
                        {
                            var t = Instantiate(diagonalWallPrefab,
                                world + CornerOffset(east: true, north: true, cell) + baseY,
                                Yaw45, root);
                            t.transform.localScale = new Vector3(cell.x * 0.1f, wallH, diagLen);
                            //t.name = $"Wall({room_name})";
                            triangle_floor_dir = 0;
                            if (skipOrthogonalWhenDiagonal) { suppressN = true; suppressE = true; }
                        }
                        // NW corner (N & W)
                        if (N && W /* && NW */)
                        {
                            var t = Instantiate(diagonalWallPrefab,
                                world + CornerOffset(east: false, north: true, cell) + baseY,
                                Yaw315, root);
                            t.transform.localScale = new Vector3(cell.x * 0.1f, wallH, diagLen);
                            //t.name = $"Wall({room_name})";
                            triangle_floor_dir = 1;
                            if (skipOrthogonalWhenDiagonal) { suppressN = true; suppressW = true; }
                        }
                        // SE corner (S & E)
                        if (S && E /* && SE */)
                        {
                            var t = Instantiate(diagonalWallPrefab,
                                world + CornerOffset(east: true, north: false, cell) + baseY,
                                Yaw135, root);
                            t.transform.localScale = new Vector3(cell.x * 0.1f, wallH, diagLen);
                            //t.name = $"Wall({room_name})";
                            triangle_floor_dir = 3;
                            if (skipOrthogonalWhenDiagonal) { suppressS = true; suppressE = true; }
                        }
                        // SW corner (S & W)
                        if (S && W /* && SW */)
                        {
                            var t = Instantiate(diagonalWallPrefab,
                                world + CornerOffset(east: false, north: false, cell) + baseY,
                                Yaw225, root);
                            t.transform.localScale = new Vector3(cell.x * 0.1f, wallH, diagLen);
                            //t.name = $"Wall({room_name})";
                            triangle_floor_dir = 2;
                            if (skipOrthogonalWhenDiagonal) { suppressS = true; suppressW = true; }
                        }
                    }
                }
                // -------- end diagonal corner smoothing --------

                // Place floor at its height (Y is up)
                    if (isFloor && floorPrefab != null && triangleFloorPrefab != null)
                    {
                    //var f = Instantiate(floorPrefab, world + new Vector3(0, ySteps * unitHeight, 0), Quaternion.identity, root);
                    //f.name = $"Floor({room_name})";
                    //f.transform.localScale = new Vector3(cell.x, 1f, cell.y); // thickness 1; adjust as needed
                    //                                                          // set floor color based on room's colorFloor, which we've grabbed earlier in the loop.
                    //var renderer = f.GetComponent<MeshRenderer>();
                    //if (renderer != null)
                    //    renderer.material.color = colorFloor;

                    // When you instantiate, skip naming in bulk builds:
                    GameObject f;
                    if (use_triangle_floor)
                    {
                        f = Instantiate(triangleFloorPrefab, world + new Vector3(-0.0f, ySteps * unitHeight, 0.0f), Quaternion.Euler(90f, 0f, triangle_floor_dir * 90), root);
                        f.name = $"Triangle({room_name},{ySteps},{triangle_floor_dir})"; // comment out in perf builds
                    }
                    else
                    {
                        f = Instantiate(floorPrefab, world + new Vector3(0, ySteps * unitHeight, 0), Quaternion.identity, root);
                        f.name = $"Floor({room_name},{ySteps})"; // comment out in perf builds
                    }
                                                                // Cache renderer on prefab variant or:
                    var rend = f.GetComponent<MeshRenderer>(); // ok once per object, but avoid if not needed
                    if (rend != null) rend.material.color = colorFloor;
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

                            //int floorSteps = GetHeightFromRoom(pos);
                            int floorSteps = rooms[room_number].GetHeightInRoom(pos);

                            float ht = Mathf.Max(1, perimeterWallSteps) * unitHeight;
                            float baseY = floorSteps * unitHeight;

                            var face = Instantiate(cliffPrefab,
                                mid + new Vector3(0, baseY + 0.5f * ht, 0),
                                RotFromDir(new Vector2Int(nx - x, nz - z)),
                                root);
                            //face.name = $"Wall({room_name})";
                            face.transform.localScale = new Vector3(cell.x, ht, cell.y * 0.1f);
                        }
                    }

                    // Only consider transitions between walkable tiles, or visualize room->void edges as cliffs if you prefer
                    if (!(isFloor && nIsFloor)) continue;

                    //int nySteps = GetHeightFromRoom(new Vector2Int(nx, nz));
                    int nySteps = rooms[room_number].GetHeightInRoom(new Vector2Int(nx, nz));
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
                        //ramp.name = "Ramp";
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
                        //face.name = $"Cliff({room_name})";
                        // Scale so its Y matches the height difference; X/Z to cell dimensions
                        face.transform.localScale = new Vector3(cell.x, heightWorld, cell.y * 0.1f); // thin face; adjust thickness
                    }
                }
            }
        }
        finally { if (local_tm) tm.End(); }
    }

    public void BuildRoomHeightsLookup(int room_number)
    {
        // Build once at the top of Build3DFromOneRoom:
        idx = new Dictionary<Vector2Int, int>(rooms[room_number].tiles.Count);
        for (int i = 0; i < rooms[room_number].tiles.Count; i++)
            idx[rooms[room_number].tiles[i]] = rooms[room_number].heights[i];
    }

    public int GetHeightFromRoom(Vector2Int pos)
        => idx.TryGetValue(pos, out var v) ? v : 999;

    int GetHeightFromRoom_slow(int room_number, Vector2Int pos)
    {
        for (int i = 0; i < rooms[room_number].tiles.Count; i++)
        {
            if (rooms[room_number].tiles[i] == pos)
                return rooms[room_number].heights[i];
        }
        return 999;
    }

//    byte GetTileFromRoom(int room_number, Vector2Int pos)
//    {
//        if (rooms[room_number].wall_hash_room.Contains(pos))
//            return WALL;
//        if (rooms[room_number].floor_hash_room.Contains(pos))
//            return FLOOR;
//        return UNKNOWN;
//    }

    bool IsTileFromRoom(int room_number, Vector2Int pos, byte tile_type)
    {
        if (tile_type == FLOOR)
            if (rooms[room_number].IsTileInRoom(pos))
                return true;
        if (tile_type == WALL)
            if (rooms[room_number].IsWallInRoom(pos))
                return true;
        return false;
    }
    
} // End class HeightMap3DBuilder