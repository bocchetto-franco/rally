using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.ProBuilder;
using UnityEngine.SceneManagement;

// One shared route for the road, shoulders, gates, props and containment.
[InitializeOnLoad]
public static class Circuit01LoopSetup
{
    const string ScenePath = "Assets/Scenes/Circuit_01.unity";
    public const string Marker = "Circuit 01 Closed Loop v2";
    public const string ShortMarker = "Circuit 01 Short Loop v1";
    const string InteractivePropsMarker = "Circuit 01 Interactive Props v1";
    sealed class Section
    {
        public float start, length, yaw, curvature, width;
        public Vector3 origin;
    }
    static readonly List<Section> sections = new List<Section>();
    static float total, originalLength;
    static bool busy;

    static Circuit01LoopSetup()
    {
        EditorApplication.delayCall += Once;
        EditorSceneManager.sceneOpened += (scene, mode) => EditorApplication.delayCall += Once;
    }
    static void Once()
    {
        if (busy || EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        { EditorApplication.delayCall += Once; return; }
        if (SceneManager.GetActiveScene().path != ScenePath)
            return;

        if (GameObject.Find(Marker) == null)
            Build();
        else if (GameObject.Find(InteractivePropsMarker) == null)
            RebuildInteractiveProps();
    }
    static void Define()
    {
        sections.Clear(); total = 0;
        Vector3 p = Vector3.zero; float h = 0;
        float[] straights = { (100 - 40 * Mathf.PI / 4) / 2, 70, 100, 45, 90, 0, 90, 45, 120 };
        float[] turns = { 45, -45, 110, -110, 35, -35, -100, 100 };
        float[] radii = { 40, 60, 25, 30, 45, 45, 25, 35 };
        for (int i = 0; i < straights.Length; i++)
        {
            Straight(straights[i], ref p, ref h);
            if (i < turns.Length) Turn(turns[i], radii[i], ref p, ref h);
            if (GameObject.Find(ShortMarker) != null && i == 5) break;
        }
        originalLength = total;
        if (GameObject.Find(ShortMarker) != null)
        {
            // Keep the original first six sections through the S-bend, then return outside them.
            Turn(180,22,ref p,ref h);
            Add(p.z+55,0,12,ref p,ref h);
            Turn(90,55,ref p,ref h);
            Add(p.x-55,0,12,ref p,ref h);
            Turn(90,55,ref p,ref h);
            Add(-p.z,0,12,ref p,ref h);
            if(p.magnitude>.01f)throw new InvalidOperationException("Short loop does not close.");
            return;
        }
        // Two opposing hairpins north of the existing course, with 44m between legs.
        Straight(160, ref p, ref h);
        Turn(180, 22, ref p, ref h);
        Straight(100, ref p, ref h);
        Turn(-180, 22, ref p, ref h);
        Straight(160, ref p, ref h);
        // Outer return runs east of the original course and below its start.
        Turn(90, 55, ref p, ref h);
        // The only long speed straight: 200m on the north-east side.
        Add(200, 0, 12, ref p, ref h);
        Turn(90, 55, ref p, ref h);
        Winding(p.z + 100, 35, ref p, ref h);
        Turn(90, 55, ref p, ref h);
        Winding(p.x - 55, 35, ref p, ref h);
        Turn(90, 55, ref p, ref h);
        Straight(-p.z, ref p, ref h);
        if (p.magnitude > .01f || Vector3.Dot(new Vector3(Mathf.Sin(h), 0, Mathf.Cos(h)), Vector3.forward) < .9999f)
            throw new InvalidOperationException("Loop endpoint or heading does not match its start.");
    }
    static void Straight(float length, ref Vector3 p, ref float h)
    {
        if (length > 120) Winding(length, 12, ref p, ref h);
        else if (length > .001f) Add(length, 0, 12, ref p, ref h);
    }
    // Four tangent circular arcs return to the original axis and heading.
    // Their forward displacement is exactly 4*r*sin(angle), preserving closure.
    static void Winding(float displacement, float angle, ref Vector3 p, ref float h)
    {
        int count = Mathf.CeilToInt(displacement / 180);
        float radius = displacement / count / (4 * Mathf.Sin(angle * Mathf.Deg2Rad));
        for (int i = 0; i < count; i++)
        {
            float sign = i % 2 == 0 ? 1 : -1;
            Turn(sign * angle, radius, ref p, ref h);
            Turn(-sign * angle, radius, ref p, ref h);
            Turn(-sign * angle, radius, ref p, ref h);
            Turn(sign * angle, radius, ref p, ref h);
        }
    }
    static void Turn(float angle, float radius, ref Vector3 p, ref float h)
    { Add(Mathf.Abs(angle) * Mathf.Deg2Rad * radius, Mathf.Sign(angle) / radius, Mathf.Abs(angle) >= 90 ? 7 : 9, ref p, ref h); }
    static void Add(float length, float curvature, float width, ref Vector3 p, ref float h)
    {
        var s = new Section { start = total, length = length, origin = p, yaw = h, curvature = curvature, width = width };
        sections.Add(s); Position(s, length, out p, out h); total += length;
    }
    static void Position(Section s, float d, out Vector3 p, out float h)
    {
        h = s.yaw + s.curvature * d;
        p = Mathf.Abs(s.curvature) < .00001f
            ? s.origin + new Vector3(Mathf.Sin(s.yaw), 0, Mathf.Cos(s.yaw)) * d
            : s.origin + new Vector3((Mathf.Cos(s.yaw) - Mathf.Cos(h)) / s.curvature, 0, (Mathf.Sin(h) - Mathf.Sin(s.yaw)) / s.curvature);
    }
    static float Hill(float d, float a, float b, float height)
    { return d <= a || d >= b ? 0 : height * .5f * (1 - Mathf.Cos(2 * Mathf.PI * (d - a) / (b - a))); }
    static void Sample(float d, out Vector3 p, out Vector3 right, out float width)
    {
        d = Mathf.Clamp(d, 0, total);
        var s = sections.FirstOrDefault(v => d <= v.start + v.length + .0001f) ?? sections.Last();
        Position(s, Mathf.Clamp(d - s.start, 0, s.length), out p, out float h);
        p.y = Hill(d, 120, 280, 10) + Hill(d, 390, 550, -7) + Hill(d, 640, 850, 13);
        if (d >= total - .0001f) p = Vector3.zero;
        right = new Vector3(Mathf.Cos(h), 0, -Mathf.Sin(h)); width = 12;
        foreach (var bend in sections.Where(v => v.curvature != 0))
        {
            float gap = Mathf.Max(bend.start - d, d - bend.start - bend.length, 0);
            width = Mathf.Min(width, Mathf.Lerp(bend.width, 12, Mathf.SmoothStep(0, 1, gap / 20)));
        }
    }
    static float[] Distances(float step)
    {
        var values = new SortedSet<float> { 0, total };
        foreach (var s in sections)
        { int n = Mathf.CeilToInt(s.length / step); for (int i = 0; i <= n; i++) values.Add(s.start + s.length * i / n); }
        return values.ToArray();
    }
    static void Quad(List<Vector3> v, List<Face> f, Vector3 a, Vector3 b, Vector3 c, Vector3 d)
    { int n = v.Count; v.AddRange(new[] { a,b,c,d }); f.Add(new Face(new[] { n,n+1,n+2,n,n+2,n+3 })); }
    static ProBuilderMesh Strip(string name, Material material, bool shoulders)
    {
        var v = new List<Vector3>(); var f = new List<Face>(); var ds = Distances(1.5f);
        for (int i = 0; i < ds.Length - 1; i++)
        {
            Sample(ds[i], out var a, out var ar, out float aw);
            Sample(ds[i+1], out var b, out var br, out float bw);
            if (!shoulders) Quad(v,f,a-ar*aw/2,b-br*bw/2,b+br*bw/2,a+ar*aw/2);
            else
            {
                Quad(v,f,a-ar*(aw/2+7),b-br*(bw/2+7),b-br*bw/2,a-ar*aw/2);
                Quad(v,f,a+ar*aw/2,b+br*bw/2,b+br*(bw/2+7),a+ar*(aw/2+7));
            }
        }
        var mesh = ProBuilderMesh.Create(v,f); mesh.name = name; mesh.ToMesh(); mesh.Refresh();
        mesh.GetComponent<MeshRenderer>().sharedMaterial = material;
        var col = mesh.gameObject.AddComponent<MeshCollider>(); col.sharedMesh = mesh.GetComponent<MeshFilter>().sharedMesh;
        return mesh;
    }
    static Material Material(string file, Color color)
    {
        string path = "Assets/Scenes/" + file + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m,path); }
        m.color = color; EditorUtility.SetDirty(m); return m;
    }
    static void Remove(string name)
    { var go = GameObject.Find(name); if (go != null) UnityEngine.Object.DestroyImmediate(go); }

    [MenuItem("Tools/Rally/Extend Circuit 01 into Closed Loop")]
    public static void Build()
    {
        var scene = SceneManager.GetActiveScene();
        if (busy || scene.path != ScenePath || EditorApplication.isPlayingOrWillChangePlaymode) return;
        busy = true;
        try
        {
            Define();
            Directory.CreateDirectory("Logs/SceneBackups");
            File.Copy(ScenePath, "Logs/SceneBackups/Circuit_01_before_loop_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".unity", false);
            var car = scene.GetRootGameObjects().Single(g => g.name == "Porsche 911 SC Rally");
            var dynamics = UnityEngine.Object.FindAnyObjectByType<RallyVehicleDynamics>();
            string dynamicsBefore = EditorJsonUtility.ToJson(dynamics);
            var oldRoad = GameObject.Find("Rally_Road_Start_to_Finish");
            if (oldRoad == null) throw new InvalidOperationException("Original road missing.");
            var grey = oldRoad.GetComponent<MeshRenderer>().sharedMaterial;
            var road = Strip("Loop road validation",grey,false);
            Physics.SyncTransforms(); ValidateRoad(road.GetComponent<MeshCollider>());
            UnityEngine.Object.DestroyImmediate(oldRoad); road.name = "Rally_Road_Start_to_Finish";
            Remove("Loop Runoff Shoulders");
            Strip("Loop Runoff Shoulders",Material("Circuit_01_Runoff",new Color(.36f,.32f,.25f)),true);
            RebuildCheckpoints(); RebuildBoundaries(); CreateProps();
            Circuit01VehicleSetup.Install();
            if (EditorJsonUtility.ToJson(dynamics) != dynamicsBefore) throw new InvalidOperationException("Porsche tuning changed.");
            Remove("Circuit 01 Closed Loop v1"); Remove(Marker); new GameObject(Marker);
            EditorSceneManager.MarkSceneDirty(scene); AssetDatabase.SaveAssets();
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Loop could not be saved.");
            Debug.Log($"CIRCUIT_01_LOOP_V2_OK: {total:F1}m; one 200m speed straight; chained curves; 2 hairpins radius 22m; 3 hills; closed position and tangent; road raycasts passed; 4 puddles; Porsche tuning preserved.");
        }
        finally { busy = false; }
    }
    public static void RunBatch() { EditorSceneManager.OpenScene(ScenePath); Build(); }
    static void ValidateRoad(MeshCollider road)
    {
        for (float d = .25f; d < total; d += 2)
        {
            Sample(d,out var p,out var r,out float w);
            foreach (float offset in new[] { -.4f*w,0,.4f*w })
                if (!road.Raycast(new Ray(p+r*offset+Vector3.up*3,Vector3.down),out var hit,6) || Mathf.Abs(hit.point.y-p.y) > .15f)
                    throw new InvalidOperationException("Road gap at " + d);
        }
    }
    public static void RebuildCheckpoints()
    {
        Define();
        var manager = UnityEngine.Object.FindAnyObjectByType<RallyCheckpointManager>();
        if (manager == null) throw new InvalidOperationException("Existing timer missing.");
        foreach (var gate in manager.GetComponentsInChildren<RallyCheckpointTrigger>(true)) UnityEngine.Object.DestroyImmediate(gate.gameObject);
        int intervals = Mathf.CeilToInt((total - 12) / 180);
        var gates = new RallyCheckpointTrigger[intervals+1];
        for (int i=0;i<gates.Length;i++)
        {
            // Separate start/finish gates avoid triggering both in the spawn volume.
            float d = Mathf.Lerp(10,total-2,i/(float)intervals);
            Sample(d,out var p,out var r,out float w);
            var go = new GameObject(i==0 ? "Checkpoint_00_Start" : i==intervals ? "Checkpoint_Loop_Finish" : $"Checkpoint_{i:00}_{d:F0}m");
            go.transform.SetParent(manager.transform); go.transform.SetPositionAndRotation(p+Vector3.up*2,Quaternion.LookRotation(Vector3.Cross(r,Vector3.up)));
            go.AddComponent<BoxCollider>().size = new Vector3(w+13,4,2);
            gates[i]=go.AddComponent<RallyCheckpointTrigger>(); gates[i].Configure(manager,i);
        }
        manager.Configure(gates); EditorUtility.SetDirty(manager);
        Remove("Race Start Finish Markers"); var lines = new GameObject("Race Start Finish Markers");
        foreach (int i in new[] { 0,intervals })
        {
            var g=gates[i].transform; float w=g.GetComponent<BoxCollider>().size.x-13;
            var v=new List<Vector3>(); var f=new List<Face>();
            Quad(v,f,new Vector3(-w/2,0,-.4f),new Vector3(-w/2,0,.4f),new Vector3(w/2,0,.4f),new Vector3(w/2,0,-.4f));
            var stripe=ProBuilderMesh.Create(v,f); stripe.name=i==0 ? "Start Line" : "Finish Line";
            stripe.transform.SetParent(lines.transform); stripe.transform.SetPositionAndRotation(g.position-Vector3.up*1.975f,g.rotation);
            stripe.GetComponent<MeshRenderer>().sharedMaterial=Material(i==0?"Circuit_01_StartLine":"Circuit_01_LoopFinish",i==0?new Color(1,.55f,.03f):Color.white);
        }
    }
    public static void RebuildBoundaries()
    {
        Define(); Remove("Soft Track Boundaries"); var root=new GameObject("Soft Track Boundaries");
        var mat=AssetDatabase.LoadAssetAtPath<PhysicsMaterial>("Assets/Scenes/Circuit_01_SoftBoundary.physicMaterial");
        var ds=Distances(4);
        for(int i=0;i<ds.Length-1;i++)
        {
            Sample(ds[i],out var a,out var ar,out float aw); Sample(ds[i+1],out var b,out var br,out float bw);
            foreach(int side in new[]{-1,1})
            {
                var p=a+side*ar*(aw/2+6.5f);var q=b+side*br*(bw/2+6.5f);
                var go=new GameObject($"Boundary_{(side<0?"Left":"Right")}_{i:0000}");go.transform.SetParent(root.transform);
                go.transform.SetPositionAndRotation((p+q)/2+Vector3.up*1.25f,Quaternion.LookRotation(q-p));
                var box=go.AddComponent<BoxCollider>();box.size=new Vector3(1,2.5f,Vector3.Distance(p,q)+.8f);box.sharedMaterial=mat;
            }
        }
    }
    [MenuItem("Tools/Rally/Rebuild Circuit 01 Interactive Props")]
    public static void RebuildInteractiveProps()
    {
        var scene = SceneManager.GetActiveScene();
        if (busy || scene.path != ScenePath || EditorApplication.isPlayingOrWillChangePlaymode) return;
        busy = true;
        try
        {
            Define();
            CreateProps();
            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();
            if (!EditorSceneManager.SaveScene(scene))
                throw new InvalidOperationException("Interactive props could not be saved.");
            Debug.Log("CIRCUIT_01_INTERACTIVE_PROPS_OK: 4 puddle brake triggers at 3.5m/s^2 and dynamic hay bales saved.");
        }
        finally { busy = false; }
    }
    static void CreateProps()
    {
        Remove("Loop Puddles"); Remove("Loop Hay Bales"); Remove(InteractivePropsMarker);
        var waterRoot=new GameObject("Loop Puddles");var hayRoot=new GameObject("Loop Hay Bales");
        var water=Material("Circuit_01_WaterPlaceholder",new Color(.04f,.42f,.8f,.65f));
        water.SetFloat("_Surface",1); water.SetFloat("_SrcBlend",5); water.SetFloat("_DstBlend",10); water.SetFloat("_ZWrite",0);
        water.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");water.SetOverrideTag("RenderType","Transparent");water.renderQueue=3000;
        var hay=Material("Circuit_01_HayPlaceholder",new Color(.66f,.46f,.12f));
        float[] puddleDistances={100,originalLength+110,total*.66f,total*.88f};
        for(int i=0;i<puddleDistances.Length;i++)
        {
            float w=i==0?6:5, length=i==0?9:i==1?12:8,d=puddleDistances[i];
            Sample(d,out var p,out var r,out float roadWidth);
            var v=new List<Vector3>();var f=new List<Face>();
            Quad(v,f,new Vector3(-w/2,0,-length/2),new Vector3(-w/2,0,length/2),new Vector3(w/2,0,length/2),new Vector3(w/2,0,-length/2));
            var mesh=ProBuilderMesh.Create(v,f);mesh.name=$"Puddle_{i+1:00}_distance_{d:F1}m_size_{w}x{length}m";
            mesh.transform.SetParent(waterRoot.transform);mesh.transform.SetPositionAndRotation(p+Vector3.up*.035f,Quaternion.LookRotation(Vector3.Cross(r,Vector3.up)));
            mesh.GetComponent<MeshRenderer>().sharedMaterial=water;
            var trigger=mesh.gameObject.AddComponent<BoxCollider>();trigger.isTrigger=true;
            trigger.center=new Vector3(0,.45f,0);trigger.size=new Vector3(w,.9f,length);
            mesh.gameObject.AddComponent<RallyPuddleSlowZone>();
        }
        int id=0;
        foreach(var bend in sections.Where(s=>s.curvature!=0 && Mathf.Abs(s.curvature*s.length*Mathf.Rad2Deg)>=90).Take(8))
        for(int j=0;j<3;j++)
        {
            float d=bend.start+bend.length*(.4f+.1f*j);Sample(d,out var p,out var r,out float w);
            var mesh=ShapeGenerator.GenerateCylinder(PivotLocation.Center,12,.7f,1.3f,0);
            mesh.name=$"HayBale_{++id:00}_outside_{d:F0}m";mesh.transform.SetParent(hayRoot.transform);
            mesh.transform.position=p-Mathf.Sign(bend.curvature)*r*(w/2+3)+Vector3.up*.65f;
            mesh.GetComponent<MeshRenderer>().sharedMaterial=hay;
            foreach(var col in mesh.GetComponents<Collider>()) UnityEngine.Object.DestroyImmediate(col);
            var capsule=mesh.gameObject.AddComponent<CapsuleCollider>();capsule.direction=1;capsule.radius=.7f;capsule.height=1.3f;
            var body=mesh.gameObject.AddComponent<Rigidbody>();body.mass=22;body.linearDamping=.15f;body.angularDamping=.25f;
            body.interpolation=RigidbodyInterpolation.Interpolate;body.collisionDetectionMode=CollisionDetectionMode.ContinuousDynamic;body.maxAngularVelocity=30;
        }
        new GameObject(InteractivePropsMarker);
        EditorUtility.SetDirty(water);
    }
}
