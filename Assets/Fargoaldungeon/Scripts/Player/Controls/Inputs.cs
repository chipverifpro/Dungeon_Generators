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
    }
}