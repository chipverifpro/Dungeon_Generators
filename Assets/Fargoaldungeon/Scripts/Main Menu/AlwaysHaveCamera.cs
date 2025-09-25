using UnityEngine;

public class AlwaysHaveCamera : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void EnsureCameraExists()
    {
        if (Camera.main == null)
        {
            var camGO = new GameObject("Main Camera");
            var cam = camGO.AddComponent<Camera>();
            camGO.tag = "MainCamera";

            // Nice defaults
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            cam.orthographic = true;
            cam.orthographicSize = 5;

            Debug.LogWarning("🐾 No camera was found — spawned a fallback Main Camera.");
        }
    }
}