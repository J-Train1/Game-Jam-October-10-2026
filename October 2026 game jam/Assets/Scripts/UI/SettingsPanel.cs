using UnityEngine;
using UnityEngine.InputSystem;

// The settings list, drawn in the game's horror style. Used by the main menu and the pause menu.
// Mouse: hover a row, drag sliders, click toggles/buttons. Keyboard: W/S or up/down to pick a row,
// A/D or left/right to change it, Enter to toggle/press, Esc to go back.
// Also contains the brightness screen ("adjust until the pumpkin on the left is barely visible").
public class SettingsPanel
{
    public System.Action OnBack;              // BACK pressed (or Esc)
    public System.Action OnCalibrationDone;   // CONTINUE pressed on the brightness screen

    public bool Calibrating { get; private set; }
    bool calibrationFromSettings;

    enum Kind { Slider, Toggle, Button }
    class Row
    {
        public string label; public Kind kind;
        public System.Func<float> get01; public System.Action<float> set01; public System.Func<string> show;
        public System.Func<bool> getB; public System.Action<bool> setB;
        public System.Action press;
    }

    readonly Row[] rows;
    int selected, dragging = -1, lastSelected = -1;
    GUIStyle labelStyle, valueStyle, headStyle, textStyle;
    readonly AudioSource src;
    static AudioClip tick;
    static Texture2D pumpkinTex;

    public SettingsPanel(AudioSource audioSource)
    {
        src = audioSource;
        rows = new[]
        {
            Slider("MOUSE SENSITIVITY", () => Mathf.InverseLerp(GameSettings.MinSensitivity, GameSettings.MaxSensitivity, GameSettings.Sensitivity),
                   v => GameSettings.Sensitivity = Mathf.Lerp(GameSettings.MinSensitivity, GameSettings.MaxSensitivity, v), () => GameSettings.Sensitivity.ToString("0.00") + "x"),
            Toggle("INVERT MOUSE Y", () => GameSettings.InvertY, b => GameSettings.InvertY = b),
            Slider("MASTER VOLUME", () => GameSettings.Master, v => GameSettings.Master = v, () => Pct(GameSettings.Master)),
            Slider("EFFECTS VOLUME", () => GameSettings.Effects, v => GameSettings.Effects = v, () => Pct(GameSettings.Effects)),
            Slider("AMBIENCE & MUSIC", () => GameSettings.Ambience, v => GameSettings.Ambience = v, () => Pct(GameSettings.Ambience)),
            Slider("BRIGHTNESS", () => (GameSettings.Brightness + 1f) * 0.5f, v => GameSettings.Brightness = v * 2f - 1f,
                   () => (GameSettings.Brightness >= 0f ? "+" : "") + GameSettings.Brightness.ToString("0.0")),
            Button("ADJUST BRIGHTNESS...", () => { Calibrating = true; calibrationFromSettings = true; }),
            Slider("FIELD OF VIEW", () => Mathf.InverseLerp(GameSettings.MinFov, GameSettings.MaxFov, GameSettings.Fov),
                   v => GameSettings.Fov = Mathf.Round(Mathf.Lerp(GameSettings.MinFov, GameSettings.MaxFov, v)), () => GameSettings.Fov.ToString("0") + "°"),
            Toggle("HEAD BOB", () => GameSettings.HeadBob, b => GameSettings.HeadBob = b),
            Slider("SCREEN EFFECTS", () => GameSettings.ScreenEffects, v => GameSettings.ScreenEffects = v, () => Pct(GameSettings.ScreenEffects)),
            Toggle("REDUCE FLASHING", () => GameSettings.ReduceFlashing, b => GameSettings.ReduceFlashing = b),
            Button("RESET TO DEFAULTS", () => GameSettings.ResetDefaults()),
            Button("BACK", () => { GameSettings.Save(); OnBack?.Invoke(); }),
        };
    }

    static string Pct(float v) => Mathf.RoundToInt(v * 100f) + "%";
    static Row Slider(string l, System.Func<float> g, System.Action<float> s, System.Func<string> show) => new Row { label = l, kind = Kind.Slider, get01 = g, set01 = s, show = show };
    static Row Toggle(string l, System.Func<bool> g, System.Action<bool> s) => new Row { label = l, kind = Kind.Toggle, getB = g, setB = s };
    static Row Button(string l, System.Action p) => new Row { label = l, kind = Kind.Button, press = p };

    /// <summary>Open straight onto the brightness screen (first time playing).</summary>
    public void StartCalibration() { Calibrating = true; calibrationFromSettings = false; }

    public void ResetSelection() { selected = 0; dragging = -1; }

    // ---------------- keyboard ----------------

    public void Update()
    {
        var kb = Keyboard.current;
        if (kb == null) return;
        bool left = kb.leftArrowKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame;
        bool right = kb.rightArrowKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame;
        bool enter = kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame;
        bool esc = kb.escapeKey.wasPressedThisFrame;

        if (Calibrating)
        {
            if (left || right) { GameSettings.Brightness = Mathf.Clamp(GameSettings.Brightness + (right ? 0.1f : -0.1f), -1f, 1f); GameSettings.Apply(); Tick(); }
            if (enter || (esc && calibrationFromSettings)) FinishCalibration();
            return;
        }

        if (kb.downArrowKey.wasPressedThisFrame || kb.sKey.wasPressedThisFrame) { selected = (selected + 1) % rows.Length; Tick(); }
        if (kb.upArrowKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame) { selected = (selected + rows.Length - 1) % rows.Length; Tick(); }
        var r = rows[selected];
        if ((left || right) && r.kind == Kind.Slider) { r.set01(Mathf.Clamp01(r.get01() + (right ? 0.05f : -0.05f))); GameSettings.Apply(); Tick(); }
        if ((left || right || enter) && r.kind == Kind.Toggle) { r.setB(!r.getB()); GameSettings.Apply(); GameSettings.Save(); Tick(); }
        if (enter && r.kind == Kind.Button) { Tick(); r.press(); }
        if (esc) { GameSettings.Save(); OnBack?.Invoke(); }
    }

    void FinishCalibration()
    {
        Calibrating = false;
        GameSettings.BrightnessSet = true;
        GameSettings.Save();
        Tick();
        if (!calibrationFromSettings) OnCalibrationDone?.Invoke();
    }

    void Tick()
    {
        if (src != null) src.PlayOneShot(tick ??= MakeTick(), 0.5f * GameSettings.Fx);
    }

    // ---------------- drawing ----------------

    void Styles()
    {
        if (labelStyle != null) return;
        labelStyle = HorrorUI.Style(HorrorUI.SerifFont, TextAnchor.MiddleLeft);
        valueStyle = HorrorUI.Style(HorrorUI.TypeFont, TextAnchor.MiddleLeft);
        headStyle = HorrorUI.Style(HorrorUI.TitleFont, TextAnchor.MiddleLeft);
        textStyle = HorrorUI.Style(HorrorUI.TypeFont);
    }

    /// <summary>Draws the list with its top-left at (x, y). alpha fades it in.</summary>
    public void OnGUI(float x, float y, float alpha)
    {
        Styles();
        if (Calibrating) { DrawCalibration(alpha); return; }

        float k = Screen.height / 1080f;
        var e = Event.current;
        bool paint = e.type == EventType.Repaint;
        float rowH = 56f * k, labelW = 470f * k, trackW = 300f * k;

        headStyle.fontSize = HorrorUI.Px(96);
        if (paint)
        {
            float hw = headStyle.CalcSize(new GUIContent("SETTINGS")).x + 5f * k * 7;
            HorrorUI.Shaky(new Rect(x, y, hw, 110f * k), "SETTINGS", headStyle, HorrorUI.A(HorrorUI.Ember, alpha), Time.unscaledTime, 1.4f * k, 5f * k, 2f * k, 2f, 0.08f, 3);
        }
        labelStyle.fontSize = HorrorUI.Px(34);
        valueStyle.fontSize = HorrorUI.Px(22);

        float top = y + 130f * k;
        if (e.type == EventType.MouseUp && dragging >= 0) { dragging = -1; GameSettings.Save(); }

        for (int i = 0; i < rows.Length; i++)
        {
            var r = rows[i];
            float ry = top + i * rowH + (r.kind == Kind.Button && i >= rows.Length - 2 ? 14f * k : 0f);
            var rowRect = new Rect(x - 40f * k, ry, labelW + trackW + 200f * k, rowH);
            var track = new Rect(x + labelW, ry + rowH * 0.5f - 2f * k, trackW, Mathf.Max(2f, 4f * k));
            var trackHit = new Rect(track.x - 12f * k, ry, track.width + 24f * k, rowH);

            // mouse
            if (alpha > 0.5f)
            {
                if (rowRect.Contains(e.mousePosition) && dragging < 0 && (e.type == EventType.MouseMove || e.type == EventType.MouseDown || paint))
                    if (selected != i) { selected = i; }
                if (e.type == EventType.MouseDown && e.button == 0 && rowRect.Contains(e.mousePosition))
                {
                    if (r.kind == Kind.Slider && trackHit.Contains(e.mousePosition)) dragging = i;
                    else if (r.kind == Kind.Toggle) { r.setB(!r.getB()); GameSettings.Apply(); GameSettings.Save(); Tick(); }
                    else if (r.kind == Kind.Button) { Tick(); r.press(); }
                    e.Use();
                }
                if (dragging == i && (e.type == EventType.MouseDrag || e.type == EventType.MouseDown || e.type == EventType.Used))
                {
                    r.set01(Mathf.Clamp01((e.mousePosition.x - track.x) / track.width));
                    GameSettings.Apply();
                }
            }
            if (!paint) continue;

            bool sel = selected == i;
            var labelRect = new Rect(x, ry, labelW, rowH);
            if (sel)
            {
                float flick = 0.75f + 0.25f * Mathf.PerlinNoise(Time.unscaledTime * 9f, i);
                HorrorUI.Glow(new Rect(x - 30f * k, labelRect.center.y - 12f * k, 24f * k, 24f * k), HorrorUI.A(HorrorUI.Ember, alpha * flick));
                HorrorUI.Glow(new Rect(x - 42f * k, labelRect.center.y - 24f * k, 48f * k, 48f * k), HorrorUI.A(HorrorUI.Ember, 0.3f * alpha * flick));
            }
            var lc = sel ? HorrorUI.Ember : HorrorUI.A(HorrorUI.Bone, r.kind == Kind.Button && i < rows.Length - 1 ? 0.6f : 0.8f);
            HorrorUI.Text(labelRect, r.label, labelStyle, HorrorUI.A(lc, lc.a * alpha), 0f, 2f * k);

            if (r.kind == Kind.Slider)
            {
                float v = r.get01();
                HorrorUI.Fill(track, HorrorUI.A(HorrorUI.Bone, 0.18f * alpha));
                HorrorUI.Fill(new Rect(track.x, track.y, track.width * v, track.height), HorrorUI.A(sel ? HorrorUI.Ember : HorrorUI.EmberDim, alpha));
                float kx = track.x + track.width * v;
                HorrorUI.Glow(new Rect(kx - 14f * k, track.center.y - 14f * k, 28f * k, 28f * k), HorrorUI.A(HorrorUI.Ember, (sel ? 0.9f : 0.45f) * alpha));
                HorrorUI.Fill(new Rect(kx - 2f * k, track.center.y - 9f * k, 4f * k, 18f * k), HorrorUI.A(sel ? HorrorUI.Bone : HorrorUI.Ash, alpha));
                HorrorUI.Text(new Rect(track.xMax + 28f * k, ry, 160f * k, rowH), r.show(), valueStyle, HorrorUI.A(HorrorUI.Bone, 0.8f * alpha), 0f, 2f * k);
            }
            else if (r.kind == Kind.Toggle)
            {
                bool on = r.getB();
                HorrorUI.Text(new Rect(track.x, ry, 90f * k, rowH), "ON", valueStyle, HorrorUI.A(on ? HorrorUI.Ember : HorrorUI.Ash, (on ? 1f : 0.45f) * alpha), 0f, 2f * k);
                HorrorUI.Text(new Rect(track.x + 90f * k, ry, 90f * k, rowH), "OFF", valueStyle, HorrorUI.A(!on ? HorrorUI.Ember : HorrorUI.Ash, (!on ? 1f : 0.45f) * alpha), 0f, 2f * k);
                float ux = on ? track.x : track.x + 90f * k;
                HorrorUI.Fill(new Rect(ux, ry + rowH * 0.5f + 14f * k, (on ? 30f : 42f) * k, Mathf.Max(1f, 2f * k)), HorrorUI.A(HorrorUI.EmberDim, alpha));
            }
        }
    }

    // ---------------- brightness screen ----------------

    void DrawCalibration(float alpha)
    {
        var e = Event.current;
        bool paint = e.type == EventType.Repaint;
        float sw = Screen.width, sh = Screen.height, k = sh / 1080f;
        if (pumpkinTex == null) pumpkinTex = MakePumpkin();

        var track = new Rect(sw * 0.5f - 250f * k, sh * 0.72f, 500f * k, Mathf.Max(2f, 4f * k));
        var trackHit = new Rect(track.x - 16f * k, track.y - 26f * k, track.width + 32f * k, 56f * k);
        textStyle.fontSize = HorrorUI.Px(30);
        var cont = new Rect(sw * 0.5f - 140f * k, sh * 0.82f, 280f * k, 60f * k);

        if (e.type == EventType.MouseDown && e.button == 0)
        {
            if (trackHit.Contains(e.mousePosition)) dragging = 999;
            else if (cont.Contains(e.mousePosition)) { FinishCalibration(); e.Use(); return; }
        }
        if (e.type == EventType.MouseUp) dragging = -1;
        if (dragging == 999 && (e.type == EventType.MouseDrag || e.type == EventType.MouseDown))
        {
            GameSettings.Brightness = Mathf.Clamp01((e.mousePosition.x - track.x) / track.width) * 2f - 1f;
            GameSettings.Apply();
        }
        if (!paint) return;

        HorrorUI.Fill(new Rect(0, 0, sw, sh), new Color(0f, 0f, 0f, alpha));
        headStyle.fontSize = HorrorUI.Px(96);
        float hw = headStyle.CalcSize(new GUIContent("BRIGHTNESS")).x + 5f * k * 9;
        HorrorUI.Shaky(new Rect((sw - hw) * 0.5f, sh * 0.08f, hw, 110f * k), "BRIGHTNESS", headStyle, HorrorUI.A(HorrorUI.Ember, alpha), Time.unscaledTime, 1.4f * k, 5f * k, 2f * k, 2f, 0.08f, 4);
        HorrorUI.Text(new Rect(0, sh * 0.22f, sw, 40f * k), "Adjust until the pumpkin on the left is barely visible", textStyle, HorrorUI.A(HorrorUI.Bone, 0.85f * alpha), 0f, 2f * k);
        HorrorUI.Text(new Rect(0, sh * 0.22f + 44f * k, sw, 40f * k), "and the one on the right can't be seen at all.", textStyle, HorrorUI.A(HorrorUI.Bone, 0.85f * alpha), 0f, 2f * k);

        // The pumpkins are drawn as dark as they'd look in the game at this brightness.
        float gain = Mathf.Pow(2f, GameSettings.Brightness);
        float size = 230f * k;
        var lp = new Rect(sw * 0.5f - size - 90f * k, sh * 0.36f, size, size);
        var rp = new Rect(sw * 0.5f + 90f * k, sh * 0.36f, size, size);
        float g1 = 0.085f * gain, g2 = 0.035f * gain;
        var old = GUI.color;
        GUI.color = new Color(g1 * 1.15f, g1 * 0.8f, g1 * 0.55f, alpha); GUI.DrawTexture(lp, pumpkinTex, ScaleMode.StretchToFill, true);
        GUI.color = new Color(g2 * 1.15f, g2 * 0.8f, g2 * 0.55f, alpha); GUI.DrawTexture(rp, pumpkinTex, ScaleMode.StretchToFill, true);
        GUI.color = old;

        float v = (GameSettings.Brightness + 1f) * 0.5f;
        HorrorUI.Fill(track, HorrorUI.A(HorrorUI.Bone, 0.18f * alpha));
        HorrorUI.Fill(new Rect(track.x, track.y, track.width * v, track.height), HorrorUI.A(HorrorUI.Ember, alpha));
        float kx = track.x + track.width * v;
        HorrorUI.Glow(new Rect(kx - 16f * k, track.center.y - 16f * k, 32f * k, 32f * k), HorrorUI.A(HorrorUI.Ember, 0.9f * alpha));
        HorrorUI.Fill(new Rect(kx - 2f * k, track.center.y - 10f * k, 4f * k, 20f * k), HorrorUI.A(HorrorUI.Bone, alpha));
        textStyle.fontSize = HorrorUI.Px(20);
        HorrorUI.Text(new Rect(0, track.y + 24f * k, sw, 30f * k), "drag, or use A / D", textStyle, HorrorUI.A(HorrorUI.Ash, 0.7f * alpha), 0f, 2f * k);

        labelStyle.fontSize = HorrorUI.Px(44);
        bool hc = cont.Contains(e.mousePosition);
        var cs = new GUIStyle(labelStyle) { alignment = TextAnchor.MiddleCenter };
        HorrorUI.Text(cont, calibrationFromSettings ? "DONE" : "CONTINUE", cs, HorrorUI.A(hc ? HorrorUI.Ember : HorrorUI.Bone, alpha), 0f, 2f * k);
    }

    // A carved jack-o'-lantern silhouette (white, tinted when drawn).
    static Texture2D MakePumpkin()
    {
        const int N = 192;
        var tex = new Texture2D(N, N, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
        var px = new Color[N * N];
        for (int j = 0; j < N; j++)
        for (int i = 0; i < N; i++)
        {
            float u = (i + 0.5f) / N * 2f - 1f, v = (j + 0.5f) / N * 2f - 1f;
            // body: three overlapping lobes
            bool body = Ell(u, v + 0.08f, 0f, 0.8f, 0.68f) || Ell(u, v + 0.08f, -0.38f, 0.5f, 0.62f) || Ell(u, v + 0.08f, 0.38f, 0.5f, 0.62f);
            bool stem = Mathf.Abs(u - 0.04f + (v - 0.6f) * 0.25f) < 0.07f && v > 0.5f && v < 0.85f;
            // carved face: two triangle eyes, a triangle nose, a jagged grin
            bool eyeL = Tri(u, v, -0.32f, 0.12f, 0.17f), eyeR = Tri(u, v, 0.32f, 0.12f, 0.17f), nose = Tri(u, v, 0f, -0.08f, 0.09f);
            bool mouth = v < -0.22f && v > -0.42f - 0.06f * Mathf.Abs(Mathf.Sin(u * 14f)) && Mathf.Abs(u) < 0.5f - (v + 0.32f) * 0.4f
                         && !(v > -0.3f && Mathf.Abs(Mathf.Repeat(u * 3.5f, 1f) - 0.5f) < 0.12f);
            bool carved = eyeL || eyeR || nose || mouth;
            float ribs = 0.85f + 0.15f * Mathf.Abs(Mathf.Cos(u * 5.5f));
            float a = ((body && !carved) || stem) ? 1f : 0f;
            px[j * N + i] = new Color(ribs, ribs, ribs, a);
        }
        tex.SetPixels(px);
        tex.Apply(false, false);
        return tex;
    }

    static bool Ell(float u, float v, float cx, float rx, float ry) { float a = (u - cx) / rx, b = v / ry; return a * a + b * b <= 1f; }
    static bool Tri(float u, float v, float cx, float cy, float s) { float dy = v - cy; return dy < s * 0.6f && dy > -s * 0.6f && Mathf.Abs(u - cx) < (dy + s * 0.6f) * 0.75f; }

    static AudioClip MakeTick()
    {
        const int sr = 44100;
        int n = sr / 16;
        var d = new float[n];
        var rng = new System.Random(9);
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)sr;
            d[i] = (Mathf.Sin(2f * Mathf.PI * 150f * t) * 0.6f + (float)(rng.NextDouble() * 2 - 1) * 0.25f) * Mathf.Exp(-t * 90f) * 0.5f;
        }
        var c = AudioClip.Create("SettingsTick", n, 1, sr, false);
        c.SetData(d, 0);
        return c;
    }
}
