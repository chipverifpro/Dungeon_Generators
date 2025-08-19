using UnityEngine;
using System.Collections.Generic;

public class HeightMap3DBuilder : MonoBehaviour
{
    public Grid grid;                         // same Grid as your Tilemap
    public float unitHeight = 1f;             // world Y per step
    public GameObject floorPrefab;
    public GameObject rampPrefab;             // oriented to face +Z by default (configurable below)
    public GameObject cliffPrefab;            // a 1x1x1 pillar you can scale in Y
    public Transform root;                    // parent for spawned meshes
    public bool onlyPerimeterWalls = true;    // skip deep interior walls

    // If your ramp mesh "forward" is +Z, map directions to rotations:
    static readonly Vector2Int[] Dir4 = { new(0,1), new(1,0), new(0,-1), new(-1,0) };
    static Quaternion RotFromDir(Vector2Int d)
    {
        if (d == new Vector2Int(0,1))  return Quaternion.Euler(0,  0, 0);   // face +Z
        if (d == new Vector2Int(1,0))  return Quaternion.Euler(0, 90, 0);
        if (d == new Vector2Int(0,-1)) return Quaternion.Euler(0,180, 0);
        return Quaternion.Euler(0,270, 0); // (-1,0)
    }

    public void Build(byte[,] map, int[,] heights)
    {
        if (root == null) root = new GameObject("Terrain3D").transform;
        // Clear old
        for (int i = root.childCount - 1; i >= 0; i--) Destroy(root.GetChild(i).gameObject);

        int w = map.GetLength(0), h = map.GetLength(1);
        Vector3 cell = grid.cellSize;

        for (int x = 0; x < w; x++)
        for (int z = 0; z < h; z++)
        {
            bool isFloor = map[x, z] == 0; // FLOOR == 0 in your project
            int ySteps = heights[x, z];

            // Optionally skip walls that are not adjacent to floor (visual cleanliness/perf)
            if (!isFloor && onlyPerimeterWalls && !HasFloorNeighbor(map, x, z)) continue;

            // Base world position of this tile center
            Vector3 world = grid.CellToWorld(new Vector3Int(x, z, 0));
            // If your Grid's tile anchor isn't centered, you may want to offset by cell * 0.5f
            // world += new Vector3(cell.x * 0.5f, 0, cell.y * 0.5f); // uncomment if needed

            // Place floor at its height (Y is up)
            if (isFloor && floorPrefab != null)
            {
                var f = Instantiate(floorPrefab, world + new Vector3(0, ySteps * unitHeight, 0), Quaternion.identity, root);
                f.transform.localScale = new Vector3(cell.x, 1f, cell.y); // thickness 1; adjust as needed
            }

            // Compare with 4 neighbors and add ramps/cliffs
            for (int i = 0; i < 4; i++)
            {
                Vector2Int d = Dir4[i];
                int nx = x + d.x, nz = z + d.y;
                if (nx < 0 || nz < 0 || nx >= w || nz >= h) continue;

                // Only consider transitions between walkable tiles, or visualize room->void edges as cliffs if you prefer
                bool nIsFloor = map[nx, nz] == 0;
                if (!(isFloor && nIsFloor)) continue;

                int nySteps = heights[nx, nz];
                int diff = nySteps - ySteps;
                if (diff == 0) continue;

                // Place transition geometry centered between the two tiles
                Vector3 mid = (world + grid.CellToWorld(new Vector3Int(nx, nz, 0))) * 0.5f;

                if (Mathf.Abs(diff) == 1 && rampPrefab != null)
                {
                    // Ramp spans from lower to higher tile
                    bool up = diff > 0;
                    // Place ramp slightly biased toward lower side so the top aligns cleanly
                    int lower = up ? ySteps : nySteps;
                    var rot = RotFromDir(d * (up ? 1 : -1)); // face uphill
                    var ramp = Instantiate(rampPrefab, mid + new Vector3(0, (lower + 0.5f) * unitHeight, 0), rot, root);
                    ramp.transform.localScale = new Vector3(cell.x, unitHeight, cell.y); // length matches cell, height equals one step
                }
                else if (Mathf.Abs(diff) >= 2 && cliffPrefab != null)
                {
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
        if (z+1 < h && map[x, z+1] == 0) return true;
        if (x+1 < w && map[x+1, z] == 0) return true;
        if (z-1 >= 0 && map[x, z-1] == 0) return true;
        if (x-1 >= 0 && map[x-1, z] == 0) return true;
        return false;
    }
}