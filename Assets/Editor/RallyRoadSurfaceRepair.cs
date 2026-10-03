using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.ProBuilder;
using UnityEngine.SceneManagement;

/// <summary>Repairs the playable road meshes without changing their geometry or colliders.</summary>
public static class RallyRoadSurfaceRepair
{
    private struct Road
    {
        public string scene;
        public string objectName;
        public string material;
        public string textureFolder;
        public bool rebuildUvs;
    }

    private static readonly Road[] Roads =
    {
        new Road { scene = "Assets/Scenes/Circuit_01.unity", objectName = "Rally_Road_Start_to_Finish",
            material = "Assets/Art/Environment/Materials/Road - dry gravel.mat",
            textureFolder = "Assets/Art/RoadSurfaces/rocky_trail_02/", rebuildUvs = false },
        new Road { scene = "Assets/Scenes/Circuit_02.unity", objectName = "Circuit02_Road",
            material = "Assets/Art/Environment/Materials/Road - dry gravel.mat",
            textureFolder = "Assets/Art/RoadSurfaces/rocky_trail_02/", rebuildUvs = true },
        new Road { scene = "Assets/Scenes/Circuit_03.unity", objectName = "Circuit03_Road",
            material = "Assets/Art/Forest/Materials/Wet forest road.mat",
            textureFolder = "Assets/Art/RoadSurfaces/muddy_tracks/", rebuildUvs = true }
    };

    [MenuItem("Tools/Rally/Repair Road Surface Materials")]
    public static void RepairAll()
    {
        if (EditorSceneManager.GetActiveScene().isDirty)
            throw new InvalidOperationException("Save or discard the current scene before repairing road surfaces.");
        string previousScene = EditorSceneManager.GetActiveScene().path;
        foreach (Road road in Roads)
            Repair(road);
        if (!string.IsNullOrEmpty(previousScene) && previousScene != Roads[Roads.Length - 1].scene)
            EditorSceneManager.OpenScene(previousScene, OpenSceneMode.Single);
        AssetDatabase.SaveAssets();
    }

    public static void RunBatch() => RepairAll();

    private static void Repair(Road road)
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(road.material);
        if (material == null || material.shader == null || material.shader.name != "Universal Render Pipeline/Lit")
            throw new InvalidOperationException("Missing URP Lit road material: " + road.material);
        if (AssetDatabase.GetAssetPath(material.GetTexture("_BaseMap")) != road.textureFolder + "diff.jpg" ||
            AssetDatabase.GetAssetPath(material.GetTexture("_BumpMap")) != road.textureFolder + "nor_gl.jpg" ||
            AssetDatabase.GetAssetPath(material.GetTexture("_MetallicGlossMap")) != road.textureFolder + "urp_mask.png")
            throw new InvalidOperationException("Road material texture bindings are wrong: " + road.material);

        Scene scene = EditorSceneManager.OpenScene(road.scene, OpenSceneMode.Single);
        GameObject go = scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
            .Select(t => t.gameObject).FirstOrDefault(g => g.name == road.objectName);
        if (go == null)
            throw new InvalidOperationException("Road object missing: " + road.objectName);
        ProBuilderMesh mesh = go.GetComponent<ProBuilderMesh>();
        MeshRenderer renderer = go.GetComponent<MeshRenderer>();
        if (mesh == null || renderer == null)
            throw new InvalidOperationException("Road is not a rendered ProBuilder mesh: " + road.objectName);

        bool hadManualUvs = mesh.faces.All(face => face.manualUV);
        bool correctMaterial = renderer.sharedMaterials.Length == 1 && renderer.sharedMaterial == material;
        if (road.rebuildUvs || !hadManualUvs || !correctMaterial)
        {
            // The original Circuit_02/03 generators wrote planar UVs but kept
            // faces in automatic-UV mode. ProBuilder recalculated those UVs.
            if (road.rebuildUvs || !hadManualUvs)
                mesh.textures = mesh.positions.Select(p =>
                {
                    Vector3 world = mesh.transform.TransformPoint(p);
                    return new Vector2(world.x / 6f, world.z / 6f);
                }).ToArray();
            foreach (Face face in mesh.faces)
                face.manualUV = true;
            renderer.sharedMaterial = material;
            mesh.SetMaterial(mesh.faces, material);
            mesh.ToMesh();
            mesh.Refresh();
            EditorUtility.SetDirty(mesh);
            EditorUtility.SetDirty(renderer);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
                throw new InvalidOperationException("Could not save road scene: " + road.scene);
        }
        Debug.Log($"ROAD_SURFACE_OK {road.scene}: {mesh.faces.Count} faces, manual UV={mesh.faces.All(face => face.manualUV)}, material={renderer.sharedMaterial.name}, texture={AssetDatabase.GetAssetPath(material.GetTexture("_BaseMap"))}");
    }
}
