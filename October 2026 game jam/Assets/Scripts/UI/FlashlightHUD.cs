using UnityEngine;
using UnityEngine.UI;

// Player HUD, bottom-left. Built entirely in code so there's nothing to set up.
//
//   [=========]        <- stamina bar (thin)
//   [=================]>  <- flashlight battery (big, with terminal nub)
//
// Battery: warm white in use, orange + blinking when low, pulsing blue while recharging,
//          red while locked out after running dry.
// Stamina: bright while sprinting, softer while recovering, faded when full,
//          red while exhausted.
public class FlashlightHUD : MonoBehaviour
{
    [Header("Layout (reference resolution 1920x1080)")]
    public Vector2 margin = new Vector2(40f, 40f);
    public Vector2 batterySize = new Vector2(150f, 52f);
    public float staminaHeight = 16f;
    public float gap = 12f;

    [Header("Battery colors")]
    public Color okColor = new Color(1f, 0.92f, 0.78f, 0.9f);
    public Color lowColor = new Color(1f, 0.45f, 0.15f, 0.95f);
    public Color chargingColor = new Color(0.55f, 0.75f, 1f, 0.85f);
    public Color lockedColor = new Color(0.75f, 0.12f, 0.08f, 0.95f);

    [Header("Stamina colors")]
    public Color staminaColor = new Color(0.95f, 0.95f, 0.9f, 0.95f);
    public Color exhaustedColor = new Color(0.8f, 0.15f, 0.1f, 0.95f);

    RectTransform batteryFill, staminaFill;
    Image batteryFillImage, staminaFillImage;
    CanvasGroup batteryGroup, staminaGroup;

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

        // ---- Flashlight battery ----
        var battery = MakeBar("Battery", canvasGo.transform, margin, batterySize, 4f, out batteryFill, out batteryFillImage, out batteryGroup);
        var nub = MakeRect("Nub", battery, new Color(1f, 1f, 1f, 0.35f));
        nub.anchorMin = new Vector2(1f, 0.3f); nub.anchorMax = new Vector2(1f, 0.7f);
        nub.pivot = new Vector2(0f, 0.5f);
        nub.anchoredPosition = Vector2.zero;
        nub.sizeDelta = new Vector2(10f, 0f);

        // ---- Stamina, directly above, same width ----
        var staminaPos = margin + new Vector2(0f, batterySize.y + gap);
        MakeBar("Stamina", canvasGo.transform, staminaPos, new Vector2(batterySize.x, staminaHeight), 3f, out staminaFill, out staminaFillImage, out staminaGroup);
    }

    // Outline + dark inside + fill whose width is driven by anchorMax.x.
    RectTransform MakeBar(string name, Transform parent, Vector2 pos, Vector2 size, float border,
        out RectTransform fill, out Image fillImage, out CanvasGroup group)
    {
        var body = MakeRect(name, parent, new Color(1f, 1f, 1f, 0.35f));
        body.anchorMin = body.anchorMax = body.pivot = Vector2.zero;
        body.anchoredPosition = pos;
        body.sizeDelta = size;
        group = body.gameObject.AddComponent<CanvasGroup>();
        group.interactable = false; group.blocksRaycasts = false;

        var inner = MakeRect("Inner", body, new Color(0f, 0f, 0f, 0.65f));
        inner.anchorMin = Vector2.zero; inner.anchorMax = Vector2.one;
        inner.offsetMin = new Vector2(border, border); inner.offsetMax = new Vector2(-border, -border);

        fill = MakeRect("Fill", inner, Color.white);
        fill.anchorMin = Vector2.zero; fill.anchorMax = Vector2.one;
        float pad = Mathf.Max(1f, border * 0.6f);
        fill.offsetMin = new Vector2(pad, pad); fill.offsetMax = new Vector2(-pad, -pad);
        fillImage = fill.GetComponent<Image>();
        return body;
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

    void Update()
    {
        if (batteryFill == null) return;
        UpdateBattery();
        UpdateStamina();
    }

    void UpdateBattery()
    {
        var fl = Flashlight.Instance;
        if (fl == null) { batteryGroup.alpha = 0f; return; }
        float b = fl.Battery01;
        batteryFill.anchorMax = new Vector2(Mathf.Clamp01(b), 1f);

        Color c;
        if (fl.IsLockedOut)
            c = lockedColor;
        else if (fl.IsOn)
        {
            c = fl.IsLow ? lowColor : okColor;
            if (fl.IsLow && Mathf.Repeat(Time.time, 0.9f) > 0.6f) c.a *= 0.35f; // blink when low
        }
        else if (fl.IsRecharging)
        {
            c = chargingColor;
            c.a *= Mathf.Lerp(0.55f, 1f, Mathf.PingPong(Time.time * 1.5f, 1f)); // gentle pulse
        }
        else
            c = okColor; // off and full

        batteryFillImage.color = c;
        batteryGroup.alpha = fl.IsOn ? 1f : 0.75f;
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
            c = exhaustedColor;
            if (Mathf.Repeat(Time.time, 0.7f) > 0.45f) c.a *= 0.45f; // slow blink while winded
            targetAlpha = 1f;
        }
        else
        {
            c = staminaColor;
            if (!pc.IsSprinting) c.a *= 0.7f;                         // softer while recovering
            targetAlpha = (s >= 0.999f && !pc.IsSprinting) ? 0.4f : 1f; // fade back when full
        }
        staminaFillImage.color = c;
        staminaGroup.alpha = Mathf.MoveTowards(staminaGroup.alpha, targetAlpha, Time.deltaTime * 2.5f);
    }
}
