using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.ProBuilder;
using UnityEngine.Rendering;
using Object=UnityEngine.Object;

[InitializeOnLoad]
public static class Circuit01CrowdSetup
{
    const string Folder="Assets/Art/Environment/QuaterniusPeople";
    const string RootName="Large Rally Crowds - Outside Barriers";
    static Circuit01CrowdSetup(){EditorApplication.update+=Poll;}
    static void Poll()
    {
        const string request="Logs/large-crowd-request.txt";
        if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode||!File.Exists(request))return;
        File.Move(request,request+".consumed-"+DateTime.Now.Ticks);
        try{Build();}catch(Exception e){File.WriteAllText("Logs/large-crowd-error.txt",e.ToString());Debug.LogException(e);}
    }
    [MenuItem("Tools/Rally/Environment/Add Four Optimized Crowds")]
    public static void Build()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new Exception("Exit Play first.");
        var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();if(scene.isDirty)throw new Exception("Save the scene first.");
        scene=EditorSceneManager.OpenScene("Assets/Scenes/Circuit_01.unity");
        if(GameObject.Find(Circuit01LoopSetup.ShortMarker)==null)throw new Exception("Expected shortened circuit.");
        if(GameObject.Find(RootName)!=null)throw new Exception("Large crowds already installed.");
        Directory.CreateDirectory("Logs/SceneBackups");File.Copy(scene.path,"Logs/SceneBackups/BeforeLargeCrowds_"+DateTime.Now.Ticks+".unity");
        var protectedComponents=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Component>(true)).Where(c=>c!=null).ToArray();
        var before=protectedComponents.Select(c=>EditorJsonUtility.ToJson(c)).ToArray();
        var names=new[]{"Male_Standing_Waving","Female_Standing_CoveringEyes","Male_Standing","Female_Standing_Hips"};
        foreach(var name in names){var imp=(ModelImporter)AssetImporter.GetAtPath(Folder+"/"+name+".fbx");if(!imp.isReadable){imp.isReadable=true;imp.SaveAndReimport();}}
        var prefabs=names.Select(n=>AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"/"+n+".prefab")).ToArray();
        if(prefabs.Any(p=>p==null))throw new Exception("Existing people prefabs missing.");
        var shirts=new Color[]{new Color(.48f,.18f,.12f),new Color(.14f,.28f,.46f),new Color(.32f,.39f,.18f),new Color(.7f,.57f,.3f)}.Select((color,i)=>
        {string path=Folder+"/Crowd_Shirt_"+i+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);if(m==null){m=new Material(AssetDatabase.LoadAssetAtPath<Material>(Folder+"/Shirt_URP.mat"));m.SetColor("_BaseColor",color);m.enableInstancing=true;AssetDatabase.CreateAsset(m,path);}return m;}).ToArray();
        var terrain=Object.FindObjectsByType<Terrain>().Single();var walls=GameObject.Find("Soft Track Boundaries").GetComponentsInChildren<BoxCollider>();
        var road=GameObject.Find("Rally_Road_Start_to_Finish").GetComponent<ProBuilderMesh>();var v=road.positions.Select(p=>road.transform.TransformPoint(p)).ToArray();
        var root=new GameObject(RootName);var centers=new List<Vector3>();var records=new List<string>();var random=new System.Random(210926);
        var previousPeople=GameObject.Find("Rally Spectators - Outside Barriers").GetComponentsInChildren<Renderer>().Select(r=>r.bounds.center).ToArray();
        float minimum=999;int accepted=0;
        for(int i=8;i<v.Length/4-8&&centers.Count<4;i+=3)
        {
            var center=(v[i*4]+v[i*4+3])*.5f;var a=(v[(i-8)*4]+v[(i-8)*4+3])*.5f;var b=(v[(i+8)*4]+v[(i+8)*4+3])*.5f;
            if(Vector3.Angle(center-a,b-center)<15||centers.Any(p=>Vector3.Distance(p,center)<85))continue;
            foreach(var wall in walls.Where(w=>w.enabled&&!w.isTrigger).OrderBy(w=>Vector3.Distance(w.transform.position,center)).Take(10))
            {
                var right=wall.transform.right;right.y=0;right.Normalize();float side=Mathf.Sign(Vector3.Dot(wall.transform.position-center,right));var outward=right*side;
                var tangent=wall.transform.forward;tangent.y=0;tangent.Normalize();
                var group=new GameObject("Crowd Zone "+(centers.Count+1)+" - 20 spectators");group.transform.SetParent(root.transform);group.transform.position=wall.transform.position+outward*10;
                var people=new List<GameObject>();var placement=new List<string>();float localMin=999;bool valid=true;
                for(int k=0;k<20;k++)
                {
                    float along=(k%5-2)*1.9f+((float)random.NextDouble()-.5f)*1.0f;
                    float depth=7+(k/5)*1.9f+((float)random.NextDouble()-.5f)*1.0f;
                    var p=wall.transform.position+outward*depth+tangent*along;var n=p-terrain.transform.position;var d=terrain.terrainData;
                    if(n.x<0||n.z<0||n.x>=d.size.x||n.z>=d.size.z||d.GetSteepness(n.x/d.size.x,n.z/d.size.z)>18||previousPeople.Any(q=>Vector2.Distance(new Vector2(q.x,q.z),new Vector2(p.x,p.z))<1.8f)){valid=false;break;}
                    p.y=terrain.SampleHeight(p)+terrain.transform.position.y;
                    var go=(GameObject)PrefabUtility.InstantiatePrefab(prefabs[(k+centers.Count)%4],group.transform);people.Add(go);
                    var facing=center-p;facing.y=0;go.transform.SetPositionAndRotation(p,Quaternion.LookRotation(facing)*Quaternion.Euler(0,(float)random.NextDouble()*36-18,0));go.transform.localScale*=.94f+(float)random.NextDouble()*.12f;
                    var bounds=BoundsOf(go);go.transform.position+=Vector3.up*(p.y-bounds.min.y);bounds=BoundsOf(go);
                    float radius=new Vector2(bounds.extents.x,bounds.extents.z).magnitude;
                    if(Circuit01VegetationExpansion.Clearance(bounds.center,v)<radius+10){valid=false;break;}
                    for(int x=-1;x<=1;x+=2)for(int z=-1;z<=1;z+=2)
                    {var local=wall.transform.InverseTransformPoint(bounds.center+new Vector3(x*bounds.extents.x,0,z*bounds.extents.z))-wall.center;if(local.x*side<wall.size.x*.5f+3)valid=false;}
                    foreach(var other in walls)
                    {var q=other.transform.InverseTransformPoint(bounds.center)-other.center;float dx=Mathf.Max(0,Mathf.Abs(q.x)-other.size.x*.5f),dz=Mathf.Max(0,Mathf.Abs(q.z)-other.size.z*.5f);float gap=new Vector2(dx,dz).magnitude-radius;localMin=Mathf.Min(localMin,gap);if(gap<3)valid=false;}
                    if(!valid)break;
                    foreach(var renderer in go.GetComponentsInChildren<Renderer>())renderer.sharedMaterials=renderer.sharedMaterials.Select(m=>m.name.Contains("Shirt")?shirts[k%4]:m).ToArray();
                    placement.Add($"person {k:00}; pose={prefabs[(k+centers.Count)%4].name}; pos={go.transform.position:F3}; yaw={go.transform.eulerAngles.y:F2}");
                }
                if(!valid){Object.DestroyImmediate(group);continue;}
                centers.Add(center);minimum=Mathf.Min(minimum,localMin);accepted+=people.Count;
                records.Add($"ZONE {centers.Count}; wall={wall.name}; min body clearance={localMin:F2}m; center={group.transform.position:F3}");records.AddRange(placement);
                Combine(group,people,centers.Count);break;
            }
        }
        if(accepted!=80||centers.Count!=4){Object.DestroyImmediate(root);throw new Exception("Could not place four safe crowds; scene not saved.");}
        for(int i=0;i<protectedComponents.Length;i++)if(before[i]!=EditorJsonUtility.ToJson(protectedComponents[i]))throw new Exception("Existing scene components changed.");
        if(root.GetComponentsInChildren<Collider>().Length!=0||root.GetComponentsInChildren<Animator>().Length!=0)throw new Exception("Unexpected runtime crowd components.");
        AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        records.Add($"PASS: {accepted} added spectators in four groups; {root.GetComponentsInChildren<MeshRenderer>().Length} combined renderers; minimum full-body barrier clearance {minimum:F2}m. Existing scene preserved. {DateTime.Now:O}");
        File.WriteAllLines("Logs/large-crowd-validation.txt",records);File.WriteAllLines(Folder+"/LargeCrowdPlacements.txt",records);
        Preview(root.transform.GetChild(0));
    }
    static Bounds BoundsOf(GameObject go){var rs=go.GetComponentsInChildren<Renderer>();var b=rs[0].bounds;foreach(var r in rs)b.Encapsulate(r.bounds);return b;}
    static void Combine(GameObject group,List<GameObject> people,int zone)
    {
        var batches=new Dictionary<Material,List<CombineInstance>>();
        foreach(var p in people)foreach(var filter in p.GetComponentsInChildren<MeshFilter>())
        {var materials=filter.GetComponent<Renderer>().sharedMaterials;for(int s=0;s<filter.sharedMesh.subMeshCount;s++)
            {var mat=materials[s];if(!batches.ContainsKey(mat))batches[mat]=new List<CombineInstance>();batches[mat].Add(new CombineInstance{mesh=filter.sharedMesh,subMeshIndex=s,transform=group.transform.worldToLocalMatrix*filter.transform.localToWorldMatrix});}}
        foreach(var batch in batches)
        {
            var mesh=new Mesh{name="Crowd_"+zone+"_"+batch.Key.name,indexFormat=IndexFormat.UInt32};mesh.CombineMeshes(batch.Value.ToArray(),true,true);mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh,Folder+"/"+mesh.name+".asset");var go=new GameObject(batch.Key.name+" - combined");go.transform.SetParent(group.transform,false);go.AddComponent<MeshFilter>().sharedMesh=mesh;
            var r=go.AddComponent<MeshRenderer>();r.sharedMaterial=batch.Key;r.shadowCastingMode=ShadowCastingMode.Off;
        }
        foreach(var p in people)Object.DestroyImmediate(p);
        var lod=group.AddComponent<LODGroup>();lod.SetLODs(new[]{new LOD(.025f,group.GetComponentsInChildren<Renderer>())});lod.RecalculateBounds();
    }
    static void Preview(Transform group)
    {
        var b=BoundsOf(group.gameObject);var road=GameObject.Find("Rally_Road_Start_to_Finish").GetComponent<ProBuilderMesh>();var points=road.positions.Select(p=>road.transform.TransformPoint(p));var near=points.OrderBy(p=>Vector3.Distance(p,b.center)).First();
        var dir=near-b.center;dir.y=0;dir.Normalize();var go=new GameObject("Temporary crowd preview");var camera=go.AddComponent<Camera>();camera.farClipPlane=600;camera.transform.position=b.center+dir*19+Vector3.up*7;camera.transform.LookAt(b.center);
        var rt=new RenderTexture(1280,720,24);camera.targetTexture=rt;camera.Render();var previous=RenderTexture.active;RenderTexture.active=rt;var image=new Texture2D(1280,720,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1280,720),0,0);image.Apply();File.WriteAllBytes("Logs/large-crowd-preview.png",image.EncodeToPNG());RenderTexture.active=previous;camera.targetTexture=null;rt.Release();Object.DestroyImmediate(rt);Object.DestroyImmediate(image);Object.DestroyImmediate(go);
    }
}
