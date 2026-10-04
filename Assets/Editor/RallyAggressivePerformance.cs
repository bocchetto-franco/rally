using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>Explicit, backed-up render optimization of the three race scenes.</summary>
[InitializeOnLoad]
public static class RallyAggressivePerformance
{
    const string Request = "Logs/aggressive-performance-request.txt";
    const string Report = "Logs/aggressive-performance.txt";
    const string Generated = "Assets/Art/PerformanceLOD";
    static readonly Dictionary<string, Mesh> meshes = new Dictionary<string, Mesh>();
    static readonly Dictionary<string, Material> materials = new Dictionary<string, Material>();
    static int current = -1;
    static string previousScene, backup;
    static bool baking;
    static DateTime bakeStarted;

    static RallyAggressivePerformance()
    {
        current = SessionState.GetInt("Rally.Performance.Scene", -1);
        baking = SessionState.GetBool("Rally.Performance.Baking", false);
        backup = SessionState.GetString("Rally.Performance.Backup", "");
        previousScene = SessionState.GetString("Rally.Performance.Previous", "");
        long.TryParse(SessionState.GetString("Rally.Performance.BakeTicks", "0"), out long ticks);
        bakeStarted = new DateTime(ticks, DateTimeKind.Utc);
        EditorApplication.update += Poll;
    }

    static void Remember()
    {
        SessionState.SetInt("Rally.Performance.Scene", current);
        SessionState.SetBool("Rally.Performance.Baking", baking);
        SessionState.SetString("Rally.Performance.Backup", backup ?? "");
        SessionState.SetString("Rally.Performance.Previous", previousScene ?? "");
        SessionState.SetString("Rally.Performance.BakeTicks", bakeStarted.Ticks.ToString());
    }

    [MenuItem("Tools/Rally/Performance/Apply Aggressive Render Optimization")]
    public static void Start()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Save the current scene and leave Play before optimization.");
        Scene active = EditorSceneManager.GetActiveScene();
        if (active.isDirty)
        {
            // Recover the interrupted bake from this explicit optimization only.
            // Preserve the in-memory copy before reopening the saved scene; never
            // silently overwrite an unrelated user-edited scene.
            string prior = File.Exists(Report) ? File.ReadAllText(Report) : "";
            if (active.path != "Assets/Scenes/Circuit_01.unity" || !prior.Contains("BAKING Circuit_01"))
                throw new InvalidOperationException("Save your unrelated scene changes before optimization.");
            string recovery = "Logs/SceneBackups/InterruptedPerformance_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".unity";
            Directory.CreateDirectory("Logs/SceneBackups");
            if (!EditorSceneManager.SaveScene(active, recovery, true))
                throw new InvalidOperationException("Could not preserve the interrupted scene.");
            EditorSceneManager.OpenScene(active.path);
        }
        if (current >= 0) return;
        previousScene = EditorSceneManager.GetActiveScene().path;
        backup = "Logs/SceneBackups/AggressivePerformance_" + DateTime.Now.ToString("yyyyMMdd_HHmmss");
        Directory.CreateDirectory(backup);
        File.WriteAllText(Report, "Aggressive render optimization " + DateTime.Now.ToString("O") + "\nBackup: " + backup + "\n");
        Directory.CreateDirectory(Generated);
        AssetDatabase.Refresh();
        OptimizeTextures();
        OptimizePipeline();
        OptimizeHeavyTree();
        current = 0;
        Remember();
        OptimizeScene();
    }

    static void Poll()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        try
        {
            if (File.Exists(Request) && current < 0 && !SessionState.GetBool("Rally.SplitTest.Active", false) &&
                !File.Exists("Temp/rally-split-screen-test.request") && !SessionState.GetBool("Rally.RenderBenchmark.Running", false))
            {
                File.Move(Request, Request + ".consumed-" + DateTime.Now.Ticks);
                Start();
            }
            else if (baking && !StaticOcclusionCulling.isRunning)
            {
                string path = "Assets/Scenes/Circuit_0" + (current + 1) + ".unity";
                string data = Path.ChangeExtension(path, null) + "/OcclusionCullingData.asset";
                if (!File.Exists(data) || new FileInfo(data).Length < 100 || File.GetLastWriteTimeUtc(data) < bakeStarted)
                    throw new InvalidOperationException("Occlusion data missing after bake: " + data);
                EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
                File.AppendAllText(Report, "BAKE OK " + data + " (" + new FileInfo(data).Length + " bytes)\n");
                baking = false;
                current++;
                Remember();
                if (current < 3) OptimizeScene();
                else
                {
                    AssetDatabase.SaveAssets();
                    if (!string.IsNullOrEmpty(previousScene)) EditorSceneManager.OpenScene(previousScene);
                    File.AppendAllText(Report, "COMPLETE: PASS\n");
                    current = -1;
                    Remember();
                }
            }
        }
        catch (Exception e)
        {
            File.AppendAllText(Report, "ERROR: " + e + "\n");
            current = -1; baking = false;
            Remember();
            Debug.LogException(e);
        }
    }

    static void OptimizeTextures()
    {
        int examined = 0, changed = 0;
        foreach (string path in AssetDatabase.GetAllAssetPaths().Where(p => p.StartsWith("Assets/", StringComparison.Ordinal)))
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) continue;
            examined++;
            bool ui = importer.textureType == TextureImporterType.Sprite || path.IndexOf("/UI/", StringComparison.OrdinalIgnoreCase) >= 0;
            bool vegetation = path.IndexOf("grass", StringComparison.OrdinalIgnoreCase) >= 0 || path.IndexOf("bush", StringComparison.OrdinalIgnoreCase) >= 0 ||
                path.IndexOf("searsia", StringComparison.OrdinalIgnoreCase) >= 0 || path.IndexOf("quiver", StringComparison.OrdinalIgnoreCase) >= 0;
            int limit = ui || vegetation ? 1024 : 2048;
            bool dirty = importer.maxTextureSize > limit || importer.textureCompression == TextureImporterCompression.Uncompressed;
            if (dirty) File.Copy(path + ".meta", backup + "/texture_" + AssetDatabase.AssetPathToGUID(path) + ".meta", true);
            importer.maxTextureSize = Mathf.Min(importer.maxTextureSize, limit);
            if (importer.textureCompression == TextureImporterCompression.Uncompressed)
                importer.textureCompression = TextureImporterCompression.Compressed;
            // Explicit overrides otherwise bypass the default cap/compression.
            foreach (string platform in new[] { "Standalone", "Android", "iPhone", "WebGL" })
            {
                var settings = importer.GetPlatformTextureSettings(platform);
                if (!settings.overridden) continue;
                bool uncompressed = settings.format == TextureImporterFormat.RGBA32 || settings.format == TextureImporterFormat.RGB24 ||
                    settings.format == TextureImporterFormat.RGBAHalf || settings.format == TextureImporterFormat.RGBAFloat;
                if (settings.maxTextureSize <= limit && settings.textureCompression != TextureImporterCompression.Uncompressed && !uncompressed) continue;
                if (!dirty) File.Copy(path + ".meta", backup + "/texture_" + AssetDatabase.AssetPathToGUID(path) + ".meta", true);
                settings.maxTextureSize = Mathf.Min(settings.maxTextureSize, limit);
                settings.textureCompression = TextureImporterCompression.Compressed;
                settings.format = TextureImporterFormat.Automatic;
                importer.SetPlatformTextureSettings(settings);
                dirty = true;
            }
            if (dirty) { importer.SaveAndReimport(); changed++; }
        }
        File.AppendAllText(Report, $"Textures: {examined} importers audited, {changed} corrected; <=2048, UI/foliage <=1024, compressed including overrides.\n");
    }

    static void OptimizePipeline()
    {
        string path = "Assets/Settings/PC_RPAsset.asset";
        File.Copy(path, backup + "/PC_RPAsset.asset", true);
        var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(path);
        pipeline.renderScale = .7f;
        pipeline.shadowDistance = 30f;
        pipeline.shadowCascadeCount = 1;
        pipeline.mainLightShadowmapResolution = 512;
        pipeline.supportsHDR = false;
        pipeline.msaaSampleCount = 1;
        pipeline.useSRPBatcher = true;
        var serialized = new SerializedObject(pipeline);
        serialized.FindProperty("m_SoftShadowQuality").intValue = 1;
        serialized.FindProperty("m_AdditionalLightShadowsSupported").boolValue = false;
        // This project hit invalid BatchDrawCommand IDs when unloading the menu
        // backdrop. Use the stable SRP/material batching path rather than shipping
        // a render optimization which floods the console on scene transitions.
        serialized.FindProperty("m_GPUResidentDrawerMode").intValue = 0;
        serialized.FindProperty("m_GPUResidentDrawerEnableOcclusionCullingInCameras").boolValue = false;
        serialized.FindProperty("m_OpaqueDownsampling").intValue = 2;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(pipeline);
        File.Copy("ProjectSettings/QualitySettings.asset", backup + "/QualitySettings.asset", true);
        QualitySettings.lodBias = .5f;
        File.Copy("ProjectSettings/ProjectSettings.asset", backup + "/ProjectSettings.asset", true);
        var settingsObject = Resources.FindObjectsOfTypeAll<PlayerSettings>().FirstOrDefault();
        if (settingsObject == null) throw new InvalidOperationException("Player settings object not available.");
        var playerSettings = new SerializedObject(settingsObject);
        SerializedProperty batching = playerSettings.FindProperty("m_BuildTargetBatching");
        if (batching == null) throw new InvalidOperationException("Player batching settings not found.");
        int standalone = -1;
        for (int i = 0; i < batching.arraySize; i++)
            if (batching.GetArrayElementAtIndex(i).FindPropertyRelative("m_BuildTarget").stringValue == "Standalone") standalone = i;
        if (standalone < 0) { standalone = batching.arraySize; batching.InsertArrayElementAtIndex(standalone); }
        SerializedProperty target = batching.GetArrayElementAtIndex(standalone);
        target.FindPropertyRelative("m_BuildTarget").stringValue = "Standalone";
        target.FindPropertyRelative("m_StaticBatching").boolValue = true;
        target.FindPropertyRelative("m_DynamicBatching").boolValue = false;
        playerSettings.ApplyModifiedPropertiesWithoutUndo();
        File.AppendAllText(Report, "URP: SRP Batcher, instanced/shared scenery materials and selective static batching (crowds/rocks); GPU Resident Drawer disabled due to invalid draw-command IDs on scene unload. Shadows 512/1 cascade/low soft quality, no additional-light shadows; opaque copy 4x box. Race scale .70 single/.55 split; split shadow range20m, second view without shadows.\n");
    }

    static void OptimizeHeavyTree()
    {
        const string path = "Assets/Art/Environment/Prefabs/quiver_tree_01_optimized.asset";
        Mesh source = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (source == null || !source.isReadable || source.triangles.Length / 3 < 100000) return;
        int before = source.triangles.Length / 3;
        Mesh reduced = ReducedMesh(source, .07f);
        if (reduced == null) throw new InvalidOperationException("Heavy Quiver tree could not be reduced safely.");
        File.Copy(path, backup + "/quiver_tree_01_optimized.asset", true);
        File.Copy(path + ".meta", backup + "/quiver_tree_01_optimized.asset.meta", true);
        string name = source.name;
        EditorUtility.CopySerialized(reduced, source);
        source.name = name;
        EditorUtility.SetDirty(source);
        AssetDatabase.SaveAssets();
        File.AppendAllText(Report, $"Heavy Quiver base mesh: {before} -> {source.triangles.Length / 3} triangles at ALL distances; source FBX untouched, native asset backed up.\n");
    }

    static void OptimizeScene()
    {
        string path = "Assets/Scenes/Circuit_0" + (current + 1) + ".unity";
        File.Copy(path, backup + "/Circuit_0" + (current + 1) + ".unity", true);
        string occlusion = Path.ChangeExtension(path, null) + "/OcclusionCullingData.asset";
        if (File.Exists(occlusion)) File.Copy(occlusion, backup + "/Circuit_0" + (current + 1) + "_OcclusionCullingData.asset", true);
        Scene scene = EditorSceneManager.OpenScene(path);
        string signature = GameplaySignature(scene);
        var all = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToArray();
        int groups = 0, simplified = 0, duplicates = 0, renderers = 0;
        foreach (LODGroup group in all.Select(t => t.GetComponent<LODGroup>()).Where(g => g != null))
        {
            if (!Scenery(group.transform)) continue;
            LOD[] levels = group.GetLODs();
            if (levels.Length == 0) continue;
            Renderer[] near = levels[0].renderers.Where(r => r != null).ToArray();
            bool crowd = Crowd(group.transform);
            float cull = crowd ? .065f : .035f;
            if (levels.Length == 1)
            {
                var reduced = new List<Renderer>();
                foreach (MeshRenderer renderer in near.OfType<MeshRenderer>())
                {
                    MeshFilter source = renderer.GetComponent<MeshFilter>();
                    if (source == null || source.sharedMesh == null || source.sharedMesh.vertexCount < 600) continue;
                    Mesh mesh = ReducedMesh(source.sharedMesh, crowd ? .1f : .07f);
                    if (mesh == null) continue;
                    var child = new GameObject("Performance LOD1 - " + renderer.name, typeof(MeshFilter), typeof(MeshRenderer));
                    child.transform.SetParent(renderer.transform, false);
                    child.GetComponent<MeshFilter>().sharedMesh = mesh;
                    var target = child.GetComponent<MeshRenderer>();
                    target.sharedMaterials = renderer.sharedMaterials;
                    target.shadowCastingMode = ShadowCastingMode.Off;
                    target.receiveShadows = renderer.receiveShadows;
                    target.lightProbeUsage = LightProbeUsage.Off;
                    target.reflectionProbeUsage = ReflectionProbeUsage.Off;
                    reduced.Add(target);
                    simplified++;
                }
                if (reduced.Count > 0)
                {
                    // Retain small source renderers for which simplification was not useful.
                    reduced.AddRange(near.Where(r => r.GetComponent<MeshFilter>() == null ||
                        !r.transform.Cast<Transform>().Any(t => t.name.StartsWith("Performance LOD1 - "))));
                    group.SetLODs(new[] { new LOD(.18f, near), new LOD(cull, reduced.ToArray()) });
                }
                else group.SetLODs(new[] { new LOD(cull, near) });
            }
            else
            {
                for (int i = 0; i < levels.Length; i++)
                    levels[i].screenRelativeTransitionHeight = Mathf.Lerp(.3f, cull, i / (float)(levels.Length - 1));
                group.SetLODs(levels);
            }
            group.fadeMode = LODFadeMode.None;
            group.RecalculateBounds();
            EditorUtility.SetDirty(group);
            groups++;
        }
        all = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToArray();
        foreach (var renderer in all.Select(t => t.GetComponent<MeshRenderer>()).Where(r => r != null && Scenery(r.transform)))
        {
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            renderer.motionVectorGenerationMode = MotionVectorGenerationMode.Camera;
            var flags = GameObjectUtility.GetStaticEditorFlags(renderer.gameObject);
            flags &= ~StaticEditorFlags.BatchingStatic;
            if (!Vegetation(renderer.transform)) flags |= StaticEditorFlags.BatchingStatic;
            flags |= StaticEditorFlags.OccludeeStatic;
            // Leaves and sparse crowds are poor occluders; do not create false walls.
            if (renderer.bounds.size.sqrMagnitude > 100 && !Crowd(renderer.transform) && !Vegetation(renderer.transform))
                flags |= StaticEditorFlags.OccluderStatic;
            else flags &= ~StaticEditorFlags.OccluderStatic;
            GameObjectUtility.SetStaticEditorFlags(renderer.gameObject, flags);
            Material[] shared = renderer.sharedMaterials;
            for (int i = 0; i < shared.Length; i++)
            {
                Material material = shared[i];
                if (material == null) continue;
                material.enableInstancing = true;
                EditorUtility.SetDirty(material);
                string key = EditorJsonUtility.ToJson(material);
                int name = key.IndexOf("\"m_Name\"", StringComparison.Ordinal);
                if (name >= 0) key = System.Text.RegularExpressions.Regex.Replace(key, "\"m_Name\":\\s*\"[^\"]*\"", "\"m_Name\":\"\"");
                if (materials.TryGetValue(key, out Material existing) && existing != material) { shared[i] = existing; duplicates++; }
                else materials[key] = material;
            }
            renderer.sharedMaterials = shared;
            renderers++;
        }
        foreach (Terrain terrain in all.Select(t => t.GetComponent<Terrain>()).Where(t => t != null))
        {
            terrain.detailObjectDistance = 65f;
            terrain.detailObjectDensity = .65f;
            terrain.treeDistance = 650f;
            terrain.treeBillboardDistance = 55f;
            terrain.heightmapPixelError = 20f;
            terrain.basemapDistance = 80f;
            terrain.drawInstanced = true;
            terrain.shadowCastingMode = ShadowCastingMode.Off;
            GameObjectUtility.SetStaticEditorFlags(terrain.gameObject,
                GameObjectUtility.GetStaticEditorFlags(terrain.gameObject) | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic);
        }
        foreach (Camera camera in all.Select(t => t.GetComponent<Camera>()).Where(c => c != null))
        {
            camera.useOcclusionCulling = true;
            camera.allowHDR = false;
            camera.allowMSAA = false;
            camera.farClipPlane = Mathf.Min(camera.farClipPlane, 450f);
            var extra = camera.GetUniversalAdditionalCameraData();
            extra.renderPostProcessing = false;
            extra.antialiasing = AntialiasingMode.None;
            extra.volumeLayerMask = 0;
        }
        foreach (Volume volume in all.Select(t => t.GetComponent<Volume>()).Where(v => v != null))
        {
            // SharedProfile is left intact; cameras skip Volume evaluation/post passes.
            volume.enabled = false;
        }
        if (signature != GameplaySignature(scene)) throw new InvalidOperationException("Gameplay signature changed; scene not saved.");
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        File.AppendAllText(Report, $"{scene.name}: {groups} stricter LODGroups, {simplified} reduced mesh renderers, {renderers} scenery renderers, {duplicates} duplicate material references consolidated; gameplay intact. Terrain details65m/density.65/tree650m/pixelError20/basemap80m.\n");
        StaticOcclusionCulling.smallestOccluder = 8f;
        StaticOcclusionCulling.smallestHole = .5f;
        StaticOcclusionCulling.backfaceThreshold = 100f;
        File.AppendAllText(Report, "BAKING " + scene.name + "\n");
        baking = true;
        bakeStarted = DateTime.UtcNow;
        Remember();
        if (!StaticOcclusionCulling.GenerateInBackground()) throw new InvalidOperationException("Could not start occlusion bake.");
    }

    static bool Crowd(Transform transform) => Ancestry(transform).Any(n => n.Contains("crowd") || n.Contains("spectator"));
    static bool Vegetation(Transform transform) => Ancestry(transform).Any(n => n.Contains("vegetation") || n.Contains("tree") || n.Contains("bush") || n.Contains("grass") || n.Contains("searsia") || n.Contains("quiver") || n.Contains("pine") || n.Contains("fern"));
    static bool Scenery(Transform transform)
    {
        if (transform.GetComponentInParent<Rigidbody>() != null || transform.GetComponentInParent<JrsVehicleController>() != null) return false;
        return Crowd(transform) || Vegetation(transform) || Ancestry(transform).Any(n => n.Contains("rocks") || n.StartsWith("rock"));
    }
    static IEnumerable<string> Ancestry(Transform transform)
    {
        for (Transform p = transform; p != null; p = p.parent) yield return p.name.ToLowerInvariant();
    }

    static Mesh ReducedMesh(Mesh source, float cell)
    {
        if (!source.isReadable) return null;
        string identity = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(source)) + "_" + source.name + "_" + cell;
        if (meshes.TryGetValue(identity, out Mesh cached)) return cached;
        string path = Generated + "/" + Hash128.Compute(identity) + ".asset";
        Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing != null) { meshes[identity] = existing; return existing; }
        // Conservative vertex clustering for distant scenery only. Submeshes and
        // UV seams stay separate, and original meshes/colliders remain untouched.
        Vector3[] vertices = source.vertices, normals = source.normals;
        Vector2[] uv = source.uv;
        Color[] colors = source.colors;
        var index = new Dictionary<(int, int, int, int, int, int), int>();
        var positions = new List<Vector3>(); var normalList = new List<Vector3>();
        var uvs = new List<Vector2>(); var colorList = new List<Color>();
        var triangles = new List<int[]>();
        int before = 0, after = 0;
        for (int sub = 0; sub < source.subMeshCount; sub++)
        {
            int[] original = source.GetTriangles(sub); before += original.Length;
            var result = new List<int>();
            int Map(int vertex)
            {
                Vector3 p = vertices[vertex]; Vector2 tex = uv.Length == vertices.Length ? uv[vertex] : Vector2.zero;
                var key = (Mathf.RoundToInt(p.x / cell), Mathf.RoundToInt(p.y / cell), Mathf.RoundToInt(p.z / cell),
                    Mathf.RoundToInt(tex.x * 32), Mathf.RoundToInt(tex.y * 32), sub);
                if (index.TryGetValue(key, out int mapped)) return mapped;
                mapped = positions.Count; index.Add(key, mapped); positions.Add(p); uvs.Add(tex);
                normalList.Add(normals.Length == vertices.Length ? normals[vertex] : Vector3.up);
                colorList.Add(colors.Length == vertices.Length ? colors[vertex] : Color.white);
                return mapped;
            }
            for (int i = 0; i < original.Length; i += 3)
            {
                int a = Map(original[i]), b = Map(original[i + 1]), c = Map(original[i + 2]);
                if (a == b || a == c || b == c) continue;
                result.Add(a); result.Add(b); result.Add(c);
            }
            after += result.Count; triangles.Add(result.ToArray());
        }
        if (after < before * .1f || after > before * .92f) { meshes[identity] = null; return null; }
        var mesh = new Mesh { name = source.name + " LOD1", indexFormat = positions.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
        mesh.SetVertices(positions); mesh.SetNormals(normalList); mesh.SetUVs(0, uvs); mesh.SetColors(colorList);
        mesh.subMeshCount = triangles.Count;
        for (int i = 0; i < triangles.Count; i++) mesh.SetTriangles(triangles[i], i);
        mesh.RecalculateBounds(); mesh.RecalculateTangents();
        AssetDatabase.CreateAsset(mesh, path);
        meshes[identity] = mesh;
        File.AppendAllText(Report, $"LOD mesh {source.name}: {before / 3} -> {after / 3} triangles\n");
        return mesh;
    }

    static string GameplaySignature(Scene scene) => string.Join("\n", scene.GetRootGameObjects()
        .SelectMany(r => r.GetComponentsInChildren<Component>(true))
        .Where(c => c is Rigidbody || c is WheelCollider || c is JrsVehicleController || c is RallyVehicleDynamics ||
            c is RallyCheckpointManager || c is RallyCheckpointTrigger || c is RallyPuddleSlowZone || c is RallyBotController)
        .Select(c => c.GetEntityId() + ":" + EditorJsonUtility.ToJson(c) + ":" + EditorJsonUtility.ToJson(c.transform)));
}
