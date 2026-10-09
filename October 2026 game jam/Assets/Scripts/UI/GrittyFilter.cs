using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Pushes the night camera filter harder at runtime: heavier film grain (also in bright areas), darker edges,
// more contrast, less colour and a little lens fringing. Nothing is drawn on top of the screen.
// Also applies two player settings: BRIGHTNESS (exposure) and SCREEN EFFECTS (how strong the grain, fringing
// and dark edges are). Only a runtime copy of the Global Volume profile is changed; the asset stays as it is.
// Sits on the Global Volume object. Every value is tweakable in the Inspector while playing.
[RequireComponent(typeof(Volume))]
public class GrittyFilter : MonoBehaviour
{
    [Range(0f, 1f)] public float grainIntensity = 1f;
    [Range(0f, 1f)] public float grainResponse = 0.15f;     // lower = grain also shows in bright areas
    public FilmGrainLookup grainType = FilmGrainLookup.Medium6;
    [Range(0f, 1f)] public float vignette = 0.44f;
    [Range(-100f, 100f)] public float contrast = 24f;
    [Range(-100f, 100f)] public float saturation = -42f;
    [Range(0f, 1f)] public float chromaticAberration = 0.22f;

    FilmGrain grain; Vignette vig; ColorAdjustments color; ChromaticAberration chroma;
    float baseExposure;

    void Start()
    {
        var p = GetComponent<Volume>().profile; // runtime instance; the asset is untouched
        if (!p.TryGet(out grain)) grain = p.Add<FilmGrain>(true);
        if (!p.TryGet(out vig)) vig = p.Add<Vignette>(true);
        if (!p.TryGet(out color)) color = p.Add<ColorAdjustments>(true);
        if (!p.TryGet(out chroma)) chroma = p.Add<ChromaticAberration>(true);
        baseExposure = color.postExposure.overrideState ? color.postExposure.value : 0f;
        GameSettings.Changed += Apply;
        Apply();
    }

    void OnDestroy() => GameSettings.Changed -= Apply;

    void OnValidate() { if (Application.isPlaying && grain != null) Apply(); }

    void Apply()
    {
        if (grain == null) return;
        float fx = Mathf.Clamp01(GameSettings.ScreenEffects);
        grain.type.Override(grainType);
        grain.intensity.Override(grainIntensity * fx);
        grain.response.Override(grainResponse);
        vig.intensity.Override(Mathf.Lerp(vignette * 0.55f, vignette, fx));
        vig.smoothness.Override(0.5f);
        color.contrast.Override(contrast);
        color.saturation.Override(saturation);
        color.postExposure.Override(baseExposure + GameSettings.Brightness);
        chroma.intensity.Override(chromaticAberration * fx);
    }
}
