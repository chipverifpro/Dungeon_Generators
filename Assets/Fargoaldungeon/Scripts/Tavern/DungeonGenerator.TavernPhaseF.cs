using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public partial class DungeonGenerator : MonoBehaviour
{
    [Serializable] public class TavernVerticalArtifacts
    {
        public Vector2Int stairsDown;   // tile in Kitchen/Storage
        public Vector2Int stairsUp;     // tile in Common
        public RectInt    upstairsHall; // level-2 hallway
        public List<RectInt> upstairsBedrooms;
        public List<RectInt> upstairsWCs;
    }

    public TavernVerticalArtifacts tavernVertical;

    public IEnumerator BuildTavernStairsAndUpper(List<Room> rooms, TimeTask tm = null)
    {
        if (tavernFootprint == null || tavernZones == null || tavernCommon == null || tavernService == null)
        {
            failure="Tavern Phase F: Needs B (footprint), C (zoning), D (common), and E (service) completed.";
            success = false;
            Debug.LogWarning(failure);
            yield break;
        }

        bool createdHere = false;
        if (tm == null) { tm = TimeManager.Instance.BeginTask("Tavern:Vertical"); createdHere = true; }

        try
        {
            var fp = tavernFootprint.rect;
            var common = GetRoomRect("CommonRoom", tavernZones.commonRect);   // prefer actual carved if available
            var kitchenR = tavernService.kitchenRect;
            var storageR = tavernService.storageRect;
            var corridor = tavernService.corridorRect;
            var frontDir = tavernFootprint.frontDir;
            var barRect = tavernCommon.barRect;

            //tilemap.ClearAllTiles();
            //rooms.Clear();
            //map = new byte[cfg.mapWidth, cfg.mapHeight];

            // ========= 1) STAIRS DOWN: Kitchen preferred, Storage fallback =========
            Vector2Int stairsDown = default;

            if (tavern.stairs.down)
            {
                // Try kitchen first: near corridor edge if possible
                if (!TryPlaceSingleTileNearEdge(kitchenR, corridor, out stairsDown))
                {
                    // Fallback in kitchen anywhere
                    stairsDown = SafeCenterTile(kitchenR);
                }

                // If kitchen failed (too small), try storage near corridor
                if (stairsDown == default || !InsideRect(stairsDown, kitchenR))
                {
                    if (!TryPlaceSingleTileNearEdge(storageR, corridor, out stairsDown))
                        stairsDown = SafeCenterTile(storageR);
                }

                if (stairsDown != default)
                {
                    rooms.Add(new Room
                    {
                        name = "StairsDown",
                        tiles = new List<Vector2Int> { stairsDown },
                        heights = new List<int> { cfg.next_floor_height },
                        colorFloor = getColor(highlight: true)
                    });
                }
            }

            if (tm.IfYield()) yield return null;

            // ========= 2) STAIRS UP: Common near Bar preferred, otherwise near front =========
            Vector2Int stairsUp = default;

            if (tavern.stairs.up)
            {
                // Try adjacent to bar (but still inside Common)
                if (!TryPlaceAdjacentInside(common, barRect, out stairsUp))
                {
                    // Try along the front side of the common (visible from entrance)
                    stairsUp = PlaceAlongFrontWall(common, frontDir);
                }

                if (stairsUp == default) stairsUp = SafeCenterTile(common);

                rooms.Add(new Room
                {
                    name = "StairsUp",
                    tiles = new List<Vector2Int> { stairsUp },
                    heights = new List<int> { cfg.ground_floor_height },
                    colorFloor = getColor(highlight: true)
                });
            }

            if (tm.IfYield()) yield return null;

            // ========= 3) UPSTAIRS: Hall + bedrooms (+WC) =========
            // Simple, readable scaffold: a central hall and rooms on both sides.
            RectInt upFoot = fp; // align to footprint; you can inset if you want balconies later

            // Decide hall orientation based on footprint aspect (long axis hall)
            bool hallHorizontal = upFoot.width >= upFoot.height;
            RectInt upHall;

            if (hallHorizontal)
            {
                int hallY = (int)upFoot.center.y - tavern.hallWidth / 2;
                hallY = Mathf.Clamp(hallY, upFoot.yMin, upFoot.yMax - tavern.hallWidth);
                upHall = new RectInt(upFoot.xMin + 1, hallY, upFoot.width - 2, Mathf.Max(2, tavern.hallWidth));
            }
            else
            {
                int hallX = (int)upFoot.center.x - tavern.hallWidth / 2;
                hallX = Mathf.Clamp(hallX, upFoot.xMin, upFoot.xMax - tavern.hallWidth);
                upHall = new RectInt(hallX, upFoot.yMin + 1, Mathf.Max(2, tavern.hallWidth), upFoot.height - 2);
            }

            // Align stairsUp to hall by ensuring a tile in the hall near stairsUp
            // (purely visual here; your pathing will handle the rest)
            // If the stairsUp is not inside the hall, we don't force-move; this is a coarse scaffold.

            // Partition both sides into bedrooms
            var bedrooms = new List<RectInt>();
            var wcs = new List<RectInt>();

            // Minimum bedroom size & desired room count (from config)
            Vector2Int bedMin = tavern.bedroomMinSize;
            int desiredRooms = Mathf.Clamp((upFoot.width * upFoot.height) / 80, tavern.minBedrooms, tavern.maxBedrooms);

            if (hallHorizontal)
            {
                // Upper band above hall
                RectInt bandTop = new RectInt(upFoot.xMin + 1, upHall.yMax, upFoot.width - 2, upFoot.yMax - upHall.yMax - 1);
                // Lower band below hall
                RectInt bandBot = new RectInt(upFoot.xMin + 1, upFoot.yMin + 1, upFoot.width - 2, upHall.yMin - (upFoot.yMin + 1));

                SliceBandIntoRooms(bandTop, horizontal: true, minSize: bedMin.x, out var topRooms);
                SliceBandIntoRooms(bandBot, horizontal: true, minSize: bedMin.x, out var botRooms);

                bedrooms.AddRange(topRooms);
                bedrooms.AddRange(botRooms);

                // WC (optional): a small 2x2 carved near the hall midpoint on one side
                var wc = MakeSmallWCNearHall(upHall, upFoot, preferTop: true, wcSize: tavern.wcSize);
                if (wc.width > 0) wcs.Add(wc);
            }
            else
            {
                // Left band
                RectInt bandLeft = new RectInt(upFoot.xMin + 1, upFoot.yMin + 1, upHall.xMin - (upFoot.xMin + 1), upFoot.height - 2);
                // Right band
                RectInt bandRight = new RectInt(upHall.xMax, upFoot.yMin + 1, upFoot.xMax - upHall.xMax - 1, upFoot.height - 2);

                SliceBandIntoRooms(bandLeft, horizontal: false, minSize: bedMin.y, out var leftRooms);
                SliceBandIntoRooms(bandRight, horizontal: false, minSize: bedMin.y, out var rightRooms);

                bedrooms.AddRange(leftRooms);
                bedrooms.AddRange(rightRooms);

                var wc = MakeSmallWCNearHall(upHall, upFoot, preferTop: false, wcSize: tavern.wcSize);
                if (wc.width > 0) wcs.Add(wc);
            }

            // Trim to desiredRooms (keep larger ones first)
            bedrooms = bedrooms
                .Where(r => r.width >= bedMin.x && r.height >= bedMin.y)
                .OrderByDescending(r => r.width * r.height)
                .Take(desiredRooms)
                .ToList();

            // Emit upstairs rooms (use distinct names; heights kept 0 unless your builder reads it as floor level)
            var hallT = RectTiles(upHall);
            rooms.Add(new Room { name = "UpstairsHall", tiles = hallT, heights = Enumerable.Repeat(cfg.next_floor_height, hallT.Count).ToList(), colorFloor = getColor(highlight: false) });

            for (int i = 0; i < bedrooms.Count; i++)
            {
                var bt = RectTiles(bedrooms[i]);
                rooms.Add(new Room { name = $"Bedroom_{i + 1}", tiles = bt, heights = Enumerable.Repeat(cfg.next_floor_height, bt.Count).ToList(), colorFloor = getColor(highlight: true) });
            }

            foreach (var wc in wcs)
            {
                var wt = RectTiles(wc);
                rooms.Add(new Room { name = "WC", tiles = wt, heights = Enumerable.Repeat(cfg.next_floor_height, wt.Count).ToList(), colorFloor = getColor(highlight: true) });
            }

            tavernVertical = new TavernVerticalArtifacts
            {
                stairsDown = stairsDown,
                stairsUp = stairsUp,
                upstairsHall = upHall,
                upstairsBedrooms = bedrooms,
                upstairsWCs = wcs
            };

            Debug.Log($"Tavern Phase F: stairsDown={stairsDown} stairsUp={stairsUp} upHall={upHall} bedrooms={bedrooms.Count} wc={wcs.Count}");
            DrawMapByRooms(rooms);
            success = true;
        }
        finally { if (createdHere) tm.End(); }
    }

    // ===================== Helpers =====================

    // Try place a tile inside 'room' that is adjacent (shares an edge) to 'edgeRect'
    bool TryPlaceSingleTileNearEdge(RectInt room, RectInt edgeRect, out Vector2Int tile)
    {
        tile = default;
        if (room.width <= 0 || room.height <= 0 || edgeRect.width <= 0 || edgeRect.height <= 0) return false;

        // Collect border tiles of room that touch edgeRect
        var choices = new List<Vector2Int>();
        foreach (var p in Outline(room))
        {
            foreach (var n in Neigh4(p))
            {
                if (InsideRect(n, edgeRect)) { choices.Add(p); break; }
            }
        }

        if (choices.Count == 0) return false;
        tile = choices[UnityEngine.Random.Range(0, choices.Count)];
        return true;
    }

    // Place a tile in 'inside' next to (sharing an edge with) 'adjacentTo'
    bool TryPlaceAdjacentInside(RectInt inside, RectInt adjacentTo, out Vector2Int tile)
    {
        tile = default;
        if (inside.width <= 0 || inside.height <= 0 || adjacentTo.width <= 0 || adjacentTo.height <= 0) return false;

        var choices = new List<Vector2Int>();
        foreach (var p in Outline(adjacentTo))
        {
            foreach (var n in Neigh4(p))
            {
                if (InsideRect(n, inside)) choices.Add(n);
            }
        }
        if (choices.Count == 0) return false;
        tile = choices[UnityEngine.Random.Range(0, choices.Count)];
        return true;
    }

    // Choose a tile along the front wall of 'common' (visible from entrance)
    Vector2Int PlaceAlongFrontWall(RectInt common, Vector2Int frontDir)
    {
        var wall = new List<Vector2Int>();
        if (frontDir == new Vector2Int(0, 1))      // front at bottom
            for (int x = common.xMin + 1; x < common.xMax - 1; x++) wall.Add(new Vector2Int(x, common.yMin + 1));
        else if (frontDir == new Vector2Int(0,-1)) // front at top
            for (int x = common.xMin + 1; x < common.xMax - 1; x++) wall.Add(new Vector2Int(x, common.yMax - 2));
        else if (frontDir == new Vector2Int(1, 0)) // front at left
            for (int y = common.yMin + 1; y < common.yMax - 1; y++) wall.Add(new Vector2Int(common.xMin + 1, y));
        else                                        // front at right
            for (int y = common.yMin + 1; y < common.yMax - 1; y++) wall.Add(new Vector2Int(common.xMax - 2, y));

        if (wall.Count == 0) return SafeCenterTile(common);
        return wall[UnityEngine.Random.Range(0, wall.Count)];
    }

    Vector2Int SafeCenterTile(RectInt r)
    {
        if (r.width <= 0 || r.height <= 0) return default;
        return new Vector2Int((int)Mathf.Clamp(r.center.x, r.xMin, r.xMax - 1),
                              (int)Mathf.Clamp(r.center.y, r.yMin, r.yMax - 1));
    }

    // Slice a band into multiple rooms along its long axis
    void SliceBandIntoRooms(RectInt band, bool horizontal, int minSize, out List<RectInt> roomsOut)
    {
        roomsOut = new List<RectInt>();
        if (band.width <= 0 || band.height <= 0) return;

        if (horizontal)
        {
            int x = band.xMin;
            while (x + minSize <= band.xMax)
            {
                // Random-ish width in [minSize .. minSize+2], clamp to band
                int w = Mathf.Min(UnityEngine.Random.Range(minSize, minSize + 3), band.xMax - x);
                // Skip tiny slivers at the end
                if (band.xMax - (x + w) > 0 && band.xMax - (x + w) < minSize) w = band.xMax - x;
                var r = new RectInt(x, band.yMin, w, band.height);
                if (r.width >= minSize) roomsOut.Add(r);
                x += w;
            }
        }
        else
        {
            int y = band.yMin;
            while (y + minSize <= band.yMax)
            {
                int h = Mathf.Min(UnityEngine.Random.Range(minSize, minSize + 3), band.yMax - y);
                if (band.yMax - (y + h) > 0 && band.yMax - (y + h) < minSize) h = band.yMax - y;
                var r = new RectInt(band.xMin, y, band.width, h);
                if (r.height >= minSize) roomsOut.Add(r);
                y += h;
            }
        }
    }

    // Overload that lets you choose WC size
RectInt MakeSmallWCNearHall(RectInt hall, RectInt upFoot, bool preferTop, Vector2Int wcSize)
{
    var size = wcSize;
    // Try four corners of the hall
    var candidates = new[]
    {
        new RectInt(hall.xMin - size.x, hall.yMin, size.x, size.y),
        new RectInt(hall.xMax,           hall.yMin, size.x, size.y),
        new RectInt(hall.xMin - size.x, hall.yMax - size.y, size.x, size.y),
        new RectInt(hall.xMax,           hall.yMax - size.y, size.x, size.y),
    }.Select(r => IntersectRect(r, upFoot))
     .Where(r => r.width == size.x && r.height == size.y)
     .ToList();

    if (candidates.Count == 0) return new RectInt(0,0,0,0);

    // Slight bias: top/left options first if preferTop
    if (preferTop)
    {
        candidates = candidates
            .OrderBy(r => r.yMin) // smaller y first (top)
            .ThenBy(r => r.xMin)
            .ToList();
    }
    else
    {
        candidates = candidates
            .OrderByDescending(r => r.xMin) // right side first
            .ThenBy(r => r.yMin)
            .ToList();
    }
    return candidates[0];
}
    // Prefer the actually carved Common room rect if present; else the zone rect
    RectInt GetRoomRect(string name, RectInt fallback)
    {
        // If you maintain per-room rects elsewhere, replace with that.
        // Otherwise, attempt to deduce the carved Common by bounding its tiles.
        var commonRoom = rooms.FirstOrDefault(r => r.name == name);
        if (commonRoom != null && commonRoom.tiles != null && commonRoom.tiles.Count > 0)
        {
            int xmin = commonRoom.tiles.Min(t => t.x);
            int xmax = commonRoom.tiles.Max(t => t.x);
            int ymin = commonRoom.tiles.Min(t => t.y);
            int ymax = commonRoom.tiles.Max(t => t.y);
            return new RectInt(xmin, ymin, xmax - xmin + 1, ymax - ymin + 1);
        }
        return fallback;
    }
}