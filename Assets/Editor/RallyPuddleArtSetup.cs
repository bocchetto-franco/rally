using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>Rebuildable, visual-only dressing over existing puddles. Physical basins are untouched.</summary>
[InitializeOnLoad]
public static class RallyPuddleArtSetup
{
    const string Folder = "Assets/Art/PuddleDressing";
    const string Request = "Temp/rally-puddle-art.request";
    const string RootName = "Rally puddle dressing";
    const int Sides = 64;
    static double idle;
    static RallyPuddleArtSetup() => EditorApplication.update += Poll;

    static void Poll()
    {
        if (!File.Exists(Request) || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
        { idle = 0; return; }
        if (idle == 0) { idle = EditorApplication.timeSinceStartup; return; }
        if (EditorApplication.timeSinceStartup - idle < 3) return;
        File.Move(Request, Request + ".consumed-" + DateTime.UtcNow.Ticks);
        try { Build(); }
        catch (Exception e) { File.AppendAllText("Logs/puddle-art.txt", "\nFAILED: " + e); Debug.LogException(e); }
    }

    [MenuItem("Tools/Rally/Improve Existing Puddle Visuals")]
    public static void Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play first.");
        for (int i=0; i<SceneManager.sceneCount; i++)
            if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Open scene has unsaved changes; no scene overwritten.");
        Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/puddle-art.txt", "Puddle visual dressing\n");
        string backup = "Logs/SceneBackups/PuddleArt_" + DateTime.Now.ToString("yyyyMMdd_HHmmss");
        Directory.CreateDirectory(backup); Directory.CreateDirectory(Folder);
        AssetDatabase.Refresh();
        var setup = EditorSceneManager.GetSceneManagerSetup();
        try
        {
            Material water = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Environment/Materials/Puddles - Unity sample.mat");
            if (water == null) throw new InvalidOperationException("Existing water material missing.");
            Material mud = MakeMudMaterial("Mud", false);
            Material forestMud = MakeMudMaterial("Forest Mud", true);
            Material silt = MakeSiltMaterial();
            Material spray = MakeSprayMaterial();
            foreach (string name in new[]{"Circuit_01", "Circuit_02", "Circuit_03"})
            {
                string path = "Assets/Scenes/" + name + ".unity";
                File.Copy(path, backup + "/" + name + ".unity");
                Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                Physics.SyncTransforms();
                string before = ProtectedState(scene);
                var puddles = scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<MeshRenderer>(true))
                    .Where(r=>r.name.StartsWith("Puddle_") && r.GetComponent<MeshFilter>() != null).OrderBy(r=>r.name).ToArray();
                int expected = name == "Circuit_01" ? 1 : 4;
                if (puddles.Length != expected) throw new InvalidOperationException(name + ": unexpected puddle count " + puddles.Length);
                int index = 0;
                foreach (var original in puddles)
                {
                    index++;
                    Transform t = original.transform;
                    Bounds bounds = original.GetComponent<MeshFilter>().sharedMesh.bounds;
                    if(index == 1) Capture(t.TransformPoint(bounds.center), "Logs/" + name + "_Puddle_Before.png");
                    Transform old = t.Find(RootName);
                    if (old != null) Object.DestroyImmediate(old.gameObject);
                    var root = new GameObject(RootName); root.transform.SetParent(t, false);
                    // Keep the parent's complete transform, source geometry, and all colliders intact.
                    var vertices = new List<Vector3>(); var triangles = new List<int>(); var uv = new List<Vector2>(); var colors = new List<Color>();
                    float phase = index * 1.79f;
                    for (int ring=0; ring<=12; ring++)
                    for (int i=0; i<Sides; i++)
                    {
                        float a = i * Mathf.PI * 2 / Sides, r = ring/12f;
                        Vector3 p = Outline(bounds, a, phase) * r + bounds.center;
                        p.y = bounds.center.y;
                        vertices.Add(p); uv.Add(new Vector2(p.x*.3f,p.z*.3f)); colors.Add(Color.white);
                    }
                    ConnectRings(triangles, 12);
                    Mesh waterMesh = MeshAsset(name + "_" + index + "_Water", vertices, triangles, uv, colors);
                    AddMesh(root.transform, "Irregular water surface", waterMesh, water);

                    vertices.Clear(); triangles.Clear(); uv.Clear(); colors.Clear();
                    // Wet textured bank: original road raycasts supply exact shoulder height.
                    for(int ring=0;ring<=6;ring++)
                    for(int i=0;i<Sides;i++)
                    {
                        float a=i*Mathf.PI*2/Sides, s=ring/6f;
                        Vector3 edge=Outline(bounds,a,phase);
                        Vector3 p=bounds.center+edge*Mathf.Lerp(.84f,1.24f,s);
                        Vector3 world=t.TransformPoint(p);
                        float ground=RoadHeight(world,t);
                        float waterY=t.TransformPoint(bounds.center).y;
                        world.y=Mathf.Lerp(waterY+.008f,Mathf.Max(ground+.022f,waterY+.015f),Mathf.SmoothStep(0,1,s));
                        p=t.InverseTransformPoint(world);
                        vertices.Add(p); uv.Add(new Vector2(world.x*.5f,world.z*.5f));
                        float shade=Mathf.Lerp(.38f,.82f,s);
                        float alpha=Mathf.SmoothStep(0,1,Mathf.InverseLerp(0,.3f,s))*(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.5f,1,s)))*.85f;
                        colors.Add(new Color(shade,shade*.87f,shade*.69f,alpha));
                    }
                    ConnectRings(triangles,6);
                    AddMesh(root.transform,"Wet irregular mud bank",MeshAsset(name+"_"+index+"_Bank",vertices,triangles,uv,colors),name=="Circuit_03"?forestMud:mud);

                    vertices.Clear(); triangles.Clear(); uv.Clear(); colors.Clear();
                    // Smooth vertex tint reinforces depth even on the original very shallow first puddle.
                    // This transparent silt film keeps the official animated water/reflections underneath.
                    for(int ring=0;ring<=12;ring++)
                    for(int i=0;i<Sides;i++)
                    {
                        float a=i*Mathf.PI*2/Sides,r=ring/12f;
                        Vector3 p=bounds.center+Outline(bounds,a,phase)*r;
                        Vector3 world=t.TransformPoint(p); world.y+=.004f; p=t.InverseTransformPoint(world);
                        vertices.Add(p);uv.Add(new Vector2(world.x*.3f,world.z*.3f));
                        float edge=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.35f,1,r));
                        Color tint=Color.Lerp(new Color(.035f,.045f,.038f,.82f),new Color(.38f,.30f,.18f,.3f),edge);
                        colors.Add(tint);
                    }
                    ConnectRings(triangles,12);
                    AddMesh(root.transform,"Dark center and shallow silty edges",MeshAsset(name+"_"+index+"_Silt",vertices,triangles,uv,colors),silt);
                    original.enabled=false;
                    var visual=t.GetComponent<RallyPuddleMudVisual>();
                    if(visual==null)visual=t.gameObject.AddComponent<RallyPuddleMudVisual>();
                    visual.localCenter=bounds.center;visual.halfSize=new Vector2(bounds.extents.x,bounds.extents.z);visual.sprayMaterial=spray;
                    if(root.GetComponentsInChildren<Collider>().Length!=0)throw new Exception("Visual dressing has collision.");
                    File.AppendAllText("Logs/puddle-art.txt",name+" / "+t.name+": original transform/trigger preserved; 3 low-poly visual surfaces.\n");
                    if(index==1)Capture(t.TransformPoint(bounds.center),"Logs/"+name+"_Puddle_After.png");
                }
                if(before!=ProtectedState(scene))throw new InvalidOperationException(name+": protected physics/gameplay state changed. Scene not saved.");
                EditorSceneManager.SaveScene(scene);
                File.AppendAllText("Logs/puddle-art.txt",name+": PASS positions, colliders, physics and gameplay signature unchanged.\n");
            }
            AssetDatabase.SaveAssets();
            File.AppendAllText("Logs/puddle-art.txt","COMPLETE: PASS\nBackup: "+backup);
        }
        finally { EditorSceneManager.RestoreSceneManagerSetup(setup); }
    }

    static Vector3 Outline(Bounds b,float a,float phase)
    {
        float r=.84f+.065f*Mathf.Sin(a*3+phase)+.045f*Mathf.Cos(a*5-phase)+.035f*Mathf.Sin(a*9+phase);
        return new Vector3(Mathf.Cos(a)*b.extents.x*r,0,Mathf.Sin(a)*b.extents.z*r);
    }

    static float RoadHeight(Vector3 p,Transform puddle)
    {
        var hits=Physics.RaycastAll(p+Vector3.up*3,Vector3.down,8,~0,QueryTriggerInteraction.Ignore);
        foreach(var hit in hits.OrderBy(h=>h.distance))
            if(!hit.transform.IsChildOf(puddle)&&hit.collider.attachedRigidbody==null&&hit.normal.y>.45f)return hit.point.y;
        return p.y;
    }

    static void ConnectRings(List<int> tri,int rings)
    {
        for(int r=0;r<rings;r++)for(int i=0;i<Sides;i++)
        {
            int a=r*Sides+i,b=r*Sides+(i+1)%Sides,c=(r+1)*Sides+i,d=(r+1)*Sides+(i+1)%Sides;
            tri.Add(a);tri.Add(b);tri.Add(d);tri.Add(a);tri.Add(d);tri.Add(c);
        }
    }

    static Mesh MeshAsset(string name,List<Vector3> v,List<int> t,List<Vector2> uv,List<Color> c)
    {
        string path=Folder+"/"+name+".asset";
        var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if(mesh==null){mesh=new Mesh{name=name};AssetDatabase.CreateAsset(mesh,path);}else mesh.Clear();
        mesh.SetVertices(v);mesh.SetTriangles(t,0);mesh.SetUVs(0,uv);mesh.SetColors(c);mesh.RecalculateNormals();mesh.RecalculateTangents();mesh.RecalculateBounds();EditorUtility.SetDirty(mesh);return mesh;
    }

    static void AddMesh(Transform parent,string name,Mesh mesh,Material material)
    {
        var go=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(parent,false);
        go.GetComponent<MeshFilter>().sharedMesh=mesh;
        var r=go.GetComponent<MeshRenderer>();r.sharedMaterial=material;r.shadowCastingMode=ShadowCastingMode.Off;
        r.receiveShadows=false;r.lightProbeUsage=LightProbeUsage.Off;r.reflectionProbeUsage=ReflectionProbeUsage.BlendProbes;
        // Not occluders: the old baked occlusion remains valid for the physical circuit.
        GameObjectUtility.SetStaticEditorFlags(go,0);
    }

    static Material MakeMudMaterial(string name,bool forest)
    {
        var m=GetMaterial(name,"Universal Render Pipeline/Particles/Lit");
        string source="Assets/Art/RoadSurfaces/"+(forest?"muddy_tracks":"rocky_trail_02")+"/";
        m.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture>(source+"diff.jpg"));
        m.SetTexture("_BumpMap",AssetDatabase.LoadAssetAtPath<Texture>(source+"nor_gl.jpg"));
        m.SetFloat("_BumpScale",.45f);m.SetFloat("_Smoothness",.52f);m.SetFloat("_Metallic",0);
        m.EnableKeyword("_NORMALMAP");Transparent(m);m.renderQueue=3005;EditorUtility.SetDirty(m);return m;
    }

    static Material MakeSprayMaterial()
    {
        var m=GetMaterial("Mud droplets","Universal Render Pipeline/Particles/Unlit");
        var defaults=GraphicsSettings.currentRenderPipeline.defaultParticleMaterial;
        if(defaults!=null)m.SetTexture("_BaseMap",defaults.mainTexture);
        Transparent(m);m.renderQueue=3010;EditorUtility.SetDirty(m);return m;
    }

    static Material MakeSiltMaterial()
    {
        var m=GetMaterial("Reflective muddy surface","Universal Render Pipeline/Particles/Lit");
        m.SetFloat("_Smoothness",.9f);m.SetFloat("_Metallic",.05f);m.SetFloat("_BumpScale",.22f);
        string normal=AssetDatabase.GUIDToAssetPath("eee3279cf0eea85468d825bd69ca206d");
        m.SetTexture("_BumpMap",AssetDatabase.LoadAssetAtPath<Texture>(normal));m.EnableKeyword("_NORMALMAP");
        Transparent(m);m.renderQueue=3004;EditorUtility.SetDirty(m);return m;
    }

    static Material GetMaterial(string name,string shader)
    {
        string path=Folder+"/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(m==null){m=new Material(Shader.Find(shader)){name=name};AssetDatabase.CreateAsset(m,path);}return m;
    }

    static void Transparent(Material m)
    {
        m.SetColor("_BaseColor",Color.white);m.SetFloat("_Surface",1);m.SetFloat("_Blend",0);
        m.SetFloat("_SrcBlend",5);m.SetFloat("_DstBlend",10);m.SetFloat("_ZWrite",0);m.SetFloat("_Cull",0);
        m.SetOverrideTag("RenderType","Transparent");m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
    }

    static string ProtectedState(Scene scene)=>string.Join("\n",scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Component>(true))
        .Where(c=>c is Collider||c is Rigidbody||c is RallyPuddleSlowZone||c is JrsVehicleController||c is RallyVehicleDynamics||c is RallyCheckpointManager||c is RallyCheckpointTrigger)
        .Select(c=>c.GetEntityId()+":"+EditorJsonUtility.ToJson(c)+":"+EditorJsonUtility.ToJson(c.transform)));

    static void Capture(Vector3 focus,string path)
    {
        var go=new GameObject("Temporary puddle art camera",typeof(Camera));var cam=go.GetComponent<Camera>();
        cam.transform.SetPositionAndRotation(focus+new Vector3(7,8,-9),Quaternion.LookRotation(new Vector3(-7,-8,9)));
        cam.fieldOfView=48;cam.nearClipPlane=.1f;cam.farClipPlane=200;cam.clearFlags=CameraClearFlags.Skybox;
        var data=cam.GetUniversalAdditionalCameraData();data.requiresDepthTexture=true;data.requiresColorTexture=true;
        var target=new RenderTexture(1280,800,24);var texture=new Texture2D(1280,800,TextureFormat.RGB24,false);
        var old=RenderTexture.active;
        try{cam.targetTexture=target;cam.Render();RenderTexture.active=target;texture.ReadPixels(new Rect(0,0,1280,800),0,0);texture.Apply();File.WriteAllBytes(path,texture.EncodeToPNG());}
        finally{cam.targetTexture=null;RenderTexture.active=old;Object.DestroyImmediate(texture);target.Release();Object.DestroyImmediate(target);Object.DestroyImmediate(go);}
    }
}
