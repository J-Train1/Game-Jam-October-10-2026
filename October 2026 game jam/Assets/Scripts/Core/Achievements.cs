using System.Collections.Generic;
using UnityEngine;

// Every achievement in the game, what's been earned (kept in SaveData) and the one place that unlocks them.
// Unlock(id) saves, shows the square pop-up at the top of the screen (AchievementToast) and raises Unlocked,
// which a Steam / mobile / VR version can listen to and mirror onto its own platform. The ids never change.
// Icons live in Resources (Achievements/<icon>.png, or the difficulty pumpkins for the escapes).
public static class Achievements
{
    public class Def
    {
        public string id, name, desc, icon;
        public int target;                  // > 0: shows a progress bar (Progress(id) of target)
        public int needsLevel;              // hidden ("???") until this difficulty is unlocked
        public Def(string id, string name, string desc, string icon, int target = 0, int needsLevel = 0)
        { this.id = id; this.name = name; this.desc = desc; this.icon = icon; this.target = target; this.needsLevel = needsLevel; }
    }

    const string Lost = "Difficulty/Pumpkin_Lost", Hunted = "Difficulty/Pumpkin_Hunted", Harvested = "Difficulty/Pumpkin_Harvested";

    public static readonly Def[] All =
    {
        new Def("FIRST_GAME",      "Into the Corn",        "Wake up in the maze for the first time.",                 "Achievements/footsteps"),
        new Def("HOWTO",           "Read the Fine Print",  "Read every page of How to Play.",                          "Achievements/book", target: 6),
        new Def("FIRST_KEY",       "Finders Keepers",      "Pick up your first rusted key.",                           "Achievements/key"),
        new Def("FIRST_FREEZE",    "Red Light",            "Freeze a pumpkin in your flashlight beam.",                "Achievements/beam"),
        new Def("DOUBLE_FEATURE",  "Double Feature",       "Hold two pumpkins frozen in your light at once.",          "Achievements/beam"),
        new Def("STARING_CONTEST", "Staring Contest",      "Keep a pumpkin frozen until your battery gives out.",      "Achievements/battery"),
        new Def("OUT_OF_BREATH",   "Running on Empty",     "Sprint until you can't catch your breath.",                "Achievements/breath"),
        new Def("FIRST_HEART",     "Heartbroken",          "Lose your first heart.",                                   "Achievements/heart"),
        new Def("ALL_SCARES",      "Front Row Seat",       "Get caught every way they know how. All three of them.",   "Achievements/jaws", target: 3),
        new Def("ONE_LOCK",        "One Down",             "Break a lock off the gate while others still hold.",       "Achievements/padlock"),
        new Def("GATE_OPEN",       "Unbarred",             "Get every lock off the iron gate.",                        "Achievements/padlock"),
        new Def("WIN_LOST",        "Lost and Found",       "Escape on LOST.",                                          Lost),
        new Def("WIN_LOST_FLAWLESS",   "Not a Scratch",    "Escape on LOST without losing a heart.",                   Lost),
        new Def("WIN_HUNTED",      "Outfoxed",             "Escape on HUNTED.",                                        Hunted, needsLevel: 1),
        new Def("WIN_HUNTED_FLAWLESS", "Untouchable",      "Escape on HUNTED without losing a heart.",                 Hunted, needsLevel: 1),
        new Def("WIN_HARVESTED",   "Survived the Harvest", "Escape on HARVESTED.",                                     Harvested, needsLevel: 2),
        new Def("WIN_HARVESTED_FLAWLESS", "Eternal Flame", "Escape on HARVESTED without your flashlight ever dying.",   Harvested, needsLevel: 2),
        new Def("LOSE_LOST",       "Lost Forever",         "Lose every heart on LOST.",                                "Achievements/tombstone"),
        new Def("LOSE_HUNTED",     "Hunted Down",          "Lose every heart on HUNTED.",                              "Achievements/tombstone", needsLevel: 1),
        new Def("LOSE_HARVESTED",  "Harvested",            "Lose your only heart on HARVESTED.",                       "Achievements/tombstone", needsLevel: 2),
        new Def("CARTOGRAPHER",    "Every Row Walked",     "Set foot in every row of a maze in a single run.",         "Achievements/map"),
        new Def("CREDITS",         "Roll Credits",         "Find out who made this nightmare.",                        "Achievements/scroll"),
        new Def("LIGHTS_OUT",      "Pitch Black",          "Escape with the brightness turned all the way down.",      "Achievements/eye"),
        new Def("BLIND_FAITH",     "Afraid of the Light",  "Escape without using your flashlight.",                    "Achievements/flashlight_off"),
        new Def("ALL",             "Pumpkin King",         "Earn every other achievement.",                            "Achievements/crown", target: 24),
    };

    /// <summary>Raised once when an achievement is earned (platform layers hook in here).</summary>
    public static event System.Action<Def> Unlocked;

    static Dictionary<string, Def> byId;
    public static Def Get(string id)
    {
        if (byId == null) { byId = new Dictionary<string, Def>(); foreach (var d in All) byId[d.id] = d; }
        return byId.TryGetValue(id, out var x) ? x : null;
    }

    public static bool Has(string id) => SaveData.D.achievements.Contains(id);

    /// <summary>Still a mystery: belongs to a difficulty the player hasn't opened yet (and isn't earned).</summary>
    public static bool IsHidden(Def d) => !Has(d.id) && !Difficulty.IsUnlocked(d.needsLevel);
    public static int Earned => SaveData.D.achievements.Count;
    public static int Total => All.Length;

    public static void Unlock(string id)
    {
        var def = Get(id);
        if (def == null) { Debug.LogWarning("[Achievements] unknown id " + id); return; }
        if (Has(id)) return;
        SaveData.D.achievements.Add(id);
        SaveData.Save();
        Debug.Log($"[Achievements] unlocked {def.name} ({id})");
        AchievementToast.Show(def);
        Unlocked?.Invoke(def);
        if (id != "ALL" && Earned >= Total - 1 && !Has("ALL")) Unlock("ALL");
    }

    /// <summary>Progress toward a counter achievement (for the bars on the ACHIEVEMENTS screen).</summary>
    public static int Progress(string id)
    {
        var d = SaveData.D;
        switch (id)
        {
            case "HOWTO": return Bits(d.howToSeen);
            case "ALL_SCARES": return Bits(d.scaresSeen);
            case "ALL": return Has("ALL") ? Total - 1 : Earned;
            default: return Has(id) ? 1 : 0;
        }
    }

    static int Bits(int m) { int c = 0; while (m != 0) { c += m & 1; m >>= 1; } return c; }

    // ---------------- hooks called from the game ----------------

    public static void HowToPageSeen(int page, int pageCount)
    {
        var d = SaveData.D;
        int bit = 1 << page;
        if ((d.howToSeen & bit) != 0) return;
        d.howToSeen |= bit;
        SaveData.Save();
        if (Bits(d.howToSeen) >= pageCount) Unlock("HOWTO");
    }

    /// <summary>A jump scare started. variant: 1 = bite, 2 = stab, 3 = spin (the ending's grab doesn't count).</summary>
    public static void SawScare(int variant)
    {
        if (variant < 1 || variant > 3) return;
        var d = SaveData.D;
        d.scaresSeen |= 1 << (variant - 1);
        SaveData.Save();
        if (Bits(d.scaresSeen) >= 3) Unlock("ALL_SCARES");
    }

    /// <summary>A heart was lost; remaining = hearts left after it.</summary>
    public static void LostHeart(int remaining)
    {
        Unlock("FIRST_HEART");
        if (remaining > 0) return;
        switch (Difficulty.Current)
        {
            case 0: Unlock("LOSE_LOST"); break;
            case 1: Unlock("LOSE_HUNTED"); break;
            default: Unlock("LOSE_HARVESTED"); break;
        }
    }

    /// <summary>The player escaped (called by the win screen).</summary>
    public static void Escaped(int level, bool noHeartsLost)
    {
        var run = AchievementTracker.Instance;
        bool batteryDied = run != null && run.BatteryDied;
        float lightTime = run != null ? run.LightOnTime : 999f;
        switch (level)
        {
            case 0: Unlock("WIN_LOST"); if (noHeartsLost) Unlock("WIN_LOST_FLAWLESS"); break;
            case 1: Unlock("WIN_HUNTED"); if (noHeartsLost) Unlock("WIN_HUNTED_FLAWLESS"); break;
            default: Unlock("WIN_HARVESTED"); if (!batteryDied) Unlock("WIN_HARVESTED_FLAWLESS"); break;
        }
        if (GameSettings.Brightness <= -0.99f) Unlock("LIGHTS_OUT");
        if (lightTime <= AchievementTracker.LightGrace) Unlock("BLIND_FAITH");
    }
}
