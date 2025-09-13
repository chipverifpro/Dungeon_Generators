using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.Tilemaps;
using Random = System.Random;

public partial class DungeonGenerator : MonoBehaviour
{
    //[Header("Config")]
    //public DungeonSettings cfg;     // ← your ScriptableObject, named as you like

    // Minimal PackMap structs (adapt to your real ones)
    public class PackCell { public int x, y, height; public bool isCorridor; public int roomId = -1; }
    public class PackRoom { public int id; public List<PackCell> cells = new(); public RectInt bounds; }
    public class PackMap
    {
        public int w, h;
        public PackCell[,] g;
        public List<PackRoom> rooms = new();
        public HashSet<(int, int)> corridors = new();
        public PackMap(int w, int h) { this.w = w; this.h = h; g = new PackCell[w, h]; for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) g[x, y] = new PackCell { x = x, y = y }; }
        public bool In(int x, int y) => (uint)x < (uint)w && (uint)y < (uint)h;
    }

    // Runtime
    public PackMap packMap;
    //Random rng;
    //Action<string> logger;


    public IEnumerator GeneratePackedRooms(int? seedOverride = null)
    {
        // Setup
        int seed = cfg.randomizeSeed ? UnityEngine.Random.Range(int.MinValue, int.MaxValue) : (seedOverride ?? cfg.seed);
        rng = new Random(seed);
        //BottomBanner.Show = cfg.showBuildProcess ? (Action<string>)BottomBanner.Show : (_)=>{};
        packMap = new PackMap(cfg.mapWidth, cfg.mapHeight);
        List<Room> rooms_temp = new(); // temporary Room list for compatibility with DrawMapByRooms
        float t0 = Time.realtimeSinceStartup;

        // 1) Corridors
        yield return StartCoroutine(RunCorridors());
        DrawMapByRooms(rooms);
        yield return new WaitForSeconds(1f);

        // 2) Room seeding
        yield return StartCoroutine(RunRoomSeeding());
        DrawMapByRooms(rooms);
        Debug.Log("After room seeding, rooms = " + rooms.Count);
        yield return new WaitForSeconds(1f);

        // 3) Room growth
        yield return StartCoroutine(RunRoomGrowth());
        DrawMapByRooms(rooms);
        Debug.Log("After room growth, rooms = " + rooms.Count);
        yield return new WaitForSeconds(1f);

        // 4) Scraps
        yield return StartCoroutine(RunScraps());
        DrawMapByRooms(rooms);
        Debug.Log("After scraps, rooms = " + rooms.Count);
        yield return new WaitForSeconds(1f);

        // 5) Doors/connectivity
        yield return StartCoroutine(RunDoors());
        DrawMapByRooms(rooms);
        Debug.Log("After doors, rooms = " + rooms.Count);
        yield return new WaitForSeconds(1f);

        BottomBanner.Show($"Done seed={seed} in {(Time.realtimeSinceStartup - t0):F2}s");
    }

    // ---------- Stage switches ----------
    IEnumerator RunCorridors()
    {
        switch (cfg.corridorAlgo)
        {
            case DungeonSettings.CorridorAlgo.WanderingMST: return Corridors_WanderingMST();
            case DungeonSettings.CorridorAlgo.MedialAxis: return Corridors_MedialAxis();
            case DungeonSettings.CorridorAlgo.GridMazes: return Corridors_GridMazes();
            case DungeonSettings.CorridorAlgo.DrunkardsWalk:
                return Corridors_DrunkardsWalk(
                walkers: cfg.corridor.drunkWalkers,
                stepsPerWalker: cfg.corridor.drunkStepsPerWalker,
                minimumStraight: cfg.corridor.drunkMinimumStraight,
                wander: cfg.corridor.wanderiness,
                corridorWidth: cfg.corridor.corridorWidth
            );
            default: return Corridors_WanderingMST();
        }
    }

    IEnumerator RunRoomSeeding()
    {
        switch (cfg.roomSeedAlgo)
        {
            case DungeonSettings.RoomSeedAlgo.AlongCorridors: return Seed_AlongCorridors();
            case DungeonSettings.RoomSeedAlgo.PoissonAlongCorridors: return Seed_PoissonAlongCorridors();
            case DungeonSettings.RoomSeedAlgo.UniformGrid: return Seed_UniformGrid();
            default: return Seed_AlongCorridors();
        }
    }
    IEnumerator RunRoomGrowth()
    {
        switch (cfg.roomGrowAlgo)
        {
            case DungeonSettings.RoomGrowAlgo.CreditWavefront: return Grow_CreditWavefront();
            //case DungeonSettings.RoomGrowAlgo.StripThenWavefront: return Grow_StripThenWavefront();
            case DungeonSettings.RoomGrowAlgo.PressureField: return Grow_PressureField();
            case DungeonSettings.RoomGrowAlgo.OrthogonalRays: return Grow_OrthogonalRays();
            default: return Grow_CreditWavefront();
        }
    }
    IEnumerator RunScraps()
    {
        switch (cfg.scrapAlgo)
        {
            case DungeonSettings.ScrapAlgo.VoronoiFill: return Scraps_VoronoiFill();
            case DungeonSettings.ScrapAlgo.ClosetsOnly: return Scraps_ClosetsOnly();
            case DungeonSettings.ScrapAlgo.NearestRoom: return Scraps_NearestRoom();
            default: return Scraps_VoronoiFill();
        }
    }
    IEnumerator RunDoors()
    {
        switch (cfg.doorAlgo)
        {
            case DungeonSettings.DoorAlgo.EnsureConnectivity: return Doors_EnsureConnectivity();
            case DungeonSettings.DoorAlgo.SparseLoops: return Doors_SparseLoops();
            case DungeonSettings.DoorAlgo.ManyLoops: return Doors_ManyLoops();
            default: return Doors_EnsureConnectivity();
        }
    }

    // ---------- Stage implementations (skeletons to fill) ----------

    //  IEnumerator Corridors_DrunkardsWalk()
    //  {
    //      BottomBanner.Show("Corridors: DrunkardsWalk");
    //      // 1) lay 'cfg.corridor.spineCount' biased random walks with width 'cfg.corridor.corridorWidth'
    //      // 2) optionally connect keypoints with loops
    //      // 3) write into PackMap.corridors and lock a 1-cell moat if you keep thin walls
    //      yield return null;
    //     }

    //    IEnumerator Corridors_WanderingMST()
    //    {
    //        BottomBanner.Show("Corridors: WanderingMST");
    //        // 1) lay 'cfg.corridor.spineCount' biased random walks with width 'cfg.corridor.corridorWidth'
    //        // 2) connect keypoints with MST + add loops with probability cfg.corridor.loopChance
    //        // 3) write into PackMap.corridors and lock a 1-cell moat if you keep thin walls
    //        yield return null;
    //    }
    IEnumerator Corridors_MedialAxis()
    {
        BottomBanner.Show("Corridors: MedialAxis");
        // derive corridors from skeleton of blocked mask; prune branches; width locked
        yield return null;
    }
    IEnumerator Corridors_GridMazes()
    {
        BottomBanner.Show("Corridors: GridMazes");
        // uniform or weighted recursive backtracker / Wilson; keep width = cfg.corridor.corridorWidth
        yield return null;
    }

    //    IEnumerator Seed_AlongCorridors()
    //    {
    //        BottomBanner.Show("Seeding: AlongCorridors");
    //        // place seeds along corridor sides every cfg.seed.spacing with jitter cfg.seed.jitter
    //        // alternate left/right by cfg.seed.alternateSides
    //        yield return null;
    //    }
    IEnumerator Seed_PoissonAlongCorridors()
    {
        BottomBanner.Show("Seeding: PoissonAlongCorridors");
        // run 1-D Poisson sampling along paths, project seeds to sides
        yield return null;
    }
    IEnumerator Seed_UniformGrid()
    {
        BottomBanner.Show("Seeding: UniformGrid");
        // grid cells at spacing; skip if too near corridors
        yield return null;
    }

    //    IEnumerator Grow_CreditWavefront()
    //    {
    //        BottomBanner.Show("Growth: CreditWavefront");
    //        // each room gets random credit in [cfg.grow.areaCreditMin..Max]
    //        // round-robin claimable frontier respecting moat = cfg.grow.wallMoat
    //        // split if area>cfg.grow.splitArea or aspect>cfg.grow.splitAspect
    //        yield return null;
    //    }
    IEnumerator Grow_PressureField()
    {
        BottomBanner.Show("Growth: PressureField");
        // maintain a pressure scalar; rooms expand into lowest-pressure valid neighbor
        yield return null;
    }
    IEnumerator Grow_OrthogonalRays()
    {
        BottomBanner.Show("Growth: OrthogonalRays");
        // extend axis-aligned slabs until 1-cell before collision; merge slabs
        yield return null;
    }

    IEnumerator Scraps_VoronoiFill()
    {
        BottomBanner.Show("Scraps: VoronoiFill");
        // assign leftovers to nearest room with 1-cell peel for walls; tiny islands -> closets
        yield return null;
    }
    IEnumerator Scraps_ClosetsOnly()
    {
        BottomBanner.Show("Scraps: ClosetsOnly");
        // mark small unassigned blobs (<= cfg.scraps.closetMaxArea) as closets; leave others as wall
        yield return null;
    }
    IEnumerator Scraps_NearestRoom()
    {
        BottomBanner.Show("Scraps: NearestRoom");
        // simply flood to nearest room but preserve 1-cell wall between different owners
        yield return null;
    }

    IEnumerator Doors_EnsureConnectivity()
    {
        BottomBanner.Show("Doors: EnsureConnectivity");
        // ensure every room hits a corridor; add minimal doors to connect all components
        yield return null;
    }
    IEnumerator Doors_SparseLoops()
    {
        BottomBanner.Show("Doors: SparseLoops");
        // ensure connectivity + add few room-room doors with far-bias cfg.doors.loopBias
        yield return null;
    }
    IEnumerator Doors_ManyLoops()
    {
        BottomBanner.Show("Doors: ManyLoops");
        // like SparseLoops but add up to cfg.doors.maxRoomToRoomDoors extra room-room doors
        yield return null;
    }

    // ======================== Corridors: Drunkard's Walk (revised) ========================

    IEnumerator Corridors_DrunkardsWalk(
        int walkers = 2,
        int stepsPerWalker = 400,
        int minimumStraight = 10,
        float wander = 30f,              // determines chance to turn 90° each step
        int corridorWidth = 1,           // 1..2 to keep it skinny
        bool bounceAtEdges = true,       // if false, pick new random start when we hit an edge
        int yieldEvery = 256             // cooperative yield cadence
    )
    {
        BottomBanner.Show("Corridors: Drunkard's Walk");
        int W = cfg.mapWidth, H = cfg.mapHeight;
        //corridorWidth = Mathf.Clamp(corridorWidth <= 0 ? cfg.corridor.corridorWidth : corridorWidth, 1, 5);

        List<PackCell> corridorCells = new(); // to pass to DrawMapByRooms
        Room tmp_room;
        Cell tmp_real_cell;

        // Simple RNG fallback: use your 'rng' if you have it; else UnityEngine.Random
        System.Func<float> R01 = () => (rng != null) ? (float)rng.NextDouble() : UnityEngine.Random.value;
        System.Func<int, int, int> RInt = (a, b) => (rng != null) ? rng.Next(a, b) : UnityEngine.Random.Range(a, b);

        int carved = 0;

        for (int wlk = 0; wlk < walkers; wlk++)
        {
            tmp_room = new();
            // Start near center (stable) or random edge if you prefer
            // Vector2Int p = new Vector2Int(W / 2, H / 2);
            Vector2Int p = RandomEdgeStart(W, H); // alternative start

            //Vector2Int dir = RandomCardinal();
            Vector2Int dir = DirAwayFromEdge(p);
            int straightRounds = (int)((R01()+1) * minimumStraight);
            for (int step = 0; step < stepsPerWalker; step++)
            {
                // Carve corridor at p
                PackCell cell_tmp;
                cell_tmp = new PackCell { x = p.x, y = p.y };
                tmp_real_cell = new Cell ( p );
                //tmp_room.cells.Add(tmp_real_cell); // This Add is done in CarveDisk
                packMap.corridors.Add((p.x, p.y)); // needed for next stage
                packMap.g[p.x, p.y].isCorridor = true;
                CarveDisk(tmp_room, p, corridorWidth); // paint corridor cell(s)
                carved++;

                // Maybe turn 90°
                // Wander the direction a bit, but verify we went a minimum distance straight
                straightRounds--;
                if (straightRounds <= 0)
                {
                    Vector2Int predir = dir;
                    if (R01() < wander / 1000f) // odds of turning
                        dir = (R01() < 0.5f) ? TurnLeft(dir, true) : TurnLeft(dir, false);
                    if (predir != dir) straightRounds = (int)((R01()+1) * minimumStraight);
                }

                // Advance
                Vector2Int np = p + dir;

                if (!In(np.x, np.y))   // must turn or teleport
                {
                    straightRounds = (int)((R01()+1f) * minimumStraight); // between min and 2*min
                    if (bounceAtEdges)
                    {
                        // bounce: straight back
                        dir = DirAwayFromEdge(p);
                        np = p + dir;

                        if (!In(np.x, np.y))
                        {
                            // fully stuck: pick a fresh random in-bounds location
                            np = new Vector2Int(RInt(0, W), RInt(0, H));
                            dir = DirAwayFromEdge(np);
                        }
                    }
                    else
                    {
                        // restart from new random position
                        np = new Vector2Int(RInt(0, W), RInt(0, H));
                        dir = DirAwayFromEdge(np);
                    }
                }

                p = np;

                // Periodic yield to keep Editor responsive
                if ((carved % yieldEvery) == 0) yield return null;
            }
            //var tmp_room = new Room { cells = new List<Cell>(corridorCells), isCorridor = true };
            tmp_room.my_room_number = rooms.Count; // a unique room number
            tmp_room.setColorFloor(highlight: false);
            tmp_room.isCorridor = true;
            foreach (var cell in tmp_room.cells)
            {
                cell.room_number = tmp_room.my_room_number;
                cell.colorFloor = tmp_room.colorFloor;
            }

            rooms.Add(tmp_room);
            Debug.Log("Drawing rooms = " + rooms.Count);
            DrawMapByRooms(rooms, clearscreen: true);
            yield return null; // new WaitForSeconds(1f);

             
            //corridorCells.Clear();
        }

        BottomBanner.Show($"Corridors: Drunkard's Walk done. Carved ~{carved} cells.");

    }

    // ======================= Corridors: WanderingMST =======================
    IEnumerator Corridors_WanderingMST()
    {
        BottomBanner.Show("Corridors: WanderingMST");
        int W = cfg.mapWidth, H = cfg.mapHeight;

        // clamp params to reasonable ranges
        int width = Mathf.Clamp(cfg.corridor.corridorWidth, 1, 2);
        int spines = Mathf.Max(1, cfg.corridor.spineCount);
        float wander = Mathf.Clamp(cfg.corridor.wanderiness, 0f, 100f);
        float loopChance = Mathf.Clamp01(cfg.corridor.loopChance);

        // 1) Make wandering spines starting near PackMap edges
        var rngf = new System.Func<float>(() => (float)rng.NextDouble());
        var nodes = new List<Vector2Int>();  // sampled waypoints along spines

        List<Room> rooms_temp = new(); // temporary Room list for compatibility with DrawMapByRooms
        Room room_temp;

        Debug.Log("Corridors WanderingMST: Beginning Drawing rooms = " + rooms.Count);
        DrawMapByRooms(rooms, clearscreen: true);
        yield return null;  // new WaitForSeconds(1f);

        var tmp_room = new Room { cells = new List<Cell>(), isCorridor = true };
        tmp_room.setColorFloor(highlight: false);

        for (int s = 0; s < spines; s++)
        {
            Debug.Log($"Corridor spine {s + 1} of {spines}");
            yield return null;
            int min_straightRounds = 20;
            int straightRounds = 0;

            //var tmp_room = new Room { cells = new List<Cell>(), isCorridor = true };
            //tmp_room.setColorFloor(highlight: false);

            // Start near a random border
            Vector2Int p = RandomEdgeStart(W, H);
            Vector2Int dir = DirAwayFromEdge(p);

            int steps = (int)(0.7f * (W + H)); // long-ish 0.7
            //steps = 50;
            int sampleEvery = 12; //12
            int sinceSample = 0;

            for (int i = 0; i < steps; i++)
            {
                Debug.Log($"  step {i + 1} of {steps} at {p.x},{p.y} dir={dir.x},{dir.y}");
                yield return null;
                //if (i == 250) break; // DEBUG CHECK to prevent infinite hang
                straightRounds++;

                CarveDisk(tmp_room, p, width); // paint corridor cell(s)
                sinceSample++;

                // Randomly sample nodes along the walk (used by MST)
                if (sinceSample >= sampleEvery)
                {
                    nodes.Add(p);
                    sinceSample = 0;
                }

                // Wander the direction a bit, but verify we went a minimum distance straight
                if (straightRounds >= min_straightRounds)
                {
                    Vector2Int predir = dir;
                    if (rngf() < wander / 1000) dir = MaybeTurn(dir, rng, wander);
                    if (predir != dir) straightRounds = 0;
                }

                // Step forward; clamp to PackMap
                Vector2Int np = p + dir;
                if (!In(np.x, np.y))
                {
                    // bounce off wall by turning left or right
                    dir = TurnLeft(dir, rngf() < 0.5f);
                    np = p + dir;
                    if (!In(np.x, np.y)) break;
                }
                p = np;

                // Cooperative yield
                if ((i & 127) == 0) yield return null;
            }

            /*
                        foreach (var loc in nodes)
                        {
                            var cell = new Cell(loc.x, loc.y);
                            cell.colorFloor = tmp_room.colorFloor;
                            tmp_room.cells.Add(cell);
                        }
                        rooms.Add(tmp_room);    // Add to master room list
            */

            yield return null; // new WaitForSeconds(1f);
        }

        room_temp = ExtractRoomFromVectors(nodes);
        Debug.Log("nodes = " + nodes.Count + " after steps, before thinned ");
        room_temp.setColorFloor(highlight: false);
        foreach (var cell in room_temp.cells) { cell.colorFloor = room_temp.colorFloor; }
        rooms_temp.Add(room_temp);
        DrawMapByRooms(rooms_temp);
        yield return null; // new WaitForSeconds(1f);

        // ---- before computing MST: dedupe + thin + cap ----
        if (nodes.Count < 2) yield break;

        // 2a) Deduplicate exact duplicates (cheap)
        var seen = new HashSet<int>();
        var dedup = new List<Vector2Int>(nodes.Count);
        foreach (var p in nodes)
        {
            int key = (p.y << 16) ^ p.x;
            if (seen.Add(key)) dedup.Add(p);
        }

        // 2b) Blue-noise thin the node set (enforce Manhattan spacing)
        int minNodeSpacing = 10;                     // tune: larger = fewer nodes
        int maxNodes = 20; //600                   // safety cap to keep MST cheap
        var thinned = new List<Vector2Int>(Mathf.Min(maxNodes, dedup.Count));
        foreach (var p in dedup)
        {
            bool ok = true;
            // small linear check is fine with cap; if you expect bigger, bucket on a coarse grid
            for (int i = 0; i < thinned.Count; i++)
            {
                if (Mathf.Abs(thinned[i].x - p.x) + Mathf.Abs(thinned[i].y - p.y) < minNodeSpacing) { ok = false; break; }
            }
            if (ok) thinned.Add(p);
            if (thinned.Count >= maxNodes) break;
        }
        nodes = thinned;

        room_temp = ExtractRoomFromVectors(nodes);
        Debug.Log("nodes = " + nodes.Count + " after thinned ");
        rooms_temp.Add(room_temp);
        DrawMapByRooms(rooms_temp);
        yield return null; // new WaitForSeconds(1f);

        // 2c) Build MST in a time-sliced way
        List<(Vector2Int a, Vector2Int b)> mstEdges = new List<(Vector2Int, Vector2Int)>(nodes.Count - 1);
        yield return StartCoroutine(ComputeMST_Yield(nodes, mstEdges, yieldEvery: 2000));  // yields during O(n²)

        Debug.Log($"  MST has {mstEdges.Count} edges connecting {nodes.Count} nodes");

        // 2d) Carve MST edges with yielding (so long lines don’t block)
        foreach (var e in mstEdges)
        {
            tmp_room = new Room { cells = new List<Cell>(), isCorridor = true };
            tmp_room.setColorFloor(highlight: false);

            yield return StartCoroutine(CarveLineWithYield(tmp_room, e.a, e.b, width, yieldEvery: 256));
            rooms.Add(tmp_room);
        }

        // 3) Add a few loop edges, but be gentle
        int extraTarget = Mathf.Min(48, Mathf.CeilToInt(nodes.Count * loopChance * 0.4f)); // hard cap
        int maxLoopLen = Mathf.Max(16, (W + H) / 12); // don’t add megascale chords
        for (int k = 0; k < extraTarget; k++)
        {
            var a = nodes[rng.Next(nodes.Count)];
            var b = nodes[rng.Next(nodes.Count)];
            if (Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y) > maxLoopLen) continue; // skip long chords

            tmp_room = new Room { cells = new List<Cell>(), isCorridor = true };
            tmp_room.setColorFloor(highlight: false);

            yield return StartCoroutine(CarveLineWithYield(tmp_room, a, b, width, yieldEvery: 256));
            rooms.Add(tmp_room);

            if ((k & 3) == 0) yield return null;
        }

        DrawMapByRooms(rooms, clearscreen: true);
        yield return new WaitForSeconds(1f);

        // Helpers...

        IEnumerator ComputeMST_Yield(List<Vector2Int> pts,
        List<(Vector2Int a, Vector2Int b)> outEdges,
        int yieldEvery = 5000)
        {
            int n = pts.Count;
            if (n <= 1) yield break;

            var inTree = new bool[n];
            var best = new int[n];
            var parent = new int[n];

            // Start at 0
            inTree[0] = true;
            for (int j = 1; j < n; j++)
            {
                best[j] = Manhattan(pts[0], pts[j]);
                parent[j] = 0;
            }
            best[0] = int.MaxValue; parent[0] = -1;

            int ops = 0;
            for (int e = 0; e < n - 1; e++)
            {
                // pick the non-tree vertex with smallest best[j]
                int k = -1, bk = int.MaxValue;
                for (int i = 0; i < n; i++)
                {
                    if (!inTree[i] && best[i] < bk) { bk = best[i]; k = i; }
                    if ((++ops % yieldEvery) == 0) yield return null; // time-slice the scan
                }
                if (k == -1) break; // disconnected (shouldn’t happen with full graph)
                inTree[k] = true;
                outEdges.Add((pts[k], pts[parent[k]]));

                // relax edges from k
                for (int j = 0; j < n; j++)
                {
                    if (inTree[j]) continue;
                    int c = Manhattan(pts[k], pts[j]);
                    if (c < best[j]) { best[j] = c; parent[j] = k; }
                    if ((++ops % yieldEvery) == 0) yield return null;
                }
            }
        }

        IEnumerator CarveLineWithYield(Room tmp_room, Vector2Int a, Vector2Int b, int width, int yieldEvery = 256)
        {
            int count = 0;
            foreach (var p in RasterizeLineSafe(a, b))
            {
                CarveDisk(tmp_room, p, width);  // your existing painter
                if ((++count % yieldEvery) == 0) yield return null;
            }
        }
    }

    // ======================= Seeding: AlongCorridors =======================
    IEnumerator Seed_AlongCorridors()
    {
        BottomBanner.Show("Seeding: AlongCorridors");

        int W = cfg.mapWidth, H = cfg.mapHeight;
        //int W = packMap.w, H = packMap.h;
        int moat = Mathf.Max(0, cfg.grow.wallMoat);
        int spacing = Mathf.Max(2, cfg.RoomSeeding.spacing);     // min spacing between seeds along corridors
        int jitter = Mathf.Clamp(cfg.RoomSeeding.jitter, 0, spacing - 1);
        float altProb = Mathf.Clamp01(cfg.RoomSeeding.alternateSides); // probability to alternate sides L/R

        /*
        // make a list of all corridor cell locations:
        foreach (Room r in rooms)
        {
            if (r.isCorridor)
            {
                foreach (Cell c in r.cells)
                {
                    // TODO: use Room and Cells instead of packMap

                    packMap.g[c.x, c.y].isCorridor = true;  // build array for fast lookup
                    if (packMap.corridors == null)
                        packMap.corridors = new HashSet<(int, int)>();
                    if (!packMap.corridors.Contains((c.x, c.y)))
                        packMap.corridors.Add((c.x, c.y));
                }
            }
        }
        */
        if (packMap.corridors == null || packMap.corridors.Count == 0)
        {
            // Convert Rooms to PackMap (FIX THIS: needed for WanderingMST, not for Drunkards Walk)
            // make a list of all corridor cell locations:
            foreach (Room r in rooms)
            {
                if (r.isCorridor)
                {
                    foreach (Cell c in r.cells)
                    {
                        // TODO: use Room and Cells instead of packMap

                        packMap.g[c.x, c.y].isCorridor = true;  // build array for fast lookup
                        if (packMap.corridors == null)
                            packMap.corridors = new HashSet<(int, int)>();
                        if (!packMap.corridors.Contains((c.x, c.y)))
                            packMap.corridors.Add((c.x, c.y));
                    }
                }
            }
            //BottomBanner.Show("  (No corridors found; seeding skipped)");
            //yield break;
        }

        // 1) Collect candidate corridor cells that are "good" for hanging rooms:
        //    Prefer straight or gently curved segments (2 corridor neighbors).
        var corridorList = new List<Vector2Int>(packMap.corridors.Count);
        foreach (var (x, y) in packMap.corridors) corridorList.Add(new Vector2Int(x, y));

        // Shuffle to avoid directional bias (blue-noise style selection later)
        Shuffle(corridorList);

        // 2) Blue-noise pick: accept a candidate if it's ≥ spacing away (Manhattan) from other chosen anchors
        var anchors = new List<Vector2Int>();
        foreach (var p in corridorList)
        {
            // skip junctions with 3+ corridor neighbors (doors are better placed by the door pass)
            int nbCorr = CountCorridorNeighbors(p.x, p.y);
            if (nbCorr == 0) continue; // corrupted mark?
            if (nbCorr >= 3) continue; // big junctions: skip as anchors

            bool farEnough = true;
            for (int i = 0; i < anchors.Count; i++)
            {
                if (Manhattan(p, anchors[i]) < spacing)
                {
                    farEnough = false;
                    break;
                }
            }
            if (!farEnough) continue;

            // Jitter forward along local corridor tangent to avoid a grid feel
            Vector2Int tangent = PickTangentDir(p.x, p.y);
            if (tangent != Vector2Int.zero && jitter > 0)
            {
                int j = rng.Next(-jitter, jitter + 1);
                var pj = p + tangent * j;
                if ((uint)pj.x < (uint)W && (uint)pj.y < (uint)H && packMap.g[pj.x, pj.y].isCorridor)
                    anchors.Add(pj);
                else
                    anchors.Add(p);
            }
            else
            {
                anchors.Add(p);
            }

            // cooperative yield
            if ((anchors.Count & 127) == 0) yield return null;
        }

        if (anchors.Count == 0)
        {
            BottomBanner.Show("  (No valid corridor anchors; seeding skipped)");
            yield break;
        }

        // 3) For each anchor, choose a side (left/right normal to corridor),
        //    offset off the corridor by moat+1, and plant a single seed cell there.
        bool flip = false; // alternate sides deterministically, with randomness via altProb
        int created = 0;

        foreach (var a in anchors)
        {
            Vector2Int t = PickTangentDir(a.x, a.y);
            if (t == Vector2Int.zero) t = RandomCardinal(); // fallback

            // choose side: alternate with probability, else random
            if (rng.NextDouble() < altProb) flip = !flip;
            Vector2Int n = Perp(t, flip); // left/right normal

            // Try placing the seed at increasing offsets starting from moat+1
            bool placed = false;
            for (int step = moat + 1; step <= moat + cfg.corridor.corridorWidth + 2; step++)
            {
                int sx = a.x + n.x * step;
                int sy = a.y + n.y * step;
                if (!In(sx, sy)) break;

                if (CanPlaceSeed(sx, sy, moat))
                {
                    CreateRoomSeedAt(sx, sy);
                    placed = true;
                    created++;
                    break;
                }
            }

            // If the chosen side fails (wall/edge), try the opposite side once
            if (!placed)
            {
                Vector2Int n2 = new Vector2Int(-n.x, -n.y);
                for (int step = moat + 1; step <= moat + cfg.corridor.corridorWidth + 3; step++)
                {
                    int sx = a.x + n2.x * step;
                    int sy = a.y + n2.y * step;
                    if (!In(sx, sy)) break;

                    if (CanPlaceSeed(sx, sy, moat))
                    {
                        CreateRoomSeedAt(sx, sy);
                        placed = true;
                        created++;
                        break;
                    }
                }
            }

            if ((created & 63) == 0) yield return null;
        }


        BottomBanner.Show($"  Seeded {created} room(s) from {anchors.Count} corridor anchors.");

        DrawMapByRooms(rooms, clearscreen: true);
        yield return null; // new WaitForSeconds(1f);
    }

    // ======================= Growth: CreditWavefront =======================
    IEnumerator Grow_CreditWavefront()
    {
        BottomBanner.Show("Growth: CreditWavefront");
        // PRECONDITIONS:
        // - PackMap.rooms contains Room objects
        // - Each Room has at least one seed Cell in PackMap.g[,] with cell.roomId = room.id
        // - Corridors already painted (cell.isCorridor = true)
        // - We will preserve a 1-cell wall moat (= cfg.grow.wallMoat) around rooms & corridors

        int moat = Mathf.Max(0, cfg.grow.wallMoat);
        int nRooms = packMap.rooms.Count;
        if (nRooms == 0) yield break;

        // credit per room
        var credits = new int[nRooms];
        for (int i = 0; i < nRooms; i++)
            credits[i] = rng.Next(cfg.grow.areaCreditMin, cfg.grow.areaCreditMax + 1);

        // Precompute a room frontier set
        var frontiers = new List<HashSet<(int x, int y)>>(nRooms);
        for (int i = 0; i < nRooms; i++)
            frontiers.Add(new HashSet<(int, int)>());

        // Initialize frontier = perimeter of current room seeds
        for (int ri = 0; ri < nRooms; ri++)
        {
            foreach (var c in packMap.rooms[ri].cells)
                foreach (var nb in FourNeighbors(c.x, c.y))
                    if (CanClaim(ri, nb.x, nb.y, moat))
                        frontiers[ri].Add((nb.x, nb.y));
        }

        int activeRooms = nRooms;
        int safety = packMap.w * packMap.h * 4; // generous
        int round = 0;
        // Build initial AABBs and per-room side cooldowns
        var aabbs = new List<RectInt>(packMap.rooms.Count);
        var cooldown = new Dictionary<int, int[]>(packMap.rooms.Count); // 0:E 1:W 2:N 3:S

        for (int ri = 0; ri < packMap.rooms.Count; ri++)
        {
            aabbs.Add(ComputeAabb(packMap.rooms[ri]));
            cooldown[ri] = new int[4];
        }

        int touched = 0;

        // ================= 1) STRIP ROUNDS (rectangular growth) =================
        int stripRounds = 40;
        int wavefrontRounds = 50;
        int targetAspect = 2; // tune: try to keep rooms from going too skinny
        int maxAspect = 5;    // tune: if exceeded, cool long axis
        int cooldownOnFail = 3; // tune: how long to cool a side that failed to grow
        int yieldEvery = 256;

        if (stripRounds > 0)
        {
            BottomBanner.Show($"Growth: Strip rounds (x{stripRounds}) + Wavefront");
            for (round = 0; round < stripRounds; round++)
            {
                bool anyGrewThisRound = false;


                for (int ri = 0; ri < packMap.rooms.Count; ri++)
                {
                    var room = packMap.rooms[ri];
                    if (room.cells.Count == 0) continue;

                    var bb = aabbs[ri];
                    int width = Mathf.Max(1, bb.width);
                    int height = Mathf.Max(1, bb.height);
                    float aspect = (float)Mathf.Max(width, height) / Mathf.Max(1, Mathf.Min(width, height));

                    // Score sides (E,W,N,S). Prefer short axis; skip cooled sides.
                    var order = ScoreSidesForStrip(ri, bb, targetAspect, aspect, cooldown[ri]);
                    //bool grown = false;

                    for (int k = 0; k < order.Count; k++)
                    {
                        int side = order[k];
                        if (cooldown[ri][side] > 0) continue;

                        if (TryGrowFullStrip(ri, ref bb, side, moat))
                        {
                            // success: update AABB & cooldown bookkeeping
                            aabbs[ri] = bb;
                            //grown = true;
                            anyGrewThisRound = true;

                            // Small guard: if aspect exploded, roll back by cooling the long axis next time
                            width = Mathf.Max(1, bb.width); height = Mathf.Max(1, bb.height);
                            aspect = (float)Mathf.Max(width, height) / Mathf.Max(1, Mathf.Min(width, height));
                            if (aspect > maxAspect)
                            {
                                // cool the long axis sides for a bit
                                if (width > height) { cooldown[ri][2] = Mathf.Max(cooldown[ri][2], cooldownOnFail); cooldown[ri][3] = Mathf.Max(cooldown[ri][3], cooldownOnFail); }
                                else { cooldown[ri][0] = Mathf.Max(cooldown[ri][0], cooldownOnFail); cooldown[ri][1] = Mathf.Max(cooldown[ri][1], cooldownOnFail); }
                            }

                            break; // grow one strip per room per round
                        }
                        else
                        {
                            cooldown[ri][side] = Mathf.Max(cooldown[ri][side], cooldownOnFail);
                        }
                    }

                    // decay cooldowns
                    var cd = cooldown[ri];
                    for (int i = 0; i < 4; i++) if (cd[i] > 0) cd[i]--;

                    if ((++touched % yieldEvery) == 0) yield return null;
                }

                // Early exit if nothing grew
                if (!anyGrewThisRound) break;
            }
        }
        // ======= Wavefront rounds (irregular growth) =======

        if (wavefrontRounds > 0)
        {
            credits = new int[nRooms];
            for (int i = 0; i < nRooms; i++)
                credits[i] = rng.Next(cfg.grow.areaCreditMin, cfg.grow.areaCreditMax + 1);

            BottomBanner.Show($"Growth: Wavefront rounds (up to {wavefrontRounds})");
            while (activeRooms > 0 && safety-- > 0)
            {
                bool anyClaimed = false;

                for (int ri = 0; ri < nRooms; ri++)
                {
                    if (credits[ri] <= 0) continue;
                    if (frontiers[ri].Count == 0) continue;

                    // Pick a frontier cell (simple heuristic: prefer most neighbors of same room to smooth shapes)
                    var claim = PickFrontier(frontiers[ri], ri);

                    if (claim.x >= 0)
                    {
                        ClaimCell(ri, claim.x, claim.y);
                        credits[ri]--;
                        anyClaimed = true;

                        // Update frontier around the new claim
                        foreach (var nb in FourNeighbors(claim.x, claim.y))
                        {
                            if (CanClaim(ri, nb.x, nb.y, moat))
                                frontiers[ri].Add((nb.x, nb.y));
                        }
                        // Remove the claimed cell from all frontiers
                        frontiers[ri].Remove((claim.x, claim.y));
                        for (int rj = 0; rj < nRooms; rj++)
                            if (rj != ri) frontiers[rj].Remove((claim.x, claim.y));
                    }

                    // yield cooperatively
                    if (((ri + round) & 63) == 0) yield return null;
                }

                // Clean up exhausted rooms
                activeRooms = 0;
                for (int ri = 0; ri < nRooms; ri++)
                    if (credits[ri] > 0 && frontiers[ri].Count > 0) activeRooms++;

                // Optionally split oversized rooms every few rounds
                if ((round++ % 20) == 0)
                    SplitOversizedRooms(moat, frontiers);

                if (!anyClaimed) break; // no more expansion possible
            }
        }

        // TODO: convert claimed PackCells into Room.cells lists
        //for (int ri = 0; ri < nRooms; ri++)
        //{
        //    var r = packMap.rooms[ri];
        //    r.cells.Clear();
        //}

        // DEBUG: Delete all non-corridor Room Cells (only contains seeds, which would be duplicated):
        foreach (Room room in rooms)
        {
            if (room.isCorridor == false) room.cells.Clear();
        }
        
        // foreach (Room r in rooms) r.cells.Clear();  // Debug: eliminate everything
        for (int x = 0; x < packMap.w; x++)
        {
            for (int y = 0; y < packMap.h; y++)
            {
                var c = packMap.g[x, y];
                if (c.roomId < 0) continue;
                // find room by id and add cell to that room
                foreach (var r in rooms)
                {
                    if (r.my_room_number == c.roomId)
                    {
                        r.cells.Add(new Cell(x, y) { colorFloor = r.colorFloor });
                        break;
                    }
                }
            }
            yield return null;
        }
        
        DrawMapByRooms(rooms, clearscreen: true);
        yield return new WaitForSeconds(1f);
    }

    // =========================================================
    // ==================== HELPERS ============================
    // =========================================================

    // These all need to be cleaned up, commented, and organized better....

    (int x, int y) PickFrontier(HashSet<(int x, int y)> frontier, int ri)
    {
        int bestScore = int.MinValue;
        (int x, int y) best = (-1, -1);
        foreach (var p in frontier)
        {
            int s = 0;
            // score by how many owned neighbors (smooth boundary) and how far from corridors
            foreach (var nb in FourNeighbors(p.x, p.y))
            {
                if ((uint)nb.x >= (uint)packMap.w || (uint)nb.y >= (uint)packMap.h) continue;
                if (packMap.g[nb.x, nb.y].roomId == ri) s += 2;
                if (packMap.g[nb.x, nb.y].isCorridor) s -= 3;
            }
            if (s > bestScore) { bestScore = s; best = p; }
        }
        // fallback if set somehow empty
        if (best.x < 0 && frontier.Count > 0) foreach (var p in frontier) { best = p; break; }
        return best;
    }

    void SplitOversizedRooms(int moatCells, List<HashSet<(int, int)>> frontiers)
    {
        for (int i = 0; i < packMap.rooms.Count; i++)
        {
            var r = packMap.rooms[i];
            int area = r.cells.Count;
            if (area <= cfg.grow.splitArea) continue; // Don't cut room

            // Compute room AABB and aspect ratio
            int minx = int.MaxValue, miny = int.MaxValue, maxx = int.MinValue, maxy = int.MinValue;
            foreach (var c in r.cells) { if (c.x < minx) minx = c.x; if (c.x > maxx) maxx = c.x; if (c.y < miny) miny = c.y; if (c.y > maxy) maxy = c.y; }
            int w = Mathf.Max(1, maxx - minx + 1);
            int h = Mathf.Max(1, maxy - miny + 1);
            float aspect = (float)Mathf.Max(w, h) / Mathf.Max(1, Mathf.Min(w, h));
            if (aspect < cfg.grow.splitAspect) continue; // Don't cut room

            bool splitVert = (w >= h); // cut along long axis
            int cut = splitVert ? (minx + w / 2) : (miny + h / 2);

            // Create new room
            var newRoom = new PackRoom { id = packMap.rooms.Count, cells = new List<PackCell>() };
            Room new_real_room = new();
            new_real_room.setColorFloor(highlight: false);  // DEBUG: make it look like a corridor
            // Reassign cells and carve a 1-cell wall along cut line
            var keep = new List<PackCell>();
            foreach (var c in r.cells)
            {
                bool leftSide;
                bool onCut;
                if (cfg.useThinWalls)
                {
                    leftSide = splitVert ? (c.x < cut) : (c.y < cut);
                    onCut = false;
                }
                else
                {
                    leftSide = splitVert ? (c.x < cut) : (c.y < cut);
                    onCut = splitVert ? (c.x == cut) : (c.y == cut);
                }

                if (onCut)
                {
                    // leave as wall: unassign
                    int old_room_id = c.roomId;
                    c.roomId = -1;
                    // remove from the Room.Cells list. // DEBUG: Does this work????
                    DeleteAllCellsAtPos(new Vector2Int(c.x, c.y));
                    //Cell cell;
                    //cell = rooms[old_room_id].cells.Find(cell => cell.pos.x == c.x && cell.pos.y == c.y);
                    //if (!rooms[old_room_id].cells.Remove(cell))
                    //    Debug.Log("onCut: Failed trying to remove cell from room ");

                    // clear the tile on the screen
                    tilemap.SetTile(new Vector3Int(c.x, c.y, 0), null);
                    continue;
                }
                if (leftSide) // keep in original room
                {
                    int old_room_id = c.roomId;
                    keep.Add(c);

                }
                else // rightSide, move to new room
                {
                    int old_room_id = c.roomId;
                    int new_room_id = newRoom.id;
                    c.roomId = newRoom.id;
                    newRoom.cells.Add(c);

                    // add a cell to a new room
                    Cell new_real_cell = new(c.x, c.y, 0);    // DEBUG: Set z to float new room above others
                    new_real_room.cells.Add(new_real_cell);

                    // remove from the Room.Cells list.  
                    //Debug.Log("old_room_id = " + old_room_id);
                    // DEBUG: NEVER MATCHES old_room_id, so search through all rooms
                    DeleteAllCellsAtPos(new Vector2Int(c.x, c.y));

                    // clear the tile on the screen
                    tilemap.SetTile(new Vector3Int(c.x, c.y, 0), null);
                }
            }
            r.cells = keep;
            if (newRoom.cells.Count > 0)
            {
                packMap.rooms.Add(newRoom);
                rooms.Add(new_real_room);
            }

            Debug.Log($"Splitting room {i} into newroom {newRoom.id}; splitvert {splitVert}; cutline = {cut}");
            // refresh frontiers roughly (cheap approach: recompute perimeter for both rooms)
            // NOTE: frontiers list size must match rooms; expand if we added a new room
            while (frontiers.Count < packMap.rooms.Count) frontiers.Add(new HashSet<(int, int)>());
            RebuildFrontierFor(i, moatCells, frontiers[i]);
            RebuildFrontierFor(newRoom.id, moatCells, frontiers[newRoom.id]);

            
        }
            
    }

    // Temporary hack for getting rid of lost cells
    int DeleteAllCellsAtPos(Vector2Int pos)
    {
        int num_deleted = 0;
        for (int try_room_id = 0; try_room_id < rooms.Count; try_room_id++)
        {
            int cell_index = rooms[try_room_id].cells.FindIndex(cell => cell.pos.x == pos.x && cell.pos.y == pos.y);
            if (cell_index != -1)
            {
                Cell cell = rooms[try_room_id].cells[cell_index];
                Debug.Log($"DeleteAllCellsAtPos: cell_index = {cell_index}, room_number = {try_room_id}");//, old_room_id = {old_room_id}");
                rooms[try_room_id].cells.RemoveAt(cell_index);
                num_deleted++;
            }
        }
        return num_deleted;
    }
    void RebuildFrontierFor(int ri, int moatCells, HashSet<(int, int)> dst)
    {
        dst.Clear();
        foreach (var c in packMap.rooms[ri].cells)
            foreach (var nb in FourNeighbors(c.x, c.y))
                if (CanClaim(ri, nb.x, nb.y, moatCells))
                    dst.Add((nb.x, nb.y));
    }

    // ComputeAabb() gets the bounding rectangle of all cell locations in a PackRoom
    RectInt ComputeAabb(PackRoom r)
    {
        int minx = int.MaxValue, miny = int.MaxValue, maxx = int.MinValue, maxy = int.MinValue;
        foreach (var c in r.cells)
        {
            if (c.x < minx) minx = c.x; if (c.x > maxx) maxx = c.x;
            if (c.y < miny) miny = c.y; if (c.y > maxy) maxy = c.y;
        }
        if (minx == int.MaxValue) return new RectInt(0, 0, 0, 0);
        return new RectInt(minx, miny, maxx - minx + 1, maxy - miny + 1);
    }

    // Return sides in best-first order: 0:E,1:W,2:N,3:S
    List<int> ScoreSidesForStrip(int ri, RectInt bb, float targetAsp, float currentAsp, int[] cd)
    {
        int w = Mathf.Max(1, bb.width), h = Mathf.Max(1, bb.height);
        bool preferShortAxis = (w > h * targetAsp); // true => grow N/S; false => E/W preferred if h > w*targetAsp

        var list = new List<(int side, int score)>(4);
        int baseScoreE = (h); // E/W adds a column of 'h' cells
        int baseScoreW = (h);
        int baseScoreN = (w); // N/S adds a row of 'w' cells
        int baseScoreS = (w);

        int cooldownPenalty(int side) => (cd[side] > 0) ? (cd[side] * 1000) : 0;

        // start with base gain
        int sE = baseScoreE - cooldownPenalty(0);
        int sW = baseScoreW - cooldownPenalty(1);
        int sN = baseScoreN - cooldownPenalty(2);
        int sS = baseScoreS - cooldownPenalty(3);

        // compactness bias: push short axis first
        if (preferShortAxis) { sN += 10; sS += 10; }
        else                 { sE += 10; sW += 10; }

        list.Add((0, sE)); list.Add((1, sW)); list.Add((2, sN)); list.Add((3, sS));
        list.Sort((a, b) => b.score.CompareTo(a.score));
        var order = new List<int>(4) { list[0].side, list[1].side, list[2].side, list[3].side };
        return order;
    }

    // Try to grow a full 1-cell strip on the chosen side. Returns true if the whole strip was claimed.
    // side: 0=E (x=max+1), 1=W (x=min-1), 2=N (y=max+1), 3=S (y=min-1)
    bool TryGrowFullStrip(int ri, ref RectInt bb, int side, int moatCells)
    {
        int W = cfg.mapWidth, H = cfg.mapHeight;
        int minx = bb.xMin, maxx = bb.xMax - 1;
        int miny = bb.yMin, maxy = bb.yMax - 1;

        if (side == 0) // E
        {
            int x = maxx + 1;
            if ((uint)x >= (uint)W) return false;
            for (int y = miny; y <= maxy; y++)
                if (!CanClaim(ri, x, y, moatCells)) return false;

            for (int y = miny; y <= maxy; y++) ClaimCell(ri, x, y);
            bb.width += 1;
            return true;
        }
        if (side == 1) // W
        {
            int x = minx - 1;
            if (x < 0) return false;
            for (int y = miny; y <= maxy; y++)
                if (!CanClaim(ri, x, y, moatCells)) return false;

            for (int y = miny; y <= maxy; y++) ClaimCell(ri, x, y);
            bb.x -= 1; bb.width += 1;
            return true;
        }
        if (side == 2) // N
        {
            int y = maxy + 1;
            if ((uint)y >= (uint)H) return false;
            for (int x = minx; x <= maxx; x++)
                if (!CanClaim(ri, x, y, moatCells)) return false;

            for (int x = minx; x <= maxx; x++) ClaimCell(ri, x, y);
            bb.height += 1;
            return true;
        }
        else // 3:S
        {
            int y = miny - 1;
            if (y < 0) return false;
            for (int x = minx; x <= maxx; x++)
                if (!CanClaim(ri, x, y, moatCells)) return false;

            for (int x = minx; x <= maxx; x++) ClaimCell(ri, x, y);
            bb.y -= 1; bb.height += 1;
            return true;
        }
    }

    // Wavefront helpers (compactness-biased pick)
    (int x,int y) PickFrontier_CompactBias(HashSet<(int x,int y)> frontier, int ri)
    {
        int W = cfg.mapWidth, H = cfg.mapHeight;
        int bestScore = int.MinValue; (int x,int y) best = (-1,-1);
        foreach (var p in frontier)
        {
            int s = 0;
            foreach (var nb in FourNeighbors(p.x, p.y))
            {
                if ((uint)nb.x >= (uint)W || (uint)nb.y >= (uint)H) continue;
                if (packMap.g[nb.x, nb.y].roomId == ri) s += 2;               // prefer filling along our boundary
                if (packMap.g[nb.x, nb.y].isCorridor) s -= 3;                  // keep distance to corridors
            }
            if (s > bestScore) { bestScore = s; best = p; }
        }
        if (best.x < 0 && frontier.Count > 0) foreach (var p in frontier) { best = p; break; }
        return best;
    }

    IEnumerable<(int x,int y)> FourNeighbors(int x, int y)
    {
        int W = cfg.mapWidth, H = cfg.mapHeight;
        if (x > 0)       yield return (x - 1, y);
        if (x < W - 1)   yield return (x + 1, y);
        if (y > 0)       yield return (x, y - 1);
        if (y < H - 1)   yield return (x, y + 1);
    }

    bool CanClaim(int ri, int x, int y, int moatCells)
    {
        if (cfg.useThinWalls) moatCells = 0;

        int W = cfg.mapWidth, H = cfg.mapHeight;
        if ((uint)x >= (uint)W || (uint)y >= (uint)H) return false;
        var c = packMap.g[x, y];
        if (c.isCorridor) return false;
        if (c.roomId >= 0 && c.roomId != ri) return false;

        // keep a moat around corridors and other rooms
        for (int dy = -moatCells; dy <= moatCells; dy++)
        for (int dx = -moatCells; dx <= moatCells; dx++)
        {
            int nx = x + dx, ny = y + dy;
            if ((uint)nx >= (uint)W || (uint)ny >= (uint)H) continue;
            var n = packMap.g[nx, ny];
            if (n.isCorridor) return false;
            if (n.roomId >= 0 && n.roomId != ri) return false;
        }
        return true;
    }

    void ClaimCell(int ri, int x, int y)
    {
        var c = packMap.g[x, y];
        if (c.roomId == ri) return;
        c.roomId = ri;
        packMap.rooms[ri].cells.Add(c);
    }




    
    // ---- local helpers ----

    int CountCorridorNeighbors(int x, int y)
    {
        int W=cfg.mapWidth, H=cfg.mapHeight;
        int c = 0;
        if (x > 0 && packMap.g[x - 1, y].isCorridor) c++;
        if (x < W - 1 && packMap.g[x + 1, y].isCorridor) c++;
        if (y > 0 && packMap.g[x, y - 1].isCorridor) c++;
        if (y < H - 1 && packMap.g[x, y + 1].isCorridor) c++;
        return c;
    }

    // PickTangentDir() works for corridors 1 or 2 cells wide, not for 3 or more.
    Vector2Int PickTangentDir_old(int x, int y)
    {
        int W=cfg.mapWidth, H=cfg.mapHeight;
        // Favor a straight neighbor pair if present (→ a stable tangent)
        bool L = x > 0 && packMap.g[x - 1, y].isCorridor;
        bool R = x < W - 1 && packMap.g[x + 1, y].isCorridor;
        bool D = y > 0 && packMap.g[x, y - 1].isCorridor;
        bool U = y < H - 1 && packMap.g[x, y + 1].isCorridor;

        if (L && R) return new Vector2Int(1, 0);
        if (D && U) return new Vector2Int(0, 1);

        // Corner or dead-end: pick whichever neighbor exists (prefer continuity)
        if (R) return new Vector2Int(1, 0);
        if (L) return new Vector2Int(-1, 0);
        if (U) return new Vector2Int(0, 1);
        if (D) return new Vector2Int(0, -1);

        return Vector2Int.zero;
    }

    // Pick a tangent direction at (x,y) by counting how many contiguous corridor
    // cells exist to the Left/Right/Down/Up (L/R/D/U). Wider corridors are handled
    // because we look past a single neighbor. Returns (1,0) for horizontal,
    // (0,1) for vertical, or Vector2Int.zero if nothing usable is found.
    Vector2Int PickTangentDir(int x, int y, int scanLen = 12, int minStraight = 2)
    {
        int W = cfg.mapWidth, H = cfg.mapHeight;

        int L = CountRun(x, y, -1, 0, scanLen);
        int R = CountRun(x, y, 1, 0, scanLen);
        int D = CountRun(x, y, 0, -1, scanLen);
        int U = CountRun(x, y, 0, 1, scanLen);

        int horizontal = L + R;
        int vertical = D + U;

        // Strong signal: both sides present along an axis
        bool hasHoriz = (L >= minStraight && R >= minStraight);
        bool hasVert = (D >= minStraight && U >= minStraight);

        if (hasHoriz && !hasVert) return new Vector2Int(1, 0);
        if (!hasHoriz && hasVert) return new Vector2Int(0, 1);

        // If both (junction) or neither (corner/dead-end), pick the axis with more total run.
        if (horizontal > vertical) return new Vector2Int(1, 0);
        if (vertical > horizontal) return new Vector2Int(0, 1);

        // Tie-breakers:
        // 1) prefer the side with the single longest run
        int longest = Mathf.Max(Mathf.Max(L, R), Mathf.Max(D, U));
        if (longest == 0) return Vector2Int.zero; // isolated or no corridors around

        if (longest == L || longest == R) return new Vector2Int(1, 0);
        if (longest == D || longest == U) return new Vector2Int(0, 1);

        return Vector2Int.zero; // very rare fallback
    }

    int CountRun(int sx, int sy, int dx, int dy, int maxSteps)
    {
        int W = cfg.mapWidth, H = cfg.mapHeight;

        int c = 0;
        for (int i = 1; i <= maxSteps; i++)
        {
            int nx = sx + dx * i, ny = sy + dy * i;
            if ((uint)nx >= (uint)W || (uint)ny >= (uint)H) break;

            // Fast path using your grid flag:
            if (!packMap.g[nx, ny].isCorridor) break;

            c++;
        }
        return c;
    }

    Vector2Int Perp(Vector2Int t, bool left)
    {
        // left: (x,y)->(-y,x) ; right: (x,y)->(y,-x)
        return left ? new Vector2Int(-t.y, t.x) : new Vector2Int(t.y, -t.x);
    }

    void Shuffle(List<Vector2Int> list)
    {
        for (int i = list.Count - 1; i > 0; --i)
        {
            int j = rng.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    bool CanPlaceSeed(int x, int y, int moatCells)
    {
        if (!In(x, y)) return false;
        var c = packMap.g[x, y];
        if (c.isCorridor) return false;
        if (c.roomId >= 0) return false;

        // Enforce a moat around corridors and other rooms
        for (int dy = -moatCells; dy <= moatCells; dy++)
            for (int dx = -moatCells; dx <= moatCells; dx++)
            {
                int nx = x + dx, ny = y + dy;
                if (!In(nx, ny)) continue;
                var n = packMap.g[nx, ny];
                if (n.isCorridor) return false;
                if (n.roomId >= 0) return false;
            }
        return true;
    }

    void CreateRoomSeedAt(int x, int y)
    {
        var r = new PackRoom { id = packMap.rooms.Count, cells = new List<PackCell>() };
        var c = packMap.g[x, y];
        //var c = new PackCell { x = x, y = y };      // DEBUG TRY creating new cell.. no difference seen.
        //packMap.g[x, y] = c;

        c.roomId = r.id;
        r.cells.Add(c);
        r.bounds = new RectInt(x, y, 1, 1);
        packMap.rooms.Add(r);
        // Also add to main room list for drawing
        Room room = new Room { my_room_number = r.id, cells = new List<Cell> { new Cell(x, y, 2) }, isCorridor = false };
        // DEBUG: Don't include the seed in the new Room.
        //Room room = new Room { my_room_number = r.id, cells = new List<Cell> { }, isCorridor = false };
        room.setColorFloor(highlight: true);
        rooms.Add(room);
        Debug.Log($"Created Room {c.roomId} seed at {x},{y}");
    }




    // ======================= Convert PackMap to usable format =======================
/*
    public List<Room> ExtractRoomsFromPackedRooms(PackMap packMap)
    {
        Color color;
        Debug.Log($"Extracting {packMap.rooms.Count} rooms and {packMap.corridors.Count} corridors from PackMap...");

        var result = new List<Room>(packMap.rooms.Count);
        //HashSet<(int, int)> corridorHash = new();
        // Convert corridors
        foreach (var pr in packMap.corridors)
        {
            List<Cell> cells = new();

            //if (pr.cells.Count == 0) continue;
            // Finalize room bounds
            int minx = int.MaxValue, miny = int.MaxValue, maxx = int.MinValue, maxy = int.MinValue;

            // Create this Room's cell list (x,y) only
            foreach (var c in packMap.g)
            {
                cells.Add(new Cell(c.x, c.y));
                { if (c.x < minx) minx = c.x; if (c.x > maxx) maxx = c.x; if (c.y < miny) miny = c.y; if (c.y > maxy) maxy = c.y; }
            }
            Debug.Log($" Corridor has {cells.Count} cells, bounds {minx},{miny} to {maxx},{maxy}");

            // Create Room object
            var r = new Room
            {
                my_room_number = 1,
                area = cells.Count,
                bounds = new RectInt(minx, miny, Mathf.Max(1, maxx - minx + 1), Mathf.Max(1, maxy - miny + 1)),
                cells = cells,
                isCorridor = true,
            };
            r.setColorFloor(highlight: false);
            color = r.colorFloor;
            foreach (Cell cell in r.cells) cell.colorFloor = color;
            result.Add(r);
        }


        // Repeat for rooms
        foreach (var pr in packMap.rooms)
        {
            List<Cell> cells = new();

            if (pr.cells.Count == 0) continue;
            // Finalize room bounds
            int minx = int.MaxValue, miny = int.MaxValue, maxx = int.MinValue, maxy = int.MinValue;
            foreach (var c in pr.cells) { if (c.x < minx) minx = c.x; if (c.x > maxx) maxx = c.x; if (c.y < miny) miny = c.y; if (c.y > maxy) maxy = c.y; }
            pr.bounds = new RectInt(minx, miny, Mathf.Max(1, maxx - minx + 1), Mathf.Max(1, maxy - miny + 1));

            // Create this Room's cell list (x,y) only

            foreach (var c in pr.cells)
            {
                cells.Add(new Cell(c.x, c.y));
            }

            // Create Room object
            var r = new Room
            {
                my_room_number = pr.id,
                area = pr.cells.Count,
                bounds = pr.bounds,
                cells = cells,
                isCorridor = false,
            };
            r.setColorFloor(highlight: true);
            color = r.colorFloor;
            foreach (Cell cell in r.cells) cell.colorFloor = color;
            result.Add(r);
        }
        return result;
    }
*/
    public Room ExtractRoomFromVectors(List<Vector2Int> vect)
    {
        Color color;
        Debug.Log($"Extracting {vect.Count} vectors..");

        var result = new Room();
        //HashSet<(int, int)> corridorHash = new();
        // Convert corridors
        foreach (var pr in vect)
        {
            List<Cell> cells = new();

            //if (pr.cells.Count == 0) continue;
            // Finalize room bounds
            int minx = int.MaxValue, miny = int.MaxValue, maxx = int.MinValue, maxy = int.MinValue;

            // Create this Room's cell list (x,y) only
            foreach (var c in vect)
            {
                cells.Add(new Cell(c.x, c.y));
                { if (c.x < minx) minx = c.x; if (c.x > maxx) maxx = c.x; if (c.y < miny) miny = c.y; if (c.y > maxy) maxy = c.y; }
            }

            // Create Room object
            var r = new Room
            {
                my_room_number = 1,
                area = cells.Count,
                bounds = new RectInt(minx, miny, Mathf.Max(1, maxx - minx + 1), Mathf.Max(1, maxy - miny + 1)),
                cells = cells,
                isCorridor = true,
            };
            r.setColorFloor(highlight: false);
            color = r.colorFloor;
            foreach (Cell cell in r.cells) cell.colorFloor = color;

            return r;
        }

        return result;
    }


    // ======================= Shared Utility functions =======================
    // shared functions pulled out of above
    Vector2Int RandomCardinal()
    {
        switch (rng.Next(0, 4))
        {
            case 0: return new Vector2Int(1, 0);
            case 1: return new Vector2Int(-1, 0);
            case 2: return new Vector2Int(0, 1);
            default: return new Vector2Int(0, -1);
        }
    }

    // This replaces RandomCardinal for starting positions to keep from following borders too much.
    Vector2Int DirAwayFromEdge(Vector2Int pos)
    {
        int border = 10;    // distance to edge that is considered too close
        int W = cfg.mapWidth, H = cfg.mapHeight;
        if ((W - pos.x) < border) return new Vector2Int(-1, 0);
        if ((pos.x) < border)     return new Vector2Int(1, 0);
        if ((H - pos.y) < border) return new Vector2Int(0, -1);
        if ((pos.y) < border)     return new Vector2Int(0, 1);
        return RandomCardinal();  // not near any edge
    }

    Vector2Int TurnLeft(Vector2Int d, bool left)
    {
        // left: (x,y)->(-y,x) ; right: (x,y)->(y,-x)
        return left ? new Vector2Int(-d.y, d.x) : new Vector2Int(d.y, -d.x);
    }

    bool In(int x, int y) => (uint)x < (uint)cfg.mapWidth && (uint)y < (uint)cfg.mapHeight
                            && x>=0 && y>=0;

    // If you want random edge starts instead of center:
    Vector2Int RandomEdgeStart(int w, int h)
    {
        int edge = rng.Next(0, 4);
        return edge switch
        {
            0 => new Vector2Int(rng.Next(0, w), 0),
            1 => new Vector2Int(rng.Next(0, w), h - 1),
            2 => new Vector2Int(0, rng.Next(0, h)),
            _ => new Vector2Int(w - 1, rng.Next(0, h)),
        };
    }

    // Uses your existing painter & storage; keep it dumb for debugging.
    void CarveDisk(Room tmp_room, Vector2Int c, int penWidth)
    {
        int W = cfg.mapWidth, H = cfg.mapHeight;
        int min = -(int)Math.Floor((penWidth / 2f)); // makes the negative more to zero
        int max = min + penWidth - 1;

        // Debug.Log($"  CarveDisk at {c.x},{c.y} width={penWidth}");
        for (int dy = min; dy <= max; dy++)
            for (int dx = min; dx <= max; dx++)
            {
                int x = c.x + dx, y = c.y + dy;
                if ((uint)x >= (uint)W || (uint)y >= (uint)H) continue;
                // use square pen or trim corners of round one?
                if (cfg.useRoundPen && (dx * dx + dy * dy > (penWidth / 2f) * (penWidth / 2f))) continue; // disk; swap to diamond if you prefer

                // create a new cell and add it to the room that was passed in
                var tmp_cell = new Cell(x, y);
                tmp_cell.colorFloor = tmp_room.colorFloor;
                tmp_room.cells.Add(tmp_cell);

                // add it to PackMap also
                PackCell packCell = new();
                packCell.x = x;
                packCell.y = y;
                packCell.isCorridor = true;
                packMap.corridors.Add((x,y));
                packMap.g[x, y] = packCell;
            }
    }

    Vector2Int MaybeTurn(Vector2Int d, Random r, float wander)
    {
        // with some prob, keep going; else turn 90° left/right
        if (r.NextDouble() < (wander/1000f)) return d; //(0.65f - 0.4f * wander)) return d;
        return (r.Next(2) == 0) ? TurnLeft(d, true) : TurnLeft(d, false);
    }

    int Manhattan(Vector2Int a, Vector2Int b) => Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
    
    // RasterizeLineSafe() looks to be a Bresenham line algorithm for any point to point lines
    IEnumerable<Vector2Int> RasterizeLineSafe(Vector2Int a, Vector2Int b)
    {
        int x0 = a.x, y0 = a.y, x1 = b.x, y1 = b.y;
        int dx = Mathf.Abs(x1 - x0), sx = x0 < x1 ? 1 : -1;
        int dy = -Mathf.Abs(y1 - y0), sy = y0 < y1 ? 1 : -1;
        int err = dx + dy;

        // Hard cap: the line cannot be longer than dx+|dy|+1 steps
        int maxSteps = dx + (-dy) + 1;

        for (int steps = 0; steps < maxSteps; steps++)
        {
            yield return new Vector2Int(x0, y0);
            if (x0 == x1 && y0 == y1) yield break;

            int e2 = err << 1; // 2*err
            bool stepped = false;

            if (e2 >= dy) { err += dy; x0 += sx; stepped = true; }
            if (e2 <= dx) { err += dx; y0 += sy; stepped = true; }

            // Safety: if neither branch moved (shouldn’t happen), force a move toward target
            if (!stepped)
            {
                if (x0 != x1) x0 += sx;
                else if (y0 != y1) y0 += sy;
            }
        }
        // If we ever fall out by hitting maxSteps, just stop
        yield break;
    }

}
