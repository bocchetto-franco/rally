using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;
using TMPro;
using UnityEngine.TextCore.LowLevel;

/// <summary>Explicit, repeatable capture/export. Source circuits are never saved.</summary>
public static class RoadBlendMenuRefresh
{
    const string Request = "Temp/rally-menu-art.request";
    const string Folder = "Assets/Resources/MenuBackdrops";
    static int meshIndex;
    static double idleSince;
    static string track;
    static readonly Dictionary<Renderer, Renderer> renderers = new Dictionary<Renderer, Renderer>();
    static readonly Dictionary<LODGroup, LODGroup> lods = new Dictionary<LODGroup, LODGroup>();

    static void Poll()
    {
        if (!File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode || Application.isPlaying
            || SessionState.GetBool("Rally.SplitTest.Active", false) || SessionState.GetBool("Rally.RenderBenchmark.Running", false)) { idleSince = 0; return; }
        if (idleSince == 0) { idleSince = EditorApplication.timeSinceStartup; return; }
        if (EditorApplication.timeSinceStartup - idleSince < 2) return;
        idleSince = 0;
        File.Move(Request, Request + ".consumed-" + DateTime.UtcNow.Ticks);
        try { Build(); }
        catch (Exception error) { Directory.CreateDirectory("Logs"); File.WriteAllText("Logs/menu-art.txt", "FAILED\n" + error); Debug.LogException(error); }
    }

    public static void Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play before capturing menu art.");
        // Read saved copies additively: never save, close or discard an already-open scene.
        Scene previous = SceneManager.GetActiveScene(), temporary = default;
        var existing = Enumerable.Range(0, SceneManager.sceneCount).Select(SceneManager.GetSceneAt).ToArray();
        var originalLights = existing.SelectMany(s=>s.GetRootGameObjects()).SelectMany(g=>g.GetComponentsInChildren<Light>(true)).ToDictionary(l=>l,l=>l.cullingMask);
        string before = OpenSceneSignature(existing);
        string snapshot = "Logs/SceneBackups/MenuRoadBlend_" + DateTime.Now.ToString("yyyyMMdd_HHmmss");
        Directory.CreateDirectory(snapshot);
        string temporaryFolder = "Assets/Editor/RoadBlendCapture_" + DateTime.UtcNow.Ticks;
        Directory.CreateDirectory(temporaryFolder);
        File.WriteAllText("Logs/menu-art.txt","Refreshing road-blend backgrounds from saved scenes.\n");
        Directory.CreateDirectory(Folder);
        Directory.CreateDirectory("Assets/Resources/MenuPreviews");
        AssetDatabase.Refresh();
        try
        {
            foreach (var light in originalLights) light.Key.cullingMask &= ~(1 << 31);
            foreach (string name in RallyGameSession.CircuitScenes)
            {
                track = name; meshIndex = 0; renderers.Clear(); lods.Clear();
                Directory.CreateDirectory(Folder + "/Meshes/" + track);
                AssetDatabase.Refresh();
                string sceneCopy = snapshot + "/" + name + ".unity";
                File.Copy("Assets/Scenes/" + name + ".unity", sceneCopy);
                string importedCopy=temporaryFolder+"/"+name+".unity";
                if(!AssetDatabase.CopyAsset("Assets/Scenes/"+name+".unity",importedCopy))throw new IOException("Cannot create isolated preview scene.");
                Scene source = temporary = EditorSceneManager.OpenScene(importedCopy, OpenSceneMode.Additive);
                SceneManager.SetActiveScene(source);
                var waypoints = source.GetRootGameObjects().First(g => g.name == "AI_Waypoints").transform;
                Vector3[] points = Enumerable.Range(0, waypoints.childCount).Select(i => waypoints.GetChild(i).position).ToArray();
                // Frame an actual curved section, not an arbitrary empty center of the map.
                int apex = Enumerable.Range(4, points.Length - 8).OrderByDescending(i =>
                    Vector3.Angle(points[i] - points[i - 3], points[i + 3] - points[i])).First();
                Vector3 forward = (points[apex + 3] - points[apex - 3]).normalized;
                var root = new GameObject(name + " Menu Environment");
                var data = root.AddComponent<RallyMenuBackdropScene>();
                data.focus = points[apex] + Vector3.up * 2f;
                data.cameraPosition = data.focus - forward * 58f + Vector3.Cross(Vector3.up, forward) * 32f + Vector3.up * 35f;
                data.skybox = RenderSettings.skybox;
                data.ambientSky = RenderSettings.ambientSkyColor;
                data.ambientEquator = RenderSettings.ambientEquatorColor;
                data.ambientGround = RenderSettings.ambientGroundColor;
                data.ambientIntensity = RenderSettings.ambientIntensity;
                data.fog = RenderSettings.fog;
                data.fogColor = RenderSettings.fogColor;
                data.fogDensity = RenderSettings.fogDensity;
                data.fogMode = RenderSettings.fogMode;
                foreach (GameObject original in source.GetRootGameObjects())
                    if (original != root) CopyTree(original.transform, root.transform);
                foreach (var pair in lods)
                {
                    LOD[] levels = pair.Key.GetLODs();
                    for (int i = 0; i < levels.Length; i++)
                        levels[i].renderers = levels[i].renderers.Where(r => r != null && renderers.ContainsKey(r)).Select(r => renderers[r]).ToArray();
                    pair.Value.SetLODs(levels);
                }
                if (root.GetComponentsInChildren<Collider>(true).Length != 0 || root.GetComponentsInChildren<Rigidbody>(true).Length != 0)
                    throw new InvalidOperationException("Menu export must contain no gameplay physics.");
                foreach (Light light in source.GetRootGameObjects().Where(g => g != root).SelectMany(g => g.GetComponentsInChildren<Light>())) light.enabled = false;
                Capture(root, data, "Assets/Resources/MenuPreviews/" + name + ".png");
                PrefabUtility.SaveAsPrefabAsset(root, Folder + "/" + name + ".prefab");
                Object.DestroyImmediate(root);
                SceneManager.SetActiveScene(previous);
                EditorSceneManager.CloseScene(temporary, true); temporary = default;
            }
            foreach (string guid in AssetDatabase.FindAssets("t:Mesh",new[]{Folder}))
                AssetDatabase.SaveAssetIfDirty(AssetDatabase.LoadAssetAtPath<Mesh>(AssetDatabase.GUIDToAssetPath(guid)));
            AssetDatabase.Refresh();
            foreach (string name in RallyGameSession.CircuitScenes)
            {
                var importer = (TextureImporter)AssetImporter.GetAtPath("Assets/Resources/MenuPreviews/" + name + ".png");
                importer.maxTextureSize = 1024;
                importer.textureCompression = TextureImporterCompression.Compressed;
                importer.mipmapEnabled = false;
                importer.SaveAndReimport();
            }
            Directory.CreateDirectory("Logs");
            foreach (var light in originalLights) light.Key.cullingMask = light.Value;
            if (before != OpenSceneSignature(existing)) throw new InvalidOperationException("An originally open scene changed during menu export.");
            File.WriteAllText("Logs/menu-art.txt", "COMPLETE: PASS\n3 environment-only prefabs, real 1024x576 previews. Source scenes untouched. LODs preserved.\nOriginally open scenes, including unsaved changes, preserved by signature.\n");
        }
        finally
        {
            SceneManager.SetActiveScene(previous);
            if(temporary.IsValid() && temporary.isLoaded)EditorSceneManager.CloseScene(temporary,true);
            foreach(var light in originalLights)if(light.Key!=null)light.Key.cullingMask=light.Value;
            // These are only this invocation's disposable scene copies; saved sources and Logs backups remain.
            AssetDatabase.DeleteAsset(temporaryFolder);
        }
    }

    static string OpenSceneSignature(Scene[] scenes) => string.Join("\n",scenes.Select(s=>s.path+":"+s.isDirty+":"+string.Join("\n",s.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Component>(true)).Where(c=>c!=null).Select(c=>c.GetEntityId()+":"+EditorJsonUtility.ToJson(c)))));

    static bool HasVisual(Transform source)
    {
        if (!source.gameObject.activeInHierarchy || source.GetComponent<JrsVehicleController>() != null || source.GetComponent<Canvas>() != null) return false;
        Light light = source.GetComponent<Light>();
        if (source.GetComponent<MeshFilter>() != null || source.GetComponent<Terrain>() != null || (light != null && light.type == LightType.Directional && light.enabled)) return true;
        foreach (Transform child in source) if (HasVisual(child)) return true;
        return false;
    }

    static void BuildFont(string name, string source)
    {
        const string directory = "Assets/Resources/MenuFonts";
        Directory.CreateDirectory(directory);
        AssetDatabase.Refresh();
        string path = directory + "/" + name + ".asset";
        TMP_FontAsset asset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
        if (asset == null)
        {
            Font font = AssetDatabase.LoadAssetAtPath<Font>(source);
            asset = TMP_FontAsset.CreateFontAsset(font, 64, 9, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic);
            asset.name = name + " Rally";
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.AddObjectToAsset(asset.material, asset);
            foreach (Texture2D atlas in asset.atlasTextures) AssetDatabase.AddObjectToAsset(atlas, asset);
        }
        string characters = string.Concat(Enumerable.Range(32, 95).Select(i => (char)i)) + "ÁÉÍÓÚÜÑáéíóúüñ¿¡°·/";
        if (!asset.TryAddCharacters(characters, out string missing)) throw new InvalidOperationException("Missing menu font characters: " + missing);
        EditorUtility.SetDirty(asset);
    }

    static void CopyTree(Transform original, Transform parent)
    {
        if (!HasVisual(original)) return;
        var go = new GameObject(original.name) { layer = 31 };
        go.transform.SetParent(parent, false);
        go.transform.localPosition = original.localPosition;
        go.transform.localRotation = original.localRotation;
        go.transform.localScale = original.localScale;
        MeshFilter filter = original.GetComponent<MeshFilter>();
        MeshRenderer renderer = original.GetComponent<MeshRenderer>();
        if (filter != null && filter.sharedMesh != null && renderer != null)
        {
            Mesh mesh = filter.sharedMesh;
            if (!EditorUtility.IsPersistent(mesh))
            {
                string path = Folder + "/Meshes/" + track + "/Mesh_" + meshIndex++ + ".asset";
                Mesh stored = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if (stored == null) { stored = Object.Instantiate(mesh); AssetDatabase.CreateAsset(stored, path); }
                else { EditorUtility.CopySerialized(mesh, stored); EditorUtility.SetDirty(stored); }
                mesh = stored;
            }
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var copy = go.AddComponent<MeshRenderer>();
            EditorUtility.CopySerialized(renderer, copy);
            renderers[renderer] = copy;
        }
        Terrain terrain = original.GetComponent<Terrain>();
        if (terrain != null)
        {
            var copy = go.AddComponent<Terrain>();
            EditorUtility.CopySerialized(terrain, copy);
            copy.drawInstanced = true;
            Collider collider = go.GetComponent<Collider>();
            if (collider != null) Object.DestroyImmediate(collider);
        }
        Light light = original.GetComponent<Light>();
        if (light != null && light.type == LightType.Directional && light.enabled)
        {
            var copy = go.AddComponent<Light>();
            EditorUtility.CopySerialized(light, copy);
            copy.cullingMask = 1 << 31;
        }
        LODGroup lod = original.GetComponent<LODGroup>();
        if (lod != null)
        {
            var copy = go.AddComponent<LODGroup>();
            EditorUtility.CopySerialized(lod, copy);
            lods[lod] = copy;
        }
        foreach (Transform child in original) CopyTree(child, go.transform);
    }

    static void Capture(GameObject root, RallyMenuBackdropScene data, string path)
    {
        var go = new GameObject("Menu capture camera", typeof(Camera));
        Camera camera = go.GetComponent<Camera>();
        camera.cullingMask = 1 << 31; camera.fieldOfView = 55f;
        camera.clearFlags = CameraClearFlags.Skybox; camera.farClipPlane = 1000f;
        Vector3 position = data.cameraPosition;
        foreach (Terrain terrain in root.GetComponentsInChildren<Terrain>())
            position.y = Mathf.Max(position.y, terrain.SampleHeight(position) + terrain.transform.position.y + 12f);
        data.cameraPosition = position;
        go.transform.SetPositionAndRotation(position, Quaternion.LookRotation(data.focus - position));
        var target = new RenderTexture(1024, 576, 24);
        var texture = new Texture2D(1024, 576, TextureFormat.RGB24, false);
        RenderTexture previous = RenderTexture.active;
        try
        {
            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;
            texture.ReadPixels(new Rect(0, 0, 1024, 576), 0, 0); texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
        }
        finally { camera.targetTexture=null; RenderTexture.active = previous; Object.DestroyImmediate(texture); target.Release(); Object.DestroyImmediate(target); Object.DestroyImmediate(go); }
    }
}
