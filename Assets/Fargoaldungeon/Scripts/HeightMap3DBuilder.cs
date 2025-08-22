using UnityEngine;
using System.Collections.Generic;
using System.CodeDom.Compiler;

public class HeightMap3DBuilder : MonoBehaviour
{
    public DungeonGenerator generator;
    public Grid grid;                         // same Grid as your Tilemap
    public float unitHeight = 1f;             // world Y per step
    public GameObject floorPrefab;
    public GameObject rampPrefab;             // oriented to face +Z by default (configurable below)
    public GameObject cliffPrefab;            // a 1x1x1 pillar you can scale in Y
    public Transform root;                    // parent for spawned meshes
    public bool onlyPerimeterWalls = true;    // skip deep interior walls

    public GameObject diagonalWallPrefab; // thin strip or quad oriented along +Z
    public bool useDiagonalCorners = true;
    public bool skipOrthogonalWhenDiagonal = true;
    public int perimeterWallSteps = 3; // height of perimeter faces in steps

    [HideInInspector] public byte WALL = 1;
    [HideInInspector] public byte FLOOR = 2;
    //[HideInInspector] public byte RAMP = 3;

    // If your ramp mesh "forward" is +Z, map directions to rotations:
    static readonly Vector2Int[] Dir4 = { new(0, 1), new(1, 0), new(0, -1), new(-1, 0) };
    static Quaternion RotFromDir(Vector2Int d)
    {
        if (d == new Vector2Int(0,1))  return Quaternion.Euler(0,  0, 0);   // face +Z
        if (d == new Vector2Int(1,0))  return Quaternion.Euler(0, 90, 0);
        if (d == new Vector2Int(0,-1)) return Quaternion.Euler(0,180, 0);
        return Quaternion.Euler(0,270, 0); // (-1,0)
    }

    // 45° yaw helpers
    static readonly Quaternion Yaw45  = Quaternion.Euler(0,  -45, 0);
    static readonly Quaternion Yaw135 = Quaternion.Euler(0, -135, 0);
    static readonly Quaternion Yaw225 = Quaternion.Euler(0, -225, 0);
    static readonly Quaternion Yaw315 = Quaternion.Euler(0, -315, 0);

    // experiments with terrain height...
    void randomFloorHeights()
    {
        for (int x = 0; x < generator.map.GetLength(0); x++)
        {
            for (int z = 0; z < generator.map.GetLength(1); z++)
            {
                if (generator.map[x, z] == FLOOR) generator.mapHeights[x, z] = UnityEngine.Random.Range(0, 3); ; // raise floors
            }
        }
    }

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

    public void Destroy3D()
    {
        if (root == null) return;
        for (int i = root.childCount - 1; i >= 0; i--)
            Destroy(root.GetChild(i).gameObject);

    }
    
    public void Build(byte[,] map, int[,] heights)
    {
        Vector3 mid = new();
        Vector3 world = new();
        Vector3 nWorld = new();

        if (root == null) root = new GameObject("Terrain3D").transform;
        // Clear old
        Destroy3D();
        //for (int i = root.childCount - 1; i >= 0; i--) Destroy(root.GetChild(i).gameObject);

        int w = map.GetLength(0), hi = map.GetLength(1);
        Vector3 cell = grid.cellSize;

        for (int x = 0; x < w; x++)
            for (int z = 0; z < hi; z++)
            {
                bool isFloor = map[x, z] == FLOOR;
                bool isWall = map[x, z] == WALL;
                int ySteps = heights[x, z];

                // Optionally skip walls that are not adjacent to floor (visual cleanliness/perf)
                if (!isFloor && onlyPerimeterWalls && !HasFloorNeighbor(map, x, z)) continue;

                // Base world position of this tile center
                world = grid.CellToWorld(new Vector3Int(x, z, 0));

                // If your Grid's tile anchor isn't centered, you may want to offset by cell * 0.5f
                // world += new Vector3(cell.x * 0.5f, 0, cell.y * 0.5f); // uncomment if needed

                // -------- diagonal corner smoothing (before orthogonal perimeter faces) --------
                bool suppressN = false, suppressE = false, suppressS = false, suppressW = false;

                if (useDiagonalCorners && isFloor && diagonalWallPrefab != null)
                {
                    bool N = (z + 1 < hi) && map[x, z + 1] == WALL;
                    bool S = (z - 1 >= 0) && map[x, z - 1] == WALL;
                    bool E = (x + 1 < w) && map[x + 1, z] == WALL;
                    bool W = (x - 1 >= 0) && map[x - 1, z] == WALL;

                    // Optional: require the true corner tile to also be wall (uncomment if desired)
                    // bool NE = (x+1 < w && z+1 < hi) && map[x+1, z+1] == WALL;
                    // bool NW = (x-1 >= 0 && z+1 < hi) && map[x-1, z+1] == WALL;
                    // bool SE = (x+1 < w && z-1 >= 0) && map[x+1, z-1] == WALL;
                    // bool SW = (x-1 >= 0 && z-1 >= 0) && map[x-1, z-1] == WALL;

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
                // -------- end diagonal corner smoothing --------

                // Place floor at its height (Y is up)
                if (isFloor && floorPrefab != null)
                {
                    var f = Instantiate(floorPrefab, world + new Vector3(0, ySteps * unitHeight, 0), Quaternion.identity, root);
                    f.transform.localScale = new Vector3(cell.x, 1f, cell.y); // thickness 1; adjust as needed
                }

                // Compare with 4 neighbors and add ramps/cliffs
                //for (int i = 0; i < 4; i++)
                for (int i = 0; i < 4; i++)
                {
                    Vector2Int d = Dir4[i];
                    int nx = x + d.x, nz = z + d.y;
                    if (nx < 0 || nz < 0 || nx >= w || nz >= hi) continue;

                    bool nIsFloor = map[nx, nz] == FLOOR;
                    bool nIsWall = map[nx, nz] == WALL;

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

                            int floorSteps = heights[x, z];
                            float ht = Mathf.Max(1, perimeterWallSteps) * unitHeight;
                            float baseY = floorSteps * unitHeight;

                            var face = Instantiate(cliffPrefab,
                                mid + new Vector3(0, baseY + 0.5f * ht, 0),
                                RotFromDir(new Vector2Int(nx - x, nz - z)),
                                root);
                            //                            var face = Instantiate(cliffPrefab,
                            //                                mid + new Vector3(0, baseY + 0.5f * ht, 0),
                            //                                RotFromDir(new Vector2Int(nx - x, nz - z)),
                            //                                root);

                            face.transform.localScale = new Vector3(cell.x, ht, cell.y * 0.1f);
                        }
                    }

                    // Only consider transitions between walkable tiles, or visualize room->void edges as cliffs if you prefer

                    if (!(isFloor && nIsFloor)) continue;

                    int nySteps = heights[nx, nz];
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
    }

    bool HasFloorNeighbor(byte[,] map, int x, int z)
    {
        int w = map.GetLength(0), h = map.GetLength(1);
        if (z+1 < h && map[x, z+1] == FLOOR) return true;
        if (x+1 < w && map[x+1, z] == FLOOR) return true;
        if (z-1 >= 0 && map[x, z-1] == FLOOR) return true;
        if (x-1 >= 0 && map[x-1, z] == FLOOR) return true;
        return false;
    }
}