using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Player HUD, bottom-left, built entirely in code. Sits on a soft dark smudge so it reads as grime rather than boxes.
//
//   BREATH                 <- typewriter label (+ "winded" when exhausted)
//   ____________           <- thin worn stamina line
//   LIGHT        dying     <- typewriter label + state word (dying / recharging / dead)
//   [#][#][#][#][#][ ]>    <- old battery cells, amber, flickering like a weak bulb
//
// Battery: amber cells in use, the last cells blink blood-red when low, pale cold pulse while recharging,
//          dark red + "dead" while locked out after running dry.
// Stamina: bone-white line, blood-red + "winded" when exhausted, fades back when full.
public class FlashlightHUD : MonoBehaviour
{
    [Header("Layout (reference resolution 1920x1080)")]
    public Vector2 margin = new Vector2(44f, 40f);
    public int cells = 6;
    public Vector2 cellSize = new Vector2(24f, 30f);
    public float cellGap = 5f;
    public float breathLineHeight = 5f;

    [Header("Battery colors")]
    public Color cellColor = new Color(0.98f, 0.62f, 0.22f, 0.95f);
    public Color cellLowColor = new Color(0.86f, 0.07f, 0.05f, 0.95f);
    public Color cellChargingColor = new Color(0.62f, 0.76f, 0.86f, 0.8f);
    public Color cellDeadColor = new Color(0.45f, 0.03f, 0.03f, 0.9f);

    [Header("Stamina colors")]
    public Color breathColor = new Color(0.87f, 0.82f, 0.70f, 0.95f);
    public Color breathOutColor = new Color(0.82f, 0.08f, 0.05f, 0.95f);

    readonly List<RectTransform> cellFills = new List<RectTransform>();
    readonly List<Image> cellImages = new List<Image>();
    RectTransform staminaFill;
    Image staminaFillImage;
    CanvasGroup batteryGroup, staminaGroup;
    Text lightLabel, lightState, breathLabel, breathState;

    public static Font HudFont => HorrorUI.TypeFont;

    void Start() => Build();

    void Build()
    {
        var canvasGo = new GameObject("HUDCanvas", typeof(Canvas), typeof(CanvasScaler));
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        var root = canvasGo.transform;

        // Grimy smudge behind the whole cluster, bleeding out of the corner.
        var smudge = MakeRect("Smudge", root, new Color(0f, 0f, 0f, 0.62f));
        smudge.GetComponent<Image>().sprite = HorrorUI.BlobSprite;
        smudge.anchorMin = smudge.anchorMax = Vector2.zero;
        smudge.pivot = new Vector2(0.5f, 0.5f);
        smudge.sizeDelta = new Vector2(620f, 440f);
        smudge.anchoredPosition = new Vector2(70f, 60f);

        float rowW = cells * cellSize.x + (cells - 1) * cellGap;

        // ---- Battery cells ----
        var battery = MakeGroup("Battery", root, out batteryGroup);
        for (int i = 0; i < cells; i++)
        {
            var frame = MakeRect("Cell" + (i + 1), battery, new Color(0.87f, 0.82f, 0.70f, 0.28f));
            frame.anchorMin = frame.anchorMax = frame.pivot = Vector2.zero;
            frame.sizeDelta = cellSize;
            frame.anchoredPosition = margin + new Vector2(i * (cellSize.x + cellGap), 0f);
            frame.localRotation = Quaternion.Euler(0f, 0f, (Mathf.PerlinNoise(i * 1.7f, 3.1f) - 0.5f) * 3f); // hand-placed

            var inner = MakeRect("Inner", frame, new Color(0.02f, 0.015f, 0.01f, 0.85f));
            Stretch(inner, 2f);
            var fill = MakeRect("Fill", inner, cellColor);
            Stretch(fill, 2f);
            cellFills.Add(fill);
            cellImages.Add(fill.GetComponent<Image>());
        }
        var nub = MakeRect("Nub", battery, new Color(0.87f, 0.82f, 0.70f, 0.28f));
        nub.anchorMin = nub.anchorMax = nub.pivot = Vector2.zero;
        nub.sizeDelta = new Vector2(6f, cellSize.y * 0.45f);
        nub.anchoredPosition = margin + new Vector2(rowW + 3f, cellSize.y * 0.275f);

        float labelY = margin.y + cellSize.y + 4f;
        lightLabel = MakeText("LightLabel", battery, "LIGHT", 22, TextAnchor.LowerLeft, new Vector2(margin.x, labelY), new Vector2(rowW, 28f));
        lightState = MakeText("LightState", battery, "", 18, TextAnchor.LowerRight, new Vector2(margin.x, labelY), new Vector2(rowW + 10f, 28f));

        // ---- Breath (stamina), above ----
        float sy = labelY + 36f;
        var stamina = MakeGroup("Stamina", root, out staminaGroup);
        var track = MakeRect("Track", stamina, new Color(0.87f, 0.82f, 0.70f, 0.16f));
        track.anchorMin = track.anchorMax = track.pivot = Vector2.zero;
        track.sizeDelta = new Vector2(rowW, breathLineHeight);
        track.anchoredPosition = new Vector2(margin.x, sy);
        staminaFill = MakeRect("Fill", track, breathColor);
        staminaFill.anchorMin = Vector2.zero; staminaFill.anchorMax = Vector2.one;
        staminaFill.offsetMin = staminaFill.offsetMax = Vector2.zero;
        staminaFillImage = staminaFill.GetComponent<Image>();
        breathLabel = MakeText("BreathLabel", stamina, "BREATH", 18, TextAnchor.LowerLeft, new Vector2(margin.x, sy + breathLineHeight + 3f), new Vector2(rowW, 24f));
        breathState = MakeText("BreathState", stamina, "", 16, TextAnchor.LowerRight, new Vector2(margin.x, sy + breathLineHeight + 3f), new Vector2(rowW + 10f, 24f));
    }

    // ---------------- builders ----------------

    static Transform MakeGroup(string name, Transform parent, out CanvasGroup group)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasGroup));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        group = go.GetComponent<CanvasGroup>();
        group.interactable = false; group.blocksRaycasts = false;
        return go.transform;
    }

    static void Stretch(RectTransform r, float inset)
    {
        r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
        r.offsetMin = new Vector2(inset, inset); r.offsetMax = new Vector2(-inset, -inset);
    }

    static RectTransform MakeRect(string name, Transform parent, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        return go.GetComponent<RectTransform>();
    }

    public static Text MakeText(string name, Transform parent, string text, int size, TextAnchor align, Vector2 pos, Vector2 box)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Text), typeof(Shadow));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = Vector2.zero;
        rt.anchoredPosition = pos;
        rt.sizeDelta = box;
        var t = go.GetComponent<Text>();
        t.font = HudFont;
        t.fontSize = size;
        t.alignment = align;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.raycastTarget = false;
        t.color = HorrorUI.A(HorrorUI.Bone, 0.85f);
        t.text = text;
        var sh = go.GetComponent<Shadow>();
        sh.effectColor = new Color(0f, 0f, 0f, 0.9f);
        sh.effectDistance = new Vector2(2f, -2f);
        return t;
    }

    // ---------------- update ----------------

    void Update()
    {
        if (cellFills.Count == 0) return;
        UpdateBattery();
        UpdateStamina();
    }

    void UpdateBattery()
    {
        var fl = Flashlight.Instance;
        if (fl == null) { batteryGroup.alpha = 0f; return; }
        float b = Mathf.Clamp01(fl.Battery01);
        float t = Time.time;

        // Weak-bulb shimmer while it's on.
        float shimmer = fl.IsOn ? Mathf.Lerp(0.82f, 1f, Mathf.PerlinNoise(t * 9f, 0.3f)) : 1f;

        Color baseC; string state = ""; Color stateC = HorrorUI.Bone;
        if (fl.IsLockedOut) { baseC = cellDeadColor; state = "dead"; stateC = HorrorUI.BloodBright; }
        else if (fl.IsRecharging)
        {
            baseC = cellChargingColor;
            baseC.a *= Mathf.Lerp(0.5f, 1f, Mathf.PingPong(t * 1.4f, 1f));
            state = "recharging"; stateC = HorrorUI.Cold;
        }
        else if (fl.IsOn && fl.IsLow) { baseC = cellLowColor; state = "dying"; stateC = HorrorUI.BloodBright; }
        else baseC = cellColor;

        bool blink = fl.IsOn && fl.IsLow && Mathf.Repeat(t, 0.8f) > 0.55f;
        for (int i = 0; i < cellFills.Count; i++)
        {
            float f = Mathf.Clamp01(b * cells - i);
            cellFills[i].anchorMax = new Vector2(f, 1f);
            var c = baseC;
            c.r *= shimmer; c.g *= shimmer; c.b *= shimmer;
            if (blink) c.a *= 0.3f;
            // the very last sliver of a cell looks dimmer, like it's draining
            if (f < 1f) c.a *= Mathf.Lerp(0.55f, 1f, f);
            cellImages[i].color = c;
        }

        lightState.text = state;
        float sa = state == "dying" ? (Mathf.Repeat(t, 0.8f) > 0.55f ? 0.35f : 1f) : 0.9f;
        lightState.color = HorrorUI.A(stateC, sa);
        lightLabel.color = HorrorUI.A(HorrorUI.Bone, fl.IsOn ? 0.85f : 0.6f);
        batteryGroup.alpha = fl.IsOn ? 1f : 0.8f;
    }

    void UpdateStamina()
    {
        var pc = PlayerController.Instance;
        if (pc == null) { staminaGroup.alpha = 0f; return; }
        float s = Mathf.Clamp01(pc.Stamina01);
        staminaFill.anchorMax = new Vector2(s, 1f);

        Color c;
        float targetAlpha;
        if (pc.IsExhausted)
        {
            c = breathOutColor;
            if (Mathf.Repeat(Time.time, 0.7f) > 0.45f) c.a *= 0.45f; // slow blink while winded
            breathState.text = "winded";
            breathState.color = HorrorUI.A(HorrorUI.BloodBright, c.a);
            targetAlpha = 1f;
        }
        else
        {
            c = breathColor;
            if (!pc.IsSprinting) c.a *= 0.7f;
            breathState.text = "";
            targetAlpha = (s >= 0.999f && !pc.IsSprinting) ? 0.3f : 1f; // fade back when full
        }
        staminaFillImage.color = c;
        staminaGroup.alpha = Mathf.MoveTowards(staminaGroup.alpha, targetAlpha, Time.deltaTime * 2.5f);
    }
}
