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

    GameObject ddBT;//building drop down

    GameObject ddWT;//wall dd

    GameObject ddPS;//platformSerface
    GameObject ddWTP;//platformSerface
    GameObject ddMt;// mesh templates


    Canvas showingCanvas;

    public List<GameObject> mms;

    private GameObject multiSelectPanel;
    private Transform multiSelectContent;
    private TextMeshProUGUI multiSelectHeader;
    private GameObject multiSelectDetailPanel;
    private TextMeshProUGUI multiSelectDetailText;
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
        mms[index].transform.localScale = Vector3.one;
    }
    public void disVisableAll()
    {
        for (int i = 0; i < mms.Count; i++)
        {
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
        TMP_Dropdown dd =        ddBT.GetComponent<TMP_Dropdown>();
        dd.ClearOptions();
        List<BuildingType> btp = MetaMap.instance.buildingTypes;
        List<string> optionTexts = btp.Select(bt => bt.name).ToList();
        dd.AddOptions(optionTexts);
    }
    public void changebtc(int ind)
    {
        BuildingType bt = MetaMap.instance.buildingTypes[ind];
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
        TMP_Dropdown dd = ddWT.GetComponent<TMP_Dropdown>();
        TMP_Dropdown dt = ddWTP.GetComponent<TMP_Dropdown>();
        dd.ClearOptions();
        dt.ClearOptions();
        List<WallType> btp = MetaMap.instance.wallTypes;
        List<string> optionTexts = btp.Select(bt => bt.name).ToList();
        dd.AddOptions(optionTexts);
        dt.AddOptions(optionTexts);
    }
    public void changewtc(int ind)
    {
        WallType wt = MetaMap.instance.wallTypes[ind];
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
        WallType wt = MetaMap.instance.wallTypes[ind];
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
        TMP_Dropdown dd = ddMt.GetComponent<TMP_Dropdown>();
        dd.ClearOptions();
        List<MeshTemplate> btp = MetaMap.instance.meshTemplates;
        List<string> optionTexts = btp.Select(bt => bt.name).ToList();
        dd.AddOptions(optionTexts);
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
        panelRect.anchorMin = new Vector2(0.82f, 0.02f);
        panelRect.anchorMax = new Vector2(0.99f, 0.98f);
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;

        Image panelBg = multiSelectPanel.AddComponent<Image>();
        panelBg.color = new Color(0.12f, 0.12f, 0.14f, 0.95f);

        VerticalLayoutGroup panelVLG = multiSelectPanel.AddComponent<VerticalLayoutGroup>();
        panelVLG.spacing = 5;
        panelVLG.padding = new RectOffset(8, 8, 8, 8);
        panelVLG.childAlignment = TextAnchor.UpperCenter;
        panelVLG.childForceExpandWidth = true;
        panelVLG.childForceExpandHeight = false;

        // --- Header ---
        GameObject headerGO = new GameObject("Header");
        headerGO.transform.SetParent(multiSelectPanel.transform, false);
        multiSelectHeader = headerGO.AddComponent<TextMeshProUGUI>();
        multiSelectHeader.text = "多选";
        multiSelectHeader.fontSize = 18;
        multiSelectHeader.fontStyle = FontStyles.Bold;
        multiSelectHeader.color = new Color(0f, 0.9f, 0.9f);
        multiSelectHeader.alignment = TextAlignmentOptions.Center;
        if (multiSelectHeader.font == null)
            multiSelectHeader.font = TMP_Settings.defaultFontAsset;
        LayoutElement headerLE = headerGO.AddComponent<LayoutElement>();
        headerLE.preferredHeight = 30;

        // --- Scroll View (list area) ---
        BuildScrollView(multiSelectPanel.transform);

        // --- Detail Panel ---
        BuildDetailPanel(multiSelectPanel.transform);

        multiSelectPanel.SetActive(false);
    }

    private void BuildScrollView(Transform parent)
    {
        GameObject scrollGO = new GameObject("ScrollView");
        scrollGO.transform.SetParent(parent, false);

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

        multiSelectHeader.text = items.Count + " items selected";

        ClearEntries();
        for (int i = 0; i < items.Count; i++)
            CreateEntry(items[i], i);

        if (detailViewingItem != null && !items.Contains(detailViewingItem))
        {
            multiSelectDetailPanel.SetActive(false);
            detailViewingItem = null;
        }
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
        if (detailViewingItem == item)
        {
            multiSelectDetailPanel.SetActive(false);
            detailViewingItem = null;
        }
        else
        {
            detailViewingItem = item;
            multiSelectDetailText.text = item.getInfoText();
            multiSelectDetailPanel.SetActive(true);
        }
        RefreshMultiSelectPanel(ToolController.inste.misSelected);
    }

    private void OnEntryRemoved(MapItem item)
    {
        ToolController.inste.misSelected.Remove(item);

        if (detailViewingItem == item)
        {
            multiSelectDetailPanel.SetActive(false);
            detailViewingItem = null;
        }

        if (ToolController.inste.misSelected.Count == 0)
            HideMultiSelectPanel();
        else
            RefreshMultiSelectPanel(ToolController.inste.misSelected);
    }

    #endregion
}
