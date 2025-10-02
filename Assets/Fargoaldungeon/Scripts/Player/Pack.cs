using System.Collections.Generic;
using UnityEngine;

public class Pack : MonoBehaviour
{
    public Player player;   // reference to player class, which handles all the player inputs
    public DungeonGenerator gen;
    public Transform PackParentObject;  // Parent object that already exists in the scene.  All the PlayerAgents will be attached under it.
    public GameObject agentVisual;  // Optional visual (e.g., Capsule/Cube). Can be null.
    public BreadcrumbTrail trail;   // The BreadcrumbTrail class
    //public BreadcrumbTrail breadcrumbTrailPrefab; // Assign this in the Inspector.  It is a prefab visualization for what?  Breadcrumb trail or each breadcrumb?
    //public BreadcrumbTrail trailPrefabObj; // An object representing the trail (I think)

    [Header("Current Pack")]
    // pack related parameters:
    public Agent PackLeader;            // current leader, (usually controlled by player)
    public List<Agent> packList;        // All pack members
    public bool inFollowFormation = true;
    public bool inGroupFormation = false;
    public bool soloMode = false;       // not travelling as a pack

    //public Formations formation; // future: SingleFile, Triangle, SideBySide, TwoWide, DefensiveCircle 

    [Header("Pack Members Object Creation")]
    public int squadSize = 4;       // 1 leader + (squadSize - 1) followers
    public float initialSpacing = 1.5f;

    void Start()
    {
        if (PackParentObject == null)
        {
            Debug.LogError("Pack (parent) is not assigned.");
            return;
        }
        //if (breadcrumbTrailPrefab != null)
        //    trailPrefabObj = Instantiate(breadcrumbTrailPrefab, PackParentObject);
        //else
        //    trailPrefabObj = null;

        //CreatePackObjects();  // Crreate Leader and Followers under the Pack object
        for (int i = 0; i < 4; i++)
            CreatePackAgent();
    }

    public Agent PlayerAgent1;

    public void CreatePackAgent()
    {
        // Make a copy at the same position/rotation
        Agent clone = Instantiate(PlayerAgent1);

        // Optional: parent under something
        clone.transform.SetParent(PackParentObject, false);

        // Optional: move it a little so you can see both
        clone.transform.position += Vector3.right * 2f;

        packList.Add(clone);
    }

    // old function builds from scratch.  Other version above copies one already created which is much simpler to manage.
    public void CreatePackObjects()
    {
        if (squadSize < 1) squadSize = 1;

        // --- Create leader ---
        // Create leader object
        GameObject leaderObj = CreateAgentGO("LeaderObj", PackParentObject, agentVisual);

        // Create and attach a BreadcrumTrail class to the leaderObj
        //BreadcrumbTrail trail = leaderObj.AddComponent<BreadcrumbTrail>();

        // try creating the trail as a component of Pack
        BreadcrumbTrail trail = PackParentObject.gameObject.AddComponent<BreadcrumbTrail>();
        trail.name = "Pack_Breadcrumb_Trail_Component";

        // Create and attach a PlayerAgent class to the LeaderObj
        PlayerAgent leader = leaderObj.AddComponent<PlayerAgent>();
        leader.name = "Pack_Leader_Agent";

        // Add references to the PlayerAgent class
        packList.Add(leader);           // Add it to packList
        trail.leader = leader;    // Add it to BreadCrumbTrail

        // Pick a starting location for the leader object.
        leaderObj.transform.localPosition = Vector3.zero;

        // --- Create Followers ---
        for (int i = 1; i < squadSize; i++)
        {
            // create a follower object
            GameObject followerObj = CreateAgentGO($"FollowerObj_{i}", PackParentObject, agentVisual);

            // Create and attach a PlayerAgent class to the FollowerObj.
            PlayerAgent follower = followerObj.AddComponent<PlayerAgent>();
            follower.name = $"Follower_Agent_{i}" + i;

            // Add references to the PlayerAgent class
            packList.Add(follower);
            trail.AddFollower(follower);

            // give a starting location for the follower object.
            followerObj.transform.localPosition = new Vector3(0f, 0f, -i * initialSpacing);
        }
    }

    private GameObject CreateAgentGO(string name, Transform parent, GameObject visualPrefab)
    {
        GameObject go;
        if (visualPrefab != null)
        {
            go = Instantiate(visualPrefab, parent);
            go.name = name;
        }
        else
        {
            go = new GameObject(name);
            go.transform.SetParent(parent, false);

            // Give a simple visible shape if none provided
            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Body";
            body.transform.SetParent(go.transform, false);
            body.transform.localPosition = Vector3.zero;
        }
        return go;
    }

}


/*
[RequireComponent(typeof(BreadcrumbTrail))]
public class FollowAgent : Agent
{
    [Header("Chain Links")]
    public Transform frontAgent;                 // The agent directly ahead
    public BreadcrumbTrail frontAgentTrail;      // That agent's trail (leader or another follower)

    [Header("Following Settings")]
    public float spacing = 1.5f;                 // Minimum distance to maintain behind frontAgent
    public float moveSpeed = 4.5f;
    public float arriveRadius = 0.1f;            // Consider crumb "reached" inside this radius
    public float lookAtTurnSpeed = 8f;           // Smooth face toward move direction

    // Each follower keeps an index into the front agent's crumb list (oldest->newest)
    private int targetCrumbIndex = 0;

    // Reserve hooks for future formation logic
    public bool formationMode = false;

    void Update()
    {
        if (formationMode)
        {
            // Future: compute a slot offset relative to a formation anchor and move there.
            HoldFormation();
            return;
        }

        ChaseCrumbs();
    }

    private void ChaseCrumbs()
    {
        if (frontAgent == null || frontAgentTrail == null)
            return;

        var crumbs = frontAgentTrail.crumbs;
        Vector3 targetPos;

        if (crumbs.Count == 0)
        {
            // No crumbs yet: fallback to chasing the front agent with spacing safeguard
            targetPos = frontAgent.position;
        }
        else
        {
            // Ensure targetCrumbIndex is within range
            targetCrumbIndex = Mathf.Clamp(targetCrumbIndex, 0, crumbs.Count - 1);

            // If we're close to the current crumb, advance toward newer ones
            float distToCurrent = Vector3.Distance(transform.position, crumbs[targetCrumbIndex].position);
            while (distToCurrent < arriveRadius && targetCrumbIndex < crumbs.Count - 1)
            {
                targetCrumbIndex++;
                distToCurrent = Vector3.Distance(transform.position, crumbs[targetCrumbIndex].position);
            }

            targetPos = crumbs[targetCrumbIndex].position;
        }

        // Maintain minimum spacing behind the front agent
        float distToFront = Vector3.Distance(transform.position, frontAgent.position);
        float speed = moveSpeed;

        if (distToFront < spacing)
        {
            // Too close: slow/stop to maintain gap
            float t = Mathf.InverseLerp(spacing * 0.5f, spacing, distToFront);
            speed *= Mathf.Clamp01(t); // smoothly reduce speed as we get too close
        }

        // Move toward the target (XZ plane)
        Vector3 flatTarget = new Vector3(targetPos.x, transform.position.y, targetPos.z);
        Vector3 to = (flatTarget - transform.position);
        Vector3 dir = to.sqrMagnitude > 1e-6f ? to.normalized : Vector3.zero;

        transform.position += dir * speed * Time.deltaTime;

        // Smoothly rotate to face movement direction
        if (dir.sqrMagnitude > 1e-6f)
        {
            Quaternion look = Quaternion.LookRotation(dir, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, look, lookAtTurnSpeed * Time.deltaTime);
        }

        // Optionally, advance the crumb pointer when we pass near it,
        // even if we had to slow down for spacing.
        if (frontAgentTrail.crumbs.Count > 0 && targetCrumbIndex < frontAgentTrail.crumbs.Count)
        {
            if (Vector3.Distance(transform.position, frontAgentTrail.crumbs[targetCrumbIndex].position) < arriveRadius)
            {
                if (targetCrumbIndex < frontAgentTrail.crumbs.Count - 1) targetCrumbIndex++;
            }
        }
    }

    private void HoldFormation()
    {
        // Placeholder: keep the follower mostly in place, maintaining spacing if the
        // front agent is within range. Later, compute desired slot relative to a formation anchor.
        if (frontAgent == null) return;

        float distToFront = Vector3.Distance(transform.position, frontAgent.position);
        if (distToFront < spacing * 0.9f)
        {
            // Nudge back a bit if we’re crowding
            Vector3 away = (transform.position - frontAgent.position).normalized;
            transform.position += away * (spacing - distToFront) * 0.5f * Time.deltaTime;
        }
    }

    // Optional: call this if you ever need to reset the follower to the “back of the line”
    public void ResetCrumbIndex()
    {
        targetCrumbIndex = 0;
    }
}


public class SquadSpawner : MonoBehaviour
{
}
*/





/*
[DefaultExecutionOrder(-200)] // init early
public class ScentGrid : MonoBehaviour
{
    [System.Serializable]
    public struct ScentEvent
    {
        public int agentId;
        public float dropTime;      // Time.time when placed
        public float intensity;     // at creation, scales with agent stinkiness
    }

    [System.Serializable]
    public class Cell
    {
        public readonly List<ScentEvent> events = new List<ScentEvent>(4);
    }

    [Header("Scent Parameters")]
    public float ScentInterval = 30f;       // interval to decay/spread scents (seconds)
    public float ScentDecayRate = 0.9f;     // decay by percent per ScentInterval
    public float ScentSpreadAmount = 0.1f;  // neighbors get this percent added per ScentInterval
    public float ScentMinimum = 0.005f;       // amount below which the scent completely disappears

    void Awake()
    {
        grid = new Cell[width, height];
        for (int x = 0; x < width; x++)
            for (int z = 0; z < height; z++)
                grid[x, z] = new Cell();
    }

    public bool WorldToCell(Vector3 pos, out int cx, out int cz)
    {
        Vector3 local = pos - origin;
        cx = Mathf.FloorToInt(local.x / cellSize);
        cz = Mathf.FloorToInt(local.z / cellSize);
        return cx >= 0 && cx < width && cz >= 0 && cz < height;
    }

    public Vector3 CellCenter(int cx, int cz)
    {
        return origin + new Vector3((cx + 0.5f) * cellSize, 0f, (cz + 0.5f) * cellSize);
    }

    /// Add a scent event at a world position (clamped to the cell)
    public void AddScent(Vector3 worldPos, int agentId, float stinkiness)
    {
        if (!WorldToCell(worldPos, out int cx, out int cz)) return;
        var cell = grid[cx, cz];
        cell.events.Add(new ScentEvent
        {
            agentId = agentId,
            dropTime = Time.time,
            baseIntensity = Mathf.Max(0f, stinkiness)
        });

        // soft trim per-cell (keeps newest)
        if (cell.events.Count > 64) cell.events.RemoveRange(0, cell.events.Count - 64);
    }

    /// Exponential decay using half-life. Returns current intensity.
    public float CurrentIntensity(ScentEvent e)
    {
        float age = Mathf.Max(0f, Time.time - e.dropTime);
        // I(t) = I0 * 0.5^(age / halfLife)
        if (halfLifeSeconds <= 0.0001f) return 0f;
        return e.baseIntensity * Mathf.Pow(0.5f, age / halfLifeSeconds);
    }

    /// Total intensity in a cell for: all agents, a specific target, or all except a target.
    public float GetCellIntensity(int cx, int cz, int? onlyAgentId = null)
    {
        if (cx < 0 || cz < 0 || cx >= width || cz >= height) return 0f;
        float sum = 0f;
        var list = grid[cx, cz].events;
        for (int i = 0; i < list.Count; i++)
        {
            var e = list[i];
            if (onlyAgentId.HasValue && e.agentId != onlyAgentId.Value) continue;
            sum += CurrentIntensity(e);
        }
        return sum;
    }

    /// Optional periodic cleanup—call occasionally (not every frame)
    public void TrimAged(float olderThanSeconds)
    {
        float cutoff = Time.time - olderThanSeconds;
        for (int x = 0; x < width; x++)
            for (int z = 0; z < height; z++)
            {
                var list = grid[x, z].events;
                int keepStart = 0;
                // find first index with dropTime >= cutoff
                while (keepStart < list.Count && list[keepStart].dropTime < cutoff) keepStart++;
                if (keepStart > 0) list.RemoveRange(0, keepStart);
            }
    }
}
*/


/*
[RequireComponent(typeof(CharacterController))]
public class ScentAgentEmitter : MonoBehaviour
{
    public DungeonGenerator gen;
    public int agentId = 0;
    [Tooltip("Relative strength of the agent's scent.")]
    public float stinkiness = 1.0f;

    [Header("Drop Settings")]
    public float dropEveryDistance = 0.5f;

    private Vector3 lastDropPos;
    private bool primed;

    void Update()
    {
        if (gen.cellGrid == null) return;

        if (!primed)
        {
            lastDropPos = transform.position;
            leader.AddScent(lastDropPos, agentId, stinkiness);
            primed = true;
            return;
        }

        if ((transform.position - lastDropPos).sqrMagnitude >= dropEveryDistance * dropEveryDistance)
        {
            gen.cellGrid.AddScent(transform.position, agentId, stinkiness);
            lastDropPos = transform.position;
        }
    }
}


public class ScentTrackerOverlay : MonoBehaviour
{
    [Header("References")]
    public DungeonGenerator gen;
    public Transform viewer; // who is tracking (for range culling)

    [Header("Tracking")]
    public bool showOverlay = true;
    public int? targetAgentId = null; // null = any scent; set to specific agentId to track that target
    public float trackerSensitivity = 0.5f; // higher = can notice weaker trails
    public float viewRadius = 20f;          // only show nearby cells

    [Header("Visuals")]
    public float maxAlpha = 0.35f;
    public Gradient colorByStrength; // assign in Inspector (e.g., blue->green->yellow->red)
    public Material translucentMat;  // Standard/URP Lit with Rendering Mode = Transparent
    public float yHeight = 1.8f;     // cube height to “fill airspace”

    // Simple pool of cubes
    private readonly List<GameObject> pool = new List<GameObject>();
    private int poolUse;

    void LateUpdate()
    {
        if (!showOverlay || gen.cellGrid == null || translucentMat == null) { HideAll(); return; }

        poolUse = 0;

        // optional: trim aged scent once in a while
        //if (Time.frameCount % 30 == 0) gen.cellGrid.TrimAged(grid.trimOlderThan);

        Vector3 center = (viewer != null) ? viewer.position : transform.position;
        float r2 = viewRadius * viewRadius;

        for (int x = 0; x < gen.cfg.mapWidth; x++)
            for (int z = 0; z < gen.cfg.mapHeight; z++)
            {
                Vector3 cellCenter = gen.cellGrid.CellCenter(x, z);
                if ((cellCenter - center).sqrMagnitude > r2) continue;

                float intensity = gen.cellGrid.GetCellIntensity(x, z, targetAgentId);
                // Compare against sensitivity to cull super-weak cells
                if (intensity <= 0f) continue;

                float norm = intensity / Mathf.Max(0.0001f, trackerSensitivity); // >1 = strong
                norm = Mathf.Clamp01(norm);

                var go = GetCube();
                go.transform.position = cellCenter + new Vector3(0f, yHeight * 0.5f, 0f);
                go.transform.localScale = new Vector3(gen.cellGrid.cellSize, yHeight, gen.cellGrid.cellSize);

                // color & alpha by strength
                Color c = (colorByStrength != null) ? colorByStrength.Evaluate(norm) : Color.cyan;
                c.a = norm * maxAlpha;

                var mr = go.GetComponent<MeshRenderer>();
                mr.sharedMaterial = translucentMat;
                mr.enabled = true;

                // push per-instance color via MaterialPropertyBlock
                var mpb = new MaterialPropertyBlock();
                mr.GetPropertyBlock(mpb);
                mpb.SetColor("_BaseColor", c);       // URP Lit
                mpb.SetColor("_Color", c);           // Built-in Standard fallback
                mr.SetPropertyBlock(mpb);
            }

        // disable any unused pooled cubes
        for (int i = poolUse; i < pool.Count; i++)
            pool[i].SetActive(false);
    }

    private void HideAll()
    {
        for (int i = 0; i < pool.Count; i++)
            pool[i].SetActive(false);
    }

    private GameObject GetCube()
    {
        if (poolUse < pool.Count)
        {
            var go = pool[poolUse++];
            go.SetActive(true);
            return go;
        }
        else
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(go.GetComponent<Collider>());
            var mr = go.GetComponent<MeshRenderer>();
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            go.name = "ScentOverlayCell";
            pool.Add(go);
            poolUse++;
            return go;
        }
    }
}
*/


