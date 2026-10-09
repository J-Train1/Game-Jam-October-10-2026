using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

// After a jump scare ends on black: the heart bar fades in, the heart you just lost shakes, cracks down the
// middle and its halves fall away (leaving an empty slot), then the hearts you have left pulse like a heartbeat.
//  - Hearts left: everything resets out of sight (you're back at the start of the SAME maze, keys and unlocked
//    locks kept, stamina and battery refilled, every pumpkin moved far away), then the black fades back to play.
//  - No hearts left: GAME OVER, then PLAY AGAIN (new maze) / BACK TO MENU buttons.
// Created on demand by JumpScare; no scene setup. Hearts are drawn in code (no art needed).
public class DeathScreen : MonoBehaviour
{
    public static bool IsShowing { get; private set; }

    [Header("Timing (seconds)")]
    public float heartsIn = 0.5f;
    public float holdBefore = 0.45f;
    public float shakeTime = 0.45f;
    public float breakTime = 1.0f;
    [Tooltip("How long the hearts and text stay on screen after the heart breaks, before respawning.")]
    public float holdAfter = 4f;
    public float heartsOut = 0.5f;
    public float fadeBackIn = 1.1f;
    public float gameOverIn = 1.0f;
    public float promptDelay = 1.2f;

    [Header("Look")]
    public float heartSize = 130f;   // at 1080p
    public float heartGap = 34f;

    PumpkinMonster catcher;
    int max, before, after;
    bool gameOver, respawned, released, crackPlayed, canRestart, gameOverPlayed;
    EndChoice choice;
    float start;
    Texture2D full, empty, halfL, halfR;
    AudioSource src;
    AudioClip crackClip;
    GUIStyle titleStyle, promptStyle, subStyle, flavorStyle;
    string flavor, gameOverLine;

    public static void Begin(PumpkinMonster catcher)
    {
        var go = new GameObject("DeathScreen");
        var d = go.AddComponent<DeathScreen>();
        d.catcher = catcher;
    }

    void Awake()
    {
        IsShowing = true;
        start = Time.unscaledTime;
    }

    void Start()
    {
        var lives = PlayerLives.Get();
        max = lives.Max;
        before = lives.Remaining;
        after = lives.LoseLife();
        gameOver = after <= 0;
        flavor = after == 1 ? Pick(OneLines) : Pick(TwoPlusLines);
        gameOverLine = Pick(GameOverLines);

        full = MakeHeart(HeartPart.Full);
        empty = MakeHeart(HeartPart.Empty);
        halfL = MakeHeart(HeartPart.Left);
        halfR = MakeHeart(HeartPart.Right);

        src = gameObject.AddComponent<AudioSource>();
        src.spatialBlend = 0f;
        src.playOnAwake = false;
        crackClip = MakeCrack();
        Debug.Log(gameOver ? "[Death] last heart lost: GAME OVER" : $"[Death] heart lost, {after} left: respawning");
    }

    void OnDestroy()
    {
        IsShowing = false;
        if (full) Destroy(full);
        if (empty) Destroy(empty);
        if (halfL) Destroy(halfL);
        if (halfR) Destroy(halfR);
    }

    float T => Time.unscaledTime - start;
    float TShake => heartsIn + holdBefore;
    float TBreak => TShake + shakeTime;
    float TBroken => TBreak + breakTime;
    float TOut => TBroken + holdAfter;
    float TFade => TOut + heartsOut;
    float TGameOver => TBroken + 0.6f;

    void Update()
    {
        float t = T;
        if (!crackPlayed && t >= TBreak)
        {
            crackPlayed = true;
            var snap = GameAudio.CrackSmall();
            if (snap != null) src.PlayOneShot(snap, 0.9f * GameSettings.Fx); else if (crackClip) src.PlayOneShot(crackClip, 0.9f * GameSettings.Fx);
            var thud = GameAudio.Get("Impact_Low");
            if (thud != null) src.PlayOneShot(thud, (gameOver ? 0.8f : 0.45f) * GameSettings.Fx);
        }
        if (gameOver && !gameOverPlayed && t >= TGameOver)
        {
            gameOverPlayed = true;
            var g = GameAudio.Get("Game_Over");
            if (g != null) src.PlayOneShot(g, 0.85f * GameSettings.Fx);
        }

        if (!gameOver)
        {
            if (!respawned && t >= TOut) { respawned = true; Respawn(); }               // still fully black
            if (!released && t >= TFade + fadeBackIn * 0.35f) { released = true; Release(); }
            if (t >= TFade + fadeBackIn) Destroy(gameObject);
            return;
        }

        if (!canRestart && t >= TGameOver + gameOverIn + promptDelay * 0.5f) canRestart = true;
        if (choice == null) choice = new EndChoice(src);
        choice.Update(canRestart);
    }

    // Back to the start of the same maze; everything that could hurt you moved far away. Hidden under black.
    void Respawn()
    {
        PumpkinMonster.ResetRound();
        var gen = MazeGenerator.Instance;
        var pc = PlayerController.Instance;
        Vector3 spawn = gen != null && gen.Grid != null ? gen.Grid.TileToWorld(gen.Grid.startTile) + Vector3.up * 0.05f
                                                         : (pc != null ? pc.transform.position : Vector3.zero);
        if (pc != null)
        {
            pc.Respawn(spawn, Quaternion.identity);
            pc.enabled = true;
            pc.InputEnabled = false; // until the fade is mostly done
        }
        if (Flashlight.Instance != null) Flashlight.Instance.AddBattery(Flashlight.Instance.maxBattery);

        if (PumpkinSpawner.Instance != null) PumpkinSpawner.Instance.RelocateAll(spawn);
        else if (catcher != null) catcher.ResetTo(catcher.transform.position);

        var hud = FindFirstObjectByType<FlashlightHUD>(FindObjectsInactive.Include);
        if (hud != null) hud.gameObject.SetActive(true);
    }

    void Release()
    {
        var pc = PlayerController.Instance;
        if (pc != null) pc.InputEnabled = true;
    }

    // ---------------- Drawing ----------------

    void OnGUI()
    {
        GUI.depth = -1500;
        float t = T, sw = Screen.width, sh = Screen.height, k = sh / 1080f;

        // Black backdrop (fades out at the very end when respawning).
        float blackA = 1f;
        if (!gameOver && t >= TFade) blackA = 1f - Smooth((t - TFade) / fadeBackIn);
        Draw(new Rect(0, 0, sw, sh), Texture2D.whiteTexture, new Color(0f, 0f, 0f, blackA));

        // Hearts.
        float heartsA = Mathf.Clamp01(t / heartsIn);
        if (!gameOver && t >= TOut) heartsA *= 1f - Mathf.Clamp01((t - TOut) / heartsOut);
        if (gameOver && t >= TGameOver) heartsA *= Mathf.Lerp(1f, 0.35f, Mathf.Clamp01((t - TGameOver) / 0.6f));

        float size = heartSize * k, gap = heartGap * k;
        float totalW = max * size + (max - 1) * gap;
        float x0 = (sw - totalW) * 0.5f;
        float y = sh * (gameOver ? 0.36f : 0.42f) - size * 0.5f;
        int lost = before - 1;
        float beat = Heartbeat(t);

        if (heartsA > 0.001f)
        {
            // A dim blood haze behind the row that swells with the heartbeat.
            float haze = heartsA * (0.22f + 0.12f * beat);
            HorrorUI.Glow(new Rect(x0 - size, y - size * 0.6f, totalW + size * 2f, size * 2.2f), HorrorUI.A(HorrorUI.Blood, haze));

            for (int i = 0; i < max; i++)
            {
                var r = new Rect(x0 + i * (size + gap), y, size, size);
                if (i < after)
                {
                    float s = 1f + 0.07f * beat * Mathf.Clamp01((t - TBroken + 0.3f) / 0.3f);
                    Draw(Scale(r, s), full, new Color(1f, 1f, 1f, heartsA));
                }
                else if (i == lost) DrawBreaking(r, t, heartsA, k);
                else Draw(r, empty, new Color(1f, 1f, 1f, heartsA));
            }

            if (!gameOver && t >= TBroken - 0.2f)
            {
                EnsureStyles();
                subStyle.fontSize = HorrorUI.Px(44);
                flavorStyle.fontSize = HorrorUI.Px(25);
                float a = heartsA * Mathf.Clamp01((t - TBroken + 0.2f) / 0.5f);
                float a2 = heartsA * Mathf.Clamp01((t - TBroken - 0.35f) / 0.6f);
                string line = after == 1 ? "ONE HEART REMAINS" : HorrorUI.NumberWord(after).ToUpper() + " HEARTS REMAIN";
                HorrorUI.Shaky(new Rect(0, y + size + 22 * k, sw, 60 * k), line, subStyle, HorrorUI.A(HorrorUI.Bone, a),
                               t, 1.2f * k, 6f * k, 1.5f * k, 1.2f, 0.08f, 3);
                HorrorUI.Text(new Rect(0, y + size + 86 * k, sw, 36 * k), flavor, flavorStyle,
                              HorrorUI.A(HorrorUI.BloodBright, a2 * 0.9f), 0f, 2f * k);
            }
        }

        if (gameOver && t >= TGameOver)
        {
            EnsureStyles();
            titleStyle.fontSize = HorrorUI.Px(112);
            subStyle.fontSize = HorrorUI.Px(36);
            promptStyle.fontSize = HorrorUI.Px(24);
            float a1 = Mathf.Clamp01((t - TGameOver) / gameOverIn);
            float a3 = Mathf.Clamp01((t - TGameOver - gameOverIn * 0.6f) / 1.0f);
            float a2 = Mathf.Clamp01((t - TGameOver - gameOverIn - promptDelay * 0.5f) / 0.8f);

            // The title slams in slightly too big and settles, over a slow blood glow.
            float settle = 1f + 0.25f * Mathf.Pow(1f - Mathf.Clamp01((t - TGameOver) / 0.35f), 3f);
            float ty = sh * 0.52f;
            HorrorUI.Glow(new Rect(sw * 0.15f, ty - 120 * k, sw * 0.7f, 360 * k), HorrorUI.A(HorrorUI.Blood, 0.45f * a1 * (0.85f + 0.15f * Heartbeat(t))));
            int baseSize = titleStyle.fontSize;
            titleStyle.fontSize = Mathf.RoundToInt(baseSize * settle);
            HorrorUI.Shaky(new Rect(0, ty, sw, 140 * k), "GAME OVER", titleStyle, HorrorUI.A(HorrorUI.BloodBright, a1),
                           t, 2.2f * k, 10f * k, 3f * k, 2.5f, 0.12f, 7);
            titleStyle.fontSize = baseSize;

            HorrorUI.Shaky(new Rect(0, ty + 150 * k, sw, 50 * k), gameOverLine, subStyle, HorrorUI.A(HorrorUI.Bone, a3 * 0.85f),
                           t, 0.8f * k, 5f * k, 1f * k, 0.8f, 0.05f, 11);

            if (choice != null) choice.OnGUI(ty + 270 * k, a2, canRestart);
        }

        // Film over everything: dark edges (bleeding red on game over) and grain.
        HorrorUI.Vignette(0.9f * blackA, Color.black);
        if (gameOver && t >= TGameOver)
            HorrorUI.Vignette(Mathf.Clamp01((t - TGameOver) / 1.5f) * (0.35f + 0.15f * Heartbeat(t)), HorrorUI.Blood);
        HorrorUI.Grain(0.07f * blackA);
        if (choice != null) choice.DrawFadeOut();
    }

    void DrawBreaking(Rect r, float t, float alpha, float k)
    {
        // The empty slot shows through once it's broken.
        float emptyA = alpha * Mathf.Clamp01((t - TBreak) / 0.4f);
        if (emptyA > 0.001f) Draw(r, empty, new Color(1f, 1f, 1f, emptyA));

        if (t < TBreak)
        {
            // Shake harder and harder, swelling slightly, before it cracks.
            float s = t >= TShake ? Mathf.Clamp01((t - TShake) / shakeTime) : 0f;
            float dx = Mathf.Sin(t * 75f) * 7f * k * s;
            float dy = Mathf.Cos(t * 63f) * 3f * k * s;
            var rr = Scale(r, 1f + 0.08f * s);
            rr.x += dx; rr.y += dy;
            Draw(rr, full, new Color(1f, 1f, 1f, alpha));
            return;
        }

        float u = Mathf.Clamp01((t - TBreak) / breakTime);
        if (u >= 1f) return;
        float sep = 46f * k * (1f - Mathf.Pow(1f - u, 3f));   // halves spring apart...
        float fall = (190f * u * u - 25f * u) * k;            // ...hop, then fall
        float ang = 28f * u;
        float a = alpha * (1f - Mathf.Pow(u, 1.6f));
        var c = new Color(1f, 1f, 1f, a);

        var m = GUI.matrix;
        var left = new Rect(r.x - sep, r.y + fall, r.width, r.height);
        GUIUtility.RotateAroundPivot(-ang, new Vector2(left.center.x - r.width * 0.15f, left.center.y));
        Draw(left, halfL, c);
        GUI.matrix = m;
        var right = new Rect(r.x + sep, r.y + fall, r.width, r.height);
        GUIUtility.RotateAroundPivot(ang, new Vector2(right.center.x + r.width * 0.15f, right.center.y));
        Draw(right, halfR, c);
        GUI.matrix = m;

        // A brief flash on the crack.
        if (u < 0.12f) Draw(r, Texture2D.whiteTexture, new Color(1f, 0.9f, 0.9f, 0.0f)); // (kept subtle: no full flash)
    }

    void EnsureStyles()
    {
        if (titleStyle != null) return;
        titleStyle = HorrorUI.Style(HorrorUI.DripFont);
        subStyle = HorrorUI.Style(HorrorUI.SerifFont);
        flavorStyle = HorrorUI.Style(HorrorUI.TypeFont);
        promptStyle = HorrorUI.Style(HorrorUI.TypeFont);
    }

    static readonly string[] TwoPlusLines = { "It let you go. For now.", "The corn drags you back...", "Not yet. Not like this.", "Something in the field is laughing." };
    static readonly string[] OneLines = { "One more and you're harvest.", "It's learning where you hide.", "You can hear it breathing now.", "The field wants you to stay." };
    static readonly string[] GameOverLines = { "THE FIELD KEEPS YOU", "YOU BELONG TO THE PATCH NOW", "ANOTHER SCARECROW FOR THE CORN", "NOBODY LEAVES THE MAZE" };
    static string Pick(string[] a) => a[Random.Range(0, a.Length)];

    static void Draw(Rect r, Texture tex, Color c)
    {
        if (tex == null || c.a <= 0.001f) return;
        var old = GUI.color;
        GUI.color = c;
        GUI.DrawTexture(r, tex, ScaleMode.StretchToFill, true);
        GUI.color = old;
    }

    static Rect Scale(Rect r, float s)
    {
        float w = r.width * s, h = r.height * s;
        return new Rect(r.center.x - w * 0.5f, r.center.y - h * 0.5f, w, h);
    }

    static float Smooth(float x) { x = Mathf.Clamp01(x); return x * x * (3f - 2f * x); }

    // Lub-dub every 1.1 s.
    static float Heartbeat(float t)
    {
        float p = t % 1.1f;
        return Mathf.Exp(-Mathf.Pow((p - 0.06f) / 0.05f, 2f)) + 0.6f * Mathf.Exp(-Mathf.Pow((p - 0.26f) / 0.05f, 2f));
    }

    // ---------------- Heart textures ----------------

    enum HeartPart { Full, Empty, Left, Right }

    // Classic heart curve: (x^2 + y^2 - 1)^3 - x^2 y^3 <= 0.
    static bool InHeart(float x, float y) { float a = x * x + y * y - 1f; return a * a * a - x * x * y * y * y <= 0f; }

    // Zigzag crack from the top notch to the bottom tip.
    static readonly Vector2[] Crack =
    {
        new Vector2(0.00f, 1.6f), new Vector2(0.00f, 1.0f), new Vector2(-0.13f, 0.62f), new Vector2(0.11f, 0.22f),
        new Vector2(-0.09f, -0.18f), new Vector2(0.08f, -0.55f), new Vector2(0.00f, -1.3f),
    };

    static float CrackX(float y)
    {
        for (int i = 0; i < Crack.Length - 1; i++)
        {
            var a = Crack[i]; var b = Crack[i + 1];
            if (y <= a.y && y >= b.y) return Mathf.Lerp(a.x, b.x, (a.y - y) / Mathf.Max(0.0001f, a.y - b.y));
        }
        return 0f;
    }

    static Texture2D MakeHeart(HeartPart part)
    {
        const int N = 160;
        var tex = new Texture2D(N, N, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        var px = new Color32[N * N];
        const int SS = 3;
        for (int j = 0; j < N; j++)
        for (int i = 0; i < N; i++)
        {
            int coverOuter = 0, coverInner = 0, coverSide = 0;
            for (int sj = 0; sj < SS; sj++)
            for (int si = 0; si < SS; si++)
            {
                float u = (i + (si + 0.5f) / SS) / N * 2f - 1f;    // -1..1
                float v = (j + (sj + 0.5f) / SS) / N * 2f - 1f;    // -1..1 (bottom to top)
                float x = u * 1.32f, y = v * 1.32f + 0.18f;
                if (!InHeart(x, y)) continue;
                coverOuter++;
                if (InHeart(x / 0.82f, (y - 0.06f) / 0.82f)) coverInner++;
                float cx = CrackX(y);
                if (part == HeartPart.Left && x < cx - 0.035f) coverSide++;
                if (part == HeartPart.Right && x > cx + 0.035f) coverSide++;
            }
            float outerA = coverOuter / (float)(SS * SS);
            float innerA = coverInner / (float)(SS * SS);
            float sideA = coverSide / (float)(SS * SS);

            float uc = (i + 0.5f) / N * 2f - 1f, vc = (j + 0.5f) / N * 2f - 1f;
            float xc = uc * 1.32f, yc = vc * 1.32f + 0.18f;
            Color col;
            if (part == HeartPart.Empty)
            {
                var rim = new Color(0.36f, 0.12f, 0.11f);
                var inside = new Color(0.05f, 0.025f, 0.025f);
                col = Color.Lerp(rim, inside, innerA);
                col.a = outerA * Mathf.Lerp(0.95f, 0.55f, innerA);
            }
            else
            {
                // Deep red with a light upper-left and a dark lower edge, a soft highlight, and a near-black rim.
                float g = Mathf.Clamp01(0.55f + 0.35f * vc - 0.15f * uc);
                var fill = Color.Lerp(new Color(0.22f, 0.0f, 0.02f), new Color(0.78f, 0.06f, 0.07f), g);
                // mottled, like it's still wet
                fill *= Mathf.Lerp(0.8f, 1.05f, Mathf.PerlinNoise(uc * 4.5f + 10f, vc * 4.5f));
                fill.a = 1f;
                float hl = Mathf.Exp(-(Mathf.Pow((uc + 0.42f) / 0.2f, 2f) + Mathf.Pow((vc - 0.38f) / 0.13f, 2f)));
                fill = Color.Lerp(fill, new Color(1f, 0.75f, 0.7f), hl * 0.28f);
                var rim = new Color(0.06f, 0.0f, 0.01f);
                col = Color.Lerp(rim, fill, innerA);
                col.a = outerA;
                if (part != HeartPart.Full)
                {
                    // Dark edge along the crack on each half.
                    float dist = Mathf.Abs(xc - CrackX(yc));
                    if (dist < 0.11f) col = Color.Lerp(rim, col, Mathf.Clamp01((dist - 0.035f) / 0.075f));
                    col.a = sideA;
                }
            }
            px[j * N + i] = col;
        }
        tex.SetPixels32(px);
        tex.Apply(false, true);
        return tex;
    }

    // Placeholder crack: a sharp crunch + a low thud (to be replaced with a recorded sound).
    static AudioClip MakeCrack()
    {
        const int sr = 44100;
        int n = Mathf.RoundToInt(sr * 0.7f);
        var data = new float[n];
        var rng = new System.Random(99);
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)sr;
            float noise = (float)(rng.NextDouble() * 2 - 1);
            float crunch = noise * (Mathf.Exp(-t * 35f) + 0.6f * Mathf.Exp(-Mathf.Abs(t - 0.05f) * 120f));
            float thud = Mathf.Sin(2f * Mathf.PI * (70f - 30f * t) * t) * Mathf.Exp(-t * 9f);
            data[i] = Mathf.Clamp((crunch * 0.6f + thud * 0.9f) * 0.9f, -1f, 1f);
        }
        var clip = AudioClip.Create("HeartCrack", n, 1, sr, false);
        clip.SetData(data, 0);
        return clip;
    }
}
