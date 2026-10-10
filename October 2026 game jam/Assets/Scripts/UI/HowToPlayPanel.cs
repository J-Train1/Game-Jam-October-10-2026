using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// HOW TO PLAY: a few pages, each with a photograph of the game (Assets/Resources/HowToPlay/Shot_*.jpg) and a short
// explanation. Used by the main menu and the pause menu, drawn in the same style as the settings.
// Mouse: PREV / NEXT / BACK, or click a page dot. Keyboard: A / D or left / right to turn pages, Esc to go back.
// The photographs are made with Tools > How To Play > Capture Screenshots (in Play Mode, in the Maze scene).
public class HowToPlayPanel
{
    public System.Action OnBack;

    class Page
    {
        public string title, shot, caption;
        public string[] lines;
        public string keysTitle;
        public string[] keys;   // "CAP,CAP,/,CAP|what it does"  (caps side by side; "/" is drawn as "or")
    }

    static readonly Page[] Pages =
    {
        new Page
        {
            title = "THE GOAL", shot = "Shot_Maze", caption = "fig. 1 — the rows, after dark",
            lines = new[]
            {
                "You wake somewhere in a corn maze, in the dead of night.",
                "Rusted keys are hidden at the ends of its rows. Find every one.",
                "Then find the iron gate, unlock it, and get out.",
            },
            keys = new[] { "W,A,S,D|walk", "MOUSE|look around" },
        },
        new Page
        {
            title = "THE KEYS", shot = "Shot_Key", caption = "fig. 2 — something glinting at a dead end",
            lines = new[]
            {
                "Keys glint in the dark at dead ends. Walk into one to take it.",
                "The keys you carry are shown on your screen.",
                "But every key you take wakes another one of them.",
            },
        },
        new Page
        {
            title = "YOUR LIGHT", shot = "Shot_Freeze", caption = "fig. 3 — it cannot move while you watch it",
            lines = new[]
            {
                "They cannot move while your flashlight is on them. Keep the beam on one and back away.",
                "The battery drains while the light is on and recharges while it's off. If it dies, it won't come back right away.",
            },
            keys = new[] { "F,/,RIGHT MOUSE|flashlight on / off" },
        },
        new Page
        {
            title = "STAY QUIET", shot = "Shot_Hunt", caption = "fig. 4 — they hear more than they see",
            lines = new[]
            {
                "Walking is silent. Running is not: they hear it, and they will come.",
                "Sprinting uses your breath. Run out, and you'll have to stop and gasp for air.",
            },
            keys = new[] { "SHIFT|sprint" },
        },
        new Page
        {
            title = "THE GATE", shot = "Shot_Gate", caption = "fig. 5 — the only way out",
            lines = new[]
            {
                "The gate is held shut with one lock for every key. Walk up to it to use the keys you carry.",
                "Once it opens, follow the candles out of the field... and don't stop.",
            },
        },
        new Page
        {
            title = "THREE HEARTS", shot = "Shot_Dark", caption = "fig. 6 — without the light",
            lines = new[]
            {
                "If one of them catches you, you lose a heart and wake back at the start.",
                "Lose all three, and the field keeps you.",
            },
            keysTitle = "CONTROLS",
            keys = new[] { "W,A,S,D|walk", "MOUSE|look", "SHIFT|sprint", "F,/,RIGHT MOUSE|flashlight", "ESC|pause" },
        },
    };

    static readonly string[] Roman = { "I", "II", "III", "IV", "V", "VI", "VII", "VIII" };
    static readonly Dictionary<string, Texture2D> shots = new Dictionary<string, Texture2D>();

    int page, hover = -1, lastHover = -1;
    float pageTime;
    readonly AudioSource src;
    static AudioClip tick;
    GUIStyle headStyle, titleStyle, bodyStyle, captionStyle, capStyle, navStyle, smallStyle;

    public HowToPlayPanel(AudioSource audioSource) { src = audioSource; }

    /// <summary>Start from the first page.</summary>
    public void Open() { page = 0; pageTime = Time.unscaledTime; hover = lastHover = -1; }

    static Texture2D Shot(string name)
    {
        if (!shots.TryGetValue(name, out var t))
        {
            t = Resources.Load<Texture2D>("HowToPlay/" + name);
            shots[name] = t;
        }
        return t;
    }

    void Go(int p)
    {
        p = Mathf.Clamp(p, 0, Pages.Length - 1);
        if (p == page) return;
        page = p;
        pageTime = Time.unscaledTime;
        Tick();
    }

    void Next() { if (page < Pages.Length - 1) Go(page + 1); else Back(); }
    void Prev() => Go(page - 1);
    void Back() { Tick(); OnBack?.Invoke(); }

    // ---------------- keyboard ----------------

    public void Update()
    {
        var kb = Keyboard.current;
        if (kb == null) return;
        if (kb.escapeKey.wasPressedThisFrame || kb.backspaceKey.wasPressedThisFrame) { Back(); return; }
        if (kb.rightArrowKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame) { if (page < Pages.Length - 1) Go(page + 1); }
        if (kb.leftArrowKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame) Prev();
        if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame) Next();
    }

    void Tick()
    {
        if (src != null) src.PlayOneShot(tick ??= MakeTick(), 0.5f * GameSettings.Fx);
    }

    // ---------------- drawing ----------------

    void Styles()
    {
        if (headStyle != null) return;
        headStyle = HorrorUI.Style(HorrorUI.TitleFont, TextAnchor.MiddleLeft);
        titleStyle = HorrorUI.Style(HorrorUI.SerifFont, TextAnchor.MiddleLeft);
        bodyStyle = HorrorUI.Style(HorrorUI.TypeFont, TextAnchor.UpperLeft);
        bodyStyle.wordWrap = true;
        captionStyle = HorrorUI.Style(HorrorUI.TypeFont, TextAnchor.MiddleLeft);
        capStyle = HorrorUI.Style(HorrorUI.TypeFont, TextAnchor.MiddleCenter);
        navStyle = HorrorUI.Style(HorrorUI.SerifFont, TextAnchor.MiddleCenter);
        smallStyle = HorrorUI.Style(HorrorUI.TypeFont, TextAnchor.MiddleCenter);
    }

    /// <summary>Draws the whole screen. alpha fades it in.</summary>
    public void OnGUI(float alpha)
    {
        Styles();
        var e = Event.current;
        bool paint = e.type == EventType.Repaint;
        float sw = Screen.width, sh = Screen.height;
        // Laid out on a 1920 x 1080 board, scaled to fit and centred.
        float k = Mathf.Min(sh / 1080f, sw / 1920f);
        float ox = (sw - 1920f * k) * 0.5f, oy = (sh - 1080f * k) * 0.5f;
        Rect R(float x, float y, float w, float h) => new Rect(ox + x * k, oy + y * k, w * k, h * k);
        int Fs(float s) => Mathf.Max(1, Mathf.RoundToInt(s * k));

        var p = Pages[page];
        float pa = alpha * HorrorUI.Smooth((Time.unscaledTime - pageTime) / 0.3f); // page content fades in on each turn

        // ---- buttons (mouse) ----
        var backR = R(110, 940, 220, 70);
        var prevR = R(1300, 940, 230, 70);
        var nextR = R(1570, 940, 230, 70);
        float dotsX = 960f - (Pages.Length - 1) * 18f;
        hover = -1;
        if (alpha > 0.5f)
        {
            Vector2 m = e.mousePosition;
            if (backR.Contains(m)) hover = 0;
            else if (page > 0 && prevR.Contains(m)) hover = 1;
            else if (nextR.Contains(m)) hover = 2;
            else
                for (int i = 0; i < Pages.Length; i++)
                    if (R(dotsX + i * 36f - 16f, 955, 32, 40).Contains(m)) hover = 10 + i;

            if (e.type == EventType.MouseDown && e.button == 0 && hover >= 0)
            {
                if (hover == 0) Back();
                else if (hover == 1) Prev();
                else if (hover == 2) Next();
                else Go(hover - 10);
                e.Use();
            }
            if (hover != lastHover && e.type == EventType.Repaint) { if (hover >= 0) Tick(); lastHover = hover; }
        }
        if (!paint) return;

        HorrorUI.Fill(new Rect(0, 0, sw, sh), new Color(0f, 0f, 0f, 0.78f * alpha));

        // ---- header ----
        headStyle.fontSize = Fs(96);
        float hw = headStyle.CalcSize(new GUIContent("HOW TO PLAY")).x + 5f * k * 11;
        HorrorUI.Shaky(new Rect(ox + 110f * k, oy + 50f * k, hw, 110f * k), "HOW TO PLAY", headStyle, HorrorUI.A(HorrorUI.Ember, alpha),
                       Time.unscaledTime, 1.4f * k, 5f * k, 2f * k, 2f, 0.08f, 5);
        smallStyle.fontSize = Fs(24);
        smallStyle.alignment = TextAnchor.MiddleRight;
        HorrorUI.Text(R(1500, 80, 300, 50), $"page {page + 1} of {Pages.Length}", smallStyle, HorrorUI.A(HorrorUI.Ash, 0.8f * alpha), 0f, 2f * k);
        smallStyle.alignment = TextAnchor.MiddleCenter;

        // ---- the photograph ----
        var img = R(110, 200, 960, 540);
        HorrorUI.Fill(R(98, 188, 984, 564), HorrorUI.A(new Color(0.03f, 0.025f, 0.02f), 0.95f * alpha));
        var tex = Shot(p.shot);
        if (tex != null)
        {
            var old = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, pa);
            GUI.DrawTexture(img, tex, ScaleMode.ScaleAndCrop, false);
            GUI.color = old;
        }
        else
        {
            HorrorUI.Fill(img, HorrorUI.A(new Color(0.06f, 0.05f, 0.04f), pa));
            smallStyle.fontSize = Fs(26);
            HorrorUI.Text(img, "[ the photograph is missing ]", smallStyle, HorrorUI.A(HorrorUI.Ash, 0.6f * pa), 0f, 2f * k);
        }
        Border(R(98, 188, 984, 564), Mathf.Max(1f, 2f * k), HorrorUI.A(HorrorUI.Rust, 0.9f * alpha));
        Border(img, Mathf.Max(1f, 1f * k), HorrorUI.A(Color.black, 0.9f * alpha));
        captionStyle.fontSize = Fs(22);
        HorrorUI.Text(R(104, 760, 976, 36), p.caption, captionStyle, HorrorUI.A(HorrorUI.Ash, 0.85f * pa), 0f, 2f * k);

        // ---- the words ----
        float x = 1150f, w = 650f, y = 196f;
        titleStyle.fontSize = Fs(30);
        HorrorUI.Text(R(x, y, w, 40), Roman[page], titleStyle, HorrorUI.A(HorrorUI.EmberDim, pa), 0f, 2f * k);
        y += 38f;
        titleStyle.fontSize = Fs(62);
        HorrorUI.Text(R(x, y, w, 76), p.title, titleStyle, HorrorUI.A(HorrorUI.Ember, pa), 0f, 3f * k);
        y += 84f;
        HorrorUI.Fill(R(x, y, 220, 2), HorrorUI.A(HorrorUI.Rust, pa));
        y += 30f;

        bodyStyle.fontSize = Fs(29);
        foreach (var line in p.lines)
        {
            float h = bodyStyle.CalcHeight(new GUIContent(line), w * k) / k;
            HorrorUI.Glow(R(x - 30f, y + 9f, 16, 16), HorrorUI.A(HorrorUI.Ember, 0.8f * pa));
            HorrorUI.Text(R(x, y, w, h + 4f), line, bodyStyle, HorrorUI.A(HorrorUI.Bone, 0.92f * pa), 0f, 2f * k);
            y += h + 22f;
        }

        if (p.keys != null)
        {
            y += 10f;
            if (p.keysTitle != null)
            {
                titleStyle.fontSize = Fs(34);
                HorrorUI.Text(R(x, y, w, 44), p.keysTitle, titleStyle, HorrorUI.A(HorrorUI.EmberDim, pa), 0f, 2f * k);
                y += 52f;
            }
            capStyle.fontSize = Fs(22);
            captionStyle.fontSize = Fs(25);
            foreach (var row in p.keys)
            {
                var parts = row.Split('|');
                float cx = x;
                foreach (var cap in parts[0].Split(','))
                {
                    if (cap == "/")
                    {
                        HorrorUI.Text(R(cx, y, 44, 48), "or", capStyle, HorrorUI.A(HorrorUI.Ash, pa), 0f, 2f * k);
                        cx += 48f;
                        continue;
                    }
                    float cw = Mathf.Max(48f, capStyle.CalcSize(new GUIContent(cap)).x / k + 26f);
                    var cr = R(cx, y, cw, 46);
                    HorrorUI.Fill(cr, HorrorUI.A(new Color(0.09f, 0.07f, 0.05f), 0.9f * pa));
                    HorrorUI.Fill(R(cx, y + 42f, cw, 4), HorrorUI.A(Color.black, 0.6f * pa)); // a little depth under the cap
                    Border(cr, Mathf.Max(1f, 1.5f * k), HorrorUI.A(HorrorUI.Bone, 0.45f * pa));
                    HorrorUI.Text(cr, cap, capStyle, HorrorUI.A(HorrorUI.Bone, pa), 0f, 1.5f * k);
                    cx += cw + 8f;
                }
                HorrorUI.Text(R(Mathf.Max(cx + 14f, x + 250f), y, w - 250f, 46), parts.Length > 1 ? parts[1] : "", captionStyle,
                              HorrorUI.A(HorrorUI.Bone, 0.75f * pa), 0f, 2f * k);
                y += 58f;
            }
        }

        // ---- navigation ----
        navStyle.fontSize = Fs(40);
        NavButton(backR, "BACK", hover == 0, true, alpha, k, 40);
        NavButton(prevR, "‹ PREV", hover == 1, page > 0, alpha, k, 41);
        NavButton(nextR, page < Pages.Length - 1 ? "NEXT ›" : "DONE", hover == 2, true, alpha, k, 42);
        for (int i = 0; i < Pages.Length; i++)
        {
            var d = R(dotsX + i * 36f - 6f, 969, 12, 12);
            if (i == page)
            {
                HorrorUI.Glow(R(dotsX + i * 36f - 16f, 959, 32, 32), HorrorUI.A(HorrorUI.Ember, 0.9f * alpha));
                HorrorUI.Fill(d, HorrorUI.A(HorrorUI.Ember, alpha));
            }
            else HorrorUI.Fill(d, HorrorUI.A(hover == 10 + i ? HorrorUI.Bone : HorrorUI.Ash, (hover == 10 + i ? 0.9f : 0.5f) * alpha));
        }
        smallStyle.fontSize = Fs(18);
        HorrorUI.Text(R(0, 1028, 1920, 30), "A / D  or  ← →  to turn pages          ESC  to go back", smallStyle,
                      HorrorUI.A(HorrorUI.Ash, 0.55f * alpha), 0f, 1.5f * k);
    }

    void NavButton(Rect r, string label, bool hot, bool enabled, float alpha, float k, int seed)
    {
        if (!enabled) { HorrorUI.Text(r, label, navStyle, HorrorUI.A(HorrorUI.Ash, 0.25f * alpha), 0f, 2f * k); return; }
        if (hot)
        {
            float flick = 0.75f + 0.25f * Mathf.PerlinNoise(Time.unscaledTime * 9f, seed);
            float tw = navStyle.CalcSize(new GUIContent(label)).x;
            HorrorUI.Glow(new Rect(r.center.x - tw * 0.5f - 34f * k, r.center.y - 12f * k, 24f * k, 24f * k), HorrorUI.A(HorrorUI.Ember, alpha * flick));
            HorrorUI.Shaky(r, label, navStyle, HorrorUI.A(HorrorUI.Ember, alpha), Time.unscaledTime, 1.2f * k, 0f, 1.5f * k, 2f, 0.08f, seed);
        }
        else HorrorUI.Text(r, label, navStyle, HorrorUI.A(HorrorUI.Bone, 0.8f * alpha), 0f, 2f * k);
    }

    static void Border(Rect r, float t, Color c)
    {
        HorrorUI.Fill(new Rect(r.x, r.y, r.width, t), c);
        HorrorUI.Fill(new Rect(r.x, r.yMax - t, r.width, t), c);
        HorrorUI.Fill(new Rect(r.x, r.y, t, r.height), c);
        HorrorUI.Fill(new Rect(r.xMax - t, r.y, t, r.height), c);
    }

    static AudioClip MakeTick()
    {
        const int sr = 44100;
        int n = sr / 16;
        var d = new float[n];
        var rng = new System.Random(13);
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)sr;
            d[i] = (Mathf.Sin(2f * Mathf.PI * 145f * t) * 0.6f + (float)(rng.NextDouble() * 2 - 1) * 0.25f) * Mathf.Exp(-t * 90f) * 0.5f;
        }
        var c = AudioClip.Create("HowToTick", n, 1, sr, false);
        c.SetData(d, 0);
        return c;
    }
}
