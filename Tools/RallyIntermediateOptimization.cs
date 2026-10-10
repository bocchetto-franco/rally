using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

// Explicit editor operation: never runs on import. No road/physics/height changes.
public static class RallyIntermediateOptimization
{
    const string Report = "Logs/intermediate-optimization.txt";
    static readonly string[] Scenes = { "Assets/Scenes/Circuit_01.unity", "Assets/Scenes/Circuit_02.unity", "Assets/Scenes/Circuit_03.unity" };
    static string backup;
    static SceneSetup[] previous;
    static int bakeIndex;
    static DateTime bakeStarted;
    static readonly HashSet<string> backedUp = new HashSet<string>();

    static void Idle()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || StaticOcclusionCulling.isRunning)
            throw new InvalidOperationException("Unity is not idle.");
        for (int i=0;i<SceneManager.sceneCount;i++)
            if(SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save open scenes first.");
    }
    static void Backup(string path)
    {
        if (!File.Exists(path) || !backedUp.Add(path)) return;
        string target=Path.Combine(backup,path);
        Directory.CreateDirectory(Path.GetDirectoryName(target));
        File.Copy(path,target,true);
    }
    static void Log(string text) => File.AppendAllText(Report,text+"\n");
    static IEnumerable<string> Names(Transform t) { for(;t!=null;t=t.parent) yield return t.name.ToLowerInvariant(); }
    static bool Vehicle(Transform t) => t.GetComponentInParent<Rigidbody>(true)!=null || t.GetComponentInParent<JrsVehicleController>(true)!=null;
    static bool Crowd(Transform t) => Names(t).Any(n=>n.Contains("crowd")||n.Contains("spectator"));
    static bool Plant(Transform t) => Names(t).Any(n=>n.Contains("vegetation")||n.Contains("tree")||n.Contains("bush")||n.Contains("grass")||n.Contains("scrub")||n.Contains("pine")||n.Contains("rooibos")||n.Contains("quiver")||n.Contains("fern")||n.Contains("searsia"));
    static bool Scenery(Transform t) => !Vehicle(t) && (Crowd(t)||Plant(t)||Names(t).Any(n=>n.Contains("rocks")||n.StartsWith("rock")||n.Contains("outcrop")));
    static bool AdditionalRock(Transform t) => !Scenery(t) && !Vehicle(t) && Names(t).Any(n=>n.Contains("granite")||n.Contains("boulder")||n.Contains("stones"));
    static bool RemainingDecoration(Transform t) => !Scenery(t) && !AdditionalRock(t) && !Vehicle(t) &&
        Names(t).Any(n=>n=="circuit 02 - sierra landscape detail"||n=="circuit 01 - desert rally dressing");
    static Component[] All(Scene s)=>s.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Component>(true)).Where(c=>c!=null).ToArray();
    static string Protected(Scene s) => string.Join("\n",All(s).Where(c=>c is Transform || c is Collider || c is Rigidbody ||
        (c is MonoBehaviour && !(c is Volume) && !(c is UniversalAdditionalCameraData)))
        .Select(c=>c.GetEntityId()+":"+EditorJsonUtility.ToJson(c)));

    public static object Audit()
    {
        Idle(); var setup=EditorSceneManager.GetSceneManagerSetup(); var rows=new List<object>();
        try { foreach(string path in Scenes) {
            var scene=EditorSceneManager.OpenScene(path); var all=All(scene);
            rows.Add(new { path, sceneryRenderers=all.OfType<MeshRenderer>().Count(r=>Scenery(r.transform)),
                lodGroups=all.OfType<LODGroup>().Count(), noLod=all.OfType<MeshRenderer>().Count(r=>r.enabled&&r.gameObject.activeInHierarchy&&Scenery(r.transform)&&r.GetComponentInParent<LODGroup>(true)==null),
                unclassifiedLod=all.OfType<LODGroup>().Where(g=>!Scenery(g.transform)).Select(g=>g.name).Distinct().Take(22).ToArray(),
                shadows=all.OfType<MeshRenderer>().Count(r=>Scenery(r.transform)&&r.shadowCastingMode!=ShadowCastingMode.Off),
                lights=all.OfType<Light>().Where(l=>l.enabled&&l.gameObject.activeInHierarchy).Select(l=>new{l.name,l.type,l.lightmapBakeType}).ToArray(),
                volumes=all.OfType<Volume>().Select(v=>new{v.name,v.enabled}).ToArray(),
                terrains=all.OfType<Terrain>().Select(t=>new{t.detailObjectDistance,t.detailObjectDensity,t.treeDistance,t.basemapDistance,t.heightmapPixelError,
                    details=t.terrainData.detailPrototypes.Select(p=>new{p.useInstancing,valid=p.Validate(out string error),error}).ToArray()}).ToArray() });
        }} finally { EditorSceneManager.RestoreSceneManagerSetup(setup); }
        return rows;
    }

    public static void Apply()
    {
        Idle(); backedUp.Clear(); backup="Logs/SceneBackups/IntermediatePerformance_"+DateTime.Now.ToString("yyyyMMdd_HHmmss");
        Directory.CreateDirectory(backup);
        if(File.Exists(Report)) File.Copy(Report,"Logs/intermediate-optimization-previous-pass.txt",true);
        File.WriteAllText(Report,"Intermediate optimization: "+DateTime.Now+"\nBackup: "+backup+"\n");
        previous=EditorSceneManager.GetSceneManagerSetup();
        try {
            OptimizeTextures();
            foreach(string path in Scenes) OptimizeScene(path);
            AssetDatabase.SaveAssets(); Log("APPLY COMPLETE: PASS. Occlusion rebake still required.");
        } catch(Exception e) { Log("FAILED: "+e); throw; }
        finally { EditorSceneManager.RestoreSceneManagerSetup(previous); }
    }
    static void OptimizeTextures()
    {
        // Resource-loaded selected cars aren't necessarily direct scene dependencies.
        var sources=Scenes.Concat(new[]{"Assets/Resources/Vehicles/BmwE30.prefab","Assets/Resources/Vehicles/AudiQuattro.prefab"});
        var dependencies=new HashSet<string>(AssetDatabase.GetDependencies(sources.ToArray(),true));
        int changed=0, audited=0;
        foreach(string path in dependencies.OrderBy(p=>p)) {
            var importer=AssetImporter.GetAtPath(path) as TextureImporter;
            if(importer==null || !path.StartsWith("Assets/")) continue;
            // Font atlases and original police-asset sources must remain untouched.
            if(path.Contains("MenuFonts/")||path.Contains("TextMesh Pro/")||path.StartsWith("Assets/JS Vehicle Physics Controller/")) continue;
            bool roadOrCar=path.StartsWith("Assets/Art/RoadSurfaces/")||path.StartsWith("Assets/Art/Vehicles/")||path.StartsWith("Assets/auto/");
            int cap=roadOrCar?2048:1024; audited++;
            bool update=importer.maxTextureSize>cap || importer.textureCompression==TextureImporterCompression.Uncompressed;
            foreach(string platform in new[]{"Standalone","Android","WebGL"}) {
                var ps=importer.GetPlatformTextureSettings(platform);
                if(ps.overridden && (ps.maxTextureSize>cap || ps.format==TextureImporterFormat.RGBA32 || ps.format==TextureImporterFormat.RGB24)) update=true;
            }
            if(!update) continue;
            Backup(path+".meta");
            importer.maxTextureSize=Math.Min(importer.maxTextureSize,cap);
            importer.textureCompression=TextureImporterCompression.Compressed;
            importer.compressionQuality=50;
            foreach(string platform in new[]{"Standalone","Android","WebGL"}) {
                var ps=importer.GetPlatformTextureSettings(platform); if(!ps.overridden) continue;
                ps.maxTextureSize=Math.Min(ps.maxTextureSize,cap); ps.textureCompression=TextureImporterCompression.Compressed;
                ps.format=TextureImporterFormat.Automatic; importer.SetPlatformTextureSettings(ps);
            }
            importer.SaveAndReimport(); changed++; Log("TEXTURE "+path+" max="+cap+" compressed");
        }
        Log("Texture importers audited="+audited+", changed="+changed+"; road/cars retained at <=2048; font atlases/police asset excluded.");
    }
    static void OptimizeScene(string path)
    {
        Backup(path); Backup(Path.ChangeExtension(path,null)+"/OcclusionCullingData.asset");
        Scene scene=EditorSceneManager.OpenScene(path); string signature=Protected(scene); var all=All(scene);
        int groups=0, added=0, shadowChanges=0, batching=0, instanced=0;
        foreach(LODGroup group in all.OfType<LODGroup>().Where(g=>Scenery(g.transform))) {
            LOD[] levels=group.GetLODs(); if(levels.Length==0)continue;
            for(int i=0;i<levels.Length;i++) levels[i].screenRelativeTransitionHeight=Mathf.Min(.85f,levels[i].screenRelativeTransitionHeight*1.25f);
            group.SetLODs(levels); group.fadeMode=LODFadeMode.None; EditorUtility.SetDirty(group); groups++;
        }
        foreach(MeshRenderer renderer in all.OfType<MeshRenderer>().Where(r=>Scenery(r.transform))) {
            if(renderer.enabled && renderer.GetComponentInParent<LODGroup>(true)==null) {
                var lod=renderer.gameObject.AddComponent<LODGroup>();
                if(lod==null) throw new Exception("Cannot add LOD: "+renderer.name);
                lod.SetLODs(new[]{new LOD(Crowd(renderer.transform)?.08f:Plant(renderer.transform)?.045f:.025f,new Renderer[]{renderer})});
                lod.RecalculateBounds(); added++;
            }
            if(renderer.bounds.size.y<3.5f && renderer.shadowCastingMode!=ShadowCastingMode.Off) { renderer.shadowCastingMode=ShadowCastingMode.Off; shadowChanges++; }
            var flags=GameObjectUtility.GetStaticEditorFlags(renderer.gameObject);
            flags |= StaticEditorFlags.OccludeeStatic;
            // Keep alpha vegetation out of occluder sets; do not memory-duplicate repeated plants.
            if(Plant(renderer.transform)||Crowd(renderer.transform)) flags &= ~StaticEditorFlags.OccluderStatic;
            if(Plant(renderer.transform)) flags &= ~StaticEditorFlags.BatchingStatic;
            else { flags |= StaticEditorFlags.BatchingStatic; batching++; }
            GameObjectUtility.SetStaticEditorFlags(renderer.gameObject,flags);
            foreach(Material m in renderer.sharedMaterials.Where(m=>m!=null).Distinct()) {
                if(!m.enableInstancing) { string mp=AssetDatabase.GetAssetPath(m); Backup(mp); m.enableInstancing=true; EditorUtility.SetDirty(m); instanced++; }
            }
        }
        foreach(Terrain terrain in all.OfType<Terrain>()) {
            terrain.detailObjectDistance=Mathf.Min(terrain.detailObjectDistance,50f);
            terrain.detailObjectDensity=Mathf.Min(terrain.detailObjectDensity,.55f);
            terrain.treeDistance=Mathf.Min(terrain.treeDistance,500f);
            terrain.treeBillboardDistance=Mathf.Min(terrain.treeBillboardDistance,45f);
            terrain.heightmapPixelError=Mathf.Max(terrain.heightmapPixelError,14f);
            terrain.basemapDistance=Mathf.Min(terrain.basemapDistance,100f); terrain.drawInstanced=true;
            foreach(var prototype in terrain.terrainData.detailPrototypes)
                if(!prototype.Validate(out string error)) Log("DETAIL existing invalid prototype: "+error);
            Log(scene.name+" TERRAIN: detail50 density.55 tree500 billboard45 pixelError="+terrain.heightmapPixelError+" basemap="+terrain.basemapDistance+"; instanced="+terrain.drawInstanced);
        }
        foreach(Volume volume in all.OfType<Volume>()) volume.enabled=false;
        foreach(Camera camera in all.OfType<Camera>()) { camera.useOcclusionCulling=true; camera.GetUniversalAdditionalCameraData().renderPostProcessing=false; }
        if(Protected(scene)!=signature) throw new InvalidOperationException("Protected transforms/physics/gameplay changed: "+path);
        EditorSceneManager.MarkSceneDirty(scene); if(!EditorSceneManager.SaveScene(scene))throw new Exception("Scene save failed.");
        Log(scene.name+": protected signature PASS, LOD earlier="+groups+", added="+added+", small shadows off="+shadowChanges+", batching flagged="+batching+", newly instancing-enabled materials="+instanced);
    }
    public static void Bake()
    {
        Idle(); previous=EditorSceneManager.GetSceneManagerSetup(); bakeIndex=0;
        EditorApplication.update-=Tick; EditorApplication.update+=Tick;
        try { BeginBake(); } catch { EditorApplication.update-=Tick; EditorSceneManager.RestoreSceneManagerSetup(previous); throw; }
    }
    public static void CompleteRockLODs()
        => CompleteExtraLODs(false);
    public static void CompleteRemainingDecoration()
        => CompleteExtraLODs(true);
    static void CompleteExtraLODs(bool remaining)
    {
        Idle(); var setup=EditorSceneManager.GetSceneManagerSetup();
        Func<Transform,bool> selected=remaining?(Func<Transform,bool>)RemainingDecoration:AdditionalRock;
        try { foreach(string path in Scenes) {
            Scene scene=EditorSceneManager.OpenScene(path); var all=All(scene); string signature=Protected(scene); int count=0, batching=0;
            foreach(LODGroup group in all.OfType<LODGroup>().Where(g=>selected(g.transform))) {
                var levels=group.GetLODs();
                for(int i=0;i<levels.Length;i++)levels[i].screenRelativeTransitionHeight=Mathf.Min(.85f,levels[i].screenRelativeTransitionHeight*1.25f);
                group.SetLODs(levels); group.fadeMode=LODFadeMode.None; count++;
            }
            foreach(MeshRenderer renderer in all.OfType<MeshRenderer>().Where(r=>selected(r.transform))) {
                var flags=GameObjectUtility.GetStaticEditorFlags(renderer.gameObject);
                flags |= StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic;
                GameObjectUtility.SetStaticEditorFlags(renderer.gameObject,flags); batching++;
                if(renderer.bounds.size.y<3.5f) renderer.shadowCastingMode=ShadowCastingMode.Off;
            }
            if(Protected(scene)!=signature)throw new Exception("Protected scene changed in rock pass.");
            if(count>0||batching>0) {EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);}
            Log(scene.name+(remaining?" remaining private dressing":" additional named granite/boulder")+" LOD="+count+", batching="+batching+"; protected PASS");
        }}finally{EditorSceneManager.RestoreSceneManagerSetup(setup);}
    }
    static void BeginBake()
    {
        EditorSceneManager.OpenScene(Scenes[bakeIndex]); bakeStarted=DateTime.UtcNow;
        Log("BAKE START "+Scenes[bakeIndex]);
        if(!StaticOcclusionCulling.GenerateInBackground())throw new Exception("Failed to start occlusion.");
    }
    static void Tick()
    {
        if(StaticOcclusionCulling.isRunning)return;
        try {
            if(SceneManager.GetActiveScene().path!=Scenes[bakeIndex])throw new Exception("Scene switched during bake.");
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            var output=new FileInfo(Path.ChangeExtension(Scenes[bakeIndex],null)+"/OcclusionCullingData.asset");
            if(!output.Exists||output.Length==0||output.LastWriteTimeUtc<bakeStarted)throw new Exception("Bake output not regenerated.");
            Log("BAKE OK "+Scenes[bakeIndex]+" bytes="+output.Length);
            if(++bakeIndex<Scenes.Length) { BeginBake(); return; }
            Log("BAKES COMPLETE: PASS");
        }catch(Exception e){ Log("BAKE FAILED: "+e); }
        EditorApplication.update-=Tick; EditorSceneManager.RestoreSceneManagerSetup(previous);
    }
}
