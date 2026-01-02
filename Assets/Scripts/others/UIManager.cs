using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    public static UIManager instance;
    GameObject pMM;
    GameObject rMM;
    GameObject bMM;
    GameObject bEM;
    GameObject ddBT;
    void Start()
    {
        instance = this;    
        Debug.Log("UI Manager init");
        pMM =  transform.Find("PinManager").gameObject;
        if(pMM == null )
        {
            Debug.Log("pmmNot F!");
        }
        rMM = transform.Find("RefManager").gameObject;
        bMM = transform.Find("BuilderManager").gameObject;
        bEM = transform.Find("BuildingEditor").gameObject;
        ddBT = transform.Find("BuildingEditor/BuildingTypes").gameObject;
    }

    // Update is called once per frame
    void Update()
    {
        
    }
    public void disVisableAll()
    {
        Debug.Log("tryDisableAll");
        if (pMM == null) { Debug.Log("pmf!"); }
        if (pMM.transform == null) { Debug.Log("pmtfn!"); }
        if (pMM.transform.localScale == null) { Debug.Log("pmtlsfn!"); }
        pMM.transform.localScale = new Vector3(0f, 0f, 0f);
        rMM.transform.localScale = new Vector3(0f, 0f, 0f);
        bMM.transform.localScale = Vector3.zero;
        bEM.transform.localScale = Vector3.zero;

    }
    public void enablePinManager()
    {
        disVisableAll();
        pMM.transform.localScale = new Vector3(1f, 1f, 1f);
    }
    public void enableRefManager()
    {
        disVisableAll();
        rMM.transform.localScale = new Vector3(1f, 1f, 1f);

    }

    public void enableBuilderManager()
    {
        disVisableAll();
        bMM.transform.localScale = Vector3.one;
    }
    public void enableBuildingEditor()
    {
        disVisableAll();
        bEM.transform.localScale = Vector3.one;
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

}
