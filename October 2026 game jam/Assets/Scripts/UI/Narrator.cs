using System.Collections.Generic;
using UnityEngine;

// Whispered captions low in the middle of the screen: an old-print line plus a small typewriter line under it.
// They tremble in, hang for a moment and fade. Used for the tutorial intro, one-time contextual tips (first hunt,
// first freeze, low/dead light, winded), key pickups, the locked gate and the gate opening.
// Added automatically by KeyHUD; Narrator.Say(...) can be called from anywhere.
public class Narrator : MonoBehaviour
{
    public static Narrator Instance { get; private set; }

    public float introDelay = 1.4f;
    public float fadeIn = 0.6f;
    public float fadeOut = 0.9f;
    [Tooltip("How long tutorial lines stay fully visible.")]
    public float tutorialHold = 3.6f;

    class Line { public string main, sub; public float hold; public Color subColor; public bool finale; }
    readonly List<Line> queue = new List<Line>();
    Line current;
    float currentStart;
    bool introDone, keysHooked, gateHooked;
    GUIStyle mainStyle, subStyle;

    // Tutorial lines that already showed this play session (a replay doesn't repeat them).
    static bool fullIntroSeen;
    static readonly HashSet<string> tipsSeen = new HashSet<string>();

    static readonly string[] KeyLines = { "A rusted key.", "Cold iron, wet with dew.", "Another key. Something heard that." };
    static Color Hint => HorrorUI.A(HorrorUI.Ash, 0.95f);
    static Color Warn => HorrorUI.A(HorrorUI.BloodBright, 0.9f);

    void Awake() => Instance = this;
    void OnDestroy() { if (Instance == this) Instance = null; }

    public static void Say(string main, string sub = null, float hold = 2.6f, bool interrupt = false)
    {
        if (Instance == null) return;
        Instance.Enqueue(main, sub, hold, Warn, interrupt);
    }

    // interrupt = show right now; whatever was showing goes back to the front of the queue (unless it was nearly done).
    void Enqueue(string main, string sub, float hold, Color subColor, bool interrupt)
    {
        var l = new Line { main = main, sub = sub, hold = hold, subColor = subColor };
        if (!interrupt) { queue.Add(l); return; }
        if (current != null && Time.time - currentStart < fadeIn + current.hold * 0.6f) queue.Insert(0, current);
        current = l;
        currentStart = Time.time;
    }

    // One-time contextual tutorial.
    void Tip(string id, string main, string sub, Color subColor, bool interrupt = true)
    {
        if (!tipsSeen.Add(id)) return;
        Enqueue(main, sub, tutorialHold, subColor, interrupt);
    }

    void Update()
    {
        var km = KeyManager.Instance;
        if (!keysHooked && km != null) { keysHooked = true; km.OnKeyCollected += OnKey; }
        var gate = ExitGate.Instance;
        if (!gateHooked && gate != null)
        {
            gateHooked = true;
            gate.OnRattled += OnRattle;
            gate.OnOpened += () => Enqueue("The gate groans open.", "follow the path to the end", 3.4f, HorrorUI.A(HorrorUI.Ember, 0.9f), true);
        }

        if (!introDone && Time.timeSinceLevelLoad >= introDelay)
        {
            introDone = true;
            int n = km != null ? km.Total : 3;
            string keys = $"{HorrorUI.NumberWord(n)} rusted keys are hidden in the maze.";
            if (!fullIntroSeen)
            {
                fullIntroSeen = true;
                Enqueue("You wake in the corn. It is long past midnight.", "[ W A S D ]  walk        [ MOUSE ]  look", tutorialHold, Hint, false);
                Enqueue("Your flashlight is all you have.", "[ F ]  or  [ RIGHT MOUSE ]  flashlight on / off", tutorialHold, Hint, false);
                Enqueue(keys, "find them all - each one opens a lock on the gate", tutorialHold, Hint, false);
                Enqueue("The gate is the only way out.", "it is somewhere on the edge of the field", tutorialHold, Hint, false);
                Enqueue("Something hunts these rows.", "it cannot move while your light is on it", tutorialHold, Warn, false);
                Enqueue("Run if you have to. It will hear you.", "[ SHIFT ]  sprint - running is loud", tutorialHold, Hint, false);
                Enqueue("You have three hearts.", "lose them all and the field keeps you", tutorialHold, Warn, false);
                // The send-off: slower, redder, the edges of the screen bleed, and the second line arrives late.
                queue.Add(new Line
                {
                    main = "The last one who woke here never left.",
                    sub = "now he wears a pumpkin for a head... and he's been carving one for you.",
                    hold = 6.5f, subColor = HorrorUI.A(HorrorUI.BloodBright, 0.95f), finale = true,
                });
            }
            else
            {
                Enqueue(keys, "find them - find the gate", 3.2f, Hint, false);
                Enqueue("It cannot move while your light is on it.", "keep it in the beam", 3.2f, Warn, false);
            }
        }

        WatchForTips();

        if (current != null && Time.time - currentStart > fadeIn + current.hold + fadeOut) current = null;
        if (current == null && queue.Count > 0) { current = queue[0]; queue.RemoveAt(0); currentStart = Time.time; }
    }

    // Contextual tutorials the first time each thing happens.
    void WatchForTips()
    {
        if (PumpkinMonster.PlayerIsCaught || DeathScreen.IsShowing || WinScreen.IsShowing || GraveFinale.IsPlaying) return;

        foreach (var m in PumpkinMonster.All)
        {
            if (m == null || !m.isActiveAndEnabled) continue;
            if (m.IsFrozen)
                Tip("frozen", "Hold it there. Back away slowly.", "it moves again the moment the light leaves it", Hint);
            else if (m.CurrentState == PumpkinMonster.State.Hunt)
                Tip("hunt", "It's coming for you.", "find it and put your light on it", Warn);
        }

        var fl = Flashlight.Instance;
        if (fl != null)
        {
            if (fl.IsLockedOut)
                Tip("dead", "The light is dead.", "wait in the dark for it to recharge, then  [ F ]", Warn);
            else if (fl.IsOn && fl.IsLow)
                Tip("low", "Your light is dying.", "switch it off  [ F ]  and it slowly recharges", Warn);
        }

        var pc = PlayerController.Instance;
        if (pc != null && pc.IsExhausted)
            Tip("winded", "Your lungs are burning.", "stop running and let your breath come back", Hint);
    }

    void OnKey(int collected, int total)
    {
        if (collected <= 0) return;
        if (collected >= total)
            Enqueue("The last key. Now find the gate.", "something else is awake now", 3.2f, Warn, true);
        else
        {
            Enqueue(KeyLines[Mathf.Min(collected - 1, KeyLines.Length - 1)], $"{collected} of {total} keys", 2.8f, HorrorUI.A(HorrorUI.Brass, 0.9f), true);
            if (collected == 1)
                Tip("morekeys", "Every key you take wakes another of them.", "listen for them in the corn", Warn, false);
        }
    }

    void OnRattle(int locksLeft)
    {
        string sub = locksLeft == 1 ? "you need one more key" : $"you need {HorrorUI.NumberWord(locksLeft).ToLower()} more keys";
        Enqueue("Locked. The chains won't give.", sub, 2.8f, Warn, true);
    }

    void OnGUI()
    {
        if (current == null || Event.current == null) return;
        if (PumpkinMonster.PlayerIsCaught || DeathScreen.IsShowing || WinScreen.IsShowing || GraveFinale.IsPlaying) return;
        GUI.depth = -500;

        float t = Time.time - currentStart;
        float a = t < fadeIn ? HorrorUI.Smooth(t / fadeIn)
                : t < fadeIn + current.hold ? 1f
                : 1f - HorrorUI.Smooth((t - fadeIn - current.hold) / fadeOut);
        if (a <= 0.002f) return;

        if (mainStyle == null)
        {
            mainStyle = HorrorUI.Style(HorrorUI.SerifFont);
            subStyle = HorrorUI.Style(HorrorUI.TypeFont);
        }
        bool fin = current.finale;
        mainStyle.fontSize = HorrorUI.Px(fin ? 44 : 38);
        subStyle.fontSize = HorrorUI.Px(fin ? 25 : 21);

        float sw = Screen.width, sh = Screen.height, k = sh / 1080f;
        float y = sh * (fin ? 0.68f : 0.74f);

        if (fin)
        {
            // The world dims and the edges bleed while it speaks.
            HorrorUI.Vignette(0.75f * a, Color.black);
            HorrorUI.Vignette(0.35f * a * (0.8f + 0.2f * Mathf.Sin(Time.time * 5.5f)), HorrorUI.Blood);
            HorrorUI.Glow(new Rect(sw * 0.08f, y - 110 * k, sw * 0.84f, 330 * k), new Color(0f, 0f, 0f, 0.75f * a));
        }
        else
        {
            // Dark smudge so it reads over the moonlit corn.
            HorrorUI.Glow(new Rect(sw * 0.2f, y - 60 * k, sw * 0.6f, 200 * k), new Color(0f, 0f, 0f, 0.55f * a));
        }

        float drift = (1f - a) * 6f * k; // drifts up slightly as it appears / settles
        HorrorUI.Shaky(new Rect(0, y + drift, sw, 60 * k), current.main, mainStyle, HorrorUI.A(HorrorUI.Bone, a),
                       Time.time, (fin ? 1.3f : 0.7f) * k, (fin ? 3f : 1.5f) * k, (fin ? 2.2f : 1.2f) * k,
                       fin ? 1.4f : 0.6f, fin ? 0.1f : 0.04f, current.main.Length);
        if (!string.IsNullOrEmpty(current.sub))
        {
            float delay = fin ? 1.8f : 0.35f;
            float sa = a * Mathf.Clamp01((t - delay) / (fin ? 1.2f : 0.5f));
            var sr = new Rect(0, y + (fin ? 70 : 58) * k + drift, sw, 34 * k);
            var sc = HorrorUI.A(current.subColor, current.subColor.a * sa);
            if (fin) HorrorUI.Shaky(sr, current.sub, subStyle, sc, Time.time, 0.9f * k, 0.5f * k, 1.2f * k, 0.8f, 0.06f, 77);
            else HorrorUI.Text(sr, current.sub, subStyle, sc, 0f, 2f * k);
        }
    }
}
