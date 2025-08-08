using UnityEngine;
using TMPro;

public class DungeonGUISelector : MonoBehaviour
{
    public TMP_Dropdown roomAlgorithmDropdown;
    public TMP_Dropdown tunnelsAlgorithmDropdown;
    public DungeonGenerator generator;

    public void OnRoomAlgorithmSelected(int index)
    {
        string selected = roomAlgorithmDropdown.options[index].text;
        Debug.Log("Room Algorithm selected: " + selected);

        generator.RoomAlgorithm = (DungeonAlgorithm)index;
        generator.RegenerateDungeon();
    }

    public void OnTunnelsAlgorithmSelected(int index)
    {
        string selected = tunnelsAlgorithmDropdown.options[index].text;
        Debug.Log("Tunnels Algorithm selected: " + selected);

        generator.TunnelsAlgorithm = (TunnelsAlgorithm)index;
        generator.RegenerateDungeon();
    }

    void Start()
    {
        roomAlgorithmDropdown.value = (int)generator.RoomAlgorithm;
        roomAlgorithmDropdown.onValueChanged.AddListener(OnRoomAlgorithmSelected);

        tunnelsAlgorithmDropdown.value = (int)generator.TunnelsAlgorithm;
        tunnelsAlgorithmDropdown.onValueChanged.AddListener(OnTunnelsAlgorithmSelected);
    }
}