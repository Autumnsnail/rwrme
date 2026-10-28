using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>Branch ancestry tree shared by UI rendering and tests.</summary>
public static class CollaborationBranchTree
{
    public sealed class Row
    {
        public CollaborationPanel.Branch branch;
        public int depth;
        public bool hasChildren;
    }

    public static List<Row> Build(CollaborationPanel.Branch[] branches, ISet<string> collapsed, string filter)
    {
        var nodes = (branches ?? new CollaborationPanel.Branch[0]).ToDictionary(b => b.name, StringComparer.Ordinal);
        var children = nodes.Keys.ToDictionary(k => k, k => new List<CollaborationPanel.Branch>(), StringComparer.Ordinal);
        var roots = new List<CollaborationPanel.Branch>();
        foreach (var branch in nodes.Values)
        {
            if (!string.IsNullOrEmpty(branch.parent) && branch.parent != branch.name && nodes.ContainsKey(branch.parent))
                children[branch.parent].Add(branch);
            else roots.Add(branch);
        }
        var included = new HashSet<string>(StringComparer.Ordinal);
        bool searching = !string.IsNullOrWhiteSpace(filter);
        foreach (var branch in nodes.Values)
        {
            if (searching && branch.name.IndexOf(filter.Trim(), StringComparison.OrdinalIgnoreCase) < 0) continue;
            var cursor = branch;
            var chain = new HashSet<string>(StringComparer.Ordinal);
            while (cursor != null && chain.Add(cursor.name))
            {
                included.Add(cursor.name);
                cursor = !string.IsNullOrEmpty(cursor.parent) && nodes.TryGetValue(cursor.parent, out var parent) ? parent : null;
            }
        }
        var result = new List<Row>();
        var visited = new HashSet<string>(StringComparer.Ordinal);
        Action<CollaborationPanel.Branch, int> visit = null;
        visit = (branch, depth) =>
        {
            if (!included.Contains(branch.name) || !visited.Add(branch.name)) return;
            bool hasChildren = children[branch.name].Any(b => included.Contains(b.name));
            result.Add(new Row { branch = branch, depth = depth, hasChildren = hasChildren });
            if (!searching && collapsed.Contains(branch.name))
            {
                var pending = new Stack<CollaborationPanel.Branch>(children[branch.name]);
                while (pending.Count > 0)
                {
                    var child = pending.Pop();
                    if (visited.Add(child.name)) foreach (var item in children[child.name]) pending.Push(item);
                }
                return;
            }
            foreach (var child in children[branch.name].OrderBy(b => b.name, StringComparer.Ordinal)) visit(child, depth + 1);
        };
        foreach (var branch in roots.OrderBy(b => b.name == "main" ? 0 : 1).ThenBy(b => b.name, StringComparer.Ordinal)) visit(branch, 0);
        // An old or externally edited server may contain an ancestry cycle.
        foreach (var branch in nodes.Values.OrderBy(b => b.name, StringComparer.Ordinal)) visit(branch, 0);
        return result;
    }
}
