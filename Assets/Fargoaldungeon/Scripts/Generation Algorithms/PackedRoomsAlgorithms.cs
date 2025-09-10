using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = System.Random;

public partial class DungeonGenerator : MonoBehaviour
{
    //[Header("Config")]
    //public DungeonSettings cfg;     // ← your ScriptableObject, named as you like

    // Minimal map structs (adapt to your real ones)
    public class PackCell { public int x,y,height; public bool isCorridor; public int roomId=-1; }
    public class PackRoom { public int id; public List<Cell> cells = new(); public RectInt bounds; }
    public class PackMap
    {
        public int w,h;
        public PackCell[,] g;
        public List<Room> rooms = new();
        public HashSet<(int,int)> corridors = new();
        public PackMap(int w,int h){ this.w=w; this.h=h; g=new PackCell[w,h]; for(int y=0;y<h;y++)for(int x=0;x<w;x++) g[x,y]=new PackCell{ x=x,y=y }; }
        public bool In(int x,int y)=> (uint)x<(uint)w && (uint)y<(uint)h;
    }

    // Runtime
    public PackMap packMap;
    //Random rng;
    //Action<string> logger;

    public IEnumerator GeneratePackedRooms(int? seedOverride = null)
    {
        // Setup
        int seed = cfg.randomizeSeed ? UnityEngine.Random.Range(int.MinValue,int.MaxValue) : (seedOverride ?? cfg.seed);
        rng = new Random(seed);
        //Debug.Log = cfg.showBuildProcess ? (Action<string>)Debug.Log : (_)=>{};
        packMap = new PackMap(cfg.mapWidth, cfg.mapHeight);

        float t0 = Time.realtimeSinceStartup;

        // 1) Corridors
        yield return StartCoroutine(RunCorridors());
        // 2) Room seeding
        yield return StartCoroutine(RunRoomSeeding());
        // 3) Room growth
        yield return StartCoroutine(RunRoomGrowth());
        // 4) Scraps
        yield return StartCoroutine(RunScraps());
        // 5) Doors/connectivity
        yield return StartCoroutine(RunDoors());

        Debug.Log($"Done seed={seed} in {(Time.realtimeSinceStartup-t0):F2}s");
    }

    // ---------- Stage switches ----------
    IEnumerator RunCorridors()
    {
        switch (cfg.corridorAlgo)
        {
            case DungeonSettings.CorridorAlgo.WanderingMST:   return Corridors_WanderingMST();
            case DungeonSettings.CorridorAlgo.MedialAxis:     return Corridors_MedialAxis();
            case DungeonSettings.CorridorAlgo.GridMazes:      return Corridors_GridMazes();
            default:                                          return Corridors_WanderingMST();
        }
    }
    IEnumerator RunRoomSeeding()
    {
        switch (cfg.roomSeedAlgo)
        {
            case DungeonSettings.RoomSeedAlgo.AlongCorridors:        return Seed_AlongCorridors();
            case DungeonSettings.RoomSeedAlgo.PoissonAlongCorridors: return Seed_PoissonAlongCorridors();
            case DungeonSettings.RoomSeedAlgo.UniformGrid:           return Seed_UniformGrid();
            default:                                                 return Seed_AlongCorridors();
        }
    }
    IEnumerator RunRoomGrowth()
    {
        switch (cfg.roomGrowAlgo)
        {
            case DungeonSettings.RoomGrowAlgo.CreditWavefront: return Grow_CreditWavefront();
            case DungeonSettings.RoomGrowAlgo.PressureField:   return Grow_PressureField();
            case DungeonSettings.RoomGrowAlgo.OrthogonalRays:  return Grow_OrthogonalRays();
            default:                                           return Grow_CreditWavefront();
        }
    }
    IEnumerator RunScraps()
    {
        switch (cfg.scrapAlgo)
        {
            case DungeonSettings.ScrapAlgo.VoronoiFill:  return Scraps_VoronoiFill();
            case DungeonSettings.ScrapAlgo.ClosetsOnly:  return Scraps_ClosetsOnly();
            case DungeonSettings.ScrapAlgo.NearestRoom:  return Scraps_NearestRoom();
            default:                                     return Scraps_VoronoiFill();
        }
    }
    IEnumerator RunDoors()
    {
        switch (cfg.doorAlgo)
        {
            case DungeonSettings.DoorAlgo.EnsureConnectivity: return Doors_EnsureConnectivity();
            case DungeonSettings.DoorAlgo.SparseLoops:        return Doors_SparseLoops();
            case DungeonSettings.DoorAlgo.ManyLoops:          return Doors_ManyLoops();
            default:                                          return Doors_EnsureConnectivity();
        }
    }

    // ---------- Stage implementations (skeletons to fill) ----------
    IEnumerator Corridors_WanderingMST()
    {
        Debug.Log("▶ Corridors: WanderingMST");
        // 1) lay 'cfg.corridor.spineCount' biased random walks with width 'cfg.corridor.corridorWidth'
        // 2) connect keypoints with MST + add loops with probability cfg.corridor.loopChance
        // 3) write into map.corridors and lock a 1-cell moat if you keep thin walls
        yield return null;
    }
    IEnumerator Corridors_MedialAxis()
    {
        Debug.Log("▶ Corridors: MedialAxis");
        // derive corridors from skeleton of blocked mask; prune branches; width locked
        yield return null;
    }
    IEnumerator Corridors_GridMazes()
    {
        Debug.Log("▶ Corridors: GridMazes");
        // uniform or weighted recursive backtracker / Wilson; keep width = cfg.corridor.corridorWidth
        yield return null;
    }

    IEnumerator Seed_AlongCorridors()
    {
        Debug.Log("▶ Seeding: AlongCorridors");
        // place seeds along corridor sides every cfg.seed.spacing with jitter cfg.seed.jitter
        // alternate left/right by cfg.seed.alternateSides
        yield return null;
    }
    IEnumerator Seed_PoissonAlongCorridors()
    {
        Debug.Log("▶ Seeding: PoissonAlongCorridors");
        // run 1-D Poisson sampling along paths, project seeds to sides
        yield return null;
    }
    IEnumerator Seed_UniformGrid()
    {
        Debug.Log("▶ Seeding: UniformGrid");
        // grid cells at spacing; skip if too near corridors
        yield return null;
    }

    IEnumerator Grow_CreditWavefront()
    {
        Debug.Log("▶ Growth: CreditWavefront");
        // each room gets random credit in [cfg.grow.areaCreditMin..Max]
        // round-robin claimable frontier respecting moat = cfg.grow.wallMoat
        // split if area>cfg.grow.splitArea or aspect>cfg.grow.splitAspect
        yield return null;
    }
    IEnumerator Grow_PressureField()
    {
        Debug.Log("▶ Growth: PressureField");
        // maintain a pressure scalar; rooms expand into lowest-pressure valid neighbor
        yield return null;
    }
    IEnumerator Grow_OrthogonalRays()
    {
        Debug.Log("▶ Growth: OrthogonalRays");
        // extend axis-aligned slabs until 1-cell before collision; merge slabs
        yield return null;
    }

    IEnumerator Scraps_VoronoiFill()
    {
        Debug.Log("▶ Scraps: VoronoiFill");
        // assign leftovers to nearest room with 1-cell peel for walls; tiny islands -> closets
        yield return null;
    }
    IEnumerator Scraps_ClosetsOnly()
    {
        Debug.Log("▶ Scraps: ClosetsOnly");
        // mark small unassigned blobs (<= cfg.scraps.closetMaxArea) as closets; leave others as wall
        yield return null;
    }
    IEnumerator Scraps_NearestRoom()
    {
        Debug.Log("▶ Scraps: NearestRoom");
        // simply flood to nearest room but preserve 1-cell wall between different owners
        yield return null;
    }

    IEnumerator Doors_EnsureConnectivity()
    {
        Debug.Log("▶ Doors: EnsureConnectivity");
        // ensure every room hits a corridor; add minimal doors to connect all components
        yield return null;
    }
    IEnumerator Doors_SparseLoops()
    {
        Debug.Log("▶ Doors: SparseLoops");
        // ensure connectivity + add few room-room doors with far-bias cfg.doors.loopBias
        yield return null;
    }
    IEnumerator Doors_ManyLoops()
    {
        Debug.Log("▶ Doors: ManyLoops");
        // like SparseLoops but add up to cfg.doors.maxRoomToRoomDoors extra room-room doors
        yield return null;
    }

    // tiny loggerger sugar
    //void logger(string s){ logger?.Invoke(s); }
}