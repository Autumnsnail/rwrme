using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class UIManager : MonoBehaviour
{
    public static UIManager instance;
    GameObject pMM;
    GameObject rMM;
    GameObject bMM;
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
}
