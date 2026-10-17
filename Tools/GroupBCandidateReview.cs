using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Candidate inspection only. Never changes vehicle selection, tuning or scenes.
public static class GroupBCandidateReview
{
    const string Staging = "Assets/Editor/GroupBCandidateReview";
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new Exception("Exit Play before inspecting models.");
        if (Directory.Exists(Staging)) throw new Exception("Candidate staging already exists; inspect it before retrying.");
        Directory.CreateDirectory(Staging);
        string[] names = { "DeltaEric", "DeltaPack", "PeugeotPack" };
        string[] paths = {
            "Logs/GroupBCandidates/Delta/source/Lancia Delta S4.fbx",
            "Logs/GroupBCandidates/Pack/source/Expanded/Cars/Lancia S4.fbx",
            "Logs/GroupBCandidates/Pack/source/Expanded/Cars/Peugeot 205.fbx"
        };
        for (int i=0;i<names.Length;i++) File.Copy(paths[i], Staging+"/"+names[i]+".fbx");
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        var report = new StringBuilder("Unity candidate bounds review (metres; length / width / height).\nNo candidate accepted or installed.\n");
        var scene = EditorSceneManager.NewPreviewScene();
        try
        {
            Measure("Audi reference", "Assets/Resources/Vehicles/AudiQuattro.prefab", scene, report);
            for (int i=0;i<names.Length;i++) Measure(names[i],Staging+"/"+names[i]+".fbx",scene,report);
            File.WriteAllText("Logs/GroupBCandidates/unity-bounds.txt",report.ToString());
            Debug.Log(report.ToString());
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(scene);
            // The originals remain in Logs/GroupBCandidates and Downloads. Only our
            // temporary import is removed via the OS recycle bin, outside game Resources.
            if (!AssetDatabase.MoveAssetToTrash(Staging)) Debug.LogWarning("Candidate staging cleanup requires attention: "+Staging);
        }
    }

    static void Measure(string label,string path,UnityEngine.SceneManagement.Scene scene,StringBuilder report)
    {
        var instance=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path));
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(instance,scene);
        try
        {
            instance.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);
            var rs=instance.GetComponentsInChildren<MeshRenderer>(true);
            var b=rs[0].bounds;foreach(var r in rs)b.Encapsulate(r.bounds);
            var s=b.size;
            // Both imported FBX sets and the Audi have Y up. Accept either X or Z
            // as longitudinal axis; no nonuniform scaling is performed.
            float length=Mathf.Max(s.x,s.z),width=Mathf.Min(s.x,s.z),height=s.y;
            report.AppendLine($"{label}: raw {length:F5} / {width:F5} / {height:F5}; root scale {instance.transform.localScale}; renderers {rs.Length}");
            foreach(var r in rs) report.AppendLine("  mesh: "+r.name+" bounds "+r.bounds.size.ToString("F4"));
            var originalScale=instance.transform.localScale;
            foreach(float scale in new[]{4f/length,1.8f/width})
            {
                instance.transform.localScale=originalScale*scale;
                var scaled=rs[0].bounds;foreach(var r in rs)scaled.Encapsulate(r.bounds);
                report.AppendLine($"  Applied uniform scale {scale:F5}; measured renderer bounds: {Mathf.Max(scaled.size.x,scaled.size.z):F3} / {Mathf.Min(scaled.size.x,scaled.size.z):F3} / {scaled.size.y:F3}");
            }
        }
        finally { UnityEngine.Object.DestroyImmediate(instance); }
    }
}
