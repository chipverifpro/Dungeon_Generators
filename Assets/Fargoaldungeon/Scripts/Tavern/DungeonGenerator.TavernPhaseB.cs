using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Phase B: Footprint & Orientation for a Tavern (drop-in).
/// Assumes DungeonGenerator has: cfg (with tavern sub-config), map size, and RoomFitsWorld(RectInt).
/// </summary>
public partial class DungeonGenerator : MonoBehaviour
{

    // ====== Output container for Phase B ======
    [Serializable]
    public class TavernFootprint
    {
        public RectInt rect;
        public Vector2Int frontDir;            // one of (0,1),(1,0),(0,-1),(-1,0)
        public List<Vector2Int> tiles;         // all footprint tiles
        public List<Vector2Int> doorSpan;      // contiguous tiles on the front wall
        public List<Vector2Int> frontFacade;   // front exterior segments (excluding door area)
        public List<Vector2Int> exterior;      // all exterior perimeter tiles (unique)

        public int score;                      // soft score used to choose best candidate
    }

    // Store latest footprint (for later phases)
    public TavernFootprint tavernFootprint;

    // ====== Public entry point ======
    // 

    public IEnumerator BuildTavernFootprint(List<Room> rooms, TimeTask tm = null)
    {
        if (!tavern.enabled) yield break;

        bool createdHere = false;
        if (tm == null) { tm = TimeManager.Instance.BeginTask("Tavern:Footprint"); createdHere = true; }

        try
        {
            tilemap.ClearAllTiles();
            rooms.Clear();
            map = new byte[cfg.mapWidth, cfg.mapHeight];

            // We’ll try up to 4 street sides if needed (unless user fixed one)
            var tryDirs = new List<Vector2Int>();
            var fixedDir = PickFrontDir();
            if (!string.IsNullOrEmpty(tavern.streetEdge))
            {
                tryDirs.Add(fixedDir);
            }
            else
            {
                tryDirs.Add(fixedDir);
                tryDirs.AddRange(new[] { new Vector2Int(1, 0), new Vector2Int(0, -1), new Vector2Int(-1, 0), new Vector2Int(0, 1) }
                                 .Where(d => d != fixedDir));
            }

            TavernFootprint best = null;

            // Diagnostics
            int rejectsBounds = 0, rejectsRound = 0, rejectsDoor = 0, rejectsOther = 0;

            foreach (var frontDir in tryDirs)
            {
                int attempts = Mathf.Max(16, tavern.validationRetries);
                for (int i = 0; i < attempts; i++)
                {
                    var cand = SampleCandidate(frontDir, i);
                    if (cand == null) { rejectsOther++; if (tm.IfYield()) yield return null; continue; }

                    if (!RectInsideMap(cand.rect))
                    {
                        rejectsBounds++; if (tm.IfYield()) yield return null; continue;
                    }

                    if (cfg.roundWorld && !RoomFitsWorld(cand.rect, samples: 24, margin: 0.0f))
                    {
                        rejectsRound++; if (tm.IfYield()) yield return null; continue;
                    }

                    if (cand.doorSpan == null || cand.doorSpan.Count == 0)
                    {
                        rejectsDoor++; if (tm.IfYield()) yield return null; continue;
                    }

                    cand.score = ScoreFootprint(cand);
                    if (best == null || cand.score > best.score) best = cand;

                    if (tm.IfYield()) yield return null;
                }

                if (best != null) break; // found one on this side
            }

            // Optional final fallback: relax min size by 10% if still nothing
            if (best == null)
            {
                const bool allowFallbackRelax = true;
                if (allowFallbackRelax)
                {
                    var oldMin = tavern.minSize;
                    tavern.minSize = new Vector2Int(
                        Mathf.Max(8, Mathf.FloorToInt(oldMin.x * 0.9f)),
                        Mathf.Max(8, Mathf.FloorToInt(oldMin.y * 0.9f))
                    );
                    // Try once more on all sides with smaller min
                    foreach (var frontDir in tryDirs)
                    {
                        for (int i = 0; i < 16; i++)
                        {
                            var cand = SampleCandidate(frontDir, i + 999); // different RNG space
                            if (cand == null) { rejectsOther++; continue; }
                            if (!RectInsideMap(cand.rect)) { rejectsBounds++; continue; }
                            if (cfg.roundWorld && !RoomFitsWorld(cand.rect, samples: 24, margin: 0.0f)) { rejectsRound++; continue; }
                            if (cand.doorSpan == null || cand.doorSpan.Count == 0) { rejectsDoor++; continue; }
                            cand.score = ScoreFootprint(cand);
                            if (best == null || cand.score > best.score) best = cand;
                        }
                        if (best != null) break;
                    }
                    tavern.minSize = oldMin; // restore
                }
            }

            if (best == null)
            {
                success = false;
                failure =
                    $"Tavern Phase B: failed to find a valid footprint. " +
                    $"Rejects — Bounds:{rejectsBounds}, Round:{rejectsRound}, Door:{rejectsDoor}, Other:{rejectsOther}. " +
                    $"Try: decrease tavern.minSize, reduce worldMargin, or enable inward front inset.";
                Debug.LogWarning(failure);
                yield break;
            }

            tavernFootprint = best;

            // Emit a placeholder Room if you keep a list
            Room r = new Room
            {
                name = "TavernFootprint",
                tiles = best.tiles,
                heights = Enumerable.Repeat(-99, best.tiles.Count).ToList(),
            };
            rooms.Add(r);

            Debug.Log($"Tavern Phase B: rect={best.rect} frontDir={best.frontDir} doorTiles={best.doorSpan.Count} score={best.score}");

            DrawMapByRooms(rooms);
            success = true;
            Debug.Log($"Tavern Phase B: Tavern footprint at {tavernFootprint.rect.position} size {tavernFootprint.rect.size}");
        }
        finally
        {
            if (createdHere) tm.End();
        }
    }

    // ====== Helpers ======

    Vector2Int PickFrontDir()
    {
        string s = (tavern.streetEdge ?? "").Trim().ToUpperInvariant();
        if (s == "N") return new Vector2Int(0, 1);
        if (s == "E") return new Vector2Int(1, 0);
        if (s == "S") return new Vector2Int(0, -1);
        if (s == "W") return new Vector2Int(-1, 0);

        // Random choice if not specified
        int k = UnityEngine.Random.Range(0, 4);
        return k switch
        {
            0 => new Vector2Int(0, 1),   // N
            1 => new Vector2Int(1, 0),   // E
            2 => new Vector2Int(0, -1),  // S
            _ => new Vector2Int(-1, 0),  // W
        };
    }

    TavernFootprint SampleCandidate(Vector2Int frontDir, int attemptIndex)
    {
        // 1) Size sampling
        int minW = tavern.minSize.x;
        int minH = tavern.minSize.y;

        // Add tiny random slack as attempts grow
        int slack = Mathf.Min(4, attemptIndex / 4);
        int w = UnityEngine.Random.Range(minW, minW + 1 + slack);
        int h = UnityEngine.Random.Range(minH, minH + 1 + slack);

        // Clamp aspect
        float aspect = (float)Mathf.Max(w, h) / Mathf.Max(1, Mathf.Min(w, h));
        if (aspect > tavern.maxAspect)
        {
            // fix by nudging the smaller side up
            if (w > h) h = Mathf.CeilToInt(w / tavern.maxAspect);
            else       w = Mathf.CeilToInt(h / tavern.maxAspect);
        }

        // 2) Choose a position that keeps a small margin from map edges
        int margin = Mathf.Max(0, tavern.worldMargin);
        int maxX = cfg.mapWidth  - w - margin;
        int maxY = cfg.mapHeight - h - margin;

        if (maxX <= margin || maxY <= margin) return null;

        // Align so that the front wall lies along the chosen street side
        // We bias the rectangle so its front wall is near the corresponding edge.
        Vector2Int origin;
        if (frontDir == new Vector2Int(0, 1))    // front faces north → rect front at bottom edge
            origin = new Vector2Int(UnityEngine.Random.Range(margin, maxX), margin);
        else if (frontDir == new Vector2Int(0, -1)) // faces south → rect front at top edge
            origin = new Vector2Int(UnityEngine.Random.Range(margin, maxX), cfg.mapHeight - h - margin);
        else if (frontDir == new Vector2Int(1, 0))  // faces east → rect front at left edge
            origin = new Vector2Int(margin, UnityEngine.Random.Range(margin, maxY));
        else                                        // faces west → rect front at right edge
            origin = new Vector2Int(cfg.mapWidth - w - margin, UnityEngine.Random.Range(margin, maxY));

        var rect = new RectInt(origin.x, origin.y, w, h);

        // 3) Tiles & perimeter
        var tiles = RectTiles(rect);
        var exterior = RectPerimeter(rect).ToList();

        // 4) Front wall line and door span
        var frontWall = ExtractFrontWall(rect, frontDir);
        if (frontWall.Count == 0) return null;

        int spanW = UnityEngine.Random.Range(tavern.frontSpan.x, tavern.frontSpan.y + 1);
        var doorSpan = PickDoorSpan(frontWall, spanW);

        // 5) Front facade segments that are window-capable (front wall minus door & 1-tile buffers)
        var frontFacade = ComputeFrontFacade(frontWall, doorSpan);

        return new TavernFootprint
        {
            rect = rect,
            frontDir = frontDir,
            tiles = tiles,
            doorSpan = doorSpan,
            frontFacade = frontFacade,
            exterior = exterior
        };
    }

    bool RectInsideMap(RectInt r)
    {
        return r.xMin >= 0 && r.yMin >= 0 && r.xMax <= cfg.mapWidth && r.yMax <= cfg.mapHeight;
    }

    List<Vector2Int> RectTiles(RectInt r)
    {
        var list = new List<Vector2Int>(r.width * r.height);
        for (int y = r.yMin; y < r.yMax; y++)
            for (int x = r.xMin; x < r.xMax; x++)
                list.Add(new Vector2Int(x, y));
        return list;
    }

    IEnumerable<Vector2Int> RectPerimeter(RectInt r)
    {
        // top & bottom
        for (int x = r.xMin; x < r.xMax; x++) { yield return new Vector2Int(x, r.yMin); }
        for (int x = r.xMin; x < r.xMax; x++) { yield return new Vector2Int(x, r.yMax - 1); }
        // left & right
        for (int y = r.yMin; y < r.yMax; y++) { yield return new Vector2Int(r.xMin, y); }
        for (int y = r.yMin; y < r.yMax; y++) { yield return new Vector2Int(r.xMax - 1, y); }
    }

    List<Vector2Int> ExtractFrontWall(RectInt r, Vector2Int frontDir)
    {
        var list = new List<Vector2Int>();
        if (frontDir == new Vector2Int(0, 1))        // north-facing → front wall is bottom row
            for (int x = r.xMin; x < r.xMax; x++) list.Add(new Vector2Int(x, r.yMin));
        else if (frontDir == new Vector2Int(0, -1))  // south-facing → front wall is top row
            for (int x = r.xMin; x < r.xMax; x++) list.Add(new Vector2Int(x, r.yMax - 1));
        else if (frontDir == new Vector2Int(1, 0))   // east-facing → front wall is left col
            for (int y = r.yMin; y < r.yMax; y++) list.Add(new Vector2Int(r.xMin, y));
        else                                         // west-facing → front wall is right col
            for (int y = r.yMin; y < r.yMax; y++) list.Add(new Vector2Int(r.xMax - 1, y));
        return list;
    }

    List<Vector2Int> PickDoorSpan(List<Vector2Int> wall, int spanWidth)
    {
        if (wall.Count < spanWidth) return new List<Vector2Int>();

        // Avoid corners by at least 1 tile
        int startMin = 1;
        int startMax = wall.Count - spanWidth - 1;
        if (startMax < startMin) startMax = startMin;

        int start = UnityEngine.Random.Range(startMin, startMax + 1);

        // Determine if wall is horizontal or vertical by comparing coords
        bool horizontal = wall.Count >= 2 && wall[0].y == wall[1].y;

        var span = new List<Vector2Int>(spanWidth);
        for (int i = 0; i < spanWidth; i++)
        {
            var p = horizontal ? new Vector2Int(wall[start + i].x, wall[start].y)
                               : new Vector2Int(wall[start].x, wall[start + i].y);
            span.Add(p);
        }
        return span;
    }

    List<Vector2Int> ComputeFrontFacade(List<Vector2Int> wall, List<Vector2Int> doorSpan)
    {
        var setDoor = new HashSet<Vector2Int>(doorSpan);
        // 1-tile buffers at each end of the door span
        var doorSorted = doorSpan.OrderBy(p => p.x + p.y * 8192).ToList();
        var buffered = new HashSet<Vector2Int>(setDoor);

        if (doorSorted.Count > 0)
        {
            // find neighbors along the wall to buffer by one on each side if available
            var first = doorSorted.First();
            var last = doorSorted.Last();
            // choose axis automatically
            bool horizontal = wall.Count >= 2 && wall[0].y == wall[1].y;
            var b1 = horizontal ? new Vector2Int(first.x - 1, first.y) : new Vector2Int(first.x, first.y - 1);
            var b2 = horizontal ? new Vector2Int(last.x + 1, last.y) : new Vector2Int(last.x, last.y + 1);
            if (wall.Contains(b1)) buffered.Add(b1);
            if (wall.Contains(b2)) buffered.Add(b2);
        }

        var facade = new List<Vector2Int>(wall.Count);
        foreach (var p in wall)
            if (!buffered.Contains(p))
                facade.Add(p);
        return facade;
    }

    int ScoreFootprint(TavernFootprint f)
    {
        // Soft scoring: bigger front wall, door somewhat centered, decent area
        int frontLen = f.frontFacade.Count + f.doorSpan.Count;
        int area = f.rect.width * f.rect.height;

        // center bias: measure doorSpan midpoint distance from wall midpoint
        float doorMid;
        float wallMid;
        bool horizontal = (f.frontFacade.Count + f.doorSpan.Count) >= 2 &&
                          ((f.frontFacade.Count > 1 ? f.frontFacade[0].y == f.frontFacade[1].y
                                                    : f.doorSpan.Count > 1 && f.doorSpan[0].y == f.doorSpan[1].y));
        if (horizontal)
        {
            var xs = f.doorSpan.Select(p => p.x).OrderBy(v => v).ToList();
            doorMid = (xs.First() + xs.Last()) * 0.5f;
            wallMid = (f.rect.xMin + f.rect.xMax - 1) * 0.5f;
        }
        else
        {
            var ys = f.doorSpan.Select(p => p.y).OrderBy(v => v).ToList();
            doorMid = (ys.First() + ys.Last()) * 0.5f;
            wallMid = (f.rect.yMin + f.rect.yMax - 1) * 0.5f;
        }
        float centerPenalty = Mathf.Abs(doorMid - wallMid);

        // score formula (tweak as desired)
        int score = 0;
        score += frontLen * 10;
        score += Mathf.RoundToInt(area * 0.2f);
        score -= Mathf.RoundToInt(centerPenalty * 3f);

        return score;
    }
}