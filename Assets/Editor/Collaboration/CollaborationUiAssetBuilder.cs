#if UNITY_EDITOR
using System;
using System.Linq;
using System.IO;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine.TextCore;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

/// <summary>Rebuilds serialized UI assets through Unity APIs without saving the user's scene.</summary>
public static class CollaborationUiAssetBuilder
{
    const string DirectoryPath = "Assets/Resources/UI";
    const string PrefabPath = DirectoryPath + "/CollaborationPanel.prefab";
    static TMP_FontAsset font;
    static Sprite sprite;
    static readonly Color Panel = new Color(.34f, .338f, .338f, 1);
    static readonly Color Dark = new Color(.25f, .25f, .25f, 1);
    static readonly Color Accent = new Color(.25f, .45f, .7f, 1);

    [MenuItem("Tools/Rwrme/Rebuild Collaboration UI")]
    public static string Build()
    {
        var current = SceneManager.GetActiveScene();
        var selection = Selection.objects;
        var existing = UnityEngine.Resources.FindObjectsOfTypeAll<UIManager>().FirstOrDefault(ui => ui.gameObject.scene.IsValid());
        var reference = existing == null ? null : existing.transform.Find("ButtonRefpManager");
        if (reference == null) throw new InvalidOperationException("Open a scene containing OnlyUI before rebuilding.");
        sprite = reference.GetComponent<Image>().sprite;
        EnsureFolder("Assets/Resources"); EnsureFolder(DirectoryPath);
        font = GetFont();
        var temporary = EditorSceneManager.NewPreviewScene();
        try
        {
            var root = new GameObject("CollaborationUI", typeof(RectTransform));
            SceneManager.MoveGameObjectToScene(root, temporary);
            Stretch(root.GetComponent<RectTransform>(), 0, 0, 0, 0);
            var view = root.AddComponent<CollaborationView>();
            var entry = UnityEngine.Object.Instantiate(reference.gameObject, root.transform, false);
            entry.name = "ButtonCollaboration"; view.launcher = entry.GetComponent<Button>();
            view.launcher.onClick = new Button.ButtonClickedEvent();
            foreach (var label in entry.GetComponentsInChildren<TMP_Text>(true))
            {
                label.font = font; label.text = "协作\n历史"; label.fontSize = 24;
                label.alignment = TextAlignmentOptions.Center; label.enableAutoSizing = true; label.fontSizeMin = 14; label.fontSizeMax = 24;
                Stretch(label.rectTransform, 4, 4, 4, 4);
            }
            CoverUiLayout.ApplyLeftBottomButton(entry.transform as RectTransform, 3);

            view.modal = Fill(root.transform, "Modal", new Color(0, 0, 0, .32f));
            var body = new GameObject("BranchWindow", typeof(RectTransform), typeof(Image));
            body.transform.SetParent(view.modal.transform, false); body.GetComponent<Image>().color = Panel;
            view.window = body.GetComponent<RectTransform>();
            view.window.anchorMin = view.window.anchorMax = view.window.pivot = new Vector2(.5f, .5f); view.window.sizeDelta = new Vector2(1200, 860);
            var header = Top(body.transform, "Header", 0, 0, 0, 52, Dark);
            var drag = header.AddComponent<DraggableUIPanel>(); drag.target = view.window;
            Label(header.transform, "Title", "远程协作", 20, 8, 540, 36, 23, Color.white);
            view.closeButton = Button(header.transform, "Close", "X", 1000, 10, 28, 28, new Color(.35f, .22f, .22f, 1), Color.white);
            var closeRect = view.closeButton.transform as RectTransform; closeRect.anchorMin = closeRect.anchorMax = new Vector2(1, 1); closeRect.anchoredPosition = new Vector2(-40, -10);
            view.connectionLabel = Label(body.transform, "ConnectionStatus", "尚未连接远程项目", 20, 68, 650, 28, 15, Color.white);
            view.connectionButton = Button(body.transform, "ConnectionSettings", "连接设置", 784, 64, 112, 34, new Color(.76f, .76f, .76f, 1), Color.black);
            Right(view.connectionButton.transform as RectTransform, -256, -64);
            view.refreshButton = Button(body.transform, "Refresh", "刷新历史", 908, 64, 112, 34, new Color(.76f, .76f, .76f, 1), Color.black);
            Right(view.refreshButton.transform as RectTransform, -132, -64);

            view.branchToolsButton = Button(body.transform,"WorklineTools","工作线管理",0,64,112,34,new Color(.76f,.76f,.76f,1),Color.black);
            Right(view.branchToolsButton.transform as RectTransform,-380,-64);
            AnchorWidth(view.connectionLabel.rectTransform,20,416); view.connectionLabel.enableWordWrapping=false; view.connectionLabel.overflowMode=TextOverflowModes.Ellipsis;
            var tree = Fill(body.transform, "BranchTree", new Color(.28f, .28f, .28f, 1));
            view.branchSidebar = tree;
            var treeRect = tree.transform as RectTransform; treeRect.anchorMin = new Vector2(0, 0); treeRect.anchorMax = new Vector2(0, 1); treeRect.offsetMin = new Vector2(20, 112); treeRect.offsetMax = new Vector2(304, -114);
            view.repositoryLabel = Label(tree.transform, "Repository", "远程项目", 12, 10, 260, 26, 18, Color.white);
            view.searchInput = Input(tree.transform, "Filter", "筛选工作线…", 12, 46, 260, 32);
            var treeScroll = Scroll(tree.transform, "Branches", 8, 90, 8, 58, out var content);
            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>(); layout.spacing = 4; layout.childControlWidth = true; layout.childControlHeight = true; layout.childForceExpandWidth = true; layout.childForceExpandHeight = false;
            layout.padding = new RectOffset(4, 4, 4, 4);
            var fitter = content.gameObject.AddComponent<ContentSizeFitter>(); fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            view.treeContent = content; view.rowTemplate = Row(content); view.rowTemplate.gameObject.SetActive(false);
            view.treeEmptyLabel = Label(treeScroll.viewport, "Empty", "连接后显示工作线", 8, 20, 250, 60, 14, new Color(.78f, .78f, .78f, 1));
            view.createButton = Button(tree.transform, "CreateBranch", "+ 新建命名工作线", 12, 0, 260, 34, new Color(.76f, .76f, .76f, 1), Color.black);
            var cr = view.createButton.transform as RectTransform; cr.anchorMin = cr.anchorMax = new Vector2(0, 0); cr.pivot = Vector2.zero; cr.anchoredPosition = new Vector2(12, 12);

            var details = Fill(body.transform, "BranchDetails", new Color(.30f, .30f, .30f, 1)); Stretch(details.transform as RectTransform, 20, 114, 20, 112); view.details=details.transform as RectTransform; tree.SetActive(false);
            view.selectionLabel = Label(details.transform, "Selection", "地图历史", 20, 12, 440, 34, 24, Color.white);
            AnchorWidth(view.selectionLabel.rectTransform,20,190); view.selectionLabel.enableWordWrapping=false; view.selectionLabel.overflowMode=TextOverflowModes.Ellipsis;
            view.selectionStateLabel = Label(details.transform, "SelectionState", "未连接", 480, 17, 160, 24, 13, new Color(.7f, .85f, .74f, 1));
            Right(view.selectionStateLabel.transform as RectTransform, -180, -17);
            BuildCommitGraph(view, details.transform);
            var detailBox = Bottom(details.transform, "SelectedCommit", 20, 161, 20, 52);
            view.detailLabel = Text(detailBox.transform, "Details", "", 14, new Color(.85f,.85f,.85f,1)); Stretch(view.detailLabel.rectTransform,0,0,0,0);
            view.detailLabel.overflowMode = TextOverflowModes.Ellipsis;
            var currentBox = Bottom(details.transform, "CurrentWorkspace", 20, 134, 20, 22);
            view.currentLabel = Text(currentBox.transform, "CurrentBranch", "", 14, Color.white); Stretch(view.currentLabel.rectTransform,0,0,0,0);
            view.currentLabel.overflowMode = TextOverflowModes.Ellipsis; view.currentLabel.enableWordWrapping=false;
            view.messageInput = Input(details.transform, "CommitMessage", "本次修改说明（可选）", 20, 0, 620, 32);
            var msg = view.messageInput.transform as RectTransform; msg.anchorMin=Vector2.zero; msg.anchorMax=new Vector2(1,0); msg.pivot=new Vector2(0,0);
            msg.offsetMin=new Vector2(20,94); msg.offsetMax=new Vector2(-20,126);
            var actions = Bottom(details.transform, "NavigationActions", 20, 50, 20, 34);
            var navLayout = actions.AddComponent<HorizontalLayoutGroup>(); navLayout.spacing=10; navLayout.childControlWidth=navLayout.childControlHeight=navLayout.childForceExpandWidth=true;
            view.switchButton = FlexButton(actions.transform, "SwitchBranch", "载入所选节点", Accent, Color.white); view.switchLabel=view.switchButton.GetComponentInChildren<TMP_Text>();
            view.updateButton = FlexButton(actions.transform, "UpdateBranch", "获取最新节点", new Color(.76f,.76f,.76f,1), Color.black);
            var publishing = Bottom(details.transform, "PublishingActions",20,8,20,34);
            var publishingLayout=publishing.AddComponent<HorizontalLayoutGroup>(); publishingLayout.spacing=10; publishingLayout.childControlWidth=publishingLayout.childControlHeight=publishingLayout.childForceExpandWidth=true;
            view.publishButton=FlexButton(publishing.transform,"Publish","提交地图",Accent,Color.white);
            view.mergeButton=FlexButton(publishing.transform,"Merge","标记合并 A",new Color(.76f,.76f,.76f,1),Color.black);
            view.mergeButton.gameObject.SetActive(true);

            var footer = Fill(body.transform, "StatusBar", Dark); var fr = footer.transform as RectTransform; fr.anchorMin = Vector2.zero; fr.anchorMax = new Vector2(1, 0); fr.offsetMin = new Vector2(20, 16); fr.offsetMax = new Vector2(-20, 94);
            var log = Scroll(footer.transform, "StatusScroll", 12, 8, 12, 8, out var logContent);
            view.statusLabel = Text(logContent, "Status", "修改不同 SVG 元素会自动合并；冲突时显示具体元素 ID。", 14, Color.white);
            var statusRect = view.statusLabel.rectTransform; statusRect.anchorMin = new Vector2(0, 1); statusRect.anchorMax = Vector2.one; statusRect.pivot = new Vector2(.5f, 1); statusRect.sizeDelta = Vector2.zero;
            var logFit = logContent.gameObject.AddComponent<ContentSizeFitter>(); logFit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var logLayout = logContent.gameObject.AddComponent<VerticalLayoutGroup>(); logLayout.childControlWidth = logLayout.childControlHeight = true; logLayout.childForceExpandHeight = false;
            view.reloadButton = Button(footer.transform, "Reload", "重新载入地图", 0, 0, 180, 32, Accent, Color.white); Right(view.reloadButton.transform as RectTransform, -188, -8); view.reloadButton.gameObject.SetActive(false);

            BuildConnection(view); BuildCreate(view); BuildConfirm(view); BuildMerge(view);
            view.modal.SetActive(false);
            var asset = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            if (asset == null) throw new InvalidOperationException("Failed to save collaboration prefab.");
            AssetDatabase.SaveAssets();
            return PrefabPath;
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(temporary); SceneManager.SetActiveScene(current); Selection.objects = selection;
        }
    }
    [MenuItem("Tools/Rwrme/Install Collaboration UI In Open Scene")]
    public static string InstallInOpenScene()
    {
        var scene = SceneManager.GetActiveScene();
        if (scene.isDirty) throw new InvalidOperationException("The current scene has unsaved user edits; the prefab is ready for manual placement.");
        var ui = UnityEngine.Resources.FindObjectsOfTypeAll<UIManager>().FirstOrDefault(item => item.gameObject.scene == scene);
        if (ui == null) throw new InvalidOperationException("The active scene has no OnlyUI.");
        var prefab = AssetDatabase.LoadAssetAtPath<CollaborationView>(PrefabPath);
        if (prefab == null) throw new InvalidOperationException("Build the collaboration UI prefab first.");
        var view = ui.GetComponentInChildren<CollaborationView>(true);
        if (view == null)
        {
            var root = (GameObject)PrefabUtility.InstantiatePrefab(prefab.gameObject, scene);
            Undo.RegisterCreatedObjectUndo(root, "Install collaboration UI");
            root.transform.SetParent(ui.transform, false); root.name = "CollaborationUI";
            view = root.GetComponent<CollaborationView>();
        }
        string[] names = { "ButtonRefpManager", "Button3rdManager", "ButtonGeneralSettingsManager" };
        for (int i = 0; i < names.Length; i++)
        {
            var rect = ui.transform.Find(names[i]) as RectTransform;
            if (rect == null) continue;
            Undo.RecordObject(rect, "Align left toolbar UI assets"); CoverUiLayout.ApplyLeftBottomButton(rect, i);
        }
        Undo.FlushUndoRecordObjects();
        if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save the updated UI scene.");
        return scene.path + ": OnlyUI/CollaborationUI prefab installed; left toolbar aligned.";
    }
    static void BuildCommitGraph(CollaborationView view, Transform parent)
    {
        var panel=Fill(parent,"CommitGraph",new Color(.23f,.23f,.23f,1)); Stretch(panel.transform as RectTransform,12,58,12,224);
        var graph=panel.AddComponent<CollaborationCommitGraph>(); view.commitGraph=graph;
        graph.scroll=Scroll(panel.transform,"History",6,6,6,40,out var content); graph.content=content;
        content.anchorMin=content.anchorMax=new Vector2(0,1); content.pivot=new Vector2(0,1);
        graph.scroll.horizontal=true; graph.scroll.scrollSensitivity=40;
        graph.scroll.viewport.offsetMin=new Vector2(0,12);
        var track=Fill(graph.scroll.transform,"HorizontalScrollbar",new Color(.18f,.18f,.18f,1));
        var tr=track.transform as RectTransform; tr.anchorMin=Vector2.zero; tr.anchorMax=new Vector2(1,0); tr.offsetMin=Vector2.zero; tr.offsetMax=new Vector2(-12,7);
        var bar=track.AddComponent<Scrollbar>(); bar.direction=Scrollbar.Direction.LeftToRight;
        var handle=Fill(track.transform,"Handle",new Color(.6f,.6f,.6f,1)); bar.handleRect=handle.transform as RectTransform; bar.targetGraphic=handle.GetComponent<Image>();
        graph.scroll.horizontalScrollbar=bar; graph.scroll.horizontalScrollbarVisibility=ScrollRect.ScrollbarVisibility.AutoHide;
        var edges=new GameObject("ParentEdges",typeof(RectTransform),typeof(CanvasRenderer),typeof(CollaborationGraphEdges)); edges.transform.SetParent(content,false);
        var er=edges.transform as RectTransform; er.anchorMin=er.anchorMax=er.pivot=new Vector2(0,1);
        graph.lines=edges.GetComponent<CollaborationGraphEdges>(); graph.lines.raycastTarget=false;
        var button=Button(content,"CommitNodeTemplate","",0,0,700,60,Color.clear,Color.white);
        UnityEngine.Object.DestroyImmediate(button.transform.Find("Label").gameObject);
        var node=button.gameObject.AddComponent<CollaborationCommitNode>(); graph.nodeTemplate=node;
        node.selectButton=button; node.background=button.GetComponent<Image>();
        var circle=new GameObject("Node",typeof(RectTransform)); circle.transform.SetParent(button.transform,false);
        node.dotRect=circle.transform as RectTransform; node.dotRect.anchorMin=node.dotRect.anchorMax=new Vector2(0,1); node.dotRect.pivot=new Vector2(.5f,.5f); node.dotRect.sizeDelta=new Vector2(20,20);
        var knob=AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
        var ring=Fill(circle.transform,"CurrentRing",Color.white); node.currentRing=ring.GetComponent<Image>(); node.currentRing.sprite=knob; node.currentRing.raycastTarget=false;
        var dot=Fill(circle.transform,"Dot",Color.white); Stretch(dot.transform as RectTransform,4,4,4,4); node.dot=dot.GetComponent<Image>(); node.dot.sprite=knob; node.dot.raycastTarget=false;
        var text=Fill(button.transform,"Description",Color.clear,false); node.textRect=text.transform as RectTransform;
        node.titleLabel=Label(text.transform,"Message","",0,3,500,22,15,Color.white); AnchorWidth(node.titleLabel.rectTransform,0,0);
        node.metaLabel=Label(text.transform,"AuthorAndTime","",0,26,500,16,12,new Color(.73f,.76f,.8f,1)); AnchorWidth(node.metaLabel.rectTransform,0,0);
        node.stateLabel=Label(text.transform,"BranchHeads","",0,44,500,15,11,new Color(.73f,.9f,.77f,1)); AnchorWidth(node.stateLabel.rectTransform,0,0);
        foreach(var label in new[]{node.titleLabel,node.metaLabel,node.stateLabel}) { label.enableWordWrapping=false; label.overflowMode=TextOverflowModes.Ellipsis; }
        node.gameObject.SetActive(false);
        var hintBox=Bottom(panel.transform,"HistoryHint",12,8,170,24); view.historyHintLabel=Text(hintBox.transform,"Hint","",12,new Color(.75f,.75f,.75f,1)); Stretch(view.historyHintLabel.rectTransform,0,0,0,0);
        view.historyHintLabel.enableWordWrapping=false; view.historyHintLabel.overflowMode=TextOverflowModes.Ellipsis;
        view.historyMoreButton=Button(panel.transform,"LoadOlder","加载更早提交",0,0,148,26,new Color(.76f,.76f,.76f,1),Color.black);
        var more=view.historyMoreButton.transform as RectTransform; more.anchorMin=more.anchorMax=new Vector2(1,0); more.pivot=Vector2.one; more.anchoredPosition=new Vector2(-12,34);
    }
    static GameObject Bottom(Transform parent,string name,float left,float bottom,float right,float height)
    {
        var go=Fill(parent,name,Color.clear,false); var rt=go.transform as RectTransform;
        rt.anchorMin=Vector2.zero; rt.anchorMax=new Vector2(1,0); rt.offsetMin=new Vector2(left,bottom); rt.offsetMax=new Vector2(-right,bottom+height); return go;
    }
    static void BuildConnection(CollaborationView view)
    {
        var overlay = Dialog(view.modal.transform, "ConnectionDialog", 600, 470, "连接远程项目", out var card);
        view.connectionDialog = overlay;
        Label(card.transform, "ServerLabel", "服务器 URL", 20, 62, 550, 22, 14, Color.white); view.serverInput = Input(card.transform, "Server", "http://mc3.rhymc.com:35469", 20, 88, 560, 34);
        Label(card.transform, "ProjectLabel", "项目 ID", 20, 134, 550, 22, 14, Color.white); view.projectInput = Input(card.transform, "Project", "my-map", 20, 160, 560, 34);
        Label(card.transform, "UserLabel", "用户 ID（必填）", 20, 206, 550, 22, 14, Color.white); view.userInput = Input(card.transform, "User", "alice", 20, 232, 560, 34);
        Label(card.transform, "TokenLabel", "用户令牌", 20, 278, 550, 22, 14, Color.white); view.tokenInput = Input(card.transform, "Token", "仅保存在本次会话中", 20, 304, 560, 34); view.tokenInput.contentType = TMP_InputField.ContentType.Password;
        Label(card.transform, "Hint", "支持 HTTP / HTTPS；省略协议时默认 HTTP。", 20, 350, 560, 28, 13, new Color(.8f, .8f, .8f, 1));
        view.connectionCloseButton = Button(card.transform, "Close", "取消", 20, 410, 140, 36, new Color(.76f, .76f, .76f, 1), Color.black);
        view.disconnectButton = Button(card.transform, "Disconnect", "断开连接", 172, 410, 140, 36, new Color(.76f, .76f, .76f, 1), Color.black);
        view.connectButton = Button(card.transform, "Connect", "连接项目", 324, 410, 256, 36, Accent, Color.white);
        overlay.SetActive(false);
    }
    static void BuildCreate(CollaborationView view)
    {
        var overlay = Dialog(view.modal.transform, "CreateBranchDialog", 560, 270, "新建命名工作线", out var card); view.createDialog = overlay;
        view.createFromLabel = Label(card.transform, "FromBranch", "从选中节点创建工作线", 20, 66, 520, 44, 15, Color.white);
        view.branchNameInput = Input(card.transform, "BranchName", "users/alice/feature", 20, 126, 520, 36);
        view.createCancelButton = Button(card.transform, "Cancel", "取消", 20, 210, 160, 36, new Color(.76f, .76f, .76f, 1), Color.black);
        view.createConfirmButton = Button(card.transform, "CreateAndSwitch", "创建并切换", 192, 210, 348, 36, Accent, Color.white); overlay.SetActive(false);
    }
    static void BuildMerge(CollaborationView view)
    {
        var overlay=Dialog(view.modal.transform,"MergeDialog",1160,820,"合并节点与冲突处理",out var card);
        var merge=overlay.AddComponent<CollaborationMergeDialog>(); view.mergeDialog=merge; merge.card=card.transform as RectTransform;
        var lineA=Box(card.transform,"EdgeA",280,85,166,2,Accent); (lineA.transform as RectTransform).localEulerAngles=new Vector3(0,0,-12);
        var lineB=Box(card.transform,"EdgeB",280,145,163,2,new Color(.9f,.62f,.32f,1)); (lineB.transform as RectTransform).localEulerAngles=new Vector3(0,0,9);
        var arrowA=Box(card.transform,"ArrowUpper",431,115,12,2,new Color(.7f,.85f,.74f,1));(arrowA.transform as RectTransform).localEulerAngles=new Vector3(0,0,-35);
        var arrowB=Box(card.transform,"ArrowLower",431,127,12,2,new Color(.7f,.85f,.74f,1));(arrowB.transform as RectTransform).localEulerAngles=new Vector3(0,0,35);
        var a=Box(card.transform,"InputA",20,62,260,46,Accent); merge.leftNodeLabel=Text(a.transform,"Node","A",18,Color.white); Stretch(merge.leftNodeLabel.rectTransform,12,4,12,4); merge.leftNodeLabel.alignment=TextAlignmentOptions.Center;
        var b=Box(card.transform,"InputB",20,122,260,46,new Color(.58f,.37f,.2f,1)); merge.rightNodeLabel=Text(b.transform,"Node","B",18,Color.white); Stretch(merge.rightNodeLabel.rectTransform,12,4,12,4); merge.rightNodeLabel.alignment=TextAlignmentOptions.Center;
        var result=Box(card.transform,"NewMergeNode",440,92,300,56,new Color(.26f,.42f,.31f,1)); merge.resultNodeLabel=Text(result.transform,"Node","新合并节点",17,Color.white); Stretch(merge.resultNodeLabel.rectTransform,8,4,8,4); merge.resultNodeLabel.alignment=TextAlignmentOptions.Center;
        Label(card.transform,"PreserveHint","创建一个新的双父节点\nA、B 和已有后续节点均保留",770,84,360,64,16,new Color(.79f,.9f,.81f,1));
        merge.summaryLabel=Label(card.transform,"MergeSummary","正在预览…",20,178,1120,24,14,Color.white); AnchorWidth(merge.summaryLabel.rectTransform,20,20);
        var list=Fill(card.transform,"ConflictList",Dark); var lr=list.transform as RectTransform; lr.anchorMin=new Vector2(0,0);lr.anchorMax=new Vector2(0,1);lr.offsetMin=new Vector2(20,180);lr.offsetMax=new Vector2(250,-212);
        Scroll(list.transform,"Items",4,4,4,4,out var content); merge.conflictContent=content;
        var layout=content.gameObject.AddComponent<VerticalLayoutGroup>();layout.spacing=6;layout.padding=new RectOffset(4,4,4,4);layout.childControlWidth=layout.childControlHeight=layout.childForceExpandWidth=true;layout.childForceExpandHeight=false;
        content.gameObject.AddComponent<ContentSizeFitter>().verticalFit=ContentSizeFitter.FitMode.PreferredSize;
        merge.rowTemplate=Button(content,"ConflictRowTemplate","冲突元素",0,0,210,54,new Color(.31f,.31f,.31f,1),Color.white);
        var le=merge.rowTemplate.gameObject.AddComponent<LayoutElement>();le.preferredHeight=le.minHeight=54;merge.rowTemplate.gameObject.SetActive(false);
        merge.conflictTitle=Label(card.transform,"ConflictTitle","无冲突",270,212,870,26,18,Color.white);AnchorWidth(merge.conflictTitle.rectTransform,270,20);merge.conflictTitle.enableWordWrapping=false;merge.conflictTitle.overflowMode=TextOverflowModes.Ellipsis;
        merge.modeLabel=Label(card.transform,"ConflictScope","",270,244,870,22,13,new Color(.83f,.83f,.83f,1));AnchorWidth(merge.modeLabel.rectTransform,270,20);
        var sources=Top(card.transform,"VersionComparison",270,274,20,266,Color.clear,false);
        var columns=sources.AddComponent<HorizontalLayoutGroup>();columns.spacing=8;columns.childControlWidth=columns.childControlHeight=columns.childForceExpandWidth=true;
        MergeColumn(sources.transform,"Ancestor","共同基础",new Color(.3f,.36f,.33f,1),out merge.ancestorText,out merge.ancestorImage);
        MergeColumn(sources.transform,"Left","节点 A",Accent,out merge.leftText,out merge.leftImage);
        MergeColumn(sources.transform,"Right","节点 B",new Color(.58f,.37f,.2f,1),out merge.rightText,out merge.rightImage);
        var choices=Top(card.transform,"VersionChoices",270,550,20,34,Color.clear,false);
        var choicesLayout=choices.AddComponent<HorizontalLayoutGroup>();choicesLayout.spacing=8;choicesLayout.childControlWidth=choicesLayout.childControlHeight=choicesLayout.childForceExpandWidth=true;
        merge.baseButton=FlexButton(choices.transform,"UseBase","采用共同基础",new Color(.76f,.76f,.76f,1),Color.black);
        merge.leftButton=FlexButton(choices.transform,"UseA","采用 A",Accent,Color.white);
        merge.rightButton=FlexButton(choices.transform,"UseB","采用 B",new Color(.58f,.37f,.2f,1),Color.white);
        var edits=Top(card.transform,"ResolutionActions",270,594,20,32,Color.clear,false);
        var editLayout=edits.AddComponent<HorizontalLayoutGroup>();editLayout.spacing=8;editLayout.childControlWidth=editLayout.childControlHeight=editLayout.childForceExpandWidth=true;
        merge.deleteButton=FlexButton(edits.transform,"Delete","删除此项",new Color(.6f,.33f,.33f,1),Color.white);
        merge.customButton=FlexButton(edits.transform,"EditCustom","手动修改",new Color(.76f,.76f,.76f,1),Color.black);
        merge.resetButton=FlexButton(edits.transform,"Reset","重置此项",new Color(.76f,.76f,.76f,1),Color.black);
        merge.applyButton=FlexButton(edits.transform,"ApplyCustom","应用手动修改",Accent,Color.white);
        merge.customArea=Top(card.transform,"CustomEditor",270,274,20,266,Panel);
        merge.customText=MultiInput(merge.customArea.transform,"Content",false);Stretch(merge.customText.transform as RectTransform,8,8,8,54);
        merge.parentLabel=Label(merge.customArea.transform,"ParentLabel","父图层 ID",8,222,108,26,14,Color.white);
        merge.parentInput=Input(merge.customArea.transform,"Parent","@root",120,220,510,32);AnchorWidth(merge.parentInput.transform as RectTransform,120,8);
        merge.customArea.SetActive(false);
        var note=Bottom(card.transform,"LocalEditsNotice",20,142,20,28);var noteText=Text(note.transform,"Hint","本次只合并 A、B 的已提交版本；当前未提交修改会另存工作副本，不参与本次合并。",13,new Color(.8f,.8f,.8f,1));Stretch(noteText.rectTransform,0,0,0,0);
        merge.messageInput=Input(card.transform,"MergeMessage","合并说明",20,0,1120,32);var mr=merge.messageInput.transform as RectTransform;mr.anchorMin=Vector2.zero;mr.anchorMax=new Vector2(1,0);mr.offsetMin=new Vector2(20,104);mr.offsetMax=new Vector2(-20,136);
        var status=Bottom(card.transform,"MergeStatus",20,57,20,38);merge.statusLabel=Text(status.transform,"Status","",13,Color.white);Stretch(merge.statusLabel.rectTransform,0,0,0,0);merge.statusLabel.overflowMode=TextOverflowModes.Ellipsis;
        merge.closeButton=Button(card.transform,"CancelMerge","取消",20,0,140,34,new Color(.76f,.76f,.76f,1),Color.black);var cr=merge.closeButton.transform as RectTransform;cr.anchorMin=cr.anchorMax=Vector2.zero;cr.pivot=Vector2.zero;cr.anchoredPosition=new Vector2(20,16);
        merge.commitButton=Button(card.transform,"CommitMerge","创建并载入合并节点",0,0,300,34,Accent,Color.white);var br=merge.commitButton.transform as RectTransform;br.anchorMin=br.anchorMax=new Vector2(1,0);br.pivot=new Vector2(1,0);br.anchoredPosition=new Vector2(-20,16);
        overlay.SetActive(false);
    }
    static void MergeColumn(Transform parent,string name,string title,Color color,out TMP_InputField input,out RawImage image)
    {
        var root=Box(parent,name,0,0,280,266,Dark);var layout=root.AddComponent<LayoutElement>();layout.flexibleWidth=1;layout.minWidth=120;
        var header=Top(root.transform,"Header",0,0,0,32,color);var label=Text(header.transform,"Title",title,15,Color.white);Stretch(label.rectTransform,8,4,8,4);
        input=MultiInput(root.transform,"Version",true);Stretch(input.transform as RectTransform,8,40,8,8);
        var frame=Fill(root.transform,"ImagePreview",Color.clear,false);Stretch(frame.transform as RectTransform,8,40,8,8);
        var go=new GameObject("Image",typeof(RectTransform),typeof(RawImage),typeof(AspectRatioFitter));go.transform.SetParent(frame.transform,false);Stretch(go.transform as RectTransform,0,0,0,0);image=go.GetComponent<RawImage>();image.raycastTarget=false;go.GetComponent<AspectRatioFitter>().aspectMode=AspectRatioFitter.AspectMode.FitInParent;
    }
    static TMP_InputField MultiInput(Transform parent,string name,bool readOnly)
    {
        var input=Input(parent,name,"",0,0,280,220);input.lineType=TMP_InputField.LineType.MultiLineNewline;input.readOnly=readOnly;input.characterLimit=262144;
        input.textComponent.enableWordWrapping=true;input.textComponent.alignment=TextAlignmentOptions.TopLeft;input.textComponent.fontSize=13;
        input.GetComponent<Image>().color=new Color(.20f,.20f,.20f,1);input.textComponent.color=Color.white;return input;
    }
    static void BuildConfirm(CollaborationView view)
    {
        var overlay = Dialog(view.modal.transform, "ConfirmationDialog", 580, 300, "确认操作", out var card); view.confirmationDialog = overlay;
        view.confirmationTitle = card.transform.Find("Header/Title").GetComponent<TMP_Text>();
        view.confirmationText = Label(card.transform, "Explanation", "", 20, 72, 540, 120, 16, Color.white);
        view.confirmationCancelButton = Button(card.transform, "Cancel", "取消", 20, 240, 160, 36, new Color(.76f, .76f, .76f, 1), Color.black);
        view.confirmationAcceptButton = Button(card.transform, "Accept", "保存并切换", 192, 240, 368, 36, Accent, Color.white); view.confirmationAcceptLabel = view.confirmationAcceptButton.GetComponentInChildren<TMP_Text>(); overlay.SetActive(false);
    }
    static GameObject Dialog(Transform parent, string name, int width, int height, string title, out GameObject card)
    {
        var overlay = Fill(parent, name, new Color(0, 0, 0, .46f));
        card = new GameObject("Card", typeof(RectTransform), typeof(Image)); card.transform.SetParent(overlay.transform, false); card.GetComponent<Image>().color = Panel;
        var rt = card.transform as RectTransform; rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(.5f, .5f); rt.sizeDelta = new Vector2(width, height);
        var header = Top(card.transform, "Header", 0, 0, 0, 48, Dark); Label(header.transform, "Title", title, 20, 7, width - 40, 34, 21, Color.white); return overlay;
    }
    static CollaborationBranchRow Row(RectTransform parent)
    {
        var button = Button(parent, "BranchRowTemplate", "", 0, 0, 260, 38, new Color(.31f, .31f, .31f, 1), Color.white);
        UnityEngine.Object.DestroyImmediate(button.transform.Find("Label").gameObject);
        var le = button.gameObject.AddComponent<LayoutElement>(); le.preferredHeight = le.minHeight = 38;
        var row = button.gameObject.AddComponent<CollaborationBranchRow>(); row.selectButton = button; row.background = button.GetComponent<Image>();
        var connector = Box(button.transform, "Connector", 0, 0, 10, 1, new Color(.58f, .58f, .58f, 1)); row.connector = connector.transform as RectTransform;
        row.connector.anchorMin = row.connector.anchorMax = new Vector2(0, .5f); row.connector.pivot = new Vector2(0, .5f);
        row.disclosureButton = Button(button.transform, "Disclosure", "-", 8, 0, 22, 30, Color.clear, Color.white); row.disclosureRect = row.disclosureButton.transform as RectTransform;
        row.disclosureRect.anchorMin = row.disclosureRect.anchorMax = new Vector2(0, .5f); row.disclosureRect.pivot = new Vector2(0, .5f); row.disclosureLabel = row.disclosureButton.GetComponentInChildren<TMP_Text>();
        row.nameLabel = Text(button.transform, "Name", "main", 15, Color.white); row.nameRect = row.nameLabel.rectTransform; Stretch(row.nameRect, 34, 0, 42, 0); row.nameLabel.overflowMode = TextOverflowModes.Ellipsis; row.nameLabel.enableWordWrapping = false; row.nameLabel.alignment = TextAlignmentOptions.MidlineLeft;
        row.stateLabel = Label(button.transform, "State", "当前", 214, 6, 42, 26, 11, new Color(.75f, .9f, .78f, 1)); Right(row.stateLabel.rectTransform, -46, -6);
        return row;
    }
    static ScrollRect Scroll(Transform parent, string name, float left, float top, float right, float bottom, out RectTransform content)
    {
        var root = Fill(parent, name, Color.clear, false); Stretch(root.transform as RectTransform, left, top, right, bottom);
        var scroll = root.AddComponent<ScrollRect>(); scroll.horizontal = false; scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 24;
        var viewport = Fill(root.transform, "Viewport", new Color(1, 1, 1, .001f)); viewport.AddComponent<RectMask2D>(); scroll.viewport = viewport.transform as RectTransform;
        var go = new GameObject("Content", typeof(RectTransform)); go.transform.SetParent(viewport.transform, false); content = go.transform as RectTransform;
        content.anchorMin = new Vector2(0, 1); content.anchorMax = Vector2.one; content.pivot = new Vector2(.5f, 1); content.sizeDelta = Vector2.zero; scroll.content = content;
        var track = Fill(root.transform, "Scrollbar", new Color(.2f, .2f, .2f, 1));
        var trackRect = track.transform as RectTransform; trackRect.anchorMin = new Vector2(1,0); trackRect.anchorMax = Vector2.one;
        trackRect.offsetMin = new Vector2(-7,0); trackRect.offsetMax = Vector2.zero;
        var bar = track.AddComponent<Scrollbar>(); bar.direction = Scrollbar.Direction.BottomToTop;
        var handle = Fill(track.transform, "Handle", new Color(.65f,.65f,.65f,1)); bar.handleRect = handle.transform as RectTransform; bar.targetGraphic = handle.GetComponent<Image>();
        scroll.verticalScrollbar = bar; scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
        scroll.viewport.offsetMax = new Vector2(-11,0);
        return scroll;
    }
    static TMP_InputField Input(Transform parent, string name, string placeholder, float x, float y, float width, float height)
    {
        var root = Box(parent, name, x, y, width, height, Color.white); var image = root.GetComponent<Image>(); image.sprite = sprite; image.type = Image.Type.Sliced;
        var input = root.AddComponent<TMP_InputField>(); input.targetGraphic = image;
        var viewport = Fill(root.transform, "Text Area", Color.clear, false); Stretch(viewport.transform as RectTransform, 8, 0, 8, 0); viewport.AddComponent<RectMask2D>();
        var text = Text(viewport.transform, "Text", "", 16, Color.black); Stretch(text.rectTransform, 0, 0, 0, 0); text.alignment = TextAlignmentOptions.MidlineLeft; text.enableWordWrapping = false;
        var hint = Text(viewport.transform, "Placeholder", placeholder, 15, new Color(0, 0, 0, .45f)); Stretch(hint.rectTransform, 0, 0, 0, 0); hint.alignment = TextAlignmentOptions.MidlineLeft;
        input.textViewport = viewport.transform as RectTransform; input.textComponent = text; input.placeholder = hint; input.fontAsset = font;
        input.customCaretColor = true; input.caretColor = Color.black; input.selectionColor = new Color(.25f, .45f, .7f, .35f); return input;
    }
    static Button FlexButton(Transform parent, string name, string text, Color color, Color textColor)
    {
        var value = Button(parent, name, text, 0, 0, 200, 36, color, textColor); var layout = value.gameObject.AddComponent<LayoutElement>(); layout.minWidth = 120; layout.flexibleWidth = 1; return value;
    }
    static Button Button(Transform parent, string name, string text, float x, float y, float width, float height, Color color, Color textColor)
    {
        var go = Box(parent, name, x, y, width, height, color); var image = go.GetComponent<Image>(); image.sprite = sprite; image.type = Image.Type.Sliced;
        var button = go.AddComponent<Button>(); button.targetGraphic = image; var colors = button.colors; colors.highlightedColor = new Color(.85f, .9f, 1, 1); colors.pressedColor = new Color(.62f, .71f, .84f, 1); colors.disabledColor = new Color(.55f, .55f, .55f, .65f); button.colors = colors;
        var label = Text(go.transform, "Label", text, 16, textColor); Stretch(label.rectTransform, 4, 2, 4, 2); label.alignment = TextAlignmentOptions.Center; label.enableAutoSizing = true; label.fontSizeMin = 11; label.fontSizeMax = 16; return button;
    }
    static TMP_Text Label(Transform parent, string name, string text, float x, float y, float width, float height, float size, Color color)
    {
        var label = Text(parent, name, text, size, color); Position(label.rectTransform, x, y, width, height); return label;
    }
    static TMP_Text Text(Transform parent, string name, string text, float size, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI)); go.transform.SetParent(parent, false);
        var label = go.GetComponent<TextMeshProUGUI>(); label.font = font; label.text = text; label.fontSize = size; label.color = color; label.raycastTarget = false; label.richText = false; label.enableWordWrapping = true; label.overflowMode = TextOverflowModes.Overflow; return label;
    }
    static GameObject Box(Transform parent, string name, float x, float y, float width, float height, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image)); go.transform.SetParent(parent, false); Position(go.transform as RectTransform, x, y, width, height); go.GetComponent<Image>().color = color; return go;
    }
    static GameObject Fill(Transform parent, string name, Color color, bool graphic = true)
    {
        var go = graphic ? new GameObject(name, typeof(RectTransform), typeof(Image)) : new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false); Stretch(go.transform as RectTransform, 0, 0, 0, 0); if (graphic) go.GetComponent<Image>().color = color; return go;
    }
    static GameObject Top(Transform parent, string name, float left, float top, float right, float height, Color color, bool graphic = true)
    {
        var go = Fill(parent, name, color, graphic); var rt = go.transform as RectTransform; rt.anchorMin = new Vector2(0, 1); rt.anchorMax = Vector2.one; rt.offsetMin = new Vector2(left, -top - height); rt.offsetMax = new Vector2(-right, -top); return go;
    }
    static void Position(RectTransform rt, float x, float y, float width, float height) { rt.anchorMin = rt.anchorMax = new Vector2(0, 1); rt.pivot = new Vector2(0, 1); rt.anchoredPosition = new Vector2(x, -y); rt.sizeDelta = new Vector2(width, height); }
    static void Right(RectTransform rt, float x, float y) { rt.anchorMin = rt.anchorMax = new Vector2(1, 1); rt.pivot = new Vector2(0, 1); rt.anchoredPosition = new Vector2(x, y); }
    static void AnchorWidth(RectTransform rt, float left, float right) { var p = rt.anchoredPosition; var h = rt.sizeDelta.y; rt.anchorMin = new Vector2(0, 1); rt.anchorMax = Vector2.one; rt.offsetMin = new Vector2(left, p.y - h); rt.offsetMax = new Vector2(-right, p.y); }
    static void Stretch(RectTransform rt, float left, float top, float right, float bottom) { rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = new Vector2(left, bottom); rt.offsetMax = new Vector2(-right, -top); }
    static void EnsureFolder(string path) { if (AssetDatabase.IsValidFolder(path)) return; int split = path.LastIndexOf('/'); EnsureFolder(path.Substring(0, split)); AssetDatabase.CreateFolder(path.Substring(0, split), path.Substring(split + 1)); }
    // TMP 3 uses these native packing helpers through InternalsVisibleTo. Keep
    // access in the editor builder; runtime UI only needs the baked atlas.
    static object FontEngineCall(string name, object[] arguments)
    {
        var method = typeof(FontEngine).GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
            .First(m => m.Name == name && m.GetParameters().Length == arguments.Length
                && m.GetParameters().Select((p, i) => arguments[i] == null || p.ParameterType.IsInstanceOfType(arguments[i])).All(match => match));
        return method.Invoke(null, arguments);
    }
    static TMP_FontAsset GetFont()
    {
        const string path = DirectoryPath + "/CollaborationFont.asset";
        var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
        string sourcePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "msyh.ttc");
        FontEngine.InitializeFontEngine();
        if (FontEngine.LoadFontFace(sourcePath, 32) != FontEngineError.Success)
            throw new InvalidOperationException("The editor needs a CJK system font to bake the collaboration atlas.");
        var asset = ScriptableObject.CreateInstance<TMP_FontAsset>();
        asset.name = "CollaborationFont"; asset.faceInfo = FontEngine.GetFaceInfo();
        asset.atlasPopulationMode = AtlasPopulationMode.Static;
        var version = new SerializedObject(asset); version.FindProperty("m_Version").stringValue = "1.1.0";
        version.FindProperty("m_AtlasWidth").intValue = 1024; version.FindProperty("m_AtlasHeight").intValue = 1024;
        version.FindProperty("m_AtlasPadding").intValue = 4; version.FindProperty("m_AtlasRenderMode").intValue = (int)GlyphRenderMode.SDFAA;
        version.ApplyModifiedPropertiesWithoutUndo();
        string text = string.Join("\n", System.IO.Directory.GetFiles("Assets/Scripts/Collaboration","*.cs").Select(File.ReadAllText))
            + File.ReadAllText("Assets/Editor/Collaboration/CollaborationUiAssetBuilder.cs");
        var characters = Enumerable.Range(32, 95).Select(c => (uint)c).Concat(text.Where(c => c > 127).Select(c => (uint)c)).Distinct().ToArray();
        var characterGlyphs = characters.Select(c => new KeyValuePair<uint, uint>(c, (uint)FontEngineCall("GetGlyphIndex", new object[] { c }))).Where(p => p.Value != 0).ToList();
        var atlas = new Texture2D(1024, 1024, TextureFormat.Alpha8, false); atlas.name = "CollaborationFont Atlas";
        FontEngineCall("ResetAtlasTexture", new object[] { atlas });
        var free = new List<GlyphRect> { new GlyphRect(0, 0, 1023, 1023) }; var used = new List<GlyphRect>();
        var packing = new object[] { characterGlyphs.Select(p => p.Value).Distinct().ToList(), 4, GlyphPackingMode.BestShortSideFit,
            free, used, GlyphRenderMode.SDFAA, atlas, null };
        if (!(bool)FontEngineCall("TryAddGlyphsToTexture", packing)) throw new InvalidOperationException("Collaboration atlas is too small.");
        var glyphs = (Glyph[])packing[7];
        asset.glyphTable.AddRange(glyphs.Where(g => g != null));
        var lookup = asset.glyphTable.ToDictionary(g => g.index);
        asset.characterTable.AddRange(characterGlyphs.Select(pair => new TMP_Character(pair.Key, lookup[pair.Value])));
        asset.atlasTextures = new[] { atlas };
        var template = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
        asset.material = new Material(template.material); asset.material.name = "CollaborationFont Material";
        asset.material.SetTexture("_MainTex", atlas); asset.material.SetFloat("_TextureWidth", 1024); asset.material.SetFloat("_TextureHeight", 1024); asset.material.SetFloat("_GradientScale", 5);
        asset.ReadFontAssetDefinition();
        if (existing != null)
        {
            // Keep the existing asset GUID and the material/atlas subasset identities.
            var oldAtlas=existing.atlasTextures[0]; var oldMaterial=existing.material; var temporaryMaterial=asset.material;
            EditorUtility.CopySerialized(atlas,oldAtlas); oldMaterial.CopyPropertiesFromMaterial(temporaryMaterial); oldMaterial.mainTexture=oldAtlas;
            asset.atlasTextures=new[]{oldAtlas}; asset.material=oldMaterial;
            EditorUtility.CopySerialized(asset,existing); existing.ReadFontAssetDefinition();
            EditorUtility.SetDirty(oldAtlas); EditorUtility.SetDirty(oldMaterial); EditorUtility.SetDirty(existing);
            UnityEngine.Object.DestroyImmediate(atlas); UnityEngine.Object.DestroyImmediate(temporaryMaterial); UnityEngine.Object.DestroyImmediate(asset);
            AssetDatabase.SaveAssets(); return existing;
        }
        AssetDatabase.CreateAsset(asset, path); AssetDatabase.AddObjectToAsset(atlas, asset); AssetDatabase.AddObjectToAsset(asset.material, asset);
        EditorUtility.SetDirty(asset); AssetDatabase.SaveAssets(); return asset;
    }
}
#endif
