using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.ProBuilder;
using UnityEngine.ProBuilder.MeshOperations;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>Localized river crossing: preserves the rest of the track and the tuned cars.</summary>
[InitializeOnLoad]
public static class Circuit03RiverBridge
{
    const string PathScene = "Assets/Scenes/Circuit_03.unity";
    const string Folder = "Assets/Art/Forest/Circuit03/RiverBridge";
    const string Request = "Logs/circuit03-river-bridge.request";
    const string Report = "Logs/circuit03-river-bridge.txt";
    const string Key = "Rally.RiverBridge.Bake";
    const string RootName = "Circuit 03 - Timber River Crossing";
    const float HalfDeck = 20f;
    [Serializable] sealed class Layout
    {
        public string scene = "Circuit_03", notes = "";
        public float lengthMeters = 0;
        public Vector3[] centerline = Array.Empty<Vector3>(), puddleCenters = Array.Empty<Vector3>(), crowdCenters = Array.Empty<Vector3>();
        public float[] distances = Array.Empty<float>(), roadWidths = Array.Empty<float>();
    }
    static Layout layout;
    static Vector3 center, forward, right;
    static float station;
    static Terrain terrain;
    static GameObject root;
    static Mesh cube;
    static readonly Dictionary<Material, List<CombineInstance>> timber = new Dictionary<Material, List<CombineInstance>>();
    static readonly List<Vector3> river = new List<Vector3>();
    static readonly List<BoxCollider> newWalls = new List<BoxCollider>();

    static Circuit03RiverBridge() => EditorApplication.update += Poll;
    static void Poll()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        try
        {
            if (SessionState.GetBool(Key, false))
            {
                if (StaticOcclusionCulling.isRunning) return;
                SessionState.SetBool(Key, false);
                string data = "Assets/Scenes/Circuit_03/OcclusionCullingData.asset";
                if (File.GetLastWriteTimeUtc(data).Ticks < long.Parse(SessionState.GetString(Key + "Ticks", "0")))
                    throw new InvalidOperationException("Fresh occlusion data not found.");
                EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
                File.AppendAllText(Report, "BAKE OK; BUILD COMPLETE: PASS\n");
            }
            if (!File.Exists(Request) || StaticOcclusionCulling.isRunning ||
                SessionState.GetBool("Rally.SplitTest.Active", false) || SessionState.GetBool("Rally.RenderBenchmark.Running", false)) return;
            File.Move(Request, Request + ".consumed-" + DateTime.UtcNow.Ticks);
            Build();
        }
        catch (Exception e) { SessionState.SetBool(Key, false); File.AppendAllText(Report, "FAILED: " + e + "\n"); Debug.LogException(e); }
    }

    [MenuItem("Tools/Rally/Circuit 03/Build Timber River Crossing")]
    public static void Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play first.");
        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save open scene changes first.");
        string backup = "Logs/SceneBackups/RiverBridge_" + DateTime.Now.ToString("yyyyMMdd_HHmmss");
        Directory.CreateDirectory(backup);
        File.Copy(PathScene, backup + "/Circuit_03.unity");
        File.Copy("Assets/Scenes/Circuit_03/OcclusionCullingData.asset", backup + "/OcclusionCullingData.asset");
        string layoutPath = "Assets/Art/Forest/Circuit03/Circuit03Layout.json";
        File.Copy(layoutPath, backup + "/Circuit03Layout.json");
        Scene scene = EditorSceneManager.OpenScene(PathScene);
        if (GameObject.Find(RootName) != null || Directory.Exists(Folder)) throw new InvalidOperationException("River crossing already exists.");
        File.WriteAllText(Report, "River bridge localized build. Backup: " + backup + "\n");
        string cars = CarSignature(scene);
        layout = JsonUtility.FromJson<Layout>(File.ReadAllText(layoutPath));
        int middle = Enumerable.Range(0, layout.distances.Length).OrderBy(i => Mathf.Abs(layout.distances[i] - 900)).First();
        station = layout.distances[middle]; center = layout.centerline[middle];
        forward = Vector3.ProjectOnPlane(layout.centerline[middle + 8] - layout.centerline[middle - 8], Vector3.up).normalized;
        right = Vector3.Cross(Vector3.up, forward);
        center.y += 1f;
        terrain = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Terrain>()).Single();
        Directory.CreateDirectory(Folder); AssetDatabase.Refresh();
        root = new GameObject(RootName);
        root.transform.SetPositionAndRotation(center, Quaternion.LookRotation(forward));
        var oldRoad = GameObject.Find("Circuit03_Road").GetComponent<ProBuilderMesh>();
        var oldRunoff = GameObject.Find("Circuit03_Runoff").GetComponent<ProBuilderMesh>();
        ModifyRoad(oldRoad, false); ModifyRoad(oldRunoff, true);
        BuildRiver();
        FitTerrain();
        BuildTimber();
        UpdateBoundaries();
        UpdateRouteAndGates();
        foreach (float s in new[] { -48f, 48f })
        {
            var marker = new GameObject(s < 0 ? "Crossing Entry" : "Crossing Exit");
            marker.transform.SetParent(root.transform);
            marker.transform.SetPositionAndRotation(NewPoint(station + s), Quaternion.LookRotation(NewPoint(station+s+1)-NewPoint(station+s-1)));
        }
        ClearAndReseatPlants();
        Physics.SyncTransforms();
        ValidateDrivingSurface();
        if (cars != CarSignature(scene)) throw new InvalidOperationException("Car tuning or grid pose changed.");
        var player = GameObject.Find("Porsche 911 SC Rally");
        if (player == null || !player.activeInHierarchy) throw new InvalidOperationException("Player not active at the start.");
        for (int i = 0; i < layout.centerline.Length; i++)
        {
            layout.centerline[i] = NewPoint(layout.distances[i]);
            layout.roadWidths[i] = Mathf.Lerp(layout.roadWidths[i], 10.4f, Weight(layout.distances[i] - station));
        }
        layout.notes += " Localized timber river bridge near 900m; preserve RiverBridge assets when editing terrain. Station distances retained for compatibility.";
        File.WriteAllText(layoutPath, JsonUtility.ToJson(layout, true));
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        Capture(center - forward * 34 + Vector3.up * 3, center + forward * 10, "Logs/Circuit03_Bridge_Driving.png");
        Capture(center + right * 37 + Vector3.up * 19 - forward * 29, center - Vector3.up, "Logs/Circuit03_Bridge_Overview.png");
        File.AppendAllText(Report, $"Station {station:F1}m. Deck 40 x 10.4m, smooth approaches within +/-52m. River 160m. Cars preserved; center and both wheel lanes raycast PASS.\n");
        SessionState.SetString(Key + "Ticks", DateTime.UtcNow.Ticks.ToString());
        if (!StaticOcclusionCulling.Compute()) throw new InvalidOperationException("Bake did not start.");
        SessionState.SetBool(Key, true); File.AppendAllText(Report, "BAKING Circuit_03\n");
    }

    static float Weight(float s) => 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(22, 52, Mathf.Abs(s)));
    static Vector3 Original(float distance)
    {
        int i = Array.BinarySearch(layout.distances, distance);
        if (i >= 0) return layout.centerline[i];
        i = Mathf.Clamp(~i - 1, 0, layout.distances.Length - 2);
        return Vector3.Lerp(layout.centerline[i], layout.centerline[i + 1], Mathf.InverseLerp(layout.distances[i], layout.distances[i + 1], distance));
    }
    static Vector3 NewPoint(float distance) => Vector3.Lerp(Original(distance), center + forward * (distance - station), Weight(distance - station));
    static float Nearest(Vector3 p, out float gap)
    {
        float best = float.MaxValue, distance = 0;
        for (int i = 0; i < layout.centerline.Length - 1; i++)
        {
            Vector3 a = layout.centerline[i], b = layout.centerline[i + 1];
            Vector2 d = new Vector2(b.x - a.x, b.z - a.z), q = new Vector2(p.x - a.x, p.z - a.z);
            float t = Mathf.Clamp01(Vector2.Dot(q, d) / Mathf.Max(.00001f, d.sqrMagnitude));
            float sq = (q - d * t).sqrMagnitude;
            if (sq < best) { best = sq; distance = Mathf.Lerp(layout.distances[i], layout.distances[i + 1], t); }
        }
        gap = Mathf.Sqrt(best); return distance;
    }
    static Vector3 Map(Vector3 p)
    {
        float d = Nearest(p, out _), w = Weight(d - station);
        if (w <= 0) return p;
        Vector3 tangent = Vector3.ProjectOnPlane(Original(d + .2f) - Original(d - .2f), Vector3.up).normalized;
        Vector3 lateral = Vector3.Cross(Vector3.up, tangent);
        float side = Vector3.Dot(p - Original(d), lateral);
        Vector3 newLateral = Vector3.Cross(Vector3.up, NewPoint(d + .2f) - NewPoint(d - .2f)).normalized;
        Vector3 shifted = NewPoint(d) + newLateral * side * Mathf.Lerp(1, 10.4f / 9, w);
        shifted.y += p.y - Original(d).y;
        return shifted;
    }
    static void ModifyRoad(ProBuilderMesh mesh, bool runoff)
    {
        Vector3[] old = mesh.positions.ToArray();
        var remove = mesh.faces.Where(f =>
        {
            Vector3 c = Vector3.zero; foreach (int i in f.distinctIndexes) c += mesh.transform.TransformPoint(old[i]);
            c /= f.distinctIndexes.Count;
            return Mathf.Abs(Nearest(c, out _) - station) < 18;
        }).ToArray();
        mesh.positions = old.Select(p => mesh.transform.InverseTransformPoint(Map(mesh.transform.TransformPoint(p)))).ToArray();
        mesh.DeleteFaces(remove); mesh.ToMesh(); mesh.Refresh();
        mesh.GetComponent<MeshCollider>().sharedMesh = mesh.GetComponent<MeshFilter>().sharedMesh;
        File.AppendAllText(Report, (runoff ? "Runoff" : "Road") + ": removed " + remove.Length + " faces beneath crossing; approach vertices blended locally.\n");
    }

    static float RiverBend(float x) => 5f * Mathf.Sin(x * .046f) * Mathf.Clamp01(Mathf.Abs(x) / 25);
    static float RiverHalfWidth(float x) => (8.5f + Mathf.Sin(x * .055f) * 1.1f) * Mathf.Lerp(.12f, 1, Mathf.SmoothStep(0, 1, (80 - Mathf.Abs(x)) / 20));
    static void BuildRiver()
    {
        river.Clear(); var vertices = new List<Vector3>(); var uv = new List<Vector2>(); var triangles = new List<int>();
        for (int i = 0; i <= 160; i++)
        {
            float x = i - 80, bend = RiverBend(x), hw = RiverHalfWidth(x);
            river.Add(center + right * x + forward * bend);
            for (int j = 0; j <= 8; j++)
            { vertices.Add(new Vector3(x, -2.4f, bend + Mathf.Lerp(-hw, hw, j / 8f))); uv.Add(new Vector2(x * .16f, j / 8f * 2)); }
            if (i == 160) continue;
            for (int j = 0; j < 8; j++) { int a = i * 9 + j; triangles.AddRange(new[] { a, a + 1, a + 10, a, a + 10, a + 9 }); }
        }
        var mesh = new Mesh { name = "Forest river surface" }; mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0); mesh.SetUVs(0, uv); mesh.RecalculateNormals(); mesh.RecalculateBounds();
        AssetDatabase.CreateAsset(mesh, Folder + "/RiverSurface.asset");
        var water = new Material(AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Environment/Materials/Puddles - Unity sample.mat")) { name = "Flowing forest river" };
        water.SetVector("_RippleSpeed", new Vector4(.065f, .008f, .042f, -.012f));
        water.SetVector("_RippleScale", new Vector4(.23f, .19f, .15f, .22f));
        water.SetFloat("_OpaqueDepth", 2.3f); water.SetFloat("_RefractionStrength", .004f);
        water.SetColor("_DepthColor", new Color(.08f, .17f, .15f, 0));
        AssetDatabase.CreateAsset(water, Folder + "/Flowing forest river.mat");
        MeshObject("River - animated water", mesh, water);
    }
    static void FitTerrain()
    {
        // Isolate this local edit; the original terrain asset remains recoverable.
        TerrainData source = terrain.terrainData;
        TerrainData data = Object.Instantiate(source); data.name = "Circuit03 River Terrain";
        AssetDatabase.CreateAsset(data, Folder + "/Circuit03_RiverTerrain.asset");
        terrain.terrainData = data; terrain.GetComponent<TerrainCollider>().terrainData = data;
        int n = data.heightmapResolution; float[,] heights = data.GetHeights(0, 0, n, n);
        Vector3 origin = terrain.transform.position, size = data.size;
        int changed = 0;
        for (int z = 0; z < n; z++) for (int x = 0; x < n; x++)
        {
            Vector3 p = origin + new Vector3(x * size.x / (n - 1), heights[z,x] * size.y, z * size.z / (n - 1));
            Vector3 q = root.transform.InverseTransformPoint(p);
            if (Mathf.Abs(q.x) > 98 || Mathf.Abs(q.z) > 68) continue;
            float d = Nearest(p, out float roadGap);
            if (Mathf.Abs(d - station) > 58 && roadGap < 18) continue;
            float riverGap = Mathf.Abs(q.z - RiverBend(Mathf.Clamp(q.x, -80, 80)));
            float half = RiverHalfWidth(Mathf.Clamp(q.x, -80, 80));
            float alongFade = 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(78, 97, Mathf.Abs(q.x)));
            float w = (1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(half, half + 22, riverGap))) * alongFade;
            float bed = center.y - 3.8f + Mathf.SmoothStep(0, 1, Mathf.InverseLerp(half - 2, half + 4, riverGap)) * 2.6f;
            float target = Mathf.Lerp(p.y, Mathf.Min(p.y, bed), w);
            // Abutments/approaches meet the moved road; retain the channel under the span.
            if (Mathf.Abs(d - station) > 18 && Mathf.Abs(d - station) < 56 && roadGap < 16)
            {
                float fit = (1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(6, 16, roadGap))) * Weight(d - station);
                target = Mathf.Lerp(target, NewPoint(d).y - .13f, fit);
            }
            if (Mathf.Abs(p.y - target) > .001f) { heights[z,x] = (target - origin.y) / size.y; changed++; }
        }
        data.SetHeights(0, 0, heights); EditorUtility.SetDirty(data);
        File.AppendAllText(Report, "Local river terrain: " + changed + " height samples changed; original TerrainData retained.\n");
    }

    static Material Wood(string name, Color color)
    {
        var m = new Material(AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Forest/Materials/Kenney_woodBarkDark.mat")) { name = name };
        m.SetColor("_BaseColor", color); m.SetFloat("_Smoothness", .18f); m.enableInstancing = true;
        AssetDatabase.CreateAsset(m, Folder + "/" + name + ".mat"); return m;
    }
    static void Box(Material m, Vector3 p, Vector3 size, Quaternion rotation)
    {
        if (!timber.TryGetValue(m, out var list)) timber[m] = list = new List<CombineInstance>();
        list.Add(new CombineInstance { mesh = cube, transform = Matrix4x4.TRS(p, rotation, size) });
    }
    static void Beam(Material m, Vector3 a, Vector3 b, float thickness) => Box(m, (a + b) / 2, new Vector3(thickness, thickness, (b-a).magnitude), Quaternion.LookRotation(b-a));
    static BoxCollider Collision(string name, Vector3 p, Vector3 size)
    {
        var go = new GameObject(name); go.transform.SetParent(root.transform, false); go.transform.localPosition = p;
        var box = go.AddComponent<BoxCollider>(); box.size = size; return box;
    }
    static void BuildTimber()
    {
        timber.Clear(); var primitive = GameObject.CreatePrimitive(PrimitiveType.Cube); cube = primitive.GetComponent<MeshFilter>().sharedMesh; Object.DestroyImmediate(primitive);
        var boards = Wood("Weathered timber", new Color(.29f,.19f,.10f));
        var alternate = Wood("Timber variation", new Color(.36f,.25f,.14f));
        var beams = Wood("Dark timber beams", new Color(.16f,.105f,.055f));
        var stone = new Material(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Forest/Prefabs/Rock.prefab").GetComponentInChildren<Renderer>().sharedMaterial);
        if (stone.shader == null) throw new InvalidOperationException("Stone shader missing.");
        stone.name = "Bridge abutment stone"; AssetDatabase.CreateAsset(stone, Folder + "/Bridge abutment stone.mat");
        for (int i = 0; i < 80; i++) Box(i % 5 == 0 ? alternate : boards, new Vector3(0,-.14f,-19.75f + i*.5f), new Vector3(10.4f,.3f,.492f), Quaternion.identity);
        Collision("Continuous smooth deck collider", new Vector3(0,-.16f,0), new Vector3(10.4f,.35f,40.2f));
        foreach (float side in new[]{-1f,1f})
        {
            Box(beams, new Vector3(side*3.7f,-.65f,0), new Vector3(.48f,.9f,40.6f), Quaternion.identity);
            Box(beams, new Vector3(side*5.03f,.12f,0), new Vector3(.22f,.24f,40.4f), Quaternion.identity);
            foreach (float y in new[]{.65f,1.3f}) Box(boards, new Vector3(side*5.15f,y,0), new Vector3(.23f,.2f,40.8f), Quaternion.identity);
            for (int j = 0; j <= 10; j++)
            {
                float z = -20 + j*4;
                Box(beams, new Vector3(side*5.15f,.69f,z), new Vector3(.36f,1.55f,.36f), Quaternion.identity);
                Box(alternate, new Vector3(side*5.15f,1.51f,z), new Vector3(.46f,.12f,.46f), Quaternion.identity);
                if (j < 10)
                {
                    Beam(boards,new Vector3(side*5.15f,.25f,z+.3f),new Vector3(side*5.15f,1.16f,z+3.7f),.13f);
                    Beam(boards,new Vector3(side*5.15f,1.16f,z+.3f),new Vector3(side*5.15f,.25f,z+3.7f),.13f);
                }
            }
        }
        foreach(float z in new[]{-18.8f,18.8f})
        {
            Box(stone, new Vector3(0,-1.6f,z), new Vector3(11.2f,2.8f,2.5f), Quaternion.identity);
            Box(beams, new Vector3(0,-.73f,z), new Vector3(10.9f,.5f,.65f), Quaternion.identity);
        }
        foreach (var pair in timber)
        {
            var mesh = new Mesh { name = pair.Key.name, indexFormat = IndexFormat.UInt32 };
            mesh.CombineMeshes(pair.Value.ToArray(), true, true); mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh, Folder + "/" + pair.Key.name + ".asset");
            MeshObject(pair.Key.name, mesh, pair.Key);
        }
    }
    static void MeshObject(string name, Mesh mesh, Material material)
    {
        var go = new GameObject(name); go.transform.SetParent(root.transform, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var r = go.AddComponent<MeshRenderer>(); r.sharedMaterial = material;
        r.shadowCastingMode = ShadowCastingMode.Off;
        GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.OccludeeStatic | StaticEditorFlags.BatchingStatic);
    }

    static void UpdateBoundaries()
    {
        newWalls.Clear(); var old = GameObject.Find("Circuit 03 - Soft Track Boundaries");
        int disabled = 0;
        foreach (BoxCollider wall in old.GetComponentsInChildren<BoxCollider>())
        {
            if (Mathf.Abs(Nearest(wall.transform.position, out _) - station) > 56) continue;
            wall.gameObject.SetActive(false); disabled++;
        }
        var physics = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>("Assets/Scenes/Circuit_01_SoftBoundary.physicMaterial");
        for (float s = -58; s < 58; s += 2)
        foreach (float side in new[]{-1f,1f})
        {
            float offsetA = Mathf.Lerp(11,5.35f,1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(20,51,Mathf.Abs(s))));
            float offsetB = Mathf.Lerp(11,5.35f,1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(20,51,Mathf.Abs(s+2))));
            Vector3 a = NewPoint(station+s), b = NewPoint(station+s+2), r = Vector3.Cross(Vector3.up,(b-a).normalized).normalized;
            a += r*side*offsetA; b += r*side*offsetB;
            var go = new GameObject("Bridge boundary " + side + " " + s);
            go.transform.SetParent(root.transform); go.transform.SetPositionAndRotation((a+b)/2+Vector3.up*1.1f,Quaternion.LookRotation(b-a));
            var box = go.AddComponent<BoxCollider>(); box.size = new Vector3(.55f,2.2f,Vector3.Distance(a,b)+.25f); box.sharedMaterial = physics;
            newWalls.Add(box);
        }
        File.AppendAllText(Report, "Replaced " + disabled + " old boundary segments with tapered bridge containment.\n");
    }
    static void UpdateRouteAndGates()
    {
        Transform waypoints = GameObject.Find("AI_Waypoints").transform;
        var points = waypoints.Cast<Transform>().Select(t => new KeyValuePair<float,Transform>(Nearest(t.position,out _),t)).ToList();
        foreach (var point in points) if (Mathf.Abs(point.Key-station)<60) point.Value.position = NewPoint(point.Key);
        for(float s=-48;s<=48;s+=5)
        {
            float d=station+s;
            if(points.Any(p=>Mathf.Abs(p.Key-d)<2))continue;
            var go=new GameObject("Bridge waypoint");go.transform.SetParent(waypoints);go.transform.position=NewPoint(d);
            points.Add(new KeyValuePair<float,Transform>(d,go.transform));
        }
        points=points.OrderBy(p=>p.Key).ToList();
        for(int i=0;i<points.Count;i++){points[i].Value.SetSiblingIndex(i);points[i].Value.name="Waypoint_"+(i+1).ToString("00");}
        foreach(var gate in Object.FindObjectsByType<RallyCheckpointTrigger>())
        {
            float d=Nearest(gate.transform.position,out _);if(Mathf.Abs(d-station)>58)continue;
            Vector3 p=Map(gate.transform.position);gate.transform.SetPositionAndRotation(p,Quaternion.LookRotation(NewPoint(d+1)-NewPoint(d-1)));
        }
        File.AppendAllText(Report, "AI loop: " + points.Count + " ordered waypoints; bridge spacing approximately 5m. Checkpoint order retained.\n");
    }
    static void ClearAndReseatPlants()
    {
        int removed=0,moved=0;
        var groups=new[]{GameObject.Find("Circuit 03 - Forest"),GameObject.Find("Circuit 03 - Dense Pine Groves")};
        foreach(var group in groups.Where(g=>g!=null))foreach(Transform plant in group.transform)
        {
            Vector3 q=root.transform.InverseTransformPoint(plant.position);
            if(Mathf.Abs(q.x)>97||Mathf.Abs(q.z)>68)continue;
            float riverGap=Mathf.Abs(q.z-RiverBend(Mathf.Clamp(q.x,-80,80)));
            float d=Nearest(plant.position,out _);Vector3 route=NewPoint(d);
            var rs=plant.GetComponentsInChildren<Renderer>();if(rs.Length==0)continue;
            Bounds bounds=rs[0].bounds;foreach(var r in rs)bounds.Encapsulate(r.bounds);
            float radius=new Vector2(bounds.extents.x,bounds.extents.z).magnitude;
            float roadGap=Vector2.Distance(new Vector2(route.x,route.z),new Vector2(bounds.center.x,bounds.center.z));
            if((Mathf.Abs(q.x)<83&&riverGap<RiverHalfWidth(q.x)+radius+2)||(Mathf.Abs(d-station)<56&&roadGap<5.2f+radius+8))
            {plant.gameObject.SetActive(false);removed++;continue;}
            float y=terrain.SampleHeight(plant.position)+terrain.transform.position.y;
            plant.position+=Vector3.up*(y-bounds.min.y-.03f);moved++;
        }
        File.AppendAllText(Report,$"Local vegetation: {removed} plants deactivated (recoverable) outside water/bridge; {moved} seated on edited banks.\n");
    }
    static void ValidateDrivingSurface()
    {
        float maxStep=0; Vector3 previous=NewPoint(station-55);
        for(float s=-55;s<=55;s+=.5f)
        {
            Vector3 p=NewPoint(station+s), r=Vector3.Cross(Vector3.up,(NewPoint(station+s+.2f)-NewPoint(station+s-.2f)).normalized);
            foreach(float lane in new[]{-3f,0,3f})
            {
                var hits=Physics.RaycastAll(p+r*lane+Vector3.up*5,Vector3.down,12,~0,QueryTriggerInteraction.Ignore);
                var surface=hits.Where(h=>h.collider.name=="Circuit03_Road"||h.collider.name=="Continuous smooth deck collider").OrderBy(h=>h.distance).ToArray();
                if(surface.Length==0||Mathf.Abs(surface[0].point.y-p.y)>.09f)throw new InvalidOperationException("Gap/step in driving lane at bridge station "+s+" lane "+lane);
                foreach(var wall in newWalls)if(wall.bounds.Contains(p+r*lane+Vector3.up*.5f))throw new InvalidOperationException("Containment crosses driving lane.");
            }
            maxStep=Mathf.Max(maxStep,Mathf.Abs(p.y-previous.y));previous=p;
        }
        File.AppendAllText(Report,$"Road continuity: 663 lane raycasts PASS; maximum rise per 0.5m={maxStep:F3}m.\n");
    }
    static string CarSignature(Scene scene)=>string.Join("\n",scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Component>(true))
        .Where(c=>c is Rigidbody||c is WheelCollider||c is JrsVehicleController||c is RallyVehicleDynamics||c is RallyBotController)
        .Select(c=>c.GetEntityId()+":"+EditorJsonUtility.ToJson(c)+":"+EditorJsonUtility.ToJson(c.transform)));
    static void Capture(Vector3 position,Vector3 target,string path)
    {
        var go=new GameObject("Bridge preview");var camera=go.AddComponent<Camera>();camera.fieldOfView=65;camera.farClipPlane=450;
        camera.GetUniversalAdditionalCameraData().requiresDepthTexture=true;camera.GetUniversalAdditionalCameraData().requiresColorTexture=true;
        go.transform.SetPositionAndRotation(position,Quaternion.LookRotation(target-position));
        var rt=new RenderTexture(1280,720,24);var tex=new Texture2D(1280,720,TextureFormat.RGB24,false);var old=RenderTexture.active;
        try{camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;tex.ReadPixels(new Rect(0,0,1280,720),0,0);tex.Apply();File.WriteAllBytes(path,tex.EncodeToPNG());}
        finally{RenderTexture.active=old;camera.targetTexture=null;Object.DestroyImmediate(tex);rt.Release();Object.DestroyImmediate(rt);Object.DestroyImmediate(go);}
    }
}
