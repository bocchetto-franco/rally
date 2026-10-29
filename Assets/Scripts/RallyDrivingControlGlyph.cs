using UnityEngine;
using UnityEngine.UI;

/// <summary>Small vector button drawings: no textures or font-dependent symbol glyphs.</summary>
[RequireComponent(typeof(CanvasRenderer))]
public sealed class RallyDrivingControlGlyph : MaskableGraphic
{
    public enum Shape { Trigger, Triangle, Square, Circle }
    Shape shape;
    public void SetShape(Shape value, Color tint)
    {
        shape = value; color = tint; raycastTarget = false; SetVerticesDirty();
    }
    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        if (shape == Shape.Circle)
        {
            for (int i = 0; i < 24; i++)
            {
                float a = i * Mathf.PI / 12, b = (i + 1) * Mathf.PI / 12;
                Line(mesh, new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 12,
                    new Vector2(Mathf.Cos(b), Mathf.Sin(b)) * 12);
            }
        }
        else if (shape == Shape.Triangle)
        {
            Line(mesh, new Vector2(0, 11), new Vector2(-11, -9));
            Line(mesh, new Vector2(-11, -9), new Vector2(11, -9));
            Line(mesh, new Vector2(11, -9), new Vector2(0, 11));
        }
        else
        {
            float top = shape == Shape.Trigger ? 13 : 10, bottom = shape == Shape.Trigger ? 10 : 10;
            Line(mesh, new Vector2(-top, 10), new Vector2(top, 10));
            Line(mesh, new Vector2(top, 10), new Vector2(bottom, -10));
            Line(mesh, new Vector2(bottom, -10), new Vector2(-bottom, -10));
            Line(mesh, new Vector2(-bottom, -10), new Vector2(-top, 10));
        }
    }
    void Line(VertexHelper mesh, Vector2 start, Vector2 end)
    {
        Vector2 d = (end - start).normalized;
        Vector2 n = new Vector2(-d.y, d.x) * 1.1f;
        int first = mesh.currentVertCount;
        mesh.AddVert(start - n, color, Vector2.zero); mesh.AddVert(start + n, color, Vector2.zero);
        mesh.AddVert(end + n, color, Vector2.zero); mesh.AddVert(end - n, color, Vector2.zero);
        mesh.AddTriangle(first, first + 1, first + 2); mesh.AddTriangle(first, first + 2, first + 3);
    }
}
