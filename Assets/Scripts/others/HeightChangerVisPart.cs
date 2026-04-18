using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HeightChangerVisPart : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void ChangeHeight(string height)
    {
        float.TryParse(height, out float heightValue);
        if(ToolController.inste.currentTool is HeightBush htb) htb.height = heightValue;
    }
    public void ChangeHardness(string hardness)
    {
        float.TryParse(hardness, out float hardnessValue);
        if(ToolController.inste.currentTool is HeightBush htb) htb.hardness = hardnessValue;
    }
    public void changeRange(string range)
    {
        float.TryParse(range, out float rangeValue);
        if(ToolController.inste.currentTool is HeightBush htb)  htb.range = rangeValue;
    }
}
