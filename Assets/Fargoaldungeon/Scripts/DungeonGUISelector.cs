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
    public CellularAutomata ca; // Reference to the CellularAutomata script

    public void OnRegenerateClicked()
    {
        //generator.StopAllCoroutines();
        //ca.StopAllCoroutines();
        StartCoroutine(generator.RegenerateDungeon());
    }

    public void OnRoomAlgorithmSelected(int index)
    {
        string selected = roomAlgorithmDropdown.options[index].text;
        Debug.Log("Room Algorithm selected: " + selected);

        cfg.RoomAlgorithm = (DungeonSettings.RoomAlgorithm_e)index;
        //generator.StopAllCoroutines();
        //ca.StopAllCoroutines();
        //Start();
        StartCoroutine(generator.RegenerateDungeon());
    }

    public void OnTunnelsAlgorithmSelected(int index)
    {
        string selected = tunnelsAlgorithmDropdown.options[index].text;
        Debug.Log("Tunnels Algorithm selected: " + selected);

        cfg.TunnelsAlgorithm = (DungeonSettings.TunnelsAlgorithm_e)index;
        //generator.StopAllCoroutines();
        //ca.StopAllCoroutines();
        //Start();
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