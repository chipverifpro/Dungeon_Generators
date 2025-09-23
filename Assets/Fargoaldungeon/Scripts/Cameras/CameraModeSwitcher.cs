using Cinemachine;
using UnityEngine;

public class CameraModeSwitcher : MonoBehaviour
{
    public CinemachineVirtualCamera vcamFP, vcamTop;
    public KeyCode toggleKey = KeyCode.Tab;

    void Update()
    {
        if (Input.GetKeyDown(toggleKey))
        {
            bool topActive = vcamTop.Priority > vcamFP.Priority;
            vcamTop.Priority = topActive ? 0 : 10;
            vcamFP.Priority  = topActive ? 10 : 0;
        }
    }
}
