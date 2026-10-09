using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Makes the night camera look like worn, cheap film.
//  1) Pushes the Global Volume harder at runtime: heavier grain (also in bright areas), darker edges,
//     more contrast, less colour and a little lens fringing. Only a runtime copy of the profile is changed,
//     so Assets/Settings/Jam_NightVolume.asset stays as it is.
//  2) Draws a film overlay at ~12 fps: dust and hairs, vertical scratches that hang around for a few frames,
//     and a faint exposure flicker.
// Sits on the Global Volume object. Every value is tweakable in the Inspector while playing.
[RequireComponent(typeof(Volume))]
public class GrittyFilter : MonoBehaviour
{
    [Header("Post-processing push")]
    [Range(0f, 1f)] public float grainIntensity = 1f;
    [Range(0f, 1f)] public float grainResponse = 0.15f;     // lower = grain also shows in bright areas
    public FilmGrainLookup grainType = FilmGrainLookup.Medium6;
    [Range(0f, 1f)] public float vignette = 0.44f;
    [Range(-100f, 100f)] public float contrast = 24f;
    [Range(-100f, 100f)] public float saturation = -42f;
    [Range(0f, 1f)] public float chromaticAberration = 0.22f;

    [Header("Film overlay")]
    public bool overlay = true;
    public float filmFps = 12f;
    [Range(0f, 1f)] public float dustAmount = 0.5f;
    [Range(0f, 1f)] public float scratchAmount = 0.55f;
    [Range(0f, 0.2f)] public float flicker = 0.06f;

    Volume volume;
    FilmGrain grain; Vignette vig; ColorAdjustments color; ChromaticAberration chroma;

    static Texture2D dustTex;
    float nextFrame;
    Rect dustUV;
    float flickerA;
    bool dustFlip;

    struct Scratch { public float x, w, life, drift, alpha; public bool light; }
    readonly Scratch[] scratches = new Scratch[5];

    void Start()
    {
        volume = GetComponent<Volume>();
        var p = volume.profile; // runtime instance; the asset is untouched
        if (!p.TryGet(out grain)) grain = p.Add<FilmGrain>(true);
        if (!p.TryGet(out vig)) vig = p.Add<Vignette>(true);
        if (!p.TryGet(out color)) color = p.Add<ColorAdjustments>(true);
        if (!p.TryGet(out chroma)) chroma = p.Add<ChromaticAberration>(true);
        Apply();
    }

    void OnValidate() { if (Application.isPlaying && grain != null) Apply(); }

    void Apply()
    {
        grain.type.Override(grainType);
        grain.intensity.Override(grainIntensity);
        grain.response.Override(grainResponse);
        vig.intensity.Override(vignette);
        vig.smoothness.Override(0.5f);
        color.contrast.Override(contrast);
        color.saturation.Override(saturation);
        chroma.intensity.Override(chromaticAberration);
    }

    void Update()
    {
        if (!overlay || Time.unscaledTime < nextFrame) return;
        nextFrame = Time.unscaledTime + 1f / Mathf.Max(1f, filmFps);

        // New "frame" of film: dust jumps somewhere else, exposure wobbles, scratches age.
        dustUV = new Rect(Random.value, Random.value, 1.2f, 1.2f * Screen.height / Mathf.Max(1f, Screen.width));
        dustFlip = Random.value < 0.5f;
        flickerA = flicker * (0.4f + 0.6f * Random.value) * (Random.value < 0.08f ? 2.2f : 1f);

        for (int i = 0; i < scratches.Length; i++)
        {
            var s = scratches[i];
            s.life -= 1f;
            s.x += s.drift;
            if (s.life <= 0f && Random.value < 0.06f * scratchAmount * 2f)
            {
                s.x = Random.Range(0.04f, 0.96f);
                s.w = Random.Range(1f, 2.5f);
                s.life = Random.Range(3, 14);
                s.drift = Random.Range(-0.002f, 0.002f);
                s.light = Random.value < 0.55f;
                s.alpha = Random.Range(0.15f, 0.4f);
            }
            scratches[i] = s;
        }
    }

    void OnGUI()
    {
        if (!overlay || Event.current.type != EventType.Repaint) return;
        GUI.depth = 100; // behind the narration / death / win screens
        float sw = Screen.width, sh = Screen.height, k = sh / 1080f;

        // Exposure flicker.
        if (flickerA > 0.001f) HorrorUI.Fill(new Rect(0, 0, sw, sh), new Color(0f, 0f, 0f, flickerA));

        // Dust and hairs.
        if (dustAmount > 0.001f)
        {
            if (dustTex == null) dustTex = MakeDust();
            var old = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, dustAmount);
            var uv = dustUV;
            if (dustFlip) { uv.x += uv.width; uv.width = -uv.width; }
            GUI.DrawTextureWithTexCoords(new Rect(0, 0, sw, sh), dustTex, uv, true);
            GUI.color = old;
        }

        // Scratches.
        for (int i = 0; i < scratches.Length; i++)
        {
            var s = scratches[i];
            if (s.life <= 0f || scratchAmount <= 0.001f) continue;
            float a = s.alpha * scratchAmount * Mathf.Clamp01(s.life / 3f);
            var c = s.light ? new Color(0.9f, 0.88f, 0.8f, a) : new Color(0f, 0f, 0f, a * 1.4f);
            float x = s.x * sw;
            HorrorUI.Fill(new Rect(x, 0, Mathf.Max(1f, s.w * k), sh), c);
            // a broken bit next to it now and then
            if ((i + (int)(Time.unscaledTime * filmFps)) % 3 == 0)
                HorrorUI.Fill(new Rect(x + 3f * k, sh * Mathf.Repeat(s.x * 7.3f, 1f), Mathf.Max(1f, k), sh * 0.25f), HorrorUI.A(c, c.a * 0.6f));
        }
    }

    // Sparse specks (mostly dark, a few light) plus a handful of curly hairs. Tiles.
    static Texture2D MakeDust()
    {
        const int N = 512;
        var tex = new Texture2D(N, N, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat, hideFlags = HideFlags.HideAndDontSave };
        var px = new Color32[N * N];
        var rng = new System.Random(1337);

        void Dot(int cx, int cy, float r, byte v, byte a)
        {
            int ri = Mathf.CeilToInt(r);
            for (int y = -ri; y <= ri; y++)
            for (int x = -ri; x <= ri; x++)
            {
                float d = Mathf.Sqrt(x * x + y * y);
                if (d > r) continue;
                int px_ = (cx + x + N) % N, py_ = (cy + y + N) % N;
                byte aa = (byte)(a * Mathf.Clamp01(1.2f - d / r));
                if (aa > px[py_ * N + px_].a) px[py_ * N + px_] = new Color32(v, v, v, aa);
            }
        }

        for (int i = 0; i < 70; i++)
        {
            bool light = rng.NextDouble() < 0.4;
            float r = (float)(0.6 + rng.NextDouble() * rng.NextDouble() * 3.2);
            Dot(rng.Next(N), rng.Next(N), r, light ? (byte)230 : (byte)8, (byte)(140 + rng.Next(110)));
        }
        for (int h = 0; h < 6; h++)
        {
            float x = rng.Next(N), y = rng.Next(N);
            float ang = (float)(rng.NextDouble() * Mathf.PI * 2);
            int len = 25 + rng.Next(60);
            bool light = rng.NextDouble() < 0.3;
            for (int s = 0; s < len; s++)
            {
                ang += (float)(rng.NextDouble() - 0.5) * 0.5f;
                x += Mathf.Cos(ang); y += Mathf.Sin(ang);
                Dot((int)x, (int)y, 0.8f, light ? (byte)220 : (byte)10, 170);
            }
        }
        tex.SetPixels32(px);
        tex.Apply(false, false);
        return tex;
    }
}
