using UnityEngine;

// What the game remembers between sessions: what you've unlocked and how you've done. Never the middle of a run.
// One small JSON blob in PlayerPrefs ("save.v1"), loaded on first use. Platform-neutral on purpose, so the
// mobile / VR / Steam versions can keep using it (Steam etc. just mirror what's in here).
// Grows as features arrive (achievements, rewards, stats); old saves load fine because missing fields keep defaults.
public static class SaveData
{
    [System.Serializable]
    public class Data
    {
        public int version = 1;
        public int highestUnlocked = 0;           // 0 = only the first difficulty
        public int lastDifficulty = 0;            // the one selected on the menu last time
        public int[] escapes = new int[3];        // escapes per difficulty
        public float[] bestTime = new float[3];   // seconds; 0 = never escaped
        public int deaths;                        // game overs
    }

    const string Key = "save.v1";
    static Data data;

    public static Data D
    {
        get
        {
            if (data != null) return data;
            data = null;
            string json = PlayerPrefs.GetString(Key, "");
            if (!string.IsNullOrEmpty(json))
            {
                try { data = JsonUtility.FromJson<Data>(json); }
                catch (System.Exception ex) { Debug.LogWarning("[Save] couldn't read the save, starting fresh: " + ex.Message); }
            }
            data ??= new Data();
            Fix(data);
            return data;
        }
    }

    // Arrays from an older/shorter save get resized so every difficulty has a slot.
    static void Fix(Data d)
    {
        int n = Difficulty.All.Length;
        if (d.escapes == null || d.escapes.Length < n) System.Array.Resize(ref d.escapes, n);
        if (d.bestTime == null || d.bestTime.Length < n) System.Array.Resize(ref d.bestTime, n);
        d.highestUnlocked = Mathf.Clamp(d.highestUnlocked, 0, n - 1);
        d.lastDifficulty = Mathf.Clamp(d.lastDifficulty, 0, d.highestUnlocked);
    }

    public static void Save()
    {
        if (data == null) return;
        PlayerPrefs.SetString(Key, JsonUtility.ToJson(data));
        PlayerPrefs.Save();
    }

    /// <summary>Forget everything (Settings > Reset Progress, or the editor tools).</summary>
    public static void ResetAll()
    {
        data = new Data();
        Fix(data);
        Save();
        Debug.Log("[Save] progress reset");
    }
}
