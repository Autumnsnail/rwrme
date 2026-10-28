using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class CollaborationBranchRow : MonoBehaviour
{
    public Button selectButton, disclosureButton;
    public Image background;
    public RectTransform disclosureRect, nameRect, connector;
    public TMP_Text nameLabel, stateLabel, disclosureLabel;
    public void Present(CollaborationBranchTree.Row row, bool collapsed, bool selected, bool current, Action select, Action expand)
    {
        nameLabel.text = row.branch.name;
        stateLabel.text = current ? "当前" : string.IsNullOrEmpty(row.branch.head) ? "空" : "";
        background.color = selected ? new Color(.25f, .45f, .7f, 1) : current ? new Color(.24f, .31f, .27f, 1) : new Color(.31f, .31f, .31f, 1);
        float indent = Mathf.Min(row.depth, 10) * 18f;
        disclosureRect.anchoredPosition = new Vector2(8 + indent, 0);
        nameRect.offsetMin = new Vector2(34 + indent, 0);
        connector.gameObject.SetActive(row.depth > 0);
        connector.anchoredPosition = new Vector2(7 + indent, 0);
        disclosureButton.gameObject.SetActive(row.hasChildren);
        disclosureLabel.text = collapsed ? "+" : "-";
        selectButton.onClick.RemoveAllListeners(); selectButton.onClick.AddListener(() => select());
        disclosureButton.onClick.RemoveAllListeners(); disclosureButton.onClick.AddListener(() => expand());
    }
}
