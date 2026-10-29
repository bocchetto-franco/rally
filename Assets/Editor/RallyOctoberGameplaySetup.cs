using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.ProBuilder;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

public static class RallyOctoberGameplaySetup
{
    const string Report="Logs/Gameplay_20261028/setup.txt";
    const string Art="Assets/Art/RallyTurbo";
    struct Edge { public Vector3 a,b; public int count; }
    static List<Edge> edges;
    static Dictionary<string,Vector3> shifted;
    static Bounds? bridge;
    static float factor;
    static float bridgeBlend;
    static Dictionary<Vector2Int,List<Edge>> edgeGrid;
    static IEnumerable<T> All<T>(Scene scene) where T:Component => scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<T>(true));
    static void Set(Object target,string field,float value) {var s=new SerializedObject(target);s.FindProperty(field).floatValue=value;s.ApplyModifiedPropertiesWithoutUndo();}
    static string Key(Vector3 p)=>Mathf.RoundToInt(p.x*1000)+":"+Mathf.RoundToInt(p.z*1000);
    static string EdgeKey(Vector3 a,Vector3 b){string x=Key(a),y=Key(b);return string.CompareOrdinal(x,y)<0?x+"|"+y:y+"|"+x;}
    static Vector2 Flat(Vector3 v)=>new Vector2(v.x,v.z);
    static float Cross(Vector2 a,Vector2 b)=>a.x*b.y-a.y*b.x;

    [MenuItem("Tools/Rally/Apply October Gameplay")]
    public static void Run()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)throw new Exception("Editor must be idle");
        for(int i=0;i<SceneManager.sceneCount;i++)if(SceneManager.GetSceneAt(i).isDirty)throw new Exception("Unsaved scene");
        Directory.CreateDirectory("Logs/Gameplay_20261028");
        File.WriteAllText(Report,"October gameplay setup; source scenes backed up before changes\n");
        var setup=EditorSceneManager.GetSceneManagerSetup();
        try
        {
            var lightning=Lightning();var material=Material();
            for(int index=1;index<=3;index++)
            {
                var scene=EditorSceneManager.OpenScene($"Assets/Scenes/Circuit_0{index}.unity");
                string roadName=index==1?"Rally_Road_Start_to_Finish":$"Circuit0{index}_Road";
                var road=All<ProBuilderMesh>(scene).Single(m=>m.name==roadName);
                var settings=road.GetComponent<RallyRoadWidthProfile>()??road.gameObject.AddComponent<RallyRoadWidthProfile>();
                if(!settings.applied)
                {
                    factor=settings.widthMultiplier;
                    bridgeBlend=settings.bridgeBlendDistance;
                    bridge=null;
                    if(index==3)
                    {
                        var deck=All<Collider>(scene).FirstOrDefault(c=>c.name=="Continuous smooth deck collider");
                        if(deck!=null)bridge=deck.bounds;
                    }
                    try { Widen(scene,road,settings); }
                    catch(Exception error) when(index==3)
                    {
                        File.AppendAllText(Report,"C03 width retained to protect custom bridge geometry: "+error.Message+"\n");
                        scene=EditorSceneManager.OpenScene(scene.path);road=All<ProBuilderMesh>(scene).Single(m=>m.name==roadName);
                        settings=road.GetComponent<RallyRoadWidthProfile>()??road.gameObject.AddComponent<RallyRoadWidthProfile>();settings.widthMultiplier=1;
                    }
                    settings.applied=true;EditorUtility.SetDirty(settings);
                }
                foreach(var dynamics in All<RallyVehicleDynamics>(scene))
                    if(dynamics.VehicleController!=null && dynamics.VehicleController.GetComponentInParent<RallyBotController>()==null)
                    {Set(dynamics,"maximumSteerAngle",55);Set(dynamics,"steeringResponse",96);Set(dynamics.VehicleController,"maxSteerAngle",55);}
                foreach(var input in All<JrsInputController>(scene))Set(input,"steerSpeed",96);
                foreach(var input in All<RallyLocalPlayerInput>(scene))Set(input,"steeringResponse",96);
                foreach(var bot in All<RallyBotController>(scene)){Set(bot,"easySpeedMultiplier",.65025f);Set(bot,"easyBrakingMultiplier",.765f);}
                foreach(var car in All<JrsVehicleController>(scene))
                    if(car.name==RallyGameSession.VehicleName || car.GetComponentInParent<RallyBotController>()!=null)
                        if(car.GetComponent<RallyVehicleTurbo>()==null)car.gameObject.AddComponent<RallyVehicleTurbo>();
                foreach(var camera in All<JrsFollowCamera>(scene))
                    if(camera.GetComponent<Camera>()!=null && camera.GetComponent<RallyCameraRoadShake>()==null)camera.gameObject.AddComponent<RallyCameraRoadShake>();
                foreach(var zone in All<RallyTurboZone>(scene))
                {
                    foreach(Transform child in zone.transform.Cast<Transform>().ToArray())Object.DestroyImmediate(child.gameObject);
                    var visual=new GameObject("Turbo Lightning Pickup",typeof(MeshFilter),typeof(MeshRenderer));visual.transform.SetParent(zone.transform,false);
                    visual.transform.localPosition=new Vector3(0,1.8f,0);visual.GetComponent<MeshFilter>().sharedMesh=lightning;
                    var renderer=visual.GetComponent<MeshRenderer>();renderer.sharedMaterial=material;
                    renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;renderer.receiveShadows=false;
                    var s=new SerializedObject(zone);s.FindProperty("pickupVisual").objectReferenceValue=visual.transform;s.ApplyModifiedPropertiesWithoutUndo();
                }
                Physics.SyncTransforms();VerifyRoute(scene,road);
                EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
                File.AppendAllText(Report,$"{scene.name}: saved, {All<RallyTurboZone>(scene).Count()} pickups, steering 55/96, bot multipliers .65025/.765\n");
            }
            foreach(string guid in AssetDatabase.FindAssets("Bot_Car t:Prefab"))
            {
                string path=AssetDatabase.GUIDToAssetPath(guid);var root=PrefabUtility.LoadPrefabContents(path);
                try{foreach(var bot in root.GetComponentsInChildren<RallyBotController>(true)){Set(bot,"easySpeedMultiplier",.65025f);Set(bot,"easyBrakingMultiplier",.765f);}PrefabUtility.SaveAsPrefabAsset(root,path);}
                finally{PrefabUtility.UnloadPrefabContents(root);}
            }
            File.AppendAllText(Report,"COMPLETE: PASS\n");
        }
        catch(Exception error){File.AppendAllText(Report,"FAILED: "+error+"\n");throw;}
        finally{if(setup.Any(s=>s.isActive && s.isLoaded))EditorSceneManager.RestoreSceneManagerSetup(setup);else EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity");}
    }

    static float ScaleAt(Vector3 point)
    {
        if(!bridge.HasValue)return factor;
        var b=bridge.Value;Vector3 closest=b.ClosestPoint(new Vector3(point.x,b.center.y,point.z));
        float gap=Vector2.Distance(Flat(point),Flat(closest));
        return Mathf.Lerp(1,factor,Mathf.SmoothStep(0,1,Mathf.InverseLerp(5,5+bridgeBlend,gap)));
    }

    static void Widen(Scene scene,ProBuilderMesh road,RallyRoadWidthProfile settings)
    {
        var vertices=road.positions.Select(road.transform.TransformPoint).ToArray();
        var map=new Dictionary<string,Edge>();
        foreach(var face in road.faces)
        {
            var ids=face.distinctIndexes.OrderBy(i=>i).ToArray();if(ids.Length!=4)throw new Exception("Road requires quad surface");
            for(int i=0;i<4;i++)
            {
                var a=vertices[ids[i]];var b=vertices[ids[(i+1)%4]];string key=EdgeKey(a,b);
                if(map.TryGetValue(key,out var edge)){edge.count++;map[key]=edge;}else map[key]=new Edge{a=a,b=b,count=1};
            }
        }
        edges=map.Values.Where(e=>e.count==1).ToList();shifted=new Dictionary<string,Vector3>();
        edgeGrid=new Dictionary<Vector2Int,List<Edge>>();
        foreach(var edge in edges)
        {
            var min=Cell(Vector3.Min(edge.a,edge.b));var max=Cell(Vector3.Max(edge.a,edge.b));
            for(int x=min.x;x<=max.x;x++)for(int z=min.y;z<=max.y;z++)
            {var cell=new Vector2Int(x,z);if(!edgeGrid.TryGetValue(cell,out var bucket))edgeGrid[cell]=bucket=new List<Edge>();bucket.Add(edge);}
        }
        File.AppendAllText(Report,$"{scene.name}: indexing {vertices.Length} road vertices / {road.faces.Count} faces / {edges.Count} perimeter edges\n");
        foreach(var face in road.faces)
        {
            var ids=face.distinctIndexes.OrderBy(i=>i).ToArray();
            ExpandPair(vertices[ids[0]],vertices[ids[3]]);ExpandPair(vertices[ids[1]],vertices[ids[2]]);
        }
        var modified=vertices.Select(v=>{var p=shifted[Key(v)];p.y=v.y;return road.transform.InverseTransformPoint(p);}).ToArray();
        SavePositions(road,modified);
        // Move inner shoulder/blend vertices smoothly, keeping outer terrain seam fixed.
        foreach(var mesh in All<ProBuilderMesh>(scene))
            if(mesh!=road && (mesh.name=="Loop Runoff Shoulders" || mesh.name.Contains("_Runoff")))
                SavePositions(mesh,mesh.positions.Select(p=>mesh.transform.InverseTransformPoint(ShiftOutside(mesh.transform.TransformPoint(p),true))).ToArray());
        var blend=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).FirstOrDefault(t=>t.name=="Road to terrain - soft edges");
        if(blend!=null)foreach(var filter in blend.GetComponentsInChildren<MeshFilter>(true))
        {
            Mesh mesh=Object.Instantiate(filter.sharedMesh);mesh.name=filter.sharedMesh.name+" wide";
            mesh.vertices=mesh.vertices.Select(p=>filter.transform.InverseTransformPoint(ShiftOutside(filter.transform.TransformPoint(p),true))).ToArray();
            mesh.RecalculateBounds();mesh.RecalculateNormals();filter.sharedMesh=mesh;
        }
        foreach(var wall in All<BoxCollider>(scene).Where(c=>c.enabled && RallyTrackBoundary.IsBoundary(c)))
        {
            Vector3 a=wall.transform.TransformPoint(wall.center-Vector3.forward*wall.size.z*.5f);
            Vector3 b=wall.transform.TransformPoint(wall.center+Vector3.forward*wall.size.z*.5f);
            a=ShiftOutside(a,false);b=ShiftOutside(b,false);
            Vector3 direction=b-a;
            if(direction.sqrMagnitude<.01f)continue;
            wall.transform.rotation=Quaternion.LookRotation(direction,Vector3.up);
            wall.transform.position=(a+b)*.5f-wall.transform.TransformVector(wall.center);
            Vector3 size=wall.size;size.z=direction.magnitude/Mathf.Abs(wall.transform.lossyScale.z);wall.size=size;
        }
        File.AppendAllText(Report,$"{scene.name}: widened road {factor:F2}x, {edges.Count} outer edges; bridge protected={bridge.HasValue}\n");
    }

    static void ExpandPair(Vector3 a,Vector3 b)
    {
        if((Flat(b)-Flat(a)).sqrMagnitude<.00001f || Mathf.Approximately(ScaleAt((a+b)*.5f),1f))
        {if(!shifted.ContainsKey(Key(a)))shifted[Key(a)]=a;if(!shifted.ContainsKey(Key(b)))shifted[Key(b)]=b;return;}
        Vector2 centre=(Flat(a)+Flat(b))*.5f, dir=(Flat(b)-Flat(a)).normalized;
        float left=float.NegativeInfinity,right=float.PositiveInfinity;
        foreach(var edge in Nearby(new Vector3(centre.x,a.y,centre.y)))
        {
            Vector2 p=Flat(edge.a),segment=Flat(edge.b)-p;float denominator=Cross(dir,segment);
            if(Mathf.Abs(denominator)<.00001f)continue;
            float t=Cross(p-centre,segment)/denominator,u=Cross(p-centre,dir)/denominator;
            if(u<-.001f || u>1.001f)continue;
            if(t<=.001f)left=Mathf.Max(left,t);if(t>=-.001f)right=Mathf.Min(right,t);
        }
        if(float.IsInfinity(left)||float.IsInfinity(right)||right-left<.1f)throw new Exception("Cannot resolve road cross-section");
        Vector2 middle=centre+dir*((left+right)*.5f);
        foreach(var point in new[]{a,b})
        {
            string key=Key(point);if(shifted.ContainsKey(key))continue;
            Vector2 result=middle+(Flat(point)-middle)*ScaleAt(new Vector3(middle.x,point.y,middle.y));
            shifted[key]=new Vector3(result.x,point.y,result.y);
        }
    }

    static Vector3 ShiftOutside(Vector3 point,bool feather)
    {
        float nearest=float.PositiveInfinity;Vector3 best=Vector3.zero,delta=Vector3.zero;
        foreach(var edge in Nearby(point))
        {
            Vector2 leg=Flat(edge.b-edge.a);float t=Mathf.Clamp01(Vector2.Dot(Flat(point-edge.a),leg)/Mathf.Max(.001f,leg.sqrMagnitude));
            Vector3 p=Vector3.Lerp(edge.a,edge.b,t);float distance=(Flat(point)-Flat(p)).sqrMagnitude;
            if(distance>=nearest)continue;nearest=distance;best=p;
            delta=Vector3.Lerp(shifted[Key(edge.a)]-edge.a,shifted[Key(edge.b)]-edge.b,t);delta.y=0;
        }
        float amount=1;
        if(feather && Vector3.Dot(point-best,delta)>0)amount=1-Mathf.SmoothStep(0,1,Mathf.Sqrt(nearest)/7f);
        return point+delta*amount;
    }
    static Vector2Int Cell(Vector3 p)=>new Vector2Int(Mathf.FloorToInt(p.x/32),Mathf.FloorToInt(p.z/32));
    static IEnumerable<Edge> Nearby(Vector3 p)
    {
        var cell=Cell(p);
        for(int x=-1;x<=1;x++)for(int z=-1;z<=1;z++)
            if(edgeGrid.TryGetValue(cell+new Vector2Int(x,z),out var bucket))foreach(var edge in bucket)yield return edge;
    }
    static void SavePositions(ProBuilderMesh mesh,Vector3[] vertices)
    {
        mesh.MakeUnique(); // Never edit a mesh referenced by a menu backdrop/another scene.
        mesh.positions=vertices;mesh.ToMesh();mesh.Refresh(RefreshMask.All & ~RefreshMask.UV);
        var collider=mesh.GetComponent<MeshCollider>();if(collider!=null){collider.sharedMesh=null;collider.sharedMesh=mesh.GetComponent<MeshFilter>().sharedMesh;}
        EditorUtility.SetDirty(mesh);EditorUtility.SetDirty(mesh.GetComponent<MeshFilter>().sharedMesh);
    }
    public static void VerifyRoute(Scene scene,ProBuilderMesh road)
    {
        var route=scene.GetRootGameObjects().First(g=>g.name=="AI_Waypoints").transform;
        var surfaces=All<Collider>(scene).Where(c=>c.enabled && !c.isTrigger &&
            (c.name==road.name || c.name=="Continuous smooth deck collider")).ToArray();
        int checkedPoints=0;
        for(int i=0;i<route.childCount;i++)
        {
            Vector3 a=route.GetChild(i).position,b=route.GetChild((i+1)%route.childCount).position;
            int count=Mathf.Max(1,Mathf.CeilToInt(Vector3.Distance(a,b)/2));
            for(int j=0;j<count;j++)
            {
                Vector3 point=Vector3.Lerp(a,b,j/(float)count),forward=(b-a).normalized,right=Vector3.Cross(Vector3.up,forward).normalized;
                foreach(float offset in new[]{-1f,0f,1f})
                {
                    var ray=new Ray(point+right*offset+Vector3.up*10,Vector3.down);bool supported=false;
                    float surfaceY=point.y;
                    foreach(var surface in surfaces)if(surface.Raycast(ray,out var hit,30)){supported=true;surfaceY=hit.point.y;break;}
                    if(!supported)throw new Exception($"{scene.name}: missing driving support at {i}/{j}, lane {offset}");
                    var centre=new Vector3(point.x,surfaceY+.9f,point.z)+right*offset;
                    foreach(var obstruction in Physics.OverlapBox(centre,new Vector3(.85f,.4f,1.5f),Quaternion.LookRotation(forward),~0,QueryTriggerInteraction.Ignore))
                        if(RallyTrackBoundary.IsBoundary(obstruction))throw new Exception($"{scene.name}: containment blocks car envelope at {i}/{j}, lane {offset}: {obstruction.name}");
                    checkedPoints++;
                }
            }
        }
        File.AppendAllText(Report,$"{scene.name}: support PASS at {checkedPoints} route/lane samples, including closure\n");
    }

    static Material Material()
    {
        string path=Art+"/Turbo pickup gold.mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(m==null){m=new Material(Shader.Find("Universal Render Pipeline/Unlit"));m.color=new Color(1f,.82f,.08f);AssetDatabase.CreateAsset(m,path);}return m;
    }
    static Mesh Lightning()
    {
        string path=Art+"/Turbo lightning.asset";var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(mesh!=null)return mesh;
        var outline=new[]{new Vector2(.15f,1.2f),new Vector2(-.55f,-.1f),new Vector2(-.08f,-.1f),new Vector2(-.28f,-1.1f),new Vector2(.65f,.3f),new Vector2(.08f,.3f)};
        var v=new List<Vector3>();foreach(float z in new[]{-.09f,.09f})foreach(var p in outline)v.Add(new Vector3(p.x,p.y,z));
        int[] front={0,1,2,0,2,5,2,3,4,2,4,5};var triangles=new List<int>(front);
        for(int i=0;i<front.Length;i+=3)triangles.AddRange(new[]{front[i]+6,front[i+2]+6,front[i+1]+6});
        for(int i=0;i<6;i++){int n=(i+1)%6;triangles.AddRange(new[]{i,i+6,n+6,i,n+6,n});}
        mesh=new Mesh{name="Turbo lightning"};mesh.SetVertices(v);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();AssetDatabase.CreateAsset(mesh,path);return mesh;
    }
}
