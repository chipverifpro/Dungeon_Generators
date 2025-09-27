using UnityEngine;

public partial class Player : MonoBehaviour
{
    // This is a primitive move in direction function.
    public Vector2 GetMovementInput_Primitive()
    {
        // 1) gather keyboard input -> desired world direction (XY)
        Vector2 input = new Vector2(
            Input.GetAxisRaw("Horizontal"),  // A/D or Left/Right
            Input.GetAxisRaw("Vertical")     // W/S or Up/Down
        );

        if (input.sqrMagnitude > 1f) input.Normalize();
        return input;
    }

    public BottomBanner bottomBanner;

    [Header("Movement")]
    //public float baseSpeed = 6f;                // forward speed (units/sec)
    public float turnSpeedDegPerSec = 180f;     // A/D rotate speed; set from cfg if you want
    public bool useXZPlane = false;             // false = XY floor (tilemap), true = XZ floor (3D)

    [Header("Collision")]
    //[Range(0.1f, 0.49f)] public float radius = 0.30f;
    //public int constraintIters = 3;

    // Pose state
    Vector2 pos2;          // XY or XZ (depending on useXZPlane)
    float yawDeg;          // facing yaw in degrees (around Z for XY, around Y for XZ)

    void Input_Start()
    {
        var p = transform.position;
        if (useXZPlane) pos2 = new Vector2(p.x, p.z);
        else            pos2 = new Vector2(p.x, p.y);

        // Initialize yaw from current rotation
        yawDeg = useXZPlane ? transform.eulerAngles.y : transform.eulerAngles.z;
    }

    void Input_Update()
    {
        if (gen == null || gen.cellGrid == null) return;

        // 1) Input: A/D rotate, W/S move forward/back
        float turn = Input.GetAxisRaw("Horizontal"); // A/D
        float thrust = Input.GetAxisRaw("Vertical"); // W/S

        // Rotate
        yawDeg += turn * turnSpeedDegPerSec * Time.deltaTime;
 
        // commit rotation ALWAYS (even if thrust == 0)
        if (useXZPlane) transform.rotation = Quaternion.Euler(0f, yawDeg + 90f, 0f);
        else transform.rotation = Quaternion.Euler(0f, 0f, yawDeg + 90f);

        // Forward direction unit vector in 2D plane
        float yawRad = - yawDeg * Mathf.Deg2Rad;
        Vector2 fwd2 = new Vector2(Mathf.Cos(yawRad), Mathf.Sin(yawRad)); // XY forward (or XZ’s X/Z)

        // Desired 2D motion (no strafing here)
        Vector2 desiredDir2 = fwd2 * Mathf.Clamp(thrust, -1f, 1f);
        float speed = baseSpeed * SampleSlopeMultiplier(pos2, desiredDir2);

        // 2) Integrate and resolve against grid edges
        Vector2 p0 = pos2;
        Vector2 p1 = p0 + desiredDir2 * speed * Time.deltaTime;
        p1 = ResolveGridConstraints(p0, p1, radius, constraintIters);

        // 3) Commit position & rotation to Transform
        pos2 = p1;

        if (useXZPlane)
        {
            var t = transform.position;
            t.x = pos2.x; t.z = pos2.y; // note: pos2.y -> world Z
            transform.position = t;
            //transform.rotation = Quaternion.Euler(0f, yawDeg, 0f); // rotate around Y for 3D
        }
        else
        {
            var t = transform.position;
            t.x = pos2.x; t.y = pos2.y; // XY floor
            transform.position = t;
            //transform.rotation = Quaternion.Euler(0f, 0f, yawDeg); // rotate around Z for XY
        }
    }
    //BottomBanner.Show($"x = {posXY.x}, y = {posXY.y}, yawDeg = {yawDeg}");
}