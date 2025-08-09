using System.Collections.Generic;
using UnityEngine;

public class BezierDraw : MonoBehaviour
{
    public DungeonSettings cfg; // Reference to the DungeonSettings ScriptableObject

    public List<Vector2Int> DrawBezierCorridor(Vector2Int start, Vector2Int end)
    {
        int length = 0;
        Vector2 p0 = start;
        Vector2 p3 = end;
        int controlOffsetLimited;
        Vector2 mid = (p0 + p3) * 0.5f;
        Vector2 dir = (p3 - p0).normalized;
        Vector2 perp = Vector2.Perpendicular(dir);

        length = (int)Vector2.Distance(p0, p3);

        // Short corridors need less control offset or they go crazy
        if (cfg.controlOffset > (int)(length / cfg.max_control))
            controlOffsetLimited = (int)(length / cfg.max_control);
        else
            controlOffsetLimited = (int)cfg.controlOffset;

        // Improved control points: pull toward midpoint + random bend
        Vector2 p1 = Vector2.Lerp(p0, mid, 0.5f) + perp * Random.Range(-controlOffsetLimited, controlOffsetLimited);
        Vector2 p2 = Vector2.Lerp(p3, mid, 0.5f) + perp * Random.Range(-controlOffsetLimited, controlOffsetLimited);

        length = GetEstimatedLength(p0, p1, p2, p3);
        List<Vector2Int> path = SampleBezierCurve(p0, p1, p2, p3, length);

        return path;
    }

    List<Vector2Int> SampleBezierCurveUnordered(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, int steps)
    {
        HashSet<Vector2Int> sampledPoints = new HashSet<Vector2Int>();

        for (int i = 0; i <= steps; i++)
        {
            float t = (float)i / steps;
            Vector2 point = CubicBezier(p0, p1, p2, p3, t);
            sampledPoints.Add(Vector2Int.RoundToInt(point));
        }

        return new List<Vector2Int>(sampledPoints);
    }

    List<Vector2Int> SampleBezierCurve(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, int steps)
    {
        HashSet<Vector2Int> seen = new HashSet<Vector2Int>();
        List<Vector2Int> orderedPoints = new List<Vector2Int>();

        for (int i = 0; i <= steps; i++)
        {
            float t = (float)i / steps;
            Vector2 point = CubicBezier(p0, p1, p2, p3, t);
            Vector2Int tile = Vector2Int.RoundToInt(point);

            if (seen.Add(tile)) // Add returns true if it was not already in the set
            {
                orderedPoints.Add(tile);
            }
        }

        return orderedPoints;
    }

    Vector2 CubicBezier(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t)
    {
        float u = 1 - t;
        return u * u * u * p0
             + 3 * u * u * t * p1
             + 3 * u * t * t * p2
             + t * t * t * p3;
    }

    int GetEstimatedLength(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3)
    {
        float linearLength = Vector2.Distance(p0, p3);
        float controlLength = Vector2.Distance(p0, p1) + Vector2.Distance(p1, p2) + Vector2.Distance(p2, p3);
        float estimate = (linearLength + controlLength) / 2f;
        return (int)estimate;
    }

}