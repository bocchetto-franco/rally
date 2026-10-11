using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

/// <summary>CREDITS.md is canonical. Sync runtime/distributed text and retain legal notices, never edit scenes.</summary>
[InitializeOnLoad]
public sealed class RallyCreditsSetup : IPreprocessBuildWithReport
{
    const string RuntimeCopy = "Assets/Resources/Credits/Credits.txt";
    const string DistributedCopy = "Assets/StreamingAssets/CREDITS.md";
    static bool synced;
    public int callbackOrder => -1000;
    static RallyCreditsSetup() => EditorApplication.update += InitialSync;
    static void InitialSync()
    {
        if (synced || EditorApplication.isCompiling || EditorApplication.isUpdating ||
            EditorApplication.isPlayingOrWillChangePlaymode || BuildPipeline.isBuildingPlayer) return;
        synced = true;
        try { Sync(); }
        catch (Exception error) { Debug.LogError("Credits sync failed: " + error.Message); }
    }
    [MenuItem("Tools/Rally/Sync Credits and License Notices")]
    public static void Sync()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || BuildPipeline.isBuildingPlayer)
            throw new InvalidOperationException("Sync credits outside Play and builds.");
        byte[] source = File.ReadAllBytes("CREDITS.md");
        CopyBytes(source, RuntimeCopy);
        CopyBytes(source, DistributedCopy);
        Copy("Docs/Licenses/JRS-MIT.txt", "JRS-MIT.txt");
        Copy("Docs/Licenses/Apache-2.0.txt", "Apache-2.0.txt");
        Copy("Assets/Art/Environment/UnityWaterSample/LICENSE.md", "UnityWaterSample.txt");
        Copy("Assets/Art/Forest/KenneyNature/License.txt", "KenneyNature.txt");
        Copy("Assets/Art/Environment/Circuit01Dressing/KenneyRacing/License.txt", "KenneyRacing.txt");
        Copy("Assets/Art/Environment/QuaterniusPeople/License.txt", "QuaterniusPeople.txt");
        Copy("Assets/TextMesh Pro/Fonts/LiberationSans - OFL.txt", "LiberationSans-OFL.txt");
        foreach (string name in new[] { "Oswald-Bold - OFL", "Roboto-Bold - License", "Anton OFL", "Bangers - OFL", "Unity - OFL" })
            Copy("Assets/TextMesh Pro/Examples & Extras/Fonts/" + name + ".txt", name + ".txt");
        foreach (PackageInfo package in PackageInfo.GetAllRegisteredPackages())
        {
            if (!Directory.Exists(package.resolvedPath)) continue;
            foreach (string path in Directory.GetFiles(package.resolvedPath))
            {
                string name = Path.GetFileName(path);
                if (name.EndsWith(".meta", StringComparison.OrdinalIgnoreCase)) continue;
                if (name.StartsWith("LICENSE", StringComparison.OrdinalIgnoreCase) ||
                    name.IndexOf("NOTICE", StringComparison.OrdinalIgnoreCase) >= 0)
                    Copy(path, "Packages/" + package.name + "/" + name + ".txt");
            }
        }
        Copy("Docs/PackageLicenseInventory.csv", "PackageLicenseInventory.csv");
        Copy("Docs/AssetLicenseInventory.csv", "AssetLicenseInventory.csv");
        AssetDatabase.ImportAsset(RuntimeCopy, ImportAssetOptions.ForceSynchronousImport);
        AssetDatabase.Refresh();
        Debug.Log("Credits: canonical text and legal notices synchronized; unclear licenses remain flagged.");
    }
    static void Copy(string source, string name) => CopyBytes(File.ReadAllBytes(source), "Assets/StreamingAssets/Licenses/" + name);
    static void CopyBytes(byte[] bytes, string destination)
    {
        if (File.Exists(destination) && File.ReadAllBytes(destination).SequenceEqual(bytes)) return;
        Directory.CreateDirectory(Path.GetDirectoryName(destination));
        File.WriteAllBytes(destination, bytes);
    }
    public void OnPreprocessBuild(BuildReport report)
    {
        // Validate only: do not change/import assets while BuildPipeline collects them.
        byte[] expected = File.ReadAllBytes("CREDITS.md");
        if (!File.Exists(RuntimeCopy) || !File.Exists(DistributedCopy) ||
            !File.ReadAllBytes(RuntimeCopy).SequenceEqual(expected) || !File.ReadAllBytes(DistributedCopy).SequenceEqual(expected))
            throw new BuildFailedException("CREDITS.md changed: run Tools > Rally > Sync Credits and License Notices before building.");
    }
}
