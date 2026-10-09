using System.Collections.Generic;
using UnityEngine;

// The game's sound files live in Assets/Resources/Audio (made from the project's sound packs + crafted ones).
// GameAudio.Get("Name") loads one; GameAudio.Play2D / PlayAt play one-shots at the Effects volume.
//
//   Crack_Big_1-3, Crack_Small_1-2, Creak_1-3   wood snaps / strain (jump scares, heart break, things behind you)
//   Step_1-8                                     footsteps on dry corn leaves and dirt
//   Flashlight_On / _Off / _Dead                 switch clicks
//   Gasp, Pant                                   out of breath
//   Gate_Open, Claw                              iron gate creak, dirt scrabbling
//   Ambience_Wind, Ambience_Drone, Music_Menu    loops
//   Key_Bell, Game_Over, Win_Choir, Impact_Low, Rumble_Loop, Far_Cry   stingers
public static class GameAudio
{
    static readonly Dictionary<string, AudioClip> cache = new Dictionary<string, AudioClip>();
    static AudioSource src2D;

    public static AudioClip Get(string name)
    {
        if (cache.TryGetValue(name, out var c) && c != null) return c;
        c = Resources.Load<AudioClip>("Audio/" + name);
        if (c == null) Debug.LogWarning("[Audio] missing Resources/Audio/" + name);
        cache[name] = c;
        return c;
    }

    /// <summary>Random one of name_1 .. name_count.</summary>
    public static AudioClip Pick(string name, int count) => Get(name + "_" + Random.Range(1, count + 1));

    public static AudioClip CrackBig() => Pick("Crack_Big", 3);
    public static AudioClip CrackSmall() => Pick("Crack_Small", 2);
    public static AudioClip Creak() => Pick("Creak", 3);

    /// <summary>Non-positional one-shot (scaled by the Effects volume).</summary>
    public static void Play2D(AudioClip clip, float volume = 1f)
    {
        if (clip == null) return;
        if (src2D == null)
        {
            var go = new GameObject("GameAudio2D");
            src2D = go.AddComponent<AudioSource>();
            src2D.playOnAwake = false;
            src2D.spatialBlend = 0f;
        }
        src2D.PlayOneShot(clip, volume * GameSettings.Fx);
    }

    /// <summary>Positional one-shot in the world (e.g. a twig snapping behind you).</summary>
    public static void PlayAt(AudioClip clip, Vector3 position, float volume = 1f, float pitch = 1f, float minDistance = 1.5f, float maxDistance = 25f)
    {
        if (clip == null) return;
        var go = new GameObject("Sound_" + clip.name);
        go.transform.position = position;
        var s = go.AddComponent<AudioSource>();
        s.clip = clip;
        s.spatialBlend = 1f;
        s.rolloffMode = AudioRolloffMode.Logarithmic;
        s.minDistance = minDistance;
        s.maxDistance = maxDistance;
        s.dopplerLevel = 0f;
        s.pitch = pitch;
        s.volume = volume * GameSettings.Fx;
        s.Play();
        Object.Destroy(go, clip.length / Mathf.Max(0.1f, pitch) + 0.2f);
    }
}
