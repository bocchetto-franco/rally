using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>A curved rally arrow drawn as UI geometry; no font glyph or texture dependency.</summary>
[RequireComponent(typeof(CanvasRenderer))]
public sealed class RallyPaceNoteArrow : MaskableGraphic
{
    RallyBrakeWarningTrigger.TurnDirection direction;
    int grade = 3;
    bool hairpin;
    static readonly float[] Angles = { 150f, 115f, 90f, 65f, 40f, 23f };

    public void SetNote(RallyBrakeWarningTrigger.TurnDirection turn, int number, bool isHairpin)
    {
        direction = turn;
        grade = Mathf.Clamp(number, 1, 6);
        hairpin = isHairpin;
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        float angle = (hairpin ? 180f : Angles[grade - 1]) * Mathf.Deg2Rad;
        var points = new List<Vector2> { new Vector2(0f, -.48f), new Vector2(0f, -.12f) };
        const float radius = .36f;
        const int arcSegments = 32;
        for (int i = 1; i <= arcSegments; i++)
        {
            float a = angle * i / arcSegments;
            points.Add(new Vector2(radius * (1f - Mathf.Cos(a)), -.12f + radius * Mathf.Sin(a)));
        }
        Vector2 tangent = new Vector2(Mathf.Sin(angle), Mathf.Cos(angle));
        Vector2 side = new Vector2(-tangent.y, tangent.x);
        Vector2 tip = points[points.Count - 1] + tangent * .16f;
        Vector2 left = points[points.Count - 1] - tangent * .05f + side * .16f;
        Vector2 right = points[points.Count - 1] - tangent * .05f - side * .16f;
        Vector2 min = Vector2.Min(tip, Vector2.Min(left, right)), max = Vector2.Max(tip, Vector2.Max(left, right));
        foreach (Vector2 p in points) { min = Vector2.Min(min, p - Vector2.one * .055f); max = Vector2.Max(max, p + Vector2.one * .055f); }
        Rect rect = GetPixelAdjustedRect();
        float scale = Mathf.Min(rect.width / (max.x - min.x), rect.height / (max.y - min.y)) * .88f;
        Vector2 center = (min + max) * .5f;
        Vector2 Map(Vector2 p)
        {
            p = (p - center) * scale;
            if (direction == RallyBrakeWarningTrigger.TurnDirection.Left) p.x = -p.x;
            return p + rect.center;
        }
        // Shared pairs form a continuous strip. Separate segment quads leave
        // little cracks along the outside of the bend when MSAA is disabled.
        for (int i = 0; i < points.Count; i++)
        {
            Vector2 incoming = (points[i] - points[Mathf.Max(0, i - 1)]).normalized;
            Vector2 outgoing = (points[Mathf.Min(points.Count - 1, i + 1)] - points[i]).normalized;
            Vector2 along = (incoming + outgoing).normalized;
            Vector2 normal = new Vector2(-along.y, along.x) * .055f;
            Add(vh, Map(points[i] - normal)); Add(vh, Map(points[i] + normal));
            if (i == 0) continue;
            int previous = (i - 1) * 2, current = i * 2;
            vh.AddTriangle(previous, previous + 1, current + 1);
            vh.AddTriangle(previous, current + 1, current);
        }
        int head = vh.currentVertCount;
        Add(vh, Map(left)); Add(vh, Map(tip)); Add(vh, Map(right));
        vh.AddTriangle(head, head + 1, head + 2);
    }

    void Add(VertexHelper vh, Vector2 position)
    {
        var vertex = UIVertex.simpleVert;
        vertex.position = position;
        vertex.color = color;
        vh.AddVert(vertex);
    }
}
