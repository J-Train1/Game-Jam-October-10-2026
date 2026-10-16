using UnityEngine;
using UnityEngine.InputSystem;

// ACHIEVEMENTS, from the main menu. A scrolling list of big, readable cards (2 across) with icon, name and
// description; earned ones glow, locked ones are dark, and the ones for nights you haven't unlocked yet stay
// "? ? ?". At the bottom, set apart: the PUMPKIN KING card, and under it your overall progress.
// Scroll: mouse wheel, W/S or up/down, Page Up/Down. Esc / BACK to leave.
public class AchievementsPanel
{
    public System.Action OnBack;

    // Layout (on a 1920 x 1080 board).
    const float ViewTop = 188f, ViewBottom = 948f, ViewLeft = 70f, ViewRight = 1850f;
    const int Cols = 2;
    const float CellW = 860f, CellH = 124f, GapX = 40f, GapY = 14f;

    float openedAt, scroll, scrollTarget;
    bool hoverBack, lastHoverBack;
    readonly AudioSource src;
    static AudioClip tick;
    static Texture2D fadeTex;
    GUIStyle headStyle, nameStyle, descStyle, smallStyle, navStyle, countStyle, kingStyle, pctStyle;

    public AchievementsPanel(AudioSource audioSource) { src = audioSource; }

    public void Open() { openedAt = Time.unscaledTime; hoverBack = lastHoverBack = false; scroll = scrollTarget = 0f; }

    public void Update()
    {
        var kb = Keyboard.current;
        if (kb != null)
        {
            if (kb.escapeKey.wasPressedThisFrame || kb.backspaceKey.wasPressedThisFrame) { Back(); return; }
            if (kb.downArrowKey.wasPressedThisFrame || kb.sKey.wasPressedThisFrame) scrollTarget += 140f;
            if (kb.upArrowKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame) scrollTarget -= 140f;
            if (kb.pageDownKey.wasPressedThisFrame) scrollTarget += 600f;
            if (kb.pageUpKey.wasPressedThisFrame) scrollTarget -= 600f;
            if (kb.homeKey.wasPressedThisFrame) scrollTarget = 0f;
            if (kb.endKey.wasPressedThisFrame) scrollTarget = float.MaxValue;
        }
        var mouse = Mouse.current;
        if (mouse != null) { float wy = mouse.scroll.ReadValue().y; if (Mathf.Abs(wy) > 0.01f) scrollTarget -= Mathf.Sign(wy) * 110f; }
        scrollTarget = Mathf.Clamp(scrollTarget, 0f, MaxScroll);
        scroll = Mathf.Lerp(scroll, scrollTarget, 1f - Mathf.Exp(-14f * Time.unscaledDeltaTime));
    }

    static int GridCount => Achievements.All.Length - 1;          // everything but PUMPKIN KING
    static int Rows => (GridCount + Cols - 1) / Cols;
    static float GridH => Rows * (CellH + GapY);
    const float KingGap = 50f, KingH = 300f, BarGap = 36f, BarBlockH = 140f;
    static float ContentH => GridH + KingGap + KingH + BarGap + BarBlockH + 20f;
    static float MaxScroll => Mathf.Max(0f, ContentH - (ViewBottom - ViewTop));

    void Back() { Tick(); OnBack?.Invoke(); }
    void Tick() { if (src != null) src.PlayOneShot(tick ??= MakeTick(), 0.5f * GameSettings.Fx); }

    void Styles()
    {
        if (headStyle != null) return;
        headStyle = HorrorUI.Style(HorrorUI.TitleFont, TextAnchor.MiddleLeft);
        nameStyle = HorrorUI.Style(HorrorUI.SerifFont, TextAnchor.MiddleLeft);
        descStyle = HorrorUI.Style(HorrorUI.TypeFont, TextAnchor.UpperLeft);
        descStyle.wordWrap = true;
        smallStyle = HorrorUI.Style(HorrorUI.TypeFont, TextAnchor.MiddleCenter);
        countStyle = HorrorUI.Style(HorrorUI.TypeFont, TextAnchor.MiddleRight);
        navStyle = HorrorUI.Style(HorrorUI.SerifFont, TextAnchor.MiddleCenter);
        kingStyle = HorrorUI.Style(HorrorUI.TitleFont, TextAnchor.MiddleCenter);
        pctStyle = HorrorUI.Style(HorrorUI.TitleFont, TextAnchor.MiddleRight);
    }

    public void OnGUI(float alpha)
    {
        Styles();
        var e = Event.current;
        bool paint = e.type == EventType.Repaint;
        float sw = Screen.width, sh = Screen.height;
        float k = Mathf.Min(sh / 1080f, sw / 1920f);
        float ox = (sw - 1920f * k) * 0.5f, oy = (sh - 1080f * k) * 0.5f;
        Rect R(float x, float y, float w, float h) => new Rect(ox + x * k, oy + y * k, w * k, h * k);
        int Fs(float s) => Mathf.Max(1, Mathf.RoundToInt(s * k));
        float now = Time.unscaledTime;
        var backR = R(1600, 975, 240, 70);

        if (alpha > 0.5f)
        {
            hoverBack = backR.Contains(e.mousePosition);
            if (hoverBack && e.type == EventType.MouseDown && e.button == 0) { Back(); e.Use(); }
        }
        if (!paint) return;
        if (hoverBack != lastHoverBack) { lastHoverBack = hoverBack; if (hoverBack) Tick(); }

        HorrorUI.Fill(new Rect(0, 0, sw, sh), new Color(0f, 0f, 0f, 0.86f * alpha));

        // ---- header ----
        headStyle.fontSize = Fs(96);
        float hw = headStyle.CalcSize(new GUIContent("ACHIEVEMENTS")).x + 5f * k * 12;
        HorrorUI.Shaky(new Rect(ox + 80f * k, oy + 36f * k, hw, 110f * k), "ACHIEVEMENTS", headStyle, HorrorUI.A(HorrorUI.Ember, alpha),
                       now, 1.4f * k, 5f * k, 2f * k, 2f, 0.08f, 14);
        int earned = Achievements.Earned, total = Achievements.Total;
        countStyle.fontSize = Fs(26);
        HorrorUI.Text(R(1300, 70, 540, 40), $"{earned} / {total} earned", countStyle, HorrorUI.A(HorrorUI.Bone, 0.8f * alpha), 0f, 2f * k);
        HorrorUI.Fill(R(80, 166, 1760, 1.5f), HorrorUI.A(HorrorUI.Rust, 0.8f * alpha));

        // ---- the scrolling list (clipped to the view) ----
        var view = R(ViewLeft, ViewTop, ViewRight - ViewLeft, ViewBottom - ViewTop);
        GUI.BeginGroup(view);
        // Inside the group: x/y are relative to the view's top-left; content scrolls up by 'scroll'.
        Rect C(float x, float y, float w, float h) => new Rect((x - ViewLeft) * k, (y - ViewTop - scroll) * k, w * k, h * k);
        float startX = (1920f - (Cols * CellW + (Cols - 1) * GapX)) * 0.5f;
        var all = Achievements.All;
        int gi = 0;
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i].id == "ALL") continue;
            float cx = startX + (gi % Cols) * (CellW + GapX);
            float cy = ViewTop + 6f + (gi / Cols) * (CellH + GapY);
            float ca = alpha * HorrorUI.Smooth((now - openedAt - 0.05f - gi * 0.015f) / 0.35f);
            var cell = C(cx, cy, CellW, CellH);
            if (cell.yMax > 0 && cell.y < view.height) DrawCell(all[i], cell, ca, k, Fs, e.mousePosition, now);
            gi++;
        }
        float kingY = ViewTop + 6f + GridH + KingGap;
        DrawKing(C(460f, kingY, 1000f, KingH), alpha, k, Fs, now);
        DrawProgress(C(360f, kingY + KingH + BarGap, 1200f, BarBlockH), earned, total, alpha, k, Fs, now);
        GUI.EndGroup();

        // Soft fade where the list runs under the top / bottom edge, and a thin scroll bar.
        fadeTex ??= MakeFade();
        var old = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.95f * alpha);
        if (scroll > 2f) GUI.DrawTexture(new Rect(view.x, view.y, view.width, 40f * k), fadeTex);
        if (scroll < MaxScroll - 2f) GUI.DrawTextureWithTexCoords(new Rect(view.x, view.yMax - 50f * k, view.width, 50f * k), fadeTex, new Rect(0, 1, 1, -1));
        GUI.color = old;
        if (MaxScroll > 1f)
        {
            var track = R(1872f, ViewTop, 4f, ViewBottom - ViewTop);
            HorrorUI.Fill(track, HorrorUI.A(HorrorUI.Bone, 0.1f * alpha));
            float frac = (ViewBottom - ViewTop) / ContentH;
            float th = track.height * frac, ty = track.y + (track.height - th) * (scroll / MaxScroll);
            HorrorUI.Fill(new Rect(track.x - 1f * k, ty, track.width + 2f * k, th), HorrorUI.A(HorrorUI.EmberDim, 0.9f * alpha));
            smallStyle.fontSize = Fs(16);
            if (scroll < 10f) HorrorUI.Text(R(0, 952, 1920, 24), "scroll for more  ↓", smallStyle, HorrorUI.A(HorrorUI.Ash, 0.6f * alpha * (0.6f + 0.4f * Mathf.Sin(now * 3f))), 0f, 1f * k);
        }

        // ---- back ----
        navStyle.fontSize = Fs(42);
        if (hoverBack)
        {
            float tw = navStyle.CalcSize(new GUIContent("BACK")).x;
            float flick = 0.75f + 0.25f * Mathf.PerlinNoise(now * 9f, 6f);
            HorrorUI.Glow(new Rect(backR.center.x - tw * 0.5f - 34f * k, backR.center.y - 12f * k, 24f * k, 24f * k), HorrorUI.A(HorrorUI.Ember, alpha * flick));
            HorrorUI.Shaky(backR, "BACK", navStyle, HorrorUI.A(HorrorUI.Ember, alpha), now, 1.2f * k, 0f, 1.5f * k, 2f, 0.08f, 51);
        }
        else HorrorUI.Text(backR, "BACK", navStyle, HorrorUI.A(HorrorUI.Bone, 0.8f * alpha), 0f, 2f * k);
    }

    void DrawCell(Achievements.Def d, Rect c, float ca, float k, System.Func<float, int> Fs, Vector2 mouse, float now)
    {
        if (ca <= 0.01f) return;
        bool has = Achievements.Has(d.id);
        bool hidden = Achievements.IsHidden(d);
        bool hot = c.Contains(mouse);

        HorrorUI.Fill(c, HorrorUI.A(has ? new Color(0.10f, 0.055f, 0.02f) : new Color(0.045f, 0.038f, 0.032f), (hot ? 0.97f : 0.85f) * ca));
        if (has) HorrorUI.Fill(new Rect(c.x, c.y, Mathf.Max(2f, 4f * k), c.height), HorrorUI.A(HorrorUI.Ember, 0.95f * ca));
        else if (hot) HorrorUI.Fill(new Rect(c.x, c.y, Mathf.Max(2f, 3f * k), c.height), HorrorUI.A(HorrorUI.Rust, 0.9f * ca));

        float isz = 94f * k;
        var ir = new Rect(c.x + 18f * k, c.y + (c.height - isz) * 0.5f, isz, isz);
        if (has) HorrorUI.Glow(new Rect(ir.x - 20f * k, ir.y - 20f * k, ir.width + 40f * k, ir.height + 40f * k), HorrorUI.A(HorrorUI.Ember, 0.32f * ca));
        var tex = AchievementToast.Icon(d.icon);
        if (tex != null)
        {
            var old = GUI.color;
            float lit = has ? 1f : hidden ? 0.05f : 0.25f;
            GUI.color = HorrorUI.Out(new Color(lit, lit, lit, ca));
            GUI.DrawTexture(ir, HorrorUI.Tex(tex), ScaleMode.ScaleToFit, true);
            GUI.color = old;
        }

        float tx = ir.xMax + 22f * k, tw = c.xMax - tx - 16f * k;
        nameStyle.fontSize = Fs(34);
        descStyle.fontSize = Fs(21);
        string name = hidden ? "? ? ?" : d.name;
        string desc = hidden ? "Unlock a harder night to reveal this one." : d.desc;
        HorrorUI.Text(new Rect(tx, c.y + 12f * k, tw, 44f * k), name, nameStyle,
                      HorrorUI.A(has ? HorrorUI.Ember : HorrorUI.Bone, (has ? 1f : hidden ? 0.4f : 0.7f) * ca), 0f, 2f * k);
        HorrorUI.Text(new Rect(tx, c.y + 58f * k, tw, 58f * k), desc, descStyle,
                      HorrorUI.A(has ? HorrorUI.Bone : HorrorUI.Ash, (has ? 0.85f : hidden ? 0.5f : 0.75f) * ca), 0f, 1.5f * k);

        if (has)
        {
            smallStyle.fontSize = Fs(16);
            HorrorUI.Text(new Rect(c.xMax - 130f * k, c.y + 10f * k, 120f * k, 26f * k), "EARNED", smallStyle, HorrorUI.A(HorrorUI.EmberDim, 0.9f * ca), 0f, 1f * k);
        }
        else if (d.target > 0 && !hidden)
        {
            int p = Mathf.Min(Achievements.Progress(d.id), d.target);
            var pb = new Rect(c.xMax - 190f * k, c.y + 24f * k, 120f * k, Mathf.Max(2f, 5f * k));
            HorrorUI.Fill(pb, HorrorUI.A(HorrorUI.Bone, 0.15f * ca));
            HorrorUI.Fill(new Rect(pb.x, pb.y, pb.width * p / d.target, pb.height), HorrorUI.A(HorrorUI.Ember, ca));
            smallStyle.fontSize = Fs(18);
            HorrorUI.Text(new Rect(pb.xMax + 8f * k, c.y + 12f * k, 52f * k, 28f * k), $"{p}/{d.target}", smallStyle, HorrorUI.A(HorrorUI.Bone, 0.75f * ca), 0f, 1f * k);
        }
    }

    // PUMPKIN KING: centred, bigger, with its own brass frame.
    void DrawKing(Rect c, float alpha, float k, System.Func<float, int> Fs, float now)
    {
        var d = Achievements.Get("ALL");
        if (d == null) return;
        float ca = alpha * HorrorUI.Smooth((now - openedAt - 0.4f) / 0.5f);
        if (ca <= 0.01f) return;
        bool has = Achievements.Has("ALL");
        float breathe = 0.8f + 0.2f * Mathf.Sin(now * 1.6f);

        HorrorUI.Glow(new Rect(c.x - 80f * k, c.y - 60f * k, c.width + 160f * k, c.height + 120f * k),
                      HorrorUI.A(has ? HorrorUI.Ember : HorrorUI.Brass, (has ? 0.3f : 0.12f) * ca * breathe));
        HorrorUI.Fill(c, HorrorUI.A(new Color(0.07f, 0.045f, 0.02f), 0.92f * ca));
        float bw = Mathf.Max(1f, 3f * k);
        var bc = HorrorUI.A(has ? HorrorUI.Ember : HorrorUI.Brass, (has ? 1f : 0.6f) * ca);
        Border(c, bw, bc);
        var inner = new Rect(c.x + 8f * k, c.y + 8f * k, c.width - 16f * k, c.height - 16f * k);
        Border(inner, Mathf.Max(1f, 1f * k), HorrorUI.A(HorrorUI.Rust, 0.8f * ca));

        float isz = 132f * k;
        var ir = new Rect(c.center.x - isz * 0.5f, c.y + 22f * k, isz, isz);
        HorrorUI.Glow(new Rect(ir.x - 40f * k, ir.y - 40f * k, ir.width + 80f * k, ir.height + 80f * k), HorrorUI.A(HorrorUI.Ember, (has ? 0.45f : 0.15f) * ca * breathe));
        var tex = AchievementToast.Icon(d.icon);
        if (tex != null)
        {
            var old = GUI.color;
            float lit = has ? 1f : 0.35f;
            GUI.color = HorrorUI.Out(new Color(lit, lit, lit, ca));
            GUI.DrawTexture(ir, HorrorUI.Tex(tex), ScaleMode.ScaleToFit, true);
            GUI.color = old;
        }

        kingStyle.fontSize = Fs(64);
        var nr = new Rect(c.x, c.y + 158f * k, c.width, 74f * k);
        if (has) HorrorUI.Shaky(nr, d.name.ToUpper(), kingStyle, HorrorUI.A(HorrorUI.Ember, ca), now, 1.4f * k, 4f * k, 2f * k, 2f, 0.06f, 77);
        else HorrorUI.Text(nr, d.name.ToUpper(), kingStyle, HorrorUI.A(HorrorUI.Brass, 0.7f * ca), 0f, 3f * k);
        smallStyle.fontSize = Fs(22);
        HorrorUI.Text(new Rect(c.x, c.y + 236f * k, c.width, 32f * k), has ? "The corn bows to you." : d.desc, smallStyle,
                      HorrorUI.A(HorrorUI.Bone, 0.8f * ca), 0f, 1.5f * k);
    }

    // Overall progress: a segmented bar, one notch per achievement, lit ones glowing.
    void DrawProgress(Rect c, int earned, int total, float alpha, float k, System.Func<float, int> Fs, float now)
    {
        float ca = alpha * HorrorUI.Smooth((now - openedAt - 0.55f) / 0.5f);
        if (ca <= 0.01f) return;
        float pct = total > 0 ? (float)earned / total : 0f;

        var labelR = new Rect(c.x, c.y, c.width * 0.6f, 56f * k);
        nameStyle.fontSize = Fs(34);
        HorrorUI.Text(labelR, "YOUR HARVEST", nameStyle, HorrorUI.A(HorrorUI.Bone, 0.85f * ca), 0f, 2f * k);
        pctStyle.fontSize = Fs(60);
        HorrorUI.Text(new Rect(c.x, c.y - 8f * k, c.width, 70f * k), Mathf.RoundToInt(pct * 100f) + "%", pctStyle,
                      HorrorUI.A(pct >= 1f ? HorrorUI.Ember : HorrorUI.Ember, ca), 1.2f * k, 3f * k);

        // The bar.
        float barY = c.y + 70f * k, barH = 26f * k;
        var bar = new Rect(c.x, barY, c.width, barH);
        HorrorUI.Fill(new Rect(bar.x - 3f * k, bar.y - 3f * k, bar.width + 6f * k, bar.height + 6f * k), HorrorUI.A(HorrorUI.Rust, 0.7f * ca));
        HorrorUI.Fill(bar, HorrorUI.A(new Color(0.03f, 0.02f, 0.015f), ca));
        float gap = 4f * k, seg = (bar.width - gap * (total + 1)) / total;
        for (int i = 0; i < total; i++)
        {
            var sr = new Rect(bar.x + gap + i * (seg + gap), bar.y + gap, seg, bar.height - gap * 2f);
            bool lit = i < earned;
            if (lit)
            {
                float f = 0.85f + 0.15f * Mathf.PerlinNoise(now * 4f, i * 0.7f);
                HorrorUI.Fill(sr, HorrorUI.A(Color.Lerp(HorrorUI.EmberDim, HorrorUI.Ember, (float)i / Mathf.Max(1, total - 1)), ca * f));
            }
            else HorrorUI.Fill(sr, HorrorUI.A(HorrorUI.Bone, 0.06f * ca));
        }
        if (earned > 0)
        {
            float edge = bar.x + gap + earned * (seg + gap) - gap * 0.5f;
            HorrorUI.Glow(new Rect(edge - 30f * k, bar.center.y - 30f * k, 60f * k, 60f * k), HorrorUI.A(HorrorUI.Ember, 0.5f * ca));
        }
        smallStyle.fontSize = Fs(20);
        HorrorUI.Text(new Rect(c.x, barY + barH + 14f * k, c.width, 28f * k), $"{earned} of {total} achievements earned", smallStyle,
                      HorrorUI.A(HorrorUI.Ash, 0.85f * ca), 0f, 1.5f * k);
    }

    static void Border(Rect r, float t, Color c)
    {
        HorrorUI.Fill(new Rect(r.x, r.y, r.width, t), c);
        HorrorUI.Fill(new Rect(r.x, r.yMax - t, r.width, t), c);
        HorrorUI.Fill(new Rect(r.x, r.y, t, r.height), c);
        HorrorUI.Fill(new Rect(r.xMax - t, r.y, t, r.height), c);
    }

    // Vertical fade: opaque at the top, clear at the bottom.
    static Texture2D MakeFade()
    {
        var t = new Texture2D(1, 32, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
        for (int y = 0; y < 32; y++) t.SetPixel(0, y, new Color(1f, 1f, 1f, Mathf.SmoothStep(0f, 1f, y / 31f)));
        t.Apply(false, false);
        return t;
    }

    static AudioClip MakeTick()
    {
        const int sr = 44100;
        int n = sr / 16;
        var data = new float[n];
        var rng = new System.Random(23);
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)sr;
            data[i] = (Mathf.Sin(2f * Mathf.PI * 138f * t) * 0.6f + (float)(rng.NextDouble() * 2 - 1) * 0.25f) * Mathf.Exp(-t * 90f) * 0.5f;
        }
        var c = AudioClip.Create("AchievementsTick", n, 1, sr, false);
        c.SetData(data, 0);
        return c;
    }
}
