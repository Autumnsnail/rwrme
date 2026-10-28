#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class CollaborationUiSmokeTest
{
    public static string Run()
    {
        TestWorkspaces();
        TestGraph();
        var current = SceneManager.GetActiveScene(); bool dirty = current.isDirty;
        var selection = Selection.objects;
        var scene = EditorSceneManager.NewPreviewScene();
        RenderTexture target = null; Texture2D image = null; Camera previewCamera = null;
        try
        {
            var prefab = AssetDatabase.LoadAssetAtPath<CollaborationView>("Assets/Resources/UI/CollaborationPanel.prefab");
            if (prefab == null) throw new Exception("Missing collaboration prefab.");
            foreach (var field in typeof(CollaborationView).GetFields(BindingFlags.Public | BindingFlags.Instance))
                if (typeof(UnityEngine.Object).IsAssignableFrom(field.FieldType) && field.GetValue(prefab) == null) throw new Exception("Unbound prefab field: " + field.Name);
            var cameraGo = new GameObject("UI test camera", typeof(Camera)); SceneManager.MoveGameObjectToScene(cameraGo, scene);
            var camera = cameraGo.GetComponent<Camera>(); previewCamera = camera; camera.orthographic = true; camera.orthographicSize = 540; camera.transform.position = new Vector3(0, 0, -10); camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.13f, .14f, .15f, 1); camera.cullingMask = 1 << 5; camera.overrideSceneCullingMask = EditorSceneManager.GetSceneCullingMask(scene);
            var light = new GameObject("UI test light", typeof(Light)); SceneManager.MoveGameObjectToScene(light, scene); light.GetComponent<Light>().type = LightType.Directional;
            var canvasGo = new GameObject("OnlyUI test", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster)); SceneManager.MoveGameObjectToScene(canvasGo, scene);
            var canvas = canvasGo.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1;
            CoverUiLayout.ApplySharedCanvasScaler(canvasGo.GetComponent<CanvasScaler>());
            var originalUi = UnityEngine.Resources.FindObjectsOfTypeAll<UIManager>().FirstOrDefault(ui => ui.gameObject.scene == current);
            if (originalUi != null)
            {
                var names = new[] { "ButtonRefpManager", "Button3rdManager", "ButtonGeneralSettingsManager" };
                for (int i=0;i<names.Length;i++)
                {
                    var originalButton=originalUi.transform.Find(names[i]);
                    if(originalButton==null)continue;
                    var button=UnityEngine.Object.Instantiate(originalButton.gameObject,canvas.transform,false);
                    CoverUiLayout.ApplyLeftBottomButton(button.transform as RectTransform,i);
                }
            }
            var view = UnityEngine.Object.Instantiate(prefab, canvas.transform, false);
            var sessionGo = new GameObject("Test collaboration session"); SceneManager.MoveGameObjectToScene(sessionGo, scene); var session = sessionGo.AddComponent<CollaborationPanel>();
            Set(session, "server", "http://127.0.0.1:8090"); Set(session, "project", "greatcliff"); Set(session, "userId", "alice");
            Set(session, "branch", "main"); Set(session, "selectedBranch", "main"); Set(session, "visible", true); Set(session, "connected", true); Set(session, "view", view);
            Set(session, "checkout", new CollaborationPanel.Checkout { origin = "http://127.0.0.1:8090", project = "greatcliff", branch = "main", head = new string('a', 64), digest = "test", known = true });
            Set(session, "branches", new[] {
                new CollaborationPanel.Branch { name="main", parent="", head=new string('a',64) },
                new CollaborationPanel.Branch { name="users/alice", parent="main", head=new string('b',64) },
                new CollaborationPanel.Branch { name="feature/road", parent="users/alice", head=new string('c',64) },
                new CollaborationPanel.Branch { name="feature/cliff", parent="users/alice", head=new string('d',64) },
                new CollaborationPanel.Branch { name="users/bob", parent="main", head=new string('e',64) },
                new CollaborationPanel.Branch { name="props/harbor", parent="users/bob", head=new string('f',64) }
            });
            Set(session,"selectedRevision",new string('a',64));
            Set(session,"commits",FixtureCommits());
            view.Bind(session);
            if(view.WorklinesVisible || view.branchSidebar.activeSelf || !view.mergeButton.gameObject.activeSelf || view.details.offsetMin.x!=20)
                throw new Exception("Default UI must focus on nodes without branch management.");
            if(view.commitGraph.content.GetComponentsInChildren<CollaborationCommitNode>().Any(n=>n.stateLabel.text.Contains("main")||n.stateLabel.text.Contains("users/")))
                throw new Exception("Default graph must hide internal workline names.");
            view.branchToolsButton.onClick.Invoke();
            if(!view.WorklinesVisible || !view.branchSidebar.activeSelf || view.details.offsetMin.x!=324) throw new Exception("Optional workline management did not expand.");
            var rows = view.treeContent.GetComponentsInChildren<CollaborationBranchRow>().Where(r => r.gameObject.activeInHierarchy).ToArray();
            if (rows.Length != 6) throw new Exception("Tree did not render all branches.");
            rows.Single(r => r.nameLabel.text == "main").disclosureButton.onClick.Invoke();
            if (view.treeContent.GetComponentsInChildren<CollaborationBranchRow>().Length != 1) throw new Exception("Branch collapse failed.");
            view.treeContent.GetComponentsInChildren<CollaborationBranchRow>().Single().disclosureButton.onClick.Invoke();
            rows = view.treeContent.GetComponentsInChildren<CollaborationBranchRow>();
            rows.Single(r => r.nameLabel.text == "users/bob").selectButton.onClick.Invoke();
            if (session.SelectedBranch != "users/bob" || session.CurrentBranch != "main" || !view.switchButton.interactable) throw new Exception("Selection changed the working branch or failed to enable switching.");
            view.switchButton.onClick.Invoke();
            if (!view.confirmationDialog.activeSelf) throw new Exception("Switch confirmation is not wired.");
            view.confirmationCancelButton.onClick.Invoke();
            view.createButton.onClick.Invoke();
            if (!view.createDialog.activeSelf || !view.createFromLabel.text.Contains(new string('e',10))) throw new Exception("Create branch parent was lost.");
            view.createCancelButton.onClick.Invoke();
            view.connectionButton.onClick.Invoke();
            if (!view.connectionDialog.activeSelf || view.serverInput.text != session.Server || view.tokenInput.contentType != TMP_InputField.ContentType.Password) throw new Exception("Connection settings are not bound.");
            view.connectionCloseButton.onClick.Invoke();
            view.searchInput.text = "harbor";
            if (view.treeContent.GetComponentsInChildren<CollaborationBranchRow>().Length != 3) throw new Exception("Filtered tree must retain ancestors.");
            view.searchInput.text = "";
            if (view.launcher.GetComponent<RectTransform>().anchoredPosition != new Vector2(12,252)) throw new Exception("Launcher does not match left toolbar.");
            var nodes=view.commitGraph.content.GetComponentsInChildren<CollaborationCommitNode>();
            if(nodes.Length!=9 || view.commitGraph.lines.edges.Count!=9) throw new Exception("Commit graph or merge edges missing.");
            var historic=nodes.Single(n=>n.titleLabel.text=="Initial map"); historic.selectButton.onClick.Invoke();
            if(session.SelectedRevision!=new string('1',64) || session.CurrentHead!=new string('a',64) || !view.switchButton.interactable) throw new Exception("Historical selection changed checkout.");
            historic.selectButton.onClick.Invoke();
            if(!view.confirmationDialog.activeSelf || !view.confirmationText.text.Contains(new string('1',10))) throw new Exception("Double-click node jump is not wired.");
            view.confirmationCancelButton.onClick.Invoke();
            view.createButton.onClick.Invoke();
            if(!view.createFromLabel.text.Contains(new string('1',10))) throw new Exception("Create dialog lost selected revision.");
            view.createCancelButton.onClick.Invoke();
            var detached=new CollaborationPanel.Checkout {origin=session.Server,project=session.Project,branch="main",head=new string('1',64),known=true,detached=true};
            Set(session,"checkout",detached); view.Refresh(session);
            if(!view.publishButton.interactable || !view.mergeButton.interactable || !view.updateButton.interactable || !view.createButton.interactable) throw new Exception("Historical nodes must support direct automatic submissions.");
            Set(session,"checkout",new CollaborationPanel.Checkout {origin=session.Server,project=session.Project,branch="main",head=new string('a',64),known=true});
            session.SelectBranch("main");
            view.branchToolsButton.onClick.Invoke();
            if(view.WorklinesVisible || view.details.offsetMin.x!=20) throw new Exception("Workline tools did not collapse.");
            var uiFont = view.selectionLabel.font;
            if (!uiFont.HasCharacters("自动分叉续接提交基础工作线节点历史协作远程项目连接")) throw new Exception("Chinese UI glyphs are missing.");
            foreach (var item in canvasGo.GetComponentsInChildren<Transform>(true)) item.gameObject.layer = 5;
            target = new RenderTexture(1600, 900, 24); camera.targetTexture = target;
            Canvas.ForceUpdateCanvases(); foreach (var label in view.GetComponentsInChildren<TMP_Text>()) label.ForceMeshUpdate();
            camera.Render(); var previous = RenderTexture.active; RenderTexture.active = target;
            image = new Texture2D(1600,900,TextureFormat.RGB24,false); image.ReadPixels(new Rect(0,0,1600,900),0,0); image.Apply(); RenderTexture.active = previous;
            Directory.CreateDirectory("artifacts"); File.WriteAllBytes("artifacts/collaboration-ui.png", image.EncodeToPNG());
            TestMergeDialog(view,session);
            Canvas.ForceUpdateCanvases(); foreach (var label in view.GetComponentsInChildren<TMP_Text>()) label.ForceMeshUpdate();
            camera.Render(); previous=RenderTexture.active; RenderTexture.active=target; image.ReadPixels(new Rect(0,0,1600,900),0,0);image.Apply();RenderTexture.active=previous;
            File.WriteAllBytes("artifacts/collaboration-merge.png",image.EncodeToPNG());
            return "Visual merge references, anchor marking, three-way comparison, PNG preview, custom resolution encoding, unresolved commit guard, ready commit, safe cancellation, plus prefab references, 6 branches, 9 commit nodes, merge edges, historical double-click jump, direct historical submission, collapsed workline tools, hidden internal names, consumed workspace isolation, toolbar and CJK font passed. Preview: artifacts/collaboration-ui.png and artifacts/collaboration-merge.png";
        }
        finally
        {
            if (previewCamera != null) previewCamera.targetTexture = null;
            if (target != null) { target.Release(); UnityEngine.Object.DestroyImmediate(target); }
            if (image != null) UnityEngine.Object.DestroyImmediate(image);
            EditorSceneManager.ClosePreviewScene(scene); SceneManager.SetActiveScene(current); Selection.objects = selection;
            if (current.isDirty != dirty) throw new Exception("Smoke test changed the user's scene dirty state.");
        }
    }
    static void TestMergeDialog(CollaborationView view,CollaborationPanel session)
    {
        foreach(var field in typeof(CollaborationMergeDialog).GetFields(BindingFlags.Public|BindingFlags.Instance))
            if(typeof(UnityEngine.Object).IsAssignableFrom(field.FieldType)&&field.GetValue(view.mergeDialog)==null)throw new Exception("Unbound merge field: "+field.Name);
        session.SelectRevision(new string('c',64));view.mergeButton.onClick.Invoke();
        if(session.MergeAnchor!=new string('c',64)||!view.commitGraph.content.GetComponentsInChildren<CollaborationCommitNode>().Any(n=>n.stateLabel.text.Contains("合并 A")))throw new Exception("Merge A was not marked on graph.");
        view.mergeButton.onClick.Invoke();if(!string.IsNullOrEmpty(session.MergeAnchor))throw new Exception("Merge marking did not cancel.");
        var state=new CollaborationMergeSession(new string('c',64),new string('f',64));
        var first=new CollaborationMergeConflict {key="element:objects.svg#building42rwrme",path="objects.svg",element="building42rwrme",kind="element"};
        var png=new CollaborationMergeConflict {key="file:road.png",path="road.png",element="",kind="file"};
        var plan=new CollaborationMergePlan {left=state.left,right=state.right,ancestor=new string('3',64),previewId="test",unresolved=2,automaticFiles=7,fileCount=9,conflicts=new[]{first,png}};
        state.Accept(plan);
        Func<int,CollaborationMergeValue> element=x=>new CollaborationMergeValue {exists=true,editable=true,parent="layerObjects",digest=x.ToString(),text="<rect id=\"building42rwrme\" x=\""+x+"\" y=\"80\" width=\"48\" height=\"32\" />",fields=new[]{new CollaborationMergeField{name="type",value="rect"},new CollaborationMergeField{name="layer",value="layerObjects"},new CollaborationMergeField{name="id",value="building42rwrme"},new CollaborationMergeField{name="x",value=x.ToString()},new CollaborationMergeField{name="y",value="80"}}};
        var detail=new CollaborationMergeDetail {key=first.key,canEdit=true,canDelete=true,ancestor=element(80),left=element(100),right=element(140)};
        state.detail=detail;Set(session,"mergeSession",state);Set(session,"mergeAnchor",state.left);view.Refresh(session);
        var dialog=view.mergeDialog;
        if(!dialog.gameObject.activeSelf||dialog.commitButton.interactable||!dialog.leftButton.interactable||!dialog.rightButton.interactable||!dialog.leftText.text.Contains("100"))throw new Exception("Merge conflict review or commit guard failed.");
        dialog.customButton.onClick.Invoke();if(!dialog.customArea.activeSelf||!dialog.customText.text.Contains("building42rwrme"))throw new Exception("Manual conflict editor did not open.");
        state.Choose("custom","<rect id=\"building42rwrme\" x=\"120\"/>","layerObjects");
        if(!System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(state.Resolutions.Single().content)).Contains("120")||state.validated)throw new Exception("Manual resolution encoding or invalidation failed.");
        state.Reset();first.resolved=png.resolved=true;plan.unresolved=0;state.Accept(plan);view.Refresh(session);
        if(!dialog.commitButton.interactable)throw new Exception("Resolved merge did not enable creation.");
        var texture=new Texture2D(2,2);texture.SetPixels(new[]{Color.red,Color.green,Color.blue,Color.white});texture.Apply();string data=Convert.ToBase64String(texture.EncodeToPNG());UnityEngine.Object.DestroyImmediate(texture);
        state.selectedKey=png.key;state.detail=new CollaborationMergeDetail {key=png.key,canEdit=false,canDelete=true,ancestor=new CollaborationMergeValue{exists=true,image=data,digest="base"},left=new CollaborationMergeValue{exists=true,image=data,digest="a"},right=new CollaborationMergeValue{exists=true,image=data,digest="b"}};view.Refresh(session);
        if(!dialog.leftImage.gameObject.activeSelf||dialog.leftImage.texture==null||dialog.customButton.interactable)throw new Exception("PNG conflict preview failed.");
        dialog.closeButton.onClick.Invoke();if(session.MergeSession!=null||dialog.gameObject.activeSelf)throw new Exception("Merge cancellation failed.");
        first.resolved=png.resolved=false;plan.unresolved=2;state=new CollaborationMergeSession(plan.left,plan.right);state.Accept(plan);state.detail=detail;Set(session,"mergeSession",state);Set(session,"mergeAnchor",state.left);Set(session,"status","请在合并面板中解决冲突。");view.Refresh(session);
    }
    static CollaborationPanel.Commit[] FixtureCommits()
    {
        Func<char,string> id=c=>new string(c,64);
        return new[] {
            new CollaborationPanel.Commit {id=id('a'),parents=new[]{id('2'),id('c')},author="alice",message="Merge road into main",created=1800000900},
            new CollaborationPanel.Commit {id=id('f'),parents=new[]{id('e')},author="bob",message="Harbor objects",created=1800000800},
            new CollaborationPanel.Commit {id=id('d'),parents=new[]{id('b')},author="alice",message="Cliff platform",created=1800000700},
            new CollaborationPanel.Commit {id=id('c'),parents=new[]{id('b')},author="alice",message="Road terrain",created=1800000600},
            new CollaborationPanel.Commit {id=id('2'),parents=new[]{id('1')},author="alice",message="Base terrain",created=1800000500},
            new CollaborationPanel.Commit {id=id('e'),parents=new[]{id('3')},author="bob",message="Add props",created=1800000400},
            new CollaborationPanel.Commit {id=id('b'),parents=new[]{id('3')},author="alice",message="Start terrain work",created=1800000300},
            new CollaborationPanel.Commit {id=id('3'),parents=new[]{id('1')},author="alice",message="Shared map layout",created=1800000200},
            new CollaborationPanel.Commit {id=id('1'),parents=new string[0],author="alice",message="Initial map",created=1800000100}
        };
    }
    public static string TestGraph()
    {
        var layout=CollaborationCommitGraphModel.Build(FixtureCommits());
        if(layout.rows.Count!=9 || layout.edges.Count!=9 || layout.lanes<2) throw new Exception("Fork/merge topology was lost.");
        if(layout.edges.Any(e=>e.parent.index<=e.child.index)) throw new Exception("Parent rendered before child.");
        if(layout.edges.Count(e=>e.child.commit.id==new string('a',64))!=2) throw new Exception("Merge needs two parent edges.");
        var incomplete=CollaborationCommitGraphModel.Build(FixtureCommits().Take(4));
        if(incomplete.rows.Count!=4 || incomplete.edges.Count!=1) throw new Exception("Paged/incomplete history failed.");
        var working=new CollaborationPanel.Checkout {branch="main",head="a",detached=false};
        var history=new CollaborationPanel.Checkout {branch="main",head="a",detached=true};
        if(CollaborationWorkspaceStore.Key(working)==CollaborationWorkspaceStore.Key(history)
            || CollaborationWorkspaceStore.Key(history)==CollaborationWorkspaceStore.Key("main","b")) throw new Exception("Historical workspaces overlap.");
        return "Graph topology, fork lanes, merge parents, partial pages and historical workspace keys passed.";
    }
    static void TestWorkspaces()
    {
        string directory = Path.Combine(Path.GetTempPath(), "rwrme-workspace-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var original = new[] { new CollaborationPanel.MapFile { path = "objects.svg", content = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("base")) } };
            var modified = new[] { new CollaborationPanel.MapFile { path = "objects.svg", content = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("unpublished")) } };
            string baseline = CollaborationWorkspaceStore.Digest(original);
            var a = new CollaborationPanel.Workspace { checkout = new CollaborationPanel.Checkout { branch = "a", digest = baseline, head = "base-a", known = true }, files = modified };
            var b = new CollaborationPanel.Workspace { checkout = new CollaborationPanel.Checkout { branch = "b", digest = baseline, head = "base-b", known = true }, files = original };
            string pathA = Path.Combine(directory,"a.json"), pathB = Path.Combine(directory,"b.json");
            CollaborationWorkspaceStore.Save(pathA,a); CollaborationWorkspaceStore.Save(pathB,b);
            a = CollaborationWorkspaceStore.Load(pathA); b = CollaborationWorkspaceStore.Load(pathB);
            if (a.checkout.head != "base-a" || b.checkout.head != "base-b") throw new Exception("Branch baselines were mixed.");
            if (!CollaborationWorkspaceStore.ShouldRestore(a,false) || CollaborationWorkspaceStore.ShouldRestore(b,false)
                || CollaborationWorkspaceStore.ShouldRestore(a,true)) throw new Exception("Local change restore policy failed.");
            CollaborationWorkspaceStore.Save(pathA,a);
            if (CollaborationWorkspaceStore.Load(pathB).checkout.head != "base-b" || Directory.GetFiles(directory,"*.new").Length!=0) throw new Exception("Atomic workspace update failed.");
            a.submitted=true; CollaborationWorkspaceStore.Save(pathA,a);
            if(CollaborationWorkspaceStore.ShouldRestore(CollaborationWorkspaceStore.Load(pathA),false)) throw new Exception("Already submitted edits were restored as pending changes.");
            if(CollaborationWorkspaceStore.Load(pathB).checkout.head!="base-b") throw new Exception("Submitting a historical workspace changed another workspace.");
            var tree = CollaborationBranchTree.Build(new[] { new CollaborationPanel.Branch { name="a",parent="b" },new CollaborationPanel.Branch { name="b",parent="a" },new CollaborationPanel.Branch { name="orphan",parent="missing" } },new System.Collections.Generic.HashSet<string>(),"");
            if (tree.Count!=3) throw new Exception("Legacy/orphan branch tree failed.");
        }
        finally { foreach (string file in Directory.GetFiles(directory)) File.Delete(file); Directory.Delete(directory); }
    }
    static void Set(object target, string field, object value) { target.GetType().GetField(field, BindingFlags.NonPublic|BindingFlags.Instance).SetValue(target,value); }
}
#endif
