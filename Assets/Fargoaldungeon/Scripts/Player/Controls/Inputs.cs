using UnityEngine;

public partial class Player : MonoBehaviour
{

    public BottomBanner bottomBanner;

    [Header("Movement")]
    //public float baseSpeed = 6f;                // forward speed (units/sec)
    public float turnSpeedDegPerSec = 180f;     // A/D rotate speed; set from cfg if you want
    public bool useXZPlane = false;             // false = XY floor (tilemap), true = XZ floor (3D)

    [Header("Player to Walls adjustment")]
    public float xCorrection = 0.5f;
    public float yCorrection = 0.5f;
    public float yawCorrection = 90f;
    public float heightCorrection = 1f;
    //[Range(0.1f, 0.49f)] public float radius = 0.30f;
    //public int constraintIters = 3;

    // Pose state
    Vector2 pos2;          // XY or XZ (depending on useXZPlane)
    float yawDeg;          // facing yaw in degrees (around Z for XY, around Y for XZ)

    void Input_Start()
    {
        var p = transform.position;
        if (useXZPlane) pos2 = new Vector2(p.x + xCorrection, p.z + yCorrection);
        else pos2 = new Vector2(p.x + xCorrection, p.y + yCorrection);

        // Initialize yaw from current rotation
        yawDeg = useXZPlane ? transform.eulerAngles.y - yawCorrection : transform.eulerAngles.z - yawCorrection;
    }

    void Input_Update()
    {
        // 1) Input: A/D rotate, W/S move forward/back
        float turn = Input.GetAxisRaw("Horizontal"); // A/D
        float thrust = Input.GetAxisRaw("Vertical"); // W/S

        if ((Mathf.Abs(turn) > 1e-10f) || (Mathf.Abs(thrust) > 1e-10f))   // reject tiny movements
        {
            Move_Update(turn, thrust);  // Only needed if we want to move
        }
    }
}