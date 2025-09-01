using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public partial class DungeonGenerator : MonoBehaviour
{

        // ====== Config shapes (lightweight, adjust to your cfg layout) ======
    [Serializable]
    public class TavernFeatureFlags
    {
        public bool hasStage = true;
        public bool hasBooths = true;
        public bool hasCellar = true;
        public bool hasStables = false;
    }

    [Serializable]
    public class TavernStairFlags
    {
        public bool up = true;
        public bool down = true;
    }

    [Serializable]
    public class TavernConfig
    {
        public bool enabled = true;

        public Vector2Int minSize = new Vector2Int(24, 18);
        public float maxAspect = 2.0f;

        public Vector2 commonPct = new Vector2(0.55f, 0.70f);
        public Vector2 servicePct = new Vector2(0.20f, 0.35f);
        public Vector2 privatePct = new Vector2(0.10f, 0.20f);

        public int hallWidth = 2;


        public Vector2Int frontSpan = new Vector2Int(2, 3);  // door span width range
        public int windowEvery = 3;                          // later phases

        public TavernFeatureFlags features = new TavernFeatureFlags();
        public TavernStairFlags stairs = new TavernStairFlags();

        public int validationRetries = 24;                   // candidate tries

        // Street edge bias (N/E/S/W). If empty, picked at random.
        [Tooltip ("Street edge bias: N, E, S, W or blank=random")]
        public string streetEdge = "";                       // "N","E","S","W" or ""
        public int worldMargin = 1;                          // keep footprint slightly inside edge

        [Header("Phase D (Common Room)")]
        // ---- Phase D (Common Room) tunables ----
        public Vector2Int hearthSize = new Vector2Int(2, 1); // fireplace footprint
        public Vector2Int stageSize  = new Vector2Int(4, 2); // stage footprint

        public Vector2Int boothSize  = new Vector2Int(2, 3); // each booth rect
        public int maxBooths         = 3;                    // number of booth alcoves

        public int barDepth = 2;
        // Visual clamping for the bar shape (helps readability when space is tight)
        public int barMinVisualWidth = 3;  // minimal bar width when horizontal
        public int barMinVisualHeight = 1;  // minimal bar thickness if squeezed

        // ---- Phase E (Service / Back-of-House) ----
        [Header ("Phase E (Service / Back-of-House)")]
        // Staff corridor width (number of tiles)
        public int corridorWidth = 2;

        // Fallback growth allowance when trying to place rooms
        public int maxGrow = 6;
        
        // Kitchen minimum footprint
        public Vector2Int kitchenMin = new Vector2Int(5, 4);

        // Storage minimum footprint
        public Vector2Int storageMin = new Vector2Int(4, 3);

        // Office minimum footprint
        public Vector2Int officeMin = new Vector2Int(3, 4);

        // Rear storage band depth (min tiles deep along rear wall)
        public int rearBandMinDepth = 3;

        // Corridor expansion margin when checking adjacency
        public int corridorAdjacencyBuffer = 2;

        // ---- Phase F parameters (NEW) ----
        [Header ("Phase F (Upstairs)")]
        public Vector2Int bedroomMinSize = new Vector2Int(3, 3);
        public Vector2Int wcSize         = new Vector2Int(2, 2);
        public int minBedrooms           = 3;
        public int maxBedrooms           = 6;
    }

    // Add this to your main cfg:
    [Header("Tavern Settings")]
    public TavernConfig tavern = new TavernConfig();

    public int buildingTries = 10;


    // ======= BuildTavern: Main tavern phase dispatch, retries.
    //         Only adds final structure to main rooms list
    public IEnumerator BuildTavern(TimeTask tm = null)
    {
        bool createdHere = false;
        if (tm == null) { tm = TimeManager.Instance.BeginTask("Tavern:Zoning"); createdHere = true; }

        try
        {
            // initialize a local room list and clear the maps
            List<Room> building_rooms = new();
            global.tilemap.ClearAllTiles();
            global.rooms.Clear();
            tm2d.map = new byte[cfg.mapWidth, cfg.mapHeight];

            // TAVERN
            if (tavern.enabled)
            {
                building_rooms.Clear();
                int tries;
                ca.success = false;
                while (!ca.success)
                {
                    // Phase B
                    BottomBanner.Show("Build Tavern Footprint");
                    ca.success = false;
                    for (tries = 0; (tries < buildingTries) && (ca.success == false); tries++)
                    {
                        yield return BuildTavernFootprint(building_rooms, tm: null);
                        yield return null;
                        if (ca.success == false) continue; // try again
                        yield return tm.YieldOrDelay(cfg.stepDelay);
                    }
                    if (ca.success == false) continue; // start over

                    // Phase C
                    BottomBanner.Show("Build Tavern Zoning");
                    ca.success = false;
                    for (tries = 0; (tries < buildingTries) && (ca.success == false); tries++)
                    {
                        yield return BuildTavernZoning(building_rooms, tm: null);
                        yield return null;
                        if (ca.success == false) continue;
                        yield return tm.YieldOrDelay(cfg.stepDelay);
                    }
                    if (ca.success == false) continue;

                    // PHASE D
                    BottomBanner.Show("Build Tavern Common");
                    ca.success = false;
                    for (tries = 0; (tries < buildingTries) && (ca.success == false); tries++)
                    {
                        yield return BuildTavernCommon(building_rooms, tm: null);
                        yield return null;
                        if (ca.success == false) continue;
                        yield return tm.YieldOrDelay(cfg.stepDelay);
                    }
                    if (ca.success == false) continue;

                    // Phase E
                    BottomBanner.Show("Build Tavern Service");
                    ca.success = false;
                    for (tries = 0; (tries < buildingTries) && (ca.success == false); tries++)
                    {
                        yield return BuildTavernService(building_rooms, tm: null);
                        yield return null;
                        if (ca.success == false) continue;
                        yield return tm.YieldOrDelay(cfg.stepDelay);
                    }
                    if (ca.success == false) continue;

                    // Phase F
                    /*BottomBanner.Show("Build Tavern Stairs and Upper");
                    ca.success = false;
                    for (tries = 0; (tries < buildingTries) && (ca.success == false); tries++)
                    {
                        yield return BuildTavernStairsAndUpper(building_rooms, tm: null);
                        yield return null;
                        if (ca.success == false) continue;
                        yield return tm.YieldOrDelay(cfg.stepDelay);
                    }
                    if (ca.success == false) continue;
                    */
                }
                if (ca.success == true)
                {
                    // Done with tavern, add it to full world
                    global.rooms.AddRange(building_rooms);
                }
            }
        }
        finally
        {
            if (createdHere) tm.End();
        }
    }
}
