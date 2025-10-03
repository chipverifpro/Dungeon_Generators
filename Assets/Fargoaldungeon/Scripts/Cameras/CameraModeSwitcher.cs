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
    //public Transform player;
    public Player player;
    public float height = 20f;
    public bool playerVisible = true;

    private Coroutine waiter = null;

    void Update()
    {
        if (Input.GetKeyDown(toggleKey))
        {
            current_camera = (current_camera + 1) % 3;
            vcamTop.Priority = 0;
            vcamFP.Priority = 0;
            vcamOverhead.Priority = 0;
            playerVisible = true;
            player.camera_refresh_needed = true;

            switch (current_camera)
            {
                case 0:
                    vcamTop.Priority = 10;
                    break;
                case 1:
                    vcamFP.Priority = 10;
                    //playerVisible = false;   // hide player in first person mode
                    break;
                case 2:
                    vcamOverhead.Priority = 10;
                    break;
            }
        }

        if (player.camera_refresh_needed)
        {
            //if (waiter!=null) StopCoroutine(waiter);  // in case WaitForArrival was already running, kill it.

            playerVisible = (vcamFP.Priority==10) ? false : true; // hide player in first person mode

            if (!playerVisible)
            {
                // Wait for camera to arrive at first person before disabling player visibility
                waiter = StartCoroutine(WaitForArrival(vcamFP, onArrived: onArrivedAtFP));
            }
            else
            {
                //playerModel.SetActive(true);
                player.agent.DogPrefab.SetActive(true);
            }
            player.camera_refresh_needed = false;
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
        //playerModel.SetActive(false);
        player.agent.DogPrefab.SetActive(playerVisible);
        return;
    }




    void LateUpdate()
    {
        // all cameras point to current agent
        if (player == null) return;
        vcamTop.transform.position = new Vector3(
            player.agent.transform.position.x,
            player.agent.height,
            player.agent.transform.position.z
        );
        // top camera override angle so north is top of screen
        vcamTop.transform.rotation = Quaternion.Euler(90f, 0f, 0f); // always north up
    }
}
