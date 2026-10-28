using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Serialized merge review and conflict editor; never edits a live map during preview.</summary>
public sealed class CollaborationMergeDialog : MonoBehaviour
{
    public RectTransform card, conflictContent;
    public Button rowTemplate, closeButton, leftButton, rightButton, baseButton, deleteButton, customButton, applyButton, resetButton, commitButton;
    public TMP_Text leftNodeLabel, rightNodeLabel, resultNodeLabel, summaryLabel, conflictTitle, statusLabel, modeLabel, parentLabel;
    public TMP_InputField ancestorText, leftText, rightText, customText, parentInput, messageInput;
    public RawImage ancestorImage, leftImage, rightImage;
    public GameObject customArea;
    CollaborationPanel controller;
    readonly List<Button> rows = new List<Button>();
    readonly Texture2D[] textures = new Texture2D[3];
    readonly string[] imageKeys = new string[3];
    CollaborationMergeSession displayedSession;
    string displayedKey;
    bool customMode, customSeeded;
    public void Bind(CollaborationPanel value)
    {
        controller=value; rowTemplate.gameObject.SetActive(false);
        closeButton.onClick.AddListener(value.CancelVisualMerge);
        leftButton.onClick.AddListener(()=>Choose("left")); rightButton.onClick.AddListener(()=>Choose("right")); baseButton.onClick.AddListener(()=>Choose("base"));
        deleteButton.onClick.AddListener(()=>Choose("delete")); resetButton.onClick.AddListener(value.ResetMergeConflict);
        customButton.onClick.AddListener(()=>{ customMode=!customMode; Refresh(value); });
        applyButton.onClick.AddListener(()=>value.ResolveMergeConflict("custom",customText.text,parentInput.text));
        commitButton.onClick.AddListener(()=>value.CommitVisualMerge(messageInput.text));
    }
    void Choose(string choice) { customMode=false; controller.ResolveMergeConflict(choice); }
    public void Refresh(CollaborationPanel value)
    {
        controller=value; var state=value.MergeSession;
        gameObject.SetActive(state!=null);
        if(state==null) { ReleaseTextures(); displayedSession=null; displayedKey=null; return; }
        var parent=card.parent as RectTransform;
        if(parent!=null) card.sizeDelta=new Vector2(Mathf.Min(1160,parent.rect.width*.94f),Mathf.Min(820,parent.rect.height*.96f));
        bool newSession=displayedSession!=state;
        if(newSession) { displayedSession=state; displayedKey=null; customMode=false; messageInput.SetTextWithoutNotify("合并节点 "+Short(state.left)+" + "+Short(state.right)); }
        leftNodeLabel.text="A  "+Short(state.left); rightNodeLabel.text="B  "+Short(state.right);
        resultNodeLabel.text="新合并节点\n保留 A、B 两个父节点";
        if(!string.IsNullOrEmpty(state.completedHead)) resultNodeLabel.text="已创建 "+Short(state.completedHead)+"\n保留 A、B 两个父节点";
        var plan=state.plan; var conflicts=plan?.conflicts??new CollaborationMergeConflict[0];
        summaryLabel.text=plan==null?"正在计算合并计划…":("共同基础 "+Short(plan.ancestor)+"    ·    自动处理 "+plan.automaticFiles+" 个文件    ·    冲突 "+conflicts.Length+" 项 / 待解决 "+plan.unresolved+" 项");
        bool editable=!value.Busy&&!state.attempted;
        for(int i=0;i<conflicts.Length;i++)
        {
            if(i==rows.Count) rows.Add(Instantiate(rowTemplate,conflictContent,false));
            var row=rows[i]; var conflict=conflicts[i]; row.gameObject.SetActive(true);
            row.GetComponentInChildren<TMP_Text>().text=(conflict.resolved?"已解决 · ":"! ")+(string.IsNullOrEmpty(conflict.element)?conflict.path:conflict.element)+"\n"+(conflict.resolved?ChoiceName(conflict.choice):"待解决");
            row.GetComponent<Image>().color=conflict.key==state.selectedKey?new Color(.25f,.45f,.7f,1):new Color(.31f,.31f,.31f,1);
            row.interactable=editable; row.onClick.RemoveAllListeners(); string key=conflict.key; row.onClick.AddListener(()=>value.SelectMergeConflict(key));
        }
        for(int i=conflicts.Length;i<rows.Count;i++) rows[i].gameObject.SetActive(false);
        var current=conflicts.FirstOrDefault(c=>c.key==state.selectedKey);
        var detail=state.detail!=null&&state.detail.key==state.selectedKey?state.detail:null;
        conflictTitle.text=current==null?"无冲突 · 可直接创建合并节点":current.path+(string.IsNullOrEmpty(current.element)?"":"  # "+current.element);
        modeLabel.text=current?.kind=="structure"?"结构冲突：此项将选择完整 SVG 文件，请核对其他元素。":current?.kind=="element"?"仅处理此 ID；其他元素的独立修改会保留。":"文件冲突：可选择任一版本，文本文件也可手动修改。";
        if(displayedKey!=state.selectedKey)
        {
            displayedKey=state.selectedKey; customMode=false; customSeeded=false; ReleaseTextures(); customText.SetTextWithoutNotify(""); parentInput.SetTextWithoutNotify("");
        }
        if(detail!=null)
        {
            PresentValue(detail.ancestor,detail.ancestor,ancestorText,ancestorImage,0);
            PresentValue(detail.left,detail.ancestor,leftText,leftImage,1); PresentValue(detail.right,detail.ancestor,rightText,rightImage,2);
            if(!customSeeded)
            {
                var seed=new[]{detail.left,detail.right,detail.ancestor}.FirstOrDefault(v=>v!=null&&v.editable);
                if(seed!=null) { customText.SetTextWithoutNotify(seed.text??""); parentInput.SetTextWithoutNotify(seed.parent??""); }
                customSeeded=true;
            }
        }
        else
        {
            ancestorText.SetTextWithoutNotify(current==null?"没有需要人工处理的冲突。":"正在读取版本…"); leftText.SetTextWithoutNotify(""); rightText.SetTextWithoutNotify("");
            foreach(var image in new[]{ancestorImage,leftImage,rightImage}) image.gameObject.SetActive(false);
            foreach(var input in new[]{ancestorText,leftText,rightText}) input.gameObject.SetActive(true);
        }
        leftButton.interactable=rightButton.interactable=baseButton.interactable=editable&&detail!=null;
        deleteButton.interactable=editable&&detail!=null&&detail.canDelete;
        customButton.interactable=editable&&detail!=null&&detail.canEdit;
        resetButton.interactable=editable&&current!=null&&current.resolved;
        customArea.SetActive(customMode&&detail!=null); parentInput.gameObject.SetActive(current?.kind=="element"); parentLabel.gameObject.SetActive(current?.kind=="element");
        customText.interactable=parentInput.interactable=applyButton.interactable=editable; applyButton.gameObject.SetActive(customMode);
        closeButton.interactable=!value.Busy; messageInput.interactable=editable;
        commitButton.interactable=!value.Busy&&state.validated&&plan!=null&&plan.unresolved==0;
        commitButton.GetComponentInChildren<TMP_Text>().text=!string.IsNullOrEmpty(state.completedHead)?"载入已创建节点":state.attempted?"重试创建（不会重复）":"创建并载入合并节点";
        statusLabel.text=value.Busy?value.Status:state.attempted?value.Status+"\n可重试相同合并；请求不会重复创建节点。":value.Status;
        LayoutRebuilder.MarkLayoutForRebuild(conflictContent);
    }
    void PresentValue(CollaborationMergeValue value,CollaborationMergeValue ancestor,TMP_InputField input,RawImage image,int index)
    {
        value=value??new CollaborationMergeValue();
        input.SetTextWithoutNotify(Describe(value,ancestor));
        bool hasImage=!string.IsNullOrEmpty(value.image);
        if(hasImage && imageKeys[index]!=value.digest)
        {
            ReleaseTexture(index); var texture=new Texture2D(2,2,TextureFormat.RGBA32,false);
            try { if(texture.LoadImage(Convert.FromBase64String(value.image),true)) { textures[index]=texture; imageKeys[index]=value.digest; image.texture=texture; image.GetComponent<AspectRatioFitter>().aspectRatio=(float)texture.width/texture.height; }
                else DestroyTexture(texture); }
            catch { DestroyTexture(texture); }
        }
        image.gameObject.SetActive(hasImage&&textures[index]!=null); input.gameObject.SetActive(!image.gameObject.activeSelf);
    }
    static string Describe(CollaborationMergeValue value,CollaborationMergeValue ancestor)
    {
        if(!value.exists)return "此版本中不存在（已删除）";
        var text=new StringBuilder();
        if(value.fields!=null&&value.fields.Length>0)
        {
            foreach(var field in value.fields)
            {
                var original=ancestor?.fields?.FirstOrDefault(f=>f.name==field.name);
                text.Append(original!=null&&original.value!=field.value?"● ":"  ").Append(field.name=="layer"?"图层":field.name=="type"?"类型":field.name).Append(" = ").AppendLine(field.value);
            }
            text.AppendLine("\n元素内容：");
        }
        if(value.editable)text.Append(value.text);
        else text.Append("二进制文件\n").Append(value.bytes).Append(" 字节\n内容 ").Append(value.digest);
        return text.ToString();
    }
    static string ChoiceName(string choice) => choice=="left"?"采用 A":choice=="right"?"采用 B":choice=="base"?"采用共同基础":choice=="delete"?"删除":"手动修改";
    static string Short(string id)=>string.IsNullOrEmpty(id)?"初始空节点":id.Substring(0,Mathf.Min(10,id.Length));
    void ReleaseTexture(int i) { if(textures[i]!=null)DestroyTexture(textures[i]); textures[i]=null; imageKeys[i]=null; }
    void ReleaseTextures() { for(int i=0;i<textures.Length;i++) ReleaseTexture(i); }
    static void DestroyTexture(Texture2D texture) { if(Application.isPlaying)Destroy(texture);else DestroyImmediate(texture); }
    void OnDestroy() { ReleaseTextures(); }
}
