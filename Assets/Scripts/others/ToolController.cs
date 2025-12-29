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
    
    // 用于拖选可视化的 LineRenderer
    private LineRenderer dragVisualizer;
    
    void Start()
    {
        orthographicCamera = Camera.main;
        tools.Add(new SelecterTool("Selecter"));
        tools.Add(new PinTool("TankPin",GameObject.Find("PinTank") ));//tool1 = Pin Tank
        tools.Add(new DragTool("DragSelect", this)); //tool2 = Drag Select
        currentTool = tools[2];
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
            if (Input.mousePosition.x / Screen.width <0.85)
            {
                /*
                Ray ray = orthographicCamera.ScreenPointToRay(Input.mousePosition);
                RaycastHit hit; // �洢������ײ��Ϣ
                Vector3 worldPoint = new Vector3(0, 0, 0);
                if (Physics.Raycast(ray, out hit, Mathf.Infinity,1<<6))//Pinable
                {
                    worldPoint = hit.point;
                    Debug.Log("���������������: " + worldPoint);
                }
                */

                Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
                RaycastHit hit;
                int layerMask = 1 << 6;
                if (Physics.Raycast(ray, out hit, Mathf.Infinity, layerMask))
                {
                    Vector3 hitPosition = hit.point;

                    GameObject hitObject = hit.collider.gameObject;
                    Debug.Log("���������������: " + hitPosition);
                    currentTool.startUse(hitPosition);

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

    public virtual void startUse(Vector3 Position)
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
    
    public override void startUse(Vector3 position)
    {
        base.startUse(position);
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