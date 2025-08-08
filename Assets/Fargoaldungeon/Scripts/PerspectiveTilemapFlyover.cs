using System.Collections;
using UnityEngine;
using UnityEngine.Tilemaps;

public class PerspectiveTilemapFlyover : MonoBehaviour
{
    public Camera cam;
    public Tilemap tilemap;

    [Header("Flight Settings")]
    public float flyDuration = 1.5f;
    [Range(0f, 1f)] public float padding = 0.06f;
    public float startTiltDeg = 45f; // angled view
    public float endTiltDeg = 90f;   // straight down
    public float yawDeg = 30f;       // around Y axis
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
        StartCoroutine(FlyToTopDown());
    }

    IEnumerator FlyToTopDown()
    {
        yield return new WaitForSeconds(0.3f); // allow scene to load
        // Get bounds of actual tiles
        tilemap.CompressBounds();
        Bounds localBounds = tilemap.localBounds;
        Vector3 centerWorld = tilemap.transform.TransformPoint(localBounds.center);

        // Bounding sphere radius in world space
        Vector3 extentsWorld = Vector3.Scale(localBounds.extents, tilemap.transform.lossyScale);
        float radius = extentsWorld.magnitude;

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
}