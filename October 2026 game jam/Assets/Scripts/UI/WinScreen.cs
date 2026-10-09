using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

// Shown when the player touches the coffin: controls off, the pumpkins stop, the HUD hides, the screen slowly
// fades to black, then "YOU ESCAPED" trembles in over an ember glow, with a line of narration, the time spent
// in the corn and the hearts you kept. Then PLAY AGAIN (new maze) / BACK TO MENU buttons.
// Created on demand (WinScreen.Show()); no scene setup.
public class WinScreen : MonoBehaviour
{
    public static bool IsShowing { get; private set; }

    public float fadeTime = 2.2f;
    public float textDelay = 0.4f;
    public float promptDelay = 1.6f;

    float start, escapeTime;
    int heartsLeft, heartsMax;
    string line;
    GUIStyle title, sub, stats, prompt;
    bool canRestart, fromBlack, choirPlayed;
    EndChoice choice;

    static readonly string[] Lines =
    {
        "The corn lets you go... this time.",
        "Behind you, the field goes quiet.",
        "Somewhere back there, something is still searching.",
        "You made it out. It will remember you.",
    };

    public static void Show() => Show(false, -1f);

    /// <summary>fromBlack: the screen is already black (after the ending scare), so skip the slow fade.
    /// escapeTime: seconds to show as the time in the corn (-1 = time since the level loaded).</summary>
    public static void Show(bool fromBlack, float escapeTime)
    {
        if (IsShowing) return;
        pendingFromBlack = fromBlack;
        pendingEscapeTime = escapeTime;
        var go = new GameObject("WinScreen");
        go.AddComponent<WinScreen>();
    }

    static bool pendingFromBlack;
    static float pendingEscapeTime = -1f;

    void Awake()
    {
        IsShowing = true;
        start = Time.unscaledTime;
        fromBlack = pendingFromBlack;
        if (fromBlack) start -= fadeTime - 0.9f; // already black: hold a beat of darkness, then the title
        escapeTime = pendingEscapeTime >= 0f ? pendingEscapeTime : Time.timeSinceLevelLoad;
        pendingFromBlack = false; pendingEscapeTime = -1f;
        line = Lines[Random.Range(0, Lines.Length)];
        var lives = FindFirstObjectByType<PlayerLives>();
        heartsLeft = lives != null ? lives.Remaining : 0;
        heartsMax = lives != null ? lives.Max : 0;

        if (PlayerController.Instance != null) { PlayerController.Instance.InputEnabled = false; PlayerController.Instance.enabled = false; } // so clicking a button doesn't re-grab the mouse
        var hud = FindFirstObjectByType<FlashlightHUD>();
        if (hud != null) hud.gameObject.SetActive(false);
        foreach (var m in PumpkinMonster.All.ToArray())
        {
            if (m == null) continue;
            m.enabled = false;
            var a = m.GetComponentInChildren<Animator>();
            if (a != null) a.speed = 0f;
        }
    }

    void OnDestroy() => IsShowing = false;

    void Update()
    {
        float t = Time.unscaledTime - start;
        if (!choirPlayed && t >= fadeTime - 0.4f)
        {
            choirPlayed = true;
            var c = GameAudio.Get("Win_Choir");
            if (c != null)
            {
                var s = gameObject.AddComponent<AudioSource>();
                s.playOnAwake = false; s.spatialBlend = 0f;
                s.PlayOneShot(c, 0.8f * GameSettings.Amb);
            }
        }
        if (!canRestart && t >= fadeTime + textDelay + promptDelay) canRestart = true;
        if (choice == null)
        {
            var src = gameObject.AddComponent<AudioSource>();
            src.playOnAwake = false; src.spatialBlend = 0f;
            choice = new EndChoice(src);
        }
        choice.Update(canRestart);
        if (choice.Leaving) IsShowing = false;
    }

    void OnGUI()
    {
        GUI.depth = -2000;
        float t = Time.unscaledTime - start;
        float sw = Screen.width, sh = Screen.height, k = sh / 1080f;

        float fade = fromBlack ? 1f : HorrorUI.Smooth(t / fadeTime);
        HorrorUI.Fill(new Rect(0, 0, sw, sh), new Color(0f, 0f, 0f, fade));

        if (title == null)
        {
            title = HorrorUI.Style(HorrorUI.TitleFont);
            sub = HorrorUI.Style(HorrorUI.SerifFont);
            stats = HorrorUI.Style(HorrorUI.TypeFont);
            prompt = HorrorUI.Style(HorrorUI.TypeFont);
        }
        title.fontSize = HorrorUI.Px(124);
        sub.fontSize = HorrorUI.Px(34);
        stats.fontSize = HorrorUI.Px(26);
        prompt.fontSize = HorrorUI.Px(23);

        float a1 = Mathf.Clamp01((t - fadeTime) / 1.4f);
        float a2 = Mathf.Clamp01((t - fadeTime - textDelay - 0.5f) / 1.0f);
        float a3 = Mathf.Clamp01((t - fadeTime - textDelay - 1.1f) / 1.0f);
        float a4 = Mathf.Clamp01((t - fadeTime - textDelay - promptDelay) / 0.8f);

        float ty = sh * 0.3f;

        // Ember haze breathing behind the title, like a lantern behind fog.
        float breathe = 0.85f + 0.15f * Mathf.Sin(t * 1.3f);
        HorrorUI.Glow(new Rect(sw * 0.12f, ty - 170 * k, sw * 0.76f, 480 * k), HorrorUI.A(HorrorUI.Ember, 0.16f * a1 * breathe));
        HorrorUI.Glow(new Rect(sw * 0.3f, ty - 60 * k, sw * 0.4f, 260 * k), HorrorUI.A(HorrorUI.Ember, 0.12f * a1 * breathe));

        // Title rises a touch as it fades in.
        float rise = (1f - HorrorUI.Smooth(a1)) * 30f * k;
        HorrorUI.Shaky(new Rect(0, ty + rise, sw, 150 * k), "YOU ESCAPED", title, HorrorUI.A(HorrorUI.Ember, a1),
                       t, 1.6f * k, 8f * k, 2.5f * k, 2f, 0.1f, 5);

        HorrorUI.Text(new Rect(0, ty + 160 * k, sw, 50 * k), line, sub, HorrorUI.A(HorrorUI.Bone, a2 * 0.9f), 0f, 2f * k);

        // Divider: a thin scratched line.
        float divW = 360 * k * HorrorUI.Smooth(a3);
        HorrorUI.Fill(new Rect((sw - divW) * 0.5f, ty + 232 * k, divW, Mathf.Max(1f, 1.5f * k)), HorrorUI.A(HorrorUI.EmberDim, a3 * 0.8f));

        int mins = Mathf.FloorToInt(escapeTime / 60f), secs = Mathf.FloorToInt(escapeTime % 60f);
        string statLine = $"time in the corn   {mins}:{secs:00}";
        if (heartsMax > 0) statLine += $"        hearts kept   {heartsLeft} / {heartsMax}";
        HorrorUI.Text(new Rect(0, ty + 252 * k, sw, 40 * k), statLine, stats, HorrorUI.A(HorrorUI.Ash, a3), 0f, 2f * k);

        HorrorUI.Vignette(0.9f * fade, Color.black);
        HorrorUI.Grain(0.07f * fade);
        if (choice != null)
        {
            choice.OnGUI(sh * 0.78f, a4, canRestart);
            choice.DrawFadeOut();
        }
    }
}
