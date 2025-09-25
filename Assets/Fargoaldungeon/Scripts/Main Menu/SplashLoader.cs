using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SplashLoader : MonoBehaviour
{
    [Header("Timing")]
    public float minSplashSeconds = 1.5f;  // brief pause
    public float fadeSeconds = 0.6f;       // fade out duration

    [Header("UI (optional)")]
    public CanvasGroup splashCanvas;       // assign if you want fade; else leave null

    [Header("Debug/UX")]
    public bool allowSkip = true;          // press any key / click to skip after min time

    void Start()
    {
        StartCoroutine(LoadMenuRoutine());
    }

    IEnumerator LoadMenuRoutine()
    {
        // Start async load of MenuScene in background
        AsyncOperation op = SceneManager.LoadSceneAsync("MainMenu");
        op.allowSceneActivation = false;

        float t = 0f;
        // Wait at least the minimum time while it loads
        while (t < minSplashSeconds || op.progress < 0.9f)
        {
            t += Time.deltaTime;
            // Optional: allow skip after min time
            if (allowSkip && t >= minSplashSeconds && (Input.anyKeyDown || Input.GetMouseButtonDown(0)))
                break;
            yield return null;
        }

        // Optional fade-out
        if (splashCanvas != null && fadeSeconds > 0f)
        {
            float f = 0f;
            float start = splashCanvas.alpha;
            while (f < 1f)
            {
                f += Time.deltaTime / fadeSeconds;
                splashCanvas.alpha = Mathf.Lerp(start, 0f, f);
                yield return null;
            }
        }

        // Go to Menu
        op.allowSceneActivation = true;
    }
}