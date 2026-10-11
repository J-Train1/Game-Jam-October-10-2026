using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// The pumpkin monster (replaces the scarecrows).
// Wander (slow shamble) -> Hunt (hears or sees you) -> Search (goes where it last sensed you) -> Wander.
// Flashlight: the beam on it (head, chest or feet) means it has seen you. While hunting, it FREEZES in
// the beam: snaps into a creepy pose, whips around to face you, and locks its head on yours (look-at IK).
// Unlit, it hurries after you a little faster than you walk; sprinting gets you away. Touching you catches you,
// which hands the monster to JumpScare (one of three random scares), then DeathScreen (lose a heart / game over).
// Moves along the maze tile grid (MazeGrid paths): no NavMesh, no collider needed.
// A little cunning:
//  - Picking up a key or reaching the gate makes as much noise as sprinting: pumpkins within earshot come to look.
//  - When two or more hunt you at once, the closest one chases while the others take a different route and try
//    to cut you off ahead of where you're running (like the ghosts in Pac-Man). Close enough, they all just chase.
public class PumpkinMonster : MonoBehaviour
{
    public enum State { Wander, Hunt, Search }

    public static readonly List<PumpkinMonster> All = new List<PumpkinMonster>();
    /// <summary>Raised once per catch when a pumpkin catches the player (before the jump scare starts).</summary>
    public static event System.Action<PumpkinMonster> PlayerCaught;
    public static bool PlayerIsCaught { get; private set; }
    public static void ResetRound() => PlayerIsCaught = false;

    [Header("Speeds (m/s)")]
    public float wanderSpeed = 1.1f;
    public float searchSpeed = 2.2f;
    [Tooltip("Just above the player's 3 m/s walk, below the 5.5 m/s sprint: walking away fails, sprinting works.")]
    public float huntSpeed = 3.4f;
    [Tooltip("Degrees per second it turns while moving.")]
    public float turnSpeed = 540f;
    [Tooltip("How fast it whips around to face you when the beam hits it (seconds).")]
    public float freezeTurnTime = 0.08f;

    [Header("Animation playback speed")]
    public float wanderAnimSpeed = 0.75f;
    public float searchAnimSpeed = 1.1f;
    public float huntAnimSpeed = 1.8f;
    [Tooltip("Animator state played while hunting. Walk = sped-up shamble, Run = the crouched run clip.")]
    public string huntAnimState = "Walk";

    [Header("Senses")]
    public float sightRange = 12f;
    [Range(0f, 180f)] public float sightHalfAngle = 70f;
    [Tooltip("Inside this distance it notices you even behind it (if corn doesn't block the line).")]
    public float closeSense = 2.5f;
    [Tooltip("Seconds without hearing or seeing you before it goes searching.")]
    public float loseTargetAfter = 4f;
    [Tooltip("After reaching your last known spot, how many nearby spots it checks before wandering again.")]
    public int searchHops = 2;
    public float eyeHeight = 1.6f;

    [Header("Look-at (frozen pose)")]
    [Range(0f, 1f)] public float frozenBodyLook = 0.35f;
    [Range(0f, 1f)] public float frozenLookClamp = 0.25f;

    [Header("Pack hunting")]
    [Tooltip("Noise radius of picking up a key / reaching the gate (0 = same as the player's sprint).")]
    public float objectiveNoiseRadius = 0f;
    [Tooltip("Flankers stop circling around and chase straight in when this many tiles (on foot) from the player.")]
    public int flankChaseTiles = 3;
    [Tooltip("How far ahead of the player (tiles on foot) a flanker tries to cut them off.")]
    public Vector2Int flankAhead = new Vector2Int(2, 8);
    [Tooltip("Seconds between flank route updates.")]
    public float flankReplan = 1f;

    [Header("Catch")]
    public float catchDistance = 1.1f;
    [Tooltip("Only used if the jump scare can't run (no camera): reload after this many seconds.")]
    public float reloadDelay = 1.2f;

    [Header("Debug")]
    public bool logStates = true;

    public State CurrentState { get; private set; }
    public bool IsFrozen { get; private set; }
    /// <summary>True while this monster is performing the jump scare.</summary>
    public bool ScareMode { get; private set; }
    /// <summary>Humanoid head bone (used by the jump scare to frame the face).</summary>
    public Transform Head => anim != null && anim.isHuman ? anim.GetBoneTransform(HumanBodyBones.Head) : null;

    Animator anim;
    MazeGrid grid;
    Transform player;
    Transform playerHead;
    List<Vector2Int> path;
    int pathIndex;
    float repathTimer;
    float lastSenseTime = -999f;
    Vector3 lastKnownPos;
    int hopsLeft;
    string currentAnim;
    float turnVel;
    float lookWeight;
    readonly Vector3[] bodyPoints = new Vector3[3];
    AnimatorCullingMode baseCulling;

    // Jump-scare arm IK (set by JumpScare each frame).
    bool scareHandRight = true;
    Vector3 scareHandPos, scareHandHint;
    float scareHandWeight;
    Vector3 otherHandPos, otherHandHint;
    float otherHandWeight;

    // Pack hunting
    bool hooked;
    KeyManager hookedKeys;
    ExitGate hookedGate;
    float flankTimer;
    bool flanking;
    /// <summary>True while this monster is circling round to cut the player off (for debugging).</summary>
    public bool IsFlanking => flanking && CurrentState == State.Hunt;

    void OnEnable() { if (!All.Contains(this)) All.Add(this); }
    void OnDestroy()
    {
        All.Remove(this);
        if (hookedKeys != null) hookedKeys.OnKeyCollected -= OnKeyNoise;
        if (hookedGate != null) { hookedGate.OnLockRemoved -= OnGateNoise; hookedGate.OnRattled -= OnGateRattle; }
    }

    void Awake()
    {
        anim = GetComponentInChildren<Animator>();
        if (anim != null)
        {
            anim.applyRootMotion = false; // movement is driven by this script
            baseCulling = anim.cullingMode;
            var relay = anim.GetComponent<PumpkinIK>();
            if (relay == null) relay = anim.gameObject.AddComponent<PumpkinIK>();
            relay.owner = this;
        }
    }

    void Start()
    {
        grid = MazeGenerator.Instance != null ? MazeGenerator.Instance.Grid : null;
        if (PlayerController.Instance != null) player = PlayerController.Instance.transform;
        playerHead = Camera.main != null ? Camera.main.transform : player;
        lastKnownPos = transform.position;
        if (grid == null || player == null)
        {
            Debug.LogWarning("[Pumpkin] No maze or player found; monster disabled.");
            enabled = false;
            return;
        }
        SetState(State.Wander);
    }

    void Update()
    {
        if (PlayerIsCaught || grid == null) return;
        float dt = Time.deltaTime;
        if (!hooked) HookNoises();

        bool lit = IsLit();
        bool sensed = lit || Sense();
        if (sensed) { lastSenseTime = Time.time; lastKnownPos = player.position; }

        switch (CurrentState)
        {
            case State.Wander:
                if (sensed) { SetState(State.Hunt); return; }
                if (FollowPath(wanderSpeed, dt)) SetPath(PickTile(CurrentTile(), 8, 40));
                break;

            case State.Search:
                if (sensed) { SetState(State.Hunt); return; }
                if (FollowPath(searchSpeed, dt))
                {
                    if (hopsLeft-- > 0) SetPath(PickTile(CurrentTile(), 2, 6));
                    else SetState(State.Wander);
                }
                break;

            case State.Hunt:
                if (lit) { Freeze(); return; }
                Unfreeze();
                if (Time.time - lastSenseTime > loseTargetAfter) { SetState(State.Search); return; }
                Hunt(dt, sensed);
                break;
        }
    }

    // ---------------- States ----------------

    void SetState(State s)
    {
        CurrentState = s;
        path = null;
        flanking = false;
        IsFrozen = false;
        switch (s)
        {
            case State.Wander:
                PlayAnim("Walk", 0.3f, wanderAnimSpeed);
                SetPath(PickTile(CurrentTile(), 8, 40));
                break;
            case State.Hunt:
                repathTimer = 0f;
                PlayAnim(huntAnimState, 0.15f, huntAnimSpeed);
                break;
            case State.Search:
                hopsLeft = searchHops;
                PlayAnim("Walk", 0.3f, searchAnimSpeed);
                SetPath(grid.WorldToTile(lastKnownPos));
                break;
        }
        if (logStates) Debug.Log($"[Pumpkin] {name} -> {s}");
    }

    void Hunt(float dt, bool sensed)
    {
        if (Flank(dt)) { TryCatch(); return; }
        var here = CurrentTile();
        var goal = grid.WorldToTile(lastKnownPos);
        if (!grid.IsOpen(goal) || here == goal)
        {
            // Same tile: go straight for the player (or the spot it last sensed them).
            Vector3 to = lastKnownPos - transform.position; to.y = 0f;
            float d = to.magnitude;
            if (d > 0.05f) MoveToward(to, Mathf.Min(huntSpeed * dt, d), dt);
            else if (!sensed) { SetState(State.Search); return; } // reached the spot, nobody here
            path = null;
        }
        else
        {
            repathTimer -= dt;
            if (path == null || repathTimer <= 0f) { SetPath(goal); repathTimer = 0.35f; }
            FollowPath(huntSpeed, dt);
        }
        TryCatch();
    }

    void Freeze()
    {
        if (!IsFrozen)
        {
            IsFrozen = true;
            PlayAnim("Pose", 0.06f, 1f);
        }
        // Whip around to face the player.
        Vector3 to = player.position - transform.position; to.y = 0f;
        if (to.sqrMagnitude > 0.0001f)
        {
            float targetYaw = Quaternion.LookRotation(to).eulerAngles.y;
            float yaw = Mathf.SmoothDampAngle(transform.eulerAngles.y, targetYaw, ref turnVel, freezeTurnTime);
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        }
    }

    void Unfreeze()
    {
        if (!IsFrozen) return;
        IsFrozen = false;
        turnVel = 0f;
        PlayAnim(huntAnimState, 0.1f, huntAnimSpeed);
    }

    void TryCatch()
    {
        Vector3 d = player.position - transform.position; d.y = 0f;
        if (d.magnitude <= catchDistance) Catch();
    }

    void Catch()
    {
        if (PlayerIsCaught || WinScreen.IsShowing) return;
        PlayerIsCaught = true;
        if (logStates) Debug.Log($"[Pumpkin] {name} caught the player");
        if (PlayerController.Instance != null) PlayerController.Instance.InputEnabled = false;
        PlayerCaught?.Invoke(this);
        if (!JumpScare.Trigger(this)) StartCoroutine(ReloadAfter(reloadDelay)); // fallback: no camera
    }

    /// <summary>Called by JumpScare: snap into the creepy pose, stop all AI, lock the face onto the camera.</summary>
    public void BeginJumpScare()
    {
        ScareMode = true;
        IsFrozen = false;
        path = null;
        if (anim != null) anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        PlayAnim("Pose", 0.05f, 1f);
        enabled = false; // AI off; the PumpkinIK relay still drives the look-at
    }

    /// <summary>
    /// Back to normal at a new spot, wandering (after a jump scare, or when the player respawns).
    /// Works whether the monster is mid-scare, hidden, frozen or hunting.
    /// </summary>
    public void ResetTo(Vector3 position)
    {
        ScareMode = false;
        IsFrozen = false;
        scareHandWeight = 0f;
        otherHandWeight = 0f;
        lookWeight = 0f;
        turnVel = 0f;
        lastSenseTime = -999f;
        if (!gameObject.activeSelf) gameObject.SetActive(true);
        transform.SetPositionAndRotation(position, Quaternion.Euler(0f, Random.Range(0, 4) * 90f, 0f));
        if (anim != null) { anim.cullingMode = baseCulling; anim.speed = 1f; }
        currentAnim = null;
        enabled = true;
        if (grid != null) SetState(State.Wander);
    }

    /// <summary>Jump scare only: drive one hand to a world position with IK (weight 0 = off).</summary>
    /// <summary>The second hand during a scare (the one SetScareHand isn't using). Used by the two-handed grab.</summary>
    public void SetScareOtherHand(Vector3 position, Vector3 elbowHint, float weight)
    {
        otherHandPos = position;
        otherHandHint = elbowHint;
        otherHandWeight = Mathf.Clamp01(weight);
    }

    public void SetScareHand(bool rightHand, Vector3 position, Vector3 elbowHint, float weight)
    {
        scareHandRight = rightHand;
        scareHandPos = position;
        scareHandHint = elbowHint;
        scareHandWeight = Mathf.Clamp01(weight);
    }

    IEnumerator ReloadAfter(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        SceneLoader.Reload();
    }

    // ---------------- Noise: keys and the gate ----------------

    void HookNoises()
    {
        if (hookedKeys == null && KeyManager.Instance != null) { hookedKeys = KeyManager.Instance; hookedKeys.OnKeyCollected += OnKeyNoise; }
        if (hookedGate == null && ExitGate.Instance != null)
        {
            hookedGate = ExitGate.Instance;
            hookedGate.OnLockRemoved += OnGateNoise;
            hookedGate.OnRattled += OnGateRattle;
        }
        hooked = hookedKeys != null && hookedGate != null;
    }

    void OnKeyNoise(int collected, int total) { if (collected > 0) HearNoiseAtPlayer(); }
    void OnGateNoise(int removed, int total) => HearNoiseAtPlayer();
    void OnGateRattle(int locksLeft) => HearNoiseAtPlayer();

    // The same rule as hearing the player sprint: within the radius (straight line), it knows where you are.
    void HearNoiseAtPlayer()
    {
        if (!enabled || ScareMode || PlayerIsCaught || player == null || grid == null) return;
        var pc = PlayerController.Instance;
        float r = objectiveNoiseRadius > 0f ? objectiveNoiseRadius : (pc != null ? pc.SprintNoiseRadius : 14f);
        Vector3 to = player.position - transform.position; to.y = 0f;
        if (to.magnitude > r) return;
        lastSenseTime = Time.time;
        lastKnownPos = player.position;
        if (logStates) Debug.Log($"[Pumpkin] {name} heard a key / the gate");
        if (CurrentState != State.Hunt) SetState(State.Hunt);
    }

    // ---------------- Pack hunting: cut the player off ----------------

    static int fieldFrame = -1;
    static Vector2Int fieldTile;
    static int[,] playerField;

    /// <summary>Distance (tiles on foot) from the player's tile to every tile; worked out once per frame.</summary>
    int[,] PlayerField()
    {
        var pt = grid.WorldToTile(player.position);
        if (fieldFrame != Time.frameCount || pt != fieldTile || playerField == null)
        {
            fieldFrame = Time.frameCount;
            fieldTile = pt;
            playerField = grid.IsOpen(pt) ? grid.DistanceField(pt) : null;
        }
        return playerField;
    }

    // Returns true if this monster is flanking this frame (and has moved); false = chase normally.
    bool Flank(float dt)
    {
        var dP = PlayerField();
        if (dP == null) { flanking = false; return false; }
        var here = CurrentTile();
        int myD = dP[here.x, here.y];

        // Who's closest to the player among the hunters? That one chases; the rest go around.
        PumpkinMonster leader = null;
        int best = int.MaxValue;
        foreach (var m in All)
        {
            if (m == null || !m.isActiveAndEnabled || m.ScareMode || m.CurrentState != State.Hunt || m.grid == null) continue;
            var t = m.CurrentTile();
            int d = dP[t.x, t.y];
            if (d < 0) continue;
            if (d < best || (d == best && m.GetInstanceID() < (leader != null ? leader.GetInstanceID() : int.MaxValue))) { best = d; leader = m; }
        }
        if (leader == null || leader == this || myD < 0 || myD <= flankChaseTiles) { if (flanking) path = null; flanking = false; return false; }

        // The pack shares what it knows: the leader's sighting keeps the flanker on the hunt.
        if (leader.lastSenseTime > lastSenseTime) { lastSenseTime = leader.lastSenseTime; lastKnownPos = leader.lastKnownPos; }

        flankTimer -= dt;
        if (!flanking || path == null || flankTimer <= 0f)
        {
            flankTimer = flankReplan;
            var target = FlankTarget(leader, dP);
            if (target == null) { if (flanking) path = null; flanking = false; return false; } // cornered: just come straight in
            path = FindPathAvoiding(here, target.Value, leader);
            pathIndex = (path != null && path.Count > 1) ? 1 : 0;
            if (path == null) { flanking = false; return false; }
            if (!flanking && logStates) Debug.Log($"[Pumpkin] {name} flanking toward {target.Value} (leader {leader.name})");
            flanking = true;
        }
        if (FollowPath(huntSpeed, dt))
        {
            // At the cut-off point: lie in wait facing the way you'd come, and re-think shortly.
            flankTimer = Mathf.Min(flankTimer, 0.4f);
            Vector3 to = player.position - transform.position; to.y = 0f;
            if (to.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(to), turnSpeed * 0.5f * dt);
        }
        return true;
    }

    // A tile a few steps ahead of the player, on the far side from the leader (where they'd run to),
    // that this monster can reach about as soon as the player can.
    Vector2Int? FlankTarget(PumpkinMonster leader, int[,] dP)
    {
        var pt = grid.WorldToTile(player.position);
        var dA = grid.DistanceField(leader.CurrentTile());
        var dB = grid.DistanceField(CurrentTile());
        int aP = dA[pt.x, pt.y];
        if (aP < 0) return null;
        Vector2Int? bestT = null;
        float bestScore = float.MaxValue;
        for (int x = 0; x < grid.width; x++)
        for (int y = 0; y < grid.height; y++)
        {
            int p = dP[x, y], a = dA[x, y], b = dB[x, y];
            if (p < flankAhead.x || p > flankAhead.y || a < 0 || b < 0) continue;
            if (a <= aP) continue;                     // on the leader's side of the player: no use
            float score = (b - p) + 0.35f * Mathf.Abs(p - 4);
            if (grid.OpenNeighborCount(new Vector2Int(x, y)) >= 3) score -= 1.5f; // a crossing: cut it and they're boxed in
            if (score < bestScore) { bestScore = score; bestT = new Vector2Int(x, y); }
        }
        return bestT;
    }

    // Shortest route that stays off the leader's path where it can (cost 1 per tile, 5 on the leader's route).
    List<Vector2Int> FindPathAvoiding(Vector2Int from, Vector2Int to, PumpkinMonster leader)
    {
        if (!grid.IsOpen(from) || !grid.IsOpen(to)) return null;
        var avoid = new HashSet<Vector2Int>();
        if (leader.path != null)
            for (int i = Mathf.Max(0, leader.pathIndex - 1); i < leader.path.Count; i++) avoid.Add(leader.path[i]);
        avoid.Add(leader.CurrentTile());
        avoid.Remove(from);

        var cost = new Dictionary<Vector2Int, int> { [from] = 0 };
        var prev = new Dictionary<Vector2Int, Vector2Int> { [from] = from };
        var open = new List<Vector2Int> { from };
        while (open.Count > 0)
        {
            int bi = 0;
            for (int i = 1; i < open.Count; i++) if (cost[open[i]] < cost[open[bi]]) bi = i;
            var c = open[bi];
            open.RemoveAt(bi);
            if (c == to) break;
            foreach (var n in grid.OpenNeighbors(c))
            {
                int nc = cost[c] + (avoid.Contains(n) ? 5 : 1);
                if (!cost.TryGetValue(n, out int old) || nc < old)
                {
                    if (!cost.ContainsKey(n)) open.Add(n);
                    cost[n] = nc;
                    prev[n] = c;
                }
            }
        }
        if (!prev.ContainsKey(to)) return null;
        var route = new List<Vector2Int>();
        for (var c = to; c != from; c = prev[c]) route.Add(c);
        route.Add(from);
        route.Reverse();
        return route;
    }

    // ---------------- Senses ----------------

    bool IsLit()
    {
        var fl = Flashlight.Instance;
        if (fl == null) return false;
        Vector3 p = transform.position;
        bodyPoints[0] = p + Vector3.up * 1.75f; // head
        bodyPoints[1] = p + Vector3.up * 1.1f;  // chest
        bodyPoints[2] = p + Vector3.up * 0.3f;  // legs
        return fl.IsAnyPointLit(bodyPoints);
    }

    bool Sense()
    {
        Vector3 to = player.position - transform.position; to.y = 0f;
        float d = to.magnitude;
        var pc = PlayerController.Instance;
        if (pc != null && d <= pc.NoiseRadius) return true;                       // heard
        if (d > sightRange) return false;
        if (d > closeSense && Vector3.Angle(transform.forward, to) > sightHalfAngle) return false;
        return ClearLine(transform.position + Vector3.up * eyeHeight, playerHead.position); // seen
    }

    /// <summary>True if no corn wall (or exit blocker) sits between the two points.</summary>
    public static bool ClearLine(Vector3 from, Vector3 to)
    {
        Vector3 d = to - from;
        float len = d.magnitude;
        if (len < 0.01f) return true;
        if (Physics.Raycast(from, d / len, out var hit, len, ~0, QueryTriggerInteraction.Ignore))
            return hit.collider.GetComponentInParent<MazeGenerator>() == null;
        return true;
    }

    // ---------------- IK (called from PumpkinIK on the Animator object) ----------------

    public void ApplyIK(Animator a)
    {
        if (playerHead == null && Camera.main != null) playerHead = Camera.main.transform;
        var goal = scareHandRight ? AvatarIKGoal.RightHand : AvatarIKGoal.LeftHand;
        var other = scareHandRight ? AvatarIKGoal.LeftHand : AvatarIKGoal.RightHand;
        var hint = scareHandRight ? AvatarIKHint.RightElbow : AvatarIKHint.LeftElbow;
        if (ScareMode && playerHead != null)
        {
            // Jump scare: whole upper body and face pushed straight into the camera.
            lookWeight = 1f;
            a.SetLookAtWeight(1f, 0.6f, 1f, 0f, 0.5f);
            a.SetLookAtPosition(playerHead.position);

            var otherHint = scareHandRight ? AvatarIKHint.LeftElbow : AvatarIKHint.RightElbow;
            a.SetIKPositionWeight(other, otherHandWeight);
            a.SetIKPosition(other, otherHandPos);
            a.SetIKHintPositionWeight(otherHint, otherHandWeight * 0.6f);
            a.SetIKHintPosition(otherHint, otherHandHint);
            a.SetIKPositionWeight(goal, scareHandWeight);
            a.SetIKPosition(goal, scareHandPos);
            a.SetIKHintPositionWeight(hint, scareHandWeight * 0.6f);
            a.SetIKHintPosition(hint, scareHandHint);
            return;
        }
        a.SetIKPositionWeight(AvatarIKGoal.LeftHand, 0f);
        a.SetIKPositionWeight(AvatarIKGoal.RightHand, 0f);
        float target = IsFrozen ? 1f : (CurrentState == State.Hunt ? 0.5f : 0f);
        lookWeight = Mathf.MoveTowards(lookWeight, target, Time.deltaTime * (IsFrozen ? 12f : 4f));
        if (lookWeight <= 0.001f || playerHead == null) { a.SetLookAtWeight(0f); return; }
        if (IsFrozen) a.SetLookAtWeight(lookWeight, frozenBodyLook, 1f, 0f, frozenLookClamp);
        else a.SetLookAtWeight(lookWeight, 0.1f, 0.8f, 0f, 0.6f);
        a.SetLookAtPosition(playerHead.position);
    }

    // ---------------- Movement ----------------

    Vector2Int CurrentTile()
    {
        var t = grid.WorldToTile(transform.position);
        if (grid.IsOpen(t)) return t;
        foreach (var n in grid.OpenNeighbors(t)) return n; // nudge back onto the path
        return grid.startTile;
    }

    void SetPath(Vector2Int goal)
    {
        path = grid.FindPath(CurrentTile(), goal);
        pathIndex = (path != null && path.Count > 1) ? 1 : 0;
    }

    // Returns true when the path is finished (or there is none).
    bool FollowPath(float speed, float dt)
    {
        if (path == null || pathIndex >= path.Count) return true;
        Vector3 target = grid.TileToWorld(path[pathIndex]);
        Vector3 to = target - transform.position; to.y = 0f;
        float step = speed * dt;
        if (to.magnitude <= step)
        {
            transform.position = new Vector3(target.x, transform.position.y, target.z);
            pathIndex++;
            return pathIndex >= path.Count;
        }
        MoveToward(to, step, dt);
        return false;
    }

    void MoveToward(Vector3 flatDir, float step, float dt)
    {
        Vector3 dir = flatDir.normalized;
        transform.position += dir * step;
        transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(dir), turnSpeed * dt);
    }

    /// <summary>A random cell tile between minDist and maxDist tiles away on foot (or the farthest one in range).</summary>
    Vector2Int PickTile(Vector2Int from, int minDist, int maxDist)
    {
        var dist = grid.DistanceField(from);
        var options = new List<Vector2Int>();
        Vector2Int far = from;
        int farD = -1;
        for (int cx = 0; cx < grid.cellsX; cx++)
        for (int cy = 0; cy < grid.cellsY; cy++)
        {
            var t = MazeGrid.CellToTile(cx, cy);
            int d = dist[t.x, t.y];
            if (d < 0 || d > maxDist) continue;
            if (d > farD) { farD = d; far = t; }
            if (d >= minDist) options.Add(t);
        }
        return options.Count > 0 ? options[Random.Range(0, options.Count)] : far;
    }

    void PlayAnim(string state, float fade, float speed)
    {
        if (anim == null || anim.runtimeAnimatorController == null) return;
        anim.speed = speed;
        if (currentAnim == state) return;
        currentAnim = state;
        anim.CrossFadeInFixedTime(state, fade, 0);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.5f, 0f, 0.35f);
        Gizmos.DrawWireSphere(transform.position + Vector3.up * eyeHeight, sightRange);
        if (grid == null || path == null) return;
        Gizmos.color = Color.red;
        for (int i = Mathf.Max(1, pathIndex); i < path.Count; i++)
            Gizmos.DrawLine(grid.TileToWorld(path[i - 1]) + Vector3.up * 0.2f, grid.TileToWorld(path[i]) + Vector3.up * 0.2f);
    }
}
