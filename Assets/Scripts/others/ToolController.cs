using System;
using System.Collections;
using System.Collections.Generic;
using System.Xml.Serialization;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static UnityEngine.UI.Image;

public class ToolController : MonoBehaviour
{
    // Start is called before the first frame update
    public List<Tool> tools = new List<Tool>();
    Tool currentTool;
    public Camera orthographicCamera;
    public static ToolController inste;
    
    // 用于拖选可视化的 LineRenderer
    private LineRenderer dragVisualizer;

    private SideTool sdt =new SideTool();


    public MapItem miSelected;
    
    void Start()
    {
        inste = this;
        orthographicCamera = Camera.main;
        tools.Add(new SelecterTool("Selecter"));
        tools.Add(new PinTool("TankPin",GameObject.Find("PinTank") ));//tool1 = Pin Tank
        tools.Add(new DrawerTool("DrawerSelect", this)); //tool2 = PainterBuilding
        currentTool = tools[0];
        // 创建拖选可视化对象
        CreateDragVisualizer();
    }
    
    void CreateDragVisualizer()
    {
        GameObject visualizerObj = new GameObject("DragVisualizer");
        visualizerObj.transform.SetParent(transform);
        dragVisualizer = visualizerObj.AddComponent<LineRenderer>();
        
        // 设置 LineRenderer 属性 - 更粗更醒目
        dragVisualizer.positionCount = 5; // 矩形需要5个点（首尾相连）
        dragVisualizer.startWidth = 1.5f; // 增加线宽
        dragVisualizer.endWidth = 1.5f;
        dragVisualizer.useWorldSpace = true;
        dragVisualizer.loop = true;
        
        // 创建一个不受深度影响的材质（始终显示在最前面）
        Shader shader = Shader.Find("Hidden/Internal-Colored");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        
        Material lineMaterial = new Material(shader);
        lineMaterial.color = new Color(0f, 1f, 1f, 1f); // 青色，更醒目
        
        // 设置渲染队列为 Overlay，确保在所有物体之上
        lineMaterial.renderQueue = 4000; // Overlay 队列
        
        // 禁用深度测试，使其始终显示在最前面
        lineMaterial.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.Always);
        lineMaterial.SetInt("_ZWrite", 0);
        
        dragVisualizer.material = lineMaterial;
        dragVisualizer.startColor = new Color(0f, 1f, 1f, 1f); // 青色
        dragVisualizer.endColor = new Color(1f, 1f, 0f, 1f); // 黄色渐变
        
        // 设置阴影相关
        dragVisualizer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        dragVisualizer.receiveShadows = false;
        
        // 默认隐藏
        dragVisualizer.enabled = false;
    }
    
    // Update is called once per frame
    void Update()
    {
        if(Input.GetMouseButtonDown(0))
        {
            if(sdt.state!=0)
            {
                sdt.state = 0;
            }else
            { 

            if (Input.mousePosition.x / Screen.width < 0.85)
            {

                Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
                RaycastHit hit;
                int layerMask = 1 << 6;
                if (Physics.Raycast(ray, out hit, Mathf.Infinity, layerMask))
                {
                    Vector3 hitPosition = hit.point;

                    GameObject hitObject = hit.collider.gameObject.transform.root.gameObject;
                    Debug.Log("ToolManager:hit at" + hitObject.ToSafeString());
                    currentTool.startUse(hitPosition, hitObject);

                }

            }
            }
        }
        
        // 拖动过程中更新工具
        if (Input.GetMouseButton(0))
        {
            if (Input.mousePosition.x / Screen.width < 0.85)
            {
                Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
                RaycastHit hit;
                int layerMask = 1 << 6;
                if (Physics.Raycast(ray, out hit, Mathf.Infinity, layerMask))
                {
                    Vector3 currentPosition = hit.point;
                    currentTool.OnDragging(currentPosition);
                }
            }
        }
        
        if(Input.GetMouseButtonUp(0))
        {
            currentTool.EndUse();
        }

        if(Input.GetKeyDown(KeyCode.G))
        {
            sdt.tChangeMode(1);
        }

        if (Input.GetKeyDown(KeyCode.R))
        {
            sdt.tChangeMode(2);

        }

        if (Input.GetKeyDown(KeyCode.S))
        {
            sdt.tChangeMode(3);
        }

        sdt.mi = miSelected;

        Vector2 ofst = new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y"));
        
        sdt.update(ofst);
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
    
    public void setToolDrag()
    {
        Debug.Log("set Tool to DragSelect");
        currentTool = tools[2];
    }

    public void setToolWithIndex(int ind)
    {
        currentTool = tools[ind];
    }

    public GameObject InsOnePref(GameObject partten)
    {
        return Instantiate(partten);
    }
    // 供工具访问可视化器
    public LineRenderer GetDragVisualizer()
    {
        return dragVisualizer;
    }
}

public class Tool
{
    public string m_name;
    public Tool(string name)
    {
        m_name = name;
    }

    public virtual void startUse(Vector3 Position,GameObject hitO)
    {
        Debug.Log("tryUse");
    }
    
    public virtual void OnDragging(Vector3 currentPosition)
    {
        // 默认实现：什么都不做
    }
    
    public virtual void EndUse()
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
     public override void startUse(Vector3 position,GameObject hitObject)
    {
        Debug.Log("try use piner");
        pinObject.transform.position = position;
    }
}

public class SelecterTool : Tool
{
    public SelecterTool(string name) : base(name)
    {

    }
    public override void startUse(Vector3 position, GameObject hitO)
    {
        if (hitO.GetComponent<MapItem>()!= null) ;
        ToolController.inste.miSelected = hitO.GetComponent<MapItem>();
    }


}

public class DragTool : Tool
{
    private ToolController controller;
    private LineRenderer visualizer;
    private Vector3 startPosition;
    private Vector3 currentPosition;
    private bool isDragging = false;
    
    // 用于存储选中的对象
    public List<GameObject> selectedObjects = new List<GameObject>();
    
    public DragTool(string name, ToolController toolController) : base(name)
    {
        controller = toolController;
    }
    
    public override void startUse(Vector3 position, GameObject hitO)
    {
        Debug.Log($"开始拖选，起点: {position}");
        
        startPosition = position;
        currentPosition = position;
        isDragging = true;
        
        // 获取可视化器
        visualizer = controller.GetDragVisualizer();
        if (visualizer != null)
        {
            visualizer.enabled = true;
            UpdateVisualizer();
        }
        
        // 清空之前的选择
        selectedObjects.Clear();
    }
    
    public override void OnDragging(Vector3 position)
    {
        if (!isDragging) return;
        
        currentPosition = position;
        UpdateVisualizer();
        
        // 可以在这里实时更新选中的对象
        // UpdateSelection();
    }
    
    public override void EndUse()
    {
        base.EndUse();
        
        if (!isDragging) return;
        
        Debug.Log($"拖选结束，起点: {startPosition}, 终点: {currentPosition}");
        isDragging = false;
        
        // 隐藏可视化器
        if (visualizer != null)
        {
            visualizer.enabled = false;
        }
        
        // 执行选择逻辑
        PerformSelection();
    }
    
    private void UpdateVisualizer()
    {
        if (visualizer == null) return;
        
        // 创建矩形的四个角点（在较高的位置，确保可见）
        // 使用固定高度，保证始终在地形上方
        float y = Mathf.Max(startPosition.y, currentPosition.y) + 10f; // 抬高10单位，确保可见
        
        Vector3 p1 = new Vector3(startPosition.x, y, startPosition.z);
        Vector3 p2 = new Vector3(currentPosition.x, y, startPosition.z);
        Vector3 p3 = new Vector3(currentPosition.x, y, currentPosition.z);
        Vector3 p4 = new Vector3(startPosition.x, y, currentPosition.z);
        
        // 设置矩形的5个点（首尾相连）
        visualizer.SetPosition(0, p1);
        visualizer.SetPosition(1, p2);
        visualizer.SetPosition(2, p3);
        visualizer.SetPosition(3, p4);
        visualizer.SetPosition(4, p1); // 闭合矩形
        
        // 可选：添加脉动效果
        float pulse = 1f + 0.2f * Mathf.Sin(Time.time * 5f);
        visualizer.startWidth = 1.5f * pulse;
        visualizer.endWidth = 1.5f * pulse;
    }
    
    private void PerformSelection()
    {
        // 获取拖选框的范围
        float minX = Mathf.Min(startPosition.x, currentPosition.x);
        float maxX = Mathf.Max(startPosition.x, currentPosition.x);
        float minZ = Mathf.Min(startPosition.z, currentPosition.z);
        float maxZ = Mathf.Max(startPosition.z, currentPosition.z);
        
        Debug.Log($"选择范围: X[{minX:F2}, {maxX:F2}], Z[{minZ:F2}, {maxZ:F2}]");
        
        // 查找范围内的所有对象（Layer 6 - Pinable）
        Collider[] colliders = Physics.OverlapBox(
            new Vector3((minX + maxX) / 2, startPosition.y, (minZ + maxZ) / 2),
            new Vector3((maxX - minX) / 2, 10f, (maxZ - minZ) / 2),
            Quaternion.identity,
            1 << 6 // Layer 6 - Pinable
        );
        
        selectedObjects.Clear();
        foreach (Collider col in colliders)
        {
            selectedObjects.Add(col.gameObject);
            Debug.Log($"选中对象: {col.gameObject.name} at {col.transform.position}");
            
            // 可以在这里添加视觉反馈，例如高亮显示
            // HighlightObject(col.gameObject);
        }
        
        Debug.Log($"共选中 {selectedObjects.Count} 个对象");
    }
    
    // 可选：为选中的对象添加高亮效果
    private void HighlightObject(GameObject obj)
    {
        // 例如：改变颜色、添加轮廓等
        Renderer renderer = obj.GetComponent<Renderer>();
        if (renderer != null)
        {
            // 可以修改材质颜色或添加特效
        }
    }
}

public class DrawerTool : Tool
{
    private ToolController controller;
    private LineRenderer visualizer;
    private Vector3 startPosition;
    private Vector3 currentPosition;
    private bool isDragging = false;
    private GameObject hittenObject;


    public DrawerTool(string name, ToolController toolController) : base(name)
    {
        controller = toolController;
    }

    public override void startUse(Vector3 position, GameObject hitO)
    {
        Debug.Log($"开始拖选，起点: {position}");

        startPosition = position;
        currentPosition = position;
        hittenObject = hitO;
        isDragging = true;

        // 获取可视化器
        visualizer = controller.GetDragVisualizer();
        if (visualizer != null)
        {
            visualizer.enabled = true;
            UpdateVisualizer();
        }
    }

    public override void OnDragging(Vector3 position)
    {
        if (!isDragging) return;

        currentPosition = position;
        UpdateVisualizer();

    }

    public override void EndUse()
    {
        base.EndUse();

        if (!isDragging) return;

        Debug.Log($"拖选结束，起点: {startPosition}, 终点: {currentPosition}");
        isDragging = false;

        // 隐藏可视化器
        if (visualizer != null)
        {
            visualizer.enabled = false;
        }
        createBuilding();
    }

    private void UpdateVisualizer()
    {
        if (visualizer == null) return;

        float y = Mathf.Max(startPosition.y, currentPosition.y) + 10f;

        Vector3 p1 = new Vector3(startPosition.x, y, startPosition.z);
        Vector3 p2 = new Vector3(currentPosition.x, y, startPosition.z);
        Vector3 p3 = new Vector3(currentPosition.x, y, currentPosition.z);
        Vector3 p4 = new Vector3(startPosition.x, y, currentPosition.z);

        visualizer.SetPosition(0, p1);
        visualizer.SetPosition(1, p2);
        visualizer.SetPosition(2, p3);
        visualizer.SetPosition(3, p4);
        visualizer.SetPosition(4, p1); // 闭合矩形

        visualizer.startWidth = 1.5f ;
        visualizer.endWidth = 1.5f ;
    }

    private void createBuilding()
    {
        GameObject go = ToolController.inste.InsOnePref(MapImporter.instate.BuildingPref);
        Building bd = go.GetComponent<Building>();
        bd.layerIndex = 1;
        if(hittenObject.GetComponent<MapItem>() != null)
        {
            bd.layerIndex = hittenObject.GetComponent<MapItem>().layerIndex + 1;
        }
        bd.position = new Vector2( startPosition.x,startPosition.z);
        Vector3 dis = currentPosition - startPosition;
        if (Mathf.Approximately(dis.x, 0f)) dis.x = 0.001f;
        if (Mathf.Approximately(dis.z, 0f)) dis.z = 0.001f;
        bd.size = new Vector2 (Mathf.Abs( dis.x), Mathf.Abs(dis.z) );
        
        if(dis.x < 0f)
        {
            if (dis.z < 0f)
            {
                bd.rotation = 270;
                bd.size = new Vector2(bd.size.y, bd.size.x);
            }
            else
            {
                bd.rotation = 180;
            }
        }
        else
        {
            if(dis.z < 0f)
            {
                bd.rotation = 0;
                
            }
            else
            {
                bd.rotation = 90;
                bd.size = new Vector2(bd.size.y, bd.size.x);
            }
        }

        bd.material = "BuildingWhite2";
        bd.height = 2;
        MetaMap.instance.defaultLayer.mapItems.Add(bd);
        bd.id = MetaMap.instance.getNewItemId("building");
        bd.scatterThis();
        ToolController.inste.miSelected = bd;

    }

}

public class SideTool
{
    public MapItem mi=null;
    public int state = 0;//0null;1g;2r;3s
    public SideTool()
    {

    }
    public void tChangeMode(int i)
    {
        Debug.Log("ToolController:try change " + i);
        if (i == state)
        {
            state = 0;
        }
        else 
        {
            state = i;
        }
    }

    public void update(Vector2 offset )
    {
        if (mi == null) return;
        if (state == 1)//g
        {

            MeRect mr = mi as MeRect;
            if (mr != null)
            {
                mr.position = mr.position + offset*1;
            }

        }
        if(state == 2)//r
        {
            MeRect mr = mi as MeRect;
            if (mr != null)
            {
                mr.rotation = mr.rotation - offset.x * 1;
            }
        }
        mi.scatterThis();
    }

}