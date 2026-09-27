using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Places the player and three existing bot prefab instances on a two-by-two starting grid.</summary>
public static class RallyStartingGridSetup
{
    private static readonly string[] ScenePaths =
    {
        "Assets/Scenes/Circuit_01.unity",
        "Assets/Scenes/Circuit_02.unity",
        "Assets/Scenes/Circuit_03.unity"
    };
    private static readonly string[] CarNames =
    {
        "Porsche 911 SC Rally", "Bot_Car", "Bot_Car_02", "Bot_Car_03"
    };
    private static readonly string[] SlotNames =
    {
        "Grid_01_Player", "Grid_02_Bot_Car", "Grid_03_Bot_Car_02", "Grid_04_Bot_Car_03"
    };
    private const float FrontRowMeters = 4f;
    private const float RowSpacingMeters = 13f;
    private const float ColumnOffsetMeters = 2f;

    [MenuItem("Tools/Rally/Place Starting Grids In All Circuits")]
    public static void InstallAll()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorSceneManager.GetActiveScene().isDirty)
            throw new InvalidOperationException("Save the active scene and leave Play before changing the starting grids.");

        foreach (string path in ScenePaths)
        {
            Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            GameObject route = FindRoot(scene, "AI_Waypoints");
            if (route == null || route.transform.childCount < 2)
                throw new InvalidOperationException(scene.name + " has no usable AI_Waypoints.");
            GameObject[] cars = new GameObject[CarNames.Length];
            for (int i = 0; i < cars.Length; i++)
            {
                cars[i] = FindRoot(scene, CarNames[i]);
                if (cars[i] == null)
                    throw new InvalidOperationException(scene.name + " is missing " + CarNames[i]);
            }
            PlaceGrid(scene, cars, route.transform);
            VerifyGrid(scene, cars);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
                throw new InvalidOperationException("Could not save " + path);
        }
        Debug.Log("STARTING_GRIDS_COMPLETE: Circuit_01, Circuit_02, Circuit_03");
    }

    public static void PlaceGrid(Scene scene, GameObject[] cars, Transform route)
    {
        if (cars == null || cars.Length != 4 || route == null || route.childCount < 2)
            throw new ArgumentException("The grid needs a player, three bots and a route.");

        string roadName = scene.name == "Circuit_01" ? "Rally_Road_Start_to_Finish" :
            scene.name.Replace("_", "") + "_Road";
        GameObject road = GameObject.Find(roadName);
        MeshCollider roadCollider = road != null ? road.GetComponent<MeshCollider>() : null;
        if (roadCollider == null)
            throw new InvalidOperationException(scene.name + " is missing its road MeshCollider.");

        Vector3 start = route.GetChild(0).position;
        Vector3 forward = Vector3.ProjectOnPlane(route.GetChild(1).position - start, Vector3.up).normalized;
        if (forward.sqrMagnitude < 0.9f)
            throw new InvalidOperationException(scene.name + " has an invalid starting direction.");
        Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
        Quaternion heading = Quaternion.LookRotation(forward, Vector3.up);
        if (!RoadHit(roadCollider, cars[0].transform.position, out RaycastHit oldHit))
            throw new InvalidOperationException(scene.name + " player is not over the road before grid placement.");
        float rideHeight = Mathf.Clamp(cars[0].transform.position.y - oldHit.point.y, 0.45f, 1f);

        GameObject root = FindRoot(scene, "Starting Grid");
        if (root == null)
            root = new GameObject("Starting Grid");
        root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        for (int i = 0; i < cars.Length; i++)
        {
            int row = i / 2;
            int column = i % 2;
            float distance = FrontRowMeters - row * RowSpacingMeters;
            float side = (column == 0 ? -1f : 1f) * ColumnOffsetMeters;
            Vector3 sample = start + forward * distance + right * side;
            if (!RoadHit(roadCollider, sample, out RaycastHit hit))
                throw new InvalidOperationException(scene.name + " grid slot " + (i + 1) + " is off the road.");
            Vector3 position = hit.point + Vector3.up * rideHeight;

            Transform marker = root.transform.Find(SlotNames[i]);
            if (marker == null)
            {
                marker = new GameObject(SlotNames[i]).transform;
                marker.SetParent(root.transform, false);
            }
            marker.SetPositionAndRotation(position, heading);
            cars[i].transform.SetPositionAndRotation(position, heading);
            PrefabUtility.RecordPrefabInstancePropertyModifications(cars[i].transform);
        }
        Physics.SyncTransforms();
        VerifyGrid(scene, cars);
    }

    public static void VerifyGrid(Scene scene, GameObject[] cars)
    {
        GameObject grid = FindRoot(scene, "Starting Grid");
        if (grid == null || cars == null || cars.Length != 4)
            throw new InvalidOperationException(scene.name + " has no complete starting grid.");
        GameObject checkpoint = FindRoot(scene, "Checkpoint System");
        BoxCollider startGate = checkpoint != null ? checkpoint.transform.Find("Checkpoint_00_Start")?.GetComponent<BoxCollider>() : null;
        if (startGate == null)
            throw new InvalidOperationException(scene.name + " has no start checkpoint trigger.");

        string roadName = scene.name == "Circuit_01" ? "Rally_Road_Start_to_Finish" :
            scene.name.Replace("_", "") + "_Road";
        MeshCollider road = GameObject.Find(roadName)?.GetComponent<MeshCollider>();
        if (road == null)
            throw new InvalidOperationException(scene.name + " has no road MeshCollider.");
        Physics.SyncTransforms();

        for (int i = 0; i < cars.Length; i++)
        {
            Transform marker = grid.transform.Find(SlotNames[i]);
            if (marker == null || Vector3.Distance(marker.position, cars[i].transform.position) > 0.02f ||
                Quaternion.Angle(marker.rotation, cars[i].transform.rotation) > 0.2f)
                throw new InvalidOperationException(scene.name + " grid marker does not match " + CarNames[i]);
            BoxCollider body = FindBodyCollider(cars[i]);
            if (body == null)
                throw new InvalidOperationException(CarNames[i] + " has no body BoxCollider.");

            if (Physics.ComputePenetration(body, body.transform.position, body.transform.rotation,
                    startGate, startGate.transform.position, startGate.transform.rotation, out _, out _))
                throw new InvalidOperationException(CarNames[i] + " begins inside the start checkpoint.");

            foreach (WheelCollider wheel in PhysicsWheels(scene, cars[i]))
            {
                if (!RoadHit(road, wheel.transform.position, out _))
                    throw new InvalidOperationException(CarNames[i] + " has a wheel outside the road: " + wheel.name);
            }

            Vector3 center = body.transform.TransformPoint(body.center);
            Vector3 half = Vector3.Scale(body.size * 0.5f, Abs(body.transform.lossyScale));
            foreach (Collider nearby in Physics.OverlapBox(center, half, body.transform.rotation,
                         ~0, QueryTriggerInteraction.Ignore))
            {
                if (nearby == body || nearby == road || nearby.isTrigger || IsAnyCarCollider(nearby, cars))
                    continue;
                if (Physics.ComputePenetration(body, body.transform.position, body.transform.rotation,
                        nearby, nearby.transform.position, nearby.transform.rotation, out _, out _))
                    throw new InvalidOperationException(CarNames[i] + " overlaps " + nearby.name + " in " + scene.name);
            }

            for (int other = 0; other < i; other++)
            {
                BoxCollider otherBody = FindBodyCollider(cars[other]);
                if (Physics.ComputePenetration(body, body.transform.position, body.transform.rotation,
                        otherBody, otherBody.transform.position, otherBody.transform.rotation, out _, out _))
                    throw new InvalidOperationException(CarNames[i] + " overlaps " + CarNames[other]);
            }
        }
    }

    private static bool RoadHit(MeshCollider road, Vector3 position, out RaycastHit hit) =>
        road.Raycast(new Ray(position + Vector3.up * 80f, Vector3.down), out hit, 200f);

    private static BoxCollider FindBodyCollider(GameObject car)
    {
        foreach (BoxCollider box in car.GetComponentsInChildren<BoxCollider>())
            if (box.enabled && !box.isTrigger && box.name.Contains("BodyCollider"))
                return box;
        return null;
    }

    private static WheelCollider[] PhysicsWheels(Scene scene, GameObject car)
    {
        RallyVehicleDynamics dynamics = car.GetComponent<RallyVehicleDynamics>();
        if (dynamics == null && car.name == CarNames[0])
            dynamics = FindRoot(scene, "Porsche Rally Dynamics")?.GetComponent<RallyVehicleDynamics>();
        if (dynamics == null)
            throw new InvalidOperationException(car.name + " has no rally vehicle dynamics.");
        var properties = new SerializedObject(dynamics);
        string[] names = { "frontLeft", "frontRight", "rearLeft", "rearRight" };
        var wheels = new WheelCollider[names.Length];
        for (int i = 0; i < wheels.Length; i++)
        {
            wheels[i] = properties.FindProperty(names[i])?.objectReferenceValue as WheelCollider;
            if (wheels[i] == null || !wheels[i].transform.IsChildOf(car.transform))
                throw new InvalidOperationException(car.name + " lost the " + names[i] + " physics wheel.");
        }
        return wheels;
    }

    private static bool IsAnyCarCollider(Collider collider, GameObject[] cars)
    {
        foreach (GameObject car in cars)
            if (collider.transform.IsChildOf(car.transform))
                return true;
        return false;
    }

    private static Vector3 Abs(Vector3 value) => new Vector3(Mathf.Abs(value.x), Mathf.Abs(value.y), Mathf.Abs(value.z));

    private static GameObject FindRoot(Scene scene, string name)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
            if (root.name == name)
                return root;
        return null;
    }
}
