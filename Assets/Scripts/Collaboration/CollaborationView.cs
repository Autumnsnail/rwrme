using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Serialized branch browser and commit graph in Resources/UI/CollaborationPanel.prefab.</summary>
public sealed class CollaborationView : MonoBehaviour
{
    public GameObject modal, connectionDialog, createDialog, confirmationDialog;
    public RectTransform window, treeContent, details;
    public GameObject branchSidebar;
    public Button branchToolsButton;
    public CollaborationBranchRow rowTemplate;
    public CollaborationCommitGraph commitGraph;
    public CollaborationMergeDialog mergeDialog;
    public Button historyMoreButton;
    public TMP_Text historyHintLabel;
    public Button launcher, closeButton, connectionButton, refreshButton, switchButton, publishButton, updateButton, mergeButton, createButton, reloadButton;
    public Button connectButton, disconnectButton, connectionCloseButton, createConfirmButton, createCancelButton, confirmationAcceptButton, confirmationCancelButton;
    public TMP_InputField serverInput, projectInput, userInput, tokenInput, searchInput, messageInput, branchNameInput;
    public TMP_Text connectionLabel, repositoryLabel, treeEmptyLabel, selectionLabel, selectionStateLabel, detailLabel, currentLabel, statusLabel;
    public TMP_Text createFromLabel, confirmationTitle, confirmationText, confirmationAcceptLabel, switchLabel;
    CollaborationPanel controller;
    readonly HashSet<string> collapsed = new HashSet<string>(StringComparer.Ordinal);
    readonly List<CollaborationBranchRow> rows = new List<CollaborationBranchRow>();
    bool showWorklines;
    public bool WorklinesVisible => showWorklines;
    Action confirmation;
    string lastClicked;
    float lastClickTime;

    public void Bind(CollaborationPanel value)
    {
        controller = value;
        launcher.onClick.AddListener(value.Open); closeButton.onClick.AddListener(value.Close);
        connectionButton.onClick.AddListener(ShowConnection); refreshButton.onClick.AddListener(value.RefreshBranches);
        historyMoreButton.onClick.AddListener(value.LoadMoreHistory);
        branchToolsButton.onClick.AddListener(() => { showWorklines = !showWorklines; Refresh(value); });
        switchButton.onClick.AddListener(RequestSwitch);
        publishButton.onClick.AddListener(() => value.Publish(messageInput.text));
        updateButton.onClick.AddListener(() =>
        {
            if (value.IsDetached) Confirm("返回工作副本", "保存此节点上的本地修改，然后返回之前的工作副本。", "保存并返回", value.UpdateCurrent);
            else Confirm("获取最新节点", "保存并备份当前地图，然后载入当前工作线的最新节点。尚未提交的本地修改可从备份恢复。", "备份并更新", value.UpdateCurrent);
        });
        mergeButton.onClick.AddListener(value.PickForMerge);
        mergeDialog.Bind(value);
        createButton.onClick.AddListener(() =>
        {
            createFromLabel.text = "从节点 " + Short(value.SelectedRevision) + " 创建命名工作线并切换。当前正编辑此节点时会携带本地修改；日常提交无需手动创建。";
            branchNameInput.SetTextWithoutNotify(value.UserId + "/feature");
            createDialog.SetActive(true); branchNameInput.Select();
        });
        createConfirmButton.onClick.AddListener(() => { createDialog.SetActive(false); value.CreateBranch(branchNameInput.text); });
        createCancelButton.onClick.AddListener(() => createDialog.SetActive(false));
        connectButton.onClick.AddListener(() => value.Connect(serverInput.text, projectInput.text, userInput.text, tokenInput.text));
        disconnectButton.onClick.AddListener(() => { value.Disconnect(); tokenInput.SetTextWithoutNotify(""); });
        connectionCloseButton.onClick.AddListener(() => connectionDialog.SetActive(false));
        confirmationCancelButton.onClick.AddListener(() => { confirmation = null; confirmationDialog.SetActive(false); });
        confirmationAcceptButton.onClick.AddListener(() => { var action = confirmation; confirmation = null; confirmationDialog.SetActive(false); action?.Invoke(); });
        reloadButton.onClick.AddListener(value.ReloadCurrentScene);
        searchInput.onValueChanged.AddListener(_ => RenderTree());
        rowTemplate.gameObject.SetActive(false);
        connectionDialog.SetActive(false); createDialog.SetActive(false); confirmationDialog.SetActive(false);
        CoverUiLayout.ApplyLeftBottomButton(launcher.transform as RectTransform, 3);
        Refresh(value);
    }
    void Update()
    {
        if (controller == null || !controller.Visible || controller.Busy || !Input.GetKeyDown(KeyCode.Escape)) return;
        if (controller.MergeSession != null) controller.CancelVisualMerge();
        else if (confirmationDialog.activeSelf) { confirmation = null; confirmationDialog.SetActive(false); }
        else if (createDialog.activeSelf) createDialog.SetActive(false);
        else if (connectionDialog.activeSelf) connectionDialog.SetActive(false);
        else controller.Close();
    }
    public void ShowConnection()
    {
        if (controller == null || controller.Busy) return;
        serverInput.SetTextWithoutNotify(controller.Server); projectInput.SetTextWithoutNotify(controller.Project); userInput.SetTextWithoutNotify(controller.UserId);
        tokenInput.SetTextWithoutNotify(""); connectionDialog.SetActive(true); Refresh(controller);
    }
    public void HideConnection() { tokenInput.SetTextWithoutNotify(""); connectionDialog.SetActive(false); }
    void Confirm(string title, string text, string accept, Action action)
    {
        if (controller.Busy) return;
        confirmationTitle.text = title; confirmationText.text = text; confirmationAcceptLabel.text = accept;
        confirmation = action; confirmationDialog.SetActive(true);
    }
    void RequestSwitch()
    {
        if (!controller.Connected || controller.Busy || !controller.CanSwitch) return;
        string target = Short(controller.SelectedRevision);
        Confirm(controller.SelectionIsNode ? "载入所选节点" : "切换工作副本", "载入 " + target + "？当前地图会保存到本地工作副本，返回时恢复未提交的修改。"
            + "\n在此节点编辑后可直接提交；基础节点已有后续时会自动分叉。", "保存并载入", controller.SwitchToSelected);
    }
    public void Refresh(CollaborationPanel value)
    {
        controller = value;
        modal.SetActive(value.Visible || value.NeedsReload);
        if (modal.activeSelf)
        {
            var parent = window.parent as RectTransform;
            if (parent != null) window.sizeDelta = new Vector2(Mathf.Min(1200, parent.rect.width * .90f), Mathf.Min(860, parent.rect.height * .92f));
        }
        branchSidebar.SetActive(showWorklines);
        details.offsetMin = new Vector2(showWorklines ? 324 : 20,112); details.offsetMax = new Vector2(-20,-114);
        branchToolsButton.GetComponentInChildren<TMP_Text>().text = showWorklines ? "收起工作线" : "工作线管理";
        branchToolsButton.interactable = !value.Busy;
        mergeButton.gameObject.SetActive(true);
        bool ready = value.Connected && !value.Busy && !value.NeedsReload;
        launcher.interactable = !value.Busy; closeButton.interactable = !value.Busy && !value.NeedsReload;
        connectionButton.interactable = !value.Busy && !value.NeedsReload;
        refreshButton.interactable = ready; createButton.interactable = ready && value.Branches.Length > 0;
        publishButton.interactable = ready && value.CanSubmit; updateButton.interactable = ready;
        updateButton.GetComponentInChildren<TMP_Text>().text = value.IsDetached ? "返回工作副本" : "获取最新节点";
        mergeButton.interactable = ready && !string.IsNullOrEmpty(value.SelectedRevision);
        mergeButton.GetComponentInChildren<TMP_Text>().text = string.IsNullOrEmpty(value.MergeAnchor) ? "标记合并 A" : value.MergeAnchor==value.SelectedRevision ? "取消合并标记" : "合并 A 与所选节点";
        switchButton.interactable = ready && value.CanSwitch;
        switchLabel.text = !value.SelectionIsNode ? "载入工作副本" : "载入所选节点";
        messageInput.interactable = ready; searchInput.interactable = !value.Busy;
        reloadButton.gameObject.SetActive(value.NeedsReload); reloadButton.interactable = !value.Busy;
        connectButton.interactable = !value.Busy && !value.Connected;
        disconnectButton.interactable = value.Connected && !value.Busy;
        connectionCloseButton.interactable = !value.Busy;
        serverInput.interactable = projectInput.interactable = userInput.interactable = tokenInput.interactable = !value.Busy && !value.Connected;
        createConfirmButton.interactable = createCancelButton.interactable = !value.Busy;
        connectionLabel.text = value.Connected ? value.Project + "  /  " + value.UserId + "  ·  已连接" : "尚未连接远程项目";
        repositoryLabel.text = value.Connected ? value.Project : "远程项目";
        currentLabel.text = "当前基础   " + (value.CurrentReady ? Short(value.CurrentHead) : "本地地图 · 尚未提交")
            + "    ·    提交时自动判断续接或分叉";
        currentLabel.color = value.CurrentReady ? new Color(.74f,.93f,.79f,1) : Color.white;
        var commit = value.Commits.FirstOrDefault(c => c.id == value.SelectedRevision);
        selectionLabel.text = string.IsNullOrEmpty(value.SelectedRevision) ? "地图历史" : "节点 " + Short(value.SelectedRevision);
        selectionStateLabel.text = !value.Connected ? "未连接" : value.SelectedRevision == value.CurrentHead && value.CurrentReady ? "当前基础" : "可继续编辑";
        detailLabel.text = !value.Connected ? "连接项目后浏览节点，双击载入。" : Short(value.SelectedRevision) + "  ·  "
            + (commit?.author ?? "") + "  " + (commit != null && commit.created > 0 ? DateTimeOffset.FromUnixTimeSeconds((long)commit.created).LocalDateTime.ToString("yyyy-MM-dd HH:mm") : "")
            + "\n" + (commit?.message ?? (string.IsNullOrEmpty(value.SelectedRevision) ? "提交地图后创建首个节点" : "载入此节点后可直接编辑并提交"));
        statusLabel.text = string.IsNullOrEmpty(value.Status) ? "从当前基础节点提交：没有后续就续接；已有后续则自动分叉，无需命名或手动建分支。单击选择节点，双击载入。" : value.Status;
        historyMoreButton.gameObject.SetActive(value.HasMoreHistory); historyMoreButton.interactable = ready;
        historyHintLabel.text = value.Commits.Length == 0 ? "提交地图后显示节点" : value.Commits.Length + " 个提交  ·  单击选择 / 双击跳转";
        commitGraph.Present(value, value.SelectRevision, RequestSwitch, showWorklines, value.MergeAnchor);
        mergeDialog.Refresh(value);
        RenderTree();
    }
    static string Short(string revision) => string.IsNullOrEmpty(revision) ? "尚无节点" : revision.Substring(0, Mathf.Min(10, revision.Length));
    void RenderTree()
    {
        if (controller == null) return;
        var model = CollaborationBranchTree.Build(controller.Branches, collapsed, searchInput.text);
        treeEmptyLabel.gameObject.SetActive(model.Count == 0);
        treeEmptyLabel.text = controller.Connected ? "没有匹配的工作线" : "连接后显示工作线";
        for (int i = 0; i < model.Count; i++)
        {
            if (i == rows.Count) rows.Add(Instantiate(rowTemplate, treeContent, false));
            var row = model[i]; var widget = rows[i]; widget.gameObject.SetActive(true);
            widget.selectButton.interactable = !controller.Busy; widget.disclosureButton.interactable = !controller.Busy;
            string name = row.branch.name;
            widget.Present(row, collapsed.Contains(name), name == controller.SelectedBranch, name == controller.CurrentBranch && controller.CurrentReady && !controller.IsDetached,
                () =>
                {
                    bool twice = lastClicked == name && Time.unscaledTime - lastClickTime < .35f;
                    lastClicked = name; lastClickTime = Time.unscaledTime;
                    controller.SelectBranch(name); if (twice) RequestSwitch();
                },
                () => { if (!collapsed.Add(name)) collapsed.Remove(name); RenderTree(); });
        }
        for (int i = model.Count; i < rows.Count; i++) rows[i].gameObject.SetActive(false);
        LayoutRebuilder.MarkLayoutForRebuild(treeContent);
    }
}
