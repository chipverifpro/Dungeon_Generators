using Cinemachine;
using UnityEngine;

public class CameraModeSwitcher : MonoBehaviour
{
    public CinemachineVirtualCamera vcamFP, vcamTop;
    public GameObject playerModel;
    public KeyCode toggleKey = KeyCode.Tab;

    void Update()
    {
        if (Input.GetKeyDown(toggleKey))
        {
            bool topActive = vcamTop.Priority > vcamFP.Priority;
            vcamTop.Priority = topActive ? 0 : 10;
            vcamFP.Priority = topActive ? 10 : 0;

            // Hide/show player mesh depending on mode
            if (playerModel != null)
            {
                // If switching into FP (topActive == true), hide the model
                playerModel.SetActive(topActive ? false : true);
            }

        }
    }
    public Transform player;
    public float height = 20f;

    void LateUpdate()
    {
        if (player == null) return;
        vcamTop.transform.position = new Vector3(
            player.position.x,
            height,
            player.position.z
        );
        vcamTop.transform.rotation = Quaternion.Euler(90f, 0f, 0f); // always north up
    }
}
