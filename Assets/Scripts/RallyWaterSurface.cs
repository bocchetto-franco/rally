using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Read-only footprint shared by water warnings and camera shake, including visual-only pools.</summary>
public sealed class RallyWaterSurface
{
    public readonly MonoBehaviour Source;
    readonly RallyPuddleMudVisual visual;
    readonly RallyPuddleSlowZone zone;
    readonly BoxCollider box;
    public Vector3 Center => visual != null ? visual.transform.TransformPoint(visual.localCenter) : box.bounds.center;
    public bool Active => Source != null && Source.isActiveAndEnabled;
    public float Radius => visual != null
        ? Mathf.Min(12f, Mathf.Max(visual.halfSize.x * visual.transform.lossyScale.x, visual.halfSize.y * visual.transform.lossyScale.z))
        : Mathf.Min(12f, box.bounds.extents.magnitude);

    RallyWaterSurface(RallyPuddleMudVisual value)
    {
        Source = visual = value; zone = value.GetComponent<RallyPuddleSlowZone>();
        box = value.GetComponent<BoxCollider>();
    }
    RallyWaterSurface(RallyPuddleSlowZone value)
    {
        Source = zone = value; box = value.GetComponent<BoxCollider>();
    }
    public bool Contains(Rigidbody body)
    {
        if (!Active || body == null) return false;
        if (zone != null && zone.ContainsVehicle(body)) return true;
        if (visual != null)
        {
            Vector3 p = visual.transform.InverseTransformPoint(body.position) - visual.localCenter;
            float x = p.x / Mathf.Max(.1f, visual.halfSize.x), z = p.z / Mathf.Max(.1f, visual.halfSize.y);
            return x*x + z*z <= 1f && Mathf.Abs(p.y * visual.transform.lossyScale.y) < 3f;
        }
        Vector3 local = box.transform.InverseTransformPoint(body.position) - box.center;
        return Mathf.Abs(local.x) <= box.size.x*.5f && Mathf.Abs(local.z) <= box.size.z*.5f && Mathf.Abs(local.y) <= box.size.y*.5f+2f;
    }
    public static RallyWaterSurface[] Read(Scene scene)
    {
        var list = new List<RallyWaterSurface>();
        foreach (var root in scene.GetRootGameObjects())
        {
            foreach (var visual in root.GetComponentsInChildren<RallyPuddleMudVisual>()) list.Add(new RallyWaterSurface(visual));
            foreach (var zone in root.GetComponentsInChildren<RallyPuddleSlowZone>())
                if (zone.GetComponent<RallyPuddleMudVisual>() == null) list.Add(new RallyWaterSurface(zone));
        }
        return list.ToArray();
    }
}
