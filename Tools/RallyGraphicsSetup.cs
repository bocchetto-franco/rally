using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class RallyGraphicsSetup
{
    public static void StopVerification()
    {
        var flags = System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic;
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            var type = assembly.GetType("RallySelectionUxCheck");
            if (type == null || (string)type.GetField("Report", flags)?.GetValue(null) != "Logs/graphics-atmosphere-play.txt") continue;
            if ((bool)type.GetField("stopping", flags).GetValue(null)) continue;
            type.GetMethod("Finish", flags).Invoke(null, new object[] { new Exception("Restarting after profile asset save repair") });
        }
    }
    const string Folder = "Assets/Art/Atmosphere";
    const string Report = "Logs/graphics-atmosphere-setup.txt";
    public static void InspectNozzles()
    {
        Idle(); var text = new System.Text.StringBuilder();
        foreach (string id in new[] { "LanciaS4", "Peugeot205T16", "AudiQuattro" })
        {
            var root = Load<GameObject>("Assets/Resources/Vehicles/" + id + ".prefab");
            var visual = root.GetComponent<RallyVehicleVisual>();
            var points = root.GetComponentsInChildren<MeshFilter>(true).SelectMany(m => m.sharedMesh.vertices.Select(v => root.transform.InverseTransformPoint(m.transform.TransformPoint(v))))
                .Where(v => v.z < visual.bodyBounds.min.z + .25f && v.y < visual.bodyBounds.min.y + .4f)
                .GroupBy(v => new Vector3(Mathf.Round(v.x * 20) / 20, Mathf.Round(v.y * 20) / 20, Mathf.Round(v.z * 20) / 20))
                .OrderBy(g => g.Key.z).ThenBy(g => g.Key.y).ToArray();
            text.AppendLine(id + " bounds " + visual.bodyBounds);
            foreach (var g in points.Take(60)) text.AppendLine(g.Key.ToString("F2") + " vertices=" + g.Count());
        }
        File.WriteAllText("Logs/exhaust-anchor-measurements.txt", text.ToString());
    }
    static void Log(string message) => File.AppendAllText(Report, message + "\n");
    static T Load<T>(string path) where T : UnityEngine.Object => AssetDatabase.LoadAssetAtPath<T>(path) ?? throw new Exception("Missing " + path);
    static void Idle()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) throw new Exception("Unity must be idle");
        for (int i = 0; i < SceneManager.sceneCount; i++) if (SceneManager.GetSceneAt(i).isDirty) throw new Exception("Unsaved scene: " + SceneManager.GetSceneAt(i).path);
    }
    public static void Inspect()
    {
        Idle(); File.WriteAllText(Report, "Graphics inspection\n");
        var setup = EditorSceneManager.GetSceneManagerSetup();
        try
        {
            foreach (string name in new[] { "Circuit_01", "Circuit_03" })
            {
                var scene = EditorSceneManager.OpenScene("Assets/Scenes/" + name + ".unity");
                var sun = RenderSettings.sun;
                Log(name + " Sun=" + (sun ? sun.transform.eulerAngles + " color=" + sun.color + " intensity=" + sun.intensity : "NONE"));
                foreach (var root in scene.GetRootGameObjects())
                    foreach (var r in root.GetComponentsInChildren<Renderer>(true))
                        if (r.name.Contains("Road") && r.sharedMaterial != null) Log(r.name + " = " + AssetDatabase.GetAssetPath(r.sharedMaterial));
                var car = UnityEngine.Object.FindObjectsByType<JrsVehicleController>().First(c => c.rearLeftDustParticleSystem != null);
                Log("Smoke material " + AssetDatabase.GetAssetPath(car.rearLeftDustParticleSystem.GetComponent<ParticleSystemRenderer>().sharedMaterial));
            }
        }
        finally { EditorSceneManager.RestoreSceneManagerSetup(setup); }
    }
    static Material Clone(string source, string name)
    {
        string path = Folder + "/" + name + ".mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null) { mat = new Material(Load<Material>(source)) { name = name }; AssetDatabase.CreateAsset(mat, path); }
        return mat;
    }
    static RallyAtmosphereProfile Profile(string name)
    {
        string path = Folder + "/" + name + ".asset";
        var value = AssetDatabase.LoadAssetAtPath<RallyAtmosphereProfile>(path);
        if (value == null) { value = ScriptableObject.CreateInstance<RallyAtmosphereProfile>(); AssetDatabase.CreateAsset(value, path); }
        return value;
    }
    static Dictionary<Component, string> Snapshot(Scene scene) => scene.GetRootGameObjects()
        .SelectMany(r => r.GetComponentsInChildren<Component>(true)).Where(c => c != null)
        .ToDictionary(c => c, c => EditorJsonUtility.ToJson(c));

    public static void Apply()
    {
        Idle(); Directory.CreateDirectory("Logs/SceneBackups/Atmosphere_20261020");
        File.WriteAllText(Report, "Graphics-only profile installation\n");
        var setup = EditorSceneManager.GetSceneManagerSetup();
        if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Art", "Atmosphere");
        try
        {
            var sunset = Profile("Circuit01 - Sunset");
            var sunsetSky = Clone("Assets/Art/Environment/Materials/Sky - warm procedural desert.mat", "Sunset - warm horizon");
            sunsetSky.SetColor("_SkyTint", new Color(.64f, .44f, .49f)); sunsetSky.SetColor("_GroundColor", new Color(.43f, .29f, .22f));
            sunsetSky.SetFloat("_AtmosphereThickness", 1.4f); sunsetSky.SetFloat("_Exposure", 1.1f);
            sunset.skybox = sunsetSky; sunset.sunEuler = new Vector3(16, -38, 0); sunset.sunIntensity = 1.25f;
            sunset.sunColor = new Color(1, .72f, .46f); sunset.shadowStrength = .85f;
            sunset.ambientSky = new Color(.58f, .48f, .51f); sunset.ambientEquator = new Color(.51f, .40f, .29f); sunset.ambientGround = new Color(.27f, .23f, .21f);
            sunset.fogStart = 140; sunset.fogEnd = 440; sunset.fogColor = new Color(.71f, .53f, .43f); sunset.rain = false;

            var forest = Profile("Circuit03 - Rain");
            forest.skybox = Load<Material>("Assets/Art/Forest/Materials/Overcast sky.mat");
            forest.sunEuler = new Vector3(48, -30, 0); forest.sunColor = new Color(.78f, .86f, .94f); forest.sunIntensity = .85f; forest.shadowStrength = .65f;
            forest.ambientSky = new Color(.49f, .56f, .61f); forest.ambientEquator = new Color(.36f, .42f, .42f); forest.ambientGround = new Color(.24f, .27f, .27f);
            forest.fogStart = 55; forest.fogEnd = 290; forest.fogColor = new Color(.53f, .61f, .65f); forest.rain = true;
            forest.originalRoadMaterial = Load<Material>("Assets/Art/Forest/Materials/Wet forest road.mat");
            var wet = Clone("Assets/Art/Forest/Materials/Wet forest road.mat", "Forest road - rainfall");
            wet.SetColor("_BaseColor", new Color(.60f, .67f, .61f)); wet.SetColor("_Color", new Color(.60f, .67f, .61f));
            // Keep normal/albedo/AO textures; uniform water film covers the dry roughness map.
            wet.SetTexture("_MetallicGlossMap", null); wet.DisableKeyword("_METALLICSPECGLOSSMAP");
            wet.SetFloat("_Metallic", 0); wet.SetFloat("_Smoothness", .52f); wet.SetFloat("_EnvironmentReflections", 1); wet.SetFloat("_SpecularHighlights", 1);
            forest.wetRoadMaterial = wet;
            foreach (var asset in new UnityEngine.Object[] { sunset, forest, sunsetSky, wet }) EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets(); // Scene switches unload unreferenced assets; persist before opening scenes.
            foreach (var pair in new[] { ("Circuit_01", "Circuit01 - Sunset"), ("Circuit_03", "Circuit03 - Rain") })
            {
                string path = "Assets/Scenes/" + pair.Item1 + ".unity";
                string backup = "Logs/SceneBackups/Atmosphere_20261020/" + pair.Item1 + ".unity";
                if (!File.Exists(backup)) File.Copy(path, backup);
                var scene = EditorSceneManager.OpenScene(path);
                var activeProfile = Load<RallyAtmosphereProfile>(Folder + "/" + pair.Item2 + ".asset");
                var existing = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<RallyCircuitAtmosphere>(true)).FirstOrDefault();
                var before = Snapshot(scene);
                if (activeProfile.rain)
                {
                    var car = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<JrsVehicleController>(true)).First(c => c.rearLeftDustParticleSystem != null);
                    string source = AssetDatabase.GetAssetPath(car.rearLeftDustParticleSystem.GetComponent<ParticleSystemRenderer>().sharedMaterial);
                    var rain = Clone(source, "Rain - existing particle shader");
                    if (rain.HasProperty("_BaseColor")) rain.SetColor("_BaseColor", Color.white);
                    if (rain.HasProperty("_SoftParticleFadeParams")) rain.SetVector("_SoftParticleFadeParams", new Vector4(0, 2.5f, 0, 0));
                    activeProfile.rainMaterial = rain;
                    EditorUtility.SetDirty(rain); EditorUtility.SetDirty(activeProfile); AssetDatabase.SaveAssets();
                    Log("Rain reuses " + source + " / shader " + rain.shader.name);
                }
                if (existing == null)
                {
                    var go = new GameObject(pair.Item1 == "Circuit_01" ? "Circuit 01 - Sunset Profile" : "Circuit 03 - Rain Profile");
                    go.AddComponent<RallyCircuitAtmosphere>().profile = activeProfile;
                }
                else if (existing.profile != activeProfile) throw new Exception("Existing profile differs; refusing to overwrite it");
                foreach (var root in scene.GetRootGameObjects()) foreach (var c in root.GetComponentsInChildren<Component>(true))
                    if (c != null && before.TryGetValue(c, out var json) && json != EditorJsonUtility.ToJson(c))
                        throw new Exception("Protected component changed: " + c.name + " " + c.GetType());
                EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
                Log(pair.Item1 + " PASS: " + before.Count + " existing components unchanged; added one profile root only.");
            }
            Log("COMPLETE: PASS. C02 untouched; no terrain data, geometry, car physics, checkpoints or waypoints edited.");
        }
        finally { EditorSceneManager.RestoreSceneManagerSetup(setup); }
    }
}
