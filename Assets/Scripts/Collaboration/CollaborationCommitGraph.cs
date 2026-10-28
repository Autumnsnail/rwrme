using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public sealed class CollaborationCommitGraph : MonoBehaviour
{
    public ScrollRect scroll;
    public RectTransform content;
    public CollaborationGraphEdges lines;
    public CollaborationCommitNode nodeTemplate;
    readonly List<CollaborationGraphEdges> extraLines = new List<CollaborationGraphEdges>();
    readonly List<CollaborationCommitNode> nodes = new List<CollaborationCommitNode>();
    CollaborationCommitGraphModel.Layout layout;
    string selection;
    float lastWidth;
    public void Present(CollaborationPanel session, Action<string> select, Action activate, bool showWorklines = false, string mergeAnchor = null)
    {
        var source = session.Commits.ToList();
        // Old branch tips may lie outside this history page. They remain navigable.
        foreach (var id in session.Branches.Select(b => b.head).Concat(new[] {session.CurrentHead, session.SelectedRevision}).Where(id => !string.IsNullOrEmpty(id)).Distinct())
            if (!source.Any(c => c.id == id)) source.Add(new CollaborationPanel.Commit { id=id, message="较早的节点 · 加载更早提交以查看详情", parents=new string[0] });
        layout = CollaborationCommitGraphModel.Build(source);
        // Keep each Graphic below uGUI's 65k vertex limit when paging long histories.
        const int edgesPerGraphic = 500;
        lines.edges = layout.edges.Take(edgesPerGraphic).ToList(); lines.SetVerticesDirty();
        int batches = Mathf.Max(0, (layout.edges.Count - 1) / edgesPerGraphic);
        for (int i=0;i<batches;i++)
        {
            if (i==extraLines.Count) { var copy=Instantiate(lines,content,false); copy.transform.SetAsFirstSibling(); extraLines.Add(copy); }
            extraLines[i].gameObject.SetActive(true);
            extraLines[i].edges=layout.edges.Skip((i+1)*edgesPerGraphic).Take(edgesPerGraphic).ToList(); extraLines[i].SetVerticesDirty();
        }
        for(int i=batches;i<extraLines.Count;i++) extraLines[i].gameObject.SetActive(false);
        nodeTemplate.gameObject.SetActive(false);
        var parents = new HashSet<string>(source.SelectMany(c => c.parents ?? new string[0]));
        for (int i=0;i<layout.rows.Count;i++)
        {
            if (nodes.Count==i) nodes.Add(Instantiate(nodeTemplate, content, false));
            var row = layout.rows[i]; var node = nodes[i]; node.gameObject.SetActive(true);
            var rect = node.transform as RectTransform;
            rect.anchorMin = new Vector2(0,1); rect.anchorMax = Vector2.one; rect.pivot = new Vector2(0,1);
            rect.offsetMin = new Vector2(0, -i*64-60); rect.offsetMax = new Vector2(0, -i*64);
            string heads = showWorklines ? string.Join("  /  ", session.Branches.Where(b => b.head == row.commit.id).Select(b => b.name)) : parents.Contains(row.commit.id) ? "" : "末端节点";
            node.Present(row, layout.lanes*28+22, CollaborationGraphEdges.LaneColor(row.lane), row.commit.id==session.SelectedRevision,
                row.commit.id==session.CurrentHead, heads, !session.Busy, select, activate, row.commit.id==mergeAnchor);
        }
        for(int i=layout.rows.Count;i<nodes.Count;i++) nodes[i].gameObject.SetActive(false);
        Resize();
        if (selection != session.SelectedRevision)
        {
            var selected = layout.rows.FirstOrDefault(r => r.commit.id == session.SelectedRevision);
            if (selected != null) content.anchoredPosition = new Vector2(0, Mathf.Clamp(selected.index*64 - scroll.viewport.rect.height*.3f, 0, Mathf.Max(0,content.rect.height-scroll.viewport.rect.height)));
            selection = session.SelectedRevision;
        }
    }
    void LateUpdate() { if (layout != null && Mathf.Abs(lastWidth-scroll.viewport.rect.width)>.5f) Resize(); }
    void Resize()
    {
        if(layout==null)return;
        lastWidth=scroll.viewport.rect.width;
        content.sizeDelta=new Vector2(Mathf.Max(lastWidth,layout.lanes*28+390),Mathf.Max(scroll.viewport.rect.height,layout.rows.Count*64+8));
        lines.rectTransform.sizeDelta=content.sizeDelta;
        foreach(var graphic in extraLines) graphic.rectTransform.sizeDelta=content.sizeDelta;
    }
}
