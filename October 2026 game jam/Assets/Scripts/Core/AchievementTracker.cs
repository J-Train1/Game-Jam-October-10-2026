using System.Collections.Generic;
using UnityEngine;

// Watches one run in the maze for achievements: first game, keys, the gate, freezing pumpkins, the flashlight,
// running out of breath and walking every row. Remembers per-run facts the win screen asks about
// (did the battery ever die, how long the light was on). Added automatically by KeyHUD.
public class AchievementTracker : MonoBehaviour
{
    public static AchievementTracker Instance { get; private set; }

    /// <summary>Seconds of light allowed for "without using your flashlight" (it starts on: time to switch it off).</summary>
    public const float LightGrace = 4f;

    public bool BatteryDied { get; private set; }
    public float LightOnTime { get; private set; }

    KeyManager keys;
    ExitGate gate;
    float lastFrozenTime = -99f;
    bool wasLockedOut;
    readonly HashSet<Vector2Int> visited = new HashSet<Vector2Int>();
    int tilesToVisit = -1;

    void Awake() => Instance = this;

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (keys != null) keys.OnKeyCollected -= OnKey;
        if (gate != null) { gate.OnLockRemoved -= OnLock; gate.OnOpened -= OnGateOpened; }
    }

    void Start() => Achievements.Unlock("FIRST_GAME");

    void Update()
    {
        if (keys == null && KeyManager.Instance != null) { keys = KeyManager.Instance; keys.OnKeyCollected += OnKey; }
        if (gate == null && ExitGate.Instance != null) { gate = ExitGate.Instance; gate.OnLockRemoved += OnLock; gate.OnOpened += OnGateOpened; }
        if (PauseMenu.IsPaused || PumpkinMonster.PlayerIsCaught || WinScreen.IsShowing) return;

        // Pumpkins frozen in the beam.
        int frozen = 0;
        foreach (var m in PumpkinMonster.All)
            if (m != null && m.isActiveAndEnabled && m.IsFrozen) frozen++;
        if (frozen > 0) { lastFrozenTime = Time.time; Achievements.Unlock("FIRST_FREEZE"); }
        if (frozen >= 2) Achievements.Unlock("DOUBLE_FEATURE");

        // The flashlight: how long it's on, and whether it ever died (on a pumpkin: Staring Contest).
        var fl = Flashlight.Instance;
        if (fl != null)
        {
            if (fl.IsOn && !GraveFinale.IsPlaying) LightOnTime += Time.deltaTime;
            if (fl.IsLockedOut && !wasLockedOut)
            {
                BatteryDied = true;
                if (Time.time - lastFrozenTime < 0.5f) Achievements.Unlock("STARING_CONTEST");
            }
            wasLockedOut = fl.IsLockedOut;
        }

        // Out of breath.
        var pc = PlayerController.Instance;
        if (pc != null && pc.IsExhausted) Achievements.Unlock("OUT_OF_BREATH");

        // Every row walked: every open tile of the maze (not the gate's gap) stood on in this run.
        var gen = MazeGenerator.Instance;
        if (pc != null && gen != null && gen.Grid != null && !Achievements.Has("CARTOGRAPHER"))
        {
            var g = gen.Grid;
            if (tilesToVisit < 0)
            {
                tilesToVisit = 0;
                for (int x = 0; x < g.width; x++)
                for (int y = 0; y < g.height; y++)
                    if (g.IsOpen(x, y) && new Vector2Int(x, y) != g.exitTile) tilesToVisit++;
            }
            var t = g.WorldToTile(pc.transform.position);
            if (g.IsOpen(t) && t != g.exitTile && visited.Add(t) && visited.Count >= tilesToVisit)
                Achievements.Unlock("CARTOGRAPHER");
        }
    }

    /// <summary>How much of this maze has been walked (0..1), for the curious.</summary>
    public float Explored => tilesToVisit > 0 ? (float)visited.Count / tilesToVisit : 0f;

    void OnKey(int collected, int total) { if (collected > 0) Achievements.Unlock("FIRST_KEY"); }

    // A lock came off, but not all of the gate's locks will this visit (not enough keys yet).
    void OnLock(int removed, int total)
    {
        int haveKeys = keys != null ? keys.Collected : 0;
        if (removed < total && haveKeys < total) Achievements.Unlock("ONE_LOCK");
    }

    void OnGateOpened() => Achievements.Unlock("GATE_OPEN");
}
