using UnityEngine;



[CreateAssetMenu(fileName = "DungeonSettings", menuName = "Scriptable Objects/DungeonSettings")]
public class DungeonSettings : ScriptableObject
{
    // Shared enumerations
    public enum DungeonAlgorithm_e { Scatter_Overlap, Scatter_NoOverlap, CellularAutomata, CellularAutomataPerlin }
    public enum TunnelsAlgorithm_e { TunnelsOrthographic, TunnelsStraight, TunnelsOrganic, TunnelsCurved }

    [Header("General Settings")]
    public bool showBuildProcess = true;
    public float stepDelay = 0.2f;

    public bool randomizeSeed = true;
    public int seed = 0;
    public int mapWidth = 150;
    public int mapHeight = 150;
    public int roomAttempts = 50;
    public int roomsMax = 10;

    public int minRoomSize = 20;
    public int maxRoomSize = 40;
    public bool allowOverlappingRooms = false;
    public bool shortestTunnels = false;
    public bool ovalRooms = false;
    public bool directCorridors = false;
    public int corridorWidth = 3;
    public int wallThickness = 2;
    public float jitterChance = 0.2f; // Chance to introduce a "wiggle" in corridors

    public DungeonAlgorithm_e RoomAlgorithm = DungeonAlgorithm_e.Scatter_Overlap;
    public TunnelsAlgorithm_e TunnelsAlgorithm = TunnelsAlgorithm_e.TunnelsOrganic;

    // Settings for Cellular Automata
    [Header("Cellular Automata Settings")]
    public bool useCellularAutomata = false;
    [Range(50, 60)] public int fillPercent = 45;
    public int totalSteps = 5;

    [Header("Perlin Noise Settings")]
    public bool usePerlin = true;
    [Range(0.01f, 0.1f)]
    [Tooltip("Low = big rooms | High = small rooms")]
    public float perlinScale = 0.05f;
    [Range(0.01f, 0.5f)]
    [Tooltip("Low = big rooms | High = small rooms")]
    public float perlin2Scale = 0.05f;
    [Range(0.4f, 0.6f)]
    [Tooltip("Low = many rooms | High = fewer rooms")]
    public float perlinThreshold = 0.5f;
    [Tooltip("Percent of white noise overlayed")]
    [Range(0, 100)] public int noiseOverlay = 40; // Percent of white noise overlayed

    [Header("Cleanup Settings")]
    public int MinimumRoomSize = 100; // Threshold for tiny rooms
    public int MinimumRockSize = 20; // Threshold for minimum size of in-room obstacle
    public int softBorderSize = 5; // Size of the border around the map

    [Header("Bezier Corridor Settings")]
    public float controlOffset = 5f;
    public float max_control = 0.1f;
    
    // Neighbor Cache Settings
    public NeighborCache.Shape neighborShape = NeighborCache.Shape.Square;
    public bool includeDiagonals = true;
}
