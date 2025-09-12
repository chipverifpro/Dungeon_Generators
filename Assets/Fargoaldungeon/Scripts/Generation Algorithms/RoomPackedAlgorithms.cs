using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
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
        Debug.Log("After room seeding, rooms = " + rooms_temp.Count);
        yield return new WaitForSeconds(1f);

        // 3) Room growth
        yield return StartCoroutine(RunRoomGrowth());
        DrawMapByRooms(rooms);
        Debug.Log("After room growth, rooms = " + rooms_temp.Count);
        yield return new WaitForSeconds(1f);

        // 4) Scraps
        yield return StartCoroutine(RunScraps());
        DrawMapByRooms(rooms);
        Debug.Log("After scraps, rooms = " + rooms_temp.Count);
        yield return new WaitForSeconds(1f);

        // 5) Doors/connectivity
        yield return StartCoroutine(RunDoors());
        DrawMapByRooms(rooms);
        Debug.Log("After doors, rooms = " + rooms_temp.Count);
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
                walkers: 5,
                stepsPerWalker: 100, // short-ish for debug
                turnProbability: 0.15f,
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
        float turnProbability = 0.30f,   // chance to turn 90° each step
        int corridorWidth = 1,           // 1..2 to keep it skinny
        bool bounceAtEdges = true,       // if false, pick new random start when we hit an edge
        int yieldEvery = 256             // cooperative yield cadence
    )
    {
        BottomBanner.Show("Corridors: Drunkard's Walk");
        int W = cfg.mapWidth, H = cfg.mapHeight;
        corridorWidth = Mathf.Clamp(corridorWidth <= 0 ? cfg.corridor.corridorWidth : corridorWidth, 1, 2);

        List<Cell> corridorCells = new(); // to pass to DrawMapByRooms

        // Simple RNG fallback: use your 'rng' if you have it; else UnityEngine.Random
        System.Func<float> R01 = () => (rng != null) ? (float)rng.NextDouble() : UnityEngine.Random.value;
        System.Func<int, int, int> RInt = (a, b) => (rng != null) ? rng.Next(a, b) : UnityEngine.Random.Range(a, b);

        int carved = 0;

        for (int wlk = 0; wlk < walkers; wlk++)
        {
            // Start near center (stable) or random edge if you prefer
            // Vector2Int p = new Vector2Int(W / 2, H / 2);
            Vector2Int p = RandomEdgeStart(W, H); // alternative start

            Vector2Int dir = RandomCardinal();

            for (int step = 0; step < stepsPerWalker; step++)
            {
                // Carve corridor at p
                corridorCells.Add(new Cell(p.x, p.y));
                carved++;

                // Maybe turn 90°
                if (R01() < turnProbability)
                    dir = (R01() < 0.5f) ? TurnLeft(dir, true) : TurnLeft(dir, false);

                // Advance
                Vector2Int np = p + dir;

                if (!In(np.x, np.y))
                {
                    if (bounceAtEdges)
                    {
                        // bounce: turn and try to continue
                        dir = TurnLeft(dir, R01() < 0.5f);
                        np = p + dir;

                        if (!In(np.x, np.y))
                        {
                            // fully stuck: pick a fresh random in-bounds location
                            np = new Vector2Int(RInt(0, W), RInt(0, H));
                            dir = RandomCardinal();
                        }
                    }
                    else
                    {
                        // restart from new random position
                        np = new Vector2Int(RInt(0, W), RInt(0, H));
                        dir = RandomCardinal();
                    }
                }

                p = np;

                // Periodic yield to keep Editor responsive
                if ((carved % yieldEvery) == 0) yield return null;
            }
            var tmp_room = new Room { cells = new List<Cell>(corridorCells), isCorridor = true };
            tmp_room.setColorFloor(highlight: false);
            foreach (var cell in tmp_room.cells) { cell.colorFloor = tmp_room.colorFloor; }

            rooms.Add(tmp_room);
            Debug.Log("Drawing rooms = " + rooms.Count);
            DrawMapByRooms(rooms, clearscreen: true);
            yield return new WaitForSeconds(1f);

            corridorCells.Clear();
        }

        BottomBanner.Show($"Corridors: Drunkard's Walk done. Carved ~{carved} cells.");

    }

    // ======================= Corridors: WanderingMST =======================
    IEnumerator Corridors_WanderingMST()
    {
        BottomBanner.Show("Corridors: WanderingMST");
        int W = cfg.mapWidth, H = cfg.mapHeight;
        int width = Mathf.Clamp(cfg.corridor.corridorWidth, 1, 5);
        int spines = Mathf.Max(1, cfg.corridor.spineCount);
        float wander = Mathf.Clamp01(cfg.corridor.wanderiness);
        float loopChance = Mathf.Clamp01(cfg.corridor.loopChance);

        // 1) Make wandering spines starting near PackMap edges
        var rngf = new System.Func<float>(() => (float)rng.NextDouble());
        var nodes = new List<Vector2Int>();  // sampled waypoints along spines

        List<Room> rooms_temp = new(); // temporary Room list for compatibility with DrawMapByRooms
        Room room_temp;

        Debug.Log("Corridors WanderingMST: Beginning Drawing rooms = " + rooms.Count);
        DrawMapByRooms(rooms, clearscreen: true);
        yield return new WaitForSeconds(1f);

        var tmp_room = new Room { cells = new List<Cell>(), isCorridor = true };
        tmp_room.setColorFloor(highlight: false);

        for (int s = 0; s < spines; s++)
        {
            Debug.Log($"Corridor spine {s + 1} of {spines}");
            yield return null;

            //var tmp_room = new Room { cells = new List<Cell>(), isCorridor = true };
            //tmp_room.setColorFloor(highlight: false);

            // Start near a random border
            Vector2Int p = RandomEdgeStart(W, H);
            Vector2Int dir = RandomCardinal();

            int steps = (int)(0.7f * (W + H)); // long-ish 0.7
            steps = 50;
            int sampleEvery = 12; //12
            int sinceSample = 0;

            for (int i = 0; i < steps; i++)
            {
                Debug.Log($"  step {i + 1} of {steps} at {p.x},{p.y} dir={dir.x},{dir.y}");
                yield return null;
                if (i == 250) break; // DEBUG CHECK

                CarveDisk(tmp_room, p, width); // paint corridor cell(s)
                sinceSample++;

                // Randomly sample nodes along the walk (used by MST)
                if (sinceSample >= sampleEvery)
                {
                    nodes.Add(p);
                    sinceSample = 0;
                }

                // Wander the direction a bit
                if (rngf() < 0.15f + wander * 0.5f) dir = MaybeTurn(dir, rng, wander);

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

            yield return new WaitForSeconds(1f);
        }

        room_temp = ExtractRoomFromVectors(nodes);
        Debug.Log("nodes = " + nodes.Count + " after steps, before thinned ");
        rooms_temp.Add(room_temp);
        DrawMapByRooms(rooms_temp);
        yield return new WaitForSeconds(10f);

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
        yield return new WaitForSeconds(10f);

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

    // ======================= Growth: CreditWavefront =======================
    IEnumerator Grow_CreditWavefront()
    {
        BottomBanner.Show("Growth: CreditWavefront");
        // PRECONDITIONS:
        // - PackMap.rooms contains Room objects
        // - Each Room has at least one seed Cell in PackMap.g[,] with cell.roomId = room.id
        // - Corridors already painted (cell.isCorridor = true)
        // - We will preserve a 1-cell wall moat (= cfg.grow.wallMoat) around rooms & corridors

        int moat = Mathf.Max(1, cfg.grow.wallMoat);
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
                SplitOversizedRooms(moat);

            if (!anyClaimed) break; // no more expansion possible
        }

        // ---- local helpers ----
        IEnumerable<(int x, int y)> FourNeighbors(int x, int y)
        {
            if (x > 0) yield return (x - 1, y);
            if (x < packMap.w - 1) yield return (x + 1, y);
            if (y > 0) yield return (x, y - 1);
            if (y < packMap.h - 1) yield return (x, y + 1);
        }

        bool CanClaim(int ri, int x, int y, int moatCells)
        {
            if ((uint)x >= (uint)packMap.w || (uint)y >= (uint)packMap.h) return false;
            var c = packMap.g[x, y];
            if (c.isCorridor) return false;
            if (c.roomId >= 0) return false; // already owned
            // Keep a moat around corridors & other rooms
            for (int dy = -moatCells; dy <= moatCells; dy++)
                for (int dx = -moatCells; dx <= moatCells; dx++)
                {
                    int nx = x + dx, ny = y + dy;
                    if ((uint)nx >= (uint)packMap.w || (uint)ny >= (uint)packMap.h) continue;
                    if (packMap.g[nx, ny].isCorridor) return false;
                    int rid = packMap.g[nx, ny].roomId;
                    if (rid >= 0 && rid != ri) return false;
                }
            return true;
        }

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

        void ClaimCell(int ri, int x, int y)
        {
            var c = packMap.g[x, y];
            c.roomId = packMap.rooms[ri].id;
            packMap.rooms[ri].cells.Add(c);
            // bounds will be finalized later; optional: update a running RectInt here
        }

        void SplitOversizedRooms(int moatCells)
        {
            for (int i = 0; i < packMap.rooms.Count; i++)
            {
                var r = packMap.rooms[i];
                int area = r.cells.Count;
                if (area <= cfg.grow.splitArea) continue;

                // Compute room AABB and aspect ratio
                int minx = int.MaxValue, miny = int.MaxValue, maxx = int.MinValue, maxy = int.MinValue;
                foreach (var c in r.cells) { if (c.x < minx) minx = c.x; if (c.x > maxx) maxx = c.x; if (c.y < miny) miny = c.y; if (c.y > maxy) maxy = c.y; }
                int w = Mathf.Max(1, maxx - minx + 1);
                int h = Mathf.Max(1, maxy - miny + 1);
                float aspect = (float)Mathf.Max(w, h) / Mathf.Max(1, Mathf.Min(w, h));
                if (aspect < cfg.grow.splitAspect) continue;

                bool splitVert = (w >= h); // cut along long axis
                int cut = splitVert ? (minx + w / 2) : (miny + h / 2);

                // Create new room
                var newRoom = new PackRoom { id = packMap.rooms.Count, cells = new List<PackCell>() };
                // Reassign cells and carve a 1-cell wall along cut line
                var keep = new List<PackCell>();
                foreach (var c in r.cells)
                {
                    bool leftSide = splitVert ? (c.x < cut) : (c.y < cut);
                    bool onCut = splitVert ? (c.x == cut) : (c.y == cut);

                    if (onCut)
                    {
                        // leave as wall: unassign
                        c.roomId = -1;
                        continue;
                    }
                    if (leftSide) keep.Add(c);
                    else { c.roomId = newRoom.id; newRoom.cells.Add(c); }
                }
                r.cells = keep;
                if (newRoom.cells.Count > 0) packMap.rooms.Add(newRoom);

                // refresh frontiers roughly (cheap approach: recompute perimeter for both rooms)
                // NOTE: frontiers list size must match rooms; expand if we added a new room
                while (frontiers.Count < packMap.rooms.Count) frontiers.Add(new HashSet<(int, int)>());
                RebuildFrontierFor(i, moatCells, frontiers[i]);
                RebuildFrontierFor(newRoom.id, moatCells, frontiers[newRoom.id]);
            }
        }

        void RebuildFrontierFor(int ri, int moatCells, HashSet<(int, int)> dst)
        {
            dst.Clear();
            foreach (var c in packMap.rooms[ri].cells)
                foreach (var nb in FourNeighbors(c.x, c.y))
                    if (CanClaim(ri, nb.x, nb.y, moatCells))
                        dst.Add((nb.x, nb.y));
        }
    }

    // ======================= Seeding: AlongCorridors =======================
    IEnumerator Seed_AlongCorridors()
    {
        BottomBanner.Show("Seeding: AlongCorridors");

        int W = packMap.w, H = packMap.h;
        int moat = Mathf.Max(1, cfg.grow.wallMoat);
        int spacing = Mathf.Max(2, cfg.RoomSeeding.spacing);     // min spacing between seeds along corridors
        int jitter = Mathf.Clamp(cfg.RoomSeeding.jitter, 0, spacing - 1);
        float altProb = Mathf.Clamp01(cfg.RoomSeeding.alternateSides); // probability to alternate sides L/R

        if (packMap.corridors == null || packMap.corridors.Count == 0)
        {
            BottomBanner.Show("  (No corridors found; seeding skipped)");
            yield break;
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
            for (int step = moat + 1; step <= moat + 3; step++)
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
                for (int step = moat + 1; step <= moat + 3; step++)
                {
                    int sx = a.x + n2.x * step;
                    int sy = a.y + n2.y * step;
                    if (!In(sx, sy)) break;

                    if (CanPlaceSeed(sx, sy, moat))
                    {
                        CreateRoomSeedAt(sx, sy);
                        created++;
                        break;
                    }
                }
            }

            if ((created & 63) == 0) yield return null;
        }

        BottomBanner.Show($"  Seeded {created} room(s) from {anchors.Count} corridor anchors.");

        // ---- local helpers ----
        bool In(int x, int y) => (uint)x < (uint)W && (uint)y < (uint)H;

        int CountCorridorNeighbors(int x, int y)
        {
            int c = 0;
            if (x > 0 && packMap.g[x - 1, y].isCorridor) c++;
            if (x < W - 1 && packMap.g[x + 1, y].isCorridor) c++;
            if (y > 0 && packMap.g[x, y - 1].isCorridor) c++;
            if (y < H - 1 && packMap.g[x, y + 1].isCorridor) c++;
            return c;
        }

        Vector2Int PickTangentDir(int x, int y)
        {
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

        Vector2Int Perp(Vector2Int t, bool left)
        {
            // left: (x,y)->(-y,x) ; right: (x,y)->(y,-x)
            return left ? new Vector2Int(-t.y, t.x) : new Vector2Int(t.y, -t.x);
        }

        Vector2Int RandomCardinal()
        {
            return rng.Next(4) switch
            {
                0 => new Vector2Int(1, 0),
                1 => new Vector2Int(-1, 0),
                2 => new Vector2Int(0, 1),
                _ => new Vector2Int(0, -1),
            };
        }

        int Manhattan(Vector2Int a, Vector2Int b) => Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);

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
            c.roomId = r.id;
            r.cells.Add(c);
            r.bounds = new RectInt(x, y, 1, 1);
            packMap.rooms.Add(r);
        }
    }


    // ======================= Convert PackMap to usable format =======================
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

    Vector2Int TurnLeft(Vector2Int d, bool left)
    {
        // left: (x,y)->(-y,x) ; right: (x,y)->(y,-x)
        return left ? new Vector2Int(-d.y, d.x) : new Vector2Int(d.y, -d.x);
    }

    bool In(int x, int y) => (uint)x < (uint)cfg.mapWidth && (uint)y < (uint)cfg.mapHeight;

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
    void CarveDisk(Room tmp_room, Vector2Int c, int rad)
    {
        int W = cfg.mapWidth, H = cfg.mapHeight;

        for (int dy = -rad; dy <= rad; dy++)
            for (int dx = -rad; dx <= rad; dx++)
            {
                int x = c.x + dx, y = c.y + dy;
                if ((uint)x >= (uint)W || (uint)y >= (uint)H) continue;
                if (dx * dx + dy * dy > rad * rad) continue; // disk; swap to diamond if you prefer

                // create a new cell and add it to the room that was passed in
                var tmp_cell = new Cell(x, y);
                tmp_cell.colorFloor = tmp_room.colorFloor;
                tmp_room.cells.Add(tmp_cell);
            }
    }

    Vector2Int MaybeTurn(Vector2Int d, Random r, float wander)
    {
        // with some prob, keep going; else turn 90° left/right
        if (r.NextDouble() < (0.65f - 0.4f * wander)) return d;
        return (r.Next(2) == 0) ? TurnLeft(d, true) : TurnLeft(d, false);
    }

    int Manhattan(Vector2Int a, Vector2Int b) => Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
    
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
