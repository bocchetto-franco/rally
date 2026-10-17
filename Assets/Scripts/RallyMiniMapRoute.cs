using UnityEngine;
using UnityEngine.UI;

/// <summary>Static closed X/Z route; moving markers never dirty this graphic.</summary>
[RequireComponent(typeof(CanvasRenderer))]
public sealed class RallyMiniMapRoute : MaskableGraphic
{
    Vector2[] points;
    float thickness = 3f;
    public int MeshBuildCount { get; private set; }
    public int PointCount => points == null ? 0 : points.Length;

    public void Configure(Vector2[] route, float lineWidth)
    {
        points = route; thickness = lineWidth; raycastTarget = false;
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        if (points == null || points.Length < 3) return;
        MeshBuildCount++;
        for (int i = 0; i < points.Length; i++)
        {
            Vector2 before = (points[i] - points[(i + points.Length - 1) % points.Length]).normalized;
            Vector2 after = (points[(i + 1) % points.Length] - points[i]).normalized;
            Vector2 normal = new Vector2(-after.y, after.x);
            Vector2 miter = (new Vector2(-before.y, before.x) + normal).normalized;
            if (miter.sqrMagnitude < .01f) miter = normal;
            Vector2 offset = miter * (thickness * .5f / Mathf.Max(.5f, Vector2.Dot(miter, normal)));
            mesh.AddVert(points[i] - offset, color, Vector2.zero);
            mesh.AddVert(points[i] + offset, color, Vector2.zero);
        }
        for (int i = 0; i < points.Length; i++)
        {
            int a = i * 2, b = ((i + 1) % points.Length) * 2;
            mesh.AddTriangle(a, a + 1, b + 1);
            mesh.AddTriangle(b + 1, b, a);
        }
    }
}
