#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

// Editor shortcuts for testing the unlocks.
// Also: put a number (0, 1 or 2) in a file named Assets/.jam_diff to unlock everything and select that difficulty.
[InitializeOnLoad]
static class JamProgressTools
{
    const string Trigger = "Assets/.jam_diff";

    static JamProgressTools()
    {
        EditorApplication.update += () =>
        {
            if (!File.Exists(Trigger)) return;
            int.TryParse(File.ReadAllText(Trigger).Trim(), out int level);
            File.Delete(Trigger);
            UnlockAll();
            SaveData.D.lastDifficulty = Mathf.Clamp(level, 0, Difficulty.All.Length - 1);
            SaveData.Save();
            Debug.Log("[Save] testing difficulty " + Difficulty.S.name);
        };
    }

    [MenuItem("Tools/Jam/Progress/Unlock All Difficulties")]
    static void UnlockAll()
    {
        SaveData.D.highestUnlocked = Difficulty.All.Length - 1;
        SaveData.Save();
        Debug.Log("[Save] all difficulties unlocked");
    }

    [MenuItem("Tools/Jam/Progress/Reset Progress")]
    static void ResetProgress() => SaveData.ResetAll();
}
#endif
