using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

// Esc during play: the game freezes (time + sound), the screen darkens and RESUME / SETTINGS / QUIT TO MENU
// appear. Esc again (or RESUME) carries on. Not available during a jump scare, the heart screen, the ending or
// the win/lose screens. Added automatically by KeyHUD.
public class PauseMenu : MonoBehaviour
{
    public static PauseMenu Instance { get; private set; }
    public static bool IsPaused { get; private set; }

    static readonly string[] Labels = { "RESUME", "SETTINGS", "QUIT TO MENU" };

    SettingsPanel panel;
    bool settingsOpen, leaving;
    int selected, lastSelected = -1;
    float pausedAt, leaveStart;
    AudioSource src;
    GUIStyle titleStyle, itemStyle;
    static AudioClip tick;

    void Awake()
    {
        Instance = this;
        src = gameObject.AddComponent<AudioSource>();
        src.playOnAwake = false;
        src.spatialBlend = 0f;
        src.ignoreListenerPause = true; // its clicks still play while the game's sound is paused
        panel = new SettingsPanel(src) { OnBack = () => settingsOpen = false };
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (IsPaused) { IsPaused = false; Time.timeScale = 1f; AudioListener.pause = false; }
    }

    static bool CanPause()
    {
        var pc = PlayerController.Instance;
        return pc != null && pc.enabled && !PumpkinMonster.PlayerIsCaught && !DeathScreen.IsShowing
               && !WinScreen.IsShowing && !GraveFinale.IsPlaying;
    }

    void Update()
    {
        if (leaving)
        {
            if (Time.unscaledTime - leaveStart > 0.6f)
            {
                IsPaused = false; Time.timeScale = 1f; AudioListener.pause = false;
                SceneManager.LoadScene(EndChoice.MenuScene);
            }
            return;
        }

        var kb = Keyboard.current;
        if (kb == null) return;

        if (!IsPaused)
        {
            if (kb.escapeKey.wasPressedThisFrame && CanPause()) Pause();
            return;
        }

        if (settingsOpen || panel.Calibrating) { panel.Update(); return; }

        if (kb.escapeKey.wasPressedThisFrame) { Resume(); return; }
        if (kb.downArrowKey.wasPressedThisFrame || kb.sKey.wasPressedThisFrame) { selected = (selected + 1) % Labels.Length; Tick(); }
        if (kb.upArrowKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame) { selected = (selected + Labels.Length - 1) % Labels.Length; Tick(); }
        if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame) Choose(selected);
    }

    void Pause()
    {
        IsPaused = true;
        pausedAt = Time.unscaledTime;
        selected = 0;
        settingsOpen = false;
        Time.timeScale = 0f;
        AudioListener.pause = true;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        Tick();
    }

    void Resume()
    {
        IsPaused = false;
        settingsOpen = false;
        Time.timeScale = 1f;
        AudioListener.pause = false;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        GameSettings.Save();
    }

    void Choose(int i)
    {
        Tick();
        switch (i)
        {
            case 0: Resume(); break;
            case 1: settingsOpen = true; panel.ResetSelection(); break;
            case 2: leaving = true; leaveStart = Time.unscaledTime; GameSettings.Save(); break;
        }
    }

    void Tick()
    {
        if (src != null) src.PlayOneShot(tick ??= MakeTick(), 0.5f * GameSettings.Fx);
    }

    void OnGUI()
    {
        if (!IsPaused) return;
        GUI.depth = -3000;
        var e = Event.current;
        bool paint = e.type == EventType.Repaint;
        float sw = Screen.width, sh = Screen.height, k = sh / 1080f;
        float a = Mathf.Clamp01((Time.unscaledTime - pausedAt) / 0.25f);

        if (paint)
        {
            HorrorUI.Fill(new Rect(0, 0, sw, sh), new Color(0f, 0f, 0f, 0.7f * a));
            HorrorUI.Vignette(0.8f * a, Color.black);
        }

        if (settingsOpen || panel.Calibrating)
        {
            panel.OnGUI(sw * 0.5f - 430f * k, 60f * k, a);
        }
        else
        {
            if (titleStyle == null)
            {
                titleStyle = HorrorUI.Style(HorrorUI.TitleFont);
                itemStyle = HorrorUI.Style(HorrorUI.SerifFont);
            }
            titleStyle.fontSize = HorrorUI.Px(120);
            itemStyle.fontSize = HorrorUI.Px(50);
            if (paint)
                HorrorUI.Shaky(new Rect(0, sh * 0.2f, sw, 140f * k), "PAUSED", titleStyle, HorrorUI.A(HorrorUI.Ember, a), Time.unscaledTime, 1.5f * k, 8f * k, 2f * k, 2f, 0.08f, 6);

            for (int i = 0; i < Labels.Length; i++)
            {
                var size = itemStyle.CalcSize(new GUIContent(Labels[i]));
                var r = new Rect((sw - size.x) * 0.5f, sh * 0.45f + i * 84f * k, size.x, 64f * k);
                var hit = new Rect(r.x - 50f * k, r.y - 6f * k, r.width + 100f * k, r.height + 12f * k);
                if (!leaving && hit.Contains(e.mousePosition))
                {
                    if (selected != i) { selected = i; }
                    if (e.type == EventType.MouseDown && e.button == 0) { Choose(i); e.Use(); }
                }
                if (!paint) continue;
                bool sel = selected == i;
                if (sel)
                {
                    float flick = 0.75f + 0.25f * Mathf.PerlinNoise(Time.unscaledTime * 9f, i);
                    HorrorUI.Glow(new Rect(r.x - 36f * k, r.center.y - 14f * k, 28f * k, 28f * k), HorrorUI.A(HorrorUI.Ember, a * flick));
                    HorrorUI.Glow(new Rect(r.x - 50f * k, r.center.y - 28f * k, 56f * k, 56f * k), HorrorUI.A(HorrorUI.Ember, 0.35f * a * flick));
                    HorrorUI.Shaky(r, Labels[i], itemStyle, HorrorUI.A(HorrorUI.Ember, a), Time.unscaledTime, 1.2f * k, 0f, 1.5f * k, 2f, 0.08f, 30 + i);
                }
                else
                    HorrorUI.Text(r, Labels[i], itemStyle, HorrorUI.A(HorrorUI.Bone, 0.8f * a), 0f, 2f * k);
            }
            if (selected != lastSelected) { lastSelected = selected; if (!paint) Tick(); }
        }

        if (leaving && paint)
            HorrorUI.Fill(new Rect(0, 0, sw, sh), new Color(0f, 0f, 0f, HorrorUI.Smooth((Time.unscaledTime - leaveStart) / 0.5f)));
    }

    static AudioClip MakeTick()
    {
        const int sr = 44100;
        int n = sr / 16;
        var d = new float[n];
        var rng = new System.Random(11);
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)sr;
            d[i] = (Mathf.Sin(2f * Mathf.PI * 140f * t) * 0.6f + (float)(rng.NextDouble() * 2 - 1) * 0.25f) * Mathf.Exp(-t * 90f) * 0.5f;
        }
        var c = AudioClip.Create("PauseTick", n, 1, sr, false);
        c.SetData(d, 0);
        return c;
    }
}
