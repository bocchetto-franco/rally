using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine.SceneManagement;

public static class RallyIntermediateCheck
{
    public static void Refresh() => AssetDatabase.Refresh();
    public static void Before() => typeof(RallyRenderBenchmark).GetMethod("MeasureBefore").Invoke(null, null);
    public static void After() => typeof(RallyRenderBenchmark).GetMethod("MeasureAfter").Invoke(null, null);
    public static void ControlledBefore() => typeof(RallyRenderBenchmark).GetMethod("MeasureControlledBefore").Invoke(null, null);
    public static void ControlledAfter() => typeof(RallyRenderBenchmark).GetMethod("MeasureControlledAfter").Invoke(null, null);
    public static void OriginalTextureCaps() => TextureCaps(2048);
    public static void OptimizedTextureCaps() => TextureCaps(1024);
    static void TextureCaps(int cap) {
        if(EditorApplication.isPlayingOrWillChangePlaymode||StaticOcclusionCulling.isRunning)throw new Exception("Editor must be idle.");
        string root="Logs/SceneBackups/IntermediatePerformance_20261010_142747";
        int count=0;
        foreach(string meta in Directory.GetFiles(root,"*.meta",SearchOption.AllDirectories)) {
            string path=meta.Substring(root.Length+1).Replace('\\','/');path=path.Substring(0,path.Length-5);
            var importer=AssetImporter.GetAtPath(path) as TextureImporter; if(importer==null)continue;
            importer.maxTextureSize=cap;importer.SaveAndReimport();count++;
        }
        File.AppendAllText("Logs/intermediate-controlled-textures.txt",DateTime.Now+" "+count+" imports at "+cap+"\n");
    }
    public static object Units() {
        var handles=new System.Collections.Generic.List<Unity.Profiling.LowLevel.Unsafe.ProfilerRecorderHandle>();
        Unity.Profiling.LowLevel.Unsafe.ProfilerRecorderHandle.GetAvailable(handles);
        return handles.Select(Unity.Profiling.LowLevel.Unsafe.ProfilerRecorderHandle.GetDescription)
            .Where(d=>d.Name=="GC.Alloc"||d.Name=="GC Allocated In Frame"||d.Name=="PlayerLoop"||d.Name=="GPU Frame Time")
            .Select(d=>new{d.Name,unit=d.UnitType.ToString()}).ToArray();
    }
    public static void Resume() {
        if(!EditorApplication.isPlaying && SessionState.GetBool("Rally.RenderBenchmark.Advance",false))
            typeof(RallyRenderBenchmark).GetMethod("ModeChanged",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic).Invoke(null,new object[]{PlayModeStateChange.EnteredEditMode});
        if(!EditorApplication.isPlaying && SessionState.GetBool("Rally.RenderBenchmark.TransitionPending",false)) {
            var method=typeof(RallyRenderBenchmark).GetMethod("LoadCase",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic);
            // Some headless editor sessions do not pump delayCall after leaving Play.
            var callback=(EditorApplication.CallbackFunction)Delegate.CreateDelegate(typeof(EditorApplication.CallbackFunction),method);
            EditorApplication.delayCall-=callback;
            method.Invoke(null,null);
        }
    }
    public static object State() => new {
        playing=EditorApplication.isPlaying, compiling=EditorApplication.isCompiling,
        baking=StaticOcclusionCulling.isRunning,
        benchmark=SessionState.GetBool("Rally.RenderBenchmark.Running",false),
        index=SessionState.GetInt("Rally.RenderBenchmark.Case",-1),
        advance=SessionState.GetBool("Rally.RenderBenchmark.Advance",false),
        transition=SessionState.GetBool("Rally.RenderBenchmark.TransitionPending",false),
        elapsed=EditorApplication.timeSinceStartup-(double)typeof(RallyRenderBenchmark).GetField("began",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic).GetValue(null),
        frames=((System.Collections.ICollection)typeof(RallyRenderBenchmark).GetField("Frames",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic).GetValue(null)).Count,
        scenes=Enumerable.Range(0,SceneManager.sceneCount).Select(i=>new {SceneManager.GetSceneAt(i).path,SceneManager.GetSceneAt(i).isDirty}).ToArray()
    };
}
