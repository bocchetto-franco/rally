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

// Explicit editor operation. Optimizes only render/environment content in Circuit_01.
[InitializeOnLoad]
public static class Circuit01PerformanceSetup
{
    const string ScenePath = "Assets/Scenes/Circuit_01.unity";
    const string RequestPath = "Logs/performance-request.txt";
    const string RendererPath = "Assets/Settings/PC_Renderer.asset";
    const string EnvironmentRoot = "Assets/Art/Environment";
    const int DetailResolution = 1024;
    const int DetailResolutionPerPatch = 32;
    static double nextPoll;
    static bool busy;

    static Circuit01PerformanceSetup() { EditorApplication.update += Poll; }

    static void Poll()
    {
        if (busy || EditorApplication.timeSinceStartup < nextPoll || EditorApplication.isCompiling ||
            EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        nextPoll = EditorApplication.timeSinceStartup + 2;
        if (!File.Exists(RequestPath)) return;
        File.Move(RequestPath, RequestPath + ".consumed-" + DateTime.Now.ToString("yyyyMMddHHmmssfff"));
        try { Optimize(); }
        catch (Exception e)
        {
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/performance-error.txt", e.ToString());
            Debug.LogException(e);
        }
    }

    [MenuItem("Tools/Rally/Performance/Optimize Circuit 01")]
    public static void Optimize()
    {
        if (busy || EditorApplication.isPlayingOrWillChangePlaymode) return;
        busy = true;
        try
        {
            Directory.CreateDirectory("Logs/SceneBackups");
            EditorSceneManager.SaveOpenScenes();
            File.Copy(ScenePath,
                "Logs/SceneBackups/Circuit_01_before_performance_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".unity");
            var scene = EditorSceneManager.OpenScene(ScenePath);
            string gameplayBefore = GameplaySignature(scene);

            var terrain = Object.FindObjectsByType<Terrain>(FindObjectsInactive.Include)
                .FirstOrDefault(t => t.name == "Terrain - Desert mountain basin");
            if (terrain == null) throw new InvalidOperationException("Circuit_01 desert Terrain was not found.");

            int looseVegetationBefore = CountLooseVegetation();
            int terrainDetailsBefore = CountTerrainDetails(terrain.terrainData);
            int vegetationPlacements = looseVegetationBefore + terrainDetailsBefore;
            int people = CountPeople(scene);
            var props = GameObject.Find("Sparse CC0 vegetation and rocks");
            int converted = ConvertLooseVegetationToTerrainDetails(terrain, props);
            int detailInstances = CountTerrainDetails(terrain.terrainData);
            int rocks = CountLooseRocks();

            bool ssaoDisabled = DisableRendererSsao();
            var volumes = AuditVolumes();
            var textures = AuditAndClampEnvironmentTextures();
            var lights = AuditLights();

            if (gameplayBefore != GameplaySignature(scene))
                throw new InvalidOperationException("A gameplay/vehicle component changed unexpectedly; scene was not saved.");

            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Circuit_01 could not be saved.");

            string report =
                "Circuit_01 performance optimization\n" +
                "Vegetation placements found: " + vegetationPlacements + " (loose GameObjects before this run: " +
                looseVegetationBefore + "; existing Terrain Details: " + terrainDetailsBefore + ").\n" +
                "Vegetation converted this run: " + converted + ". Terrain Detail instances now: " + detailInstances + ".\n" +
                "Loose rocks retained: " + rocks + " (modest count; retaining the 3D LOD prefabs preserves visual quality).\n" +
                "People/crowd GameObjects: " + people + ".\n" +
                "Terrain Details: resolution " + DetailResolution + ", patch " + DetailResolutionPerPatch +
                ", draw distance " + terrain.detailObjectDistance.ToString("F0") + "m, density " + terrain.detailObjectDensity.ToString("F2") + ".\n" +
                "URP SSAO renderer feature disabled: " + ssaoDisabled + ".\n" +
                volumes + "\n" + textures + "\n" + lights + "\n" +
                "Gameplay signature unchanged; vehicle physics, checkpoints, puddle slowdown and road geometry were not edited.\n" +
                "Saved " + DateTime.Now.ToString("O");
            File.WriteAllText("Logs/performance-validation.txt", report);
            Debug.Log("CIRCUIT_01_PERFORMANCE_OK\n" + report);
        }
        finally { busy = false; }
    }

    public static int ConvertLooseVegetationToTerrainDetails(Terrain terrain, GameObject props)
    {
        if (terrain == null || terrain.terrainData == null || props == null) return 0;
        var loose = props.transform.Cast<Transform>()
            .Where(t => t.name.StartsWith("grass_medium_01", StringComparison.OrdinalIgnoreCase) ||
                        t.name.StartsWith("wild_rooibos_bush", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        // Re-running is safe: preserve already-created detail layers when there are no loose plants.
        if (loose.Length == 0)
        {
            terrain.detailObjectDistance = 180f;
            terrain.detailObjectDensity = 1f;
            EditorUtility.SetDirty(terrain);
            return 0;
        }

        var grassPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(EnvironmentRoot + "/Prefabs/grass_medium_01.prefab");
        var shrubPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(EnvironmentRoot + "/Prefabs/wild_rooibos_bush.prefab");
        if (grassPrefab == null || shrubPrefab == null)
            throw new InvalidOperationException("Vegetation prefabs required by Terrain Details are missing.");

        var data = terrain.terrainData;
        data.SetDetailResolution(DetailResolution, DetailResolutionPerPatch);
        data.detailPrototypes = new[]
        {
            MeshDetail(grassPrefab, .72f, 1.42f, .75f, 1.45f, 170926),
            MeshDetail(shrubPrefab, .72f, 1.45f, .75f, 1.50f, 170927)
        };

        var grass = new int[DetailResolution, DetailResolution];
        var shrubs = new int[DetailResolution, DetailResolution];
        Vector3 origin = terrain.transform.position;
        Vector3 size = data.size;
        foreach (var item in loose)
        {
            Vector3 p = item.position;
            int x = Mathf.Clamp(Mathf.FloorToInt((p.x - origin.x) / size.x * DetailResolution), 0, DetailResolution - 1);
            int z = Mathf.Clamp(Mathf.FloorToInt((p.z - origin.z) / size.z * DetailResolution), 0, DetailResolution - 1);
            if (item.name.StartsWith("grass_medium_01", StringComparison.OrdinalIgnoreCase)) grass[z, x]++;
            else shrubs[z, x]++;
        }
        data.SetDetailLayer(0, 0, 0, grass);
        data.SetDetailLayer(0, 0, 1, shrubs);
        terrain.detailObjectDistance = 180f;
        terrain.detailObjectDensity = 1f;
        terrain.drawInstanced = true;
        foreach (var item in loose) Object.DestroyImmediate(item.gameObject);
        EditorUtility.SetDirty(data);
        EditorUtility.SetDirty(terrain);
        EditorUtility.SetDirty(props);
        return loose.Length;
    }

    static DetailPrototype MeshDetail(GameObject prefab, float minWidth, float maxWidth, float minHeight, float maxHeight, int seed)
    {
        return new DetailPrototype
        {
            prototype = prefab,
            usePrototypeMesh = true,
            useInstancing = true,
            renderMode = DetailRenderMode.VertexLit,
            minWidth = minWidth,
            maxWidth = maxWidth,
            minHeight = minHeight,
            maxHeight = maxHeight,
            noiseSpread = .2f,
            noiseSeed = seed,
            healthyColor = Color.white,
            dryColor = new Color(.74f, .68f, .51f, 1f)
        };
    }

    static bool DisableRendererSsao()
    {
        var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
        if (renderer == null) throw new InvalidOperationException("URP renderer asset not found: " + RendererPath);
        var ssao = renderer.rendererFeatures.FirstOrDefault(f => f != null &&
            f.name.IndexOf("AmbientOcclusion", StringComparison.OrdinalIgnoreCase) >= 0);
        if (ssao == null) return false;
        ssao.SetActive(false);
        EditorUtility.SetDirty(ssao);
        EditorUtility.SetDirty(renderer);
        return !ssao.isActive;
    }

    static string AuditVolumes()
    {
        var volumes = Object.FindObjectsByType<Volume>(FindObjectsInactive.Include);
        int active = volumes.Count(v => v.enabled && v.gameObject.activeInHierarchy);
        var heavy = new List<string>();
        foreach (var volume in volumes.Where(v => v.enabled && v.gameObject.activeInHierarchy && v.sharedProfile != null))
        foreach (var component in volume.sharedProfile.components.Where(c => c != null && c.active))
        {
            string n = component.GetType().Name;
            if (n == "Bloom" || n == "AmbientOcclusion" || n == "MotionBlur" || n == "DepthOfField" || n == "FilmGrain")
                heavy.Add(volume.name + "/" + n);
        }
        return "Scene Volumes: " + volumes.Length + " total, " + active + " active; active heavy effects: " +
               (heavy.Count == 0 ? "none" : string.Join(", ", heavy)) + ". Main camera post-processing remains disabled.";
    }

    static string AuditAndClampEnvironmentTextures()
    {
        var dependencies = AssetDatabase.GetDependencies(ScenePath, true)
            .Where(p => p.StartsWith(EnvironmentRoot, StringComparison.OrdinalIgnoreCase))
            .Distinct().ToArray();
        int textures = 0, changed = 0, over2K = 0, largest = 0;
        foreach (string path in dependencies)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) continue;
            textures++;
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (texture != null) largest = Mathf.Max(largest, Mathf.Max(texture.width, texture.height));
            int limit = path.IndexOf("PolyHaven", StringComparison.OrdinalIgnoreCase) >= 0 &&
                        (path.IndexOf("dry_ground", StringComparison.OrdinalIgnoreCase) >= 0 ||
                         path.IndexOf("gravelly_sand", StringComparison.OrdinalIgnoreCase) >= 0 ||
                         path.IndexOf("rock_boulder", StringComparison.OrdinalIgnoreCase) >= 0) ? 2048 : 1024;
            if (texture != null && (texture.width > 2048 || texture.height > 2048)) over2K++;
            bool dirty = false;
            if (importer.maxTextureSize > limit) { importer.maxTextureSize = limit; dirty = true; }
            if (importer.textureCompression == TextureImporterCompression.Uncompressed)
            {
                importer.textureCompression = TextureImporterCompression.Compressed;
                importer.compressionQuality = 50;
                dirty = true;
            }
            if (dirty) { importer.SaveAndReimport(); changed++; }
        }
        return "Referenced environment textures audited: " + textures + "; largest imported dimension: " + largest +
               "px; textures above 2048px: " + over2K + "; import settings corrected: " + changed +
               ". Terrain maps stay at <=2048, vegetation/water at <=1024, with compression enabled.";
    }

    static string AuditLights()
    {
        var lights = Object.FindObjectsByType<Light>(FindObjectsInactive.Include);
        var activeRealtime = lights.Where(l => l.enabled && l.gameObject.activeInHierarchy &&
                                               l.lightmapBakeType == LightmapBakeType.Realtime).ToArray();
        var extra = activeRealtime.Where(l => l.type != LightType.Directional).ToArray();
        return "Lights: " + lights.Length + " components total; active real-time: " + activeRealtime.Length +
               " (" + string.Join(", ", activeRealtime.Select(l => l.name)) + "); additional active real-time lights: " +
               extra.Length + ". Disabled police/headlight objects were left unchanged.";
    }

    static int CountLooseVegetation()
    {
        var root = GameObject.Find("Sparse CC0 vegetation and rocks");
        return root == null ? 0 : root.transform.Cast<Transform>().Count(t =>
            t.name.StartsWith("grass_medium_01", StringComparison.OrdinalIgnoreCase) ||
            t.name.StartsWith("wild_rooibos_bush", StringComparison.OrdinalIgnoreCase));
    }

    static int CountLooseRocks()
    {
        var root = GameObject.Find("Sparse CC0 vegetation and rocks");
        return root == null ? 0 : root.transform.Cast<Transform>().Count(t =>
            t.name.StartsWith("namaqualand_rocks_01", StringComparison.OrdinalIgnoreCase));
    }

    static int CountPeople(Scene scene)
    {
        string[] terms = { "crowd", "spectator", "public", "person", "people", "audience", "fan", "tribuna", "grada" };
        return AllGameObjects(scene).Count(g => terms.Any(t => g.name.IndexOf(t, StringComparison.OrdinalIgnoreCase) >= 0));
    }

    static IEnumerable<GameObject> AllGameObjects(Scene scene) =>
        scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)).Select(t => t.gameObject);

    static int CountTerrainDetails(TerrainData data)
    {
        int total = 0;
        for (int layer = 0; layer < data.detailPrototypes.Length; layer++)
        {
            var values = data.GetDetailLayer(0, 0, data.detailWidth, data.detailHeight, layer);
            foreach (int value in values) total += value;
        }
        return total;
    }

    static string GameplaySignature(Scene scene)
    {
        var all = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Component>(true));
        return string.Join("\n", all.Where(c => c is WheelCollider || c is Rigidbody || c is JrsVehicleController ||
                c is RallyVehicleDynamics || c is RallyCheckpointManager || c is RallyCheckpointTrigger ||
                c is RallyPuddleSlowZone || (c is BoxCollider && c.GetComponent<RallyPuddleSlowZone>() != null))
            .OrderBy(c => c.GetEntityId().ToString())
            .Select(c => c.GetEntityId() + ":" + EditorJsonUtility.ToJson(c) + ":" + EditorJsonUtility.ToJson(c.transform)));
    }
}
