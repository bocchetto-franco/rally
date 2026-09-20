using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.ProBuilder;
using Object=UnityEngine.Object;

[InitializeOnLoad]
public static class Circuit01SpectatorsSetup
{
    const string Folder="Assets/Art/Environment/QuaterniusPeople";
    static Circuit01SpectatorsSetup(){EditorApplication.update+=Poll;}
    static void Poll()
    {
        const string request="Logs/spectators-request.txt";
        if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode||!File.Exists(request))return;
        File.Move(request,request+".consumed-"+DateTime.Now.Ticks);
        try{Build();}catch(Exception e){File.WriteAllText("Logs/spectators-error.txt",e.ToString());Debug.LogException(e);}
    }
    [MenuItem("Tools/Rally/Environment/Add Safe Spectator Groups")]
    public static void Build()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new Exception("Exit Play first.");
        var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if(scene.isDirty)throw new Exception("Save the current scene first.");
        scene=EditorSceneManager.OpenScene("Assets/Scenes/Circuit_01.unity");
        if(GameObject.Find("Rally Spectators - Outside Barriers")!=null)throw new Exception("Spectators already installed.");
        var protectedComponents=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Component>(true)).Where(c=>c!=null).ToArray();
        var before=protectedComponents.Select(c=>EditorJsonUtility.ToJson(c)).ToArray();
        Directory.CreateDirectory("Logs/SceneBackups");File.Copy(scene.path,"Logs/SceneBackups/Circuit01_before_spectators_"+DateTime.Now.Ticks+".unity");
        var terrain=Object.FindObjectsByType<Terrain>().Single();
        var walls=GameObject.Find("Soft Track Boundaries").GetComponentsInChildren<BoxCollider>().Where(c=>c.enabled&&!c.isTrigger).ToArray();
        var road=GameObject.Find("Rally_Road_Start_to_Finish").GetComponent<ProBuilderMesh>();
        var v=road.positions.Select(p=>road.transform.TransformPoint(p)).ToArray();
        var prefabs=new[]{"Male_Standing_Waving","Female_Standing_CoveringEyes","Male_Standing","Female_Standing_Hips"}.Select(CreatePrefab).ToArray();
        var root=new GameObject("Rally Spectators - Outside Barriers");
        var centers=new List<Vector3>(); var report=new List<string>(); int count=v.Length/4;
        // Distributed candidates, rejecting straight sections and crowded or steep spots.
        for(int i=6;i<count-6 && centers.Count<4;i+=3)
        {
            Vector3 center=(v[i*4]+v[i*4+3])*.5f;
            var a=(v[(i-5)*4]+v[(i-5)*4+3])*.5f;var b=(v[(i+5)*4]+v[(i+5)*4+3])*.5f;
            if(Vector3.Angle(center-a,b-center)<12||centers.Any(p=>Vector3.Distance(p,center)<120))continue;
            foreach(var wall in walls.OrderBy(w=>Vector3.Distance(w.transform.position,center)).Take(8))
            {
                var right=wall.transform.right;right.y=0;right.Normalize();
                float side=Mathf.Sign(Vector3.Dot(wall.transform.position-center,right));
                var outward=right*side;var tangent=wall.transform.forward;tangent.y=0;tangent.Normalize();
                var positions=Enumerable.Range(0,3).Select(k=>wall.transform.position+outward*7+tangent*((k-1)*2.2f)).ToArray();
                if(positions.Any(p=>!SafeGround(p,terrain,v)))continue;
                var group=new GameObject("Spectator Group "+(centers.Count+1));group.transform.SetParent(root.transform);
                var people=new List<GameObject>(); bool valid=true;float minimum=999;
                for(int k=0;k<3;k++)
                {
                    var go=(GameObject)PrefabUtility.InstantiatePrefab(prefabs[(centers.Count+k)%4],group.transform);people.Add(go);
                    var p=positions[k];p.y=terrain.SampleHeight(p)+terrain.transform.position.y;
                    var facing=center-p;facing.y=0;go.transform.SetPositionAndRotation(p,Quaternion.LookRotation(facing));
                    var bounds=BoundsOf(go);go.transform.position+=Vector3.up*(p.y-bounds.min.y);bounds=BoundsOf(go);
                    float radius=new Vector2(bounds.extents.x,bounds.extents.z).magnitude;
                    if(Circuit01VegetationExpansion.Clearance(bounds.center,v)<radius+9)valid=false;
                    // Check the entire rendered body against the outer face of the actual wall.
                    for(int x=-1;x<=1;x+=2)for(int z=-1;z<=1;z+=2)
                    {
                        var corner=bounds.center+new Vector3(x*bounds.extents.x,0,z*bounds.extents.z);
                        var local=wall.transform.InverseTransformPoint(corner)-wall.center;
                        if(local.x*side<wall.size.x*.5f+3)valid=false;
                    }
                    foreach(var other in walls)
                    {
                        var local=other.transform.InverseTransformPoint(bounds.center)-other.center;
                        float dx=Mathf.Max(0,Mathf.Abs(local.x)-other.size.x*.5f),dz=Mathf.Max(0,Mathf.Abs(local.z)-other.size.z*.5f);
                        float clearance=new Vector2(dx,dz).magnitude-radius;minimum=Mathf.Min(minimum,clearance);
                        if(clearance<3)valid=false;
                    }
                }
                if(!valid){Object.DestroyImmediate(group);continue;}
                centers.Add(center);report.Add(group.name+"; wall="+wall.name+"; people=3; minimum full-body barrier clearance="+minimum.ToString("F2")+"m; positions="+string.Join(" / ",people.Select(p=>p.transform.position.ToString("F2"))));
                break;
            }
        }
        if(centers.Count!=4){Object.DestroyImmediate(root);throw new Exception("Could not find four safe curve locations; scene not saved.");}
        for(int i=0;i<protectedComponents.Length;i++)if(before[i]!=EditorJsonUtility.ToJson(protectedComponents[i]))throw new Exception("Existing scene component changed; refusing save.");
        AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        File.WriteAllLines("Logs/spectators-validation.txt",report.Concat(new[]{"12 people / 4 groups. Existing components unchanged. "+DateTime.Now.ToString("O")}));
        Preview(root);
    }
    static bool SafeGround(Vector3 p,Terrain t,Vector3[] road)
    {
        var n=p-t.transform.position;var d=t.terrainData;
        return n.x>0&&n.z>0&&n.x<d.size.x&&n.z<d.size.z&&d.GetSteepness(n.x/d.size.x,n.z/d.size.z)<12&&Circuit01VegetationExpansion.Clearance(p,road)>11;
    }
    static Bounds BoundsOf(GameObject go){var rs=go.GetComponentsInChildren<Renderer>();var b=rs[0].bounds;foreach(var r in rs)b.Encapsulate(r.bounds);return b;}
    static GameObject CreatePrefab(string name)
    {
        string path=Folder+"/"+name+".fbx";AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
        var importer=(ModelImporter)AssetImporter.GetAtPath(path);importer.importAnimation=false;importer.importCameras=false;importer.importLights=false;importer.SaveAndReimport();
        var root=new GameObject(name+" Spectator");var model=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path),root.transform);
        foreach(var renderer in model.GetComponentsInChildren<Renderer>())renderer.sharedMaterials=renderer.sharedMaterials.Select(original=>
        {
            string materialPath=Folder+"/"+original.name.Replace('/','_')+"_URP.mat";
            var mat=AssetDatabase.LoadAssetAtPath<Material>(materialPath);if(mat!=null)return mat;
            mat=new Material(Shader.Find("Universal Render Pipeline/Lit"));mat.SetColor("_BaseColor",original.HasProperty("_Color")?original.color:Color.gray);mat.SetFloat("_Smoothness",.1f);mat.enableInstancing=true;AssetDatabase.CreateAsset(mat,materialPath);return mat;
        }).ToArray();
        var bounds=BoundsOf(root);root.transform.localScale=Vector3.one*(1.75f/bounds.size.y);
        bounds=BoundsOf(root);model.transform.position-=new Vector3(bounds.center.x,bounds.min.y,bounds.center.z);
        var prefab=PrefabUtility.SaveAsPrefabAsset(root,Folder+"/"+name+".prefab");Object.DestroyImmediate(root);return prefab;
    }
    static void Preview(GameObject root)
    {
        var group=root.transform.GetChild(0);var person=group.GetChild(0);var p=person.position;
        var go=new GameObject("Temporary spectator preview");var camera=go.AddComponent<Camera>();camera.farClipPlane=600;
        camera.transform.position=p+person.forward*8+Vector3.up*3;camera.transform.LookAt(p+Vector3.up);
        var rt=new RenderTexture(1280,720,24);camera.targetTexture=rt;camera.Render();var previous=RenderTexture.active;RenderTexture.active=rt;
        var image=new Texture2D(1280,720,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1280,720),0,0);image.Apply();File.WriteAllBytes("Logs/spectators-preview.png",image.EncodeToPNG());RenderTexture.active=previous;
        camera.targetTexture=null;rt.Release();Object.DestroyImmediate(rt);Object.DestroyImmediate(image);Object.DestroyImmediate(go);
    }
}
