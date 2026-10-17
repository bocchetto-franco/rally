using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Asset-only visual edit: scene bytes and every activation parameter stay identical.
public static class RallyTurboVisualUpgrade
{
    const string Folder = "Assets/Art/RallyTurbo/";
    static IEnumerable<T> Components<T>(Scene s) where T : Component => s.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<T>(true));
    static string Hash(string path) { using (var sha = SHA256.Create()) return Convert.ToBase64String(sha.ComputeHash(File.ReadAllBytes(path))); }
    public static string Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) throw new Exception("Unity must be idle");
        string backup = "Logs/SceneBackups/TurboVisual_" + DateTime.Now.ToString("yyyyMMdd_HHmmss"); Directory.CreateDirectory(backup);
        var paths = RallyGameSession.CircuitScenes.Select(s => "Assets/Scenes/" + s + ".unity").ToArray();
        var hashes = paths.Select(Hash).ToArray();
        var material = AssetDatabase.LoadAssetAtPath<Material>(Folder + "Turbo yellow.mat");
        if (material == null) throw new Exception("Existing arrow material not found");
        var pending = new List<Tuple<Mesh, List<Vector3>, List<int>>>();
        // Validate all nine meshes before changing a shared asset.
        foreach (string path in paths)
        {
            var scene = EditorSceneManager.OpenPreviewScene(path);
            try
            {
                var zones = Components<RallyTurboZone>(scene).ToArray(); if (zones.Length != 3) throw new Exception("Expected three zones: " + path);
                var roads = Components<Collider>(scene).Where(c => c.enabled && !c.isTrigger &&
                    new[] { "Rally_Road_Start_to_Finish", "Circuit02_Road", "Circuit03_Road", "Continuous smooth deck collider" }.Contains(c.name)).ToArray();
                foreach (var zone in zones)
                {
                    var filter = zone.GetComponentInChildren<MeshFilter>();
                    var mesh = filter.sharedMesh;
                    if (mesh == null || !AssetDatabase.GetAssetPath(mesh).StartsWith(Folder)) throw new Exception("Unexpected marker mesh");
                    var vertices = new List<Vector3>(); var indices = new List<int>();
                    Action<Vector2[]> polygon = points =>
                    {
                        int first = vertices.Count;
                        foreach (var p in points)
                        {
                            Vector3 world = zone.transform.TransformPoint(new Vector3(p.x, 0, p.y)); RaycastHit best = default; bool found = false;
                            foreach (var road in roads)
                                if (road.Raycast(new Ray(world + Vector3.up * 30, Vector3.down), out var hit, 65) && (!found || hit.point.y > best.point.y)) { best = hit; found = true; }
                            if (!found) throw new Exception("Larger arrow is not supported by the existing road: " + zone.name);
                            vertices.Add(filter.transform.InverseTransformPoint(best.point + best.normal * .035f));
                        }
                        for (int i = 1; i < points.Length - 1; i++) indices.AddRange(new[] { first, first + i + 1, first + i });
                    };
                    foreach (float z in new[] { -3.3f, -1.1f, 1.1f, 3.3f })
                    {
                        polygon(new[] { new Vector2(-1.2f,z-.85f),new Vector2(0,z+.2f),new Vector2(0,z+.85f),new Vector2(-1.2f,z-.2f) });
                        polygon(new[] { new Vector2(0,z+.2f),new Vector2(1.2f,z-.85f),new Vector2(1.2f,z-.2f),new Vector2(0,z+.85f) });
                    }
                    pending.Add(Tuple.Create(mesh, vertices, indices));
                }
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }
        foreach (var entry in pending) File.Copy(AssetDatabase.GetAssetPath(entry.Item1), backup + "/" + Path.GetFileName(AssetDatabase.GetAssetPath(entry.Item1)));
        File.Copy(AssetDatabase.GetAssetPath(material), backup + "/Turbo yellow.mat");
        foreach (var entry in pending)
        {
            var mesh = entry.Item1; mesh.Clear(); mesh.SetVertices(entry.Item2); mesh.subMeshCount = 2;
            mesh.SetTriangles(Array.Empty<int>(), 0); mesh.SetTriangles(entry.Item3, 1); mesh.RecalculateNormals(); mesh.RecalculateBounds(); EditorUtility.SetDirty(mesh);
        }
        material.SetColor("_BaseColor", new Color(1, .88f, .3f, .55f)); material.SetColor("_Color", new Color(1, .88f, .3f, .55f));
        material.SetFloat("_Surface", 1); material.SetFloat("_Blend", 0); material.SetFloat("_AlphaClip", 0);
        BaseShaderGUI.SetupMaterialBlendMode(material); material.SetShaderPassEnabled("ShadowCaster", false); EditorUtility.SetDirty(material);
        AssetDatabase.SaveAssets();
        for (int i = 0; i < paths.Length; i++) if (Hash(paths[i]) != hashes[i]) throw new Exception("Scene bytes changed: " + paths[i]);
        string result = "Backup: " + backup + "\nCOMPLETE: PASS - nine arrow meshes, no backing triangles, 2.4 x 8.3m, alpha 0.55; all three scene files byte-identical\n";
        File.WriteAllText("Logs/turbo-visual-upgrade.txt", result); return result;
    }
}
