using UnityEngine;
using TMPro;

public class DungeonGUISelector : MonoBehaviour
{
    public DungeonSettings cfg; // Reference to the DungeonSettings ScriptableObject
    public TMP_Dropdown roomAlgorithmDropdown;
    public TMP_Dropdown tunnelsAlgorithmDropdown;
    public DungeonGenerator generator;

    public void OnRoomAlgorithmSelected(int index)
    {
        string selected = roomAlgorithmDropdown.options[index].text;
        Debug.Log("Room Algorithm selected: " + selected);

        cfg.RoomAlgorithm = (DungeonSettings.DungeonAlgorithm_e)index;
        generator.RegenerateDungeon();
    }

    public void OnTunnelsAlgorithmSelected(int index)
    {
        string selected = tunnelsAlgorithmDropdown.options[index].text;
        Debug.Log("Tunnels Algorithm selected: " + selected);

        cfg.TunnelsAlgorithm = (DungeonSettings.TunnelsAlgorithm_e)index;
        generator.RegenerateDungeon();
    }

    void Start()
    {
        roomAlgorithmDropdown.value = (int)cfg.RoomAlgorithm;
        roomAlgorithmDropdown.onValueChanged.AddListener(OnRoomAlgorithmSelected);

        tunnelsAlgorithmDropdown.value = (int)cfg.TunnelsAlgorithm;
        tunnelsAlgorithmDropdown.onValueChanged.AddListener(OnTunnelsAlgorithmSelected);
    }
}