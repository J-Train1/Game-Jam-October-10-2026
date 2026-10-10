using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

// Scene changes behind a black screen, so the old scene never flashes back and the new one's first (hitchy)
// frames are hidden. Callers fade to black themselves first; this takes over from black:
//   black -> load the scene in the background -> let it settle for a moment -> fade into it.
// A small "LOADING" flickers in the corner if it takes a while.
public class SceneLoader : MonoBehaviour
{
    public static bool IsLoading { get; private set; }

    const float SettleTime = 0.35f;   // black after the new scene starts (shaders warm up, first frames hitch)
    const float FadeTime = 0.7f;

    string scene;
    float alpha = 1f, loadStart;
    bool fading;
    GUIStyle style;

    /// <summary>Change scene behind a black screen. Safe to call more than once; only the first call counts.</summary>
    public static void Load(string sceneName)
    {
        if (IsLoading) return;
        IsLoading = true;
        var go = new GameObject("SceneLoader");
        DontDestroyOnLoad(go);
        var l = go.AddComponent<SceneLoader>();
        l.scene = sceneName;
    }

    /// <summary>Reload the scene that's open now.</summary>
    public static void Reload() => Load(SceneManager.GetActiveScene().name);

    IEnumerator Start()
    {
        loadStart = Time.unscaledTime;
        // Whatever paused the game shouldn't follow us into the next scene.
        Time.timeScale = 1f;
        AudioListener.pause = false;

        yield return null; // one fully black frame first

        var op = SceneManager.LoadSceneAsync(scene);
        if (op == null) { Finish(); yield break; }
        while (!op.isDone) yield return null;

        // The new scene is running under the black. Give it a moment, then fade in.
        float t = 0f;
        int frames = 0;
        while (t < SettleTime || frames < 3) { t += Time.unscaledDeltaTime; frames++; yield return null; }

        fading = true;
        for (float f = 0f; f < FadeTime; f += Time.unscaledDeltaTime)
        {
            alpha = 1f - HorrorUI.Smooth(f / FadeTime);
            yield return null;
        }
        Finish();
    }

    void Finish()
    {
        IsLoading = false;
        Destroy(gameObject);
    }

    void OnDestroy() => IsLoading = false;

    void OnGUI()
    {
        GUI.depth = -100000; // above everything else drawn with OnGUI
        if (Event.current.type != EventType.Repaint) return;
        float sw = Screen.width, sh = Screen.height;
        HorrorUI.Fill(new Rect(0, 0, sw, sh), new Color(0f, 0f, 0f, alpha));

        // Only show the word if loading is actually taking a moment.
        float shown = Time.unscaledTime - loadStart;
        if (fading || shown < 0.6f) return;
        style ??= HorrorUI.Style(HorrorUI.SerifFont, TextAnchor.MiddleLeft);
        style.fontSize = HorrorUI.Px(30);
        float k = sh / 1080f;
        float a = Mathf.Clamp01((shown - 0.6f) / 0.5f) * (0.55f + 0.45f * Mathf.PerlinNoise(Time.unscaledTime * 6f, 1.7f));
        int dots = 1 + (int)(Time.unscaledTime * 2.5f) % 3;
        HorrorUI.Text(new Rect(sw - 260f * k, sh - 100f * k, 240f * k, 50f * k), "LOADING" + new string('.', dots), style, HorrorUI.A(HorrorUI.EmberDim, a), 0f, 2f * k);
    }
}
