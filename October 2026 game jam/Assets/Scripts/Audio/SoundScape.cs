using UnityEngine;

// The maze's background sound and its little lies.
//  - Wind through the corn + a low drone, both looping (Ambience & Music volume). They dip during the heart
//    screen, the win screen and the grave ending.
//  - Every minute or so, when nothing is actually close, something sounds just BEHIND you: a twig snapping, two
//    slow steps in the dry leaves, a stalk creaking... and very rarely a cry far off in the field.
//    The pumpkins themselves make no sound at all.
// Added automatically by KeyHUD.
public class SoundScape : MonoBehaviour
{
    [Header("Loops")]
    [Range(0f, 1f)] public float windVolume = 0.4f;
    [Range(0f, 1f)] public float droneVolume = 0.3f;

    [Header("Something behind you")]
    public Vector2 firstScareAfter = new Vector2(35f, 60f);
    public Vector2 scareInterval = new Vector2(45f, 100f);
    [Tooltip("No fake noises when a real pumpkin is this close (it would be a giveaway).")]
    public float quietIfPumpkinWithin = 10f;
    [Range(0f, 1f)] public float scareVolume = 0.75f;

    AudioSource wind, drone;
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
        Vector3 behind = t.position - t.forward * Random.Range(3f, 5.5f) + t.right * Random.Range(-1.5f, 1.5f) + Vector3.up * 0.3f;
        float r = Random.value;
        if (r < 0.4f)
            GameAudio.PlayAt(GameAudio.CrackSmall(), behind, scareVolume * 0.8f, Random.Range(0.85f, 1.05f));
        else if (r < 0.75f)
            StartCoroutine(TwoSteps(behind, t.right));
        else if (r < 0.92f)
            GameAudio.PlayAt(GameAudio.Creak(), behind, scareVolume * 0.7f, Random.Range(0.8f, 1f));
        else
            GameAudio.Play2D(GameAudio.Get("Far_Cry"), 0.35f); // far off in the field
    }

    // Two slow, heavy steps in the leaves... then nothing.
    System.Collections.IEnumerator TwoSteps(Vector3 at, Vector3 side)
    {
        float pitch = Random.Range(0.72f, 0.85f);
        GameAudio.PlayAt(GameAudio.Pick("Step", 8), at, scareVolume, pitch);
        yield return new WaitForSeconds(Random.Range(0.55f, 0.8f));
        GameAudio.PlayAt(GameAudio.Pick("Step", 8), at + side * 0.4f, scareVolume * 0.9f, pitch * 0.97f);
    }
}
