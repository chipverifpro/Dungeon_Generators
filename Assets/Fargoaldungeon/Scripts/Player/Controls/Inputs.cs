using UnityEngine;

public partial class Player : MonoBehaviour
{
    void Input_Update()
    {
        // 1) Input: A/D rotate, W/S move forward/back
        float turn = Input.GetAxisRaw("Horizontal"); // A/D
        float thrust = Input.GetAxisRaw("Vertical"); // W/S

        if ((Mathf.Abs(turn) > 1e-10f) || (Mathf.Abs(thrust) > 1e-10f))   // reject tiny movements
        {
            Move_Update(turn, thrust);  // Only needed if we want to move
        }

        // Change the agent controllef by the player using the number keys
        if (Input.GetKeyDown(KeyCode.Alpha0)) ChangePlayerAgentById(0);
        if (Input.GetKeyDown(KeyCode.Alpha1)) ChangePlayerAgentById(1);
        if (Input.GetKeyDown(KeyCode.Alpha2)) ChangePlayerAgentById(2);
        if (Input.GetKeyDown(KeyCode.Alpha3)) ChangePlayerAgentById(3);
        if (Input.GetKeyDown(KeyCode.Alpha4)) ChangePlayerAgentById(4);
        if (Input.GetKeyDown(KeyCode.Alpha5)) ChangePlayerAgentById(5);
        if (Input.GetKeyDown(KeyCode.Alpha6)) ChangePlayerAgentById(6);
        if (Input.GetKeyDown(KeyCode.Alpha7)) ChangePlayerAgentById(7);
        if (Input.GetKeyDown(KeyCode.Alpha8)) ChangePlayerAgentById(8);
        if (Input.GetKeyDown(KeyCode.Alpha9)) ChangePlayerAgentById(9);
    }
}