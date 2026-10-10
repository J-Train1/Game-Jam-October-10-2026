using UnityEngine;

// The maze's background sound and its little lies.
//  - Wind through the corn + a low drone, both looping (Ambience & Music volume). They dip during the heart
//    screen, the win screen and the grave ending.
//  - Every 20-50 seconds, when nothing is actually close, something sounds just BEHIND you: mostly a stick snapping
//    (sometimes a second snap, closer), two slow steps, a stalk creaking... and very rarely a cry far off in the field.
//    The pumpkins themselves make no sound at all.
// Added automatically by KeyHUD.
public class SoundScape : MonoBehaviour
{
    [Header("Loops")]
    [Range(0f, 1f)] public float windVolume = 0.4f;
    [Range(0f, 1f)] public float droneVolume = 0.3f;

    [Header("Something behind you")]
    public Vector2 firstScareAfter = new Vector2(20f, 35f);
    public Vector2 scareInterval = new Vector2(22f, 48f);
    [Tooltip("No fake noises when a real pumpkin is this close (it would be a giveaway).")]
    public float quietIfPumpkinWithin = 10f;
    [Range(0f, 1f)] public float scareVolume = 0.75f;

    AudioSource wind, drone;
    int lastKind = -1, lastTwig = -1;
    float nextScare, duck = 1f, start;

    void Start()
    {
        start = Time.time;
        wind = MakeLoop("Ambience_Wind");
        drone = MakeLoop("Ambience_Drone");
        if (drone != null) drone.time = Random.Range(0f, drone.clip.length * 0.8f); // don't always start the same way
        nextScare = Time.time + Random.Range(firstScareAfter.x, firstScareAfter.y);
    }

    AudioSource MakeLoop(string name)
    {
        var clip = GameAudio.Get(name);
        if (clip == null) return null;
        var s = gameObject.AddComponent<AudioSource>();
        s.clip = clip;
        s.loop = true;
        s.playOnAwake = false;
        s.spatialBlend = 0f;
        s.volume = 0f;
        s.Play();
        return s;
    }

    void Update()
    {
        // Loops: fade in, dip during the big moments.
        float target = (DeathScreen.IsShowing || WinScreen.IsShowing) ? 0.25f : GraveFinale.IsPlaying ? 0.35f : 1f;
        duck = Mathf.MoveTowards(duck, target, Time.unscaledDeltaTime * 0.8f);
        float fadeIn = Mathf.Clamp01((Time.time - start) / 4f);
        float amb = GameSettings.Amb * duck * fadeIn;
        if (wind != null) wind.volume = windVolume * amb;
        if (drone != null) drone.volume = droneVolume * amb;

        if (Time.time >= nextScare)
        {
            nextScare = Time.time + Random.Range(scareInterval.x, scareInterval.y);
            TryScare();
        }
    }

    void TryScare()
    {
        var pc = PlayerController.Instance;
        if (pc == null || !pc.enabled || PumpkinMonster.PlayerIsCaught || PauseMenu.IsPaused
            || DeathScreen.IsShowing || WinScreen.IsShowing || GraveFinale.IsPlaying) return;
        foreach (var m in PumpkinMonster.All)
            if (m != null && m.isActiveAndEnabled && (m.transform.position - pc.transform.position).sqrMagnitude < quietIfPumpkinWithin * quietIfPumpkinWithin)
            { nextScare = Time.time + Random.Range(10f, 20f); return; }

        var t = pc.transform;
        // Somewhere behind you (within about 60 degrees of straight back), 3-7 m away.
        Vector3 back = Quaternion.AngleAxis(Random.Range(-60f, 60f), Vector3.up) * -t.forward;
        Vector3 behind = t.position + back * Random.Range(3f, 7f) + Vector3.up * 0.3f;

        // Mostly snapping sticks; never the same kind twice in a row.
        int kind;
        do
        {
            float r = Random.value;
            kind = r < 0.45f ? 0 : r < 0.62f ? 1 : r < 0.82f ? 2 : r < 0.95f ? 3 : 4;
        } while (kind == lastKind && kind != 0);
        lastKind = kind;

        switch (kind)
        {
            case 0: GameAudio.PlayAt(Twig(), behind, scareVolume * Random.Range(0.6f, 0.9f), Random.Range(0.85f, 1.1f)); break;   // a stick snaps
            case 1: StartCoroutine(TwoSnaps(behind, t.right)); break;                                                            // snap... snap
            case 2: StartCoroutine(TwoSteps(behind, t.right)); break;                                                            // two slow steps
            case 3: GameAudio.PlayAt(GameAudio.Creak(), behind, scareVolume * 0.7f, Random.Range(0.8f, 1f)); break;              // a stalk creaks
            default: GameAudio.Play2D(GameAudio.Get("Far_Cry"), 0.35f); break;                                                   // far off in the field
        }
    }

    AudioClip Twig()
    {
        int i;
        do i = Random.Range(1, 8); while (i == lastTwig);
        lastTwig = i;
        return Random.value < 0.75f ? GameAudio.Get("Twig_" + i) : GameAudio.CrackSmall();
    }

    // A stick snaps, a pause... and another one, a little closer.
    System.Collections.IEnumerator TwoSnaps(Vector3 at, Vector3 side)
    {
        GameAudio.PlayAt(Twig(), at, scareVolume * 0.6f, Random.Range(0.9f, 1.05f));
        yield return new WaitForSeconds(Random.Range(0.9f, 1.8f));
        var pc = PlayerController.Instance;
        Vector3 closer = pc != null ? Vector3.Lerp(at, pc.transform.position, 0.3f) + side * Random.Range(-0.5f, 0.5f) : at;
        GameAudio.PlayAt(Twig(), closer, scareVolume * 0.85f, Random.Range(0.85f, 1f));
    }

    // Two slow, heavy steps in the leaves... then nothing.
    System.Collections.IEnumerator TwoSteps(Vector3 at, Vector3 side)
    {
        float pitch = Random.Range(0.72f, 0.85f);
        GameAudio.PlayAt(GameAudio.Pick("Step", 10), at, scareVolume, pitch);
        yield return new WaitForSeconds(Random.Range(0.55f, 0.8f));
        GameAudio.PlayAt(GameAudio.Pick("Step", 10), at + side * 0.4f, scareVolume * 0.9f, pitch * 0.97f);
    }
}
