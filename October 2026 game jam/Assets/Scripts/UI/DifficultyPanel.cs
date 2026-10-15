using UnityEngine;
using UnityEngine.InputSystem;

// CHOOSE YOUR NIGHT: the full-screen difficulty picker opened from the main menu's DIFFICULTY item.
// Three cards side by side, each with its own jack-o'-lantern (friendly -> menacing -> rotting nightmare),
// the name, its tagline and what changes. Locked nights are shadowed silhouettes with what unlocks them.
// Mouse: hover a card, click to choose it. Keyboard: A/D or left/right, Enter to choose, Esc to go back.
public class DifficultyPanel
{
    public System.Action OnBack;

    static readonly string[] PumpkinFiles = { "Difficulty/Pumpkin_Lost", "Difficulty/Pumpkin_Hunted", "Difficulty/Pumpkin_Harvested" };
    static Texture2D[] pumpkins;

    int highlighted, lastHighlighted = -1;
    float openedAt, deniedAt = -9f, chosenAt = -9f;
    int deniedCard = -1;
    bool closing;
    bool hoverBack, lastHoverBack;
    readonly AudioSource src;
    static AudioClip tick, choose, deny;
    GUIStyle headStyle, nameStyle, tagStyle, statStyle, navStyle, smallStyle, lockStyle;

    public DifficultyPanel(AudioSource audioSource) { src = audioSource; }

    public void Open()
    {
        openedAt = Time.unscaledTime;
        highlighted = Difficulty.Current;
        lastHighlighted = highlighted;
        closing = false;
        chosenAt = -9f;
    }

    static Texture2D Pumpkin(int i)
    {
        if (pumpkins == null) pumpkins = new Texture2D[PumpkinFiles.Length];
        if (pumpkins[i] == null) pumpkins[i] = Resources.Load<Texture2D>(PumpkinFiles[i]);
        return pumpkins[i];
    }

    // ---------------- input ----------------

    public void Update()
    {
        if (closing)
        {
            if (Time.unscaledTime - chosenAt > 0.45f) { closing = false; OnBack?.Invoke(); }
            return;
        }
        var kb = Keyboard.current;
        if (kb == null) return;
        int n = Difficulty.All.Length;
        if (kb.leftArrowKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame) { highlighted = (highlighted + n - 1) % n; Tick(); }
        if (kb.rightArrowKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame) { highlighted = (highlighted + 1) % n; Tick(); }
        if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame) Choose(highlighted);
        if (kb.escapeKey.wasPressedThisFrame || kb.backspaceKey.wasPressedThisFrame) Back();
    }

    void Choose(int i)
    {
        if (closing) return;
        if (!Difficulty.IsUnlocked(i))
        {
            deniedAt = Time.unscaledTime; deniedCard = i;
            Play(deny ??= GameAudio.Get("Flashlight_Dead"), 0.9f);
            return;
        }
        Difficulty.Select(i);
        highlighted = i;
        chosenAt = Time.unscaledTime;
        closing = true;
        Play(choose ??= GameAudio.Get("Key_Bell"), 0.45f);
    }

    void Back()
    {
        Play(tick ??= MakeTick(), 0.5f);
        OnBack?.Invoke();
    }

    void Tick() => Play(tick ??= MakeTick(), 0.5f);

    void Play(AudioClip c, float v)
    {
        if (src != null && c != null) src.PlayOneShot(c, v * GameSettings.Fx);
    }

    // ---------------- drawing ----------------

    void Styles()
    {
        if (headStyle != null) return;
        headStyle = HorrorUI.Style(HorrorUI.TitleFont, TextAnchor.MiddleLeft);
        nameStyle = HorrorUI.Style(HorrorUI.SerifFont, TextAnchor.MiddleCenter);
        tagStyle = HorrorUI.Style(HorrorUI.TypeFont, TextAnchor.UpperCenter);
        tagStyle.wordWrap = true;
        statStyle = HorrorUI.Style(HorrorUI.TypeFont, TextAnchor.MiddleCenter);
        navStyle = HorrorUI.Style(HorrorUI.SerifFont, TextAnchor.MiddleCenter);
        smallStyle = HorrorUI.Style(HorrorUI.TypeFont, TextAnchor.MiddleCenter);
        lockStyle = HorrorUI.Style(HorrorUI.TitleFont, TextAnchor.MiddleCenter);
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
        int n = Difficulty.All.Length;

        // Cards: 3 across.
        const float cardW = 500f, cardH = 690f, gap = 60f, cardY = 200f;
        float startX = (1920f - (n * cardW + (n - 1) * gap)) * 0.5f;
        var backR = R(110, 950, 240, 70);

        // ---- mouse ----
        if (alpha > 0.5f && !closing)
        {
            Vector2 m = e.mousePosition;
            for (int i = 0; i < n; i++)
            {
                var cr = R(startX + i * (cardW + gap), cardY, cardW, cardH);
                if (cr.Contains(m))
                {
                    if (highlighted != i && (e.type == EventType.MouseMove || paint || e.type == EventType.MouseDown)) highlighted = i;
                    if (e.type == EventType.MouseDown && e.button == 0) { Choose(i); e.Use(); }
                }
            }
            hoverBack = backR.Contains(m);
            if (hoverBack && e.type == EventType.MouseDown && e.button == 0) { Back(); e.Use(); }
        }
        if (!paint) return;
        if (highlighted != lastHighlighted) { lastHighlighted = highlighted; Tick(); }
        if (hoverBack != lastHoverBack) { lastHoverBack = hoverBack; if (hoverBack) Tick(); }

        HorrorUI.Fill(new Rect(0, 0, sw, sh), new Color(0f, 0f, 0f, 0.82f * alpha));

        headStyle.fontSize = Fs(96);
        float hw = headStyle.CalcSize(new GUIContent("CHOOSE YOUR NIGHT")).x + 5f * k * 17;
        HorrorUI.Shaky(new Rect(ox + 110f * k, oy + 50f * k, hw, 110f * k), "CHOOSE YOUR NIGHT", headStyle, HorrorUI.A(HorrorUI.Ember, alpha),
                       now, 1.4f * k, 5f * k, 2f * k, 2f, 0.08f, 12);

        for (int i = 0; i < n; i++)
        {
            float delay = 0.08f + i * 0.12f;
            float ca = alpha * HorrorUI.Smooth((now - openedAt - delay) / 0.45f);
            DrawCard(i, R(startX + i * (cardW + gap), cardY + (1f - HorrorUI.Smooth((now - openedAt - delay) / 0.45f)) * 30f, cardW, cardH), ca, k, Fs, now);
        }

        // ---- back ----
        navStyle.fontSize = Fs(42);
        if (hoverBack)
        {
            float tw = navStyle.CalcSize(new GUIContent("BACK")).x;
            float flick = 0.75f + 0.25f * Mathf.PerlinNoise(now * 9f, 5f);
            HorrorUI.Glow(new Rect(backR.center.x - tw * 0.5f - 34f * k, backR.center.y - 12f * k, 24f * k, 24f * k), HorrorUI.A(HorrorUI.Ember, alpha * flick));
            HorrorUI.Shaky(backR, "BACK", navStyle, HorrorUI.A(HorrorUI.Ember, alpha), now, 1.2f * k, 0f, 1.5f * k, 2f, 0.08f, 50);
        }
        else HorrorUI.Text(backR, "BACK", navStyle, HorrorUI.A(HorrorUI.Bone, 0.8f * alpha), 0f, 2f * k);
        smallStyle.fontSize = Fs(18);
        HorrorUI.Text(R(0, 1028, 1920, 30), "A / D  or  ← →  to look          ENTER or click  to choose          ESC  to go back", smallStyle,
                      HorrorUI.A(HorrorUI.Ash, 0.55f * alpha), 0f, 1.5f * k);
    }

    void DrawCard(int i, Rect card, float a, float k, System.Func<float, int> Fs, float now)
    {
        if (a <= 0.01f) return;
        var d = Difficulty.All[i];
        bool locked = !Difficulty.IsUnlocked(i);
        bool hot = i == highlighted;
        bool current = i == Difficulty.Current;
        bool nightmare = i == Difficulty.All.Length - 1;
        Color accent = nightmare ? HorrorUI.BloodBright : HorrorUI.Ember;

        // Deny shake / choose pulse.
        if (i == deniedCard && now - deniedAt < 0.4f)
        {
            float u = (now - deniedAt) / 0.4f;
            card.x += Mathf.Sin(u * 40f) * 10f * k * (1f - u);
        }
        float chosenPulse = current && now - chosenAt < 0.5f ? 1f - (now - chosenAt) / 0.5f : 0f;

        // Panel.
        HorrorUI.Fill(card, HorrorUI.A(new Color(0.04f, 0.03f, 0.02f), 0.85f * a));
        if (hot && !locked)
            HorrorUI.Glow(new Rect(card.x - 40f * k, card.y - 40f * k, card.width + 80f * k, card.height + 80f * k),
                          HorrorUI.A(accent, (0.10f + 0.25f * chosenPulse) * a));
        float bw = Mathf.Max(1f, (hot ? 3f : 1.5f) * k);
        Color border = locked ? HorrorUI.A(HorrorUI.Ash, 0.25f) : hot ? accent : current ? HorrorUI.EmberDim : HorrorUI.A(HorrorUI.Rust, 0.8f);
        Border(card, bw, HorrorUI.A(border, border.a * a));

        // The pumpkin: bobs gently; the nightmare one twitches. Locked ones are dark silhouettes.
        var tex = Pumpkin(i);
        float bob = Mathf.Sin(now * 1.6f + i) * 6f * k;
        float jx = 0f, jy = 0f;
        if (nightmare && !locked && Mathf.PerlinNoise(now * 3f, 7f) > 0.72f) { jx = (Random.value - 0.5f) * 6f * k; jy = (Random.value - 0.5f) * 6f * k; }
        float ps = (hot ? 380f : 350f) * k;
        var pr = new Rect(card.center.x - ps * 0.5f + jx, card.y + 30f * k + bob + jy + (hot ? -10f * k : 0f), ps, ps);
        if (tex != null)
        {
            var old = GUI.color;
            float lit = locked ? 0.06f : hot ? 1f : 0.7f;
            if (!locked && hot) lit *= 0.9f + 0.1f * Mathf.PerlinNoise(now * 7f, i * 3f); // candle flicker inside
            GUI.color = HorrorUI.Out(new Color(lit, lit, lit, a));
            GUI.DrawTexture(pr, HorrorUI.Tex(tex), ScaleMode.ScaleToFit, true);
            GUI.color = old;
        }
        if (locked)
        {
            lockStyle.fontSize = Fs(56);
            HorrorUI.Text(new Rect(card.x, pr.center.y - 40f * k, card.width, 80f * k), "LOCKED", lockStyle,
                          HorrorUI.A(HorrorUI.Blood, 0.9f * a), 0f, 3f * k);
        }

        // Name.
        float y = card.y + 420f * k;
        nameStyle.fontSize = Fs(58);
        Color nc = locked ? HorrorUI.A(HorrorUI.Ash, 0.5f) : hot ? accent : HorrorUI.A(HorrorUI.Bone, 0.85f);
        if (hot && !locked)
            HorrorUI.Shaky(new Rect(card.x, y, card.width, 70f * k), d.name, nameStyle, HorrorUI.A(nc, a), now, 1.2f * k, 2f * k, 1.5f * k, 2f, 0.06f, 60 + i);
        else
            HorrorUI.Text(new Rect(card.x, y, card.width, 70f * k), d.name, nameStyle, HorrorUI.A(nc, nc.a * a), 0f, 2f * k);

        // Tagline / what unlocks it.
        tagStyle.fontSize = Fs(21);
        string tag = locked ? $"Escape on {Difficulty.All[i - 1].name} to unlock." : d.tagline;
        HorrorUI.Text(new Rect(card.x + 36f * k, y + 74f * k, card.width - 72f * k, 60f * k), tag, tagStyle,
                      HorrorUI.A(locked ? HorrorUI.BloodBright : HorrorUI.Bone, (locked ? 0.7f : 0.75f) * a), 0f, 1.5f * k);

        // What changes (hidden while locked).
        statStyle.fontSize = Fs(19);
        string l1, l2;
        if (locked) { l1 = "? ? ?"; l2 = ""; }
        else
        {
            l1 = $"{d.hearts} heart{(d.hearts == 1 ? "" : "s")}   ·   {d.mazeCells}x{d.mazeCells} maze   ·   {d.keys} keys";
            l2 = $"{d.startPumpkins} pumpkin{(d.startPumpkins == 1 ? "" : "s")} waiting   ·   {d.batteryLife:0}s of light";
        }
        HorrorUI.Fill(new Rect(card.center.x - 90f * k, y + 146f * k, 180f * k, Mathf.Max(1f, 1.5f * k)), HorrorUI.A(HorrorUI.Rust, 0.8f * a));
        HorrorUI.Text(new Rect(card.x, y + 160f * k, card.width, 30f * k), l1, statStyle, HorrorUI.A(HorrorUI.Ash, 0.9f * a), 0f, 1.5f * k);
        HorrorUI.Text(new Rect(card.x, y + 190f * k, card.width, 30f * k), l2, statStyle, HorrorUI.A(HorrorUI.Ash, 0.9f * a), 0f, 1.5f * k);

        // Best escape / chosen marker.
        float best = SaveData.D.bestTime[i];
        if (!locked && best > 0f)
        {
            int mi = Mathf.FloorToInt(best / 60f), se = Mathf.FloorToInt(best % 60f);
            HorrorUI.Text(new Rect(card.x, y + 222f * k, card.width, 30f * k), $"best escape  {mi}:{se:00}", statStyle,
                          HorrorUI.A(HorrorUI.Brass, 0.8f * a), 0f, 1.5f * k);
        }
        if (current)
        {
            smallStyle.fontSize = Fs(17);
            HorrorUI.Text(new Rect(card.x, card.yMax - 34f * k, card.width, 26f * k), "—  CHOSEN  —", smallStyle,
                          HorrorUI.A(accent, 0.85f * a), 0f, 1.5f * k);
        }
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
        var data = new float[n];
        var rng = new System.Random(21);
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)sr;
            data[i] = (Mathf.Sin(2f * Mathf.PI * 135f * t) * 0.6f + (float)(rng.NextDouble() * 2 - 1) * 0.25f) * Mathf.Exp(-t * 90f) * 0.5f;
        }
        var c = AudioClip.Create("DifficultyTick", n, 1, sr, false);
        c.SetData(data, 0);
        return c;
    }
}
