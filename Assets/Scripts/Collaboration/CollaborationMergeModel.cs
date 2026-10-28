using System;
using System.Collections.Generic;
using System.Linq;

[Serializable] public sealed class CollaborationMergeValue
{
    public bool exists, editable;
    public int bytes;
    public string text, image, parent, digest;
    public CollaborationMergeField[] fields;
}
[Serializable] public sealed class CollaborationMergeField { public string name, value; }
[Serializable] public sealed class CollaborationMergeConflict { public string key, path, element, kind, choice; public bool resolved; }
[Serializable] public sealed class CollaborationMergePlan
{
    public string left, right, ancestor, previewId;
    public int unresolved, automaticFiles, fileCount;
    public CollaborationMergeConflict[] conflicts;
}
[Serializable] public sealed class CollaborationMergeDetail
{
    public string key;
    public CollaborationMergeValue ancestor, left, right;
    public bool canEdit, canDelete;
}
[Serializable] public sealed class CollaborationMergeResolution { public string key, choice, content, parent; }

public sealed class CollaborationMergeSession
{
    public readonly string left, right, requestId;
    public CollaborationMergePlan plan;
    public CollaborationMergeDetail detail;
    public string selectedKey, commitMessage, completedHead, completedBranch;
    public bool validated, attempted;
    readonly Dictionary<string, CollaborationMergeResolution> choices = new Dictionary<string, CollaborationMergeResolution>(StringComparer.Ordinal);
    public CollaborationMergeSession(string left, string right)
    { this.left=left; this.right=right; requestId=Guid.NewGuid().ToString("N"); }
    public CollaborationMergeResolution[] Resolutions => choices.Values.OrderBy(c=>c.key,StringComparer.Ordinal).ToArray();
    public void Choose(string choice, string text = null, string parent = null)
    {
        if (attempted || string.IsNullOrEmpty(selectedKey)) return;
        choices[selectedKey] = new CollaborationMergeResolution {key=selectedKey,choice=choice,parent=parent,
            content=choice=="custom"?Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(text??"")):""};
        validated=false;
    }
    public void Reset() { if(!attempted && selectedKey!=null) { choices.Remove(selectedKey); validated=false; } }
    public void Accept(CollaborationMergePlan value)
    {
        plan=value; validated=true;
        var conflicts=value.conflicts??new CollaborationMergeConflict[0];
        foreach(var key in choices.Keys.Where(key=>!conflicts.Any(c=>c.key==key)).ToArray()) choices.Remove(key);
        if(!conflicts.Any(c=>c.key==selectedKey)) { selectedKey=conflicts.FirstOrDefault(c=>!c.resolved)?.key??conflicts.FirstOrDefault()?.key; detail=null; }
    }
}
