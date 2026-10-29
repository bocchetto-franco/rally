using System;
using System.Globalization;
using System.IO;
using UnityEngine;

// External read-only regression: no scene, input, preferences or game state changes.
public static class RallyFinalTimeCheck
{
    public static void Run()
    {
        Directory.CreateDirectory("Logs");
        const string report = "Logs/final-time-check.txt";
        File.WriteAllText(report, "Canonical race-time formatting regression\n");
        Check(0f, "00:00.000", report);
        Check(12.345f, "00:12.345", report);
        Check(59.9996f, "01:00.000", report);
        Check(60f, "01:00.000", report);
        Check(3599.9998f, "60:00.000", report);
        Check(3600f, "60:00.000", report);
        Check(-1f, "--:--.---", report);
        Check(float.NaN, "--:--.---", report);
        Check(float.PositiveInfinity, "--:--.---", report);
        Check(float.NegativeInfinity, "--:--.---", report);
        Check(float.MaxValue, "--:--.---", report);
        var oldCulture = CultureInfo.CurrentCulture;
        try
        {
            foreach (string culture in new[] { "en-US", "es-AR" })
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
                foreach (float elapsed in new[] { 0f, .0005f, 12.345f, 59.9995f, 59.9996f, 123.4575f, 3599.9998f, 9999.999f })
                {
                    int milliseconds = Mathf.Max(0, Mathf.RoundToInt(elapsed * 1000f));
                    string hud = $"{milliseconds / 60000:00}:{milliseconds / 1000 % 60:00}.{milliseconds % 1000:000}";
                    Check(elapsed, hud, report);
                }
            }
        }
        finally { CultureInfo.CurrentCulture = oldCulture; }
        File.AppendAllText(report, "COMPLETE: PASS; all checks are pure formatting, no Play required.\n");
    }

    static void Check(float elapsed, string expected, string report)
    {
        string actual = RallyGameSession.FormatTime(elapsed);
        if (actual != expected)
            throw new Exception("Time formatting mismatch: " + elapsed.ToString("R", CultureInfo.InvariantCulture) + " expected " + expected + " got " + actual);
        File.AppendAllText(report, "PASS " + elapsed.ToString("R", CultureInfo.InvariantCulture) + " -> " + actual + "\n");
    }
}
