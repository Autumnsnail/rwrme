using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CtrlZer : MonoBehaviour
{
    public static CtrlZer instance;
    // Start is called before the first frame update
    void Start()
    {
        instance = this;
    }

    // Update is called once per frame
    void Update()
    {
        if(Input.GetKey(KeyCode.LeftControl)&&Input.GetKeyDown(KeyCode.X))
        {
            Debug.Log("CtrlZer:try ctrl+z");
            CtrlZ();
        }
    }

    public List<MapItem> mapItems;

    public void checkPoint()
    {
        Debug.Log("CtrlZer:Saved");
        mapItems = new List<MapItem>(MetaMap.instance.defaultLayer.mapItems);
        //i hope there is gc
    }

    public void CtrlZ()
    {
        MetaMap.instance.defaultLayer.mapItems = new List<MapItem>(mapItems);
        Syncer.instence.destroyAllOutMapitems();
        Syncer.instence.ScatterMapItems();
    }

}
