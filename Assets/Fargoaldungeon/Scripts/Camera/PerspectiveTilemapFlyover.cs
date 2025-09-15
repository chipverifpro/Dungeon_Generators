using System.Collections;
using UnityEngine;
using UnityEngine.Tilemaps;

public class PerspectiveTilemapFlyover : MonoBehaviour
{
    public Camera cam;
    public Tilemap tilemap;
    public DungeonSettings cfg; // Configurable settings for project


    [Header("Flight Settings")]
    public float flyDuration = 1.5f;
    [Range(0f, 1f)] public float padding = 0.06f;
    public float startTiltDeg = -90f; // angled view
    public float endTiltDeg = -45f;   // straight down
    public float yawDeg = 0f;       // around Y axis
    public bool autoStart = true;

    void Reset()
    {
        cam = GetComponent<Camera>();
    }

    void Start()
    {
        if (autoStart) FlyNow();
    }

    public void FlyNow()
    {
        if (cam == null || tilemap == null)
        {
            Debug.LogWarning("Assign Camera and Tilemap.");
            return;
        }
        Vector3Int origin = new Vector3Int(0, 0, 0);
        Vector3Int size = new Vector3Int(cfg.mapWidth, cfg.mapHeight, 1);

        Bounds worldBounds = ComputeWorldBounds(tilemap, origin, size);
        if (worldBounds.size == Vector3.zero)
        {
            Debug.LogWarning("Tilemap bounds are zero. Ensure tiles are painted.");
            return;
        }
        // Fit your camera now, even before tiles are painted:
        FitOrthoCameraToBounds(cam, worldBounds);
        StartCoroutine(FlyToTopDown(worldBounds));
        FlyRandomNow();
    }

    IEnumerator FlyToTopDown(Bounds worldBounds)
    {
        float radius;
        Vector3 centerWorld;
        Vector3 extentsWorld;
        Bounds localBounds;

        if (worldBounds.size != Vector3.zero)
        {
            // Center of the tilemap area in world space
            centerWorld = worldBounds.center;
            localBounds = worldBounds; // Use the provided bounds directly

            // Bounding sphere that encloses the rect at any tilt/yaw
            extentsWorld = worldBounds.extents;
            radius = Mathf.Max(0.001f, extentsWorld.magnitude);
        }
        else
        {
            //yield return new WaitForSeconds(0.3f); // allow scene to load
            // Get bounds of actual tiles
            tilemap.CompressBounds();
            localBounds = tilemap.localBounds;
            centerWorld = tilemap.transform.TransformPoint(localBounds.center);

            // Bounding sphere radius in world space
            extentsWorld = Vector3.Scale(localBounds.extents, tilemap.transform.lossyScale);
            radius = extentsWorld.magnitude;
        }
        // Fit sphere to camera FOV
        float vFov = cam.fieldOfView * Mathf.Deg2Rad;
        float hFov = 2f * Mathf.Atan(Mathf.Tan(vFov * 0.5f) * cam.aspect);
        float distV = radius / Mathf.Sin(vFov * 0.5f);
        float distH = radius / Mathf.Sin(hFov * 0.5f);
        float baseDistance = Mathf.Max(distV, distH) * (1f + padding);

        float startTime = Time.time;

        while (true)
        {
            float t = (Time.time - startTime) / flyDuration;
            if (t >= 1f) t = 1f;
            float s = Mathf.SmoothStep(0f, 1f, t);

            // Interpolate tilt angle
            float tilt = Mathf.Lerp(startTiltDeg, endTiltDeg, s);

            // Compute rotation: yaw around Y, tilt around X (downward)
            Quaternion rotation = Quaternion.Euler(90f - tilt, yawDeg, 0f);

            // Forward vector in world space
            Vector3 forward = rotation * Vector3.down; // straight down at tilt=90

            // Position camera so it's looking at the center
            Vector3 position = centerWorld - forward * baseDistance;
            cam.transform.SetPositionAndRotation(position, Quaternion.LookRotation(forward, Vector3.up));

            if (t >= 1f) break;
            yield return null;

        }
        //StartCoroutine(FlyToTopDown());
    }

    // The following code allows for computing world-space bounds for a rectangular block of cells in a Tilemap.
    /// <summary>
    /// Compute world-space bounds for a rectangular block of cells.
    /// origin = bottom-left cell (or wherever you start)
    /// size   = width/height in cells (z usually 1)
    /// </summary>
    public static Bounds ComputeWorldBounds(Tilemap tilemap, Vector3Int origin, Vector3Int size)
    {
        var grid = tilemap.layoutGrid;                   // the Grid component
        Vector3 cellSize = grid.cellSize;

        // If size is zero, return empty bounds
        if (size.x <= 0 || size.y <= 0)
            return new Bounds(tilemap.transform.position, Vector3.zero);

        // Inclusive min & max cells we’ll occupy
        Vector3Int minCell = origin;
        Vector3Int maxCell = new Vector3Int(origin.x + size.x - 1,
                                            origin.y + size.y - 1,
                                            origin.z + Mathf.Max(0, size.z - 1));

        // World centers of those cells
        Vector3 minCenter = tilemap.GetCellCenterWorld(minCell);
        Vector3 maxCenter = tilemap.GetCellCenterWorld(maxCell);

        // Convert centers to outer corners (± half a cell)
        Vector3 half = cellSize * 0.5f;
        Vector3 minCorner = minCenter - half;
        Vector3 maxCorner = maxCenter + half;

        // Build bounds
        Vector3 center = (minCorner + maxCorner) * 0.5f;
        Vector3 sizeWorld = maxCorner - minCorner;
        return new Bounds(center, sizeWorld);
    }
    public static void FitOrthoCameraToBounds(Camera cam, Bounds b, float padding = 0.05f)
    {
        float aspect = cam.aspect;
        float vSize = (b.size.y * 0.5f) * (1f + padding);
        float hSize = (b.size.x * 0.5f) / aspect * (1f + padding);
        cam.orthographicSize = Mathf.Max(vSize, hSize);
        cam.transform.position = new Vector3(b.center.x, b.center.y, cam.transform.position.z);
    }



    // --- Add these settings next to your other [Header("Flight Settings")] ---
    [Header("Random Flyover")]
    public bool loopFlyover = true;
    public int hops = 12;                 // how many random hops (ignored if loopFlyover=true)
    public float hopDuration = 1.6f;      // seconds per hop
    public float holdDuration = 0.5f;     // pause on each target
    [Range(-10f, -90f)] public float pitchDeg = -45f;   // 45° off the plane
    public float viewRadiusWorld = 20f;   // how much ground span to keep in view
    public float edgeMargin = 2f;         // don't target right on the border

    // Call this instead of FlyNow() if you want the random-hop camera
    public void FlyRandomNow()
    {
        if (cam == null || tilemap == null)
        {
            Debug.LogWarning("Assign Camera and Tilemap.");
            return;
        }

        // Use whole cfg map area
        var origin = new Vector3Int(0, 0, 0);
        var size = new Vector3Int(cfg.mapWidth, cfg.mapHeight, 1);
        Bounds worldBounds = ComputeWorldBounds(tilemap, origin, size);

        if (worldBounds.size == Vector3.zero)
        {
            Debug.LogWarning("Tilemap bounds are zero. Ensure tiles are painted.");
            return;
        }

        StopAllCoroutines();
        StartCoroutine(RandomFlyover(worldBounds));
    }

    IEnumerator RandomFlyover(Bounds bounds)
    {
        if (cam.orthographic)
        {
            Debug.Log("[Flyover] Switching to perspective for angled fly.");
            cam.orthographic = false;
        }

        // Precompute once
        float pitch = Mathf.Clamp(pitchDeg, -90f, 90f);
        float pitchRad = pitch * Mathf.Deg2Rad;

        // distance so that the *horizontal* ground projection is ~viewRadiusWorld
        // For a ray pitched down by `pitch`, horizontal projection = distance * cos(pitch)
        // => distance = viewRadiusWorld / cos(pitch)
        float baseDistance = Mathf.Max(5f, viewRadiusWorld / Mathf.Cos(pitchRad));

        // Start at a sensible place:
        Vector3 target = RandomTarget(bounds, edgeMargin);
        float yaw = RandomYaw();
        Vector3 pos = CameraPosFor(target, pitch, yaw, baseDistance);

        cam.transform.SetPositionAndRotation(
            pos,
            Quaternion.LookRotation((target - pos).normalized, Vector3.up)
        );

        int remaining = Mathf.Max(1, hops);
        while (loopFlyover || remaining-- > 0)
        {
            Vector3 nextTarget = RandomTarget(bounds, edgeMargin);
            float nextYaw = RandomYaw();

            // Optionally keep distance mostly constant, but add a tiny jitter for life
            float distance = baseDistance * Mathf.Lerp(0.95f, 1.05f, Random.value);

            Vector3 startPos = cam.transform.position;
            Quaternion startRot = cam.transform.rotation;

            Vector3 endPos = CameraPosFor(nextTarget, pitch, nextYaw, distance);
            Quaternion endRot = Quaternion.LookRotation((nextTarget - endPos).normalized, Vector3.up);

            float t0 = Time.time;
            while (true)
            {
                float t = Mathf.InverseLerp(0f, hopDuration, Time.time - t0);
                if (t >= 1f) t = 1f;

                // Smoothstep for a soft ease in/out
                float s = t * t * (3f - 2f * t);

                cam.transform.SetPositionAndRotation(
                    Vector3.Lerp(startPos, endPos, s),
                    Quaternion.Slerp(startRot, endRot, s)
                );

                if (t >= 1f) break;
                yield return null;
            }

            if (holdDuration > 0f)
                yield return new WaitForSeconds(holdDuration);

            target = nextTarget;
            yaw = nextYaw;
        }
    }

    Vector3 RandomTarget(Bounds b, float margin)
    {
        float x = Random.Range(b.min.x + margin, b.max.x - margin);
        float y = Random.Range(b.min.y + margin, b.max.y - margin);
        // Tilemap is usually on Z=0 plane; keep Z on the bounds center’s Z
        float z = Mathf.Lerp(b.min.z, b.max.z, 0.5f);
        return new Vector3(x, y, z);
    }

    float RandomYaw()
    {
        // Any yaw around Y axis is fine; pick random 0..360
        return Random.Range(0f, 360f);
    }

    Vector3 CameraPosFor(Vector3 target, float pitchDeg, float yawDeg, float distance)
    {
        // Build a forward vector from yaw/pitch (downward pitch = negative X-rotation)
        Quaternion q = Quaternion.Euler(-pitchDeg, yawDeg, 0f);
        Vector3 forward = q * Vector3.forward; // direction camera "looks"
                                               // Position so that forward aims at the target from 'distance' away
        return target - forward * distance;
    }
}
