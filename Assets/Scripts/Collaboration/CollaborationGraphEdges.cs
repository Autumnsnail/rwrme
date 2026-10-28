using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Masked uGUI parent edges; no scene objects or runtime material assets.</summary>
[RequireComponent(typeof(CanvasRenderer))]
public sealed class CollaborationGraphEdges : MaskableGraphic
{
    public List<CollaborationCommitGraphModel.Edge> edges = new List<CollaborationCommitGraphModel.Edge>();
    public static Color LaneColor(int lane)
    {
        var colors = new[] { new Color(.49f,.75f,1), new Color(.62f,.86f,.58f), new Color(.94f,.68f,.4f), new Color(.83f,.62f,.97f), new Color(.4f,.86f,.82f) };
        return colors[lane % colors.Length];
    }
    protected override void OnPopulateMesh(VertexHelper helper)
    {
        helper.Clear();
        foreach (var edge in edges)
        {
            var from = new Vector2(20 + edge.child.lane * 28, -30 - edge.child.index * 64);
            var to = new Vector2(20 + edge.parent.lane * 28, -30 - edge.parent.index * 64);
            var previous = from; var color = LaneColor(edge.lane);
            // Cubic elbows leave every commit vertically, then join its parent vertically.
            int segments = edge.child.lane == edge.parent.lane ? 1 : 12;
            for (int i = 1; i <= segments; i++)
            {
                float t = i / (float)segments, u = 1 - t;
                var a = from + Vector2.down * 32; var b = to + Vector2.up * 32;
                var next = u*u*u*from + 3*u*u*t*a + 3*u*t*t*b + t*t*t*to;
                var normal = new Vector2(-(next-previous).y, (next-previous).x).normalized * 1.25f;
                int start = helper.currentVertCount;
                helper.AddVert(previous-normal,color,Vector2.zero); helper.AddVert(previous+normal,color,Vector2.zero);
                helper.AddVert(next+normal,color,Vector2.zero); helper.AddVert(next-normal,color,Vector2.zero);
                helper.AddTriangle(start,start+1,start+2); helper.AddTriangle(start,start+2,start+3); previous = next;
            }
        }
    }
}
