using UnityEngine;
using UnityEngine.Rendering;



[CreateAssetMenu(fileName = "DungeonSettings", menuName = "Scriptable Objects/DungeonSettings")]
public class DungeonSettings : ScriptableObject
{
    // Type enumerations...
    public enum RoomAlgorithm_e { Scatter_Overlap, Scatter_NoOverlap, CellularAutomata, CellularAutomataPerlin, Tavern }
    public enum TunnelsAlgorithm_e { TunnelsOrthogonal, TunnelsStraight, TunnelsOrganic, TunnelsCurved }


    [Header("Master Configurations")]
    public RoomAlgorithm_e RoomAlgorithm = RoomAlgorithm_e.Scatter_Overlap;
    public TunnelsAlgorithm_e TunnelsAlgorithm = TunnelsAlgorithm_e.TunnelsOrganic;

    [Header("General Settings")]
    public bool showBuildProcess = true;
    public float stepDelay = 0.2f; // how many seconds to wait between generation steps
    public bool randomizeSeed = true;
    public int seed = 0;

    [Header("World Map Settings")]
    public int mapWidth = 150;
    public int mapHeight = 150;
    public bool roundWorld = false; // sometimes not having square map edges is nice.
    public int maxElevation = 100;

    [Header("Scatter Room Settings")]
    public bool useScatterRooms = false;
    public int roomAttempts = 50;
    public int roomsMax = 10;
    public int minRoomSize = 20;
    public int maxRoomSize = 40;
    public bool generateOverlappingRooms = false;
    public bool MergeScatteredRooms = false;
    public bool allowVerticalStacking = true;
    public int minVerticalStackHeight = 5;  // less than this results in merged rooms
    public bool ovalRooms = false;

    // Settings for Cellular Automata
    [Header("Cellular Automata Settings")]
    public bool useCellularAutomata = false;
    [Range(40, 60)] public int cellularFillPercent = 45;
    public int CellularGrowthSteps = 5;

    [Header("Perlin Noise Settings")]
    public bool usePerlin = true;
    [Range(0.01f, 0.1f)]
    [Tooltip("Low = big rooms | High = small rooms")]
    public float perlinWavelength = 0.05f; // Low frequency Perlin for room size
    [Range(0.01f, 0.5f)]
    [Tooltip("Low = lumpy rooms | High = craggy rooms")]
    public float perlin2Wavelength = 0.05f; // Higher frequency Perlin for room roughness
    [Range(0f, 4f)]
    [Tooltip("Low = smooth perlin | High = more roughness")]
    public float perlin2Amplitude = 1f; // Multiplier for perlin2
    [Range(0.4f, 0.6f)]
    [Tooltip("Low = many rooms | High = fewer rooms")]
    public float perlinThreshold = 0.5f;

    [Header("Map Cleanup Settings")]
    public int MinimumRoomSize = 100; // Threshold for tiny rooms filter
    public int MinimumRockSize = 20; // Threshold for minimum size of in-room obstacle
    public int softBorderSize = 5; // Size of the noisy border around the map to soften edge, only works on square maps currently
    public int wallThickness = 1;  // Appearance of perimeter walls in 2D map
    public int perlinFloorHeights = 3;  // Adds a ripple to the floor.

    [Header("Corridor Settings")]
    public int corridorWidth = 3;  // Width of passages generated between rooms.
    public bool limit_slope = true;  // don't allow slopes to exceed walkability
    public int minimumRamp = 2;  // less than this is not considered a ramp
    public int maximumRamp = 8;  // more than this is considered a cliff
    
    [Header("Organic Type corridor Settings")]
    public float organicJitterChance = 0.2f; // Chance to introduce a wiggle in "organic" corridors

    [Header("Bezier Corridor Settings")]
    public float bezierControlOffset = 5f; // how curvy to make Bezier corridors
    public float bezierMaxControl = 0.1f; // clip bezierControlOffset for short Bezier corridors

    [Header("Neighbor Cache Settings")]
    public NeighborCache.Shape neighborShape = NeighborCache.Shape.Square;
    public bool includeDiagonals = true;

    [Header("Building Settings")]
    public bool createBuilding = false;
    public int cellar_floor_height = -10;
    public int ground_floor_height = 0;
    public int next_floor_height = 10;
}
