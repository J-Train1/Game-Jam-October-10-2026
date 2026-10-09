using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

// The two choices at the end of a run (GAME OVER and YOU ESCAPED): PLAY AGAIN (a new maze) and BACK TO MENU.
// Styled like the main menu: the highlighted one turns ember, trembles and gets a candle glow beside it.
// Mouse (hover + click) or keyboard (A/D, W/S or arrows to switch, Enter/Space to pick).
// Usage from a screen: call Update(active) every frame, OnGUI(...) from its OnGUI, and DrawFadeOut() last.
public class EndChoice
{
    public static readonly string[] Labels = { "PLAY AGAIN", "BACK TO MENU" };
    public const string MenuScene = "MainMenu";

    public int Selected { get; private set; } = 0;
    public bool Leaving { get; private set; }

    readonly AudioSource src;
    GUIStyle style;
    float leaveStart, shownAt = -1f;
    int chosen, lastSelected = -1;
    static AudioClip tick, thunk;

    public EndChoice(AudioSource audioSource) { src = audioSource; }

    /// <summary>Frees the mouse so the buttons can be clicked (the game keeps it locked to the view).</summary>
    public static void FreeCursor()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void Update(bool active)
    {
        if (Leaving)
        {
            if (Time.unscaledTime - leaveStart > 0.75f) Load();
            return;
        }
        if (!active) return;
        if (shownAt < 0f) { shownAt = Time.unscaledTime; FreeCursor(); }

        var kb = Keyboard.current;
        if (kb == null) return;
        bool prev = kb.leftArrowKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame || kb.upArrowKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame;
        bool next = kb.rightArrowKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame || kb.downArrowKey.wasPressedThisFrame || kb.sKey.wasPressedThisFrame;
        if (prev || next) { Selected = (Selected + (next ? 1 : Labels.Length - 1)) % Labels.Length; Hovered(); }
        if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame) Choose(Selected);
    }

    public void Choose(int i)
    {
        if (Leaving) return;
        Leaving = true;
        chosen = i;
        leaveStart = Time.unscaledTime;
        if (src != null) src.PlayOneShot(thunk ??= MakeTick("EndThunk", 0.5f, 55f, 0.6f), 0.9f * GameSettings.Fx);
    }

    void Load()
    {
        if (chosen == 1 && Application.CanStreamedLevelBeLoaded(MenuScene)) SceneManager.LoadScene(MenuScene);
        else SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    void Hovered()
    {
        if (Selected == lastSelected) return;
        lastSelected = Selected;
        if (src != null) src.PlayOneShot(tick ??= MakeTick("EndTick", 0.06f, 140f, 0.25f), 0.6f * GameSettings.Fx);
    }

    /// <summary>Draws the two buttons side by side, centred on centerY. alpha fades them in.</summary>
    public void OnGUI(float centerY, float alpha, bool active)
    {
        if (alpha <= 0.002f) return;
        float sw = Screen.width, k = Screen.height / 1080f;
        if (style == null) style = HorrorUI.Style(HorrorUI.SerifFont, TextAnchor.MiddleLeft);
        style.fontSize = HorrorUI.Px(44);
        var e = Event.current;
        bool paint = e.type == EventType.Repaint;

        float gap = 130f * k, h = 60f * k;
        var sizes = new float[Labels.Length];
        float total = gap * (Labels.Length - 1);
        for (int i = 0; i < Labels.Length; i++) { sizes[i] = style.CalcSize(new GUIContent(Labels[i])).x; total += sizes[i]; }
        float x = (sw - total) * 0.5f;
        float a = alpha * (Leaving ? 1f - Mathf.Clamp01((Time.unscaledTime - leaveStart) / 0.5f) : 1f);

        for (int i = 0; i < Labels.Length; i++)
        {
            var r = new Rect(x, centerY - h * 0.5f, sizes[i] + 12f * k, h);
            var hit = new Rect(r.x - 40f * k, r.y - 8f * k, r.width + 60f * k, r.height + 16f * k);
            if (active && !Leaving && alpha > 0.5f && hit.Contains(e.mousePosition))
            {
                if (Selected != i) { Selected = i; Hovered(); }
                if (e.type == EventType.MouseDown && e.button == 0) { Choose(i); e.Use(); }
            }
            if (paint)
            {
                if (Selected == i && active)
                {
                    float flick = 0.75f + 0.25f * Mathf.PerlinNoise(Time.time * 9f, i);
                    HorrorUI.Glow(new Rect(r.x - 30f * k, r.center.y - 14f * k, 28f * k, 28f * k), HorrorUI.A(HorrorUI.Ember, a * flick));
                    HorrorUI.Glow(new Rect(r.x - 44f * k, r.center.y - 28f * k, 56f * k, 56f * k), HorrorUI.A(HorrorUI.Ember, 0.35f * a * flick));
                    HorrorUI.Fill(new Rect(r.x, r.yMax - 6f * k, sizes[i] * (0.6f + 0.4f * Mathf.PingPong(Time.time * 0.8f, 1f)), Mathf.Max(1f, 2f * k)),
                                  HorrorUI.A(HorrorUI.EmberDim, a * 0.9f));
                    float w = sizes[i] + 3f * k * (Labels[i].Length - 1);
                    HorrorUI.Shaky(new Rect(r.x, r.y, w, r.height), Labels[i], style, HorrorUI.A(HorrorUI.Ember, a), Time.time,
                                   1.2f * k, 3f * k, 1.5f * k, 2f, 0.08f, 20 + i);
                }
                else
                    HorrorUI.Text(r, Labels[i], style, HorrorUI.A(HorrorUI.Bone, a * 0.75f), 0f, 2f * k);
            }
            x += sizes[i] + gap;
        }
    }

    /// <summary>Black over everything while leaving (call last in OnGUI).</summary>
    public void DrawFadeOut()
    {
        if (!Leaving || Event.current.type != EventType.Repaint) return;
        float f = HorrorUI.Smooth((Time.unscaledTime - leaveStart) / 0.6f);
        HorrorUI.Fill(new Rect(0, 0, Screen.width, Screen.height), new Color(0f, 0f, 0f, f));
    }

    // Placeholder click sounds (swap for real ones later).
    static AudioClip MakeTick(string name, float seconds, float freq, float noise)
    {
        const int sr = 44100;
        int n = Mathf.RoundToInt(sr * seconds);
        var d = new float[n];
        var rng = new System.Random(5);
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)sr;
            float env = Mathf.Exp(-t * (6f / seconds));
            d[i] = (Mathf.Sin(2f * Mathf.PI * freq * t) * 0.6f + (float)(rng.NextDouble() * 2 - 1) * noise) * env * 0.5f;
        }
        var c = AudioClip.Create(name, n, 1, sr, false);
        c.SetData(d, 0);
        return c;
    }
}
