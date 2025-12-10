using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public class ToolController : MonoBehaviour
{
    public static ToolController instance;
    public List<Tool> tools = new List<Tool>();
    Tool currentTool;
    public Camera orthographicCamera;

    public MapItem currentMapItem;

    Vector3 lastMousePosition;
    float shaftSpeed = 1f;//鼠标移动对应物体移动速度缩放
    private bool jButtonIdentifier;
    // Start is called before the first frame update
    void Start()
    {
        instance = this;
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
                /*
                Ray ray = orthographicCamera.ScreenPointToRay(Input.mousePosition);
                RaycastHit hit; // 存储射线碰撞信息
                Vector3 worldPoint = new Vector3(0, 0, 0);
                if (Physics.Raycast(ray, out hit, Mathf.Infinity,1<<6))//Pinable
                {
                    worldPoint = hit.point;
                    Debug.Log("鼠标点击的世界坐标: " + worldPoint);
                }
                */

                Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
                RaycastHit hit;
                int layerMask = 1 << 6;
                if (Physics.Raycast(ray, out hit, Mathf.Infinity, layerMask))
                {
                    Vector3 hitPosition = hit.point;

                    GameObject hitObject = hit.collider.gameObject;
                    Debug.Log("鼠标点击的世界坐标: " + hitPosition);
                    currentTool.startUse(hitPosition);

                }

            }
        }
        if(Input.GetMouseButtonUp(0))
        {
            currentTool.EndUse();
        }
        if (Input.GetKeyDown(KeyCode.J))
        {
            if (currentMapItem != null)
            {
                if (jButtonIdentifier == false)
                {
                    jButtonIdentifier = true;
                    lastMousePosition = Input.mousePosition;
                }
                else
                {
                    jButtonIdentifier = false;
                    Syncer.instance.ScatterMapItems();
                }

            }
        }
        if (jButtonIdentifier == true)
        {
            if (Input.mousePosition != lastMousePosition)
            {
                Vector3 delta = Input.mousePosition - lastMousePosition;
                //Debug.Log($"鼠标移动了: {delta}");
                lastMousePosition = Input.mousePosition;
                if (currentMapItem.gameObject.GetComponent<MeRect>() != null)
                {
                    currentMapItem.gameObject.GetComponent<MeRect>().position += new Vector2(delta.x, delta.y) * shaftSpeed;
                    currentMapItem.scatterThis();
                }
                if (currentMapItem.gameObject.GetComponent<Platform>() != null) 
                {
                    for (int i = 0; i < currentMapItem.gameObject.GetComponent<Platform>().positinLineL.Count; i++) 
                    {
                        currentMapItem.gameObject.GetComponent<Platform>().positinLineL[i] += new Vector2(delta.x, delta.y) * shaftSpeed;
                    }
                    for (int i = 0; i < currentMapItem.gameObject.GetComponent<Platform>().positinLineR.Count; i++)
                    {
                        currentMapItem.gameObject.GetComponent<Platform>().positinLineR[i] += new Vector2(delta.x, delta.y) * shaftSpeed;
                    }
                    currentMapItem.scatterThis();
                }
                //Debug.Log($"currentMapItem Postion: {currentMapItem.gameObject.GetComponent<MeRect>().position}");
            }
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
    public override void startUse(Vector3 position)
    {
        base.startUse(position);
        Debug.Log("try use selecter");
        Vector2 vectorTransformer = new Vector2(position.x, position.z);
        ToolController.instance.currentMapItem = VpMetaToucher.GetMapItemUnderPosition(vectorTransformer);
    }
}