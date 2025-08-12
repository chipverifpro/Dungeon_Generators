using UnityEngine;
using TMPro;
//using UnityEngine.UIElements;
using UnityEngine.UI;


public class DungeonGUISelector : MonoBehaviour
{
    public DungeonSettings cfg; // Reference to the DungeonSettings ScriptableObject
    public TMP_Dropdown roomAlgorithmDropdown;
    public TMP_Dropdown tunnelsAlgorithmDropdown;
    public UnityEngine.UI.Button regenerateButton; // Button to trigger regeneration
    public DungeonGenerator generator;

    public void OnRegenerateClicked()
    {
        StartCoroutine(generator.RegenerateDungeon());
    }

    public void OnRoomAlgorithmSelected(int index)
    {
        string selected = roomAlgorithmDropdown.options[index].text;
        Debug.Log("Room Algorithm selected: " + selected);

        cfg.RoomAlgorithm = (DungeonSettings.DungeonAlgorithm_e)index;
        StartCoroutine(generator.RegenerateDungeon());
    }

    public void OnTunnelsAlgorithmSelected(int index)
    {
        string selected = tunnelsAlgorithmDropdown.options[index].text;
        Debug.Log("Tunnels Algorithm selected: " + selected);

        cfg.TunnelsAlgorithm = (DungeonSettings.TunnelsAlgorithm_e)index;
        StartCoroutine(generator.RegenerateDungeon());
    }

    void Start()
    {
        roomAlgorithmDropdown.value = (int)cfg.RoomAlgorithm;
        roomAlgorithmDropdown.onValueChanged.AddListener(OnRoomAlgorithmSelected);

        tunnelsAlgorithmDropdown.value = (int)cfg.TunnelsAlgorithm;
        tunnelsAlgorithmDropdown.onValueChanged.AddListener(OnTunnelsAlgorithmSelected);

        regenerateButton.onClick.AddListener(OnRegenerateClicked);
    }
}