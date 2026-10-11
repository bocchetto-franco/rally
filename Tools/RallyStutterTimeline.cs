using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.Profiling;
using UnityEditorInternal;

// Offline inspection of the native Player recording. Never runs in the game.
public static class RallyStutterTimeline
{
    public static string Inspect()
    {
        var text = new StringBuilder();
        foreach (var type in new[] { typeof(ProfilerDriver), typeof(RawFrameDataView) })
        {
            text.AppendLine(type.FullName);
            foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance)
                .Where(m => m.Name == "LoadProfile" || m.Name == "SaveProfile" || m.Name == "GetRawFrameDataView" || m.Name.StartsWith("GetSample")))
                text.AppendLine(method.ToString());
            foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance)
                .Where(p => p.Name.Contains("Frame") || p.Name.Contains("frame") || p.Name == "threadName" || p.Name == "valid" || p.Name == "sampleCount"))
                text.AppendLine(property.ToString());
        }
        return text.ToString();
    }

    public static string Analyze()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new Exception("Editor must be out of Play.");
        const string root = "Logs/Stutter/Diagnostic_Circuit_03_2P_TraceIsolated.csv";
        string profile = Path.GetFullPath(root + ".raw");
        if (!File.Exists(profile)) throw new FileNotFoundException(profile);
        string backup = Path.GetFullPath("Logs/Stutter/EditorBeforeTrace_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".raw");
        bool hadData = ProfilerDriver.firstFrameIndex >= 0;
        // Preserve any existing Editor profiling session before viewing this Player recording.
        if (hadData && !ProfilerDriver.SaveProfile(backup))
            throw new Exception("Existing Editor profile could not be backed up; Player profile was not loaded.");
        var text = new StringBuilder();
        try
        {
            if (!ProfilerDriver.LoadProfile(profile, false)) throw new Exception("Native profile could not be loaded.");
            var frames = new List<KeyValuePair<int, float>>();
            int first = ProfilerDriver.firstFrameIndex, last = ProfilerDriver.lastFrameIndex;
            text.AppendLine("Native Development Timeline; instrumentation/disk overhead means these are NOT Release FPS.");
            text.AppendLine("Loaded frames " + first + ".." + last + "; profile bytes=" + new FileInfo(profile).Length);
            using (var initial = ProfilerDriver.GetRawFrameDataView(first, 0))
                if (initial.valid) text.AppendLine("First native frame start-ms=" + initial.frameStartTimeMs.ToString("F3", CultureInfo.InvariantCulture));
            for (int frame = first; frame <= last; frame++)
                using (var data = ProfilerDriver.GetRawFrameDataView(frame, 0))
                    if (data.valid) frames.Add(new KeyValuePair<int, float>(frame, data.frameTimeMs));
            foreach (var frame in frames.OrderByDescending(f => f.Value).Take(18))
                DescribeFrame(text, frame.Key);
            text.AppendLine("Additional large frames after native frame 500 (not a replacement for the full capture above):");
            foreach (var frame in frames.Where(f => f.Key >= first + 500).OrderByDescending(f => f.Value).Take(12))
                DescribeFrame(text, frame.Key);
        }
        finally
        {
            if (hadData && !ProfilerDriver.LoadProfile(backup, false))
                text.AppendLine("WARNING: Editor profile could not be restored; preserved backup: " + backup);
            File.WriteAllText(root + ".timeline.txt", text.ToString());
        }
        return "Timeline report saved: " + Path.GetFullPath(root + ".timeline.txt")
            + (hadData ? "; original Editor profile preserved at " + backup : "; no previous Editor profile");
    }

    static void DescribeFrame(StringBuilder text, int frame)
    {
        text.AppendLine("\nFRAME " + frame + " (native index; correlate CSV adjacent frames)");
        for (int thread = 0; thread < 64; thread++)
            using (var data = ProfilerDriver.GetRawFrameDataView(frame, thread))
            {
                if (!data.valid) break;
                if (data.threadName != "Main Thread" && !data.threadName.Contains("Render") && !data.threadName.Contains("Gfx")) continue;
                text.AppendLine(data.threadName + "; start-ms=" + data.frameStartTimeMs.ToString("F3", CultureInfo.InvariantCulture)
                    + "; CPU-frame-ms=" + F(data.frameTimeMs) + "; GPU-frame-ms=" + F(data.frameGpuTimeMs) + "; samples=" + data.sampleCount);
                var samples = new List<Region>();
                for (int i = 0; i < data.sampleCount; i++)
                {
                    float total = data.GetSampleTimeMs(i);
                    if (total < .05f) continue;
                    float self = total;
                    int child = i + 1;
                    for (int c = 0; c < data.GetSampleChildrenCount(i) && child < data.sampleCount; c++)
                    {
                        self -= data.GetSampleTimeMs(child);
                        child += 1 + data.GetSampleChildrenCountRecursive(child);
                    }
                    samples.Add(new Region { name = data.GetSampleName(i), total = total, self = Math.Max(0, self) });
                }
                text.AppendLine("Largest inclusive scopes (nested/overlapping, do not sum):");
                foreach (var sample in samples.OrderByDescending(s => s.total).Take(15))
                    text.AppendLine("  " + sample.name + ": inclusive=" + F(sample.total) + "ms; self=" + F(sample.self) + "ms");
                text.AppendLine("Largest self scopes:");
                foreach (var sample in samples.OrderByDescending(s => s.self).Take(12))
                    text.AppendLine("  " + sample.name + ": self=" + F(sample.self) + "ms; inclusive=" + F(sample.total) + "ms");
            }
    }
    struct Region { public string name; public float total, self; }
    static string F(float value) => value.ToString("F3", CultureInfo.InvariantCulture);
}
