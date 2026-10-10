using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

public static class RallyFourUXBuild
{
    const string Folder = "Assets/Art/RallyFinish";
    public static string Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || StaticOcclusionCulling.isRunning)
            throw new Exception("Leave Play/bake before editing scenes.");
        var setup = EditorSceneManager.GetSceneManagerSetup();
        if (Enumerable.Range(0,SceneManager.sceneCount).Any(i=>SceneManager.GetSceneAt(i).isDirty))
            throw new Exception("Save open scenes first; no user edits will be discarded.");
        string backup = "Logs/SceneBackups/FourUX_" + DateTime.Now.ToString("yyyyMMdd_HHmmss");
        Directory.CreateDirectory(backup);
        Directory.CreateDirectory(Folder); AssetDatabase.Refresh();
        var white=Material("Checker white",new Color(.9f,.9f,.85f));
        var black=Material("Checker black",new Color(.025f,.025f,.025f));
        var post=Material("Finish posts",new Color(.2f,.18f,.14f));
        File.WriteAllText("Logs/four-ux-build.txt","Backup: "+backup+"\n");
        try
        {
            foreach(string name in RallyGameSession.CircuitScenes)
            {
                string path="Assets/Scenes/"+name+".unity";
                File.Copy(path,backup+"/"+name+".unity");
                var scene=EditorSceneManager.OpenScene(path);
                string protectedBefore=Protected(scene);
                int removed=0;
                if(name=="Circuit_02")
                {
                    var pines=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true))
                        .Where(t=>t.name.StartsWith("Isolated mountain pine ",StringComparison.Ordinal)).ToArray();
                    foreach(var pine in pines){ Object.DestroyImmediate(pine.gameObject); removed++; }
                    if(removed!=12)throw new Exception("Expected exactly 12 C02 isolated pines; found "+removed);
                }
                var manager=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<RallyCheckpointManager>(true)).First();
                var finish=manager.OrderedCheckpoints.Last();
                if(finish==null)throw new Exception("No finish gate: "+name);
                if(scene.GetRootGameObjects().Any(g=>g.name=="Finish - Checkered Rally Arch"))
                    throw new Exception("Finish arch already exists; do not duplicate.");
                var gate=finish.GetComponent<BoxCollider>();
                float half=gate.size.x*.5f*finish.transform.lossyScale.x+1f;
                Vector3 center=finish.transform.position;
                var road=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<MeshCollider>(true))
                    .Where(c=>c.enabled&&!c.isTrigger&&c.GetComponentInParent<Rigidbody>()==null).ToArray();
                float ground=float.NegativeInfinity;
                foreach(var c in road)
                    if(c.Raycast(new Ray(center+Vector3.up*50,Vector3.down),out var hit,100))ground=Mathf.Max(ground,hit.point.y);
                if(float.IsNegativeInfinity(ground))throw new Exception("Finish is not over a drivable mesh: "+name);
                center.y=ground;
                var root=new GameObject("Finish - Checkered Rally Arch");
                root.transform.SetPositionAndRotation(center,Quaternion.Euler(0,finish.transform.eulerAngles.y,0));
                Cube(root.transform,"Left finish post",new Vector3(-half,3.5f,0),new Vector3(.4f,7f,.4f),post);
                Cube(root.transform,"Right finish post",new Vector3(half,3.5f,0),new Vector3(.4f,7f,.4f),post);
                Cube(root.transform,"Top support",new Vector3(0,6.9f,0),new Vector3(half*2,.18f,.3f),post);
                var mesh=new Mesh{name=name+" checkered banner"};
                var verts=new List<Vector3>(); var whites=new List<int>(); var blacks=new List<int>();
                const int columns=24,rows=2;
                for(int y=0;y<rows;y++)for(int x=0;x<columns;x++)
                {
                    float left=Mathf.Lerp(-half,half,(float)x/columns),right=Mathf.Lerp(-half,half,(float)(x+1)/columns);
                    float bottom=5.5f+y*.7f,top=bottom+.7f; int i=verts.Count;
                    verts.AddRange(new[]{new Vector3(left,bottom,-.18f),new Vector3(right,bottom,-.18f),new Vector3(right,top,-.18f),new Vector3(left,top,-.18f)});
                    ((x+y)%2==0?whites:blacks).AddRange(new[]{i,i+2,i+1,i,i+3,i+2});
                }
                mesh.SetVertices(verts);mesh.subMeshCount=2;mesh.SetTriangles(whites,0);mesh.SetTriangles(blacks,1);mesh.RecalculateNormals();mesh.RecalculateBounds();
                string meshPath=Folder+"/"+name+" banner.asset";
                if(AssetDatabase.LoadAssetAtPath<Mesh>(meshPath)!=null)throw new Exception("Banner asset exists; check previous run.");
                AssetDatabase.CreateAsset(mesh,meshPath);
                var banner=new GameObject("Checkered finish banner",typeof(MeshFilter),typeof(MeshRenderer));
                banner.transform.SetParent(root.transform,false);banner.GetComponent<MeshFilter>().sharedMesh=mesh;
                banner.GetComponent<MeshRenderer>().sharedMaterials=new[]{white,black};
                foreach(var r in root.GetComponentsInChildren<Renderer>()){r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=false;}
                // Visual-only and not an occluder: no gameplay collision or rebake required.
                if(root.GetComponentsInChildren<Collider>().Length!=0)throw new Exception("Finish must not block gameplay.");
                if(Protected(scene)!=protectedBefore)throw new Exception("Protected components changed: "+name);
                var player=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<JrsVehicleController>(true)).First(c=>c.name=="Porsche 911 SC Rally");
                if(!player.gameObject.activeInHierarchy)throw new Exception("Player inactive.");
                EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
                File.AppendAllText("Logs/four-ux-build.txt",name+": pines removed="+removed+", finish="+center+", width="+(half*2)+", clearance=5.5m; physics/waypoints/road/player preserved\n");
            }
            AssetDatabase.SaveAssets();File.AppendAllText("Logs/four-ux-build.txt","COMPLETE: PASS\n");
        }
        finally{EditorSceneManager.RestoreSceneManagerSetup(setup);}
        return "Saved all three scenes; backup: "+backup;
    }
    static string Protected(Scene s)=>string.Join("\n",s.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Component>(true))
        .Where(c=>c!=null&&(c is Collider||c is Rigidbody||c is MonoBehaviour||c is Terrain||c is Transform&&c.name.StartsWith("Waypoint_")))
        .Select(c=>c.GetEntityId()+" "+EditorJsonUtility.ToJson(c)).OrderBy(x=>x));
    static Material Material(string name,Color color)
    {
        string path=Folder+"/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(m!=null)return m;
        m=new Material(Shader.Find("Universal Render Pipeline/Lit")){name=name,enableInstancing=true};
        m.SetColor("_BaseColor",color);m.SetFloat("_Smoothness",.12f);m.SetFloat("_Cull",0);AssetDatabase.CreateAsset(m,path);return m;
    }
    static void Cube(Transform root,string name,Vector3 p,Vector3 size,Material material)
    {
        var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(root,false);
        go.transform.localPosition=p;go.transform.localScale=size;Object.DestroyImmediate(go.GetComponent<Collider>());
        go.GetComponent<Renderer>().sharedMaterial=material;
    }
}
