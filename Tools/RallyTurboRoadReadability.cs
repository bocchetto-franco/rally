using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

// Explicit editor operation: saved turbo poses/hitboxes and private road materials only.
public static class RallyTurboRoadReadability
{
    const string Report = "Logs/turbo-road-readability.txt";
    const string Folder = "Assets/Art/RoadSurfaces/ReadableRoads";
    class Route
    {
        public Vector3[] p; public float[] d;
        public float[] curveEntries;
        public float Length => d[d.Length - 1];
        public Vector3 At(float s)
        {
            s = Mathf.Repeat(s, Length);
            for (int i = 0; i < p.Length; i++) if (s <= d[i+1]) return Vector3.Lerp(p[i], p[(i+1)%p.Length], Mathf.InverseLerp(d[i], d[i+1], s));
            return p[0];
        }
        public float Station(Vector3 point)
        {
            float best = float.PositiveInfinity, station = 0;
            for (int i = 0; i < p.Length; i++)
            {
                Vector3 delta = p[(i+1)%p.Length] - p[i];
                float t = Mathf.Clamp01(Vector3.Dot(point-p[i], delta)/Mathf.Max(.001f, delta.sqrMagnitude));
                float error = (point-p[i]-delta*t).sqrMagnitude;
                if (error < best) { best=error; station=d[i]+delta.magnitude*t; }
            }
            return station;
        }
        public float Ahead(float from, float to) => Mathf.Repeat(to-from, Length);
    }
    class Candidate
    {
        public float station, clear, angle; public Vector3 position, forward, normal;
    }
    static IEnumerable<T> All<T>(Scene s) where T:Component => s.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<T>(true));
    static Route Read(Scene s)
    {
        var root=s.GetRootGameObjects().Single(r=>r.name=="AI_Waypoints").transform;
        var p=Enumerable.Range(0,root.childCount).Select(i=>root.GetChild(i).position).ToArray();
        var r=new Route{p=p,d=new float[p.Length+1]};
        for(int i=0;i<p.Length;i++)r.d[i+1]=r.d[i]+Vector3.Distance(p[i],p[(i+1)%p.Length]);
        return r;
    }
    static Collider[] Roads(Scene s)=>All<Collider>(s).Where(c=>c.enabled&&!c.isTrigger&&(c.name=="Rally_Road_Start_to_Finish"||c.name=="Circuit02_Road"||c.name=="Circuit03_Road"||c.name=="Continuous smooth deck collider")).ToArray();
    static bool Ground(Collider[] roads,Vector3 point,out RaycastHit best)
    {
        best=default;bool found=false;
        foreach(var road in roads)if(road.Raycast(new Ray(point+Vector3.up*35,Vector3.down),out var hit,75)&&(!found||hit.point.y>best.point.y)){best=hit;found=true;}
        return found;
    }
    static float DistanceToNextCurve(Route route, float station, RallyBrakeWarningTrigger[] warnings)
    {
        float result=route.Length;
        if(route.curveEntries==null)route.curveEntries=warnings.Where(w=>w.CornerGrade<5).Select(w=>route.Station(w.CurveEntry)).Distinct().ToArray();
        // Advisory trigger volumes overlap most of the forest/S sections, including
        // exits. Use actual corner entries (not grade-5 gentle kinks) plus future geometry.
        foreach(float entry in route.curveEntries)
        {
            result=Mathf.Min(result,route.Ahead(station,entry));
        }
        return result;
    }
    static List<Candidate> Candidates(Scene scene,Route route,float clearance=100)
    {
        var road=Roads(scene); var warnings=All<RallyBrakeWarningTrigger>(scene).ToArray();
        var water=All<RallyPuddleSlowZone>(scene).Select(w=>w.GetComponent<Collider>()).ToArray();
        var result=new List<Candidate>();
        for(float s=20;s<route.Length-20;s+=3)
        {
            float clear=DistanceToNextCurve(route,s,warnings);
            if(clear<clearance)continue;
            Vector3 point=route.At(s),heading=(route.At(s+8)-route.At(s-8)).normalized;
            float angle=0;
            for(float offset=0;offset<=55;offset+=10)
                angle=Mathf.Max(angle,Vector3.Angle(Vector3.ProjectOnPlane(heading,Vector3.up),Vector3.ProjectOnPlane(route.At(s+offset+8)-route.At(s+offset-8),Vector3.up)));
            if(angle>25)continue;
            if(water.Any(w=>{var b=w.bounds;b.Expand(new Vector3(8,8,18));return b.Contains(point);}))continue;
            if(!Ground(road,point,out var hit)||hit.normal.y<.85f||Mathf.Abs(hit.point.y-point.y)>3)continue;
            Vector3 right=Vector3.Cross(hit.normal,heading).normalized;
            if(!Ground(road,point+right*2.5f,out _)||!Ground(road,point-right*2.5f,out _))continue;
            result.Add(new Candidate{station=s,clear=clear,angle=angle,position=hit.point,normal=hit.normal,forward=Vector3.ProjectOnPlane(heading,hit.normal).normalized});
        }
        return result;
    }
    static Candidate[] Choose(Scene scene,Route route)
    {
        var candidates=Candidates(scene,route,80);var chosen=new List<Candidate>();
        var zones=All<RallyTurboZone>(scene).OrderBy(z=>z.name,StringComparer.Ordinal).ToArray();
        var roads=Roads(scene);
        foreach(float fraction in new[]{.18f,.48f,.77f})
        {
            var candidate=candidates.Where(c=>chosen.All(p=>Mathf.Min(route.Ahead(p.station,c.station),route.Ahead(c.station,p.station))>=95&&Vector3.Distance(c.position,p.position)>=60))
                .Where(c=>Fits(zones[chosen.Count],c,roads))
                .OrderBy(c=>Mathf.Abs(c.station-route.Length*fraction)+c.angle*3-Mathf.Min(c.clear,180)*.1f).FirstOrDefault();
            if(candidate==null)throw new Exception(scene.name+": insufficient safe zones; candidates="+candidates.Count+", chosen="+chosen.Count);
            chosen.Add(candidate);
        }
        return chosen.ToArray();
    }
    static bool Fits(RallyTurboZone zone,Candidate candidate,Collider[] roads)
    {
        Quaternion rotation=Quaternion.LookRotation(candidate.forward,candidate.normal);
        foreach(var filter in zone.GetComponentsInChildren<MeshFilter>())foreach(Vector3 vertex in filter.sharedMesh.vertices)
        {
            Vector3 local=zone.transform.InverseTransformPoint(filter.transform.TransformPoint(vertex));
            Vector3 world=candidate.position+rotation*local;
            if(!Ground(roads,world,out var hit))return false;
            float gap=world.y-hit.point.y;
            if(gap<.003f||gap>.09f)return false;
        }
        return true;
    }
    static void Idle()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling||BuildPipeline.isBuildingPlayer)throw new Exception("Unity must be idle");
        for(int i=0;i<SceneManager.sceneCount;i++)if(SceneManager.GetSceneAt(i).isDirty)throw new Exception("Unsaved scene "+SceneManager.GetSceneAt(i).path);
    }
    public static void Plan()
    {
        Idle();File.WriteAllText(Report,"Turbo/road plan from saved scene geometry and real brake approach triggers\n");
        foreach(string name in RallyGameSession.CircuitScenes)
        {
            var scene=EditorSceneManager.OpenPreviewScene("Assets/Scenes/"+name+".unity");Physics.SyncTransforms();
            try
            {
                var route=Read(scene);var warnings=All<RallyBrakeWarningTrigger>(scene).ToArray();
                File.AppendAllText(Report,name+" length="+route.Length+" safeCandidates100m="+Candidates(scene,route).Count+" candidates80m="+Candidates(scene,route,80).Count+"\n");
                foreach(var old in All<RallyTurboZone>(scene))
                {
                    float s=route.Station(old.transform.position);
                    File.AppendAllText(Report,"OLD "+old.name+" station="+s+" nextCurve="+DistanceToNextCurve(route,s,warnings)+"\n");
                }
                foreach(var p in Choose(scene,route))File.AppendAllText(Report,$"NEW station={p.station:F1}; next-curve-entry={p.clear:F1}m; heading-change-next55m={p.angle:F1}deg; position={p.position:F3}\n");
                if(name!="Circuit_03")
                {
                    var renderer=All<MeshRenderer>(scene).Single(r=>r.name==(name=="Circuit_01"?"Rally_Road_Start_to_Finish":"Circuit02_Road"));
                    File.AppendAllText(Report,"ROAD material="+AssetDatabase.GetAssetPath(renderer.sharedMaterial)+" tint="+renderer.sharedMaterial.GetColor("_BaseColor")+"\n");
                }
            }
            finally{EditorSceneManager.ClosePreviewScene(scene);}
        }
        File.AppendAllText(Report,"PLAN: PASS\n");
    }
    public static void Diagnose()
    {
        Idle();File.WriteAllText(Report,"Candidate diagnostic\n");
        foreach(string name in RallyGameSession.CircuitScenes)
        {
            var scene=EditorSceneManager.OpenPreviewScene("Assets/Scenes/"+name+".unity");Physics.SyncTransforms();
            try
            {
                var route=Read(scene);
                foreach(float margin in new[]{100f,80f,60f,40f})
                {
                    var list=Candidates(scene,route,margin);
                    File.AppendAllText(Report,name+" margin="+margin+" count="+list.Count+" stations="+string.Join(",",list.Select(c=>c.station.ToString("F0")))+"\n");
                }
                var zones=All<RallyTurboZone>(scene).OrderBy(z=>z.name,StringComparer.Ordinal).ToArray();
                foreach(var c in Candidates(scene,route,80))
                    File.AppendAllText(Report,$"FIT {name}: station={c.station:F1} clear={c.clear:F1} angle={c.angle:F1} zones={string.Join(",",zones.Select(z=>Fits(z,c,Roads(scene))?"yes":"no"))}\n");
                foreach(var note in All<RallyBrakeWarningTrigger>(scene).GroupBy(t=>t.CurveEntry).Select(g=>g.First()).OrderBy(t=>route.Station(t.CurveEntry)))
                    File.AppendAllText(Report,$"CURVE {name}: entry={route.Station(note.CurveEntry):F1}, grade={note.CornerGrade}, target={note.TargetSpeedKph}, point={note.CurveEntry}\n");
            }
            finally{EditorSceneManager.ClosePreviewScene(scene);}
        }
    }

    public static void Apply()
    {
        Idle(); Plan(); // Every circuit must have three safe supported placements before edits.
        string backup="Logs/SceneBackups/TurboReadability_"+DateTime.Now.ToString("yyyyMMdd_HHmmss");
        Directory.CreateDirectory(backup);
        if(!AssetDatabase.IsValidFolder(Folder))AssetDatabase.CreateFolder("Assets/Art/RoadSurfaces","ReadableRoads");
        var setup=EditorSceneManager.GetSceneManagerSetup();
        try
        {
            foreach(string name in RallyGameSession.CircuitScenes)
            {
                string path="Assets/Scenes/"+name+".unity";File.Copy(path,backup+"/"+name+".unity");
                var scene=EditorSceneManager.OpenScene(path);Physics.SyncTransforms();
                var route=Read(scene);var choices=Choose(scene,route);
                var zones=All<RallyTurboZone>(scene).OrderBy(z=>z.name,StringComparer.Ordinal).ToArray();
                var allowed=new HashSet<Component>();
                foreach(var z in zones){allowed.Add(z.transform);allowed.Add(z);allowed.Add(z.GetComponent<BoxCollider>());}
                MeshRenderer road=null;Material original=null;
                if(name!="Circuit_03")
                {
                    road=All<MeshRenderer>(scene).Single(r=>r.name==(name=="Circuit_01"?"Rally_Road_Start_to_Finish":"Circuit02_Road"));
                    original=road.sharedMaterial;allowed.Add(road);
                    CaptureRoad(route,name,"Before");
                }
                var protectedState=All<Component>(scene).Where(c=>c!=null&&!allowed.Contains(c)).ToDictionary(c=>c,c=>EditorJsonUtility.ToJson(c));
                for(int i=0;i<zones.Length;i++)
                {
                    var zone=zones[i];var p=choices[i];
                    var effect=new SerializedObject(zone);
                    string[] keys={"minimumSpeedKph","minimumForwardAlignment","durationSeconds","accelerationMps2","maximumSpeedKph","botSpeedBonusKph","rearmSeconds"};
                    float[] values=keys.Select(k=>effect.FindProperty(k).floatValue).ToArray();
                    Vector3 scale=zone.transform.localScale;
                    zone.transform.SetPositionAndRotation(p.position,Quaternion.LookRotation(p.forward,p.normal));
                    zone.name=zone.name.Replace("1.4m","4.4m");
                    var box=zone.GetComponent<BoxCollider>();box.size=new Vector3(5.2f,box.size.y,10f);
                    effect.Update();effect.FindProperty("activationHalfWidth").floatValue=2.2f;effect.ApplyModifiedPropertiesWithoutUndo();
                    effect.Update();
                    for(int k=0;k<keys.Length;k++)if(effect.FindProperty(keys[k]).floatValue!=values[k])throw new Exception("Turbo effect changed: "+keys[k]);
                    if(zone.transform.localScale!=scale||!box.isTrigger)throw new Exception("Unexpected turbo scale/solid collider change");
                    if(!Fits(zone,p,Roads(scene)))throw new Exception("Arrow markers not supported after move");
                    File.AppendAllText(Report,$"SAVED {name}/{zone.name}: station={p.station:F1}; next curve entry={p.clear:F1}m; lane width=4.4m; collider=5.2x{box.size.y}x10m; same boost and arrow assets\n");
                    EditorUtility.SetDirty(zone);EditorUtility.SetDirty(box);EditorUtility.SetDirty(zone.transform);
                }
                if(road!=null)
                {
                    string matPath=Folder+"/"+name+" - readable gravel.mat";
                    var mat=AssetDatabase.LoadAssetAtPath<Material>(matPath);
                    if(mat==null){mat=new Material(original){name=name+" - readable gravel"};AssetDatabase.CreateAsset(mat,matPath);}
                    Color tint=name=="Circuit_01"?new Color(.62f,.53f,.40f,1):new Color(.60f,.58f,.50f,1);
                    mat.SetColor("_BaseColor",tint);if(mat.HasProperty("_Color"))mat.SetColor("_Color",tint);
                    foreach(string texture in new[]{"_BaseMap","_BumpMap","_MetallicGlossMap","_OcclusionMap"})
                        if(mat.GetTexture(texture)!=original.GetTexture(texture)||mat.GetTextureScale(texture)!=original.GetTextureScale(texture))throw new Exception("PBR texture/tiling changed");
                    road.sharedMaterial=mat;EditorUtility.SetDirty(mat);EditorUtility.SetDirty(road);
                    File.AppendAllText(Report,"SAVED ROAD "+name+": "+matPath+" tint="+tint+"; existing PBR maps, UVs and soft edge strips retained\n");
                    CaptureRoad(route,name,"After");
                }
                VerifyActivation(zones);
                foreach(var state in protectedState)if(state.Key==null||EditorJsonUtility.ToJson(state.Key)!=state.Value)throw new Exception("Protected component changed: "+name+"/"+state.Key);
                EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
                File.AppendAllText(Report,$"PRESERVATION {name}: PASS, {protectedState.Count} other components byte-equivalent in Editor JSON\n");
            }
            File.AppendAllText(Report,"COMPLETE: PASS. Backup: "+backup+"\n");
        }
        finally{EditorSceneManager.RestoreSceneManagerSetup(setup);}
    }
    static void CaptureRoad(Route route,string scene,string phase)
    {
        if(SystemInfo.graphicsDeviceType==UnityEngine.Rendering.GraphicsDeviceType.Null)
        {
            File.AppendAllText(Report,"CAPTURE "+scene+" "+phase+": skipped (headless graphics device); material/geometry checks still enforced\n");
            return;
        }
        float station=scene=="Circuit_01"?750:510;
        Vector3 focus=route.At(station),forward=(route.At(station+8)-route.At(station-8)).normalized;
        var go=new GameObject("Temporary road contrast preview",typeof(Camera));var camera=go.GetComponent<Camera>();
        camera.transform.position=focus-forward*11+Vector3.up*6;
        camera.transform.LookAt(focus+forward*12);camera.fieldOfView=60;camera.farClipPlane=300;
        var data=camera.GetUniversalAdditionalCameraData();data.requiresDepthTexture=true;data.requiresColorTexture=true;
        var rt=new RenderTexture(1280,720,24);var pixels=new Texture2D(1280,720,TextureFormat.RGB24,false);var prior=RenderTexture.active;
        try
        {
            camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;
            pixels.ReadPixels(new Rect(0,0,1280,720),0,0);pixels.Apply();
            File.WriteAllBytes("Logs/"+scene+"_RoadContrast_"+phase+".png",pixels.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture=null;RenderTexture.active=prior;UnityEngine.Object.DestroyImmediate(pixels);
            rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(go);
        }
    }
    static void VerifyActivation(RallyTurboZone[] zones)
    {
        var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
        float oldScale=Time.timeScale;Time.timeScale=1;
        try
        {
            foreach(var zone in zones)
            {
                typeof(RallyTurboZone).GetMethod("Awake",flags).Invoke(zone,null);
                float expectedDuration=new SerializedObject(zone).FindProperty("durationSeconds").floatValue;
                foreach(string sample in new[]{"outside","outsideLength","widened","length","reverse","stationary","paused"})
                {
                    var go=new GameObject("Temporary turbo activation probe");
                    try
                    {
                        var body=go.AddComponent<Rigidbody>();body.useGravity=false;
                        go.AddComponent<JrsVehicleController>();
                        var turbo=go.AddComponent<RallyVehicleTurbo>();
                        typeof(RallyVehicleTurbo).GetMethod("Awake",flags).Invoke(turbo,null);
                        float x=sample=="outside"?2.7f:sample=="widened"?1.65f:0;
                        float z=sample=="outsideLength"?5.3f:sample=="length"?4.6f:0;
                        go.transform.SetPositionAndRotation(zone.transform.TransformPoint(new Vector3(x,1.5f,z)),zone.transform.rotation);
                        body.position=go.transform.position;body.rotation=go.transform.rotation;
                        body.linearVelocity=zone.transform.forward*(sample=="stationary"?0:sample=="reverse"?-20:20);
                        Physics.SyncTransforms();
                        Time.timeScale=sample=="paused"?0:1;
                        bool result=zone.TryActivate(body),expected=sample=="widened"||sample=="length";
                        if(result!=expected)throw new Exception(zone.name+" activation check failed: "+sample);
                        if(expected&&(!turbo.IsBoosting||Mathf.Abs(turbo.RemainingSeconds-expectedDuration)>.001f))throw new Exception("Boost duration changed");
                    }
                    finally{Time.timeScale=1;UnityEngine.Object.DestroyImmediate(go);}
                }
                File.AppendAllText(Report,"ACTIVATION "+zone.name+": PASS, production TryActivate accepts x=1.65m and z=4.6m; rejects x=2.7m/z=5.3m/reverse/stationary/pause (isolated Editor probes, not a driven lap)\n");
            }
        }
        finally{Time.timeScale=oldScale;}
    }
}
