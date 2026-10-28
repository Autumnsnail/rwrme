using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>Topological commit layout: children precede parents, including both merge parents.</summary>
public static class CollaborationCommitGraphModel
{
    public sealed class Row { public CollaborationPanel.Commit commit; public int index, lane; }
    public sealed class Edge { public Row child, parent; public int lane; }
    public sealed class Layout
    {
        public readonly List<Row> rows = new List<Row>();
        public readonly List<Edge> edges = new List<Edge>();
        public int lanes = 1;
    }
    public static Layout Build(IEnumerable<CollaborationPanel.Commit> source)
    {
        var model = new Layout();
        var commits = (source ?? Enumerable.Empty<CollaborationPanel.Commit>()).Where(c => c != null && !string.IsNullOrEmpty(c.id))
            .GroupBy(c => c.id).ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
        var children = commits.Keys.ToDictionary(id => id, id => 0, StringComparer.Ordinal);
        foreach (var commit in commits.Values)
            foreach (var parent in (commit.parents ?? new string[0]).Distinct())
                if (parent != null && children.ContainsKey(parent)) children[parent]++;
        var ready = commits.Values.Where(c => children[c.id] == 0).ToList();
        var pending = new List<string>(); var placed = new Dictionary<string, Row>(StringComparer.Ordinal);
        while (placed.Count < commits.Count)
        {
            // Also tolerate incomplete/legacy data without getting stuck on cycles.
            var commit = ready.OrderByDescending(c => c.created).ThenBy(c => c.id, StringComparer.Ordinal).FirstOrDefault()
                ?? commits.Values.First(c => !placed.ContainsKey(c.id));
            ready.Remove(commit);
            if (placed.ContainsKey(commit.id)) continue;
            int lane = pending.IndexOf(commit.id);
            if (lane < 0) { lane = pending.IndexOf(null); if (lane < 0) { lane = pending.Count; pending.Add(null); } }
            pending[lane] = null;
            var row = new Row { commit = commit, lane = lane, index = model.rows.Count };
            placed[commit.id] = row; model.rows.Add(row); model.lanes = Math.Max(model.lanes, lane + 1);
            bool first = true;
            foreach (var parent in (commit.parents ?? new string[0]).Where(p => !string.IsNullOrEmpty(p)).Distinct())
            {
                if (placed.ContainsKey(parent)) continue;
                if (!pending.Contains(parent))
                {
                    int target = first && pending[lane] == null ? lane : pending.IndexOf(null);
                    if (target < 0) { target = pending.Count; pending.Add(null); }
                    pending[target] = parent; model.lanes = Math.Max(model.lanes, target + 1);
                }
                first = false;
                if (children.ContainsKey(parent) && --children[parent] == 0) ready.Add(commits[parent]);
            }
        }
        foreach (var child in model.rows)
            foreach (var id in (child.commit.parents ?? new string[0]).Where(p => !string.IsNullOrEmpty(p)).Distinct())
                if (placed.TryGetValue(id, out var parent) && parent.index > child.index)
                    model.edges.Add(new Edge { child = child, parent = parent, lane = parent.lane });
        return model;
    }
}
