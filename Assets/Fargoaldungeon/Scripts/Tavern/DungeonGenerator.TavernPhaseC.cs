using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public partial class DungeonGenerator : MonoBehaviour
{
    [Serializable]
    public class TavernZones
    {
        public RectInt commonRect;
        public RectInt serviceRect;
        public RectInt privateRect; // width may be 0 if skipped
    }

    public TavernZones tavernZones;

    public IEnumerator BuildTavernZoning(List<Room> rooms, TimeTask tm = null)
    {
        if (tavernFootprint == null)
        {
            ca.success = false;
            ca.failure = "Tavern Phase C: No footprint yet. Run BuildTavernFootprint first.";
            Debug.LogWarning(ca.failure);
            yield break;
        }

        bool createdHere = false;
        if (tm == null) { tm = TimeManager.Instance.BeginTask("Tavern:Zoning"); createdHere = true; }

        try
        {
            global.tilemap.ClearAllTiles();
            rooms.Clear();
            tm2d.map = new byte[cfg.mapWidth, cfg.mapHeight];

            var fp = tavernFootprint.rect;
            int W = fp.width;
            int H = fp.height;
            int area = W * H;

            // Targets (pick a value inside each min..max range)
            float commonTargetPct = UnityEngine.Random.Range(tavern.commonPct.x, tavern.commonPct.y);
            float serviceTargetPct = UnityEngine.Random.Range(tavern.servicePct.x, tavern.servicePct.y);
            float privateTargetPct = UnityEngine.Random.Range(tavern.privatePct.x, tavern.privatePct.y);

            // Normalize if sum > 1
            float sum = commonTargetPct + serviceTargetPct + privateTargetPct;
            if (sum > 0.98f) { commonTargetPct /= sum; serviceTargetPct /= sum; privateTargetPct /= sum; }

            int targetCommon = Mathf.RoundToInt(area * commonTargetPct);
            int targetService = Mathf.RoundToInt(area * serviceTargetPct);
            int targetPrivate = Mathf.RoundToInt(area * privateTargetPct);

            // Minimums to make service viable (needs hall + kitchen + storage depth)
            int minServiceDepth = Mathf.Max(
                tavern.hallWidth + 1,
                tavern.kitchenMin.y,
                tavern.storageMin.y
            );

            // We’ll try a few “nudges” of the split to get a viable layout
            TavernZones best = null;
            int bestScore = int.MinValue;

            // Side choice for private strip: pick the side with longer exterior run by default
            // We'll try both sides for robustness.
            Vector2Int[] privateSideChoices = new[] {
                new Vector2Int(1,0),  // +X side
                new Vector2Int(-1,0)  // -X side
            };

            // Orientation helpers
            bool frontIsNorth = tavernFootprint.frontDir == new Vector2Int(0, 1);
            bool frontIsSouth = tavernFootprint.frontDir == new Vector2Int(0, -1);
            bool frontIsEast = tavernFootprint.frontDir == new Vector2Int(1, 0);
            bool frontIsWest = tavernFootprint.frontDir == new Vector2Int(-1, 0);

            // Try multiple depths/widths around targets
            for (int attempt = 0; attempt < 24; attempt++)
            {
                // Service as a band opposite the front, depth in tiles:
                int serviceDepthFromTarget = Mathf.Clamp(Mathf.RoundToInt((float)targetService / Mathf.Max(1, W)), minServiceDepth, H - 4);
                // Nudge depth ±2 tiles over attempts
                int nudge = (attempt % 5) - 2; // -2..+2 cycling
                int serviceDepth = Mathf.Clamp(serviceDepthFromTarget + nudge, minServiceDepth, H - 4);

                // Private as a side strip width in tiles (skip if too small)
                int privateWidthFromTarget = Mathf.Clamp(Mathf.RoundToInt((float)targetPrivate / Mathf.Max(1, H)), 0, Mathf.Max(0, W - 6));
                int privateWidth = privateWidthFromTarget;
                if (privateWidth > 0 && privateWidth < 3) privateWidth = 3; // min useful width

                foreach (var side in privateSideChoices)
                {
                    RectInt commonR, serviceR, privateR;

                    // Compute service band rect based on frontDir
                    if (frontIsNorth)
                    {
                        // front at fp.yMin → common is toward yMin, service is rear at top
                        serviceR = new RectInt(fp.xMin, fp.yMax - serviceDepth, W, serviceDepth);
                        // Private strip carves along X on full height minus service if desired
                        if (privateWidth >= 3)
                        {
                            if (side.x > 0) // right side (+X)
                                privateR = new RectInt(fp.xMax - privateWidth, fp.yMin, privateWidth, H - serviceDepth);
                            else            // left side (-X)
                                privateR = new RectInt(fp.xMin, fp.yMin, privateWidth, H - serviceDepth);
                        }
                        else privateR = new RectInt(0, 0, 0, 0);

                        // Remaining is common
                        RectInt frontBand = new RectInt(
                            side.x > 0 ? fp.xMin : (fp.xMin + privateR.width),
                            fp.yMin,
                            side.x > 0 ? (W - privateR.width) : (W - privateR.width),
                            H - serviceDepth
                        );
                        commonR = frontBand;
                    }
                    else if (frontIsSouth)
                    {
                        // front at fp.yMax-1 → common is toward yMax, service is rear at bottom
                        serviceR = new RectInt(fp.xMin, fp.yMin, W, serviceDepth);
                        if (privateWidth >= 3)
                        {
                            if (side.x > 0) // right side (+X)
                                privateR = new RectInt(fp.xMax - privateWidth, fp.yMin + serviceDepth, privateWidth, H - serviceDepth);
                            else
                                privateR = new RectInt(fp.xMin, fp.yMin + serviceDepth, privateWidth, H - serviceDepth);
                        }
                        else privateR = new RectInt(0, 0, 0, 0);

                        RectInt frontBand = new RectInt(
                            side.x > 0 ? fp.xMin : (fp.xMin + privateR.width),
                            fp.yMin + serviceDepth,
                            side.x > 0 ? (W - privateR.width) : (W - privateR.width),
                            H - serviceDepth
                        );
                        commonR = frontBand;
                    }
                    else if (frontIsEast)
                    {
                        // front at fp.xMin → common toward xMin, service is rear at right side
                        serviceR = new RectInt(fp.xMax - serviceDepth, fp.yMin, serviceDepth, H);
                        if (privateWidth >= 3)
                        {
                            // side.y decides top/bot; here we’ll still use left/right strip for simplicity
                            if (side.x > 0) // “+X” equivalent means bottom? To avoid confusion, keep side as horizontal strip:
                                privateR = new RectInt(fp.xMin, fp.yMin, privateWidth, H);
                            else
                                privateR = new RectInt(fp.xMax - serviceDepth - privateWidth, fp.yMin, privateWidth, H);
                        }
                        else privateR = new RectInt(0, 0, 0, 0);

                        // Common fills the rest (between private and service)
                        int commonX = privateR.width > 0 ? privateR.xMax : fp.xMin;
                        int commonW = fp.xMax - serviceR.xMin - (privateR.width > 0 ? privateR.width : 0);
                        if (privateR.width > 0 && privateR.xMin == fp.xMax - serviceDepth - privateR.width)
                        {
                            // private is adjacent to service on the inside; common is just the left band
                            commonX = fp.xMin;
                            commonW = fp.xMax - serviceR.xMin - privateR.width;
                        }
                        commonW = Mathf.Max(1, commonW);
                        commonR = new RectInt(commonX, fp.yMin, commonW, H);
                    }
                    else
                    {
                        // frontIsWest: front at fp.xMax-1 → common toward xMax, service is rear at left side
                        serviceR = new RectInt(fp.xMin, fp.yMin, serviceDepth, H);
                        if (privateWidth >= 3)
                        {
                            if (side.x > 0)
                                privateR = new RectInt(fp.xMin + serviceDepth, fp.yMin, privateWidth, H);
                            else
                                privateR = new RectInt(fp.xMax - privateWidth, fp.yMin, privateWidth, H);
                        }
                        else privateR = new RectInt(0, 0, 0, 0);

                        int commonX = serviceR.xMax + (privateR.width > 0 && privateR.xMin == serviceR.xMax ? privateR.width : 0);
                        int commonW = fp.xMax - commonX;
                        commonW = Mathf.Max(1, commonW);
                        commonR = new RectInt(commonX, fp.yMin, commonW, H);
                    }

                    // Clean overlaps & ensure inside fp
                    serviceR = IntersectRect(serviceR, fp);
                    privateR = IntersectRect(privateR, fp);
                    // common = footprint minus service and private (rect differences; approximate with bounding box minus overlaps)
                    RectInt commonClean = RectMinus(fp, serviceR, privateR);
                    commonR = IntersectRect(commonR, commonClean);

                    // Validate minimums
                    bool serviceOk = serviceR.width >= tavern.kitchenMin.x &&
                                     serviceR.height >= minServiceDepth - (tavern.hallWidth - 1); // corridor will be carved later
                    bool commonOk = commonR.width >= 6 && commonR.height >= 6; // a decent hall
                    bool privateOk = (privateR.width == 0 && privateR.height == 0) || (privateR.width >= 3 && privateR.height >= 4);

                    if (!serviceOk || !commonOk) continue;

                    // Scoring
                    int commonArea = commonR.width * commonR.height;
                    int serviceArea = serviceR.width * serviceR.height;
                    int privateArea = privateR.width * privateR.height;

                    int score = 0;
                    // Prefer common room close to its target and to be the largest
                    score -= Mathf.Abs(commonArea - targetCommon) / 2;
                    score -= Mathf.Abs(serviceArea - targetService) / 3;
                    score -= Mathf.Abs(privateArea - targetPrivate) / 3;
                    if (commonArea >= serviceArea && commonArea >= privateArea) score += 250;

                    // Slight bonus for wider common frontage (helps bar & stage later)
                    score += (frontIsNorth || frontIsSouth ? commonR.width : commonR.height) * 4;

                    if (score > bestScore)
                    {
                        bestScore = score;
                        best = new TavernZones { commonRect = commonR, serviceRect = serviceR, privateRect = privateR };
                    }
                }
                if (tm.IfYield()) yield return null;
            }

            if (best == null)
            {
                ca.failure = "Tavern Phase C: failed to zone the footprint. Try relaxing min sizes or aspect.";
                ca.success = false;
                Debug.LogWarning(ca.failure);
                yield break;
            }

            tavernZones = best;

            // Emit provisional zone rooms (helpful for visual debugging)
            var zCommon = RectTiles(best.commonRect);
            var zService = RectTiles(best.serviceRect);
            var zPrivate = (best.privateRect.width > 0 && best.privateRect.height > 0) ? RectTiles(best.privateRect) : new List<Vector2Int>();

            rooms.Add(new Room { name = "ZoneCommon", tiles = zCommon, heights = Enumerable.Repeat(-99, zCommon.Count).ToList() });
            rooms[rooms.Count - 1].colorFloor = Color.blue;
            rooms.Add(new Room { name = "ZoneService", tiles = zService, heights = Enumerable.Repeat(-99, zService.Count).ToList() });
            rooms[rooms.Count - 1].colorFloor = Color.yellow;
            if (zPrivate.Count > 0)
            {
                rooms.Add(new Room { name = "ZonePrivate", tiles = zPrivate, heights = Enumerable.Repeat(-99, zPrivate.Count).ToList() });
                rooms[rooms.Count - 1].colorFloor = Color.red;
            }
            Debug.Log($"Tavern Phase C: common={best.commonRect} service={best.serviceRect} private={best.privateRect} score={bestScore}");

            DrawMapByRooms(rooms);
            ca.success = true;
        }
        finally
        {
            if (createdHere) tm.End();
        }
    }

    // ---- rect helpers ----

    RectInt IntersectRect(RectInt a, RectInt b)
    {
        int xMin = Mathf.Max(a.xMin, b.xMin);
        int yMin = Mathf.Max(a.yMin, b.yMin);
        int xMax = Mathf.Min(a.xMax, b.xMax);
        int yMax = Mathf.Min(a.yMax, b.yMax);
        if (xMax <= xMin || yMax <= yMin) return new RectInt(0,0,0,0);
        return new RectInt(xMin, yMin, xMax - xMin, yMax - yMin);
    }

    RectInt RectMinus(RectInt whole, RectInt cutA, RectInt cutB)
    {
        // Approx: shrink whole so it doesn't overlap cuts on the sides they touch most.
        // This is sufficient for zoning visualization; precise polygon diff not needed here.
        RectInt r = whole;

        // If cutA spans full height and touches a side, trim along X
        if (cutA.height == r.height)
        {
            if (cutA.xMin == r.xMin) r.xMin = Mathf.Min(r.xMax, cutA.xMax);
            if (cutA.xMax == r.xMax) r.xMax = Mathf.Max(r.xMin, cutA.xMin);
        }
        if (cutB.height == r.height)
        {
            if (cutB.xMin == r.xMin) r.xMin = Mathf.Min(r.xMax, cutB.xMax);
            if (cutB.xMax == r.xMax) r.xMax = Mathf.Max(r.xMin, cutB.xMin);
        }
        // If cut spans full width and touches top/bottom, trim along Y
        if (cutA.width == r.width)
        {
            if (cutA.yMin == r.yMin) r.yMin = Mathf.Min(r.yMax, cutA.yMax);
            if (cutA.yMax == r.yMax) r.yMax = Mathf.Max(r.yMin, cutA.yMin);
        }
        if (cutB.width == r.width)
        {
            if (cutB.yMin == r.yMin) r.yMin = Mathf.Min(r.yMax, cutB.yMin + cutB.height);
            if (cutB.yMax == r.yMax) r.yMax = Mathf.Max(r.yMin, cutB.yMin);
        }

        if (r.width <= 0 || r.height <= 0) return new RectInt(0,0,0,0);
        return r;
    }
}