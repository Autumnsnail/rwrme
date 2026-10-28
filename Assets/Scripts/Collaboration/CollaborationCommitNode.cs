using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class CollaborationCommitNode : MonoBehaviour
{
    public Button selectButton;
    public Image background, dot, currentRing;
    public RectTransform dotRect, textRect;
    public TMP_Text titleLabel, metaLabel, stateLabel;
    string revision;
    float clickTime = -1;
    public void Present(CollaborationCommitGraphModel.Row row, float textLeft, Color color, bool selected, bool current,
        string heads, bool interactable, Action<string> select, Action activate, bool mergeAnchor = false)
    {
        if (revision != row.commit.id) clickTime = -1;
        revision = row.commit.id;
        dot.color = color;
        dotRect.anchoredPosition = new Vector2(20 + row.lane * 28, -30);
        currentRing.gameObject.SetActive(current || mergeAnchor); currentRing.color = mergeAnchor ? new Color(1,.69f,.34f,1) : new Color(.74f, .96f, .79f, 1);
        background.color = selected ? new Color(.23f, .40f, .61f, .85f) : new Color(.32f, .32f, .32f, row.index % 2 == 0 ? .22f : .03f);
        textRect.offsetMin = new Vector2(textLeft, 0); textRect.offsetMax = new Vector2(-12, 0);
        titleLabel.text = string.IsNullOrWhiteSpace(row.commit.message) ? "未填写提交说明" : row.commit.message.Replace('\n', ' ').Replace('\r', ' ');
        string date = row.commit.created > 0 ? DateTimeOffset.FromUnixTimeSeconds((long)row.commit.created).LocalDateTime.ToString("MM-dd HH:mm") : "";
        metaLabel.text = Short(revision) + "  ·  " + (row.commit.author ?? "") + "  " + date;
        stateLabel.text = (mergeAnchor ? "合并 A  " : "") + (current ? "● 当前  " : "") + heads;
        selectButton.interactable = interactable; selectButton.onClick.RemoveAllListeners();
        selectButton.onClick.AddListener(() =>
        {
            bool twice = clickTime >= 0 && Time.unscaledTime - clickTime < .35f;
            clickTime = Time.unscaledTime; select(revision); if (twice) activate();
        });
    }
    static string Short(string id) => id.Substring(0, Mathf.Min(10, id.Length));
}
