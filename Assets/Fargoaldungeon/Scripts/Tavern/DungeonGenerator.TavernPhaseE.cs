using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public partial class DungeonGenerator : MonoBehaviour
{
    [Serializable] public class TavernServiceArtifacts
    {
        public RectInt corridorRect;
        public RectInt kitchenRect;
        public RectInt storageRect;
        public RectInt officeRect;
        public Vector2Int rearExitTile; // outside-facing door
    }

    public TavernServiceArtifacts tavernService;

    public IEnumerator BuildTavernService(List<Room> rooms, TimeTask tm = null)
    {
        if (tavernFootprint == null || tavernZones == null || tavernCommon == null)
        {
            success = false;
            failure = "Tavern Phase E: Needs B (footprint), C (zoning), and D (common) done first.";
            Debug.LogWarning(failure);
            yield break;
        }

        bool createdHere = false;
        if (tm == null) { tm = TimeManager.Instance.BeginTask("Tavern:Service"); createdHere = true; }

        try
        {
            var fp = tavernFootprint.rect;
            var common = tavernZones.commonRect;
            var service = tavernZones.serviceRect;
            var priv = tavernZones.privateRect; // may be empty
            var frontDir = tavernFootprint.frontDir;

            // --- 1) Corridor along Common↔Service boundary (inside Service) ---
            RectInt corridor = ComputeStaffCorridor(common, service, tavern.corridorWidth);
            if (corridor.width == 0 || corridor.height == 0)
            {
                success = false;
                failure = "Tavern Phase E: Failed to carve staff corridor; aborting Phase E.";
                Debug.LogWarning(failure);
                yield break;
            }
            if (tm.IfYield()) yield return null;

            // --- 2) Kitchen adjacent to bar back-door, touching corridor ---
            RectInt kitchen = PlaceKitchenNearBarDoor(service, corridor, tavernCommon.serviceDoor, tavern.kitchenMin);
            if (kitchen.width == 0 || kitchen.height == 0)
            {
                // Try a modest fallback: grab a corner block touching corridor
                kitchen = FallbackKitchen(service, corridor, tavern.kitchenMin);
            }
            if (kitchen.width == 0 || kitchen.height == 0)
            {
                failure="Tavern Phase E: Failed to place kitchen; aborting Phase E.";
                success = false;
                Debug.LogWarning(failure);
                yield break;
            }
            if (tm.IfYield()) yield return null;

            // --- 3) Storage in remaining service area, near rear exterior, plus rear exit ---
            RectInt serviceRemainder = RectMinus(service, corridor, kitchen);
            RectInt storage; Vector2Int rearExit;
            PlaceStorageAndRearExit(fp, frontDir, serviceRemainder, corridor, out storage, out rearExit);
            if (storage.width == 0 || storage.height == 0)
            {
                // tiny fallback: a smaller storage slice off corridor
                storage = SmallStorageFallback(serviceRemainder, corridor, min: tavern.storageMin);
            }
            if (storage.width == 0 || storage.height == 0)
            {
                failure = "Tavern Phase E: Failed to place storage; aborting Phase E.";
                success = false;
                Debug.LogWarning(failure);
                yield break;
            }
            if (tm.IfYield()) yield return null;

            // --- 4) Office: prefer ZonePrivate; else carve a small office off corridor ---
            RectInt office = new RectInt(0, 0, 0, 0);
            if (priv.width >= 3 && priv.height >= 4)
            {
                // Keep office smaller and leave space for owner room later
                office = FitOfficeInPrivate(priv, tavern.officeMin);
            }
            if (office.width == 0) // fallback in service remainder
            {
                office = SmallOfficeOffCorridor(serviceRemainder, corridor, tavern.officeMin);
            }
            if (tm.IfYield()) yield return null;

            // --- Emit rooms (colorFloor included) ---
            var cCorr = RectTiles(corridor);
            rooms.Add(new Room { name = "StaffCorridor", tiles = cCorr, heights = Enumerable.Repeat(cfg.ground_floor_height, cCorr.Count).ToList(), colorFloor = getColor(highlight: false) });

            var cKit = RectTiles(kitchen);
            rooms.Add(new Room { name = "Kitchen", tiles = cKit, heights = Enumerable.Repeat(cfg.ground_floor_height, cKit.Count).ToList(), colorFloor = getColor(highlight: true) });

            var cSto = RectTiles(storage);
            rooms.Add(new Room { name = "Storage", tiles = cSto, heights = Enumerable.Repeat(cfg.ground_floor_height, cSto.Count).ToList(), colorFloor = getColor(highlight: true) });

            if (office.width > 0)
            {
                var cOff = RectTiles(office);
                rooms.Add(new Room { name = "Office", tiles = cOff, heights = Enumerable.Repeat(cfg.ground_floor_height, cOff.Count).ToList(), colorFloor = getColor(highlight: true) });
            }

            if (rearExit != default)
            {
                rooms.Add(new Room { name = "RearExit", tiles = new List<Vector2Int> { rearExit }, heights = new List<int> { cfg.ground_floor_height }, colorFloor = getColor(highlight: true) });
            }

            tavernService = new TavernServiceArtifacts
            {
                corridorRect = corridor,
                kitchenRect = kitchen,
                storageRect = storage,
                officeRect = office,
                rearExitTile = rearExit
            };

            Debug.Log($"Tavern Phase E: corridor={corridor} kitchen={kitchen} storage={storage} office={office} rearExit={rearExit}");
            DrawMapByRooms(rooms);
            success = true;
            if (tm.IfYield()) yield return null;
        }
        finally { if (createdHere) tm.End(); }
    }

    // ================= Helpers =================

    RectInt ComputeStaffCorridor(RectInt common, RectInt service, int width)
    {
        // Corridor runs along the touching edge, inside Service
        // Horizontal touching?
        if (common.yMax == service.yMin)
        {
            // Service is above Common → corridor is the bottom strip of Service
            return new RectInt(service.xMin, service.yMin, service.width, Mathf.Min(width, service.height));
        }
        if (service.yMax == common.yMin)
        {
            // Service is below Common → corridor is the top strip
            return new RectInt(service.xMin, service.yMax - Mathf.Min(width, service.height), service.width, Mathf.Min(width, service.height));
        }

        // Vertical touching?
        if (common.xMax == service.xMin)
        {
            // Service is right of Common → corridor is the left strip of Service
            return new RectInt(service.xMin, service.yMin, Mathf.Min(width, service.width), service.height);
        }
        if (service.xMax == common.xMin)
        {
            // Service is left of Common → corridor is the right strip
            return new RectInt(service.xMax - Mathf.Min(width, service.width), service.yMin, Mathf.Min(width, service.width), service.height);
        }

        return new RectInt(0,0,0,0);
    }

    RectInt PlaceKitchenNearBarDoor(RectInt service, RectInt corridor, Vector2Int svcDoor, Vector2Int minSize)
    {
        // Try to anchor a rectangle that touches both the corridor and the svcDoor tile
        if (svcDoor == default) return new RectInt(0,0,0,0);

        // Candidate sizes (try a few around min)
        var sizes = new[]
        {
            new Vector2Int(minSize.x,     minSize.y),
            new Vector2Int(minSize.x + 1, minSize.y),
            new Vector2Int(minSize.x,     minSize.y + 1)
        };

        foreach (var sz in sizes)
        {
            // Try four orientations around the door
            var rects = new[]
            {
                new RectInt(svcDoor.x - (sz.x-1), svcDoor.y - (sz.y-1), sz.x, sz.y),
                new RectInt(svcDoor.x - (sz.x-1), svcDoor.y,            sz.x, sz.y),
                new RectInt(svcDoor.x,            svcDoor.y - (sz.y-1), sz.x, sz.y),
                new RectInt(svcDoor.x,            svcDoor.y,            sz.x, sz.y)
            };

            foreach (var r0 in rects)
            {
                var r = IntersectRect(r0, service);
                if (r.width < sz.x || r.height < sz.y) continue; // clipped too much
                if (!Touches(r, corridor)) continue;
                return r;
            }
        }

        // As a looser try: expand from svcDoor towards corridor side
        var grow = GrowFrom(service, svcDoor, corridor, minSize);
        return grow;
    }

    RectInt FallbackKitchen(RectInt service, RectInt corridor, Vector2Int minSize)
    {
        // Grab a block that touches corridor (bottom/top/left/right depending on corridor placement)
        // Try corners along the corridor
        var candidates = new List<RectInt>();

        bool corridorHorizontal = corridor.width == service.width;
        if (corridorHorizontal)
        {
            // Corridor is a top or bottom strip → try rectangles immediately above/below it
            bool corridorAtBottom = corridor.yMin == service.yMin;
            int y = corridorAtBottom ? corridor.yMax : service.yMin;
            int maxH = service.height - corridor.height;
            int h = Mathf.Min(minSize.y, maxH);
            int w = Mathf.Min(minSize.x, service.width);

            candidates.Add(new RectInt(service.xMin, y, w, h));
            candidates.Add(new RectInt(service.xMax - w, y, w, h));
        }
        else
        {
            // Corridor is a left or right strip → try rectangles immediately beside it
            bool corridorAtLeft = corridor.xMin == service.xMin;
            int x = corridorAtLeft ? corridor.xMax : service.xMin;
            int maxW = service.width - corridor.width;
            int w = Mathf.Min(minSize.x, maxW);
            int h = Mathf.Min(minSize.y, service.height);

            candidates.Add(new RectInt(x, service.yMin, w, h));
            candidates.Add(new RectInt(x, service.yMax - h, w, h));
        }

        foreach (var r in candidates)
        {
            var rr = IntersectRect(r, service);
            if (rr.width >= minSize.x && rr.height >= minSize.y) return rr;
        }
        return new RectInt(0,0,0,0);
    }

    void PlaceStorageAndRearExit(RectInt footprint, Vector2Int frontDir, RectInt serviceRemainder, RectInt corridor, out RectInt storage, out Vector2Int rearExit)
    {
        storage = new RectInt(0,0,0,0);
        rearExit = default;

        if (serviceRemainder.width <= 0 || serviceRemainder.height <= 0) return;

        // Determine "rear" exterior wall (opposite frontDir)
        Vector2Int rearDir = new Vector2Int(-frontDir.x, -frontDir.y);

        // We want storage to hug the rear wall if possible
        // Build a band 3+ tiles deep along the rear side within serviceRemainder
        RectInt rearBand;
        if (rearDir == new Vector2Int(0, 1))        // rear is north/top
            rearBand = new RectInt(serviceRemainder.xMin, serviceRemainder.yMax - Mathf.Max(tavern.rearBandMinDepth, serviceRemainder.height/3), serviceRemainder.width, Mathf.Max(3, serviceRemainder.height/3));
        else if (rearDir == new Vector2Int(0, -1))  // rear is south/bottom
            rearBand = new RectInt(serviceRemainder.xMin, serviceRemainder.yMin, serviceRemainder.width, Mathf.Max(tavern.rearBandMinDepth, serviceRemainder.height/3));
        else if (rearDir == new Vector2Int(1, 0))   // rear is east/right
            rearBand = new RectInt(serviceRemainder.xMax - Mathf.Max(3, serviceRemainder.width/3), serviceRemainder.yMin, Mathf.Max(tavern.rearBandMinDepth, serviceRemainder.width/3), serviceRemainder.height);
        else                                        // rear is west/left
            rearBand = new RectInt(serviceRemainder.xMin, serviceRemainder.yMin, Mathf.Max(tavern.rearBandMinDepth, serviceRemainder.width/3), serviceRemainder.height);

        rearBand = IntersectRect(rearBand, serviceRemainder);

        // Prefer a chunk that also touches corridor (short staff path)
        RectInt prefer = IntersectRect(rearBand, ExpandRect(corridor, tavern.corridorAdjacencyBuffer));
        RectInt pick = prefer.width > 0 ? prefer : rearBand;

        // Pick a solid rectangle inside 'pick'
        storage = new RectInt(pick.xMin, pick.yMin, Mathf.Max(tavern.storageMin.x, pick.width/2), Mathf.Max(tavern.storageMin.y, pick.height/2));
        storage = IntersectRect(storage, pick);

        if (storage.width < tavern.storageMin.x || storage.height < tavern.storageMin.y)
        {
            // Try full band slice
            storage = pick;
        }

        // Rear exit: choose a perimeter tile of storage that faces OUTSIDE the footprint in rearDir
        var perim = Outline(storage).ToList();
        foreach (var p in perim.OrderBy(_ => UnityEngine.Random.value))
        {
            var q = p + rearDir; // step outward
            if (!InsideRect(q, footprint))
            {
                rearExit = p; // door sits on storage perimeter cell
                break;
            }
        }
    }

    RectInt SmallStorageFallback(RectInt serviceRemainder, RectInt corridor, Vector2Int min)
    {
        // Try a modest rectangle adjacent to the corridor
        bool corridorHorizontal = corridor.width == serviceRemainder.width;
        if (corridorHorizontal)
        {
            bool corridorAtBottom = corridor.yMin == serviceRemainder.yMin;
            int y = corridorAtBottom ? corridor.yMax : serviceRemainder.yMin;
            int maxH = serviceRemainder.height - corridor.height;
            int h = Mathf.Clamp(min.y, min.y, maxH);
            int w = Mathf.Clamp(min.x, min.x, serviceRemainder.width);
            return IntersectRect(new RectInt(serviceRemainder.xMin, y, w, h), serviceRemainder);
        }
        else
        {
            bool corridorAtLeft = corridor.xMin == serviceRemainder.xMin;
            int x = corridorAtLeft ? corridor.xMax : serviceRemainder.xMin;
            int maxW = serviceRemainder.width - corridor.width;
            int w = Mathf.Clamp(min.x, min.x, maxW);
            int h = Mathf.Clamp(min.y, min.y, serviceRemainder.height);
            return IntersectRect(new RectInt(x, serviceRemainder.yMin, w, h), serviceRemainder);
        }
    }

RectInt FitOfficeInPrivate(RectInt priv, Vector2Int minSize)
{
    if (priv.width < minSize.x || priv.height < minSize.y) return new RectInt(0,0,0,0);

    // Aim around half-width/height, but at least minSize
    int w = Mathf.Clamp(Mathf.Max(minSize.x, priv.width / 2 + 1), minSize.x, priv.width);
    int h = Mathf.Clamp(Mathf.Max(minSize.y, 4),                minSize.y, priv.height);

    // Prefer near service side (keep simple: lower-left of private for now)
    var office = new RectInt(priv.xMin, priv.yMin, w, h);
    office = IntersectRect(office, priv);
    if (office.width < minSize.x || office.height < minSize.y) return new RectInt(0,0,0,0);
    return office;
}

RectInt SmallOfficeOffCorridor(RectInt serviceRemainder, RectInt corridor, Vector2Int minSize)
{
    if (serviceRemainder.width <= 0 || serviceRemainder.height <= 0) return new RectInt(0,0,0,0);

    int w = Mathf.Clamp(minSize.x, minSize.x, serviceRemainder.width);
    int h = Mathf.Clamp(minSize.y, minSize.y, serviceRemainder.height);

    bool corridorHorizontal = corridor.width == serviceRemainder.width;
    if (corridorHorizontal)
    {
        bool corridorAtBottom = corridor.yMin == serviceRemainder.yMin;
        int y = corridorAtBottom ? corridor.yMax : serviceRemainder.yMin;
        return IntersectRect(new RectInt((int)serviceRemainder.center.x - w/2, y, w, h), serviceRemainder);
    }
    else
    {
        bool corridorAtLeft = corridor.xMin == serviceRemainder.xMin;
        int x = corridorAtLeft ? corridor.xMax : serviceRemainder.xMin;
        return IntersectRect(new RectInt(x, (int)serviceRemainder.center.y - h/2, w, h), serviceRemainder);
    }
}

    // ---- tiny geometry helpers used above ----

    bool Touches(RectInt a, RectInt b)
    {
        // share an edge (not just corner)
        bool verticalTouch =
            (a.xMax == b.xMin || b.xMax == a.xMin) &&
            !(a.yMax <= b.yMin || b.yMax <= a.yMin);
        bool horizontalTouch =
            (a.yMax == b.yMin || b.yMax == a.yMin) &&
            !(a.xMax <= b.xMin || b.xMax <= a.xMin);
        return verticalTouch || horizontalTouch;
    }

    RectInt GrowFrom(RectInt bounds, Vector2Int seed, RectInt towards, Vector2Int minSize)
    {
        // Greedy grow around 'seed' while staying inside 'bounds', prefer touching 'towards'
        int maxGrow = Mathf.Max(0, tavern.maxGrow);
        for (int sx = minSize.x; sx <= Mathf.Min(bounds.width,  minSize.x + maxGrow); sx++)
        {
            for (int sy = minSize.y; sy <= Mathf.Min(bounds.height, minSize.y + maxGrow); sy++)
            {
                var r = new RectInt(seed.x - sx/2, seed.y - sy/2, sx, sy);
                r = IntersectRect(r, bounds);
                if (r.width >= minSize.x && r.height >= minSize.y && Touches(r, towards))
                    return r;
            }
        }
        return new RectInt(0,0,0,0);
    }

    RectInt ExpandRect(RectInt r, int by)
    {
        return new RectInt(r.xMin - by, r.yMin - by, r.width + by*2, r.height + by*2);
    }

    // Uses existing helpers from earlier partials:
    // - IntersectRect(RectInt, RectInt)
    // - RectMinus(RectInt, params RectInt[])
    // - Outline(RectInt)
    // - InsideRect(Vector2Int, RectInt)
    // - RectTiles(RectInt)
}