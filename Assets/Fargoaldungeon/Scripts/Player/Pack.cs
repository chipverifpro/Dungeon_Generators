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

    // This version copies the PlayerAgent1 already created in the GUI.
    public void CreatePackAgent()
    {

        // Make a copy at the same position/rotation
        Agent clone = Instantiate(PlayerAgent1);
        gen.GetNewAgentId(clone);
        clone.name = "Player_Clone_" + (packList.Count + 1);
        trail.AddFollower(clone);
        clone.trailLeader = false;
        clone.trailFollower = true;
        // Optional: parent under something
        clone.transform.SetParent(PackParentObject, false);

        // Optional: move it a little so you can see both
        clone.transform.position += Vector3.right * 2f;

        // clone the visible prefab and attach it.
        GameObject prefabClone = Instantiate(PlayerAgent1.DogPrefab);
        prefabClone.name = "Dog_Clone_" + (packList.Count + 1);
        prefabClone.transform.SetParent(clone.transform, false);
        clone.DogPrefab = prefabClone;

        // add it to the list
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

