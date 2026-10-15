using UnityEngine;

// The three difficulties and everything they change. The first one is the original jam game.
// Only the first is open at the start; escaping on one unlocks the next (SaveData.highestUnlocked).
// Each gameplay script reads its numbers from Difficulty.S when the maze loads:
//   MazeGenerator (size), KeyManager (keys), PlayerLives (hearts), PumpkinSpawner (pumpkins at start),
//   PumpkinMonster (chase speed, sight, how far keys/the gate are heard), Flashlight (battery), PlayerController (stamina).
public static class Difficulty
{
    public struct Settings
    {
        public string name, tagline;
        public int mazeCells, keys, hearts, startPumpkins;
        public float huntSpeed, sightRange, batteryLife, stamina, noiseRadius;
    }

    public static readonly Settings[] All =
    {
        new Settings
        {
            name = "LOST", tagline = "Find your way out before they find you.",
            mazeCells = 10, keys = 3, hearts = 3, startPumpkins = 1,
            huntSpeed = 3.8f, sightRange = 12f, batteryLife = 33f, stamina = 5f, noiseRadius = 14f,
        },
        new Settings
        {
            name = "HUNTED", tagline = "A bigger field. A weaker light. They're faster now.",
            mazeCells = 12, keys = 3, hearts = 3, startPumpkins = 1,
            huntSpeed = 4.0f, sightRange = 12f, batteryLife = 25f, stamina = 5f, noiseRadius = 18f,
        },
        new Settings
        {
            name = "HARVESTED", tagline = "One heart. Two of them. Barely any light.",
            mazeCells = 14, keys = 3, hearts = 1, startPumpkins = 2,
            huntSpeed = 4.3f, sightRange = 15f, batteryLife = 5f, stamina = 4f, noiseRadius = 22f,
        },
    };

    /// <summary>The difficulty being played (the one picked on the menu).</summary>
    public static int Current => SaveData.D.lastDifficulty;
    public static Settings S => All[Current];
    public static int HighestUnlocked => SaveData.D.highestUnlocked;
    public static bool IsUnlocked(int level) => level >= 0 && level <= SaveData.D.highestUnlocked;

    /// <summary>Pick a difficulty for the next run (ignored if it's still locked).</summary>
    public static void Select(int level)
    {
        if (!IsUnlocked(level) || level == SaveData.D.lastDifficulty) return;
        SaveData.D.lastDifficulty = level;
        SaveData.Save();
    }

    /// <summary>Called when the player escapes. Records the time; returns the newly unlocked level, or -1.</summary>
    public static int RecordEscape(float seconds)
    {
        var d = SaveData.D;
        int c = Current;
        d.escapes[c]++;
        if (seconds > 0f && (d.bestTime[c] <= 0f || seconds < d.bestTime[c])) d.bestTime[c] = seconds;
        int unlocked = -1;
        if (c + 1 < All.Length && d.highestUnlocked < c + 1) { d.highestUnlocked = c + 1; unlocked = c + 1; }
        SaveData.Save();
        Debug.Log($"[Difficulty] escaped on {All[c].name} in {seconds:0}s" + (unlocked >= 0 ? $" - unlocked {All[unlocked].name}" : ""));
        return unlocked;
    }
}
