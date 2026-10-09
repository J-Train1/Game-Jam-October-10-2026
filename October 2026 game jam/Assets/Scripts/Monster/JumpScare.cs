using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Random = UnityEngine.Random;

// Jump scare when a pumpkin catches the player. One of three, picked at random (never the same twice in a row):
//
//  BITE:  face slams in, dead center, vibrating with rage, biting ~3x a second while creeping closer.
//         Head tilted to one side for 2 bites, then snaps to the other side for the rest.
//  STAB:  face holds back just enough to see the arm. It draws a claw back beside its head and STABS it into
//         the lens: camera knocked back, red floods in. Rips it out, stabs again and leaves it buried while
//         your view slumps and it stares at you.
//  SPIN:  it stares... then its head slowly turns around on its neck (scream drops to creaking, cracking bone),
//         all the way around until the back of the pumpkin faces you, SNAPS back to face you, then ratchets
//         upside down in three cracking jerks and stares at you inverted.
//
// All three share: instant hit with a white flash, wide FOV + fisheye bulge, glowing carved face, chromatic
// aberration, red vignette, crushed contrast, TV static, then a final lunge into the lens, a static flood,
// and an instant cut to black and silence. Then it hands over to DeathScreen (lose a heart / game over) and
// puts everything it changed back (camera, lights, effects, the pumpkin's glow) so the run can continue.
// Created on demand by PumpkinMonster.Catch (no scene setup). To tune, test one variant, or assign a real scream
// clip, add a JumpScare component to any object in the scene; that one is used instead.
public class JumpScare : MonoBehaviour
{
    public enum Variant { Random, Bite, Stab, Spin, Grab } // Grab is the ending's own scare; Random never picks it

    [Header("Variant")]
    [Tooltip("Random picks one of the three each time. Set one to test it.")]
    public Variant forceVariant = Variant.Random;
    [Tooltip("When random, never play the same one twice in a row.")]
    public bool avoidRepeat = true;

    [Header("Timing (seconds)")]
    public float snapTime = 0.05f;
    public float slamTime = 0.1f;
    public float biteHoldTime = 1.6f;
    public float stabHoldTime = 1.55f;
    public float spinHoldTime = 1.75f;
    public float grabHoldTime = 2.3f;   // grab -> lift -> throw -> chomp
    [Tooltip("The final lunge + full-screen static flood before the cut to black.")]
    public float finalLungeTime = 0.08f;
    [Tooltip("Silence on black before the hearts appear.")]
    public float blackTime = 0.6f;

    [Header("Framing")]
    [Tooltip("Face appears this close on frame one...")]
    public float appearDistance = 0.65f;
    [Tooltip("...and slams to this distance (Bite).")]
    public float slamDistance = 0.32f;
    [Tooltip("Bite: it creeps closer to this during the attack.")]
    public float creepDistance = 0.22f;
    [Tooltip("The final lunge ends here (into the lens).")]
    public float finalDistance = 0.04f;
    [Tooltip("Face point relative to the head bone (meters, before monster scale): up, forward.")]
    public Vector2 faceOffset = new Vector2(0.08f, 0.08f);
    [Tooltip("Camera looks slightly up into the face (degrees).")]
    public float lookUp = 6f;
    [Tooltip("Wide FOV + lens bulge makes a close face loom.")]
    public float fovWide = 82f;
    [Tooltip("Chin down a touch (degrees) so it stares from under its brow.")]
    public float headChinDown = 4f;

    [Header("Rage vibration (fast, tight; face stays on you)")]
    public float vibrateFrequency = 17f;
    public float vibrateHeadAngle = 2.5f;
    public float vibrateBodyAngle = 2.5f;

    [Header("Bite")]
    public float biteRate = 3f;
    [Tooltip("How far each bite snaps toward the camera (meters).")]
    public float biteDistance = 0.09f;
    [Tooltip("Head jerks further toward the current side on each bite (degrees).")]
    public float biteHeadSnap = 3f;
    [Tooltip("Head tilt toward the current side (degrees).")]
    public float sideTilt = 14f;
    [Tooltip("Head turn toward the current side (degrees).")]
    public float sideTurn = 6f;
    [Tooltip("Bites on the first side before it snaps over to the other side for the rest.")]
    public int switchAfterBites = 2;
    [Tooltip("How fast the head snaps to the other side (higher = snappier).")]
    public float switchSnap = 30f;
    public float bodyLean = 6f;
    public float chestLean = 9f;
    [Tooltip("Camera jolt on each bite (meters).")]
    public float biteJolt = 0.03f;

    [Header("Stab")]
    [Tooltip("Face holds this far back so you can see the arm come in.")]
    public float stabFaceDistance = 0.55f;
    [Tooltip("Seconds into the attack when each stab hits the lens. The last one stays buried.")]
    public float[] stabHits = { 0.3f, 1.05f };
    [Tooltip("Arm drawn back beside the head before each stab.")]
    public float stabWindup = 0.25f;
    [Tooltip("How long the stab takes to cross to the lens (short = violent).")]
    public float stabStrike = 0.07f;
    [Tooltip("How long the hand stays in before being ripped out (not the last stab).")]
    public float stabStuck = 0.28f;
    public float stabRetract = 0.15f;
    [Tooltip("Camera knocked back on impact (meters).")]
    public float stabKnockback = 0.06f;
    [Tooltip("Camera snaps back on impact (degrees).")]
    public float stabCamKick = 7f;
    [Tooltip("After the last stab your view slumps sideways (degrees).")]
    public float stabSlump = 14f;

    [Header("Spin")]
    public float spinFaceDistance = 0.4f;
    [Tooltip("Seconds into the attack when the head starts turning.")]
    public float spinStart = 0.3f;
    [Tooltip("When the slow turn ends (back of the head facing you).")]
    public float spinEnd = 1.05f;
    [Tooltip("Degrees turned slowly (540 = all the way round and then facing away).")]
    public float spinDegrees = 540f;
    [Tooltip("How fast it snaps the rest of the way back to face you.")]
    public float snapBackTime = 0.12f;
    [Tooltip("When it starts ratcheting upside down.")]
    public float rollStart = 1.25f;
    public int rollSteps = 3;
    public float rollStepTime = 0.08f;
    public float rollDegrees = 180f;

    [Header("Grab (the ending only)")]
    [Tooltip("Where its face is when it grabs you.")]
    public float grabFaceDistance = 0.5f;
    [Tooltip("Seconds into the attack: shaking stops / you're up over its head / it throws / you're in its mouth.")]
    public float grabShakeEnd = 0.45f, grabLiftEnd = 1.0f, grabThrowStart = 1.5f, grabThrowEnd = 1.85f;
    [Tooltip("How fast the jaws slam shut once you're in.")]
    public float grabJawTime = 0.16f;
    [Tooltip("How high above its face it holds you (meters).")]
    public float grabLiftHeight = 0.85f;
    [Tooltip("How far you tumble while flying into its mouth (degrees).")]
    public float grabTumble = 200f;
    public Color jawFlesh = new Color(0.22f, 0.07f, 0.02f);
    public Color jawGlow = new Color(1f, 0.55f, 0.12f);

    [Header("Glow (carved face lights up)")]
    public Color glowColor = new Color(1f, 0.42f, 0.08f);
    public float glowIntensity = 8f;
    public float innerLightIntensity = 2.5f;

    [Header("Face light (from below)")]
    public Color lightColor = new Color(1f, 0.55f, 0.25f);
    public float lightIntensity = 3f;
    public float lightRange = 2.5f;

    [Header("Screen effects")]
    [Tooltip("Negative bulges the center like a fisheye. If it pinches instead, flip the sign.")]
    public float lensBulge = -0.45f;
    [Range(0f, 1f)] public float chromatic = 0.8f;
    [Range(0f, 1f)] public float vignette = 0.5f;
    public Color vignetteColor = new Color(0.35f, 0f, 0f);
    public float contrast = 45f;
    [Tooltip("TV static during the attack (it floods the screen on the final lunge).")]
    [Range(0f, 1f)] public float staticDuringAttack = 0.12f;

    [Header("Audio")]
    [Tooltip("Optional: a real scream recording hits harder than the built-in synth. Leave empty for the synth.")]
    public AudioClip scareClip;
    [Range(0f, 1f)] public float volume = 1f;

    static Variant lastVariant = Variant.Random;

    Variant v;
    PumpkinMonster monster;
    Transform chest;
    Camera cam;
    Light faceLight, innerLight;
    AudioSource audioSrc;
    Volume fxVolume;
    VolumeProfile fxProfile;
    LensDistortion lens;
    ChromaticAberration chroma;
    Vignette vig;
    ColorAdjustments color;
    FilmGrain grain;
    readonly List<Material> glowMats = new List<Material>();
    readonly List<Color> glowOriginal = new List<Color>();
    readonly List<bool> glowKeyword = new List<bool>();
    Texture2D staticTex;
    Color32[] staticPixels;

    float startTime;
    Vector3 camPos, camLocalPos;
    Quaternion camStartRot, camTargetRot, monsterBaseRot, camLocalRot;
    float baseFov, baseNear, side, sideVal, rollVal;
    bool stabRight;
    bool running, blacked, handedOff, haveLocalFace;
    Vector3 localFace;
    float flashAlpha, redAlpha, staticAlpha, blackAlpha, bloodAlpha, jaw, orangeAlpha;
    static Texture2D jawTex;
    bool grabPlaced; Vector3 grabFace0; Quaternion grabLastLook;
    float hand2W; Vector3 hand2Pos, hand2Hint;

    // Per-frame outputs of the active variant.
    float oD, oPulse, oBodyLean, oChestLean, oRoll, oYaw, oChin, oSpinYaw, oRed;
    Vector3 oCamPos, oCamEuler;

    /// <summary>Start a jump scare with this monster. Returns false if it can't run (no camera).</summary>
    public static bool Trigger(PumpkinMonster m) => Trigger(m, Variant.Random, null);

    /// <summary>
    /// Start a jump scare with a chosen variant (Random = the usual pick). If onBlack is given it is called when
    /// the scare cuts to black INSTEAD of the heart-loss DeathScreen (used by the ending at the coffin).
    /// </summary>
    public static bool Trigger(PumpkinMonster m, Variant variant, System.Action onBlack)
    {
        if (m == null || Camera.main == null) return false;
        var js = FindFirstObjectByType<JumpScare>();
        if (js == null) js = new GameObject("JumpScare").AddComponent<JumpScare>();
        js.requestedVariant = variant;
        js.endOverride = onBlack;
        js.Begin(m);
        return true;
    }

    Variant requestedVariant = Variant.Random;
    System.Action endOverride;

    float HoldTime => v == Variant.Stab ? stabHoldTime : v == Variant.Spin ? spinHoldTime : v == Variant.Grab ? grabHoldTime : biteHoldTime;
    float HoldStart => slamTime;
    float HoldEnd => slamTime + HoldTime;
    float FinalEnd => HoldEnd + finalLungeTime;
    float SlamTarget => v == Variant.Stab ? stabFaceDistance : v == Variant.Spin ? spinFaceDistance : v == Variant.Grab ? grabFaceDistance : slamDistance;

    Variant Pick()
    {
        if (forceVariant != Variant.Random) return forceVariant;
        var options = new List<Variant> { Variant.Bite, Variant.Stab, Variant.Spin };
        if (avoidRepeat && options.Count > 1) options.Remove(lastVariant);
        return options[Random.Range(0, options.Count)];
    }

    void Begin(PumpkinMonster m)
    {
        if (running) return;
        running = true;
        blacked = false; handedOff = false; haveLocalFace = false;
        sideVal = 0f; rollVal = 0f; bloodAlpha = 0f; jaw = 0f; orangeAlpha = 0f; grabPlaced = false;
        flashAlpha = redAlpha = staticAlpha = blackAlpha = 0f;
        glowMats.Clear(); glowOriginal.Clear(); glowKeyword.Clear();

        monster = m;
        cam = Camera.main;
        startTime = Time.time;
        v = requestedVariant != Variant.Random ? requestedVariant : Pick();
        requestedVariant = Variant.Random;
        lastVariant = v;
        side = Random.value < 0.5f ? -1f : 1f;
        stabRight = Random.value < 0.5f;

        // Player and HUD off.
        if (PlayerController.Instance != null) PlayerController.Instance.enabled = false;
        var hud = FindFirstObjectByType<FlashlightHUD>();
        if (hud != null) hud.gameObject.SetActive(false);

        // Monster into scare mode (creepy pose, face locked on the camera, AI off).
        monster.BeginJumpScare();
        var anim = monster.GetComponentInChildren<Animator>();
        chest = null;
        if (anim != null && anim.isHuman)
        {
            chest = anim.GetBoneTransform(HumanBodyBones.UpperChest);
            if (chest == null) chest = anim.GetBoneTransform(HumanBodyBones.Chest);
            if (chest == null) chest = anim.GetBoneTransform(HumanBodyBones.Spine);
        }

        // Remember the camera so it can be put back afterwards.
        camLocalPos = cam.transform.localPosition;
        camLocalRot = cam.transform.localRotation;
        baseFov = cam.fieldOfView;
        baseNear = cam.nearClipPlane;

        // Directions: from the camera to the monster, level.
        camPos = cam.transform.position;
        Vector3 lookDir = monster.transform.position - camPos; lookDir.y = 0f;
        if (lookDir.sqrMagnitude < 0.01f) { lookDir = cam.transform.forward; lookDir.y = 0f; }
        lookDir.Normalize();
        camStartRot = cam.transform.rotation;
        camTargetRot = Quaternion.LookRotation(lookDir) * Quaternion.Euler(-lookUp, 0f, 0f);
        monsterBaseRot = Quaternion.LookRotation(-lookDir);
        monster.transform.rotation = monsterBaseRot;

        cam.nearClipPlane = 0.02f; // the face (and the claw) get right into the lens

        SetupLights();
        SetupGlow();
        SetupPostFx();
        if (staticTex == null) SetupStatic();

        audioSrc = gameObject.AddComponent<AudioSource>();
        audioSrc.spatialBlend = 0f;
        audioSrc.volume = volume;
        audioSrc.priority = 0;
        audioSrc.playOnAwake = false;
        audioSrc.clip = scareClip != null ? scareClip : BuildAudio();
        audioSrc.Play();

        Debug.Log($"[JumpScare] start: {v}");
    }

    // ---------------- Setup ----------------

    void SetupLights()
    {
        var lg = new GameObject("ScareFaceLight");
        lg.transform.SetParent(cam.transform, false);
        lg.transform.localPosition = new Vector3(0f, -0.3f, 0.05f); // from below, like a held light
        faceLight = lg.AddComponent<Light>();
        faceLight.type = LightType.Point;
        faceLight.color = lightColor;
        faceLight.intensity = lightIntensity;
        faceLight.range = lightRange;
        faceLight.shadows = LightShadows.None;

        var ig = new GameObject("ScareInnerGlow");
        ig.transform.SetParent(transform, false);
        innerLight = ig.AddComponent<Light>();
        innerLight.type = LightType.Point;
        innerLight.color = glowColor;
        innerLight.intensity = innerLightIntensity;
        innerLight.range = 1.2f;
        innerLight.shadows = LightShadows.None;
    }

    // Light up the carved face: any monster material that has an emission map gets a hot orange emission.
    void SetupGlow()
    {
        foreach (var r in monster.GetComponentsInChildren<Renderer>(true))
        foreach (var mat in r.materials) // per-monster instances
        {
            if (mat == null || !mat.HasProperty("_EmissionColor") || !mat.HasProperty("_EmissionMap")) continue;
            if (mat.GetTexture("_EmissionMap") == null) continue;
            glowOriginal.Add(mat.GetColor("_EmissionColor"));
            glowKeyword.Add(mat.IsKeywordEnabled("_EMISSION"));
            mat.EnableKeyword("_EMISSION");
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            glowMats.Add(mat);
        }
    }

    void SetupPostFx()
    {
        var go = new GameObject("ScareVolume");
        go.transform.SetParent(transform, false);
        fxVolume = go.AddComponent<Volume>();
        fxVolume.isGlobal = true;
        fxVolume.priority = 1000f; // over the night volume
        fxVolume.weight = 1f;
        fxProfile = ScriptableObject.CreateInstance<VolumeProfile>();
        fxVolume.sharedProfile = fxProfile;

        lens = fxProfile.Add<LensDistortion>(true);
        lens.intensity.Override(0f);
        lens.scale.Override(1f);
        chroma = fxProfile.Add<ChromaticAberration>(true);
        chroma.intensity.Override(0f);
        vig = fxProfile.Add<Vignette>(true);
        vig.intensity.Override(0f);
        vig.color.Override(vignetteColor);
        vig.smoothness.Override(0.55f);
        color = fxProfile.Add<ColorAdjustments>(true);
        color.contrast.Override(0f);
        color.postExposure.Override(0f);
        color.saturation.Override(0f);
        grain = fxProfile.Add<FilmGrain>(true);
        grain.type.Override(FilmGrainLookup.Large02);
        grain.intensity.Override(1f);
        grain.response.Override(0.2f);
    }

    void SetupStatic()
    {
        staticTex = new Texture2D(160, 90, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        staticPixels = new Color32[160 * 90];
    }

    // Put back everything the scare changed, so play can continue after a respawn.
    void Cleanup()
    {
        running = false;
        if (cam != null)
        {
            cam.transform.localPosition = camLocalPos;
            cam.transform.localRotation = camLocalRot;
            cam.fieldOfView = baseFov;
            cam.nearClipPlane = baseNear;
        }
        if (faceLight != null) Destroy(faceLight.gameObject);
        if (innerLight != null) Destroy(innerLight.gameObject);
        if (fxVolume != null) Destroy(fxVolume.gameObject);
        if (fxProfile != null) Destroy(fxProfile);
        if (audioSrc != null) Destroy(audioSrc);
        for (int i = 0; i < glowMats.Count; i++)
        {
            var mat = glowMats[i];
            if (mat == null) continue;
            mat.SetColor("_EmissionColor", glowOriginal[i]);
            if (!glowKeyword[i]) mat.DisableKeyword("_EMISSION");
        }
        glowMats.Clear(); glowOriginal.Clear(); glowKeyword.Clear();
        if (monster != null)
        {
            monster.SetScareHand(stabRight, Vector3.zero, Vector3.zero, 0f);
            monster.SetScareOtherHand(Vector3.zero, Vector3.zero, 0f);
        }
        jaw = 0f; orangeAlpha = 0f; grabPlaced = false;
        flashAlpha = redAlpha = staticAlpha = blackAlpha = 0f;
    }

    // ---------------- Per frame (after animation + IK, so bone tweaks stick) ----------------

    void LateUpdate()
    {
        if (!running || cam == null) return;
        float t = Time.time - startTime;

        if (t >= FinalEnd)
        {
            if (!blacked)
            {
                blacked = true;
                if (audioSrc != null) audioSrc.Stop();      // instant silence
                if (faceLight != null) faceLight.enabled = false;
                if (innerLight != null) innerLight.enabled = false;
                if (monster != null) monster.gameObject.SetActive(false);
                if (fxVolume != null) fxVolume.weight = 0f;
            }
            blackAlpha = 1f; flashAlpha = redAlpha = staticAlpha = 0f;
            if (!handedOff && t >= FinalEnd + blackTime)
            {
                handedOff = true;
                var end = endOverride;
                endOverride = null;
                if (end != null) end();              // e.g. the ending: straight to YOU ESCAPED
                else DeathScreen.Begin(monster);     // takes over the black screen this same frame
                Cleanup();
            }
            return;
        }

        var head = monster.Head;
        float s = monster.transform.lossyScale.y;
        Vector3 viewDir = camTargetRot * Vector3.forward;   // camera -> monster
        Vector3 toCam = -viewDir;
        Vector3 viewUp = camTargetRot * Vector3.up;
        // AngleAxis(+a, Cross(from, to)) rotates 'from' toward 'to'.
        Vector3 leanAxis = Vector3.Cross(Vector3.up, toCam).normalized;   // tips the top toward the camera
        Vector3 chinAxis = Vector3.Cross(toCam, Vector3.down).normalized; // tips the face downward

        // The face point, fixed to the head bone (so it follows head tilts/rolls and stays centered).
        if (!haveLocalFace && head != null)
        {
            Vector3 f0 = head.position + Vector3.up * faceOffset.x * s + monster.transform.forward * faceOffset.y * s;
            localFace = Quaternion.Inverse(head.rotation) * (f0 - head.position);
            haveLocalFace = true;
        }

        float hold = t - HoldStart;
        bool inFinal = t >= HoldEnd;
        float fin = inFinal ? Mathf.Clamp01((t - HoldEnd) / finalLungeTime) : 0f;

        // ---- Rage vibration ----
        float vib = inFinal ? 2f : 1f;
        float tf = t * vibrateFrequency;
        float n1 = Mathf.PerlinNoise(tf, 0.37f) * 2f - 1f;
        float n2 = Mathf.PerlinNoise(1.9f, tf * 1.11f) * 2f - 1f;
        float n3 = Mathf.PerlinNoise(tf * 0.91f, 6.2f) * 2f - 1f;

        // The ending's GRAB is choreographed on its own (you get picked up and thrown, so the camera leaves its spot).
        if (v == Variant.Grab) { GrabLate(t, hold, inFinal, fin, head, s, n1, n2, n3, vib); return; }

        // ---- Variant behavior -> outputs ----
        oD = SlamTarget; oPulse = 0f; oBodyLean = 0f; oChestLean = 0f; oRoll = 0f; oYaw = 0f; oChin = headChinDown;
        oSpinYaw = 0f; oRed = 0f; oCamPos = Vector3.zero; oCamEuler = Vector3.zero;
        float handW = 0f; Vector3 handPos = Vector3.zero, handHint = Vector3.zero;
        hand2W = 0f;

        if (hold >= 0f && !inFinal)
        {
            switch (v)
            {
                case Variant.Bite: BiteFrame(hold, viewDir); break;
                case Variant.Stab: StabFrame(hold, head, s, viewDir, toCam, n1, n2, ref handW, ref handPos, ref handHint); break;
                case Variant.Spin: SpinFrame(hold); break;
            }
        }

        else if (inFinal && v == Variant.Stab && stabHits.Length > 0)
        {
            // The last stab stays buried through the final lunge.
            StabFrame(Mathf.Max(hold, stabHits[stabHits.Length - 1] + stabStrike), head, s, viewDir, toCam, n1, n2, ref handW, ref handPos, ref handHint);
            oPulse = 0f;
        }
        else if (inFinal && v == Variant.Spin)
        {
            oRoll = rollVal; // stays upside down for the final lunge
        }

        // Distance: appear -> slam (overshoot) -> variant -> final lunge into the lens.
        float d;
        if (t < slamTime) d = Mathf.LerpUnclamped(appearDistance, SlamTarget, EaseOutBack(t / slamTime, 2.5f));
        else if (!inFinal) d = oD;
        else d = Mathf.Lerp(oD, finalDistance, fin * fin);

        // ---- Camera: instant snap + variant kicks + a hair of vibration + wide looming FOV ----
        float w = Mathf.Clamp01(t / snapTime);
        Quaternion camRot = Quaternion.Slerp(camStartRot, camTargetRot, w);
        cam.transform.position = camPos + oCamPos + (camTargetRot * new Vector3(n2, n3, 0f)) * 0.004f * vib;
        cam.transform.rotation = camRot * Quaternion.Euler(oCamEuler.x + n1 * 0.5f * vib, oCamEuler.y + n2 * 0.5f * vib, oCamEuler.z + n3 * 0.8f * vib);
        cam.fieldOfView = Mathf.Lerp(baseFov, fovWide, Mathf.Clamp01(t / slamTime)) + oPulse * 4f + fin * 10f;

        // ---- Body: vibrates, leans with the action ----
        monster.transform.rotation =
            Quaternion.AngleAxis(n1 * vibrateBodyAngle * vib, toCam) *
            Quaternion.AngleAxis(oBodyLean + n2 * vibrateBodyAngle * vib + fin * 10f, leanAxis) *
            monsterBaseRot;
        if (chest != null)
            chest.rotation = Quaternion.AngleAxis(oChestLean + n3 * vibrateBodyAngle * vib, leanAxis) * chest.rotation;

        // ---- Head: tilt / turn / chin (+ vibration) ----
        if (head != null)
        {
            float roll = oRoll + n1 * vibrateHeadAngle * 0.5f * vib;
            float yaw = oYaw + n2 * vibrateHeadAngle * vib;
            float chin = oChin + n3 * vibrateHeadAngle * 0.4f * vib;
            head.rotation =
                Quaternion.AngleAxis(roll, toCam) *
                Quaternion.AngleAxis(chin, chinAxis) *
                Quaternion.AngleAxis(yaw, viewUp) *
                head.rotation;
        }

        // ---- Place the monster so its face sits dead center ----
        Vector3 face = head != null && haveLocalFace
            ? head.position + head.rotation * localFace
            : monster.transform.position + Vector3.up * 1.75f * s;
        Vector3 faceTarget = v == Variant.Grab
            ? cam.transform.position + cam.transform.forward * d   // it stays in your face while it drags you down
            : camPos + viewDir * d;
        monster.transform.position += faceTarget - face;

        // ---- Spin: turn the head around the neck AFTER centering, so the neck stays put and the face orbits ----
        if (head != null && Mathf.Abs(oSpinYaw) > 0.01f)
            head.rotation = Quaternion.AngleAxis(oSpinYaw, Vector3.up) * head.rotation;

        // ---- Stab arm (IK, applied on the next animation pass) ----
        monster.SetScareHand(stabRight, handPos, handHint, handW);
        monster.SetScareOtherHand(hand2Pos, hand2Hint, hand2W);

        // ---- Glow + lights ----
        float flick = 0.8f + 0.2f * Mathf.PerlinNoise(t * 35f, 3.3f);
        float glow = glowIntensity * flick * (1f + oPulse * 0.7f + fin);
        foreach (var mat in glowMats) if (mat != null) mat.SetColor("_EmissionColor", glowColor * glow);
        if (innerLight != null && head != null)
        {
            innerLight.transform.position = head.position + toCam * 0.15f * s;
            innerLight.intensity = innerLightIntensity * flick * (1f + oPulse * 0.6f);
        }
        if (faceLight != null) faceLight.intensity = lightIntensity * flick * (1f + oPulse * 0.4f);

        // ---- Post FX ----
        float landed = Mathf.Clamp01(t / slamTime);
        if (lens != null) lens.intensity.value = Mathf.Clamp(lensBulge * (0.7f * landed + 0.3f * oPulse) * (1f + fin * 0.8f), -1f, 1f);
        if (chroma != null) chroma.intensity.value = Mathf.Clamp01(chromatic * (0.45f * landed + 0.55f * oPulse + fin));
        if (vig != null) vig.intensity.value = Mathf.Clamp01(vignette * landed + oPulse * 0.08f + fin * 0.2f + bloodAlpha * 0.3f);
        if (color != null)
        {
            color.contrast.value = contrast * landed;
            color.postExposure.value = Mathf.Lerp(1.6f, 0f, Mathf.Clamp01(t / 0.12f)) + oPulse * 0.25f; // blown-out hit, then pulses
            color.saturation.value = -20f * landed;
        }

        // ---- Screen overlays ----
        flashAlpha = Mathf.Lerp(0.9f, 0f, Mathf.Clamp01(t / 0.08f)); // white hit on frame one
        redAlpha = Mathf.Max(oPulse * 0.18f, oRed) + bloodAlpha;
        float staticSpike = Random.value < 0.08f ? Random.Range(0.15f, 0.35f) : 0f; // occasional signal hiccup
        staticAlpha = inFinal ? Mathf.Lerp(0.2f, 1f, fin * fin) : (hold >= 0f ? staticDuringAttack + staticSpike : 0f);
        if (staticAlpha > 0.001f) RefreshStatic();
    }

    // ---------------- Variants ----------------

    void BiteFrame(float hold, Vector3 viewDir)
    {
        float period = 1f / biteRate;
        float age = hold % period;
        int biteIndex = Mathf.FloorToInt(hold / period);
        const float snapIn = 0.045f;
        float bite = age < snapIn ? Mathf.SmoothStep(0f, 1f, age / snapIn) : Mathf.Exp(-(age - snapIn) * 11f);

        float sideTarget = biteIndex < switchAfterBites ? side : -side;
        sideVal = Mathf.Lerp(sideVal, sideTarget, 1f - Mathf.Exp(-switchSnap * Time.deltaTime));

        float jerk = sideVal * biteHeadSnap * bite;
        oD = Mathf.Lerp(slamDistance, creepDistance, Mathf.SmoothStep(0f, 1f, hold / biteHoldTime)) - bite * biteDistance;
        oPulse = bite;
        oBodyLean = bite * bodyLean;
        oChestLean = bite * chestLean;
        oRoll = sideVal * sideTilt + jerk * 0.3f;
        oYaw = sideVal * sideTurn + jerk;
        oChin = headChinDown - bite * 2f;
        oCamPos = viewDir * bite * biteJolt;
        oCamEuler = new Vector3(bite * 2.2f, 0f, 0f);
    }

    void StabFrame(float hold, Transform head, float s, Vector3 viewDir, Vector3 toCam, float n1, float n2,
                   ref float handW, ref Vector3 handPos, ref Vector3 handHint)
    {
        oD = stabFaceDistance;
        oChin = headChinDown + 6f;
        int last = stabHits.Length - 1;
        if (head == null || last < 0) return;

        Vector3 sideDir = monster.transform.right * (stabRight ? 1f : -1f);
        Vector3 hp = head.position;
        Vector3 rest = hp + sideDir * 0.3f * s + Vector3.down * 0.55f * s + toCam * 0.1f * s;
        Vector3 windup = hp + sideDir * 0.45f * s + Vector3.up * 0.15f * s - toCam * 0.12f * s; // drawn back beside the head
        Vector3 lensPt = camPos + viewDir * 0.03f;
        handHint = hp + sideDir * 0.55f * s + Vector3.down * 0.25f * s;

        // Pick the stab whose window we're in (the latest one that has started).
        int i = 0;
        for (int k = 0; k <= last; k++) if (hold >= stabHits[k] - stabWindup) i = k;
        float sAge = hold - stabHits[i];
        bool isLast = i == last;
        float impact = 0f;

        if (sAge < -stabWindup)
        {
            handPos = rest; handW = 0f;                               // before the first wind-up
        }
        else if (sAge < 0f)
        {
            float wu = Mathf.SmoothStep(0f, 1f, 1f + sAge / stabWindup); // wind up: arm drawn back
            handPos = i == 0 ? Vector3.Lerp(rest, windup, wu) : windup;
            handW = i == 0 ? wu : 1f;
            oBodyLean = -4f * wu;                                     // rears back
            oYaw = side * 6f * wu;
        }
        else if (sAge < stabStrike)
        {
            float k = sAge / stabStrike; k *= k;                      // accelerate into the lens
            handPos = Vector3.Lerp(windup, lensPt, k); handW = 1f;
            oBodyLean = Mathf.Lerp(-4f, 10f, k);
            oD = stabFaceDistance - 0.1f * k;
        }
        else if (isLast || sAge < stabStrike + stabStuck)
        {
            float since = sAge - stabStrike;
            impact = Mathf.Exp(-since * 12f);
            Vector3 twist = (Quaternion.LookRotation(viewDir) * new Vector3(n1, n2, 0f)) * 0.012f; // grinding it in
            handPos = lensPt + twist; handW = 1f;
            oBodyLean = 10f;
            oD = stabFaceDistance - 0.1f;
        }
        else
        {
            float r = Mathf.Clamp01((sAge - stabStrike - stabStuck) / stabRetract); // rip it out
            handPos = Vector3.Lerp(lensPt, windup, Mathf.SmoothStep(0f, 1f, r)); handW = 1f;
            oBodyLean = Mathf.Lerp(10f, -4f, r);
            oD = stabFaceDistance - 0.1f * (1f - r);
        }

        oPulse = impact;
        oChestLean = oBodyLean * 0.8f;
        oRoll = side * 5f;
        // Camera: knocked back and snapped back on impact; slumps after the last stab.
        oCamPos = -viewDir * stabKnockback * impact;
        oCamEuler = new Vector3(-stabCamKick * impact, 0f, side * 4f * impact);
        float hitsBefore = 0; for (int k = 0; k <= last; k++) if (hold >= stabHits[k]) hitsBefore++;
        if (hitsBefore > 0) bloodAlpha = Mathf.MoveTowards(bloodAlpha, 0.1f * hitsBefore, Time.deltaTime * 2f);
        oRed = impact * 0.55f;
        float lastHit = stabHits[last];
        if (hold > lastHit)
        {
            float slump = Mathf.SmoothStep(0f, 1f, (hold - lastHit) / 0.45f);
            oCamEuler.z += -side * stabSlump * slump;
            oCamPos += Vector3.down * 0.07f * slump;
        }
    }

    // GRAB (the ending): both hands clamp onto your head and it shakes you, then it lifts you up over its head
    // (it tips its face back to look up at you, the carved mouth blazing), holds you there... and THROWS you down,
    // tumbling, into its mouth. The jaws slam shut over the screen, then black.
    void GrabLate(float t, float hold, bool inFinal, float fin, Transform head, float s, float n1, float n2, float n3, float vib)
    {
        float h = inFinal ? grabHoldTime : Mathf.Max(0f, hold);
        Vector3 viewDir = camTargetRot * Vector3.forward;
        float landed = Mathf.Clamp01(t / slamTime);

        // It appears right in front of you, then stays planted: from here on YOU are the one that moves.
        Vector3 face = head != null && haveLocalFace ? head.position + head.rotation * localFace
                                                     : monster.transform.position + Vector3.up * 1.75f * s;
        if (!grabPlaced)
        {
            float d0 = t < slamTime ? Mathf.LerpUnclamped(appearDistance, grabFaceDistance, EaseOutBack(t / slamTime, 2.5f)) : grabFaceDistance;
            monster.transform.rotation = monsterBaseRot;
            monster.transform.position += (camPos + viewDir * d0) - face;
            face = camPos + viewDir * d0;
            if (t >= slamTime) { grabPlaced = true; grabFace0 = face; grabLastLook = camTargetRot; }
        }
        Vector3 F0 = grabPlaced ? grabFace0 : face;
        Vector3 overhead = F0 + Vector3.up * grabLiftHeight - viewDir * 0.2f;

        // ---- Where you are ----
        float impact = Mathf.Exp(-h * 14f);
        float shake = 0f, lift = 0f, throwK = 0f, inside = 0f, roll = 0f, fovAdd = 0f;
        Vector3 cp;
        if (h < grabShakeEnd)
        {
            shake = Mathf.Sin(h * 2f * Mathf.PI * 6.5f) * Mathf.SmoothStep(0f, 1f, h / 0.12f);
            cp = camPos + (camTargetRot * Vector3.right) * shake * 0.035f + Vector3.up * Mathf.Abs(shake) * 0.02f;
        }
        else if (h < grabLiftEnd)
        {
            lift = Mathf.SmoothStep(0f, 1f, (h - grabShakeEnd) / (grabLiftEnd - grabShakeEnd));
            cp = Vector3.Lerp(camPos, overhead, lift) + Vector3.up * Mathf.Sin(lift * Mathf.PI) * 0.12f;
        }
        else if (h < grabThrowStart)
        {
            lift = 1f;
            float dangle = Mathf.Clamp01((h - grabLiftEnd) / (grabThrowStart - grabLiftEnd));
            cp = overhead + new Vector3(n1, n2 * 0.5f, n3) * 0.02f + Vector3.up * 0.06f * Mathf.Sin(dangle * Mathf.PI); // a last hoist before the throw
        }
        else if (h < grabThrowEnd)
        {
            lift = 1f;
            throwK = Mathf.Pow((h - grabThrowStart) / (grabThrowEnd - grabThrowStart), 2.2f);
            cp = Vector3.Lerp(overhead, face + (overhead - face).normalized * 0.04f, throwK);
            roll = side * grabTumble * throwK;
            fovAdd = 24f * throwK;
        }
        else
        {
            lift = 1f; throwK = 1f;
            inside = Mathf.Clamp01((h - grabThrowEnd) / 0.2f);
            jaw = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((h - grabThrowEnd) / grabJawTime));
            cp = face + (overhead - face).normalized * 0.04f + new Vector3(n1, n2, n3) * 0.01f * (1f + jaw);
            roll = side * grabTumble;
            fovAdd = 24f;
        }

        // ---- Camera: always staring at its face ----
        Vector3 toFace = face - cp;
        if (toFace.sqrMagnitude > 0.01f) grabLastLook = Quaternion.LookRotation(toFace);
        Quaternion look = grabLastLook * Quaternion.Euler(n1 * 0.6f * vib, n2 * 0.6f * vib, roll + shake * 9f + n3 * vib);
        if (t < snapTime) look = Quaternion.Slerp(camStartRot, look, t / snapTime);
        cam.transform.SetPositionAndRotation(cp, look);
        cam.fieldOfView = Mathf.Lerp(baseFov, fovWide, landed) + fovAdd + fin * 10f;

        // ---- Hands: clamped on your head; once it throws, they pry its own mouth open ----
        var ct = cam.transform;
        Vector3 cr = ct.right, cu = ct.up, cf = ct.forward;
        float mainSign = stabRight ? -1f : 1f;  // facing you, its right hand is on your left
        float clamp = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(h / 0.12f));
        float release = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((h - grabThrowStart) / 0.12f));
        Vector3 mRight = monster.transform.right;
        Vector3 HandAt(float sgn)
        {
            Vector3 open = cp + cr * sgn * 0.6f + cf * 0.4f - cu * 0.25f;
            Vector3 grip = cp + cr * sgn * 0.15f + cf * 0.09f - cu * 0.03f;
            Vector3 pry = face + mRight * (-sgn) * 0.34f * s + Vector3.down * 0.06f * s;
            return Vector3.Lerp(Vector3.Lerp(open, grip, clamp), pry, release);
        }
        Vector3 ElbowAt(float sgn) => Vector3.Lerp(cp + cr * sgn * 0.45f + cf * 0.3f - cu * 0.4f,
                                                   face + mRight * (-sgn) * 0.6f * s + Vector3.down * 0.3f * s, release);
        Vector3 jitter = (look * new Vector3(n2, n3, 0f)) * 0.008f;
        monster.SetScareHand(stabRight, HandAt(mainSign) + jitter, ElbowAt(mainSign), 1f);
        monster.SetScareOtherHand(HandAt(-mainSign) - jitter, ElbowAt(-mainSign), 1f);

        // ---- Body + head: trembling with effort, face blazing ----
        Vector3 toCamNow = (cp - (head != null ? head.position : face)).normalized;
        monster.transform.rotation = Quaternion.AngleAxis(n1 * vibrateBodyAngle * vib, toCamNow) * monsterBaseRot;
        if (head != null)
            head.rotation = Quaternion.AngleAxis(n2 * vibrateHeadAngle * vib * (1f + lift), toCamNow) * head.rotation;

        float flick = 0.8f + 0.2f * Mathf.PerlinNoise(t * 35f, 3.3f);
        float glowK = 1f + 1.3f * lift + 3f * throwK + 4f * inside;
        foreach (var mat in glowMats) if (mat != null) mat.SetColor("_EmissionColor", glowColor * glowIntensity * flick * glowK);
        if (innerLight != null && head != null)
        {
            innerLight.transform.position = head.position + toCamNow * 0.15f * s;
            innerLight.intensity = innerLightIntensity * flick * (0.7f + 0.5f * glowK);
        }
        if (faceLight != null) faceLight.intensity = lightIntensity * flick * (1f - 0.6f * lift);

        // ---- Post FX ----
        float pulse = Mathf.Max(impact, throwK * 0.6f);
        if (lens != null) lens.intensity.value = Mathf.Clamp(lensBulge * (0.7f * landed + 0.7f * throwK) * (1f + fin * 0.8f), -1f, 1f);
        if (chroma != null) chroma.intensity.value = Mathf.Clamp01(chromatic * (0.45f * landed + 0.6f * pulse + fin));
        if (vig != null) vig.intensity.value = Mathf.Clamp01(vignette * landed + 0.15f * throwK + fin * 0.2f);
        if (color != null)
        {
            color.contrast.value = contrast * landed;
            color.postExposure.value = Mathf.Lerp(1.6f, 0f, Mathf.Clamp01(t / 0.12f)) + 0.5f * inside;
            color.saturation.value = -20f * landed;
        }

        // ---- Overlays ----
        flashAlpha = Mathf.Lerp(0.9f, 0f, Mathf.Clamp01(t / 0.08f));
        redAlpha = impact * 0.25f + bloodAlpha;
        orangeAlpha = 0.25f * throwK + 0.4f * inside;
        float staticSpike = Random.value < 0.06f ? Random.Range(0.1f, 0.25f) : 0f;
        staticAlpha = inFinal ? Mathf.Lerp(0.2f, 1f, fin * fin) : (hold >= 0f ? staticDuringAttack * 0.5f + staticSpike : 0f);
        if (staticAlpha > 0.001f) RefreshStatic();
    }

    void SpinFrame(float hold)
    {
        oD = Mathf.Lerp(spinFaceDistance, spinFaceDistance - 0.06f, Mathf.Clamp01(hold / spinHoldTime));

        // Slow turn all the way round (back of the head faces you), then snap back to face you.
        if (hold < spinStart) oSpinYaw = 0f;
        else if (hold < spinEnd) oSpinYaw = spinDegrees * Mathf.SmoothStep(0f, 1f, (hold - spinStart) / (spinEnd - spinStart));
        else
        {
            float k = Mathf.Clamp01((hold - spinEnd) / snapBackTime);
            float full = Mathf.Ceil(spinDegrees / 360f) * 360f;   // next full turn = facing you again
            oSpinYaw = Mathf.LerpUnclamped(spinDegrees, full, EaseOutBack(k, 1.8f));
            if (k >= 1f) oSpinYaw = 0f;                           // full turns == facing you
        }
        oSpinYaw *= side;

        // Then ratchet upside down in cracking jerks.
        float rollTarget = 0f;
        if (hold >= rollStart)
        {
            int steps = Mathf.Min(rollSteps, Mathf.FloorToInt((hold - rollStart) / rollStepTime) + 1);
            rollTarget = -side * rollDegrees * steps / Mathf.Max(1, rollSteps);
        }
        rollVal = Mathf.Lerp(rollVal, rollTarget, 1f - Mathf.Exp(-45f * Time.deltaTime));
        oRoll = rollVal;
        oChin = headChinDown + 4f;

        // Pulse on the big cracks: the snap back and each roll step.
        float lastEvent = -10f;
        if (hold >= spinEnd) lastEvent = spinEnd;
        for (int k = 0; k < rollSteps; k++) { float e = rollStart + k * rollStepTime; if (hold >= e) lastEvent = e; }
        oPulse = Mathf.Exp(-(hold - lastEvent) * 10f);
        oCamEuler = new Vector3(1.5f * oPulse, 0f, 0f);
    }

    // ---------------- Overlays ----------------

    void RefreshStatic()
    {
        for (int i = 0; i < staticPixels.Length; i++)
        {
            byte b = (byte)Random.Range(0, 256);
            staticPixels[i] = new Color32(b, b, b, 255);
        }
        staticTex.SetPixels32(staticPixels);
        staticTex.Apply(false);
    }

    void OnGUI()
    {
        if (!running) return;
        GUI.depth = -1000;
        var full = new Rect(0, 0, Screen.width, Screen.height);
        if (staticAlpha > 0.001f && staticTex != null) Draw(full, staticTex, new Color(1f, 1f, 1f, staticAlpha));
        if (redAlpha > 0.001f) Draw(full, Texture2D.whiteTexture, new Color(0.7f, 0f, 0f, Mathf.Clamp01(redAlpha)));
        if (orangeAlpha > 0.001f) Draw(full, Texture2D.whiteTexture, new Color(jawGlow.r, jawGlow.g * 0.8f, jawGlow.b, Mathf.Clamp01(orangeAlpha)));
        if (jaw > 0.001f)
        {
            // Its carved jaws slam shut over you: teeth from the top and bottom, interlocking in the middle.
            if (jawTex == null) jawTex = MakeJaw(jawFlesh, jawGlow);
            float sw = Screen.width, sh = Screen.height, jh = sh * 0.6f, travel = sh * 0.56f * jaw;
            GUI.color = Color.white;
            GUI.DrawTextureWithTexCoords(new Rect(0, sh - travel, sw, jh), jawTex, new Rect(0f, 0f, 1f, 1f));                  // bottom jaw, teeth up
            GUI.DrawTextureWithTexCoords(new Rect(0, travel - jh, sw, jh), jawTex, new Rect(0.5f / 7f, 1f, 1f, -1f));          // top jaw, teeth down, offset
        }
        if (flashAlpha > 0.001f) Draw(full, Texture2D.whiteTexture, new Color(1f, 1f, 1f, flashAlpha));
        if (blackAlpha > 0.001f) Draw(full, Texture2D.whiteTexture, new Color(0f, 0f, 0f, blackAlpha));
        GUI.color = Color.white;
    }

    // A jack-o'-lantern jaw: pumpkin flesh with a row of uneven carved teeth along the top edge, glowing at the cut.
    static Texture2D MakeJaw(Color flesh, Color glow)
    {
        const int W = 256, H = 128, teeth = 7;
        var tex = new Texture2D(W, H, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat, hideFlags = HideFlags.HideAndDontSave };
        var px = new Color[W * H];
        for (int x = 0; x < W; x++)
        {
            float u = x / (float)W;
            float tu = u * teeth, ti = Mathf.Floor(tu), f = tu - ti;
            float tall = 0.11f + 0.07f * Mathf.PerlinNoise(ti * 1.7f, 0.3f);           // uneven teeth
            float tooth = Mathf.Max(0f, 1f - Mathf.Abs(f - 0.5f) * 2.4f);              // triangle with a flat gap between
            float edge = 0.78f + tall * tooth;                                          // jagged top edge (0..1 height)
            for (int y = 0; y < H; y++)
            {
                float v = y / (float)(H - 1);
                float a = Mathf.Clamp01((edge - v) * H * 0.6f);                         // anti-aliased cut
                float rim = Mathf.Clamp01(1f - (edge - v) / 0.09f);                      // glowing cut edge
                float grit = Mathf.Lerp(0.7f, 1.1f, Mathf.PerlinNoise(x * 0.12f, y * 0.12f));
                var c = Color.Lerp(flesh * grit, glow, rim * rim);
                c.a = a;
                px[y * W + x] = c;
            }
        }
        tex.SetPixels(px);
        tex.Apply(false, false);
        return tex;
    }

    static void Draw(Rect r, Texture tex, Color c)
    {
        GUI.color = c;
        GUI.DrawTexture(r, tex, ScaleMode.StretchToFill);
    }

    void OnDestroy()
    {
        if (fxProfile != null) Destroy(fxProfile);
        if (staticTex != null) Destroy(staticTex);
    }

    static float EaseOutBack(float x, float c1)
    {
        float c3 = c1 + 1f;
        float a = x - 1f;
        return 1f + c3 * a * a * a + c1 * a * a;
    }

    // ---------------- Synthesized audio ----------------

    AudioClip BuildAudio()
    {
        float hs = HoldStart, he = HoldEnd;
        var chomps = new List<float>();
        var impacts = new List<float>();
        var cracks = new List<Vector2>(); // (time, loudness)
        Func<float, float> screamGain = _ => 1f;

        switch (v)
        {
            case Variant.Bite:
                for (float c = hs + 0.04f; c < he; c += 1f / biteRate) chomps.Add(c);
                break;
            case Variant.Stab:
                foreach (var h in stabHits) impacts.Add(hs + h + stabStrike);
                break;
            case Variant.Spin:
                for (float c = hs + spinStart; c < hs + spinEnd; c += 0.07f) cracks.Add(new Vector2(c, 0.35f)); // creaking turn
                cracks.Add(new Vector2(hs + spinEnd + snapBackTime * 0.5f, 1.3f));                              // SNAP back
                for (int k = 0; k < rollSteps; k++) cracks.Add(new Vector2(hs + rollStart + k * rollStepTime, 1.1f));
                float s0 = hs + spinStart, s1 = hs + spinEnd;
                screamGain = t => t < s0 ? 1f : t < s1 ? Mathf.Lerp(1f, 0.12f, Mathf.Clamp01((t - s0) / 0.15f)) : 1f; // goes quiet while it turns
                break;
        }
        return MakeAudio(FinalEnd + 0.05f, hs, he, chomps, impacts, cracks, screamGain);
    }

    // Hit: noise blast + sub boom. Screech: three detuned saws at dissonant intervals, jittery vibrato,
    // ring-modulated, bit-crushed. Growl under the attack. Variant layers: chomps (bite), wet stab impacts
    // with a whoosh before each (stab), bone creaks and cracks (spin). Final lunge: screech spikes, static floods.
    static AudioClip MakeAudio(float seconds, float holdStart, float holdEnd,
                               List<float> chomps, List<float> impacts, List<Vector2> cracks, Func<float, float> screamGain)
    {
        const int sr = 44100;
        int n = Mathf.RoundToInt(sr * seconds);
        var data = new float[n];
        var rng = new System.Random(1031);
        double p1 = 0, p2 = 0, p3 = 0, pg = 0;
        float held = 0f; int holdCount = 0;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)sr;
            float noise = (float)(rng.NextDouble() * 2 - 1);

            float f = Mathf.Lerp(1500f, 640f, Mathf.Clamp01(t / 0.45f));
            f += 60f * Mathf.Sin(2f * Mathf.PI * 2.3f * t);
            if (t > holdEnd) f += 900f * Mathf.Clamp01((t - holdEnd) / Mathf.Max(0.01f, seconds - holdEnd));
            f *= 1f + 0.045f * Mathf.Sin(2f * Mathf.PI * 15f * t) + 0.02f * noise;
            p1 += f / sr; p2 += f * 1.414 / sr; p3 += f * 2.03 / sr;
            float saw = (float)((p1 % 1.0) * 2 - 1) * 0.5f + (float)((p2 % 1.0) * 2 - 1) * 0.35f + (float)((p3 % 1.0) * 2 - 1) * 0.25f;
            float ring = saw * Mathf.Sin(2f * Mathf.PI * 43f * t);
            float scream = (saw * 0.55f + ring * 0.45f) * screamGain(t);

            float hit = t < 0.18f ? noise * Mathf.Exp(-t * 18f) * 1.4f : 0f;
            float boom = Mathf.Sin(2f * Mathf.PI * (55f - 20f * t) * t) * Mathf.Exp(-t * 6f) * 1.3f;

            float gf = 62f + 9f * Mathf.Sin(2f * Mathf.PI * 3.7f * t);
            pg += gf / sr;
            float growl = t > holdStart ? ((float)((pg % 1.0) * 2 - 1) * 0.7f + noise * 0.4f) * Mathf.Clamp01((t - holdStart) / 0.15f) : 0f;

            float layer = 0f;
            foreach (var c0 in chomps)
            {
                float c = t - c0;
                if (c >= 0f && c < 0.09f) layer += (noise * 0.7f + Mathf.Sin(2f * Mathf.PI * 90f * c)) * Mathf.Exp(-c * 40f) * 1.3f;
            }
            foreach (var h in impacts)
            {
                float c = t - h;
                if (c >= -0.12f && c < 0f) layer += noise * Mathf.Pow((c + 0.12f) / 0.12f, 2f) * 0.5f;                  // whoosh in
                if (c >= 0f && c < 0.25f)
                    layer += (Mathf.Sin(2f * Mathf.PI * 50f * c) * 1.6f * Mathf.Exp(-c * 14f)                          // thud
                            + noise * Mathf.Exp(-c * 22f) * 1.2f                                                    // crunch
                            + noise * Mathf.Sin(2f * Mathf.PI * 300f * c) * Mathf.Exp(-c * 9f) * 0.6f);             // wet squelch
            }
            foreach (var ck in cracks)
            {
                float c = t - ck.x;
                if (c >= 0f && c < 0.06f)
                    layer += (noise * Mathf.Exp(-c * 300f) * 1.5f + Mathf.Sin(2f * Mathf.PI * 1700f * c) * Mathf.Exp(-c * 200f)
                            + Mathf.Sin(2f * Mathf.PI * 75f * c) * Mathf.Exp(-c * 40f)) * ck.y;
            }

            float stat = t > holdEnd ? noise * Mathf.Clamp01((t - holdEnd) / 0.04f) : 0f;

            float x = scream * 0.9f + hit + boom + growl * 0.5f + layer + stat * 1.2f;
            x *= 2.6f;
            x = x / (1f + Mathf.Abs(x)); // soft clip

            if (holdCount <= 0) { held = Mathf.Round(x * 24f) / 24f; holdCount = 4; } // bit-crush
            holdCount--;
            data[i] = Mathf.Lerp(x, held, 0.5f) * 0.98f;
        }
        var clip = AudioClip.Create("PumpkinScare", n, 1, sr, false);
        clip.SetData(data, 0);
        return clip;
    }
}
