using UnityEngine;

// The player's hearts for this run. Lives on a scene object, so a fresh maze (scene reload) starts full again.
// If none is in the scene, one is created the first time it's needed.
public class PlayerLives : MonoBehaviour
{
    public static PlayerLives Instance { get; private set; }

    [Range(1, 6)] public int maxLives = 3;

    public int Remaining { get; private set; }
    public int Max => maxLives;

    void Awake()
    {
        Instance = this;
        Remaining = maxLives;
    }

    public static PlayerLives Get()
    {
        if (Instance != null) return Instance;
        var found = FindFirstObjectByType<PlayerLives>();
        if (found != null) return found;
        return new GameObject("PlayerLives").AddComponent<PlayerLives>();
    }

    /// <summary>Lose one heart. Returns how many are left.</summary>
    public int LoseLife()
    {
        Remaining = Mathf.Max(0, Remaining - 1);
        Debug.Log($"[Lives] lost a heart: {Remaining}/{maxLives} left");
        return Remaining;
    }
}
