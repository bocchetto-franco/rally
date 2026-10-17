using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

// Explicit authoring tool: exports mesh-only views, never modifies the source rigs.
public static class RallyMenuVehiclePreviewSetup
{
    public static void Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || BuildPipeline.isBuildingPlayer)
            throw new InvalidOperationException("Editor must be idle.");
        const string folder = "Assets/Resources/MenuVehicles";
        Directory.CreateDirectory(folder);
        AssetDatabase.Refresh();
        var floor = AssetDatabase.LoadAssetAtPath<Material>(folder + "/StudioFloor.mat");
        if (floor == null)
        {
            floor = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            floor.SetColor("_BaseColor", new Color(.095f, .105f, .12f));
            AssetDatabase.CreateAsset(floor, folder + "/StudioFloor.mat");
        }
        string[] sources = { "Assets/Resources/Vehicles/LanciaS4.prefab", "Assets/Resources/Vehicles/Peugeot205T16.prefab" };
        string[] names = { "LanciaS4", "Peugeot205T16" }; // Existing Audi preview remains byte-identical.
        Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/menu-vehicle-preview-setup.txt", "Mesh-only vehicle preview export\n");
        for (int i = 0; i < sources.Length; i++)
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(sources[i]);
            if (source == null) throw new Exception("Missing source " + sources[i]);
            var root = new GameObject(names[i]);
            try
            {
                foreach (Transform child in source.transform) Copy(child, root.transform);
                var renderers = root.GetComponentsInChildren<MeshRenderer>();
                if (renderers.Length == 0) throw new Exception("No visible geometry " + sources[i]);
                Bounds bounds = renderers[0].bounds;
                foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
                if (bounds.size.z < 3 || bounds.size.z > 6 || bounds.size.y > 3)
                    throw new Exception("Unexpected vehicle dimensions " + names[i] + " " + bounds);
                foreach (Transform child in root.transform)
                    child.localPosition -= new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
                Combine(root, names[i]);
                if (root.GetComponentsInChildren<Component>(true).Any(c => !(c is Transform) && !(c is MeshFilter) && !(c is MeshRenderer)))
                    throw new Exception("Preview contains non-visual components");
                PrefabUtility.SaveAsPrefabAsset(root, folder + "/" + names[i] + ".prefab");
                File.AppendAllText("Logs/menu-vehicle-preview-setup.txt", names[i] + ": " + renderers.Length + " source renderers -> " + root.GetComponentsInChildren<MeshRenderer>().Length + " material batches, dimensions " + bounds.size + ", no physics/scripts\n");
            }
            finally { Object.DestroyImmediate(root); }
        }
        File.AppendAllText("Logs/menu-vehicle-preview-setup.txt", "COMPLETE: PASS\n");
    }

    static void Combine(GameObject root, string name)
    {
        const string folder = "Assets/Art/MenuVehicles";
        Directory.CreateDirectory(folder); AssetDatabase.Refresh();
        var groups = new Dictionary<Material, List<CombineInstance>>();
        foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>())
        {
            var mesh = renderer.GetComponent<MeshFilter>().sharedMesh;
            for (int sub = 0; sub < mesh.subMeshCount; sub++)
            {
                var material = renderer.sharedMaterials[Math.Min(sub, renderer.sharedMaterials.Length - 1)];
                if (material == null || mesh.GetIndexCount(sub) == 0) continue;
                if (!groups.TryGetValue(material, out var group)) groups[material] = group = new List<CombineInstance>();
                group.Add(new CombineInstance { mesh = mesh, subMeshIndex = sub, transform = root.transform.worldToLocalMatrix * renderer.transform.localToWorldMatrix });
            }
        }
        int index = 0;
        foreach (var group in groups)
        {
            var mesh = new Mesh { name = name + " - " + group.Key.name, indexFormat = IndexFormat.UInt32 };
            mesh.CombineMeshes(group.Value.ToArray(), true, true, false);
            RemoveUnusedVertices(mesh);
            mesh.RecalculateBounds();
            string path = folder + "/" + name + "_" + index++ + ".asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null) AssetDatabase.CreateAsset(mesh, path);
            else { EditorUtility.CopySerialized(mesh, existing); Object.DestroyImmediate(mesh); mesh = existing; EditorUtility.SetDirty(mesh); }
            AssetDatabase.SaveAssetIfDirty(mesh);
            var part = new GameObject("Batch - " + group.Key.name); part.transform.SetParent(root.transform, false);
            part.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = part.AddComponent<MeshRenderer>(); renderer.sharedMaterial = group.Key;
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false; renderer.lightProbeUsage = LightProbeUsage.Off;
        }
        foreach (Transform child in root.transform.Cast<Transform>().Where(t => !t.name.StartsWith("Batch - ")).ToArray()) Object.DestroyImmediate(child.gameObject);
    }

    // CombineMeshes can copy a whole source vertex buffer for each material submesh.
    // Compact the derived mesh so batching doesn't multiply the menu's GPU/RAM footprint.
    static void RemoveUnusedVertices(Mesh mesh)
    {
        var triangles = mesh.triangles;
        var sourceVertices = mesh.vertices; var sourceNormals = mesh.normals; var sourceTangents = mesh.tangents; var sourceColors = mesh.colors;
        var vertices = new List<Vector3>(); var normals = new List<Vector3>(); var tangents = new List<Vector4>(); var colors = new List<Color>();
        var sourceUv = new List<Vector4>[8]; var uv = new List<Vector4>[8];
        for (int channel = 0; channel < 8; channel++) { sourceUv[channel] = new List<Vector4>(); mesh.GetUVs(channel, sourceUv[channel]); uv[channel] = new List<Vector4>(); }
        var remap = Enumerable.Repeat(-1, sourceVertices.Length).ToArray();
        for (int i = 0; i < triangles.Length; i++)
        {
            int original = triangles[i];
            if (remap[original] < 0)
            {
                remap[original] = vertices.Count; vertices.Add(sourceVertices[original]);
                if (sourceNormals.Length > 0) normals.Add(sourceNormals[original]);
                if (sourceTangents.Length > 0) tangents.Add(sourceTangents[original]);
                if (sourceColors.Length > 0) colors.Add(sourceColors[original]);
                for (int channel = 0; channel < 8; channel++) if (sourceUv[channel].Count > 0) uv[channel].Add(sourceUv[channel][original]);
            }
            triangles[i] = remap[original];
        }
        mesh.Clear(); mesh.indexFormat = vertices.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16;
        mesh.SetVertices(vertices); if (normals.Count > 0) mesh.SetNormals(normals); if (tangents.Count > 0) mesh.SetTangents(tangents); if (colors.Count > 0) mesh.SetColors(colors);
        for (int channel = 0; channel < 8; channel++) if (uv[channel].Count > 0) mesh.SetUVs(channel, uv[channel]);
        mesh.SetTriangles(triangles, 0);
    }

    static bool Visible(Transform source)
    {
        if (!source.gameObject.activeSelf) return false;
        var renderer = source.GetComponent<MeshRenderer>();
        var filter = source.GetComponent<MeshFilter>();
        if (renderer != null && renderer.enabled && filter != null && filter.sharedMesh != null) return true;
        foreach (Transform child in source) if (Visible(child)) return true;
        return false;
    }

    static void Copy(Transform source, Transform parent)
    {
        if (!Visible(source)) return;
        var copy = new GameObject(source.name);
        copy.transform.SetParent(parent, false);
        copy.transform.localPosition = source.localPosition;
        copy.transform.localRotation = source.localRotation;
        copy.transform.localScale = source.localScale;
        var filter = source.GetComponent<MeshFilter>();
        var renderer = source.GetComponent<MeshRenderer>();
        if (filter != null && filter.sharedMesh != null && renderer != null && renderer.enabled)
        {
            copy.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
            var view = copy.AddComponent<MeshRenderer>();
            view.sharedMaterials = renderer.sharedMaterials;
            view.shadowCastingMode = ShadowCastingMode.Off;
            view.receiveShadows = false;
            view.lightProbeUsage = LightProbeUsage.Off;
        }
        foreach (Transform child in source) Copy(child, copy.transform);
    }
}
