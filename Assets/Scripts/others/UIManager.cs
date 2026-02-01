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


    Canvas showingCanvas;

    public List<GameObject> mms;
    void Start()
    {
        showingCanvas = null;
        instance = this;    
        Debug.Log("UI Manager init");
        ddBT = transform.Find("BuildingEditor/BuildingTypes").gameObject;
        ddWTP = transform.Find("PlatformEditor/BaseWallTypes").gameObject;
        ddPS = transform.Find("PlatformEditor/PlatformTypes").gameObject;
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
    public void updatePT()
    {
        TMP_Dropdown dd = ddPS.GetComponent<TMP_Dropdown>();
        dd.ClearOptions();
        List<PlatformSerfaceType> btp = MetaMap.instance.PST;
        List<string> optionTexts = btp.Select(bt => bt.name).ToList();
        dd.AddOptions(optionTexts);
    }
    public void changepsc(int ind)
    {
        PlatformSerfaceType wt = MetaMap.instance.PST[ind];
        if (wt != null)
        {
            MaterialChangerTool uci = ToolController.inste.tools[4] as MaterialChangerTool;
            if (uci != null)
            {
                //uci.setMat(wt);
                ToolController.inste.setToolWithIndex(4);
                uci.setMat(wt);
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
}
