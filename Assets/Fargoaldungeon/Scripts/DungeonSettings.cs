using UnityEngine;

[CreateAssetMenu(fileName = "DungeonSettings", menuName = "Scriptable Objects/DungeonSettings")]
public class DungeonSettings : ScriptableObject
{
    public bool randomizeSeed = true;
    public int seed = 0;
    public int mapWidth = 150;
    public int mapHeight = 150;
    public int roomAttempts = 50;
    public int roomsMax = 10;

    public int minRoomSize = 4;
    public int maxRoomSize = 8;
    public bool allowOverlappingRooms = false;
    public bool shortestTunnels = false;
    public bool ovalRooms = false;
    public bool directCorridors = false;
    public int corridorWidth = 1;
    public float jitterChance = 0.2f; // Chance to introduce a "wiggle" in corridors

    public DungeonAlgorithm RoomAlgorithm = DungeonAlgorithm.Scatter_Overlap;
    public TunnelsAlgorithm TunnelsAlgorithm = TunnelsAlgorithm.TunnelsOrganic;

// Settings for Cellular Automata
    [Header("Cellular Automata Settings")]
    public int width = 150;
    public int height = 150;
    [Range(0, 100)] public int fillPercent = 45;
    public int totalSteps = 5;
    public float stepDelay = 0.3f;

    [Header("Perlin Noise Settings")]
    public bool usePerlin = true;
    public float noiseScale = 0.1f;
    public float noiseThreshold = 0.5f;

    [Header("Tiles & Tilemap")]
    public int MinimumRoomSize = 100; // Threshold for tiny rooms
    public int BorderSize = 5; // Size of the border around the map

}
