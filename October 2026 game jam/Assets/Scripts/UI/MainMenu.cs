using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

// Main menu for "Creepers in the Corn". Builds its own scenery at runtime (nothing to set up in the scene
// beyond this component and the asset slots):
//   - a moonlit corn field with a dark path leading in, corn cards behind for depth,
//   - a big moon low in the sky, the pumpkin strung up on a post like a scarecrow on the right,
//     its carved face faintly glowing; now and then (and whenever you hover QUIT) it slowly turns to look at you,
//   - the title top-left, PLAY / SETTINGS / QUIT GAME below it (mouse or W/S/arrows + Enter).
// The scene's Moonlight, Global Volume (with the gritty filter), fog and ground are reused.
public class MainMenu : MonoBehaviour
{
    [Header("Assets")]
    public GameObject pumpkinPrefab;
    public GameObject[] stalkPrefabs;
    public Material[] cardMaterials;
    [Tooltip("Scene loaded by PLAY.")]
    public string gameScene = "Maze";

    [Header("Sounds (empty = placeholder)")]
    public AudioClip hoverClip;
    public AudioClip clickClip;
    public AudioClip ambienceClip;
    [Range(0f, 1f)] public float ambienceVolume = 0.35f;
    [Range(0f, 1f)] public float musicVolume = 0.55f;
    [Range(0f, 1f)] public float windVolume = 0.25f;

    [Header("Scene layout")]
    public Vector3 cameraPosition = new Vector3(0f, 1.55f, 0f);
    public float cameraPitch = -4f;
    public Vector3 scarecrowPosition = new Vector3(2.6f, 0f, 5.2f);
    public float scarecrowHang = 0.35f;              // feet this far off the ground
    public Vector3 moonPosition = new Vector3(10.5f, 6.8f, 22f);
    public float moonSize = 6f;

    enum Item { Play, HowTo, Settings, Credits, Quit }
    static readonly string[] Labels = { "PLAY", "HOW TO PLAY", "SETTINGS", "CREDITS", "QUIT GAME" };

    Camera cam;
    Quaternion camBaseRot;
    int selected = -1, lastHover = -1;
    bool settingsOpen, howToOpen, creditsOpen, leaving;
    SettingsPanel panel;
    HowToPlayPanel howTo;
    CreditsPanel credits;
    float howToAt, creditsAt;
    float start, leaveStart;
    Item leaveAction;
    AudioSource sfx, amb, windSrc;
    GUIStyle titleStyle, smallStyle, itemStyle, panelTitle, panelText;
    static Texture2D leftShade;

    // scarecrow
    Transform rig, head;
    Quaternion headBaseLocal;
    Vector3 headLocalFace;
    float lookW, nextLook, lookUntil;
    Light faceLight;
    readonly System.Collections.Generic.List<Material> glowMats = new System.Collections.Generic.List<Material>();

    void Start()
    {
        start = Time.unscaledTime;
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        SetupWorld();
        BuildCorn();
        AddMoon();
        BuildScarecrow();

        sfx = gameObject.AddComponent<AudioSource>();
        sfx.playOnAwake = false; sfx.spatialBlend = 0f;
        amb = gameObject.AddComponent<AudioSource>();
        amb.playOnAwake = false; amb.spatialBlend = 0f; amb.loop = true;
        amb.clip = ambienceClip != null ? ambienceClip : GameAudio.Get("Music_Menu");
        if (amb.clip == null) amb.clip = MakeWind();
        windSrc = gameObject.AddComponent<AudioSource>();
        windSrc.playOnAwake = false; windSrc.spatialBlend = 0f; windSrc.loop = true;
        windSrc.clip = GameAudio.Get("Ambience_Wind");
        windSrc.volume = 0f;
        if (windSrc.clip != null) windSrc.Play();
        amb.volume = 0f;
        amb.Play();
        panel = new SettingsPanel(sfx)
        {
            OnBack = () => settingsOpen = false,
            OnCalibrationDone = () => { leaving = true; leaveAction = Item.Play; leaveStart = Time.unscaledTime; }, // first PLAY: brightness, then the game
        };
        howTo = new HowToPlayPanel(sfx) { OnBack = () => howToOpen = false };
        credits = new CreditsPanel(sfx) { OnBack = () => creditsOpen = false };
        nextLook = Time.time + Random.Range(5f, 8f);
    }

    // ---------------- World ----------------

    void SetupWorld()
    {
        // Camera (the game scene's camera lived on the player, so the menu makes its own).
        cam = Camera.main;
        if (cam == null)
        {
            var cg = new GameObject("Main Camera") { tag = "MainCamera" };
            cam = cg.AddComponent<Camera>();
            cg.AddComponent<AudioListener>();
        }
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = RenderSettings.fogColor;
        cam.fieldOfView = 70f;
        cam.nearClipPlane = 0.05f;
        cam.farClipPlane = 80f;
        var data = cam.GetUniversalAdditionalCameraData();
        data.renderPostProcessing = true;
        data.antialiasing = AntialiasingMode.FastApproximateAntialiasing;
        cam.transform.SetPositionAndRotation(cameraPosition, Quaternion.Euler(cameraPitch, 0f, 0f));
        camBaseRot = cam.transform.rotation;
        int cornLayer = LayerMask.NameToLayer("Corn");
        if (cornLayer >= 0)
        {
            var dist = new float[32];
            dist[cornLayer] = 45f;
            cam.layerCullDistances = dist;
        }

        RenderSettings.fogDensity = 0.03f; // a touch clearer than in the maze so the field reads

        var ground = GameObject.Find("Ground");
        if (ground != null)
        {
            ground.transform.position = new Vector3(0f, 0f, 15f);
            ground.transform.localScale = new Vector3(10f, 1f, 8f);
        }

        // Faint cold fill from the camera side so the corn faces aren't pitch black.
        var fill = new GameObject("MenuFill").AddComponent<Light>();
        fill.type = LightType.Directional;
        fill.color = new Color(0.55f, 0.65f, 0.9f);
        fill.intensity = 0.18f;
        fill.shadows = LightShadows.None;
        fill.transform.rotation = Quaternion.Euler(25f, 10f, 0f);
    }

    // Kept cheap: real 3D stalks only where you can actually see them (the front edge of the field, the walls of
    // the path, the side clumps), no hidden interior stalks, no stalk shadows. Everything behind the front edge is
    // flat corn cards, merged into a few big meshes.
    void BuildCorn()
    {
        var root = new GameObject("MenuCorn").transform;
        var cards = new GameObject("MenuCornCards").transform;
        var rng = new System.Random(1031);
        var s = CornPlanter.Settings.Default;
        s.edgeDepth = 0.75f; s.interiorKeep = 0f;
        const float T = 2.5f;
        const float pathX = -1.25f;
        if (stalkPrefabs != null && stalkPrefabs.Length > 0)
        {
            // Front edge of the field (one tile deep), with a gap for the path.
            for (float x = -16.25f; x <= 16.3f; x += T)
                if (Mathf.Abs(x - pathX) > 0.1f)
                    CornPlanter.FillTile(root, stalkPrefabs, new Vector3(x, 0f, 8.25f), T, false, Mathf.Abs(x - (pathX - T)) < 0.1f, true, Mathf.Abs(x - (pathX + T)) < 0.1f, rng, s);
            // Walls of the path going in, and the corn closing it off at the far end.
            for (int r = 1; r <= 2; r++)
            {
                float z = 8.25f + r * T;
                CornPlanter.FillTile(root, stalkPrefabs, new Vector3(pathX - T, 0f, z), T, false, true, false, false, rng, s);
                CornPlanter.FillTile(root, stalkPrefabs, new Vector3(pathX + T, 0f, z), T, false, false, false, true, rng, s);
            }
            CornPlanter.FillTile(root, stalkPrefabs, new Vector3(pathX, 0f, 8.25f + 3 * T), T, false, false, true, false, rng, s);
            // Side clumps framing the view.
            foreach (float x in new[] { -8.75f, -11.25f, 6.25f, 8.75f, 11.25f })
                CornPlanter.FillTile(root, stalkPrefabs, new Vector3(x, 0f, 5.75f), T, false, x < 0f, true, x > 0f, rng, s);
            foreach (var r in root.GetComponentsInChildren<MeshRenderer>())
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        // Corn cards: the rest of the field behind the front edge, out to the horizon.
        if (cardMaterials != null && cardMaterials.Length > 0)
        {
            foreach (float z in new[] { 9.6f, 12.4f, 15.2f, 18.5f, 21.5f, 25f, 29f })
                for (float x = -34f; x < 34f;)
                {
                    float w = Mathf.Round(Mathf.Lerp(2.5f, 5f, (float)rng.NextDouble()) * 2f) * 0.5f;
                    // keep the path open for the first rows so it reads as a way in
                    if (z < 16f && Mathf.Abs(x + w * 0.5f - pathX) < 1.6f + w * 0.5f) { x += 0.5f; continue; }
                    var go = new GameObject("CornCard");
                    go.transform.SetParent(cards, false);
                    go.transform.position = new Vector3(x + w * 0.5f, 0f, z + (float)(rng.NextDouble() - 0.5) * 0.7f);
                    float sc = Mathf.Lerp(0.8f, 1.05f, (float)rng.NextDouble());
                    go.transform.localScale = new Vector3(sc * (rng.NextDouble() < 0.5 ? -1f : 1f), sc, 1f);
                    go.AddComponent<MeshFilter>().sharedMesh = CardMesh(w);
                    var mr = go.AddComponent<MeshRenderer>();
                    mr.sharedMaterial = cardMaterials[rng.Next(cardMaterials.Length)];
                    mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    x += w * Mathf.Lerp(0.55f, 0.75f, (float)rng.NextDouble());
                }
            StaticBatchingUtility.Combine(cards.gameObject); // a handful of draws instead of hundreds
        }
    }

    static readonly System.Collections.Generic.Dictionary<float, Mesh> cardMeshes = new System.Collections.Generic.Dictionary<float, Mesh>();

    static Mesh CardMesh(float w)
    {
        if (cardMeshes.TryGetValue(w, out var cached) && cached != null) return cached;
        const float texW = 5f, texH = 3.8f;
        float u = Mathf.Clamp01(w / texW);
        var m = new Mesh { name = "MenuCornCard" };
        m.vertices = new[] { new Vector3(-w * 0.5f, 0f, 0f), new Vector3(w * 0.5f, 0f, 0f), new Vector3(w * 0.5f, texH, 0f), new Vector3(-w * 0.5f, texH, 0f) };
        m.uv = new[] { new Vector2(0f, 0f), new Vector2(u, 0f), new Vector2(u, 1f), new Vector2(0f, 1f) };
        m.normals = new[] { Vector3.back, Vector3.back, Vector3.back, Vector3.back };
        m.triangles = new[] { 0, 2, 1, 0, 3, 2 };
        m.RecalculateBounds();
        cardMeshes[w] = m;
        return m;
    }

    // The moon: same component as in the game, pointed where the menu wants it, and the moonlight turned to
    // shine from it (so the scarecrow stands silhouetted with a cold rim).
    void AddMoon()
    {
        var light = GameObject.Find("Moonlight");
        var host = light != null ? light : new GameObject("Moon");
        var moon = host.AddComponent<MoonSky>();
        moon.direction = (moonPosition - cameraPosition).normalized;
        moon.alignLight = light != null;
        moon.size = moonSize * 60f / Mathf.Max(1f, (moonPosition - cameraPosition).magnitude);
    }

    // ---------------- The scarecrow ----------------

    void BuildScarecrow()
    {
        if (pumpkinPrefab == null) { Debug.LogWarning("[Menu] no pumpkin prefab assigned"); return; }
        rig = new GameObject("Scarecrow").transform;
        rig.position = scarecrowPosition;
        Vector3 toCam = cameraPosition - scarecrowPosition; toCam.y = 0f;
        rig.rotation = Quaternion.LookRotation(toCam.normalized);

        var p = Instantiate(pumpkinPrefab, rig);
        p.transform.localPosition = Vector3.up * scarecrowHang;
        p.transform.localRotation = Quaternion.identity;
        // It's a prop here: no AI, no IK relay, no animation.
        foreach (var mb in p.GetComponentsInChildren<MonoBehaviour>(true)) Destroy(mb);
        var anim = p.GetComponentInChildren<Animator>();
        Transform B(HumanBodyBones hb, string name)
        {
            Transform t = null;
            if (anim != null && anim.isHuman) t = anim.GetBoneTransform(hb);
            if (t == null) t = FindDeep(p.transform, name);
            return t;
        }
        var hips = B(HumanBodyBones.Hips, "Character1_Hips");
        var spine = B(HumanBodyBones.Spine, "Character1_Spine");
        var chest = B(HumanBodyBones.Chest, "Character1_Spine1");
        var neck = B(HumanBodyBones.Neck, "Character1_Neck");
        head = B(HumanBodyBones.Head, "Character1_Head");
        var lUp = B(HumanBodyBones.LeftUpperArm, "Character1_LeftArm");
        var lLo = B(HumanBodyBones.LeftLowerArm, "Character1_LeftForeArm");
        var lHand = B(HumanBodyBones.LeftHand, "Character1_LeftHand");
        var lFing = B(HumanBodyBones.LeftMiddleProximal, "Character1_LeftHandMiddle1");
        var rUp = B(HumanBodyBones.RightUpperArm, "Character1_RightArm");
        var rLo = B(HumanBodyBones.RightLowerArm, "Character1_RightForeArm");
        var rHand = B(HumanBodyBones.RightHand, "Character1_RightHand");
        var rFing = B(HumanBodyBones.RightMiddleProximal, "Character1_RightHandMiddle1");
        var lThigh = B(HumanBodyBones.LeftUpperLeg, "Character1_LeftUpLeg");
        var lShin = B(HumanBodyBones.LeftLowerLeg, "Character1_LeftLeg");
        var lFoot = B(HumanBodyBones.LeftFoot, "Character1_LeftFoot");
        var rThigh = B(HumanBodyBones.RightUpperLeg, "Character1_RightUpLeg");
        var rShin = B(HumanBodyBones.RightLowerLeg, "Character1_RightLeg");
        var rFoot = B(HumanBodyBones.RightFoot, "Character1_RightFoot");
        if (anim != null) anim.enabled = false; // hold the pose we set below

        Vector3 R = rig.right, U = Vector3.up, F = rig.forward;
        // Face direction of the head before posing (for the "turns to look at you" later).
        if (head != null) headLocalFace = Quaternion.Inverse(head.rotation) * F;

        // Strung up: body hanging, slumped a little forward.
        Aim(hips, spine, U);
        if (spine != null) spine.rotation = Quaternion.AngleAxis(6f, R) * spine.rotation;
        if (chest != null) chest.rotation = Quaternion.AngleAxis(5f, R) * chest.rotation;
        // Arms straight out along the crossbar, forearms sagging, hands hanging limp.
        Aim(lUp, lLo, Dir(-R, -U, 6f)); Aim(lLo, lHand, Dir(-R, -U, 16f)); Aim(lHand, lFing, Dir(-R, -U, 70f));
        Aim(rUp, rLo, Dir(R, -U, 6f)); Aim(rLo, rHand, Dir(R, -U, 16f)); Aim(rHand, rFing, Dir(R, -U, 70f));
        // Legs dangling, slightly apart, toes pointing down.
        Aim(lThigh, lShin, (-U - R * 0.07f).normalized); Aim(lShin, lFoot, -U);
        Aim(rThigh, rShin, (-U + R * 0.07f).normalized); Aim(rShin, rFoot, -U);
        if (lFoot != null) lFoot.rotation = Quaternion.AngleAxis(35f, R) * lFoot.rotation;
        if (rFoot != null) rFoot.rotation = Quaternion.AngleAxis(35f, R) * rFoot.rotation;
        // Head: lolled over to one side and down, like it's been hanging there for weeks.
        Aim(neck, head, Dir(U, -R, 10f));
        if (head != null)
        {
            head.rotation = Quaternion.AngleAxis(24f, F) * Quaternion.AngleAxis(16f, R) * head.rotation;
            headBaseLocal = head.localRotation;
        }

        // The post and crossbar behind it.
        var wood = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        wood.SetColor("_BaseColor", new Color(0.16f, 0.11f, 0.07f));
        wood.SetFloat("_Smoothness", 0.08f);
        float topY = (head != null ? head.position.y : scarecrowPosition.y + 2.4f) + 0.35f;
        float armY = lUp != null ? lUp.position.y : scarecrowPosition.y + 1.9f;
        Cylinder("Post", scarecrowPosition - F * 0.16f + Vector3.up * topY * 0.5f, Vector3.up, topY, 0.075f, wood);
        Cylinder("Crossbar", new Vector3(scarecrowPosition.x, armY + 0.02f, scarecrowPosition.z) - F * 0.1f, R, 2.5f, 0.05f, wood);
        // Rope lashings at the wrists.
        var rope = new Material(wood); rope.SetColor("_BaseColor", new Color(0.32f, 0.26f, 0.16f));
        if (lLo != null) Cylinder("Rope", new Vector3(lLo.position.x, armY + 0.02f, lLo.position.z) - F * 0.1f, R, 0.09f, 0.075f, rope);
        if (rLo != null) Cylinder("Rope", new Vector3(rLo.position.x, armY + 0.02f, rLo.position.z) - F * 0.1f, R, 0.09f, 0.075f, rope);

        // Faint glow from the carved face.
        foreach (var r in p.GetComponentsInChildren<Renderer>(true))
            foreach (var m in r.materials)
                if (m.HasProperty("_EmissionColor") && m.HasProperty("_EmissionMap") && m.GetTexture("_EmissionMap") != null)
                {
                    m.EnableKeyword("_EMISSION");
                    glowMats.Add(m);
                }
        faceLight = new GameObject("ScarecrowFaceGlow").AddComponent<Light>();
        faceLight.type = LightType.Point;
        faceLight.color = new Color(1f, 0.45f, 0.1f);
        faceLight.range = 1.8f;
        faceLight.shadows = LightShadows.None;
    }

    static Vector3 Dir(Vector3 main, Vector3 toward, float degrees) =>
        (main * Mathf.Cos(degrees * Mathf.Deg2Rad) + toward * Mathf.Sin(degrees * Mathf.Deg2Rad)).normalized;

    static void Aim(Transform bone, Transform child, Vector3 dir)
    {
        if (bone == null || child == null) return;
        Vector3 cur = child.position - bone.position;
        if (cur.sqrMagnitude < 1e-6f) return;
        bone.rotation = Quaternion.FromToRotation(cur, dir) * bone.rotation;
    }

    static Transform FindDeep(Transform t, string name)
    {
        if (t.name == name) return t;
        foreach (Transform c in t) { var f = FindDeep(c, name); if (f != null) return f; }
        return null;
    }

    void Cylinder(string name, Vector3 center, Vector3 axis, float length, float radius, Material mat)
    {
        var c = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        c.name = name;
        Destroy(c.GetComponent<Collider>());
        c.transform.SetParent(rig, true);
        c.transform.position = center;
        c.transform.rotation = Quaternion.FromToRotation(Vector3.up, axis);
        c.transform.localScale = new Vector3(radius * 2f, length * 0.5f, radius * 2f);
        c.GetComponent<MeshRenderer>().sharedMaterial = mat;
    }

    // ---------------- Per frame ----------------

    void Update()
    {
        float t = Time.unscaledTime - start;
        float ambFade = GameSettings.Amb * Mathf.Clamp01(t / 3f) * (leaving ? 1f - Mathf.Clamp01((Time.unscaledTime - leaveStart) / 1.2f) : 1f);
        if (amb != null) amb.volume = (ambienceClip == null && amb.clip != null && amb.clip.name == "Music_Menu" ? musicVolume : ambienceVolume) * ambFade;
        if (windSrc != null) windSrc.volume = windVolume * ambFade;

        // Camera: slow handheld drift and a little parallax toward the mouse.
        Vector2 m = Mouse.current != null ? Mouse.current.position.ReadValue() : new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        float mx = Mathf.Clamp(m.x / Mathf.Max(1, Screen.width) - 0.5f, -0.5f, 0.5f);
        float my = Mathf.Clamp(m.y / Mathf.Max(1, Screen.height) - 0.5f, -0.5f, 0.5f);
        float tt = Time.time;
        var drift = Quaternion.Euler(Mathf.Sin(tt * 0.31f) * 0.5f - my * 1.6f, Mathf.Sin(tt * 0.23f + 1f) * 0.7f + mx * 2.4f, Mathf.Sin(tt * 0.17f) * 0.3f);
        cam.transform.rotation = Quaternion.Slerp(cam.transform.rotation, camBaseRot * drift, 1f - Mathf.Exp(-3f * Time.deltaTime));

        // Keyboard navigation.
        var kb = Keyboard.current;
        if (!leaving && kb != null)
        {
            if (howToOpen)
            {
                howTo.Update();
            }
            else if (creditsOpen)
            {
                credits.Update();
            }
            else if (settingsOpen || panel.Calibrating)
            {
                panel.Update();
            }
            else
            {
                if (kb.downArrowKey.wasPressedThisFrame || kb.sKey.wasPressedThisFrame) { selected = (selected + 1) % Labels.Length; Hover(); }
                if (kb.upArrowKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame) { selected = (selected - 1 + Labels.Length) % Labels.Length; Hover(); }
                if ((kb.enterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame) && selected >= 0) Activate((Item)selected);
            }
        }

        if (leaving && Time.unscaledTime - leaveStart > 1.3f)
        {
            if (leaveAction == Item.Play) SceneLoader.Load(gameScene);
            else
            {
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#else
                Application.Quit();
#endif
            }
            if (leaveAction != Item.Play) leaving = false; // PLAY: stay "leaving" (and black) while the maze loads
        }
    }

    void LateUpdate()
    {
        if (rig == null) return;
        float t = Time.time;
        // Creaks in the wind.
        Vector3 toCam = cameraPosition - scarecrowPosition; toCam.y = 0f;
        rig.rotation = Quaternion.LookRotation(toCam.normalized) * Quaternion.Euler(Mathf.Sin(t * 0.5f) * 0.6f, Mathf.Sin(t * 0.37f) * 1.2f, Mathf.Sin(t * 0.61f) * 0.9f);

        // Now and then -- and whenever you hover QUIT -- the head slowly comes up to look at you.
        bool quitHover = !settingsOpen && !howToOpen && !creditsOpen && selected == (int)Item.Quit;
        if (t >= nextLook) { lookUntil = t + Random.Range(1.6f, 2.6f); nextLook = t + Random.Range(9f, 15f); }
        bool looking = quitHover || t < lookUntil;
        lookW = Mathf.MoveTowards(lookW, looking ? 1f : 0f, Time.deltaTime * (looking ? 0.7f : 0.45f));
        if (head != null)
        {
            head.localRotation = headBaseLocal;
            if (lookW > 0.001f)
            {
                Vector3 face = head.rotation * headLocalFace;
                Vector3 toEye = (cam.transform.position - head.position).normalized;
                var lookRot = Quaternion.FromToRotation(face, toEye) * head.rotation;
                float w = lookW * lookW * (3f - 2f * lookW);
                head.rotation = Quaternion.Slerp(head.rotation, lookRot, w);
                if (w > 0.95f) head.rotation = Quaternion.AngleAxis((Mathf.PerlinNoise(t * 14f, 0.2f) - 0.5f) * 2.5f, toEye) * head.rotation; // trembles
            }
        }

        float flick = 0.75f + 0.25f * Mathf.PerlinNoise(t * 7f, 1.3f);
        float glow = (0.9f + 2.2f * lookW) * flick;
        foreach (var mat in glowMats) if (mat != null) mat.SetColor("_EmissionColor", new Color(1f, 0.42f, 0.08f) * glow);
        if (faceLight != null && head != null)
        {
            faceLight.transform.position = head.position + (cam.transform.position - head.position).normalized * 0.4f;
            faceLight.intensity = 0.5f * glow;
        }
    }

    // ---------------- Menu ----------------

    void Activate(Item it)
    {
        Click();
        switch (it)
        {
            case Item.Play:
                if (!GameSettings.BrightnessSet) panel.StartCalibration(); // first time: set brightness before going in
                else { leaving = true; leaveAction = Item.Play; leaveStart = Time.unscaledTime; }
                break;
            case Item.HowTo: howToOpen = true; howToAt = Time.unscaledTime; howTo.Open(); break;
            case Item.Credits: creditsOpen = true; creditsAt = Time.unscaledTime; credits.Open(); break;
            case Item.Settings: settingsOpen = true; panel.ResetSelection(); break;
            case Item.Quit: leaving = true; leaveAction = Item.Quit; leaveStart = Time.unscaledTime; break;
        }
    }

    void Hover()
    {
        if (selected == lastHover) return;
        lastHover = selected;
        if (selected < 0 || sfx == null) return;
        var c = hoverClip != null ? hoverClip : (placeholderHover ??= MakeTick(0.06f, 140f, 0.25f));
        sfx.PlayOneShot(c, 0.6f * GameSettings.Fx);
    }

    void Click()
    {
        if (sfx == null) return;
        var c = clickClip != null ? clickClip : (placeholderClick ??= MakeTick(0.5f, 55f, 0.6f));
        sfx.PlayOneShot(c, 0.9f * GameSettings.Fx);
    }

    static AudioClip placeholderHover, placeholderClick;

    void OnGUI()
    {
        float sw = Screen.width, sh = Screen.height, k = sh / 1080f, t = Time.unscaledTime - start;
        bool paint = Event.current.type == EventType.Repaint; // draw once per frame; other events only handle input
        if (titleStyle == null)
        {
            titleStyle = HorrorUI.Style(HorrorUI.TitleFont, TextAnchor.MiddleLeft);
            smallStyle = HorrorUI.Style(HorrorUI.SerifFont, TextAnchor.MiddleLeft);
            itemStyle = HorrorUI.Style(HorrorUI.SerifFont, TextAnchor.MiddleLeft);
            panelTitle = HorrorUI.Style(HorrorUI.SerifFont);
            panelText = HorrorUI.Style(HorrorUI.TypeFont);
        }

        bool panelUp = settingsOpen || howToOpen || creditsOpen || panel.Calibrating;
        if (paint && !panelUp) DrawTitle(sw, sh, k, t);

        // ---- Buttons ----
        float x0 = 110f * k;
        if (!panelUp)
        {
            itemStyle.fontSize = HorrorUI.Px(54);
            float by = 560f * k, gap = 80f * k;
            Vector2 mouse = Event.current.mousePosition;
            int hover = -1;
            for (int i = 0; i < Labels.Length; i++)
            {
                float a = Mathf.Clamp01((t - 1.4f - i * 0.18f) / 0.8f);
                var size = itemStyle.CalcSize(new GUIContent(Labels[i]));
                var hit = new Rect(x0 - 30f * k, by + i * gap - 8f * k, size.x + 90f * k, 72f * k);
                if (!leaving && a > 0.5f && hit.Contains(mouse)) hover = i;
                if (!paint) continue;
                bool sel = selected == i;
                float push = sel ? 18f * k : 0f;
                var r = new Rect(x0 + push, by + i * gap, size.x + 40f * k, 60f * k);
                if (sel)
                {
                    float flick = 0.75f + 0.25f * Mathf.PerlinNoise(Time.time * 9f, i);
                    HorrorUI.Glow(new Rect(x0 - 22f * k - 16f * k, r.center.y - 16f * k, 32f * k, 32f * k), HorrorUI.A(HorrorUI.Ember, a * flick));
                    HorrorUI.Glow(new Rect(x0 - 26f * k - 30f * k, r.center.y - 30f * k, 60f * k, 60f * k), HorrorUI.A(HorrorUI.Ember, 0.35f * a * flick));
                    HorrorUI.Fill(new Rect(x0 + push, r.yMax - 4f * k, size.x * (0.6f + 0.4f * Mathf.PingPong(Time.time * 0.8f, 1f)), Mathf.Max(1f, 2f * k)), HorrorUI.A(HorrorUI.EmberDim, a * 0.9f));
                    DrawLeftShaky(r, Labels[i], itemStyle, HorrorUI.A(HorrorUI.Ember, a), Time.time, 1.2f * k, 3f * k, 1.5f * k, 10 + i);
                }
                else
                    HorrorUI.Text(r, Labels[i], itemStyle, HorrorUI.A(HorrorUI.Bone, a * 0.8f), 0f, 2f * k);
            }
            if (hover >= 0 && hover != selected) { selected = hover; Hover(); }
            if (!leaving && Event.current.type == EventType.MouseDown && Event.current.button == 0 && hover >= 0)
            {
                Activate((Item)hover);
                Event.current.Use();
            }
        }
        else if (howToOpen)
        {
            howTo.OnGUI(Mathf.Clamp01((Time.unscaledTime - howToAt) / 0.35f));
        }
        else if (creditsOpen)
        {
            credits.OnGUI(Mathf.Clamp01((Time.unscaledTime - creditsAt) / 0.35f));
        }
        else
        {
            if (paint && !panel.Calibrating)
            {
                // Darken the left side more so the settings read over the corn.
                if (leftShade == null) leftShade = MakeLeftShade();
                GUI.color = Color.white;
                GUI.DrawTexture(new Rect(0, 0, sw * 0.75f, sh), leftShade, ScaleMode.StretchToFill, true);
                GUI.DrawTexture(new Rect(0, 0, sw * 0.75f, sh), leftShade, ScaleMode.StretchToFill, true);
            }
            panel.OnGUI(x0 + 20f * k, 40f * k, Mathf.Clamp01(t / 0.5f));
        }

        // Fade in at the start, fade to black when leaving.
        if (paint)
        {
            float black = 1f - HorrorUI.Smooth(t / 2.2f);
            if (leaving) black = Mathf.Max(black, HorrorUI.Smooth((Time.unscaledTime - leaveStart) / 1.1f));
            HorrorUI.Fill(new Rect(0, 0, sw, sh), new Color(0f, 0f, 0f, black));
        }
    }

    void DrawTitle(float sw, float sh, float k, float t)
    {
        // Shade the left side so the title and buttons read over the corn.
        if (leftShade == null) leftShade = MakeLeftShade();
        GUI.color = new Color(1f, 1f, 1f, 0.9f);
        GUI.DrawTexture(new Rect(0, 0, sw * 0.62f, sh), leftShade, ScaleMode.StretchToFill, true);
        GUI.color = Color.white;

        // ---- Title ----
        float x0 = 110f * k, y0 = 70f * k;
        float tIn = Mathf.Clamp01((t - 0.6f) / 1.6f);
        HorrorUI.Glow(new Rect(x0 - 140f * k, y0 - 90f * k, 900f * k, 560f * k), HorrorUI.A(HorrorUI.Blood, 0.28f * tIn));
        titleStyle.fontSize = HorrorUI.Px(138);
        DrawLeftShaky(new Rect(x0, y0, 0, 150f * k), "CREEPERS", titleStyle, HorrorUI.A(HorrorUI.Ember, tIn), t, 1.5f * k, 6f * k, 2f * k, 1);
        smallStyle.fontSize = HorrorUI.Px(46);
        HorrorUI.Text(new Rect(x0 + 190f * k, y0 + 132f * k, 400f * k, 56f * k), "in the", smallStyle, HorrorUI.A(HorrorUI.Bone, 0.85f * tIn), 0f, 2f * k);
        titleStyle.fontSize = HorrorUI.Px(176);
        DrawLeftShaky(new Rect(x0 + 40f * k, y0 + 168f * k, 0, 190f * k), "CORN", titleStyle, HorrorUI.A(HorrorUI.Ember, tIn), t, 2f * k, 10f * k, 2.5f * k, 2);

    }

    // Shaky title drawn left-aligned starting at r.x.
    static void DrawLeftShaky(Rect r, string s, GUIStyle st, Color c, float t, float jitter, float spacing, float chroma, int seed)
    {
        float w = st.CalcSize(new GUIContent(s)).x + spacing * (s.Length - 1);
        HorrorUI.Shaky(new Rect(r.x, r.y, w, r.height), s, st, c, t, jitter, spacing, chroma, 2f, 0.08f, seed);
    }

    static Texture2D MakeLeftShade()
    {
        const int W = 256;
        var tex = new Texture2D(W, 1, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
        for (int i = 0; i < W; i++)
        {
            float u = i / (float)(W - 1);
            tex.SetPixel(i, 0, new Color(0f, 0f, 0f, 0.8f * (1f - Mathf.SmoothStep(0.25f, 1f, u))));
        }
        tex.Apply(false, false);
        return tex;
    }

    // ---------------- Placeholder sounds ----------------

    static AudioClip MakeTick(float seconds, float freq, float noise)
    {
        const int sr = 44100;
        int n = Mathf.RoundToInt(sr * seconds);
        var d = new float[n];
        var rng = new System.Random(5);
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)sr;
            float env = Mathf.Exp(-t * (6f / seconds));
            d[i] = (Mathf.Sin(2f * Mathf.PI * freq * t) * 0.6f + (float)(rng.NextDouble() * 2 - 1) * noise) * env * 0.5f;
        }
        var c = AudioClip.Create("MenuTick", n, 1, sr, false);
        c.SetData(d, 0);
        return c;
    }

    static AudioClip MakeWind()
    {
        const int sr = 22050;
        int n = sr * 8;
        var d = new float[n];
        var rng = new System.Random(77);
        float lp = 0f, lp2 = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)sr;
            float gust = 0.5f + 0.5f * Mathf.Sin(2f * Mathf.PI * t / 8f) * Mathf.Sin(2f * Mathf.PI * t / 2.67f + 1f);
            lp += ((float)(rng.NextDouble() * 2 - 1) - lp) * (0.02f + 0.03f * gust);
            lp2 += (lp - lp2) * 0.1f;
            d[i] = lp2 * (2.5f + 3f * gust);
        }
        // loop seam: fade the first/last 0.3 s into each other
        int f = sr * 3 / 10;
        for (int i = 0; i < f; i++) { float a = i / (float)f; d[i] = d[i] * a + d[n - f + i] * (1f - a); }
        var c = AudioClip.Create("MenuWind", n - f, 1, sr, false);
        var trimmed = new float[n - f]; System.Array.Copy(d, trimmed, n - f);
        c.SetData(trimmed, 0);
        return c;
    }
}
