using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public class ToolController : MonoBehaviour
{
    // Start is called before the first frame update
    public List<Tool> tools = new List<Tool>();
    Tool currentTool;
    public Camera orthographicCamera;
    void Start()
    {
        orthographicCamera = Camera.main;
        tools.Add(new SelecterTool("Selecter"));
        currentTool = tools[0];
        tools.Add(new PinTool("TankPin",GameObject.Find("PinTank") ));//tool1 = Pin Tank
    }
    
    // Update is called once per frame
    void Update()
    {
        if(Input.GetMouseButtonDown(0))
        {
            if (Input.mousePosition.x / Screen.width <0.85)
            {
                Ray ray = orthographicCamera.ScreenPointToRay(Input.mousePosition);
                RaycastHit hit; // 存储射线碰撞信息
                Vector3 worldPoint = new Vector3(0, 0, 0);
                if (Physics.Raycast(ray, out hit, Mathf.Infinity))
                {
                    worldPoint = hit.point;
                    Debug.Log("鼠标点击的世界坐标: " + worldPoint);
                }
                currentTool.startUse(worldPoint);
            }
        }
        if(Input.GetMouseButtonUp(0))
        {
            currentTool.EndUse();
        }
    }
    public void setToolPinTank()
    {
        Debug.Log("set Tool to TankPiun");
        currentTool = tools[1];
    }
    public void setToolSelector()
    {
        currentTool = tools[0];
        UIManager.instance.disVisableAll();
    }
}

public class Tool
{
    public string m_name;
    public Tool(string name)
    {
        m_name = name;
    }

    public virtual void startUse(Vector3 Position)
    {
        Debug.Log("tryUse");
    }
    public void EndUse()
    {
        Debug.Log("EndUse");
    }
}

public class emptyTool : Tool
{
    public emptyTool(string name) : base(name)
    {
    }
}

public class PinTool : Tool
{
    public PinTool(string name) : base(name)
    {
        pinObject = null;
    }
    public PinTool(string name,GameObject mgo) : base(name)
    {
        pinObject = mgo;
    }
    public GameObject pinObject;
     public override void startUse(Vector3 position)
    {
        base.startUse(position);
        Debug.Log("try use piner");
        pinObject.transform.position = position;
    }
}

public class SelecterTool : Tool
{
    public SelecterTool(string name) : base(name)
    {
    }
}