using System.Collections;
using UnityEngine;

// The ending. Touching the coffin doesn't win straight away:
//   1. Control is taken away and the view drifts onto the coffin. Every other pumpkin stops.
//   2. The candles go out one by one, then the cold light over the coffin gutters and dies. Only your flashlight is left.
//   3. A low rumble builds, the coffin shudders, the view trembles, the flashlight starts to flicker.
//   4. Dead silence. The view sinks to the earth in front of the grave.
//   5. A hand claws up out of the soil... then the pumpkin lurches out of the ground and slowly drags itself
//      up to eye level, reaching for your face, its carved face starting to glow.
//   6. It holds there, staring, trembling... then the final jump scare, then black, then YOU ESCAPED.
// Created by ExitTrail when the coffin is touched (GraveFinale.Begin()). No scene setup.
// Sounds are placeholders: drop real clips into the slots on a GraveFinale component in the scene to replace them.
public class GraveFinale : MonoBehaviour
{
    public static bool IsPlaying { get; private set; }

    [Header("Timing (seconds)")]
    public float turnTime = 1.6f;          // view drifts onto the coffin
    public float candleGap = 0.3f;         // between candles going out
    public float rumbleTime = 2.2f;        // shudder build-up
    public float silenceTime = 0.9f;       // the fake-out
    public float clawTime = 1.1f;          // hand out of the soil
    public float riseTime = 2.6f;          // pumpkin drags itself up
    public float stareTime = 0.9f;         // holds, trembling, before the scare

    [Header("Scare")]
    [Tooltip("Grab is the ending's own scare (it never happens in the maze). Any other variant works too.")]
    public JumpScare.Variant finalScare = JumpScare.Variant.Grab;
    [Tooltip("How far out from the edge of the stone slab (on the dirt, toward you) it comes up.")]
    public float riseFromSlab = 0.85f;

    [Header("Sounds (empty = placeholder)")]
    public AudioClip snuffClip;
    public AudioClip rumbleClip;
    public AudioClip clawClip;
    public AudioClip riseClip;
    public AudioClip heartbeatClip;

    Camera cam;
    Vector3 camPos;
    Quaternion camRot;
    float escapeTime;
    PumpkinMonster grave;
    Transform coffin;
    Vector3 coffinBasePos; Quaternion coffinBaseRot;
    Vector3 lookTarget;
    float lookSharpness = 3f;
    float shake;
    AudioSource rumbleSrc, oneShots;
    Light faceGlow;

    public static void Begin()
    {
        if (IsPlaying) return;
        var existing = FindFirstObjectByType<GraveFinale>();
        var f = existing != null ? existing : new GameObject("GraveFinale").AddComponent<GraveFinale>();
        f.StartCoroutine(f.Run());
    }

    void OnDestroy() => IsPlaying = false;

    IEnumerator Run()
    {
        IsPlaying = true;
        Debug.Log("[Finale] start");
        if (rumbleClip == null) rumbleClip = GameAudio.Get("Rumble_Loop");
        if (clawClip == null) clawClip = GameAudio.Get("Claw");
        if (riseClip == null) riseClip = GameAudio.Get("Dirt_Drag");
        escapeTime = Time.timeSinceLevelLoad;
        cam = Camera.main;
        var trail = ExitTrail.Instance;
        var pc = PlayerController.Instance;
        // The pumpkin prefab: the trail's own slot first, then the spawner's (found even if Monsters is switched off).
        GameObject prefab = trail != null ? trail.gravePumpkinPrefab : null;
        if (prefab == null)
        {
            var spawner = PumpkinSpawner.Instance != null ? PumpkinSpawner.Instance
                        : FindFirstObjectByType<PumpkinSpawner>(FindObjectsInactive.Include);
            if (spawner != null) prefab = spawner.monsterPrefab;
        }
        if (cam == null || trail == null || prefab == null)
        {
            Debug.LogWarning($"[Finale] skipping to the win screen (camera {(cam != null)}, trail {(trail != null)}, pumpkin prefab {(prefab != null)})");
            Finish();
            yield break;
        }

        // ---- Take control away ----
        if (pc != null) pc.enabled = false;
        var hud = FindFirstObjectByType<FlashlightHUD>();
        if (hud != null) hud.gameObject.SetActive(false);
        foreach (var m in PumpkinMonster.All.ToArray())
        {
            if (m == null) continue;
            m.enabled = false;
            var a = m.GetComponentInChildren<Animator>();
            if (a != null) a.speed = 0f;
        }
        var fl = Flashlight.Instance;
        if (fl != null)
        {
            fl.AddBattery(fl.maxBattery);
            if (!fl.IsOn) fl.Toggle();
        }

        camPos = cam.transform.position;
        camRot = cam.transform.rotation;
        var cb = trail.CoffinBounds;
        var ab = trail.AltarBounds;
        coffin = trail.Coffin;
        if (coffin != null) { coffinBasePos = coffin.position; coffinBaseRot = coffin.rotation; }
        trail.LightsOverridden = true;

        oneShots = gameObject.AddComponent<AudioSource>();
        oneShots.spatialBlend = 0f; oneShots.playOnAwake = false;
        rumbleSrc = gameObject.AddComponent<AudioSource>();
        rumbleSrc.spatialBlend = 0f; rumbleSrc.playOnAwake = false; rumbleSrc.loop = true;
        rumbleSrc.clip = rumbleClip != null ? rumbleClip : MakeRumble();
        rumbleSrc.volume = 0f;
        rumbleSrc.Play();

        // Where it will come up: between you and the coffin.
        Vector3 feet = pc != null ? pc.transform.position : new Vector3(camPos.x, cb.min.y, camPos.z);
        Vector3 toCoffin = cb.center - feet; toCoffin.y = 0f;
        toCoffin = toCoffin.sqrMagnitude > 0.0001f ? toCoffin.normalized : cam.transform.forward;
        float groundY = feet.y;
        // On the dirt just outside the slab, on your side, but never right on top of you.
        Vector3 edge = ab.ClosestPoint(new Vector3(feet.x, ab.center.y, feet.z)); edge.y = groundY;
        Vector3 risePoint = edge - toCoffin * riseFromSlab;
        Vector3 fromFeet = risePoint - feet; fromFeet.y = 0f;
        if (Vector3.Dot(fromFeet, toCoffin) < 1.1f) risePoint = feet + toCoffin * 1.1f;
        risePoint.y = groundY;

        // ---- 1. Drift onto the coffin ----
        lookTarget = cb.center + Vector3.up * 0.25f;
        lookSharpness = 1.8f;
        yield return Wait(turnTime);

        // ---- 2. A gust: every flame flares up... then they go out one by one, the far ones first,
        //         the dark creeping toward you. Each one gutters, dies, and leaves a dull ember on the wick. ----
        var candles = new System.Collections.Generic.List<ExitTrail.Candle>(trail.Candles);
        candles.Sort((a, b) => (b.wick - feet).sqrMagnitude.CompareTo((a.wick - feet).sqrMagnitude));
        float ft = 0f;
        while (ft < 0.5f)
        {
            ft += Time.deltaTime;
            float k = 1f + 0.9f * Mathf.Sin(Mathf.Clamp01(ft / 0.5f) * Mathf.PI);
            for (int i = 0; i < candles.Count; i++) candles[i].SetFlame(k * (0.8f + 0.4f * Mathf.PerlinNoise(ft * 14f, i * 2.1f)));
            yield return null;
        }
        for (int c = 0; c < candles.Count; c++)
        {
            var cd = candles[c];
            // the ones still burning tremble while this one gutters
            float gt = 0f, gutter = 0.22f;
            while (gt < gutter)
            {
                gt += Time.deltaTime;
                cd.SetFlame(Mathf.PerlinNoise(gt * 40f, c) > 0.45f ? 1.4f * (1f - gt / gutter) : 0.15f);
                for (int i = c + 1; i < candles.Count; i++) candles[i].SetFlame(0.7f + 0.3f * Mathf.PerlinNoise(Time.time * 9f, i * 3.1f));
                yield return null;
            }
            cd.SetFlame(0f);
            cd.lit = false;
            StartCoroutine(Ember(cd.wick));
            Play(snuffClip, MakeSnuff, 0.8f);
            float wt = 0f;
            while (wt < candleGap)
            {
                wt += Time.deltaTime;
                for (int i = c + 1; i < candles.Count; i++) candles[i].SetFlame(0.7f + 0.3f * Mathf.PerlinNoise(Time.time * 9f, i * 3.1f));
                yield return null;
            }
        }
        var cl = trail.CoffinLight;
        if (cl != null)
        {
            float gt = 0f, baseI = cl.intensity;
            while (gt < 0.7f)
            {
                gt += Time.deltaTime;
                cl.intensity = baseI * (Mathf.PerlinNoise(gt * 25f, 1.3f) > 0.5f ? 1f : 0.1f) * (1f - gt / 0.7f);
                yield return null;
            }
            cl.enabled = false;
            Play(snuffClip, MakeSnuff, 0.6f);
        }
        yield return Wait(0.5f);

        // ---- 3. The ground rumbles, the coffin shudders ----
        float rt = 0f;
        while (rt < rumbleTime)
        {
            rt += Time.deltaTime;
            float u = rt / rumbleTime;
            rumbleSrc.volume = Mathf.Lerp(0.1f, 1f, u * u) * GameSettings.Fx;
            shake = 0.15f + 1.1f * u * u;
            if (coffin != null)
            {
                float j = 0.012f + 0.03f * u;
                coffin.SetPositionAndRotation(
                    coffinBasePos + new Vector3(Random.Range(-j, j), Random.Range(0f, j), Random.Range(-j, j)),
                    coffinBaseRot * Quaternion.Euler(Random.Range(-1f, 1f) * 1.5f * u, 0f, Random.Range(-1f, 1f) * 1.5f * u));
            }
            if (fl != null && u > 0.6f) fl.ForceFlicker(0.2f);
            yield return null;
        }

        // ---- 4. Silence ----
        rumbleSrc.Stop();
        shake = 0f;
        if (coffin != null) coffin.SetPositionAndRotation(coffinBasePos, coffinBaseRot);
        lookTarget = risePoint + Vector3.up * 0.1f; // the view sinks to the earth in front of the grave
        lookSharpness = 1.2f;
        yield return Wait(silenceTime);

        // ---- 5. It comes up ----
        var go = Instantiate(prefab, risePoint + Vector3.down * 5f, Quaternion.LookRotation(-toCoffin));
        go.name = "GravePumpkin";
        grave = go.GetComponent<PumpkinMonster>();
        if (grave == null) { Finish(); yield break; }
        grave.BeginJumpScare(); // creepy pose, AI off, head locked on you
        yield return null;      // let the animator pose it once so the head can be measured
        yield return null;

        float s = go.transform.lossyScale.y;
        var head = grave.Head;
        float headH = head != null ? head.position.y - go.transform.position.y : 1.7f * s;
        float headR = 0.32f * s;
        float startY = groundY - headH - headR - 0.15f;      // whole head under the soil
        float endY = (camPos.y - 0.08f) - headH;             // face level with yours
        Vector3 basePos = new Vector3(risePoint.x, startY, risePoint.z);
        go.transform.position = basePos;
        Vector3 side = Vector3.Cross(Vector3.up, toCoffin).normalized; // monster's right is -side
        bool right = true;
        Vector3 soil = risePoint - toCoffin * 0.35f + side * -0.25f; // where the hand breaks the surface
        Vector3 soil2 = risePoint - toCoffin * 0.3f + side * 0.3f;   // ...and where the second one follows it

        faceGlow = new GameObject("GraveFaceGlow").AddComponent<Light>();
        faceGlow.type = LightType.Point;
        faceGlow.color = new Color(1f, 0.45f, 0.1f);
        faceGlow.range = 1.6f;
        faceGlow.intensity = 0f;
        faceGlow.shadows = LightShadows.None;

        // 5a. A hand claws up out of the soil.
        Play(clawClip, MakeClaw, 1f);
        lookTarget = soil + Vector3.up * 0.2f;
        lookSharpness = 4f;
        go.transform.position = new Vector3(basePos.x, startY + headR * 0.6f, basePos.z);
        float ct = 0f;
        while (ct < clawTime)
        {
            ct += Time.deltaTime;
            float u = ct / clawTime;
            Vector3 hand = soil + Vector3.up * (0.05f + 0.4f * HorrorUI.Smooth(u)) + Twitch(ct, 0.03f);
            grave.SetScareHand(right, hand, hand + Vector3.down * 0.5f + side * -0.3f, 1f);
            // Halfway through, the other hand tears up beside it and claws at the dirt.
            float u2 = Mathf.InverseLerp(0.45f, 1f, u);
            Vector3 hand2 = soil2 + Vector3.up * (0.03f + 0.3f * HorrorUI.Smooth(u2)) + Twitch(ct + 4.7f, 0.035f);
            grave.SetScareOtherHand(hand2, hand2 + Vector3.down * 0.5f + side * 0.3f, u2 > 0f ? 1f : 0f);
            if (u > 0.5f && fl != null) fl.ForceFlicker(0.1f);
            yield return null;
        }

        // 5b. Lurch: the head bursts out, then it drags itself up in jerky, stop-motion steps, reaching for you.
        GameAudio.Play2D(GameAudio.Get("Dirt_Burst"), 1f);      // the ground splits as its head breaks out
        StartCoroutine(PlayAfter(riseClip, 0.25f, 0.9f));         // then the soil pours off it as it hauls itself up
        shake = 0.6f;
        Vector3 reach = camPos + (risePoint - camPos).normalized * 0.5f + Vector3.down * 0.12f + side * -0.12f;
        Vector3 reach2 = camPos + (risePoint - camPos).normalized * 0.6f + Vector3.down * 0.22f + side * 0.2f;
        float t5 = 0f;
        while (t5 < riseTime)
        {
            t5 += Time.deltaTime;
            float p;
            if (t5 < 0.22f) p = 0.38f * HorrorUI.Smooth(t5 / 0.22f);
            else if (t5 < 0.75f) p = 0.38f;
            else
            {
                float q = Mathf.Clamp01((t5 - 0.75f) / (riseTime - 0.75f));
                float steps = 8f, f = q * steps, k = Mathf.Floor(f);
                p = 0.38f + 0.62f * Mathf.Min(1f, (k + HorrorUI.Smooth(Mathf.Clamp01((f - k) * 3f))) / steps);
            }
            shake = t5 < 0.4f ? 0.7f : 0.12f;
            go.transform.position = new Vector3(basePos.x, Mathf.Lerp(startY, endY, p), basePos.z) + Twitch(t5, 0.015f);
            Vector3 hand = Vector3.Lerp(soil + Vector3.up * 0.45f, reach, HorrorUI.Smooth(Mathf.InverseLerp(0.3f, 1f, p))) + Twitch(t5 * 1.3f, 0.025f);
            grave.SetScareHand(right, hand, hand + Vector3.down * 0.4f + side * -0.35f, 1f);
            // The other hand stays planted in the dirt, hauling the body up, then lets go and reaches for you too.
            Vector3 planted = soil2 + Vector3.up * 0.06f;
            Vector3 hand2 = Vector3.Lerp(planted, reach2, HorrorUI.Smooth(Mathf.InverseLerp(0.55f, 1f, p))) + Twitch(t5 * 1.1f + 4.7f, 0.025f);
            grave.SetScareOtherHand(hand2, hand2 + Vector3.down * 0.4f + side * 0.35f, 1f);
            if (head != null)
            {
                lookTarget = head.position;
                faceGlow.transform.position = head.position + (camPos - head.position).normalized * 0.35f * s;
            }
            faceGlow.intensity = Mathf.Lerp(0f, 1.3f, p) * (0.8f + 0.2f * Mathf.PerlinNoise(t5 * 8f, 0f));
            lookSharpness = 6f;
            yield return null;
        }

        // ---- 6. It stares... ----
        shake = 0f;
        Play(heartbeatClip, MakeHeartbeat, 1f);
        float st = 0f;
        Vector3 hold = go.transform.position;
        while (st < stareTime)
        {
            st += Time.deltaTime;
            go.transform.position = hold + Twitch(st * 2f, 0.008f);
            grave.SetScareHand(right, reach + Twitch(st * 3f, 0.02f), reach + Vector3.down * 0.4f + side * -0.35f, 1f);
            grave.SetScareOtherHand(reach2 + Twitch(st * 2.6f + 4.7f, 0.02f), reach2 + Vector3.down * 0.4f + side * 0.35f, 1f);
            if (head != null) lookTarget = head.position;
            yield return null;
        }

        // ---- ...then it gets you. ----
        if (faceGlow != null) Destroy(faceGlow.gameObject);
        cam.transform.rotation = Quaternion.LookRotation((head != null ? head.position : risePoint + Vector3.up) - cam.transform.position);
        driving = false;
        Debug.Log("[Finale] final scare");
        if (!JumpScare.Trigger(grave, finalScare, () => Finish(true))) Finish();
    }

    bool driving = true;

    IEnumerator PlayAfter(AudioClip clip, float delay, float volume)
    {
        yield return new WaitForSeconds(delay);
        if (clip != null && oneShots != null) oneShots.PlayOneShot(clip, volume * GameSettings.Fx);
    }

    // A snuffed wick: a dull red ember that fades out.
    IEnumerator Ember(Vector3 at)
    {
        var l = new GameObject("Ember").AddComponent<Light>();
        l.transform.position = at + Vector3.up * 0.03f;
        l.type = LightType.Point;
        l.color = new Color(1f, 0.25f, 0.05f);
        l.range = 0.5f;
        l.shadows = LightShadows.None;
        float t = 0f;
        while (t < 1.4f && l != null)
        {
            t += Time.deltaTime;
            l.intensity = 0.35f * (1f - t / 1.4f) * (0.7f + 0.3f * Mathf.PerlinNoise(t * 12f, at.x));
            yield return null;
        }
        if (l != null) Destroy(l.gameObject);
    }

    // Camera: smooth look at the current target plus a little shake. Runs after everything else moved.
    void LateUpdate()
    {
        if (!IsPlaying || !driving || cam == null) return;
        Vector3 dir = lookTarget - cam.transform.position;
        if (dir.sqrMagnitude > 0.0001f)
        {
            var target = Quaternion.LookRotation(dir);
            camRot = Quaternion.Slerp(camRot, target, 1f - Mathf.Exp(-lookSharpness * Time.deltaTime));
        }
        float t = Time.time;
        var sh = Quaternion.Euler((Mathf.PerlinNoise(t * 22f, 0.5f) - 0.5f) * 2f * shake,
                                  (Mathf.PerlinNoise(1.7f, t * 22f) - 0.5f) * 2f * shake, 0f);
        cam.transform.SetPositionAndRotation(camPos, camRot * sh);
    }

    void Finish() => Finish(false);

    // fromBlack: called by the jump scare once the screen has cut to black.
    void Finish(bool fromBlack)
    {
        if (rumbleSrc != null) rumbleSrc.Stop();
        driving = false;
        WinScreen.Show(fromBlack, escapeTime);
        IsPlaying = false;
    }

    static Vector3 Twitch(float t, float amount) =>
        new Vector3(Mathf.PerlinNoise(t * 9f, 0.1f) - 0.5f, Mathf.PerlinNoise(0.7f, t * 9f) - 0.5f, Mathf.PerlinNoise(t * 9f, 3.3f) - 0.5f) * 2f * amount;

    static IEnumerator Wait(float s) { yield return new WaitForSeconds(s); }

    void Play(AudioClip clip, System.Func<AudioClip> fallback, float vol)
    {
        if (oneShots == null) return;
        var c = clip != null ? clip : fallback();
        if (c != null) oneShots.PlayOneShot(c, vol * GameSettings.Fx);
    }

    // ---------------- Placeholder sounds (replace with real clips) ----------------

    static AudioClip Synth(string name, float seconds, System.Func<float, System.Random, float> f)
    {
        const int sr = 44100;
        int n = Mathf.RoundToInt(sr * seconds);
        var data = new float[n];
        var rng = new System.Random(name.GetHashCode());
        for (int i = 0; i < n; i++) data[i] = Mathf.Clamp(f(i / (float)sr, rng), -1f, 1f);
        var clip = AudioClip.Create(name, n, 1, sr, false);
        clip.SetData(data, 0);
        return clip;
    }

    static AudioClip MakeSnuff() => Synth("Snuff", 0.45f, (t, r) =>
        (float)(r.NextDouble() * 2 - 1) * 0.35f * Mathf.Exp(-t * 9f) * Mathf.Clamp01(t * 60f));

    static AudioClip MakeRumble()
    {
        float lp = 0f;
        return Synth("Rumble", 4f, (t, r) =>
        {
            lp += ((float)(r.NextDouble() * 2 - 1) - lp) * 0.02f; // heavy low-pass noise
            float throb = 0.6f + 0.4f * Mathf.Sin(2f * Mathf.PI * 0.75f * t);
            return (lp * 5f + 0.35f * Mathf.Sin(2f * Mathf.PI * 38f * t)) * throb * 0.6f;
        });
    }

    static AudioClip MakeClaw() => Synth("Claw", 1.1f, (t, r) =>
    {
        float scrape = Mathf.Abs(Mathf.Sin(t * 23f)) > 0.7f ? 1f : 0.25f;
        return (float)(r.NextDouble() * 2 - 1) * 0.45f * scrape * Mathf.Clamp01(t * 8f) * Mathf.Clamp01((1.1f - t) * 4f);
    });

    static AudioClip MakeGroan() => Synth("Groan", 2.6f, (t, r) =>
    {
        float f = 62f + 8f * Mathf.Sin(t * 5.5f) - 10f * t;
        float saw = 2f * Mathf.Repeat(f * t, 1f) - 1f;
        float env = Mathf.Clamp01(t * 3f) * Mathf.Clamp01((2.6f - t) * 1.5f);
        float crunch = t < 0.3f ? (float)(r.NextDouble() * 2 - 1) * (0.3f - t) * 2.5f : 0f;
        return saw * 0.32f * env + crunch;
    });

    static AudioClip MakeHeartbeat() => Synth("Heartbeat", 1.2f, (t, r) =>
    {
        float b1 = Mathf.Exp(-Mathf.Pow((t - 0.08f) / 0.035f, 2f)), b2 = Mathf.Exp(-Mathf.Pow((t - 0.32f) / 0.035f, 2f));
        float b3 = Mathf.Exp(-Mathf.Pow((t - 0.78f) / 0.035f, 2f)), b4 = Mathf.Exp(-Mathf.Pow((t - 1.0f) / 0.035f, 2f));
        return Mathf.Sin(2f * Mathf.PI * 52f * t) * (b1 + 0.7f * b2 + b3 + 0.7f * b4) * 0.9f;
    });
}
