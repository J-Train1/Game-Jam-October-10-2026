using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Key counter: a row of little skeleton-key icons above the BREATH line (bottom-left), one per key.
//   still out there -> dark silhouette
//   collected       -> tarnished brass with a slow glint
//   used on a lock  -> dull rust
// Icons are drawn in code. Built under the FlashlightHUD object so it hides with the rest of the HUD.
public class KeyHUD : MonoBehaviour
{
    [Header("Layout (reference resolution 1920x1080)")]
    [Tooltip("Bottom-left of the row. Default sits just above the BREATH label.")]
    public Vector2 rowPosition = new Vector2(40f, 158f);
    public Vector2 keySize = new Vector2(58f, 29f);
    public float keySpacing = 6f;

    [Header("Colors")]
    public Color missingColor = new Color(0.05f, 0.04f, 0.03f, 0.75f);
    public Color heldColor = new Color(0.86f, 0.66f, 0.32f, 1f);
    public Color spentColor = new Color(0.45f, 0.22f, 0.10f, 0.7f);

    readonly List<Image> icons = new List<Image>();
    readonly List<Image> outlines = new List<Image>();
    readonly List<RectTransform> slots = new List<RectTransform>();
    Transform canvas;
    Text countText;
    int built = -1, lastHave = -1;
    float popUntil; int popIndex = -1;
    static Sprite keySprite, keyOutlineSprite;

    void Start()
    {
        if (FindFirstObjectByType<Narrator>() == null) gameObject.AddComponent<Narrator>();
    }

    void Update()
    {
        var km = KeyManager.Instance;
        if (km == null) return;
        if (canvas == null || built != km.Total) Build(km.Total);
        if (canvas == null) return;

        int have = km.Collected;
        int used = ExitGate.Instance != null ? ExitGate.Instance.Removed : 0;
        if (have != lastHave)
        {
            if (lastHave >= 0 && have > lastHave) { popIndex = have - 1; popUntil = Time.time + 0.6f; }
            lastHave = have;
        }
        float t = Time.time;
        for (int i = 0; i < icons.Count; i++)
        {
            Color c;
            if (i < used) c = spentColor;
            else if (i < have)
            {
                c = heldColor;
                // slow glint travelling along the row
                float g = Mathf.Exp(-Mathf.Pow(Mathf.Repeat(t * 0.35f - i * 0.12f, 1.6f) - 0.3f, 2f) / 0.006f);
                c = Color.Lerp(c, new Color(1f, 0.95f, 0.8f, 1f), g * 0.6f);
            }
            else c = missingColor;

            float pop = (i == popIndex && t < popUntil) ? (popUntil - t) / 0.6f : 0f;
            if (pop > 0f) c = Color.Lerp(c, new Color(1f, 0.85f, 0.5f, 1f), pop);
            icons[i].color = c;
            outlines[i].color = i < have ? new Color(0f, 0f, 0f, 0.8f) : HorrorUI.A(HorrorUI.Bone, 0.22f);
            slots[i].localScale = Vector3.one * (1f + 0.6f * pop * pop);
        }
        countText.text = $"{have} / {km.Total}";
        countText.color = HorrorUI.A(have >= km.Total ? HorrorUI.Brass : HorrorUI.Bone, have > 0 ? 0.85f : 0.45f);
    }

    void Build(int total)
    {
        var hud = FindFirstObjectByType<FlashlightHUD>();
        if (hud == null) return; // wait until the main HUD exists
        if (canvas != null) Destroy(canvas.gameObject);
        icons.Clear(); outlines.Clear(); slots.Clear();
        EnsureSprites();

        var go = new GameObject("KeyHUDCanvas", typeof(Canvas), typeof(CanvasScaler));
        go.transform.SetParent(hud.transform, false);
        var c = go.GetComponent<Canvas>();
        c.renderMode = RenderMode.ScreenSpaceOverlay;
        c.sortingOrder = 11;
        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        canvas = go.transform;

        float step = keySize.x * 0.8f + keySpacing;
        for (int i = 0; i < total; i++)
        {
            var slot = new GameObject("Key" + (i + 1), typeof(RectTransform)).GetComponent<RectTransform>();
            slot.SetParent(canvas, false);
            slot.anchorMin = slot.anchorMax = Vector2.zero;
            slot.pivot = new Vector2(0.15f, 0.5f);
            slot.sizeDelta = keySize;
            slot.anchoredPosition = rowPosition + new Vector2(i * step + keySize.x * 0.15f, keySize.y * 0.5f);
            // each key lies at a slightly different careless angle
            slot.localRotation = Quaternion.Euler(0f, 0f, -12f + (Mathf.PerlinNoise(i * 2.3f, 0.7f) - 0.5f) * 14f);

            var outline = Img("Outline", slot, keyOutlineSprite);
            var icon = Img("Icon", slot, keySprite);
            slots.Add(slot);
            outlines.Add(outline);
            icons.Add(icon);
        }
        float textX = rowPosition.x + (total - 1) * step + keySize.x * 0.95f + 8f;
        countText = FlashlightHUD.MakeText("KeyCount", canvas, "", 20, TextAnchor.MiddleLeft,
            new Vector2(textX, rowPosition.y + keySize.y * 0.1f), new Vector2(120f, 30f));
        built = total;
        lastHave = -1;
    }

    static Image Img(string name, Transform parent, Sprite s)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        var img = go.GetComponent<Image>();
        img.sprite = s;
        img.raycastTarget = false;
        return img;
    }

    // ---------------- procedural skeleton key ----------------
    // Key space: x 0..2 (bow on the left, bit on the right), y -0.5..0.5.

    static float KeyShape(float x, float y, float grow)
    {
        // bow: ring with a little trefoil bulge
        float bx = x - 0.32f, by = y;
        float r = Mathf.Sqrt(bx * bx + by * by);
        float ang = Mathf.Atan2(by, bx);
        float outerR = 0.28f - 0.055f * Mathf.Cos(ang * 3f) + grow;
        float innerR = 0.13f - grow;
        bool bow = r <= outerR && r >= innerR;
        // collar + shaft
        bool collar = x >= 0.6f - grow && x <= 0.7f + grow && Mathf.Abs(y) <= 0.11f + grow;
        bool shaft = x >= 0.58f && x <= 1.86f + grow && Mathf.Abs(y) <= 0.055f + grow;
        // bit: two teeth hanging down
        bool tooth1 = x >= 1.5f - grow && x <= 1.62f + grow && y <= 0f && y >= -0.3f - grow;
        bool tooth2 = x >= 1.7f - grow && x <= 1.86f + grow && y <= 0f && y >= -0.22f - grow;
        bool notch = x >= 1.53f && x <= 1.58f && y <= -0.17f && y >= -0.24f; // little cut in the first tooth
        return (bow || collar || shaft || ((tooth1 && !notch) || tooth2)) ? 1f : 0f;
    }

    static void EnsureSprites()
    {
        if (keySprite == null) keySprite = MakeKeySprite(0f, true);
        if (keyOutlineSprite == null) keyOutlineSprite = MakeKeySprite(0.035f, false);
    }

    static Sprite MakeKeySprite(float grow, bool shaded)
    {
        const int W = 160, H = 80, SS = 3;
        var tex = new Texture2D(W, H, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
        var px = new Color[W * H];
        for (int j = 0; j < H; j++)
        for (int i = 0; i < W; i++)
        {
            float cover = 0f;
            for (int sj = 0; sj < SS; sj++)
            for (int si = 0; si < SS; si++)
            {
                float x = (i + (si + 0.5f) / SS) / W * 2f;
                float y = (j + (sj + 0.5f) / SS) / H - 0.5f;
                cover += KeyShape(x, y, grow);
            }
            cover /= SS * SS;
            float v = 1f;
            if (shaded)
            {
                // lighter top edge, darker underside, pitted with a little noise = old metal
                float yy = (j + 0.5f) / H - 0.5f;
                v = Mathf.Clamp01(0.78f + yy * 0.6f) * Mathf.Lerp(0.8f, 1f, Mathf.PerlinNoise(i * 0.35f, j * 0.35f));
            }
            px[j * W + i] = new Color(v, v, v, cover);
        }
        tex.SetPixels(px);
        tex.Apply(false, false);
        var s = Sprite.Create(tex, new Rect(0, 0, W, H), new Vector2(0.5f, 0.5f), 100f);
        s.hideFlags = HideFlags.HideAndDontSave;
        return s;
    }
}
