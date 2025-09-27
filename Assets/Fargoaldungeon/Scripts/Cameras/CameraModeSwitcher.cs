using System.Collections;
using Cinemachine;
using UnityEngine;

public class CameraModeSwitcher : MonoBehaviour
{
    public CinemachineBrain brain;
    public CinemachineVirtualCamera vcamFP, vcamTop, vcamOverhead;
    public GameObject playerModel;
    public KeyCode toggleKey = KeyCode.Tab;
    int current_camera;
    public Transform player;
    public float height = 20f;

    void Update()
    {
        if (Input.GetKeyDown(toggleKey))
        {
            current_camera = (current_camera + 1) % 3;
            vcamTop.Priority = 0;
            vcamFP.Priority = 0;
            vcamOverhead.Priority = 0;
            bool playerVisible = true;

            switch (current_camera)
            {
                case 0:
                    vcamTop.Priority = 10;
                    break;
                case 1:
                    vcamFP.Priority = 10;
                    playerVisible = false;   // hide player in first person mode
                    break;
                case 2:
                    vcamOverhead.Priority = 10;
                    break;
            }

            if (!playerVisible)
            {
                // Wait for camera to arrive at first person before disabling player visibility
                StartCoroutine(WaitForArrival(vcamFP, onArrived: onArrivedAtFP));
            } else {
                playerModel.SetActive(true);
            }
        }
    }

    IEnumerator WaitForArrival(ICinemachineCamera target, System.Action onArrived)
    {
        // let priorities propagate one frame
        yield return null;

        // Wait until the brain is not blending AND our target is actually live
        while (brain.ActiveBlend != null || !CinemachineCore.Instance.IsLive(target))
            yield return null;

        onArrived?.Invoke();
    }

    void onArrivedAtFP()
    {
        playerModel.SetActive(false);
        return;
    }




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
