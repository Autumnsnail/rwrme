using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

/// <summary>Each branch owns a durable snapshot plus its last remote baseline.</summary>
public static class CollaborationWorkspaceStore
{
    public static string Key(CollaborationPanel.Checkout state) => Key(state.branch, state.detached ? state.head : null);
    public static string Key(string branch, string revision) => string.IsNullOrEmpty(revision) ? branch : branch + "@" + revision;

    public static string Digest(CollaborationPanel.MapFile[] files)
    {
        string manifest = string.Join("\n", files.OrderBy(f => f.path, StringComparer.Ordinal).Select(f => f.path + ":" + Hash(Convert.FromBase64String(f.content))));
        return Hash(Encoding.UTF8.GetBytes(manifest));
    }
    static string Hash(byte[] value)
    {
        using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(value)).Replace("-", "").ToLowerInvariant();
    }
    public static bool ShouldRestore(CollaborationPanel.Workspace workspace, bool forceRemote) =>
        !forceRemote && workspace?.checkout != null && !workspace.submitted && workspace.files != null && Digest(workspace.files) != workspace.checkout.digest;

    public static void Save(string path, CollaborationPanel.Workspace workspace)
    {
        if (workspace?.checkout == null || workspace.files == null) throw new ArgumentException("A workspace needs its baseline and files.");
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        string staged = path + "." + Guid.NewGuid().ToString("N") + ".new";
        try
        {
            File.WriteAllText(staged, JsonUtility.ToJson(workspace));
            if (File.Exists(path)) File.Replace(staged, path, null); else File.Move(staged, path);
        }
        finally { if (File.Exists(staged)) File.Delete(staged); }
    }
    public static CollaborationPanel.Workspace Load(string path) => File.Exists(path)
        ? JsonUtility.FromJson<CollaborationPanel.Workspace>(File.ReadAllText(path)) : null;
}
