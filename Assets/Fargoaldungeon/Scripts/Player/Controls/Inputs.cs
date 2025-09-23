using UnityEngine;

public partial class Player : MonoBehaviour
{
    public Vector2 GetMovementInput()
    {
        // 1) gather keyboard input -> desired world direction (XY)
        Vector2 input = new Vector2(
            Input.GetAxisRaw("Horizontal"),  // A/D or Left/Right
            Input.GetAxisRaw("Vertical")     // W/S or Up/Down
        );

        if (input.sqrMagnitude > 1f) input.Normalize();
        return input;
    }
}
