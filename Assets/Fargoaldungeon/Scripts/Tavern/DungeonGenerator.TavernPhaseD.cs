using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public partial class DungeonGenerator : MonoBehaviour
{
    [Serializable] public class TavernCommonArtifacts
    {
        public RectInt barRect;          // inside Common, along boundary to Service
        public Vector2Int barDoor;       // tile in Common where staff slips into Service
        public Vector2Int serviceDoor;   // adjacent tile in Service
        public RectInt hearthRect;       // small chunk on an exterior wall (optional)
        public RectInt stageRect;        // optional platform
        public List<RectInt> boothRects; // optional alcoves
    }

    public TavernCommonArtifacts tavernCommon;

    public IEnumerator BuildTavernCommon(List<Room> rooms, TimeTask tm = null)
    {

        if (tavernFootprint == null || tavernZones == null)
        {
            ca.success = false;
            ca.failure = "Tavern Phase D: Needs Phase B and C done first.";
            Debug.LogWarning(ca.failure);
            yield break;
        }

        bool createdHere = false;
        if (tm == null) { tm = TimeManager.Instance.BeginTask("Tavern:Common"); createdHere = true; }

        try
        {
            var fp      = tavernFootprint.rect;
            var common  = tavernZones.commonRect;
            var service = tavernZones.serviceRect;

            tilemap.ClearAllTiles();
            rooms.Clear();
            map = new byte[cfg.mapWidth, cfg.mapHeight];

            // 1) Find the Common↔Service shared boundary (prefer this as bar wall)
            if (!RectsTouch(common, service))
            {
                Debug.LogWarning("Tavern Phase D: Common and Service do not touch; bar will fall back to a front/long wall.");
            }

            // Compute bar rectangle along shared edge when possible, otherwise along the longest common wall
            RectInt bar = ComputeBarRect(common, service, tavern.barDepth);

            // If bar failed (too thin overlap), fall back to a long interior wall on the front side
            if (bar.width == 0 || bar.height == 0)
            {
                bar = FallbackBarRect(common, tavernFootprint.frontDir, tavern.barDepth);
            }

            // Clamp bar minimums for readability (configurable)
            if (bar.width < tavern.barMinVisualWidth && bar.height >= tavern.barMinVisualHeight)
                bar.width = Mathf.Min(tavern.barMinVisualWidth, common.width);

            if (bar.height < tavern.barMinVisualHeight)
                bar.height = Mathf.Min(Mathf.Max(tavern.barMinVisualHeight, 1), common.height);
                
            bar = IntersectRect(bar, common);
            if (bar.width <= 0 || bar.height <= 0)
            {
                ca.success = false;
                ca.failure = "Tavern Phase D: Failed to place bar—common too small. Aborting Phase D.";
                Debug.LogWarning(ca.failure);
                yield break;
            }

            // 2) Back-door: pick a center point along the bar wall that touches Service, create a door pair
            Vector2Int barDoor, svcDoor;
            if (!TryPlaceBarDoor(bar, common, service, out barDoor, out svcDoor))
            {
                // If we can't find a touching spot (fallback bar), create a short 1-tile corridor “nudge” into service
                TryPunchTinyConnector(common, service, ref bar, out barDoor, out svcDoor);
            }

            // 3) Hearth on an exterior wall not used by the bar
            RectInt hearth = ComputeHearthRect(fp, common, bar, size: tavern.hearthSize);

            // 4) Stage (optional): opposite side of the bar if space allows
            RectInt stage = new RectInt(0,0,0,0);
            if (tavern.features.hasStage)
                stage = ComputeStageRect(common, bar, desired: tavern.stageSize);

            // 5) Booths (optional): 2–4 small alcoves (2×3) on remaining walls
            var booths = new List<RectInt>();
            if (tavern.features.hasBooths)
                booths = ComputeBooths(common, bar, stage, hearth, maxCount: tavern.maxBooths);
                
            // 6) Emit rooms: BarCounter, CommonRoom (after subtracting bar/stage/booths/hearth), Door markers
            var barTiles    = RectTiles(bar);
            var hearthTiles = RectTiles(hearth);
            var stageTiles  = RectTiles(stage);
            var boothTiles  = booths.SelectMany(RectTiles).ToList();

            // Common final = common minus all reserved shapes
            var carve = new List<RectInt> { bar, hearth, stage };
            carve.AddRange(booths);


            // FIXED: Now removes the tiles from the big room where small rooms overlap
            //var commonFinalRect = RectMinus(common, carve.ToArray());
            //var commonTiles = RectTiles(commonFinalRect);
            var commonTiles = RectTiles(common);
            commonTiles = TilesMinusRects(commonTiles, carve.ToArray());

            rooms.Add(new Room { name = "BarCounter", tiles = barTiles, heights = Enumerable.Repeat(cfg.ground_floor_height, barTiles.Count).ToList(), colorFloor = ca.getColor(highlight: true) });
            if (hearth.width > 0)
            {
                rooms.Add(new Room { name = "HearthZone", tiles = hearthTiles, heights = Enumerable.Repeat(cfg.ground_floor_height, hearthTiles.Count).ToList(), colorFloor = ca.getColor(highlight: false) });
            }
            if (stage.width > 0)
            {
                rooms.Add(new Room { name = "StageZone", tiles = stageTiles, heights = Enumerable.Repeat(cfg.ground_floor_height, stageTiles.Count).ToList(), colorFloor = ca.getColor(highlight: true) });
            }
            if (boothTiles.Count > 0)
                rooms.Add(new Room { name = "BoothAlcoves", tiles = boothTiles, heights = Enumerable.Repeat(cfg.ground_floor_height, boothTiles.Count).ToList(), colorFloor = ca.getColor(highlight: false) });

            rooms.Add(new Room { name = "CommonRoom", tiles = commonTiles, heights = Enumerable.Repeat(cfg.ground_floor_height, commonTiles.Count).ToList(), colorFloor = ca.getColor(highlight: true) });

            // Visualize a door pair as single-tile “rooms” (optional but handy for debugging flow)
            if (barDoor != default && svcDoor != default)
            {
                rooms.Add(new Room { name = "BarDoor_Common", tiles = new List<Vector2Int> { barDoor }, heights = new List<int> { cfg.ground_floor_height }, colorFloor = ca.getColor(highlight: false) });
                rooms.Add(new Room { name = "BarDoor_Service", tiles = new List<Vector2Int> { svcDoor }, heights = new List<int> { cfg.ground_floor_height }, colorFloor = ca.getColor(highlight: false) });
            }

            // Save artifacts for Phase E
            tavernCommon = new TavernCommonArtifacts
            {
                barRect   = bar,
                barDoor   = barDoor,
                serviceDoor = svcDoor,
                hearthRect  = hearth,
                stageRect   = stage,
                boothRects  = booths
            };

            Debug.Log($"Tavern Phase D: bar={bar} door={barDoor}->{svcDoor} hearth={hearth} stage={stage} booths={booths.Count}");
            DrawMapByRooms(rooms);
            ca.success = true;
            if (tm.IfYield()) yield return null;
        }
        finally { if (createdHere) tm.End(); }
    }

    // ---------- Compute pieces ----------

    bool RectsTouch(RectInt a, RectInt b)
    {
        bool verticalTouch =
            (a.xMax == b.xMin || b.xMax == a.xMin) &&
            !(a.yMax <= b.yMin || b.yMax <= a.yMin); // vertical overlap
        bool horizontalTouch =
            (a.yMax == b.yMin || b.yMax == a.yMin) &&
            !(a.xMax <= b.xMin || b.xMax <= a.xMin); // horizontal overlap;
        return verticalTouch || horizontalTouch;
    }

    RectInt ComputeBarRect(RectInt common, RectInt service, int depth)
    {
        // Prefer along their shared edge
        // Horizontal boundary (service above or below)
        if (common.yMax == service.yMin || service.yMax == common.yMin)
        {
            int y = (common.yMax == service.yMin) ? (common.yMax - depth) : common.yMin; // bar inside Common
            int x0 = Mathf.Max(common.xMin, service.xMin);
            int x1 = Mathf.Min(common.xMax, service.xMax);
            int w = Mathf.Max(0, x1 - x0);
            if (w > 0) return new RectInt(x0, Mathf.Clamp(y, common.yMin, common.yMax - depth), w, Mathf.Min(depth, common.height));
        }

        // Vertical boundary (service left or right)
        if (common.xMax == service.xMin || service.xMax == common.xMin)
        {
            int x = (common.xMax == service.xMin) ? (common.xMax - depth) : common.xMin;
            int y0 = Mathf.Max(common.yMin, service.yMin);
            int y1 = Mathf.Min(common.yMax, service.yMax);
            int h = Mathf.Max(0, y1 - y0);
            if (h > 0) return new RectInt(Mathf.Clamp(x, common.xMin, common.xMax - depth), y0, Mathf.Min(depth, common.width), h);
        }

        return new RectInt(0,0,0,0); // no touch → caller will fallback
    }

    RectInt FallbackBarRect(RectInt common, Vector2Int frontDir, int depth)
    {
        // Put bar along the wall opposite the entrance (so players “see” it)
        if (frontDir == new Vector2Int(0, 1))   // front at bottom → bar at back wall
            return new RectInt(common.xMin + 1, common.yMax - depth, Mathf.Max(3, common.width - 2), Mathf.Min(depth, common.height));
        if (frontDir == new Vector2Int(0,-1))   // front at top → bar at bottom wall
            return new RectInt(common.xMin + 1, common.yMin, Mathf.Max(3, common.width - 2), Mathf.Min(depth, common.height));
        if (frontDir == new Vector2Int(1, 0))   // front at left → bar at right wall
            return new RectInt(common.xMax - depth, common.yMin + 1, Mathf.Min(depth, common.width), Mathf.Max(3, common.height - 2));
        // front west → bar at left wall
        return new RectInt(common.xMin, common.yMin + 1, Mathf.Min(depth, common.width), Mathf.Max(3, common.height - 2));
    }

    bool TryPlaceBarDoor(RectInt bar, RectInt common, RectInt service, out Vector2Int barDoor, out Vector2Int svcDoor)
    {
        barDoor = default; svcDoor = default;
        // Check four sides of bar to see if a neighbor is in Service
        // Prefer the side that actually touches Service
        var candidates = new List<(Vector2Int c, Vector2Int s)>();

        // For each tile along bar’s outer edge, test the adjacent toward Service
        foreach (var p in Outline(bar))
        {
            foreach (var n in Neigh4(p))
            {
                if (!InsideRect(n, common) && !InsideRect(n, service)) continue;

                bool inCommon  = InsideRect(p, common);
                bool intoServ  = InsideRect(n, service);
                if (inCommon && intoServ)
                    candidates.Add((p, n));
            }
        }

        if (candidates.Count == 0) return false;

        var pick = candidates[UnityEngine.Random.Range(0, candidates.Count)];
        barDoor = pick.c; svcDoor = pick.s;
        return true;
    }

    void TryPunchTinyConnector(RectInt common, RectInt service, ref RectInt bar, out Vector2Int barDoor, out Vector2Int svcDoor)
    {
        // Pick closest points between bar and service and designate a door pair
        barDoor = default; svcDoor = default;

        Vector2Int bestC = default, bestS = default;
        float bestD = float.MaxValue;

        foreach (var c in RectTiles(bar))
        foreach (var s in RectTiles(service))
        {
            float d = (c - s).sqrMagnitude;
            if (d < bestD)
            {
                bestD = d; bestC = c; bestS = s;
            }
        }

        if (bestD < float.MaxValue)
        {
            barDoor = bestC;
            svcDoor = bestS;
        }
    }

    RectInt ComputeHearthRect(RectInt footprint, RectInt common, RectInt bar, Vector2Int size)
    {
        // Choose an exterior wall segment of Common not overlapping the bar rectangle.
        // Prefer opposite or adjacent wall to the bar.
        var walls = ExteriorWalls(common, footprint);
        // Remove any wall cells overlapping bar
        walls.RemoveAll(p => InsideRect(p, bar));

        if (walls.Count == 0) return new RectInt(0,0,0,0);

        // Try to place a small 2×1 (or 2×2) along a wall
        // Pick a random wall cell and align inward
        for (int tries = 0; tries < 64; tries++)
        {
            var edge = walls[UnityEngine.Random.Range(0, walls.Count)];
            // Determine normal: which side is outside footprint
            Vector2Int normal = EdgeNormal(edge, common, footprint);
            // Place hearth just inside Common
            var origin = edge + (-normal); // one step inside
            var rect = NormalAlignedRect(origin, normal, size);
            rect = IntersectRect(rect, common);
            if (rect.width > 0 && rect.height > 0 && !RectsOverlap(rect, bar))
                return rect;
        }

        return new RectInt(0,0,0,0);
    }

    RectInt ComputeStageRect(RectInt common, RectInt bar, Vector2Int desired)
    {
        // Opposite bar side if possible
        // If bar is horizontal (wide), stage goes along the opposite horizontal wall; if vertical, opposite vertical wall.
        bool barHorizontal = bar.width >= bar.height;
        if (barHorizontal)
        {
            // Bar likely at top/bottom—stage at the other
            int y = (bar.yMin <= common.center.y) ? (common.yMax - desired.y) : common.yMin;
            var rect = new RectInt(common.xMin + 2, Mathf.Clamp(y, common.yMin, common.yMax - desired.y),
                                   Mathf.Max(3, Mathf.Min(desired.x, common.width - 4)), desired.y);
            rect = IntersectRect(rect, common);
            if (!RectsOverlap(rect, bar)) return rect;
        }
        else
        {
            // Bar likely at left/right—stage opposite
            int x = (bar.xMin <= common.center.x) ? (common.xMax - desired.x) : common.xMin;
            var rect = new RectInt(Mathf.Clamp(x, common.xMin, common.xMax - desired.x), common.yMin + 2,
                                   desired.x, Mathf.Max(2, Mathf.Min(desired.y, common.height - 4)));
            rect = IntersectRect(rect, common);
            if (!RectsOverlap(rect, bar)) return rect;
        }
        return new RectInt(0,0,0,0);
    }

    List<RectInt> ComputeBooths(RectInt common, RectInt bar, RectInt stage, RectInt hearth, int maxCount)
    {
        var list = new List<RectInt>();
        var used = new List<RectInt> { bar, stage, hearth };

        // Try along the two long walls not occupied by bar
        // Booth size ~ 2×3 (depth into room)
        Vector2Int booth = tavern.boothSize;

        for (int tries = 0; tries < 40 && list.Count < maxCount; tries++)
        {
            // Pick a wall: top, bottom, left, right (prefer not the bar side)
            int side = UnityEngine.Random.Range(0, 4);
            RectInt rect;
            switch (side)
            {
                case 0: // top wall
                    rect = new RectInt(common.xMin + 2 + UnityEngine.Random.Range(0, Mathf.Max(1, common.width - booth.x - 4)),
                                       common.yMax - booth.y, booth.x, booth.y);
                    break;
                case 1: // bottom wall
                    rect = new RectInt(common.xMin + 2 + UnityEngine.Random.Range(0, Mathf.Max(1, common.width - booth.x - 4)),
                                       common.yMin, booth.x, booth.y);
                    break;
                case 2: // left wall
                    rect = new RectInt(common.xMin, common.yMin + 2 + UnityEngine.Random.Range(0, Mathf.Max(1, common.height - booth.y - 4)),
                                       booth.y, booth.x); // rotate
                    break;
                default: // right wall
                    rect = new RectInt(common.xMax - booth.y, common.yMin + 2 + UnityEngine.Random.Range(0, Mathf.Max(1, common.height - booth.x - 4)),
                                       booth.y, booth.x); // rotate
                    break;
            }
            rect = IntersectRect(rect, common);
            if (rect.width <= 0 || rect.height <= 0) continue;
            if (used.Any(u => RectsOverlap(rect, u))) continue;
            used.Add(rect);
            list.Add(rect);
        }

        return list;
    }

    // ---------- Utility ----------

    IEnumerable<Vector2Int> Outline(RectInt r)
    {
        for (int x = r.xMin; x < r.xMax; x++) { yield return new Vector2Int(x, r.yMin); }
        for (int x = r.xMin; x < r.xMax; x++) { yield return new Vector2Int(x, r.yMax - 1); }
        for (int y = r.yMin; y < r.yMax; y++) { yield return new Vector2Int(r.xMin, y); }
        for (int y = r.yMin; y < r.yMax; y++) { yield return new Vector2Int(r.xMax - 1, y); }
    }

    IEnumerable<Vector2Int> Neigh4(Vector2Int p)
    {
        yield return new Vector2Int(p.x + 1, p.y);
        yield return new Vector2Int(p.x - 1, p.y);
        yield return new Vector2Int(p.x, p.y + 1);
        yield return new Vector2Int(p.x, p.y - 1);
    }

    bool InsideRect(Vector2Int p, RectInt r)
    {
        return p.x >= r.xMin && p.x < r.xMax && p.y >= r.yMin && p.y < r.yMax;
    }

    bool RectsOverlap(RectInt a, RectInt b)
    {
        return !(a.xMax <= b.xMin || b.xMax <= a.xMin || a.yMax <= b.yMin || b.yMax <= a.yMin);
    }

    Vector2Int EdgeNormal(Vector2Int edgeCell, RectInt r, RectInt footprint)
    {
        // Return outward normal (pointing to outside the footprint), then we'll invert for inside placement
        // Check which neighbor is outside the footprint
        var n = new[] { new Vector2Int(1,0), new Vector2Int(-1,0), new Vector2Int(0,1), new Vector2Int(0,-1) };
        foreach (var d in n)
        {
            var q = edgeCell + d;
            bool inFoot = InsideRect(q, footprint);
            bool inRoom = InsideRect(q, r);
            if (inFoot && !inRoom) return d; // moving this way leaves the room (is outward)
        }
        return new Vector2Int(0, -1);
    }

    RectInt NormalAlignedRect(Vector2Int originInside, Vector2Int inwardNormal, Vector2Int size)
    {
        // Build a rect of given size whose "depth" extends along inwardNormal
        if (inwardNormal == new Vector2Int(0, 1))     // inward is +Y, so rect extends upward from origin
            return new RectInt(originInside.x, originInside.y, size.x, size.y);
        if (inwardNormal == new Vector2Int(0, -1))    // inward is -Y, rect extends downward ending at origin
            return new RectInt(originInside.x, originInside.y - (size.y - 1), size.x, size.y);
        if (inwardNormal == new Vector2Int(1, 0))     // inward is +X
            return new RectInt(originInside.x, originInside.y, size.y, size.x); // rotate
        // inward is -X
        return new RectInt(originInside.x - (size.y - 1), originInside.y, size.y, size.x); // rotate
    }

    List<Vector2Int> ExteriorWalls(RectInt room, RectInt footprint)
    {
        // Perimeter cells of room that also sit on footprint boundary (exterior)
        var ext = new List<Vector2Int>();
        foreach (var p in Outline(room))
        {
            // A perimeter cell is exterior if stepping outward leaves the footprint
            foreach (var d in Neigh4(p))
            {
                var q = p + d;
                if (!InsideRect(q, footprint))
                {
                    ext.Add(p);
                    break;
                }
            }
        }
        return ext.Distinct().ToList();
    }

    List<Vector2Int> TilesMinusRects(List<Vector2Int> whole, params RectInt[] cuts)
    {
        foreach (var c in cuts)
        {
            for (int y = c.yMin; y < c.yMax; y++)
                for (int x = c.xMin; x < c.xMax; x++)
                    if (whole.Contains(new Vector2Int(x, y)))
                        whole.Remove(new Vector2Int(x, y));
        }
        return whole;
    }
    RectInt RectMinus(RectInt whole, params RectInt[] cuts)
    {
        // Simple heuristic: shrink “whole” inward from any side fully covered by a cut.
        RectInt r = whole;
        foreach (var c in cuts)
        {
            if (c.width == 0 || c.height == 0) continue;
            if (c.yMin <= r.yMin && c.yMax >= r.yMax)
            {
                if (c.xMin <= r.xMin) r.xMin = Mathf.Min(r.xMax, c.xMax);
                if (c.xMax >= r.xMax) r.xMax = Mathf.Max(r.xMin, c.xMin);
            }
            if (c.xMin <= r.xMin && c.xMax >= r.xMax)
            {
                if (c.yMin <= r.yMin) r.yMin = Mathf.Min(r.yMax, c.yMax);
                if (c.yMax >= r.yMax) r.yMax = Mathf.Max(r.yMin, c.yMin);
            }
        }
        if (r.width <= 0 || r.height <= 0) return new RectInt((int)whole.center.x, (int)whole.center.y, 0, 0);
        return r;
    }
}