using UnityEngine;
using UnityEngine.EventSystems; // to ignore UI clicks

public partial class Player : MonoBehaviour
{
    void Input_Update()
    {
        // 1) Input: A/D rotate, W/S move forward/back
        float turn = Input.GetAxisRaw("Horizontal"); // A/D
        float thrust = Input.GetAxisRaw("Vertical"); // W/S

        if ((Mathf.Abs(turn) > .001) || (Mathf.Abs(thrust) > .001))   // reject tiny movements
        {
            Move_Update(turn, thrust);  // Only needed if we want to move
        }

        // Change the agent controlled by the player using the number keys
        if (Input.GetKeyDown(KeyCode.Alpha1)) ChangePlayerAgentById(0);
        if (Input.GetKeyDown(KeyCode.Alpha2)) ChangePlayerAgentById(1);
        if (Input.GetKeyDown(KeyCode.Alpha3)) ChangePlayerAgentById(2);
        if (Input.GetKeyDown(KeyCode.Alpha4)) ChangePlayerAgentById(3);
        if (Input.GetKeyDown(KeyCode.Alpha5)) ChangePlayerAgentById(4);

        UpdateMouseInput();  // Click/tap to move
        MoveTowardMouseTarget();   // move toward clicked location
    }

    // Move this function into the moving.cs file.
    void MoveTowardMouseTarget()
    {
        if (agent.next_formationCrumb.valid)
        {
            //Vector3 pos3 = new(agent.pos2.x, agent.height, agent.pos2.y);
            Vector2 crumbpos2 = new(agent.next_formationCrumb.position.x, agent.next_formationCrumb.position.z);
            float dist = Vector2.Distance(agent.pos2, crumbpos2);
            if (dist < 0.1f)
            {
                //destination = Vector3.zero; // reached
                agent.next_formationCrumb.valid = false;
                agent.next_formationCrumb.position = Vector3.zero;
            }
            else
            {
                Vector2 dir = (crumbpos2 - agent.pos2).normalized;
                agent.yawDeg = Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg - yawCorrection; // face target

                Vector3 startpos = new(agent.pos2.x, agent.height, agent.pos2.y);
                Move_Update(0, 1); // always forward
                Vector3 endpos = new(agent.pos2.x, agent.height, agent.pos2.y);
                float movedist = Vector3.Distance(startpos, endpos);    // how far we moved this frame
                if (movedist < 0.01f)
                {
                    // we are stuck, so cancel the move order.
                    agent.next_formationCrumb.valid = false;
                    agent.next_formationCrumb.position = Vector3.zero;
                }
                else
                {
                    TransformPosition(agent);
                }
            }
        }
    }



    //[Header("Leader & Routing")]
    //public Transform leader;                    // your leader's Transform
    //public Player player;              // optional: your routing component with SetDestination(Vector3)

    [Header("Grid / Map")]
    public Vector3 origin = Vector3.zero;       // world-space origin of cell (0,0)
    public float cellSize = 1f;                 // world units per cell

    [Header("Raycast")]
    public LayerMask groundMask = ~0;           // set to your Ground layer(s)
    public float rayMaxDistance = 200f;

    [Header("Height Sampling (optional)")]
    public bool sampleTiltedFloorY = true;

    void AwakeMouseInput()
    {
        // if (leader && !player) player = leader.GetComponent<Player>(); // not necessary
    }

    void StartMouseInput()
    {

    }

    void UpdateMouseInput()
    {
        // Left click / primary tap
        if (!GetPrimaryDown()) return;

        // Ignore if pointer is over UI
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

        // Raycast to ground
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        if (!Physics.Raycast(ray, out var hit, rayMaxDistance, groundMask)) return;

        // Convert world → cell
        Vector3 p = hit.point;
        int cx = Mathf.FloorToInt((p.x - origin.x) / cellSize);
        int cz = Mathf.FloorToInt((p.z - origin.z) / cellSize);

        // Bounds check
        if (cx < 0 || cz < 0 || cx >= gen.cfg.mapWidth || cz >= gen.cfg.mapHeight) return;

        // Cell center (X,Z)
        float centerX = origin.x + (cx + 0.5f) * cellSize;
        float centerZ = origin.z + (cz + 0.5f) * cellSize;

        // Height Y
        float y = agent.height * gen.cfg.unitHeight;
        // y = hit.point.y; // fallback if no height sampling
        if (sampleTiltedFloorY && gen.cellGrid != null)
        {
            // Sample your tilted floor plane at the clicked tile center
            y = SampleTiltedFloorY(new Vector2(centerX, centerZ), gen.cellGrid);
        }

        Vector3 dest = new Vector3(centerX, y, centerZ);

        // Route the player's leader to that cell by reusing the breadcrumb trail system.
        // leaders don't generally follow breadcrumbs, but this is a convenient way to
        // send them to a target location.
        Crumb crumb = new Crumb();
        crumb.position = dest;
        crumb.valid = true;
        Vector2 crumbpos2 = new(crumb.position.x, crumb.position.z);
        Vector2 dir = (agent.pos2 - crumbpos2).normalized;
        crumb.yawDeg = Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg - yawCorrection; // face target
        agent.yawDeg = crumb.yawDeg;
        agent.next_formationCrumb = crumb;

        // (Optional) visual debug
        //Debug.DrawLine(Camera.main.transform.position, hit.point, Color.cyan, 0.5f);
        //Debug.DrawRay(dest, Vector3.up * 1.5f, Color.yellow, 0.75f);
        Debug.Log($"Clicked cell: ({cx},{cz}) world: {dest}");
    }

    // Mouse or touch primary press
    bool GetPrimaryDown()
    {
        if (Input.GetMouseButtonDown(0)) return true;
        if (Input.touchCount > 0 && Input.touches[0].phase == TouchPhase.Began) return true;
        return false;
    }

    // --- Tilted floor height sampling (uses your Cell.tiltFloor & height) ---
    float SampleTiltedFloorY(Vector2 worldXZ, Cell[,] grid)
    {
        int cx = Mathf.FloorToInt((worldXZ.x - origin.x) / cellSize);
        int cz = Mathf.FloorToInt((worldXZ.y - origin.z) / cellSize);
        int W = grid.GetLength(0), H = grid.GetLength(1);
        if (cx < 0 || cz < 0 || cx >= W || cz >= H) return agent ? agent.pos2.y : 0f;

        var cell = grid[cx, cz];

        // Plane normal from tilt
        Vector3 n = (cell.tiltFloor * Vector3.up).normalized;

        // Cell center point on plane at base height
        float centerX = origin.x + (cx + 0.5f) * cellSize;
        float centerZ = origin.z + (cz + 0.5f) * cellSize;
        Vector3 P0 = new Vector3(centerX, cell.height, centerZ);

        // Solve n·(X - P0)=0 for y, where X=(x,y,z)
        float ny = Mathf.Abs(n.y) < 1e-5f ? Mathf.Sign(n.y) * 1e-5f : n.y;
        float x = worldXZ.x, z = worldXZ.y;
        float y = P0.y - (n.x * (x - P0.x) + n.z * (z - P0.z)) / ny;
        return y;
    }
}