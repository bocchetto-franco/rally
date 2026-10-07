using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class RallyClassicMiniSetup
{
    const string Folder = "Assets/Art/Vehicles/ClassicMini";

    [MenuItem("Tools/Rally/Build Classic Mini Visual")]
    public static void Build()
        => BuildVariant(Folder, "ClassicMini", ConvertMaterial);

    public static void BuildVariant(string folder, string modelName, Func<Material, Material> convertMaterial)
    {
        AssetDatabase.Refresh();
        var importer = (ModelImporter)AssetImporter.GetAtPath(folder + "/" + modelName + ".fbx");
        importer.importCameras = false;
        importer.importLights = false;
        importer.importAnimation = false;
        importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
        importer.SaveAndReimport();
        Directory.CreateDirectory(folder + "/Materials");
        Directory.CreateDirectory("Assets/Resources/Vehicles");
        AssetDatabase.Refresh();
        var source = AssetDatabase.LoadAssetAtPath<GameObject>(folder + "/" + modelName + ".fbx");
        var instance = UnityEngine.Object.Instantiate(source);
        instance.name = modelName;
        GameObject root = null;
        try
        {
            var all = instance.GetComponentsInChildren<Transform>();
            var front = all.First(t => t.name == "wheel_FL");
            var rear = all.First(t => t.name == "wheel_BL");
            var frontRight = all.First(t => t.name == "wheel_FR");
            var rearRight = all.First(t => t.name == "wheel_BR");
            // Axle midpoints, not one side: front/rear track widths can differ.
            Vector3 forward = ((front.position + frontRight.position) - (rear.position + rearRight.position)).normalized;
            instance.transform.rotation = Quaternion.FromToRotation(forward, Vector3.forward) * instance.transform.rotation;

            // Preserve source transforms under a neutral Unity root. Use wheel bounds, not
            // source object origins, to determine axle locations and wheel radius.
            root = new GameObject(modelName);
            instance.transform.SetParent(root.transform, true);
            var visual = root.AddComponent<RallyVehicleVisual>();
            visual.wheels = new Transform[4];
            visual.radii = new float[4];
            var meshes = instance.GetComponentsInChildren<MeshRenderer>();
            foreach (var renderer in meshes)
            {
                renderer.sharedMaterials = renderer.sharedMaterials.Select(convertMaterial).ToArray();
            }
            var candidates = instance.GetComponentsInChildren<Transform>().Where(t => t.name.StartsWith("wheel_")).ToArray();
            var ordered = candidates.OrderByDescending(t => BoundsIn(root.transform, t.GetComponentsInChildren<MeshRenderer>()).center.z)
                .Take(2).OrderBy(t => BoundsIn(root.transform, t.GetComponentsInChildren<MeshRenderer>()).center.x)
                .Concat(candidates.OrderBy(t => BoundsIn(root.transform, t.GetComponentsInChildren<MeshRenderer>()).center.z)
                    .Take(2).OrderBy(t => BoundsIn(root.transform, t.GetComponentsInChildren<MeshRenderer>()).center.x)).ToArray();
            string[] names = { "wheel_FL", "wheel_FR", "wheel_BL", "wheel_BR" };
            for (int i = 0; i < 4; i++)
            {
                Bounds b = BoundsIn(root.transform, ordered[i].GetComponentsInChildren<MeshRenderer>());
                var pivot = new GameObject(names[i]).transform;
                pivot.SetParent(root.transform, false);
                pivot.localPosition = b.center;
                if (i % 2 == 1) pivot.localRotation = Quaternion.Euler(0, 180, 0);
                ordered[i].name = names[i] + " Mesh";
                ordered[i].SetParent(pivot, true);
                visual.wheels[i] = pivot;
                visual.radii[i] = (b.size.y + b.size.z) * .25f;
                if (visual.radii[i] < .2f || visual.radii[i] > .4f)
                    throw new Exception("Unexpected imported wheel scale: " + b);
            }
            visual.bodyBounds = BoundsIn(root.transform, instance.GetComponentsInChildren<MeshRenderer>());
            PrefabUtility.SaveAsPrefabAsset(root, "Assets/Resources/Vehicles/" + modelName + ".prefab");
            Debug.Log("VEHICLE_VISUAL_BUILT " + modelName + " " + visual.bodyBounds + " radii=" + string.Join(",", visual.radii));
            UnityEngine.Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();
        }
        finally
        {
            if (root != null) UnityEngine.Object.DestroyImmediate(root);
            else if (instance != null) UnityEngine.Object.DestroyImmediate(instance);
        }
    }

    static Material ConvertMaterial(Material original)
    {
        string path = Folder + "/Materials/" + original.name + ".mat";
        var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null) return existing;
        var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        material.name = original.name;
        material.SetColor("_BaseColor", original.HasProperty("_Color") ? original.color : Color.gray);
        material.SetFloat("_Smoothness", original.name.Contains("Chrome") ? .7f : .35f);
        material.SetFloat("_Metallic", original.name.Contains("Chrome") ? .8f : 0f);
        material.enableInstancing = true;
        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    static Bounds BoundsIn(Transform root, MeshRenderer[] renderers)
    {
        bool first = true;
        Bounds result = default;
        foreach (var renderer in renderers)
        {
            Bounds b = renderer.GetComponent<MeshFilter>().sharedMesh.bounds;
            for (int mask = 0; mask < 8; mask++)
            {
                Vector3 v = b.center + Vector3.Scale(b.extents, new Vector3((mask & 1) == 0 ? -1 : 1,
                    (mask & 2) == 0 ? -1 : 1, (mask & 4) == 0 ? -1 : 1));
                v = root.InverseTransformPoint(renderer.transform.TransformPoint(v));
                if (first) { result = new Bounds(v, Vector3.zero); first = false; }
                else result.Encapsulate(v);
            }
        }
        return result;
    }

    [MenuItem("Tools/Rally/Verify Classic Car Selection")]
    public static void Verify()
    {
        if (EditorSceneManager.GetActiveScene().isDirty && !Application.isBatchMode)
            throw new InvalidOperationException("Save the open scene before running vehicle validation.");
        string originalScene = EditorSceneManager.GetActiveScene().path;
        string saved = PlayerPrefs.GetString("Rally.SelectedVehicle", RallyGameSession.VehicleName);
        try
        {
            RallyGameSession.SelectVehicle(1);
            RallyGameSession.SelectCurrentOptions();
            RallyGameSession.RestoreSavedState();
            if (RallyGameSession.SelectedVehicle != RallyPlayerVehicleSelection.BmwName) throw new Exception("Selection reset to Porsche.");
            foreach (var sceneName in RallyGameSession.CircuitScenes)
            {
                EditorSceneManager.OpenScene("Assets/Scenes/" + sceneName + ".unity");
                var player = GameObject.Find(RallyGameSession.VehicleName).GetComponent<JrsVehicleController>();
                var rb = player.GetComponent<Rigidbody>();
                float mass = rb.mass, damping = rb.angularDamping, steering = player.maxSteerAngle;
                var oldWheel = player.rearLeftWheel;
                WheelFrictionCurve sideways = oldWheel.sidewaysFriction;
                WheelFrictionCurve forward = oldWheel.forwardFriction;
                var visual = RallyPlayerVehicleSelection.ApplyMini(player);
                if (visual.wheels.Length != 4 || rb.mass != mass || rb.angularDamping != damping || player.maxSteerAngle != steering ||
                    oldWheel != player.rearLeftWheel || !sideways.Equals(oldWheel.sidewaysFriction) || !forward.Equals(oldWheel.forwardFriction))
                    throw new Exception("Player tuning changed in " + sceneName);
                if (player.frontLeftWheelTransform != visual.wheels[0] || player.rearRightWheelTransform != visual.wheels[3])
                    throw new Exception("Wheel visuals not assigned.");
                if (visual.wheels[0].localPosition.z <= visual.wheels[2].localPosition.z ||
                    visual.wheels[0].localPosition.x >= visual.wheels[1].localPosition.x) throw new Exception("Invalid wheel order.");
                if (RallyPlayerVehicleSelection.ApplyMini(player) != visual) throw new Exception("Duplicate visual on reapply.");
                foreach (var bot in UnityEngine.Object.FindObjectsByType<RallyBotController>())
                    if (bot.GetComponentInChildren<RallyVehicleVisual>() != null) throw new Exception("A bot was modified.");
                Debug.Log("CLASSIC_CAR_SELECTION_PASS " + sceneName + " wheels=" + string.Join(",", visual.radii));
            }
        }
        finally
        {
            PlayerPrefs.SetString("Rally.SelectedVehicle", saved);
            PlayerPrefs.Save();
            RallyGameSession.RestoreSavedState();
            // Validation is read-only for scenes; discard the temporary swaps.
            if (string.IsNullOrEmpty(originalScene)) EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            else EditorSceneManager.OpenScene(originalScene);
        }
    }

    public static void BuildAndVerify() { Build(); Verify(); }
}
