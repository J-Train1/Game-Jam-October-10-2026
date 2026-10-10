using UnityEngine;
using UnityEngine.InputSystem;

// CREDITS, from the main menu: who made the game and every third-party asset in it.
// The same list is written to CREDITS.txt next to the game when it's built (see JamTools).
public class CreditsPanel
{
    public System.Action OnBack;

    public const string GameTitle = "CREEPERS IN THE CORN";
    public const string Company = "Doll Head Games LLC";
    public const string MadeBy = "James Maher";
    public const string MadeFor = "made for the October 2026 Game Jam";

    public struct Entry { public string what, who; public Entry(string what, string who) { this.what = what; this.who = who; } }
    public struct Section { public string title; public Entry[] entries; }

    public static readonly Section[] Sections =
    {
        new Section
        {
            title = "MODELS",
            entries = new[]
            {
                new Entry("Creepy Pumpkin Monster", "woxec"),
                new Entry("Stylized Cemetery Pack", "Valentine Kurakin"),
                new Entry("Rust Key", "Aleksn09"),
                new Entry("Stylize Free Props", "Erbeilo3D"),
                new Entry("Flooded Grounds", "Sandro T"),
                new Entry("Corn Cluster", "3D model"),
            },
        },
        new Section
        {
            title = "FONTS",
            entries = new[]
            {
                new Entry("Creepster", "Font Diner  ·  SIL Open Font License"),
                new Entry("Nosifer", "Typomondo  ·  SIL Open Font License"),
                new Entry("IM FELL English SC", "Igino Marini  ·  SIL Open Font License"),
                new Entry("Special Elite", "Astigmatic  ·  Apache License 2.0"),
            },
        },
        new Section
        {
            title = "MUSIC & SOUND",
            entries = new[]
            {
                new Entry("Horror Music Pack - Starter Kit", "FictiumSoundDesign"),
                new Entry("Scary Sound FX", "Anthon"),
                new Entry("Flooded Grounds (wind, leaves)", "Sandro T"),
                new Entry("Breaking Wood", "Dragon Studio  ·  Pixabay"),
                new Entry("Wood Break / Wood Stretch", "freesound_community  ·  Pixabay"),
            },
        },
    };

    static Texture2D logo;
    float openedAt;
    bool hoverBack, lastHover;
    readonly AudioSource src;
    static AudioClip tick;
    GUIStyle headStyle, sectionStyle, whatStyle, whoStyle, smallStyle, navStyle, bigStyle, dripStyle;

    public CreditsPanel(AudioSource audioSource) { src = audioSource; }

    public void Open() { openedAt = Time.unscaledTime; hoverBack = lastHover = false; }

    void Back() { Tick(); OnBack?.Invoke(); }

    public void Update()
    {
        var kb = Keyboard.current;
        if (kb == null) return;
        if (kb.escapeKey.wasPressedThisFrame || kb.backspaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame
            || kb.numpadEnterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame) Back();
    }

    void Tick()
    {
        if (src != null) src.PlayOneShot(tick ??= MakeTick(), 0.5f * GameSettings.Fx);
    }

    void Styles()
    {
        if (headStyle != null) return;
        headStyle = HorrorUI.Style(HorrorUI.TitleFont, TextAnchor.MiddleLeft);
        bigStyle = HorrorUI.Style(HorrorUI.TitleFont, TextAnchor.MiddleLeft);
        dripStyle = HorrorUI.Style(HorrorUI.DripFont, TextAnchor.MiddleLeft);
        sectionStyle = HorrorUI.Style(HorrorUI.SerifFont, TextAnchor.MiddleLeft);
        whatStyle = HorrorUI.Style(HorrorUI.TypeFont, TextAnchor.MiddleLeft);
        whoStyle = HorrorUI.Style(HorrorUI.TypeFont, TextAnchor.MiddleLeft);
        smallStyle = HorrorUI.Style(HorrorUI.TypeFont, TextAnchor.MiddleLeft);
        navStyle = HorrorUI.Style(HorrorUI.SerifFont, TextAnchor.MiddleCenter);
    }

    /// <summary>Draws the whole screen. alpha fades it in.</summary>
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
        float t = Time.unscaledTime - openedAt;
        // Things fade in one after another, like a film's end titles.
        float A(float delay) => alpha * HorrorUI.Smooth((t - delay) / 0.6f);

        var backR = R(1560, 950, 240, 70);
        hoverBack = alpha > 0.5f && backR.Contains(e.mousePosition);
        if (hoverBack && e.type == EventType.MouseDown && e.button == 0) { Back(); e.Use(); }
        if (hoverBack != lastHover && paint) { if (hoverBack) Tick(); lastHover = hoverBack; }
        if (!paint) return;

        HorrorUI.Fill(new Rect(0, 0, sw, sh), new Color(0f, 0f, 0f, 0.8f * alpha));

        headStyle.fontSize = Fs(96);
        float hw = headStyle.CalcSize(new GUIContent("CREDITS")).x + 5f * k * 7;
        HorrorUI.Shaky(new Rect(ox + 110f * k, oy + 50f * k, hw, 110f * k), "CREDITS", headStyle, HorrorUI.A(HorrorUI.Ember, alpha),
                       Time.unscaledTime, 1.4f * k, 5f * k, 2f * k, 2f, 0.08f, 7);

        // ---- left column: models, fonts ----
        float y = 210f, delay = 0.2f;
        DrawSection(Sections[0], 110f, ref y, ref delay, R, Fs, A, k);
        y += 26f;
        DrawSection(Sections[1], 110f, ref y, ref delay, R, Fs, A, k);

        // ---- right column: the game, music & sound ----
        float x2 = 1040f, y2 = 200f;
        if (logo == null) logo = Resources.Load<Texture2D>("Logo/DollHeadLogo");
        float tx = x2;
        if (logo != null)
        {
            float lh = 176f, lw = lh * logo.width / logo.height;
            float la = A(0.05f);
            float flick = 0.85f + 0.15f * Mathf.PerlinNoise(Time.unscaledTime * 2.3f, 9f);
            HorrorUI.Glow(R(x2 - 40f, y2 - 30f, lw + 80f, lh + 60f), HorrorUI.A(HorrorUI.Ember, 0.22f * la * flick));
            var old = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, la * flick);
            GUI.DrawTexture(R(x2, y2, lw, lh), logo, ScaleMode.ScaleToFit, true);
            GUI.color = old;
            tx = x2 + lw + 34f;
        }
        smallStyle.fontSize = Fs(22);
        HorrorUI.Text(R(tx, y2 + 6, 700, 32), "A GAME BY", smallStyle, HorrorUI.A(HorrorUI.Ash, 0.85f * A(0.1f)), 0f, 2f * k);
        sectionStyle.fontSize = Fs(46);
        HorrorUI.Text(R(tx, y2 + 36, 700, 58), Company, sectionStyle, HorrorUI.A(HorrorUI.Ember, A(0.15f)), 0f, 3f * k);
        whatStyle.fontSize = Fs(30);
        HorrorUI.Text(R(tx, y2 + 92, 700, 40), MadeBy, whatStyle, HorrorUI.A(HorrorUI.Bone, A(0.2f)), 0f, 2f * k);
        smallStyle.fontSize = Fs(20);
        HorrorUI.Text(R(tx, y2 + 136, 700, 28), MadeFor, smallStyle, HorrorUI.A(HorrorUI.Bone, 0.65f * A(0.25f)), 0f, 2f * k);
        HorrorUI.Text(R(tx, y2 + 162, 700, 28), "made with Unity", smallStyle, HorrorUI.A(HorrorUI.Bone, 0.65f * A(0.3f)), 0f, 2f * k);
        y2 += 228f;
        float d2 = 0.5f;
        DrawSection(Sections[2], x2, ref y2, ref d2, R, Fs, A, k);

        dripStyle.fontSize = Fs(40);
        HorrorUI.Text(R(x2, y2 + 40f, 800, 70), "Thanks for playing.", dripStyle, HorrorUI.A(HorrorUI.Blood, A(delay + 0.4f)), 0f, 2f * k);
        smallStyle.fontSize = Fs(20);
        HorrorUI.Text(R(x2, y2 + 112f, 800, 30), "...don't go back into the corn.", smallStyle, HorrorUI.A(HorrorUI.Ash, 0.7f * A(delay + 1.4f)), 0f, 2f * k);

        // ---- back ----
        navStyle.fontSize = Fs(42);
        if (hoverBack)
        {
            float tw = navStyle.CalcSize(new GUIContent("BACK")).x;
            float flick = 0.75f + 0.25f * Mathf.PerlinNoise(Time.unscaledTime * 9f, 3f);
            HorrorUI.Glow(new Rect(backR.center.x - tw * 0.5f - 34f * k, backR.center.y - 12f * k, 24f * k, 24f * k), HorrorUI.A(HorrorUI.Ember, alpha * flick));
            HorrorUI.Shaky(backR, "BACK", navStyle, HorrorUI.A(HorrorUI.Ember, alpha), Time.unscaledTime, 1.2f * k, 0f, 1.5f * k, 2f, 0.08f, 44);
        }
        else HorrorUI.Text(backR, "BACK", navStyle, HorrorUI.A(HorrorUI.Bone, 0.8f * alpha), 0f, 2f * k);
    }

    void DrawSection(Section s, float x, ref float y, ref float delay, System.Func<float, float, float, float, Rect> R,
                     System.Func<float, int> Fs, System.Func<float, float> A, float k)
    {
        sectionStyle.fontSize = Fs(36);
        float a = A(delay);
        HorrorUI.Text(R(x, y, 760, 46), s.title, sectionStyle, HorrorUI.A(HorrorUI.Ember, a), 0f, 2f * k);
        HorrorUI.Fill(R(x, y + 48f, 180, 2), HorrorUI.A(HorrorUI.Rust, a));
        y += 62f;
        delay += 0.12f;
        whatStyle.fontSize = Fs(27);
        whoStyle.fontSize = Fs(20);
        foreach (var en in s.entries)
        {
            a = A(delay);
            HorrorUI.Text(R(x, y, 800, 34), en.what, whatStyle, HorrorUI.A(HorrorUI.Bone, 0.92f * a), 0f, 2f * k);
            HorrorUI.Text(R(x + 24f, y + 30f, 800, 26), en.who, whoStyle, HorrorUI.A(HorrorUI.Ash, 0.9f * a), 0f, 1.5f * k);
            y += 62f;
            delay += 0.08f;
        }
    }

    /// <summary>The credits as plain text (written next to the built game).</summary>
    public static string AsText()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine(GameTitle);
        sb.AppendLine("A game by " + Company + " (" + MadeBy + "), " + MadeFor + ". Made with Unity.");
        sb.AppendLine();
        foreach (var s in Sections)
        {
            sb.AppendLine(s.title);
            foreach (var e in s.entries) sb.AppendLine("  " + e.what + "  -  " + e.who.Replace("  ·  ", ", "));
            sb.AppendLine();
        }
        sb.AppendLine("Font licenses are in the Licenses folder.");
        return sb.ToString();
    }

    static AudioClip MakeTick()
    {
        const int sr = 44100;
        int n = sr / 16;
        var d = new float[n];
        var rng = new System.Random(17);
        for (int i = 0; i < n; i++)
        {
            float tt = i / (float)sr;
            d[i] = (Mathf.Sin(2f * Mathf.PI * 140f * tt) * 0.6f + (float)(rng.NextDouble() * 2 - 1) * 0.25f) * Mathf.Exp(-tt * 90f) * 0.5f;
        }
        var c = AudioClip.Create("CreditsTick", n, 1, sr, false);
        c.SetData(d, 0);
        return c;
    }
}
