using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class RallyPaceNotesCheck
{
    public static void Install() => RallyBrakeWarningSetup.InstallAll();
    public static void Play() => RallyPaceNotesPlayVerification.Run();
    public static string ArrowGeometry()
    {
        var go = new GameObject("Temporary pace arrow geometry check", typeof(RectTransform));
        go.hideFlags = HideFlags.HideAndDontSave;
        try
        {
            var rect = (RectTransform)go.transform;
            rect.sizeDelta = new Vector2(176f, 122f);
            var arrow = go.AddComponent<RallyPaceNoteArrow>();
            if (go.GetComponent<CanvasRenderer>() == null) throw new Exception("Missing arrow CanvasRenderer.");
            var populate = typeof(RallyPaceNoteArrow).GetMethod("OnPopulateMesh", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.DeclaredOnly);
            int variants = 0;
            for (int grade = 1; grade <= 6; grade++)
            {
                foreach (bool hairpin in new[] { false, true })
                {
                    var positions = new Vector3[2][];
                    for (int side = 0; side < 2; side++)
                    {
                        arrow.SetNote((RallyBrakeWarningTrigger.TurnDirection)side, grade, hairpin);
                        using (var vh = new UnityEngine.UI.VertexHelper())
                        {
                            populate.Invoke(arrow, new object[] { vh });
                            if (vh.currentVertCount != 71) throw new Exception("Unexpected continuous arrow geometry.");
                            positions[side] = new Vector3[vh.currentVertCount];
                            for (int i = 0; i < vh.currentVertCount; i++)
                            {
                                var vertex = new UIVertex();
                                vh.PopulateUIVertex(ref vertex, i);
                                positions[side][i] = vertex.position;
                                if (float.IsNaN(vertex.position.x) || float.IsNaN(vertex.position.y) || !rect.rect.Contains(vertex.position))
                                    throw new Exception("Arrow vertex outside its UI rectangle.");
                            }
                        }
                        variants++;
                    }
                    for (int i = 0; i < positions[0].Length; i++)
                        if (Mathf.Abs(positions[0][i].x + positions[1][i].x) > .001f || Mathf.Abs(positions[0][i].y - positions[1][i].y) > .001f)
                            throw new Exception("Left/right arrows are not mirrored correctly.");
                }
            }
            string result = "COMPLETE: PASS - " + variants + " mirrored arrow variants, continuous strip, CanvasRenderer, finite vertices inside UI bounds.";
            File.WriteAllText("Logs/pace-notes-arrow-geometry.txt", result + "\n");
            return result;
        }
        finally { UnityEngine.Object.DestroyImmediate(go); }
    }
    public static string Status()
    {
        var type = typeof(RallyPaceNotesPlayVerification);
        object Field(string name) => type.GetField(name, System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)?.GetValue(null);
        return "play=" + EditorApplication.isPlaying + " compiling=" + EditorApplication.isCompiling +
            " scene=" + SceneManager.GetActiveScene().name + " dirty=" + SceneManager.GetActiveScene().isDirty +
            " test=" + Field("test") + " phase=" + Field("phase") + " note=" + Field("noteIndex") +
            " frame=" + Time.frameCount + " expectedFrame=" + Field("minimumFrame") + " scale=" + Time.timeScale;
    }

    public static void Audit()
    {
        var active = SceneManager.GetActiveScene();
        var report = new System.Text.StringBuilder();
        try
        {
            foreach (string name in RallyGameSession.CircuitScenes)
            {
                string path = "Assets/Scenes/" + name + ".unity";
                var scene = SceneManager.GetSceneByPath(path);
                bool opened = !scene.isLoaded;
                if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                try
                {
                    var system = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<RallyBrakeWarningSystem>(true)).Single();
                    var triggers = system.GetComponentsInChildren<RallyBrakeWarningTrigger>(true);
                    var waypoints = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)).Single(t => t.name == "AI_Waypoints");
                    var route = waypoints.Cast<Transform>().Select(t => t.position).ToArray();
                    var groups = triggers.GroupBy(t => t.transform.parent).ToArray();
                    if (groups.Length < 6) throw new Exception("Too few notes in " + name);
                    foreach (var group in groups)
                    {
                        var note = group.First();
                        if (!group.All(t => t.NoteLabel == note.NoteLabel && t.IsHairpin == note.IsHairpin && t.CurveEntry == note.CurveEntry &&
                            t.GetComponent<BoxCollider>().isTrigger)) throw new Exception("Inconsistent approach strips: " + group.Key.name);
                        if (note.CornerGrade < 1 || note.CornerGrade > 6 || note.IsHairpin && note.CornerGrade != 1)
                            throw new Exception("Invalid note grade.");
                        if (!group.Any(t => Contains(t.GetComponent<BoxCollider>(), note.CurveEntry + Vector3.up)))
                            throw new Exception("Curve entry outside its trigger coverage: " + group.Key.name);
                        float station = Project(route, note.CurveEntry);
                        // Compare headings inside this bend. An incoming sample before
                        // entry can still be turning the other way in a linked chicane.
                        Vector3 before = Vector3.ProjectOnPlane(Sample(route, station + 4f) - Sample(route, station + 1f), Vector3.up);
                        Vector3 after = Vector3.ProjectOnPlane(Sample(route, station + 18f) - Sample(route, station + 12f), Vector3.up);
                        float turn = Vector3.SignedAngle(before, after, Vector3.up);
                        if (Mathf.Abs(turn) > 4f && (turn > 0f) != (note.Direction == RallyBrakeWarningTrigger.TurnDirection.Right))
                            throw new Exception("Note points opposite to the actual AI/road route: " + group.Key.name + " signed turn=" + turn);
                        report.AppendLine($"  {note.NoteLabel}: route station {station:F0}m, independent turn {turn:F1}deg{(note.IsHairpin ? " hairpin" : "")}");
                    }
                    if (!groups.Any(g => g.First().Direction == RallyBrakeWarningTrigger.TurnDirection.Left) ||
                        !groups.Any(g => g.First().Direction == RallyBrakeWarningTrigger.TurnDirection.Right)) throw new Exception("Missing left/right notes.");
                    report.AppendLine($"PASS {name}: {groups.Length} notes, {triggers.Length} route-aligned approach strips, left/right and grades serialized.");
                }
                finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
            }
            report.AppendLine("COMPLETE: PASS");
        }
        catch (Exception e) { report.AppendLine("FAIL: " + e); throw; }
        finally { if (active.isLoaded) SceneManager.SetActiveScene(active); File.WriteAllText("Logs/pace-notes-audit.txt", report.ToString()); }
    }

    static bool Contains(BoxCollider box, Vector3 point)
    {
        Vector3 local = box.transform.InverseTransformPoint(point) - box.center;
        Vector3 half = box.size * .5f;
        return Mathf.Abs(local.x) <= half.x && Mathf.Abs(local.y) <= half.y && Mathf.Abs(local.z) <= half.z;
    }

    static float Project(Vector3[] points, Vector3 position)
    {
        float best = float.PositiveInfinity, total = 0f, station = 0f;
        for (int i = 0; i < points.Length; i++)
        {
            Vector3 delta = points[(i + 1) % points.Length] - points[i];
            float length = delta.magnitude;
            Vector3 flat = Vector3.ProjectOnPlane(delta, Vector3.up);
            float t = flat.sqrMagnitude < .001f ? 0f : Mathf.Clamp01(Vector3.Dot(Vector3.ProjectOnPlane(position - points[i], Vector3.up), flat) / flat.sqrMagnitude);
            float distance = Vector3.ProjectOnPlane(position - (points[i] + delta * t), Vector3.up).sqrMagnitude;
            if (distance < best) { best = distance; station = total + length * t; }
            total += length;
        }
        return station;
    }

    static Vector3 Sample(Vector3[] points, float distance)
    {
        float length = Enumerable.Range(0, points.Length).Sum(i => Vector3.Distance(points[i], points[(i + 1) % points.Length]));
        distance = (distance % length + length) % length;
        for (int i = 0; i < points.Length; i++)
        {
            Vector3 next = points[(i + 1) % points.Length];
            float span = Vector3.Distance(points[i], next);
            if (distance <= span) return Vector3.Lerp(points[i], next, distance / Mathf.Max(.001f, span));
            distance -= span;
        }
        return points[0];
    }
}
