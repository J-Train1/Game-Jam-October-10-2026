using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;

// One-shot: copies the interesting parts of the previous editor session's log
// into CrashReport2.cs so it can be read.
public static class JamCrashLog
{
    const string OutPath = "Assets/Scripts/Editor/CrashReport2.cs";

    [InitializeOnLoadMethod]
    static void Run()
    {
        if (File.Exists(OutPath)) return;
        var sb = new StringBuilder();
        try
        {
            string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Unity", "Editor");
            string prev = Path.Combine(dir, "Editor-prev.log");
            sb.AppendLine("log: " + prev + " exists=" + File.Exists(prev) + " modified=" + (File.Exists(prev) ? File.GetLastWriteTime(prev).ToString("u") : "-"));
            if (File.Exists(prev))
            {
                string[] lines;
                using (var fs = new FileStream(prev, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var sr = new StreamReader(fs)) lines = sr.ReadToEnd().Split('\n');
                sb.AppendLine("total lines: " + lines.Length);

                var keys = new[] { "[Maze]", "[OuterCorn]", "[Jam]", "Exception", "error CS", "D3D12", "Device", "Crash", "out of memory",
                                   "Hold on", "busy", "Reloading assemblies", "Domain Reload", "Refresh completed", "OnValidate", "Saving", "Saved scene" };
                sb.AppendLine("---- matching lines (last 80) ----");
                foreach (var l in lines.Where(l => keys.Any(k => l.Contains(k)) && !l.Contains(".dll:")).TakeLast(80))
                    sb.AppendLine(Trim(l));
                sb.AppendLine("---- last 40 lines (excluding module list) ----");
                foreach (var l in lines.Where(l => !l.Contains(".dll:")).TakeLast(40)) sb.AppendLine(Trim(l));
            }
        }
        catch (Exception e) { sb.AppendLine("EXCEPTION: " + e.Message); }
        File.WriteAllText(OutPath, "/*\n" + sb.ToString().Replace("*/", "* /") + "*/\n");
    }

    static string Trim(string s)
    {
        s = s.TrimEnd('\r');
        return s.Length > 240 ? s.Substring(0, 240) + "..." : s;
    }
}
