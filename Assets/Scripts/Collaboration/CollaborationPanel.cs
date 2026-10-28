using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;

/// <summary>Persistent collaboration session; presentation lives in a serialized uGUI prefab.</summary>
public sealed class CollaborationPanel : MonoBehaviour
{
    [Serializable] public class MapFile { public string path; public string content; }
    [Serializable] public class Branch { public string name; public string head; public string parent; }
    [Serializable] public class Commit { public string id, author, message; public string[] parents; public double created; }
    [Serializable] public class Request
    {
        public string operation, userId, project, branch, expectedHead, newBranch, sourceBranch, message, revision, cursor, baseRevision;
        public string leftRevision, rightRevision, previewId, conflictKey, requestId;
        public CollaborationMergeResolution[] resolutions;
        public int limit;
        public MapFile[] files;
    }
    [Serializable] public class Response
    {
        public bool success, forked;
        public string error, head, revision, nextCursor, branch, baseRevision;
        public CollaborationMergePlan merge;
        public CollaborationMergeDetail mergeDetail;
        public Commit[] commits;
        public string[] conflicts;
        public Branch[] branches;
        public MapFile[] files;
    }
    [Serializable] public class Checkout
    {
        public string origin, project, branch, head, digest;
        public bool known, detached;
    }
    [Serializable] public class Workspace { public Checkout checkout; public MapFile[] files; public bool submitted; }

    static CollaborationPanel instance;
    public static bool IsOpen => instance != null && (instance.visible || instance.busy || instance.reloadRequired);
    CollaborationView view;
    bool visible, busy, connected, reloadRequired;
    string server = "http://127.0.0.1:8090", project = "my-map", branch = "main", selectedBranch = "main", userId = "", token = "";
    string status = "", operationLabel = "";
    Branch[] branches = new Branch[0];
    Commit[] commits = new Commit[0];
    string selectedRevision = "", historyCursor = "";
    bool selectedNode;
    Checkout checkout;
    string mergeAnchor = "";
    CollaborationMergeSession mergeSession;
    public string MergeAnchor => mergeAnchor;
    public CollaborationMergeSession MergeSession => mergeSession;
    CursorLockMode previousCursor;
    bool previousCursorVisible;
    const long MaxBytes = 64L * 1024 * 1024;
    public bool Connected => connected;
    public bool Busy => busy;
    public bool Visible => visible;
    public bool NeedsReload => reloadRequired;
    public string Server => server;
    public string Project => project;
    public string UserId => userId;
    public string CurrentBranch => branch;
    public string SelectedBranch => selectedBranch;
    public string Status => busy ? operationLabel : status;
    public Branch[] Branches => branches;
    public Commit[] Commits => commits;
    public string SelectedRevision => selectedRevision;
    public bool SelectionIsNode => selectedNode;
    public bool HasMoreHistory => !string.IsNullOrEmpty(historyCursor);
    public bool IsDetached => CurrentReady && checkout.detached;
    public bool SelectedIsTip => selectedRevision == (branches.FirstOrDefault(b => b.name == selectedBranch)?.head ?? "");
    public bool CanSubmit => CurrentReady || branches.Any(b => b.name == branch && string.IsNullOrEmpty(b.head));
    public bool CanSwitch => !CurrentReady || branch != selectedBranch || CurrentHead != selectedRevision || IsDetached != selectedNode;
    public bool CurrentReady => Matches(checkout, branch) && (checkout.known || !string.IsNullOrEmpty(checkout.head));
    public string CurrentHead => CurrentReady ? checkout.head : "";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Install()
    {
        if (instance != null) return;
        var go = new GameObject("Map Collaboration Session");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<CollaborationPanel>();
    }
    void Awake()
    {
        userId = CollaborationIdentity.UserId;
        server = PlayerPrefs.GetString("rwrme.collaboration.server", server);
        project = PlayerPrefs.GetString("rwrme.collaboration.project", project);
    }
    void LateUpdate()
    {
        if (view != null) return;
        var canvas = CoverUiLayout.FindOnlyUiCanvas();
        if (canvas == null || UIManager.instance == null) return;
        var prefab = Resources.Load<CollaborationView>("UI/CollaborationPanel");
        if (prefab == null) { enabled = false; Debug.LogError("Collaboration UI prefab is missing."); return; }
        view = canvas.GetComponentInChildren<CollaborationView>(true);
        if (view == null) view = Instantiate(prefab, canvas.transform, false);
        view.name = "CollaborationUI";
        view.transform.SetAsLastSibling();
        try
        {
            string path = StatePath(MapRoot);
            if (File.Exists(path))
            {
                var state = JsonUtility.FromJson<Checkout>(File.ReadAllText(path));
                if (state != null && state.origin == server.TrimEnd('/') && state.project == project)
                { checkout = state; branch = state.branch; if (string.IsNullOrEmpty(selectedBranch)) selectedBranch = branch; }
            }
        }
        catch (Exception error) { status = error.Message; }
        view.Bind(this);
        Render();
    }
    void OnDestroy() { if (instance == this) instance = null; }
    void Render() { if (view != null) view.Refresh(this); }
    public void Open()
    {
        previousCursor = Cursor.lockState; previousCursorVisible = Cursor.visible;
        Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
        visible = true; Render();
    }
    public void Close()
    {
        if (busy || reloadRequired) return;
        visible = false; Render();
        Cursor.lockState = previousCursor; Cursor.visible = previousCursorVisible;
    }
    public void SelectBranch(string name)
    {
        if (busy) return;
        var found = branches.FirstOrDefault(b => b.name == name); if (found == null) return;
        selectedBranch = name; selectedRevision = found.head ?? ""; selectedNode = false; Render();
    }
    public void SelectRevision(string id)
    {
        if (busy || string.IsNullOrEmpty(id)) return;
        selectedRevision = id; selectedNode = true;
        // Keep branch context unless this is the head of another branch.
        if (!branches.Any(b => b.name == selectedBranch && b.head == id))
        {
            var tip = branches.FirstOrDefault(b => b.head == id);
            if (tip != null) selectedBranch = tip.name;
        }
        Render();
    }
    public void LoadMoreHistory() { if (connected && !busy && HasMoreHistory) StartCoroutine(Run(HistoryRoutine(true), "Loading older nodes")); }
    public void Disconnect()
    {
        if (busy) return;
        connected = false; token = ""; mergeAnchor=""; mergeSession=null; branches = new Branch[0]; commits = new Commit[0]; selectedRevision = historyCursor = "";
        status = "Disconnected"; Render();
    }
    public void Connect(string url, string projectId, string author, string secret)
    {
        if (busy) return;
        server = url.Trim(); project = projectId.Trim(); userId = author.Trim(); token = secret;
        StartCoroutine(Run(ConnectRoutine(), "Connecting"));
    }
    public void RefreshBranches() { if (connected && !busy) StartCoroutine(Run(RefreshRoutine(), "Refreshing branches")); }
    public void Publish(string message) { if (connected && !busy) StartCoroutine(Run(PublishRoutine(message), "Saving and submitting")); }
    public void SwitchToSelected() { if (connected && !busy) StartCoroutine(Run(SwitchRoutine(selectedBranch, false, selectedNode || !SelectedIsTip ? selectedRevision : null), "Switching node")); }
    public void UpdateCurrent() { if (connected && !busy) StartCoroutine(Run(SwitchRoutine(branch, !IsDetached), IsDetached ? "Returning to working branch" : "Updating branch")); }
    public void CreateBranch(string name) { if (connected && !busy) StartCoroutine(Run(CreateRoutine(name.Trim()), "Creating branch")); }
    public void MergeSelected(string message) { if (connected && !busy) StartCoroutine(Run(MergeRoutine(selectedBranch, message), "Merging branches")); }

    public void PickForMerge()
    {
        if (!connected || busy || reloadRequired || string.IsNullOrEmpty(selectedRevision)) return;
        if (string.IsNullOrEmpty(mergeAnchor)) { mergeAnchor=selectedRevision; status="已标记合并节点 A。再单击另一个节点，然后点击合并；再次点击同一节点可取消标记。"; Render(); return; }
        if (mergeAnchor==selectedRevision) { mergeAnchor=""; status="已取消合并标记。"; Render(); return; }
        mergeSession=new CollaborationMergeSession(mergeAnchor,selectedRevision);
        StartCoroutine(Run(MergePreviewRoutine(),"正在预览合并"));
    }
    public void CancelVisualMerge() { if(!busy) { mergeSession=null; mergeAnchor=""; Render(); } }
    Request MergeRequest(string operation)
    {
        var body=MakeRequest(operation,"main"); var state=mergeSession;
        body.leftRevision=state.left; body.rightRevision=state.right; body.resolutions=state.Resolutions;
        body.previewId=state.plan?.previewId; body.conflictKey=state.selectedKey; body.requestId=state.requestId;
        return body;
    }
    IEnumerator MergePreviewRoutine()
    {
        mergeSession.validated=false;
        yield return Remote(MergeRequest("merge_preview"),response=>mergeSession.Accept(response.merge ?? throw new IOException("Update masterserver for visual node merges.")));
        status=mergeSession.plan.unresolved==0?"已完成自动合并，可创建新节点。":"请在合并面板中解决冲突。";
        if(!string.IsNullOrEmpty(mergeSession.selectedKey)) yield return MergeDetailRoutine();
    }
    IEnumerator MergeDetailRoutine()
    {
        yield return Remote(MergeRequest("merge_detail"),response=>mergeSession.detail=response.mergeDetail);
    }
    public void SelectMergeConflict(string key)
    {
        if(busy || mergeSession==null || mergeSession.attempted) return;
        mergeSession.selectedKey=key; mergeSession.detail=null;
        StartCoroutine(Run(MergeDetailRoutine(),"正在读取冲突版本"));
    }
    public void ResolveMergeConflict(string choice,string text=null,string parent=null)
    {
        if(busy || mergeSession==null || mergeSession.attempted) return;
        mergeSession.Choose(choice,text,parent);
        StartCoroutine(Run(MergePreviewRoutine(),"正在校验冲突处理"));
    }
    public void ResetMergeConflict()
    {
        if(busy || mergeSession==null || mergeSession.attempted) return;
        mergeSession.Reset(); StartCoroutine(Run(MergePreviewRoutine(),"正在校验冲突处理"));
    }
    public void CommitVisualMerge(string message)
    {
        if(busy || mergeSession?.plan==null || !mergeSession.validated || mergeSession.plan.unresolved!=0) return;
        if(!mergeSession.attempted) mergeSession.commitMessage=message;
        StartCoroutine(Run(VisualMergeCommitRoutine(),"正在创建合并节点"));
    }
    IEnumerator VisualMergeCommitRoutine()
    {
        var state=mergeSession;
        // Preserve the immutable result across download/refresh failures, so a
        // retry loads the existing node rather than creating another merge.
        if(string.IsNullOrEmpty(state.completedHead))
        {
            SaveWorkspace(); state.attempted=true;
            var body=MergeRequest("merge_commit"); body.message=state.commitMessage;
            Response result=null; yield return Remote(body,response=>result=response);
            if(string.IsNullOrEmpty(result.head)||string.IsNullOrEmpty(result.branch)) throw new IOException("Invalid merge result");
            state.completedHead=result.head; state.completedBranch=result.branch;
        }
        yield return RefreshRoutine();
        yield return LoadBranch(state.completedBranch,false,state.completedHead);
        mergeSession=null; mergeAnchor="";
        status="已创建新的双父合并节点并载入；两个原节点和之前的本地工作副本均保留。";
    }

    // Drive nested iterators here so network, disk and callback failures always
    // release the busy state and leave a usable panel.
    IEnumerator Run(IEnumerator routine, string label)
    {
        if (busy) yield break;
        busy = true; operationLabel = label; Render();
        var stack = new Stack<IEnumerator>(); stack.Push(routine);
        try
        {
            while (stack.Count > 0)
            {
                object next = null; bool more = false; Exception failure = null;
                try { more = stack.Peek().MoveNext(); if (more) next = stack.Peek().Current; }
                catch (Exception error) { failure = error; }
                if (failure != null) { status = failure.Message; break; }
                if (!more) { (stack.Pop() as IDisposable)?.Dispose(); continue; }
                if (next is IEnumerator nested) stack.Push(nested);
                else yield return next;
            }
        }
        finally
        {
            while (stack.Count > 0) (stack.Pop() as IDisposable)?.Dispose();
            busy = false; operationLabel = ""; Render();
        }
    }
    void ValidateConnection()
    {
        if (!CollaborationIdentity.IsValidUserId(userId)) throw new ArgumentException("A valid user ID is required.");
        if (string.IsNullOrWhiteSpace(token) || token.Any(c => c < 33 || c > 126)) throw new ArgumentException("A printable ASCII user token is required.");
        string address = server.Trim();
        if (!address.Contains("://")) address = "http://" + address;
        Uri uri;
        if (!Uri.TryCreate(address, UriKind.Absolute, out uri) || string.IsNullOrEmpty(uri.Host)
            || !string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment)
            || (uri.Scheme != "https" && uri.Scheme != "http"))
            throw new ArgumentException("Use an HTTP or HTTPS server address without credentials, query or fragment.");
        server = uri.AbsoluteUri.TrimEnd('/');
        if (!Regex.IsMatch(project, @"\A[A-Za-z0-9][A-Za-z0-9_.-]{0,63}\z")) throw new ArgumentException("Invalid project ID.");
    }
    Request MakeRequest(string operation, string target) => new Request
    {
        operation = operation, project = project, userId = userId, branch = target,
        expectedHead = Matches(checkout, target) ? checkout.head : ""
    };
    IEnumerator Remote(Request body, Action<Response> completed)
    {
        using (var request = new UnityWebRequest(server + "/v1/repository", "POST"))
        {
            request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(JsonUtility.ToJson(body)));
            request.downloadHandler = new DownloadHandlerBuffer(); request.timeout = 120;
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Authorization", "Bearer " + token);
            yield return request.SendWebRequest();
            Response response = null;
            try { response = JsonUtility.FromJson<Response>(request.downloadHandler.text); } catch (ArgumentException) { }
            if (request.result != UnityWebRequest.Result.Success || response == null || !response.success)
            {
                string detail = response != null && !string.IsNullOrEmpty(response.error) ? response.error : request.error;
                if (body.operation == "submit" && response?.error == "Unknown operation")
                    detail = "Update and restart masterserver to enable automatic node submissions.";
                if (response?.conflicts != null && response.conflicts.Length > 0) detail = "Merge conflicts:\n" + string.Join("\n", response.conflicts);
                throw new IOException(detail);
            }
            completed(response);
        }
    }
    IEnumerator ConnectRoutine()
    {
        ValidateConnection();
        Response response = null;
        yield return Remote(MakeRequest("connect", "main"), value => response = value);
        connected = true; branches = response.branches ?? new Branch[0];
        CollaborationIdentity.SetUserId(userId);
        PlayerPrefs.SetString("rwrme.collaboration.server", server); PlayerPrefs.SetString("rwrme.collaboration.project", project); PlayerPrefs.Save();
        string path = StatePath(MapRoot);
        checkout = File.Exists(path) ? JsonUtility.FromJson<Checkout>(File.ReadAllText(path)) : null;
        branch = checkout != null && checkout.origin == server && checkout.project == project ? checkout.branch : "main";
        selectedBranch = branches.Any(b => b.name == branch) ? branch : "main";
        selectedNode = IsDetached;
        selectedRevision = IsDetached ? CurrentHead : branches.FirstOrDefault(b => b.name == selectedBranch)?.head ?? "";
        yield return HistoryRoutine(false);
        status = "Connected: " + userId;
        if (view != null) view.HideConnection();
    }
    IEnumerator RefreshRoutine()
    {
        yield return Remote(MakeRequest("connect", "main"), value => branches = value.branches ?? new Branch[0]);
        if (!branches.Any(b => b.name == selectedBranch)) selectedBranch = branches.FirstOrDefault()?.name ?? "main";
        yield return HistoryRoutine(false);
        status = "节点记录已刷新。";
    }
    IEnumerator HistoryRoutine(bool append)
    {
        var body = MakeRequest("history", "main"); body.limit = 200; body.cursor = append ? historyCursor : "0";
        yield return Remote(body, response =>
        {
            var incoming = response.commits ?? new Commit[0];
            commits = (append ? commits.Concat(incoming) : incoming).GroupBy(c => c.id).Select(g => g.First()).ToArray();
            historyCursor = response.nextCursor ?? "";
            if (response.branches != null) branches = response.branches;
        });
    }
    bool Matches(Checkout state, string name) => state != null && state.origin == server && state.project == project && state.branch == name;
    string WorkspacePath(string name) => Path.Combine(Application.persistentDataPath, "collaboration", "workspaces",
        Hash(Encoding.UTF8.GetBytes(MapRoot + "\n" + server + "\n" + project)), Hash(Encoding.UTF8.GetBytes(name)) + ".json");
    void StoreWorkspace(Workspace state) => CollaborationWorkspaceStore.Save(WorkspacePath(CollaborationWorkspaceStore.Key(state.checkout)), state);
    Workspace LoadWorkspace(string name, string revision)
    {
        string path = WorkspacePath(CollaborationWorkspaceStore.Key(name, revision));
        if (!File.Exists(path)) return null;
        var cached = CollaborationWorkspaceStore.Load(path);
        return cached != null && Matches(cached.checkout, name) && cached.files != null ? cached : null;
    }
    Workspace SaveWorkspace()
    {
        if (reloadRequired) throw new IOException("Reload the downloaded map before saving.");
        SaveCurrentMap();
        var files = ReadFiles(MapRoot);
        if (!files.Any(f => f.path == "objects.svg")) throw new IOException("Map snapshot must include objects.svg.");
        var state = Matches(checkout, branch) ? checkout : new Checkout { origin = server, project = project, branch = branch, head = "", digest = "" };
        checkout = state;
        var workspace = new Workspace { checkout = state, files = files }; StoreWorkspace(workspace); return workspace;
    }
    void RequirePublishBase()
    {
        if (!CanSubmit) throw new IOException("Load a node before submitting. Your local map is preserved in its workspace.");
    }
    IEnumerator PublishFiles(MapFile[] files, string message)
    {
        RequirePublishBase();
        var previous = checkout;
        var body = MakeRequest("submit", branch); body.baseRevision = CurrentReady ? checkout.head : "";
        body.files = files; body.message = message;
        Response submitted = null;
        yield return Remote(body, response => submitted = response);
        if (string.IsNullOrEmpty(submitted.branch) || string.IsNullOrEmpty(submitted.head) || submitted.baseRevision != body.baseRevision)
            throw new IOException("Masterserver returned an invalid submission result. Update the masterserver to support automatic forks.");
        branch = selectedBranch = submitted.branch; selectedRevision = submitted.head; selectedNode = false;
        SaveState(MapRoot, server, project, branch, submitted.head, files);
        StoreWorkspace(new Workspace { checkout = checkout, files = files });
        // The source snapshot now exists on the server. Do not later restore it
        // as pending edits on the old workline; other workspaces remain untouched.
        if (previous != null && CollaborationWorkspaceStore.Key(previous) != CollaborationWorkspaceStore.Key(checkout))
            StoreWorkspace(new Workspace { checkout = previous, files = files, submitted = true });
        status = submitted.forked ? "提交完成：基础节点已有后续提交，已自动分叉并定位到新节点。" : "提交完成，已定位到新节点。";
    }
    IEnumerator PublishRoutine(string message)
    {
        var workspace = SaveWorkspace();
        yield return PublishFiles(workspace.files, message);
        string outcome = status;
        yield return RefreshRoutine(); status = outcome;
    }
    IEnumerator SwitchRoutine(string target, bool forceRemote, string revision = null)
    {
        SaveWorkspace();
        yield return LoadBranch(target, forceRemote, revision);
    }
    IEnumerator LoadBranch(string target, bool forceRemote, string revision = null)
    {
        Response response = null;
        var body = MakeRequest("pull", target); body.revision = revision;
        yield return Remote(body, value => response = value);
        bool detached = !string.IsNullOrEmpty(revision);
        string actualRevision = response.revision ?? response.head;
        var cached = forceRemote ? null : LoadWorkspace(target, detached ? actualRevision : null);
        bool restore = CollaborationWorkspaceStore.ShouldRestore(cached, forceRemote);
        response.head = actualRevision;
        if (restore) { response.files = cached.files; response.head = cached.checkout.head; }
        if (response.files == null || response.files.Length == 0)
        {
            response.files = ReadFiles(MapRoot);
            response.head = "";
        }
        ApplyPull(MapRoot, server, body, response);
        branch = target; selectedBranch = target; selectedRevision = response.head ?? ""; selectedNode = detached;
        if (restore) checkout = cached.checkout;
        else if (string.IsNullOrEmpty(response.head)) { checkout.digest = ""; checkout.known = true; }
        checkout.detached = detached; WriteCheckout();
        StoreWorkspace(new Workspace { checkout = checkout, files = response.files });
        status = (restore ? "Restored local changes: " : "Loaded: ") + target + (detached ? " @ " + response.head.Substring(0, 12) : "");
        ReloadCurrentScene();
    }
    void WriteCheckout()
    {
        string path = StatePath(MapRoot); Directory.CreateDirectory(Path.GetDirectoryName(path)); File.WriteAllText(path, JsonUtility.ToJson(checkout));
    }
    IEnumerator CreateRoutine(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Enter a branch name.");
        string baseRevision = selectedRevision;
        Workspace carry = CurrentReady && CurrentHead == baseRevision ? SaveWorkspace() : null;
        var body = MakeRequest("create_branch", selectedBranch); body.newBranch = name; body.revision = baseRevision;
        yield return Remote(body, response => { });
        if (carry != null)
        {
            var state = JsonUtility.FromJson<Checkout>(JsonUtility.ToJson(carry.checkout));
            state.branch = name; state.detached = false;
            StoreWorkspace(new Workspace { checkout = state, files = carry.files });
        }
        yield return RefreshRoutine();
        yield return SwitchRoutine(name, false);
    }
    IEnumerator MergeRoutine(string source, string message)
    {
        if (IsDetached) throw new IOException("Return to a working branch before merging.");
        if (source == branch) throw new ArgumentException("Select another branch to merge.");
        var workspace = SaveWorkspace();
        if (!CurrentReady || Digest(workspace.files) != checkout.digest) yield return PublishFiles(workspace.files, message);
        var body = MakeRequest("merge", branch); body.sourceBranch = source;
        yield return Remote(body, response => { });
        yield return RefreshRoutine();
        yield return LoadBranch(branch, true);
        status = "Merged " + source + " into " + branch;
    }
    public void ReloadCurrentScene()
    {
        try
        {
            var scene = SceneManager.GetActiveScene();
            reloadRequired = false;
#if UNITY_EDITOR
            if (scene.buildIndex < 0)
            {
                UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(scene.path, new LoadSceneParameters(LoadSceneMode.Single));
                return;
            }
#endif
            SceneManager.LoadScene(scene.buildIndex);
        }
        catch { reloadRequired = true; throw; }
    }

    string MapRoot
    {
        get
        {
            if (MapImporter.instate == null || MapExporter.ins == null) throw new InvalidOperationException("Map editor is not ready.");
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, MapImporter.instate.basePath));
            string exportRoot = Path.GetFullPath(Path.Combine(Application.dataPath, MapExporter.ins.basePath));
            string assets = Path.GetFullPath(Application.dataPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (!root.StartsWith(assets + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(root, exportRoot, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Importer/exporter must use the same map subdirectory inside Assets.");
            return root;
        }
    }
    static string Hash(byte[] bytes)
    {
        using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
    }
    string StatePath(string root) => Path.Combine(Application.persistentDataPath, "collaboration", Hash(Encoding.UTF8.GetBytes(root)) + ".json");
    static bool ValidPath(string path)
    {
        return !string.IsNullOrEmpty(path) && path.Length <= 240 && !path.Contains("\\")
            && !path.EndsWith(".meta", StringComparison.OrdinalIgnoreCase)
            && path.Split('/').All(p => p.Length > 0 && !p.StartsWith(".") && !p.EndsWith(".") && !p.EndsWith(" ")
                && p.IndexOfAny(":<>\"|?*".ToCharArray()) < 0 && !p.Any(c => c < 32)
                && !Regex.IsMatch(p.Split('.')[0], @"\A(CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])\z", RegexOptions.IgnoreCase));
    }
    static void RejectLinks(string root)
    {
        if ((File.GetAttributes(root) & FileAttributes.ReparsePoint) != 0) throw new IOException("Linked map directories are not supported.");
        foreach (string entry in Directory.GetFileSystemEntries(root))
        {
            if ((File.GetAttributes(entry) & FileAttributes.ReparsePoint) != 0) throw new IOException("Linked map entries are not supported.");
            if (Directory.Exists(entry)) RejectLinks(entry);
        }
    }
    static MapFile[] ReadFiles(string root)
    {
        RejectLinks(root);
        long total = 0;
        var result = new List<MapFile>();
        foreach (string file in Directory.GetFiles(root, "*", SearchOption.AllDirectories).OrderBy(f => f, StringComparer.Ordinal))
        {
            if (file.EndsWith(".meta", StringComparison.OrdinalIgnoreCase)) continue;
            string path = file.Substring(root.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Replace('\\', '/');
            if (!ValidPath(path)) throw new IOException("Unsupported map file path: " + path);
            total += new FileInfo(file).Length;
            if (total > MaxBytes || result.Count >= 4096) throw new IOException("Snapshot exceeds 64 MiB or 4096 files.");
            result.Add(new MapFile { path = path, content = Convert.ToBase64String(File.ReadAllBytes(file)) });
        }
        return result.ToArray();
    }
    static string Digest(MapFile[] files) => CollaborationWorkspaceStore.Digest(files);
    void SaveState(string root, string endpoint, string projectId, string target, string revision, MapFile[] files)
    {
        checkout = new Checkout { origin = endpoint, project = projectId, branch = target, head = revision, digest = Digest(files), known = true };
        string path = StatePath(root); Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllText(path, JsonUtility.ToJson(checkout));
    }
    static void SaveCurrentMap()
    {
        string error = null;
        Application.LogCallback capture = (text, stack, type) => { if (type == LogType.Error || type == LogType.Exception) error = text; };
        Application.logMessageReceived += capture;
        try { MapExporter.ins.exportMap(); }
        finally { Application.logMessageReceived -= capture; }
        if (error != null) throw new IOException("Map export failed: " + error);
    }

    void ApplyPull(string root, string endpoint, Request request, Response response)
    {
        var files = response.files ?? new MapFile[0];
        if (files.Length == 0) throw new IOException("Empty branch: save and push your map first.");
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        long total = 0;
        foreach (var file in files)
        {
            if (!ValidPath(file.path) || !names.Add(file.path)) throw new IOException("Invalid remote snapshot path.");
            total += Convert.FromBase64String(file.content).LongLength;
            if (total > MaxBytes || files.Length > 4096) throw new IOException("Remote snapshot exceeds client limit.");
        }
        foreach (string name in names)
        {
            string[] parts = name.Split('/');
            for (int i = 1; i < parts.Length; i++)
                if (names.Contains(string.Join("/", parts.Take(i)))) throw new IOException("Remote file/directory conflict.");
        }
        if (!names.Contains("objects.svg")) throw new IOException("Remote snapshot has no objects.svg.");
        var current = ReadFiles(root);
        foreach (var remote in files)
            foreach (var local in current)
                if (remote.path.StartsWith(local.path + "/", StringComparison.OrdinalIgnoreCase)
                    || local.path.StartsWith(remote.path + "/", StringComparison.OrdinalIgnoreCase))
                    throw new IOException("File/directory replacement requires moving the local map file first: " + local.path);
        string backup = Path.Combine(Application.persistentDataPath, "collaboration", "backups", DateTime.UtcNow.ToString("yyyyMMddHHmmss") + "-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(backup);
        foreach (var file in current) WriteFile(backup, file);
        // Everything is validated before touching the working directory. Roll back any partial file writes.
        try
        {
            foreach (var file in files) WriteFile(root, file);
            foreach (var file in current.Where(f => !names.Contains(f.path))) File.Delete(Path.Combine(root, file.path));
            SaveState(root, endpoint, request.project, request.branch, response.head, files);
        }
        catch
        {
            foreach (var file in files.Where(f => !current.Any(c => c.path.Equals(f.path, StringComparison.OrdinalIgnoreCase))))
                if (File.Exists(Path.Combine(root, file.path))) File.Delete(Path.Combine(root, file.path));
            foreach (var file in current) WriteFile(root, file);
            throw;
        }
        reloadRequired = true;
        status = "拉取完成，请重新载入当前地图。原文件备份：" + backup;
    }
    static void WriteFile(string root, MapFile file)
    {
        string path = Path.Combine(root, file.path);
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllBytes(path, Convert.FromBase64String(file.content));
    }
}
