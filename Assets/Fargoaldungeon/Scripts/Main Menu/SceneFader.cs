using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneFader : MonoBehaviour
{
    public static SceneFader Instance;

    [Header("UI (optional)")]
    public CanvasGroup splashCanvasGroup;
    public CanvasGroup menuCanvasGroup;

    [Header("Timing")]
    public float minSplashSeconds = 1.5f;  // brief pause
    public float fadeDuration = 5f;       // cross fade duration

    [Header("Debug/UX")]
    public bool allowSkip = true;          // press any key / click to skip after min time

    static bool first_run = true;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            DontDestroyOnLoad(menuCanvasGroup);
            DontDestroyOnLoad(splashCanvasGroup);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void OnDestroy()
    {
        first_run = true;   // put this back
    }

    void Start()
    {
        StartCoroutine (CrossFade());
    }

    private IEnumerator CrossFade()
    {
        BottomBanner.Show("🐾 Welcome, Pup! Sniffing out treasures...");

        //if (first_run)
        //{
            // Display just the splash screen.
            splashCanvasGroup.alpha = 1;
            menuCanvasGroup.alpha = 0;

            // display splash screen for a bit.  Press any key to skip.
            yield return StartCoroutine(WaitAllowSkip(minSplashSeconds));
            
            // Fade out splash
            StartCoroutine(Fade(splashCanvasGroup, 1f, 0f));
            // Simultaneously fade in menu...
            yield return StartCoroutine(Fade(menuCanvasGroup, 0f, 1f));

            //first_run = false;
        //}
        //else // not first run
        //{
            // start with black screen, no splash screen.
        //    splashCanvasGroup.alpha = 0f; // hide spash picture always.
        //    menuCanvasGroup.alpha = 0f;   // hide menu picture initially.
            // Just fade in menu...
        //    yield return StartCoroutine(Fade(menuCanvasGroup, 0f, 1f));
        //}
    }
    public IEnumerator WaitAllowSkip(float minSplashSeconds)
    {
        float t = 0f;
        while (t < minSplashSeconds)
        {
            t += Time.deltaTime;
            // Optional: allow skip after min time
            if (allowSkip && t >= minSplashSeconds && (Input.anyKeyDown || Input.GetMouseButtonDown(0)))
            {
                break;  // skip remaining initial title time and begin crossfade
            }
            yield return null;
        }
        yield return null;
    }
    
    private IEnumerator Fade(CanvasGroup canvasGroup, float startAlpha, float targetAlpha)
    {
        canvasGroup.blocksRaycasts = true; // prevent clicks during fade

        float fadePct = 0f;

        while (fadePct < 1f)
        {
            fadePct += Time.deltaTime / fadeDuration;
            canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, fadePct);
            yield return null;
        }

        canvasGroup.alpha = targetAlpha;    // done, make sure fade is complete.
        canvasGroup.blocksRaycasts = (targetAlpha != 0f);
        yield break;
    }
}