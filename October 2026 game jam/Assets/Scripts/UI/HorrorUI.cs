using UnityEngine;

// Shared look for every on-screen element: fonts, palette and OnGUI drawing helpers
// (shaky per-letter titles, chromatic ghosting, film grain, vignette, soft glows).
// Fonts live in Assets/Resources/Fonts (all SIL Open Font / Apache licensed Google Fonts):
//   Creepster      - big Halloween titles          (TitleFont)
//   Nosifer        - dripping GAME OVER            (DripFont)
//   IM Fell SC     - old-print narration/subtitles (SerifFont)
//   Special Elite  - typewriter HUD labels/prompts (TypeFont)
public static class HorrorUI
{
    // ---------------- Palette ----------------
    public static readonly Color Ember = new Color(0.96f, 0.48f, 0.12f);
    public static readonly Color EmberDim = new Color(0.55f, 0.24f, 0.06f);
    public static readonly Color Blood = new Color(0.58f, 0.02f, 0.03f);
    public static readonly Color BloodBright = new Color(0.86f, 0.07f, 0.05f);
    public static readonly Color Bone = new Color(0.87f, 0.82f, 0.70f);
    public static readonly Color Ash = new Color(0.52f, 0.49f, 0.45f);
    public static readonly Color Brass = new Color(0.86f, 0.66f, 0.32f);
    public static readonly Color Rust = new Color(0.45f, 0.22f, 0.10f);
    public static readonly Color Cold = new Color(0.58f, 0.74f, 0.86f);

    public static Color A(Color c, float a) { c.a = a; return c; }

    // ---------------- Fonts ----------------
    static Font title, drip, serif, type, fallback;
    public static Font TitleFont => Get(ref title, "Fonts/Creepster");
    public static Font DripFont => Get(ref drip, "Fonts/Nosifer");
    public static Font SerifFont => Get(ref serif, "Fonts/FellEnglishSC");
    public static Font TypeFont => Get(ref type, "Fonts/SpecialElite");

    static Font Get(ref Font f, string path)
    {
        if (f == null) f = Resources.Load<Font>(path);
        if (f == null)
        {
            if (fallback == null) fallback = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return fallback;
        }
        return f;
    }

    public static GUIStyle Style(Font font, TextAnchor align = TextAnchor.MiddleCenter)
    {
        var s = new GUIStyle
        {
            font = font,
            alignment = align,
            wordWrap = false,
            richText = false,
            clipping = TextClipping.Overflow,
            padding = new RectOffset(0, 0, 0, 0),
        };
        return s;
    }

    public static int Px(float sizeAt1080) => Mathf.Max(1, Mathf.RoundToInt(sizeAt1080 * Screen.height / 1080f));

    // ---------------- Text ----------------

    // Text with a soft dark shadow and optional red/cyan chromatic ghosts.
    public static void Text(Rect r, string s, GUIStyle st, Color c, float chroma = 0f, float shadow = 2f)
    {
        if (c.a <= 0.002f || string.IsNullOrEmpty(s)) return;
        if (shadow > 0f)
        {
            st.normal.textColor = new Color(0f, 0f, 0f, c.a * 0.85f);
            GUI.Label(new Rect(r.x + shadow, r.y + shadow, r.width, r.height), s, st);
        }
        if (chroma > 0f)
        {
            st.normal.textColor = new Color(1f, 0.08f, 0.05f, c.a * 0.38f);
            GUI.Label(new Rect(r.x - chroma, r.y, r.width, r.height), s, st);
            st.normal.textColor = new Color(0.1f, 0.75f, 1f, c.a * 0.22f);
            GUI.Label(new Rect(r.x + chroma, r.y + chroma * 0.3f, r.width, r.height), s, st);
        }
        st.normal.textColor = c;
        GUI.Label(r, s, st);
    }

    static GUIStyle charStyle;

    // Title drawn letter by letter, centred in r. Each letter trembles, tilts and occasionally gutters out
    // like a dying bulb. spacing is extra space between letters (pixels).
    public static void Shaky(Rect r, string s, GUIStyle st, Color c, float t, float jitter, float spacing = 0f,
                             float chroma = 0f, float tilt = 3f, float flicker = 0.18f, int seed = 0)
    {
        if (c.a <= 0.002f || string.IsNullOrEmpty(s)) return;
        if (charStyle == null) charStyle = Style(st.font, TextAnchor.MiddleLeft);
        charStyle.font = st.font;
        charStyle.fontSize = st.fontSize;
        charStyle.fontStyle = st.fontStyle;

        int n = s.Length;
        var w = new float[n];
        float total = 0f;
        for (int i = 0; i < n; i++)
        {
            w[i] = charStyle.CalcSize(new GUIContent(s[i].ToString())).x;
            if (s[i] == ' ') w[i] = Mathf.Max(w[i], st.fontSize * 0.35f);
            total += w[i] + (i < n - 1 ? spacing : 0f);
        }
        float x = r.center.x - total * 0.5f;
        var m = GUI.matrix;
        for (int i = 0; i < n; i++)
        {
            if (s[i] != ' ')
            {
                float k = i * 3.17f + seed * 11.3f;
                float dx = (Mathf.PerlinNoise(t * 7.5f, k) - 0.5f) * 2f * jitter;
                float dy = (Mathf.PerlinNoise(k, t * 6.3f) - 0.5f) * 2f * jitter;
                float ang = (Mathf.PerlinNoise(t * 2.1f, k + 40f) - 0.5f) * 2f * tilt;
                var cc = c;
                if (flicker > 0f && Mathf.PerlinNoise(t * 5f, k + 90f) > 1f - flicker * 0.5f) cc.a *= 0.3f;
                var rr = new Rect(x + dx, r.y + dy, w[i] + 4f, r.height);
                GUIUtility.RotateAroundPivot(ang, new Vector2(rr.x + w[i] * 0.5f, rr.center.y));
                Text(rr, s[i].ToString(), charStyle, cc, chroma, Mathf.Max(1f, st.fontSize * 0.03f));
                GUI.matrix = m;
            }
            x += w[i] + spacing;
        }
    }

    // ---------------- Overlays ----------------
    static Texture2D grain, vignette, blob;

    // Animated film grain over the whole screen.
    public static void Grain(float alpha)
    {
        if (alpha <= 0.002f || Event.current.type != EventType.Repaint) return;
        if (grain == null)
        {
            const int N = 256;
            grain = new Texture2D(N, N, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Repeat, hideFlags = HideFlags.HideAndDontSave };
            var px = new Color32[N * N];
            var rng = new System.Random(7);
            for (int i = 0; i < px.Length; i++)
            {
                byte v = (byte)rng.Next(0, 256);
                byte a = (byte)(Mathf.Abs(v - 128) * 2);
                px[i] = new Color32(v, v, v, a);
            }
            grain.SetPixels32(px);
            grain.Apply(false, false);
        }
        float sw = Screen.width, sh = Screen.height, scale = 2f;
        var old = GUI.color;
        GUI.color = new Color(1f, 1f, 1f, alpha);
        GUI.DrawTextureWithTexCoords(new Rect(0, 0, sw, sh), grain,
            new Rect(Random.value, Random.value, sw / (256f * scale), sh / (256f * scale)));
        GUI.color = old;
    }

    // Dark edges. tint lets the edges bleed a colour (e.g. dark red on game over).
    public static void Vignette(float alpha, Color tint)
    {
        if (alpha <= 0.002f) return;
        if (vignette == null)
        {
            const int N = 128;
            vignette = new Texture2D(N, N, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
            var px = new Color[N * N];
            for (int j = 0; j < N; j++)
            for (int i = 0; i < N; i++)
            {
                float u = (i + 0.5f) / N * 2f - 1f, v = (j + 0.5f) / N * 2f - 1f;
                float d = Mathf.Sqrt(u * u * 0.8f + v * v);
                float a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.45f, 1.25f, d));
                px[j * N + i] = new Color(1f, 1f, 1f, a);
            }
            vignette.SetPixels(px);
            vignette.Apply(false, false);
        }
        var old = GUI.color;
        GUI.color = new Color(tint.r, tint.g, tint.b, alpha);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), vignette, ScaleMode.StretchToFill, true);
        GUI.color = old;
    }

    // Soft round glow (e.g. an ember haze behind a title).
    public static void Glow(Rect r, Color c)
    {
        if (c.a <= 0.002f) return;
        var old = GUI.color;
        GUI.color = c;
        GUI.DrawTexture(r, BlobTexture, ScaleMode.StretchToFill, true);
        GUI.color = old;
    }

    public static Texture2D BlobTexture
    {
        get
        {
            if (blob != null) return blob;
            const int N = 128;
            blob = new Texture2D(N, N, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
            var px = new Color[N * N];
            for (int j = 0; j < N; j++)
            for (int i = 0; i < N; i++)
            {
                float u = (i + 0.5f) / N * 2f - 1f, v = (j + 0.5f) / N * 2f - 1f;
                float d = Mathf.Clamp01(Mathf.Sqrt(u * u + v * v));
                float a = (1f - d) * (1f - d);
                px[j * N + i] = new Color(1f, 1f, 1f, a);
            }
            blob.SetPixels(px);
            blob.Apply(false, false);
            return blob;
        }
    }

    static Sprite blobSprite;
    public static Sprite BlobSprite
    {
        get
        {
            if (blobSprite == null)
            {
                var t = BlobTexture;
                blobSprite = Sprite.Create(t, new Rect(0, 0, t.width, t.height), new Vector2(0.5f, 0.5f), 100f);
                blobSprite.hideFlags = HideFlags.HideAndDontSave;
            }
            return blobSprite;
        }
    }

    public static void Fill(Rect r, Color c)
    {
        if (c.a <= 0.002f) return;
        var old = GUI.color;
        GUI.color = c;
        GUI.DrawTexture(r, Texture2D.whiteTexture);
        GUI.color = old;
    }

    public static float Smooth(float x) { x = Mathf.Clamp01(x); return x * x * (3f - 2f * x); }

    public static string NumberWord(int n)
    {
        switch (n)
        {
            case 0: return "No";
            case 1: return "One";
            case 2: return "Two";
            case 3: return "Three";
            case 4: return "Four";
            case 5: return "Five";
            case 6: return "Six";
            default: return n.ToString();
        }
    }
}
