using System.Collections.Generic;
using UnityEngine;

// The square "ACHIEVEMENT UNLOCKED" card that drops in at the top-middle of the screen with a chime.
// Several in a row queue up and show one after another. Survives scene changes; works while paused.
public class AchievementToast : MonoBehaviour
{
    const float SlideIn = 0.35f, Hold = 3.6f, SlideOut = 0.4f, Size = 270f, Top = 26f;

    static AchievementToast instance;
    readonly Queue<Achievements.Def> queue = new Queue<Achievements.Def>();
    Achievements.Def current;
    float shownAt;
    AudioSource src;
    GUIStyle labelStyle, nameStyle;
    static readonly Dictionary<string, Texture2D> icons = new Dictionary<string, Texture2D>();

    public static void Show(Achievements.Def def)
    {
        if (instance == null)
        {
            var go = new GameObject("AchievementToast");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<AchievementToast>();
        }
        instance.queue.Enqueue(def);
    }

    public static Texture2D Icon(string path)
    {
        if (string.IsNullOrEmpty(path)) return null;
        if (!icons.TryGetValue(path, out var t)) { t = Resources.Load<Texture2D>(path); icons[path] = t; }
        return t;
    }

    void Awake()
    {
        src = gameObject.AddComponent<AudioSource>();
        src.playOnAwake = false;
        src.spatialBlend = 0f;
        src.ignoreListenerPause = true;
    }

    void Update()
    {
        float now = Time.unscaledTime;
        if (current != null && now - shownAt > SlideIn + Hold + SlideOut) current = null;
        if (current == null && queue.Count > 0)
        {
            current = queue.Dequeue();
            shownAt = now;
            var clip = GameAudio.Get("Achievement");
            if (clip != null) src.PlayOneShot(clip, 0.85f * GameSettings.Fx);
        }
    }

    void OnGUI()
    {
        if (current == null) return;
        GUI.depth = -150000; // over everything, even the loading black
        if (Event.current.type != EventType.Repaint) return;

        float now = Time.unscaledTime, t = now - shownAt;
        float k = Screen.height / 1080f;
        float s = Size * k;
        // Drop in with a little overshoot, hang there, then pull back up.
        float drop;
        if (t < SlideIn) { float u = t / SlideIn; drop = 1f - Mathf.Pow(1f - u, 3f) + Mathf.Sin(u * Mathf.PI) * 0.08f; }
        else if (t < SlideIn + Hold) drop = 1f;
        else drop = 1f - HorrorUI.Smooth((t - SlideIn - Hold) / SlideOut);
        float y = Mathf.Lerp(-s - 20f * k, Top * k, drop);
        var card = new Rect((Screen.width - s) * 0.5f, y, s, s);
        float a = Mathf.Clamp01(drop * 1.5f);

        // Card: dark, ember-edged, a breathing glow behind.
        float breathe = 0.8f + 0.2f * Mathf.Sin(now * 3f);
        HorrorUI.Glow(new Rect(card.x - 50f * k, card.y - 50f * k, card.width + 100f * k, card.height + 100f * k), HorrorUI.A(HorrorUI.Ember, 0.22f * a * breathe));
        HorrorUI.Fill(card, HorrorUI.A(new Color(0.05f, 0.035f, 0.025f), 0.94f * a));
        float bw = Mathf.Max(1f, 2f * k);
        var bc = HorrorUI.A(HorrorUI.Ember, 0.9f * a);
        HorrorUI.Fill(new Rect(card.x, card.y, card.width, bw), bc);
        HorrorUI.Fill(new Rect(card.x, card.yMax - bw, card.width, bw), bc);
        HorrorUI.Fill(new Rect(card.x, card.y, bw, card.height), bc);
        HorrorUI.Fill(new Rect(card.xMax - bw, card.y, bw, card.height), bc);
        var inner = new Rect(card.x + 6f * k, card.y + 6f * k, card.width - 12f * k, card.height - 12f * k);
        float ib = Mathf.Max(1f, 1f * k);
        var ic = HorrorUI.A(HorrorUI.Rust, 0.8f * a);
        HorrorUI.Fill(new Rect(inner.x, inner.y, inner.width, ib), ic);
        HorrorUI.Fill(new Rect(inner.x, inner.yMax - ib, inner.width, ib), ic);
        HorrorUI.Fill(new Rect(inner.x, inner.y, ib, inner.height), ic);
        HorrorUI.Fill(new Rect(inner.xMax - ib, inner.y, ib, inner.height), ic);

        labelStyle ??= HorrorUI.Style(HorrorUI.TypeFont);
        nameStyle ??= new GUIStyle(HorrorUI.Style(HorrorUI.SerifFont)) { wordWrap = true };
        labelStyle.fontSize = HorrorUI.Px(21);
        HorrorUI.Text(new Rect(card.x, card.y + 12f * k, card.width, 32f * k), "ACHIEVEMENT UNLOCKED", labelStyle, HorrorUI.A(HorrorUI.Bone, a), 0f, 2f * k);
        HorrorUI.Fill(new Rect(card.center.x - 70f * k, card.y + 46f * k, 140f * k, Mathf.Max(1f, 1.5f * k)), HorrorUI.A(HorrorUI.Rust, 0.9f * a));

        // Icon, flaring as it lands.
        var tex = Icon(current.icon);
        float flare = t < SlideIn + 0.6f ? 1f - Mathf.Clamp01((t - SlideIn) / 0.6f) : 0f;
        float isz = 112f * k;
        var ir = new Rect(card.center.x - isz * 0.5f, card.y + 58f * k, isz, isz);
        HorrorUI.Glow(new Rect(ir.x - 26f * k, ir.y - 26f * k, ir.width + 52f * k, ir.height + 52f * k), HorrorUI.A(HorrorUI.Ember, (0.25f + 0.5f * flare) * a));
        if (tex != null)
        {
            var old = GUI.color;
            GUI.color = HorrorUI.Out(new Color(1f, 1f, 1f, a));
            GUI.DrawTexture(ir, HorrorUI.Tex(tex), ScaleMode.ScaleToFit, true);
            GUI.color = old;
        }

        nameStyle.fontSize = HorrorUI.Px(28);
        HorrorUI.Text(new Rect(card.x + 12f * k, card.y + 178f * k, card.width - 24f * k, 80f * k), current.name, nameStyle,
                      HorrorUI.A(HorrorUI.Ember, a), 0f, 2f * k);
    }
}
