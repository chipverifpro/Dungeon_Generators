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
        if (Input.GetKeyDown(KeyCode.Alpha0)) ChangePlayerAgentByNum(0);
        if (Input.GetKeyDown(KeyCode.Alpha1)) ChangePlayerAgentByNum(1);
        if (Input.GetKeyDown(KeyCode.Alpha2)) ChangePlayerAgentByNum(2);
        if (Input.GetKeyDown(KeyCode.Alpha3)) ChangePlayerAgentByNum(3);
        if (Input.GetKeyDown(KeyCode.Alpha4)) ChangePlayerAgentByNum(4);
        if (Input.GetKeyDown(KeyCode.Alpha5)) ChangePlayerAgentByNum(5);
        if (Input.GetKeyDown(KeyCode.Alpha6)) ChangePlayerAgentByNum(6);
        if (Input.GetKeyDown(KeyCode.Alpha7)) ChangePlayerAgentByNum(7);
        if (Input.GetKeyDown(KeyCode.Alpha8)) ChangePlayerAgentByNum(8);
        if (Input.GetKeyDown(KeyCode.Alpha9)) ChangePlayerAgentByNum(9);

    }
}