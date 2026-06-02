using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using Unity.VisualScripting.Antlr3.Runtime;
using UnityEngine;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    public static UIManager instance;

    /// <summary>右侧条（多选 / 顶点等）锚点横坐标，与 ToolController 里「可打工具射线」的右边界一致。</summary>
    public const float RightPanelAnchorMinX = 0.73f;
    public const float RightPanelAnchorMaxX = 0.90f;

    /// <summary>当前是否正在某个文本输入框（TMP_InputField / 旧版 InputField）里打字。</summary>
    public static bool IsTypingInInputField()
    {
        var es = UnityEngine.EventSystems.EventSystem.current;
        if (es == null || es.currentSelectedGameObject == null) return false;
        var go = es.currentSelectedGameObject;
        return go.GetComponent<TMP_InputField>() != null
            || go.GetComponent<UnityEngine.UI.InputField>() != null;
    }

    /// <summary>
    /// 指针是否压在「可拖拽浮动面板」（多选 / 顶点）当前矩形内。
    /// 只看这两个面板的实际屏幕矩形，不用全局 EventSystem 射线——否则会被铺满视口的
    /// raycastTarget UI 永远命中，导致 selector / 拖框失效。
    /// </summary>
    public static bool PointerOverDraggablePanel()
    {
        if (instance == null) return false;
        return instance.RectActiveAndContains(instance.multiSelectPanel)
            || instance.RectActiveAndContains(instance.vertexPanel);
    }

    private bool RectActiveAndContains(GameObject panel)
    {
        if (panel == null || !panel.activeInHierarchy) return false;
        RectTransform rt = panel.transform as RectTransform;
        if (rt == null) return false;
        Canvas cv = rt.GetComponentInParent<Canvas>();
        Camera cam = (cv != null && cv.renderMode != RenderMode.ScreenSpaceOverlay) ? cv.worldCamera : null;
        return RectTransformUtility.RectangleContainsScreenPoint(rt, Input.mousePosition, cam);
    }

    GameObject ddBT;//building drop down

    GameObject ddWT;//wall dd

    GameObject ddPS;//platformSerface
    GameObject ddWTP;//platformSerface
    GameObject ddMt;// mesh templates
    GameObject ddPosts;// post templates
    GameObject ddDecalTemplates;// decal templates
    // 通用可搜索下拉：在目标下拉框上方插一个搜索输入框，按名子串过滤选项
    class SearchableDropdown
    {
        public GameObject ddGO;
        public TMP_InputField field;
        public System.Func<List<string>> allOptions;   // 当前全量选项名（未过滤）
        public Vector2? customAnchoredPos;             // 父面板本地坐标；null = 放下拉框正上方
        public float customWidth = 0f;                 // <=0 沿用下拉框宽度
        public string placeholder = "Search...";
    }
    SearchableDropdown sdMesh, sdBuilding, sdWall, sdPlatformBaseWall;


    Canvas showingCanvas;

    public List<GameObject> mms;

    private GameObject multiSelectPanel;
    private Transform multiSelectContent;
    private TextMeshProUGUI multiSelectHeader;
    private GameObject multiSelectDetailPanel;
    private TextMeshProUGUI multiSelectDetailText;

    // 多选窗口：拖拽 / 缩放 / 折叠 / 复位 状态
    private RectTransform multiSelectRect;
    private GameObject multiSelectScrollGO;
    private GameObject multiSelectResizeGO;
    private bool multiSelectCollapsed;
    private float msCollapseShrink;   // 折叠时压低 offsetMin.y 的增量；展开时反向撤销，保留折叠期间的拖动位移
    private static readonly Vector2 MsDefaultAnchorMin = new Vector2(RightPanelAnchorMinX, 0.02f);
    private static readonly Vector2 MsDefaultAnchorMax = new Vector2(RightPanelAnchorMaxX, 0.98f);
    private const float MsHeaderHeight = 30f;
    private MapItem detailViewingItem;
    void Start()
    {
        showingCanvas = null;
        instance = this;    
        Debug.Log("UI Manager init");
        ddBT = transform.Find("BuildingEditor/BuildingTypes").gameObject;
        ddWT = transform.Find("WallEditor/WallTypes").gameObject;
        ddWTP = transform.Find("PlatformEditor/BaseWallTypes").gameObject;
        ddMt = transform.Find("MeshEditor/MeshTemplates").gameObject;
        ddDecalTemplates = transform.Find("DecalEditor/DecalTypes").gameObject;
        Transform postsTr = transform.Find("MeshEditor/PostsTemplates");
        if (postsTr != null) ddPosts = postsTr.gameObject;

        // 四个可搜索下拉：启动即建好搜索条，避免首次打开菜单时缺失（原先只在下拉框 PointerDown 惰性创建）
        sdMesh = MakeSearchable(ddMt, () => MetaMap.instance.meshTemplates.Select(t => t.name).ToList());
        // Building 编辑器上方是按钮带（y≈70~150），下拉正上方放不下；改放下方空区（Draw Bush 下方），整宽
        sdBuilding = MakeSearchable(ddBT, () => MetaMap.instance.buildingTypes.Select(t => t.name).ToList(),
            customPos: new Vector2(-0.4f, -90f), placeholder: "Search building...");
        sdWall = MakeSearchable(ddWT, () => MetaMap.instance.wallTypes.Select(t => t.name).ToList());
        sdPlatformBaseWall = MakeSearchable(ddWTP, () => MetaMap.instance.wallTypes.Select(t => t.name).ToList());

        CreateMultiSelectPanel();
        /*
        mms.Add(pMM);//0
        if (pMM == null )
        {
            Debug.Log("pmmNot F!");
        }
        rMM = transform.Find("RefManager").gameObject;
        mms.Add(rMM);//1
        bEM = transform.Find("BuildingEditor").gameObject;
        mms.Add(bEM);//2
        wEM = transform.Find("WallEditor").gameObject;
        mms.Add(wEM);//3
        ddWT = transform.Find("WallEditor/WallTypes").gameObject;
        pEM = transform.Find("PlatformEditor").gameObject;
        mms.Add(pEM);//4
        sEM = transform.Find("SpawnPointEditor").gameObject;
        mms.Add(sEM);//5
        rEM = transform.Find("RockEditor").gameObject;
        mms.Add(rEM);//6
        gEM = transform.Find("settingManager").gameObject;
        */
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void changeShowingCanvas(Canvas canvas)
    {
        if(showingCanvas!=null)
        {
            showingCanvas.enabled = false;
        }
        showingCanvas = canvas;
        if (showingCanvas == null) return;
        showingCanvas.enabled = true;
    }
    public void showMenuUseIndex(int index)
    {
        disVisableAll();
        if (mms == null || index < 0 || index >= mms.Count)
        {
            Debug.LogWarning($"UIManager.showMenuUseIndex: index {index} out of range (mms count {(mms == null ? 0 : mms.Count)})");
            return;
        }
        if (mms[index] == null)
        {
            Debug.LogWarning($"UIManager.showMenuUseIndex: mms[{index}] is not assigned in Inspector");
            return;
        }
        mms[index].transform.localScale = Vector3.one;
    }
    public void disVisableAll()
    {
        if (mms == null) return;
        for (int i = 0; i < mms.Count; i++)
        {
            if (mms[i] == null) continue;
            mms[i].transform.localScale = Vector3.zero;
        }
        /*
        Debug.Log("tryDisableAll");
        if (pMM == null) { Debug.Log("pmf!"); }
        if (pMM.transform == null) { Debug.Log("pmtfn!"); }
        if (pMM.transform.localScale == null) { Debug.Log("pmtlsfn!"); }
        pMM.transform.localScale = new Vector3(0f, 0f, 0f);
        rMM.transform.localScale = new Vector3(0f, 0f, 0f);
        bEM.transform.localScale = Vector3.zero;
        wEM.transform.localScale = Vector3.zero;
        */
    }

    public void updatebBT()
    {
        Debug.Log("UIManager:update bBt");
        RefreshSearchable(sdBuilding);
    }
    public void changebtc(int ind)
    {
        string nm = GetDropdownText(ddBT, ind);
        BuildingType bt = MetaMap.instance.buildingTypes.FirstOrDefault(x => x.name == nm);
        if (bt == null && ind >= 0 && ind < MetaMap.instance.buildingTypes.Count)
            bt = MetaMap.instance.buildingTypes[ind];   // 兜底：搜索框未建好等异常情形
        if (bt != null)
        {
            MaterialChangerTool uci =  ToolController.inste.tools[4]as MaterialChangerTool;
            if(uci!=null)
            {
                uci.setMat(bt);
                ToolController.inste.setToolWithIndex(4);
            }
        }
    }

    public void updateWT()
    {
        RefreshSearchable(sdWall);
        RefreshSearchable(sdPlatformBaseWall);
    }
    public void changewtc(int ind)
    {
        string nm = GetDropdownText(ddWT, ind);
        WallType wt = MetaMap.instance.wallTypes.FirstOrDefault(x => x.name == nm);
        if (wt == null && ind >= 0 && ind < MetaMap.instance.wallTypes.Count)
            wt = MetaMap.instance.wallTypes[ind];
        if (wt != null)
        {
            MaterialChangerTool uci = ToolController.inste.tools[4] as MaterialChangerTool;
            if (uci != null)
            {
                uci.setMat(wt);
                ToolController.inste.setToolWithIndex(4);
            }
        }
    }
    public void changebwt(int ind)
    {
        string nm = GetDropdownText(ddWTP, ind);
        WallType wt = MetaMap.instance.wallTypes.FirstOrDefault(x => x.name == nm);
        if (wt == null && ind >= 0 && ind < MetaMap.instance.wallTypes.Count)
            wt = MetaMap.instance.wallTypes[ind];
        if (wt != null)
        {
            PlatformBasewallChanger uci = ToolController.inste.tools[9] as PlatformBasewallChanger;
            if (uci != null)
            {
                //uci.setMat(wt);
                ToolController.inste.setToolWithIndex(9);
                uci.wtp = wt;
            }
        }
    }

    public void updateMTD()
    {
        RefreshSearchable(sdMesh);
    }

    // ---------- 通用可搜索下拉 ----------

    SearchableDropdown MakeSearchable(GameObject ddGO, System.Func<List<string>> allOptions,
        Vector2? customPos = null, float customWidth = 0f, string placeholder = "Search...")
    {
        var s = new SearchableDropdown
        {
            ddGO = ddGO,
            allOptions = allOptions,
            customAnchoredPos = customPos,
            customWidth = customWidth,
            placeholder = placeholder
        };
        EnsureSearchUI(s);
        return s;
    }

    /// <summary>刷新一个可搜索下拉：保留当前选中（按文本），按搜索框内容过滤。</summary>
    void RefreshSearchable(SearchableDropdown s)
    {
        if (s == null || s.ddGO == null) return;
        TMP_Dropdown dd = s.ddGO.GetComponent<TMP_Dropdown>();
        if (dd == null) return;
        string preserved = (dd.options.Count > 0 && dd.value >= 0 && dd.value < dd.options.Count)
            ? dd.options[dd.value].text : null;
        EnsureSearchUI(s);
        ApplyFilter(s, preserved);
    }

    void EnsureSearchUI(SearchableDropdown s)
    {
        if (s == null || s.field != null || s.ddGO == null) return;

        Transform parent = s.ddGO.transform.parent;
        RectTransform ddRect = s.ddGO.GetComponent<RectTransform>();
        if (parent == null || ddRect == null) return;

        GameObject searchGO = new GameObject("SearchBox", typeof(RectTransform));
        searchGO.transform.SetParent(parent, false);

        RectTransform sRect = searchGO.GetComponent<RectTransform>();
        sRect.anchorMin = ddRect.anchorMin;
        sRect.anchorMax = ddRect.anchorMax;
        sRect.pivot = ddRect.pivot;
        float w = s.customWidth > 0f ? s.customWidth : ddRect.sizeDelta.x;
        sRect.sizeDelta = new Vector2(w, 26);
        sRect.anchoredPosition = s.customAnchoredPos
            ?? (ddRect.anchoredPosition + new Vector2(0, ddRect.sizeDelta.y * 0.5f + sRect.sizeDelta.y * 0.5f + 4));

        Image bg = searchGO.AddComponent<Image>();
        bg.color = new Color(0.95f, 0.95f, 0.95f, 1f);

        s.field = searchGO.AddComponent<TMP_InputField>();

        GameObject textArea = new GameObject("TextArea", typeof(RectTransform), typeof(RectMask2D));
        textArea.transform.SetParent(searchGO.transform, false);
        RectTransform taR = textArea.GetComponent<RectTransform>();
        taR.anchorMin = Vector2.zero; taR.anchorMax = Vector2.one;
        taR.offsetMin = new Vector2(6, 2); taR.offsetMax = new Vector2(-6, -2);

        GameObject placeholder = new GameObject("Placeholder", typeof(RectTransform));
        placeholder.transform.SetParent(textArea.transform, false);
        RectTransform phRect = placeholder.GetComponent<RectTransform>();
        phRect.anchorMin = Vector2.zero; phRect.anchorMax = Vector2.one;
        phRect.offsetMin = Vector2.zero; phRect.offsetMax = Vector2.zero;
        TextMeshProUGUI ph = placeholder.AddComponent<TextMeshProUGUI>();
        ph.text = string.IsNullOrEmpty(s.placeholder) ? "Search..." : s.placeholder;
        ph.fontSize = 13;
        ph.color = new Color(0.5f, 0.5f, 0.5f);
        ph.alignment = TextAlignmentOptions.MidlineLeft;
        if (ph.font == null) ph.font = TMP_Settings.defaultFontAsset;

        GameObject textGO = new GameObject("Text", typeof(RectTransform));
        textGO.transform.SetParent(textArea.transform, false);
        RectTransform tRect = textGO.GetComponent<RectTransform>();
        tRect.anchorMin = Vector2.zero; tRect.anchorMax = Vector2.one;
        tRect.offsetMin = Vector2.zero; tRect.offsetMax = Vector2.zero;
        TextMeshProUGUI tmp = textGO.AddComponent<TextMeshProUGUI>();
        tmp.fontSize = 13;
        tmp.color = Color.black;
        tmp.alignment = TextAlignmentOptions.MidlineLeft;
        if (tmp.font == null) tmp.font = TMP_Settings.defaultFontAsset;

        s.field.textComponent = tmp;
        s.field.placeholder = ph;
        s.field.textViewport = taR;

        var captured = s;
        s.field.onValueChanged.AddListener(_ => ApplyFilter(captured, null));
    }

    void ApplyFilter(SearchableDropdown s, string preferredText)
    {
        if (s == null || s.ddGO == null) return;
        TMP_Dropdown dd = s.ddGO.GetComponent<TMP_Dropdown>();
        if (dd == null || s.allOptions == null) return;
        List<string> all = s.allOptions();
        if (all == null) return;

        string current = preferredText;
        if (current == null && dd.options.Count > 0 && dd.value >= 0 && dd.value < dd.options.Count)
            current = dd.options[dd.value].text;

        string filter = s.field != null ? s.field.text : string.Empty;
        List<string> opts;
        if (string.IsNullOrEmpty(filter))
        {
            opts = new List<string>(all);
        }
        else
        {
            string f = filter.ToLowerInvariant();
            opts = all.Where(n => !string.IsNullOrEmpty(n) && n.ToLowerInvariant().Contains(f)).ToList();
        }

        dd.ClearOptions();
        dd.AddOptions(opts);

        if (opts.Count > 0)
        {
            int idx = string.IsNullOrEmpty(current) ? 0 : opts.IndexOf(current);
            if (idx < 0) idx = 0;
            dd.SetValueWithoutNotify(idx);
            dd.RefreshShownValue();
        }
    }

    string GetDropdownText(GameObject ddGO, int index)
    {
        if (ddGO == null) return null;
        TMP_Dropdown dd = ddGO.GetComponent<TMP_Dropdown>();
        if (dd == null || index < 0 || index >= dd.options.Count) return null;
        return dd.options[index].text;
    }

    /// <summary>ToolController.setMeshScatterTool 通过显示文本解析模板，过滤后仍正确。</summary>
    public string GetMeshDropdownOptionText(int index)
    {
        return GetDropdownText(ddMt, index);
    }

    /// <summary>ToolController.setPostDrawerTool 通过显示文本解析模板。</summary>
    public string GetPostDropdownOptionText(int index)
    {
        return GetDropdownText(ddPosts, index);
    }

    public void updateDecalTemplatesDropdown()
    {
        TMP_Dropdown dd = ddDecalTemplates.GetComponent<TMP_Dropdown>();
        int saved = dd.value;
        dd.ClearOptions();
        List<DecalTemplate> btp = MetaMap.instance.DecalTemplates;
        List<string> optionTexts = btp.Select(bt => bt.name).ToList();
        dd.AddOptions(optionTexts);
        dd.SetValueWithoutNotify(Mathf.Clamp(saved, 0, Mathf.Max(0, optionTexts.Count - 1)));
    }
    public void updatePostsTemplatesDropdown()
    {
        if (ddPosts == null) return;
        TMP_Dropdown dd = ddPosts.GetComponent<TMP_Dropdown>();
        int saved = dd.value;
        dd.ClearOptions();
        List<PostTemplate> btp = MetaMap.instance.PostTemplates;
        List<string> optionTexts = btp.Select(bt => bt.name).ToList();
        dd.AddOptions(optionTexts);
        dd.SetValueWithoutNotify(Mathf.Clamp(saved, 0, Mathf.Max(0, optionTexts.Count - 1)));
    }

    public void setGeneralSetting(string s)
    {
        MetaMap.instance.m_settings = s;
    }
    public void setPlatformHeight(string height)
    {
        float h = float.Parse(height);
        PlatformHeightSetter uci = ToolController.inste.tools[10] as PlatformHeightSetter;
        if (uci != null) uci.height = h;
        ToolController.inste.setToolWithIndex(10);
    }

    #region MultiSelectPanel

    private void CreateMultiSelectPanel()
    {
        multiSelectPanel = new GameObject("MultiSelectPanel");
        multiSelectPanel.transform.SetParent(transform, false);

        RectTransform panelRect = multiSelectPanel.AddComponent<RectTransform>();
        panelRect.anchorMin = MsDefaultAnchorMin;
        panelRect.anchorMax = MsDefaultAnchorMax;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;
        multiSelectRect = panelRect;

        Image panelBg = multiSelectPanel.AddComponent<Image>();
        panelBg.color = new Color(0.12f, 0.12f, 0.14f, 0.95f);

        VerticalLayoutGroup panelVLG = multiSelectPanel.AddComponent<VerticalLayoutGroup>();
        panelVLG.spacing = 5;
        panelVLG.padding = new RectOffset(8, 8, 8, 8);
        panelVLG.childAlignment = TextAnchor.UpperCenter;
        panelVLG.childForceExpandWidth = true;
        panelVLG.childForceExpandHeight = false;

        // --- Header（同时作为拖拽抓手）---
        GameObject headerGO = new GameObject("Header");
        headerGO.transform.SetParent(multiSelectPanel.transform, false);
        Image headerBg = headerGO.AddComponent<Image>();   // 拖拽热区：实体背景条，可靠接收指针事件
        headerBg.color = new Color(0.18f, 0.20f, 0.24f, 1f);
        headerBg.raycastTarget = true;
        LayoutElement headerLE = headerGO.AddComponent<LayoutElement>();
        headerLE.preferredHeight = 30;
        DraggableUIPanel headerDrag = headerGO.AddComponent<DraggableUIPanel>();
        headerDrag.target = panelRect;

        // 标题文字作为子节点，不挡拖拽（raycastTarget=false）
        GameObject headerTextGO = new GameObject("HeaderText");
        headerTextGO.transform.SetParent(headerGO.transform, false);
        RectTransform htRect = headerTextGO.AddComponent<RectTransform>();
        htRect.anchorMin = Vector2.zero;
        htRect.anchorMax = Vector2.one;
        htRect.offsetMin = Vector2.zero;
        htRect.offsetMax = Vector2.zero;
        multiSelectHeader = headerTextGO.AddComponent<TextMeshProUGUI>();
        multiSelectHeader.text = "= Multi-select";
        multiSelectHeader.fontSize = 18;
        multiSelectHeader.fontStyle = FontStyles.Bold;
        multiSelectHeader.color = new Color(0f, 0.9f, 0.9f);
        multiSelectHeader.alignment = TextAlignmentOptions.Center;
        multiSelectHeader.raycastTarget = false;
        if (multiSelectHeader.font == null)
            multiSelectHeader.font = TMP_Settings.defaultFontAsset;

        // 最小化按钮（左 "-"）/ 关闭按钮（右 "X"，等效 ESC 退出菜单），盖在标题栏上、各自吃掉点击不触发拖拽
        MakeHeaderButton(headerGO.transform, "MinimizeBtn", "-",
            new Vector2(0, 0.5f), new Vector2(2, 0), ToggleMultiSelectCollapse);
        MakeHeaderButton(headerGO.transform, "CloseBtn", "X",
            new Vector2(1, 0.5f), new Vector2(-2, 0), CloseMultiSelectMenu);

        // --- Scroll View (list area) ---
        BuildScrollView(multiSelectPanel.transform);

        // --- Detail Panel ---（item 详细信息栏已禁用，保留代码以便回归）
        // BuildDetailPanel(multiSelectPanel.transform);

        // --- Resize 手柄（右下角，排除布局组）---
        BuildResizeHandle(multiSelectPanel.transform, panelRect);

        multiSelectPanel.SetActive(false);
    }

    private void MakeHeaderButton(Transform headerParent, string name, string glyph,
        Vector2 anchor, Vector2 anchoredPos, UnityEngine.Events.UnityAction onClick)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(headerParent, false);
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = anchor;
        rt.sizeDelta = new Vector2(26, 24);
        rt.anchoredPosition = anchoredPos;

        Image bg = go.AddComponent<Image>();
        bg.color = new Color(0.30f, 0.32f, 0.38f, 1f);
        bg.raycastTarget = true;

        Button btn = go.AddComponent<Button>();
        ColorBlock cb = btn.colors;
        cb.highlightedColor = new Color(0.42f, 0.44f, 0.52f, 1f);
        cb.pressedColor = new Color(0.20f, 0.22f, 0.26f, 1f);
        btn.colors = cb;
        btn.onClick.AddListener(onClick);

        GameObject t = new GameObject("T");
        t.transform.SetParent(go.transform, false);
        RectTransform tr = t.AddComponent<RectTransform>();
        tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one;
        tr.offsetMin = Vector2.zero; tr.offsetMax = Vector2.zero;
        TextMeshProUGUI tmp = t.AddComponent<TextMeshProUGUI>();
        tmp.text = glyph;
        tmp.fontSize = 16;
        tmp.color = Color.white;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.raycastTarget = false;
        if (tmp.font == null) tmp.font = TMP_Settings.defaultFontAsset;
    }

    private void BuildResizeHandle(Transform parent, RectTransform panelRect)
    {
        GameObject go = new GameObject("ResizeHandle");
        go.transform.SetParent(parent, false);
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(1, 0);
        rt.anchorMax = new Vector2(1, 0);
        rt.pivot = new Vector2(1, 0);
        rt.sizeDelta = new Vector2(20, 20);
        rt.anchoredPosition = Vector2.zero;

        // 不参与 VerticalLayoutGroup，保持锚定在右下角
        go.AddComponent<LayoutElement>().ignoreLayout = true;

        Image img = go.AddComponent<Image>();
        img.color = new Color(0.45f, 0.47f, 0.55f, 0.9f);
        img.raycastTarget = true;

        ResizeHandle rh = go.AddComponent<ResizeHandle>();
        rh.target = panelRect;

        go.transform.SetAsLastSibling(); // 画在内容之上，便于抓取
        multiSelectResizeGO = go;
    }

    public void ToggleMultiSelectCollapse()
    {
        if (multiSelectRect == null) return;
        multiSelectCollapsed = !multiSelectCollapsed;

        if (multiSelectCollapsed)
        {
            if (multiSelectScrollGO != null) multiSelectScrollGO.SetActive(false);
            if (multiSelectDetailPanel != null) multiSelectDetailPanel.SetActive(false);
            if (multiSelectResizeGO != null) multiSelectResizeGO.SetActive(false);

            // 只留标题栏高度：保持上边界(offsetMax.y)不动，抬高下边界。
            // 只记录"压低的增量"，不存绝对快照——这样折叠期间的拖动位移会自然保留。
            msCollapseShrink = 0f;
            RectTransform p = multiSelectRect.parent as RectTransform;
            if (p != null)
            {
                float spanH = (multiSelectRect.anchorMax.y - multiSelectRect.anchorMin.y) * p.rect.height;
                float targetH = MsHeaderHeight + 16f; // VLG 上下 padding 各 8
                Vector2 omin = multiSelectRect.offsetMin;
                float newMinY = spanH + multiSelectRect.offsetMax.y - targetH;
                msCollapseShrink = newMinY - omin.y;
                omin.y = newMinY;
                multiSelectRect.offsetMin = omin;
            }
            if (multiSelectHeader != null && !multiSelectHeader.text.StartsWith(">"))
                multiSelectHeader.text = "> " + multiSelectHeader.text.TrimStart('=', '>', ' ');
        }
        else
        {
            // 反向撤销折叠时的高度收缩；offsetMin.x / offsetMax 保持当前值，
            // 折叠期间拖动产生的位移（等量加在 min/max 上）因此得以保留。
            Vector2 omin = multiSelectRect.offsetMin;
            omin.y -= msCollapseShrink;
            multiSelectRect.offsetMin = omin;
            msCollapseShrink = 0f;

            if (multiSelectScrollGO != null) multiSelectScrollGO.SetActive(true);
            if (multiSelectDetailPanel != null) multiSelectDetailPanel.SetActive(true);
            if (multiSelectResizeGO != null) multiSelectResizeGO.SetActive(true);
            if (multiSelectHeader != null)
                multiSelectHeader.text = "= " + multiSelectHeader.text.TrimStart('>', '=', ' ');
        }
    }

    /// <summary>关闭多选菜单：等效于按 ESC 退出菜单（清空选择并隐藏相关面板）。</summary>
    public void CloseMultiSelectMenu()
    {
        if (ToolController.inste != null)
        {
            ToolController.inste.miSelected = null;
            ToolController.inste.misSelected.Clear();
        }
        changeShowingCanvas(null);
        RefreshMultiSelectPanel(null);
        RefreshVertexPanel(null);
    }

    private void BuildScrollView(Transform parent)
    {
        GameObject scrollGO = new GameObject("ScrollView");
        scrollGO.transform.SetParent(parent, false);
        multiSelectScrollGO = scrollGO;

        LayoutElement scrollLE = scrollGO.AddComponent<LayoutElement>();
        scrollLE.flexibleHeight = 1;
        scrollLE.flexibleWidth = 1;

        ScrollRect sr = scrollGO.AddComponent<ScrollRect>();
        sr.horizontal = false;
        sr.vertical = true;
        sr.scrollSensitivity = 30;
        sr.movementType = ScrollRect.MovementType.Clamped;

        // Viewport
        GameObject vpGO = new GameObject("Viewport");
        vpGO.transform.SetParent(scrollGO.transform, false);
        RectTransform vpRect = vpGO.AddComponent<RectTransform>();
        vpRect.anchorMin = Vector2.zero;
        vpRect.anchorMax = Vector2.one;
        vpRect.offsetMin = Vector2.zero;
        vpRect.offsetMax = Vector2.zero;
        Image vpImg = vpGO.AddComponent<Image>();
        vpImg.color = Color.white;
        vpGO.AddComponent<Mask>().showMaskGraphic = false;

        // Content
        GameObject contentGO = new GameObject("Content");
        contentGO.transform.SetParent(vpGO.transform, false);
        RectTransform contentRect = contentGO.AddComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0, 1);
        contentRect.anchorMax = new Vector2(1, 1);
        contentRect.pivot = new Vector2(0.5f, 1);
        contentRect.sizeDelta = Vector2.zero;

        VerticalLayoutGroup vlg = contentGO.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 3;
        vlg.padding = new RectOffset(2, 2, 2, 2);
        vlg.childAlignment = TextAnchor.UpperCenter;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;

        ContentSizeFitter csf = contentGO.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        sr.content = contentRect;
        sr.viewport = vpRect;
        multiSelectContent = contentGO.transform;
    }

    // [item 详细信息栏已禁用，保留以便回归] 取消注释 BuildDetailPanel 调用与下方方法体即可恢复
    /*
    private void BuildDetailPanel(Transform parent)
    {
        multiSelectDetailPanel = new GameObject("DetailPanel");
        multiSelectDetailPanel.transform.SetParent(parent, false);

        Image bg = multiSelectDetailPanel.AddComponent<Image>();
        bg.color = new Color(0.16f, 0.16f, 0.22f, 0.95f);

        LayoutElement le = multiSelectDetailPanel.AddComponent<LayoutElement>();
        le.preferredHeight = 130;

        GameObject textGO = new GameObject("DetailText");
        textGO.transform.SetParent(multiSelectDetailPanel.transform, false);
        RectTransform dtRect = textGO.AddComponent<RectTransform>();
        dtRect.anchorMin = Vector2.zero;
        dtRect.anchorMax = Vector2.one;
        dtRect.offsetMin = new Vector2(8, 5);
        dtRect.offsetMax = new Vector2(-8, -5);

        multiSelectDetailText = textGO.AddComponent<TextMeshProUGUI>();
        multiSelectDetailText.fontSize = 14;
        multiSelectDetailText.color = Color.white;
        multiSelectDetailText.alignment = TextAlignmentOptions.TopLeft;
        multiSelectDetailText.overflowMode = TextOverflowModes.Ellipsis;
        if (multiSelectDetailText.font == null)
            multiSelectDetailText.font = TMP_Settings.defaultFontAsset;

        multiSelectDetailPanel.SetActive(false);
    }
    */

    // ---------- public API ----------

    public void RefreshMultiSelectPanel(List<MapItem> items)
    {
        if (items == null || items.Count == 0)
        {
            HideMultiSelectPanel();
            return;
        }

        if (multiSelectPanel == null) CreateMultiSelectPanel();

        disVisableAll();
        multiSelectPanel.SetActive(true);

        multiSelectHeader.text = (multiSelectCollapsed ? "> " : "= ") + items.Count + " items selected";

        ClearEntries();
        for (int i = 0; i < items.Count; i++)
            CreateEntry(items[i], i);

        // [详细信息栏已禁用，保留以便回归]
        // if (detailViewingItem != null && !items.Contains(detailViewingItem))
        // {
        //     multiSelectDetailPanel.SetActive(false);
        //     detailViewingItem = null;
        // }
    }

    private void HideMultiSelectPanel()
    {
        if (multiSelectPanel != null)
            multiSelectPanel.SetActive(false);
        detailViewingItem = null;
    }

    // ---------- entry helpers ----------

    private void ClearEntries()
    {
        if (multiSelectContent == null) return;
        for (int i = multiSelectContent.childCount - 1; i >= 0; i--)
            Destroy(multiSelectContent.GetChild(i).gameObject);
    }

    private void CreateEntry(MapItem item, int index)
    {
        GameObject row = new GameObject("Entry_" + index);
        row.transform.SetParent(multiSelectContent, false);

        row.AddComponent<LayoutElement>().preferredHeight = 36;

        HorizontalLayoutGroup hlg = row.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 4;
        hlg.padding = new RectOffset(4, 4, 2, 2);
        hlg.childAlignment = TextAnchor.MiddleLeft;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = true;

        // --- clickable info area ---
        GameObject infoGO = new GameObject("InfoBtn");
        infoGO.transform.SetParent(row.transform, false);

        Image infoBg = infoGO.AddComponent<Image>();
        bool viewing = (detailViewingItem == item);
        infoBg.color = viewing
            ? new Color(0.12f, 0.38f, 0.48f, 0.95f)
            : new Color(0.22f, 0.22f, 0.26f, 0.9f);

        Button infoBtn = infoGO.AddComponent<Button>();
        ColorBlock cb = infoBtn.colors;
        cb.highlightedColor = new Color(0.30f, 0.30f, 0.36f, 1f);
        cb.pressedColor = new Color(0.14f, 0.14f, 0.18f, 1f);
        infoBtn.colors = cb;

        MapItem captured = item;
        infoBtn.onClick.AddListener(() => OnEntryClicked(captured));

        infoGO.AddComponent<LayoutElement>().flexibleWidth = 1;

        HorizontalLayoutGroup infoHLG = infoGO.AddComponent<HorizontalLayoutGroup>();
        infoHLG.spacing = 6;
        infoHLG.padding = new RectOffset(6, 6, 2, 2);
        infoHLG.childAlignment = TextAnchor.MiddleLeft;
        infoHLG.childForceExpandWidth = false;
        infoHLG.childForceExpandHeight = true;

        // type label
        GameObject typeGO = MakeTMP(infoGO.transform, "Type",
            item.GetType().Name, 13, new Color(0.3f, 0.85f, 0.85f));
        typeGO.AddComponent<LayoutElement>().preferredWidth = 72;

        // id label
        GameObject idGO = MakeTMP(infoGO.transform, "Id",
            item.id, 12, Color.white);
        idGO.AddComponent<LayoutElement>().flexibleWidth = 1;

        // --- remove button ---
        GameObject rmGO = new GameObject("RemoveBtn");
        rmGO.transform.SetParent(row.transform, false);

        Image rmBg = rmGO.AddComponent<Image>();
        rmBg.color = new Color(0.65f, 0.18f, 0.18f, 0.85f);

        Button rmBtn = rmGO.AddComponent<Button>();
        ColorBlock rcb = rmBtn.colors;
        rcb.highlightedColor = new Color(0.85f, 0.28f, 0.28f, 1f);
        rcb.pressedColor = new Color(0.45f, 0.10f, 0.10f, 1f);
        rmBtn.colors = rcb;
        rmBtn.onClick.AddListener(() => OnEntryRemoved(captured));

        rmGO.AddComponent<LayoutElement>().preferredWidth = 28;

        GameObject xGO = MakeTMP(rmGO.transform, "X", "\u2715", 15, Color.white);
        xGO.GetComponent<TextMeshProUGUI>().alignment = TextAlignmentOptions.Center;
        RectTransform xr = xGO.GetComponent<RectTransform>();
        xr.anchorMin = Vector2.zero;
        xr.anchorMax = Vector2.one;
        xr.offsetMin = Vector2.zero;
        xr.offsetMax = Vector2.zero;
    }

    private GameObject MakeTMP(Transform parent, string goName,
        string text, float size, Color color)
    {
        GameObject go = new GameObject(goName);
        go.transform.SetParent(parent, false);
        TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = size;
        tmp.color = color;
        tmp.overflowMode = TextOverflowModes.Ellipsis;
        tmp.raycastTarget = false;
        if (tmp.font == null)
            tmp.font = TMP_Settings.defaultFontAsset;
        return go;
    }

    // ---------- entry callbacks ----------

    private void OnEntryClicked(MapItem item)
    {
        // [item 详细信息栏已禁用，保留以便回归] 原为点击条目展开/收起详情
        // if (detailViewingItem == item)
        // {
        //     multiSelectDetailPanel.SetActive(false);
        //     detailViewingItem = null;
        // }
        // else
        // {
        //     detailViewingItem = item;
        //     multiSelectDetailText.text = item.getInfoText();
        //     multiSelectDetailPanel.SetActive(true);
        // }
        // RefreshMultiSelectPanel(ToolController.inste.misSelected);
    }

    private void OnEntryRemoved(MapItem item)
    {
        ToolController.inste.misSelected.Remove(item);

        // [详细信息栏已禁用，保留以便回归]
        // if (detailViewingItem == item)
        // {
        //     multiSelectDetailPanel.SetActive(false);
        //     detailViewingItem = null;
        // }

        if (ToolController.inste.misSelected.Count == 0)
            HideMultiSelectPanel();
        else
            RefreshMultiSelectPanel(ToolController.inste.misSelected);
    }

    #endregion

    #region VertexPanel

    private GameObject vertexPanel;
    private Transform vertexContent;
    private MapItem vertexTarget;

    public void RefreshVertexPanel(MapItem item)
    {
        Wall wall = item as Wall;
        Platform plat = item as Platform;

        if (wall == null && plat == null)
        {
            HideVertexPanel();
            return;
        }

        if (vertexPanel == null) CreateVertexPanel();
        vertexTarget = item;
        vertexPanel.SetActive(true);
        RebuildVertexEntries();
    }

    private void HideVertexPanel()
    {
        if (vertexPanel != null)
            vertexPanel.SetActive(false);
        vertexTarget = null;
    }

    private void CreateVertexPanel()
    {
        vertexPanel = new GameObject("VertexPanel");
        vertexPanel.transform.SetParent(transform, false);

        RectTransform pRect = vertexPanel.AddComponent<RectTransform>();
        pRect.anchorMin = new Vector2(RightPanelAnchorMinX, 0.02f);
        pRect.anchorMax = new Vector2(RightPanelAnchorMaxX, 0.98f);
        pRect.offsetMin = Vector2.zero;
        pRect.offsetMax = Vector2.zero;

        Image bg = vertexPanel.AddComponent<Image>();
        bg.color = new Color(0.10f, 0.10f, 0.12f, 0.95f);

        VerticalLayoutGroup vlg = vertexPanel.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 4;
        vlg.padding = new RectOffset(6, 6, 6, 6);
        vlg.childAlignment = TextAnchor.UpperCenter;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;

        GameObject headerGO = new GameObject("Header");
        headerGO.transform.SetParent(vertexPanel.transform, false);
        TextMeshProUGUI hdr = headerGO.AddComponent<TextMeshProUGUI>();
        hdr.text = "Vertices";
        hdr.fontSize = 16;
        hdr.fontStyle = FontStyles.Bold;
        hdr.color = new Color(0.3f, 0.85f, 0.85f);
        hdr.alignment = TextAlignmentOptions.Center;
        if (hdr.font == null) hdr.font = TMP_Settings.defaultFontAsset;
        headerGO.AddComponent<LayoutElement>().preferredHeight = 24;

        GameObject scrollGO = new GameObject("Scroll");
        scrollGO.transform.SetParent(vertexPanel.transform, false);
        scrollGO.AddComponent<LayoutElement>().flexibleHeight = 1;

        ScrollRect sr = scrollGO.AddComponent<ScrollRect>();
        sr.horizontal = false;
        sr.vertical = true;
        sr.scrollSensitivity = 30;
        sr.movementType = ScrollRect.MovementType.Clamped;

        GameObject vpGO = new GameObject("VP");
        vpGO.transform.SetParent(scrollGO.transform, false);
        RectTransform vpR = vpGO.AddComponent<RectTransform>();
        vpR.anchorMin = Vector2.zero;
        vpR.anchorMax = Vector2.one;
        vpR.offsetMin = Vector2.zero;
        vpR.offsetMax = Vector2.zero;
        vpGO.AddComponent<Image>().color = Color.white;
        vpGO.AddComponent<Mask>().showMaskGraphic = false;

        GameObject contentGO = new GameObject("Content");
        contentGO.transform.SetParent(vpGO.transform, false);
        RectTransform cRect = contentGO.AddComponent<RectTransform>();
        cRect.anchorMin = new Vector2(0, 1);
        cRect.anchorMax = new Vector2(1, 1);
        cRect.pivot = new Vector2(0.5f, 1);
        cRect.sizeDelta = Vector2.zero;

        VerticalLayoutGroup cvlg = contentGO.AddComponent<VerticalLayoutGroup>();
        cvlg.spacing = 2;
        cvlg.padding = new RectOffset(2, 2, 2, 2);
        cvlg.childForceExpandWidth = true;
        cvlg.childForceExpandHeight = false;
        contentGO.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        sr.content = cRect;
        sr.viewport = vpR;
        vertexContent = contentGO.transform;

        vertexPanel.SetActive(false);
    }

    private void RebuildVertexEntries()
    {
        if (vertexContent == null) return;
        for (int i = vertexContent.childCount - 1; i >= 0; i--)
            Destroy(vertexContent.GetChild(i).gameObject);

        Wall wall = vertexTarget as Wall;
        Platform plat = vertexTarget as Platform;

        if (wall != null)
        {
            AddSectionLabel("Path (" + wall.positionLine.Count + " pts)");
            for (int i = 0; i < wall.positionLine.Count; i++)
                AddVertexRow(wall.positionLine, i, "W");
            AddAddButton(wall.positionLine, "W");
        }
        else if (plat != null)
        {
            AddSectionLabel("Right (" + plat.positinLineR.Count + " pts)");
            for (int i = 0; i < plat.positinLineR.Count; i++)
                AddVertexRow(plat.positinLineR, i, "R");
            AddAddButton(plat.positinLineR, "R");

            AddSectionLabel("Left (" + plat.positinLineL.Count + " pts)");
            for (int i = 0; i < plat.positinLineL.Count; i++)
                AddVertexRow(plat.positinLineL, i, "L");
            AddAddButton(plat.positinLineL, "L");
        }
    }

    private void AddSectionLabel(string text)
    {
        GameObject go = MakeTMP(vertexContent, "Sec", text, 13, new Color(0.9f, 0.8f, 0.3f));
        go.AddComponent<LayoutElement>().preferredHeight = 20;
    }

    private void AddVertexRow(List<Vector2> list, int index, string tag)
    {
        GameObject row = new GameObject("V" + tag + index);
        row.transform.SetParent(vertexContent, false);
        row.AddComponent<LayoutElement>().preferredHeight = 28;

        HorizontalLayoutGroup hlg = row.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 3;
        hlg.padding = new RectOffset(2, 2, 1, 1);
        hlg.childAlignment = TextAnchor.MiddleLeft;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = true;

        GameObject idxGO = MakeTMP(row.transform, "Idx", index.ToString(), 11, Color.gray);
        idxGO.AddComponent<LayoutElement>().preferredWidth = 20;

        int capturedIdx = index;
        List<Vector2> capturedList = list;

        TMP_InputField xField = CreateCoordField(row.transform, "X", list[index].x);
        xField.onEndEdit.AddListener(val =>
        {
            if (float.TryParse(val, out float f) && capturedIdx < capturedList.Count)
            {
                capturedList[capturedIdx] = new Vector2(f, capturedList[capturedIdx].y);
                vertexTarget.scatterThis();
            }
        });

        TMP_InputField yField = CreateCoordField(row.transform, "Y", list[index].y);
        yField.onEndEdit.AddListener(val =>
        {
            if (float.TryParse(val, out float f) && capturedIdx < capturedList.Count)
            {
                capturedList[capturedIdx] = new Vector2(capturedList[capturedIdx].x, f);
                vertexTarget.scatterThis();
            }
        });

        GameObject delGO = new GameObject("Del");
        delGO.transform.SetParent(row.transform, false);
        Image delBg = delGO.AddComponent<Image>();
        delBg.color = new Color(0.6f, 0.15f, 0.15f, 0.85f);
        Button delBtn = delGO.AddComponent<Button>();
        delBtn.onClick.AddListener(() =>
        {
            if (capturedIdx >= capturedList.Count || capturedList.Count <= 2) return;
            Platform plat = vertexTarget as Platform;
            if (plat != null)
            {
                var otherList = (capturedList == plat.positinLineR) ? plat.positinLineL : plat.positinLineR;
                if (otherList.Count <= 2) return;
            }
            CtrlZer.instance.checkPoint();
            capturedList.RemoveAt(capturedIdx);
            if (plat != null)
            {
                var otherList = (capturedList == plat.positinLineR) ? plat.positinLineL : plat.positinLineR;
                if (capturedIdx < otherList.Count)
                    otherList.RemoveAt(capturedIdx);
            }
            vertexTarget.scatterThis();
            RebuildVertexEntries();
        });
        delGO.AddComponent<LayoutElement>().preferredWidth = 22;
        GameObject xGO = MakeTMP(delGO.transform, "X", "\u2715", 12, Color.white);
        xGO.GetComponent<TextMeshProUGUI>().alignment = TextAlignmentOptions.Center;
        RectTransform xr = xGO.GetComponent<RectTransform>();
        xr.anchorMin = Vector2.zero; xr.anchorMax = Vector2.one;
        xr.offsetMin = Vector2.zero; xr.offsetMax = Vector2.zero;
    }

    private TMP_InputField CreateCoordField(Transform parent, string label, float val)
    {
        GameObject go = new GameObject(label);
        go.transform.SetParent(parent, false);
        go.AddComponent<LayoutElement>().flexibleWidth = 1;

        Image bg = go.AddComponent<Image>();
        bg.color = new Color(0.18f, 0.18f, 0.22f, 1f);

        TMP_InputField field = go.AddComponent<TMP_InputField>();
        field.contentType = TMP_InputField.ContentType.DecimalNumber;

        GameObject textArea = new GameObject("TextArea");
        textArea.transform.SetParent(go.transform, false);
        RectTransform taRect = textArea.AddComponent<RectTransform>();
        taRect.anchorMin = Vector2.zero; taRect.anchorMax = Vector2.one;
        taRect.offsetMin = new Vector2(4, 0); taRect.offsetMax = new Vector2(-4, 0);
        textArea.AddComponent<RectMask2D>();

        GameObject textGO = new GameObject("Text");
        textGO.transform.SetParent(textArea.transform, false);
        TextMeshProUGUI tmp = textGO.AddComponent<TextMeshProUGUI>();
        tmp.fontSize = 11;
        tmp.color = Color.white;
        tmp.alignment = TextAlignmentOptions.MidlineLeft;
        if (tmp.font == null) tmp.font = TMP_Settings.defaultFontAsset;
        RectTransform tRect = textGO.GetComponent<RectTransform>();
        tRect.anchorMin = Vector2.zero; tRect.anchorMax = Vector2.one;
        tRect.offsetMin = Vector2.zero; tRect.offsetMax = Vector2.zero;

        field.textComponent = tmp;
        field.textViewport = taRect;
        field.text = val.ToString("F1");

        return field;
    }

    private void AddAddButton(List<Vector2> list, string tag)
    {
        GameObject go = new GameObject("Add" + tag);
        go.transform.SetParent(vertexContent, false);
        go.AddComponent<LayoutElement>().preferredHeight = 24;

        Image bg = go.AddComponent<Image>();
        bg.color = new Color(0.15f, 0.4f, 0.15f, 0.85f);

        Button btn = go.AddComponent<Button>();
        List<Vector2> capturedList = list;
        btn.onClick.AddListener(() =>
        {
            CtrlZer.instance.checkPointTransformOnly();
            Vector2 last = capturedList.Count > 0 ? capturedList[capturedList.Count - 1] : Vector2.zero;
            capturedList.Add(last + new Vector2(10, 0));
            vertexTarget.scatterThis();
            RebuildVertexEntries();
        });

        GameObject label = MakeTMP(go.transform, "Lbl", "+ Add Point", 12, Color.white);
        label.GetComponent<TextMeshProUGUI>().alignment = TextAlignmentOptions.Center;
        RectTransform lr = label.GetComponent<RectTransform>();
        lr.anchorMin = Vector2.zero; lr.anchorMax = Vector2.one;
        lr.offsetMin = Vector2.zero; lr.offsetMax = Vector2.zero;
    }

    #endregion
}
