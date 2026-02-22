
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;

public class ToolController : MonoBehaviour
{
    // Start is called before the first frame update
    public List<Tool> tools = new List<Tool>();
    Tool currentTool;
    public Camera orthographicCamera;
    public static ToolController inste;

    // 用于拖选可视化的 LineRenderer
    public LineRenderer dragVisualizer;

    private SideTool sdt = new SideTool();

    private Dictionary<KeyCode, int> toolShortcutMap;

    public MapItem miSelected;
    private MapItem lastMiS;

    // 用于存储复制的建筑信息
    private Building copiedBuilding = null;

    void Start()
    {
        inste = this;
        orthographicCamera = Camera.main;
        tools.Add(new SelecterTool("Selecter"));
        tools.Add(new PinTool("TankPin", GameObject.Find("PinTank")));//tool1 = Pin Tank
        tools.Add(new DrawerTool("DrawerSelect", this)); //tool2 = PainterBuilding
        tools.Add(new RoofChangerTool("RoofChanger", this)); //tool3 = roof changer
        tools.Add(new MaterialChangerTool("BuildingMaterialChanger", this)); //tool4 = material changer
        tools.Add(new heightChanger("heightChanger", this)); //tool5 = material changer
        tools.Add(new WallDrawer("wallDrawer"));//tool6 wall pather
        tools.Add(new PlatformDrawer("PlatformDrawer"));//tool7 platform pather
        tools.Add(new PlatformTypeChanger("PlatformTypeChanger", this)); //tool8 = pt changer
        tools.Add(new PlatformBasewallChanger("PlatformBasewallChanger")); //tool9 = pt baseWallType changer
        tools.Add(new PlatformHeightSetter("PlatformHeightSetter")); //tool 10 = pt heightSetter
        tools.Add(new BaseTool("PlatformHeightSetter",this)); //tool 11 = base drawer
        tools.Add(new ItemScatter("Scatter"));//tool 12 SpawnPosition pointer
        tools.Add(new Eraser("Eraser", this)); //tool 13 = SpawnerEraser
        tools.Add(new MeshScatter("Mesh Scatter")); //tool 14 = MeshScatter
        tools.Add(new TerrainMaterialPainter("Terrain painter")); //tool 15 = terrainPainter
        tools.Add(new HeightBush("HBS")); //tool 16 = terrainPainter
        tools.Add(new HeightSmudge("HS")); //tool 17 = terrainSmudge

        currentTool = tools[0];

        toolShortcutMap = new Dictionary<KeyCode, int>
        {
            { KeyCode.Alpha1, 0  },  // Selecter
            { KeyCode.Alpha2, 1  },  // TankPin
            { KeyCode.Alpha3, 2  },  // DrawerSelect (Building)
            { KeyCode.Alpha4, 3  },  // RoofChanger
            { KeyCode.Alpha5, 4  },  // MaterialChanger
            { KeyCode.Alpha6, 5  },  // HeightChanger
            { KeyCode.Alpha7, 6  },  // WallDrawer
            { KeyCode.Alpha8, 7  },  // PlatformDrawer
            { KeyCode.Alpha9, 8  },  // PlatformTypeChanger
            { KeyCode.Alpha0, 9  },  // PlatformBasewallChanger
            { KeyCode.F1,     10 },  // PlatformHeightSetter
            { KeyCode.F2,     11 },  // BaseTool
            { KeyCode.F3,     12 },  // ItemScatter
            { KeyCode.F4,     13 },  // Eraser
            { KeyCode.F5,     14 },  // MeshScatter
            { KeyCode.F6,     15 },  // TerrainMaterialPainter
            { KeyCode.F7,     16 },  // HeightBush (HBS)
            { KeyCode.F8,     17 },  // HeightSmudge
        };

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
        dragVisualizer.loop = false;

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


    void OnGUI()
    {
        if (currentTool.m_name == "HBS")
        {
            Rect rect = new Rect(500, 10, 400, 30);
            HeightBush htb = currentTool as HeightBush;

            GUI.color = Color.white;
            GUI.Label(rect, "height:" + htb.height+" hardness:"+htb.hardness +" range:"+htb.range);
        }
    }

        // Update is called once per frame
        void Update()
    {

        if (currentTool.m_name == "HBS")
        {
            HeightBush htb = currentTool as HeightBush;

            if (Input.GetKey(KeyCode.Equals))
            {
                if (Input.GetKey(KeyCode.LeftControl))
                {
                    htb.height += 0.002f;
                    htb.height = Mathf.Clamp(htb.height, 0, 1);
                }
                else if (Input.GetKey(KeyCode.LeftShift))
                {
                    htb.hardness += 0.002f;
                    htb.hardness = Mathf.Clamp(htb.hardness, 0, 50);

                }
                else
                {
                    htb.range += 1f;
                    htb.range = Mathf.Clamp(htb.range, 0, 1000);

                }
            }
            if(Input.GetKey(KeyCode.Minus))
            {
                if (Input.GetKey(KeyCode.LeftControl))
                {
                    htb.height -= 0.002f;
                    htb.height = Mathf.Clamp(htb.height, 0, 1);

                }
                else if (Input.GetKey(KeyCode.LeftShift))
                {
                    htb.hardness -= 0.002f;
                    htb.hardness = Mathf.Clamp(htb.hardness, 0, 50);

                }
                else
                {
                    htb.range -= 1f;
                    htb.range = Mathf.Clamp(htb.range, 0, 1000);

                }
            }
        }
            if (lastMiS!=miSelected)
        {
            //selectAnothermi
            Transform can = miSelected.transform.Find("Canvas");
            if(can!=null)
            {
                UIManager.instance.changeShowingCanvas(can.gameObject.GetComponent<Canvas>());
            }
            else
            {
                UIManager.instance.changeShowingCanvas(null);
            }
        }
        lastMiS = miSelected;
        if (Input.GetMouseButtonDown(0))
        {
            if (sdt.state != 0)
            {
                sdt.state = 0;
            }
            else
            {

                if ( (Input.mousePosition.x / Screen.width < 0.82 ) && (Input.mousePosition.x / Screen.width > 0.21 || Input.mousePosition.y / Screen.height > 0.23) )
                {

                    Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
                    RaycastHit hit;
                    int layerMask = 1 << 6;
                    if(currentTool is SelecterTool)
                    {
                        layerMask = (1 << 6) | (1 << 7);
                    }
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

        if (Input.GetMouseButtonUp(0))
        {
            currentTool.EndUse();
        }

        if (Input.GetKeyDown(KeyCode.G))
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

        if (Input.GetKeyDown(KeyCode.Delete))
        {
            if(!MetaMap.instance.defaultLayer.mapItems.Remove(miSelected))
            {
                //base
                MetaMap.instance.baseLayer.mapItems.Remove(miSelected);
            }
            Destroy(miSelected.gameObject.transform.root.gameObject);
        }

        // 复制功能 (Ctrl+C)
        if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl))
        {
            if (Input.GetKeyDown(KeyCode.C))
            {
                CopySelectedBuilding();
            }
            
            // 粘贴功能 (Ctrl+V)
            if (Input.GetKeyDown(KeyCode.V))
            {
                PasteBuilding();
            }
        }

        if(Input.GetKeyDown(KeyCode.Space))
        {
            currentTool.space();
        }
        if(Input.GetKeyDown(KeyCode.Escape))
        {
            currentTool.escape();
        }

        HandleToolShortcuts();

        sdt.mi = miSelected;

        Vector2 ofst = new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y"));

        Vector3 pixelPos = Input.mousePosition;
        float normalizedX = pixelPos.x / Screen.width;
        float normalizedY = pixelPos.y / Screen.height;
        Vector2 gv2 = new Vector2(normalizedX, normalizedY);
        sdt.update(ofst, gv2);
    }
    private void HandleToolShortcuts()
    {
        // 不在 Ctrl 组合键时才响应工具切换，避免与 Ctrl+C/V 等冲突
        if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) return;

        foreach (var kv in toolShortcutMap)
        {
            if (Input.GetKeyDown(kv.Key))
            {
                setToolWithIndex(kv.Value);
                break;
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
    public void setToolDrag()
    {
        Debug.Log("set Tool to DragSelect");
        currentTool = tools[2];
    }
    public void setToolWithIndex(int ind)
    {
        Debug.Log("ToolController:" + tools[ind].GetType().Name);
        currentTool = tools[ind];
    }
    public void setHeightSetter(int offset)
    {
        currentTool = tools[5];
        heightChanger hc = tools[5] as heightChanger;
        hc.offcc = offset;
    }

    public void setScatterType(string type)
    {
        System.Type tp = System.Type.GetType(type);
        if (tp != null)
        {
            Debug.Log("ToolController set type as " + tp.Name);
            ItemScatter isr =  tools[12] as ItemScatter;
            isr.setType(tp);
            setToolWithIndex(12);
        }
    }
    public void setEraserType(string type)
    {
        System.Type tp = System.Type.GetType(type);
        if (tp != null)
        {
            Debug.Log("ToolController set type as " + tp.Name);
            Eraser isr = tools[13] as Eraser;
            isr.setType(tp);
            setToolWithIndex(13);
        }
    }

    public void setTPterHei(string hei)
    {
        TerrainMaterialPainter tmp =  tools[15] as TerrainMaterialPainter;
        tmp.tarind =  int.Parse(hei);
    }
    public void setTPterRng(string ran)
    {
        TerrainMaterialPainter tmp = tools[15] as TerrainMaterialPainter;
        tmp.radius = float.Parse(ran);
    }
    public void setMeshScatterTool(int index)
    {
        MeshScatter ms = tools[14] as MeshScatter;
        ms.ChooseThis(MetaMap.instance.meshTemplates[index].name);
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

    // 复制当前选中的建筑
    private void CopySelectedBuilding()
    {
        if (miSelected == null)
        {
            Debug.Log("没有选中的对象可以复制");
            return;
        }

        Building selectedBuilding = miSelected as Building;
        if (selectedBuilding == null)
        {
            Debug.Log("选中的对象不是建筑，无法复制");
            return;
        }

        // 复制建筑信息（深拷贝所有属性）
        copiedBuilding = selectedBuilding;
        Debug.Log($"已复制建筑: {copiedBuilding.id}, 材质: {copiedBuilding.material}, 高度: {copiedBuilding.height}");
    }

    // 粘贴建筑到鼠标位置
    private void PasteBuilding()
    {
        if (copiedBuilding == null)
        {
            Debug.Log("没有复制的建筑可以粘贴");
            return;
        }

        // 获取鼠标位置（只在主画面区域粘贴）
        if (Input.mousePosition.x / Screen.width >= 0.85)
        {
            Debug.Log("不能在UI区域粘贴");
            return;
        }

        // 使用射线检测获取鼠标在世界中的位置
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;
        int layerMask = 1 << 6; // Layer 6 - Pinable

        if (Physics.Raycast(ray, out hit, Mathf.Infinity, layerMask))
        {
            Vector3 pastePosition3D = hit.point;
            
            // 创建新建筑
            GameObject newBuildingGO = Instantiate(MapImporter.instate.BuildingPref);
            Building newBuilding = newBuildingGO.GetComponent<Building>();

            // 复制所有属性
            Vector2 pastePosition2D = MathOfRwrme.U3dPosToSvgPos(new Vector2(pastePosition3D.x, pastePosition3D.z));
            
            newBuilding.height = copiedBuilding.height;
            newBuilding.material = copiedBuilding.material;
            newBuilding.position = pastePosition2D;
            newBuilding.rotation = copiedBuilding.rotation;
            newBuilding.size = copiedBuilding.size;
            newBuilding.roof = copiedBuilding.roof;
            
            // 确定图层
            newBuilding.layerIndex = copiedBuilding.layerIndex;
            if (hit.collider.gameObject.GetComponent<MapItem>() != null)
            {
                MapItem hitItem = hit.collider.gameObject.GetComponent<MapItem>();
                newBuilding.layerIndex = hitItem.layerIndex + 1;
            }

            // 生成新ID
            newBuilding.id = MetaMap.instance.getNewItemId("building");

            // 添加到地图
            MetaMap.instance.defaultLayer.mapItems.Add(newBuilding);
            
            // 刷新显示
            newBuilding.scatterThis();
            
            // 选中新建筑
            miSelected = newBuilding;

            // 记录撤销点
            CtrlZer.instance.checkPoint();

            Debug.Log($"已粘贴建筑到位置: {pastePosition2D}, ID: {newBuilding.id}");
        }
        else
        {
            Debug.Log("无法确定粘贴位置");
        }
    }
}
public class Tool
{
    public string m_name;
    public Tool(string name)
    {
        m_name = name;
    }

    public virtual void startUse(Vector3 Position, GameObject hitO)
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

    public virtual void space()
    {

    }
    public virtual void escape()
    {

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
    public PinTool(string name, GameObject mgo) : base(name)
    {
        pinObject = mgo;
    }
    public GameObject pinObject;
    public override void startUse(Vector3 position, GameObject hitObject)
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
        if (hitO.GetComponent<MapItem>() != null)
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
        visualizer.positionCount = 0;
        // 创建矩形的四个角点（在较高的位置，确保可见）
        // 使用固定高度，保证始终在地形上方
        float y = Mathf.Max(startPosition.y, currentPosition.y) + 10f; // 抬高10单位，确保可见

        Vector3 p1 = new Vector3(startPosition.x, y, startPosition.z);
        Vector3 p2 = new Vector3(currentPosition.x, y, startPosition.z);
        Vector3 p3 = new Vector3(currentPosition.x, y, currentPosition.z);
        Vector3 p4 = new Vector3(startPosition.x, y, currentPosition.z);
        visualizer.positionCount = 5;
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

        visualizer.startWidth = 1.5f;
        visualizer.endWidth = 1.5f;
    }

    private void createBuilding()
    {
        GameObject go = ToolController.inste.InsOnePref(MapImporter.instate.BuildingPref);
        Building bd = go.GetComponent<Building>();
        bd.layerIndex = 1;
        if (hittenObject.GetComponent<MapItem>() != null)
        {
            bd.layerIndex = hittenObject.GetComponent<MapItem>().layerIndex + 1;
        }
        Vector2 dis = MathOfRwrme.U3dPosToSvgPos(new Vector2(currentPosition.x, currentPosition.z)) - MathOfRwrme.U3dPosToSvgPos(new Vector2(startPosition.x, startPosition.z));
        bd.position = MathOfRwrme.U3dPosToSvgPos(new Vector2(startPosition.x, startPosition.z));
        if (Mathf.Approximately(dis.x, 0f)) dis.x = 0.001f;
        if (Mathf.Approximately(dis.y, 0f)) dis.y = 0.001f;
        bd.size = new Vector2(Mathf.Abs(dis.x), Mathf.Abs(dis.y));

        if (dis.x > 0f)
        {
            if (dis.y > 0f)
            {
                //nothing to do
            }
            else
            {
                bd.rotation = 90;
                bd.size = new Vector2(bd.size.y, bd.size.x);
            }
        }
        else
        {
            if (dis.y < 0f)
            {
                bd.rotation = 180;

            }
            else
            {
                bd.rotation = 270;
                bd.size = new Vector2(bd.size.y, bd.size.x);
            }
        }

        bd.material = "BuildingWhite2";
        bd.height = 2;
        CtrlZer.instance.checkPoint();
        MetaMap.instance.defaultLayer.mapItems.Add(bd);
        bd.id = MetaMap.instance.getNewItemId("building");
        bd.scatterThis();
        ToolController.inste.miSelected = bd;
    }

}
public class BaseTool : Tool
{
    private ToolController controller;
    private LineRenderer visualizer;
    private Vector3 startPosition;
    private Vector3 currentPosition;
    private bool isDragging = false;
    private GameObject hittenObject;


    public BaseTool(string name, ToolController toolController) : base(name)
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

        isDragging = false;

        if (visualizer != null)
        {
            visualizer.enabled = false;
        }
        createBase();
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

        visualizer.startWidth = 1.5f;
        visualizer.endWidth = 1.5f;
    }

    private void createBase()
    {
        GameObject go = ToolController.inste.InsOnePref(MapImporter.instate.BasePref);
        Base bs = go.GetComponent<Base>();
        Vector2 dis = MathOfRwrme.U3dPosToSvgPos(new Vector2(currentPosition.x, currentPosition.z)) - MathOfRwrme.U3dPosToSvgPos(new Vector2(startPosition.x, startPosition.z));
        bs.position = MathOfRwrme.U3dPosToSvgPos(new Vector2(startPosition.x, startPosition.z));
        if (Mathf.Approximately(dis.x, 0f)) dis.x = 0.001f;
        if (Mathf.Approximately(dis.y, 0f)) dis.y = 0.001f;
        bs.size = new Vector2(Mathf.Abs(dis.x), Mathf.Abs(dis.y));
        
        if(bs.size.x < 0f)
        {
            bs.size.x = bs.size.x * -1;
            bs.position.x = bs.position.x - bs.size.x;
        }
        if (bs.size.y < 0f)
        {
            bs.size.y = bs.size.y * -1;
            bs.position.y = bs.position.y - bs.size.y;
        }


        CtrlZer.instance.checkPoint();
        bs.id = MetaMap.instance.getNewItemId("base");
        bs._name = "newbase";
        bs.factionIndex = -1;
        MetaMap.instance.baseLayer.mapItems.Add(bs);
        bs.scatterThis();
        ToolController.inste.miSelected = bs;
    }

}
public class RoofChangerTool : Tool
{
    private ToolController controller;
    public bool buserstat = false;
    public RoofChangerTool(string name, ToolController toolController) : base(name)
    {
        controller = toolController;
    }

    public override void startUse(Vector3 position, GameObject hitO)
    {
        if (hitO != null)
        {
            Building bd = hitO.GetComponent<Building>();
            if (bd != null)
            {
                CtrlZer.instance.checkPoint();
                bd.roof = !bd.roof;
                bd.scatterThis();
            }

        }
    }

}
public class MaterialChangerTool : Tool
{
    private ToolController controller;
    public mapItemType bt;
    public MaterialChangerTool(string name, ToolController toolController) : base(name)
    {
        controller = toolController;
    }

    public void setMat(mapItemType material)
    {
        Debug.Log("ToolController:mt set as " + material.name);

        bt = material;
    }

    public override void startUse(Vector3 position, GameObject hitO)
    {
        if (hitO != null && bt != null)
        {
            MapItem mi = hitO.GetComponent<MapItem>();
            if (mi == null) return;

            if (mi is Platform plt)
            {
                if(this.bt is PlatformSerfaceType pst)
                {
                    plt.top_material = pst.name;
                }
                else if (this.bt is WallType wt)
                {
                    plt.wall_template = wt.name;
                }
                mi.scatterThis();
            }
            else
            {
                string min = mi.GetType().Name;
                string mtn = bt.GetType().Name;

                if (min.Substring(0, 4) == mtn.Substring(0, 4))
                {
                    mi.material = bt.name;
                    mi.scatterThis();
                }
            }

        }
    }
}
public class heightChanger : Tool
{
    private ToolController controller;
    public int offcc = 1;
    public heightChanger(string name, ToolController toolController) : base(name)
    {
        controller = toolController;
    }

    public override void startUse(Vector3 position, GameObject hitO)
    {
        if (hitO != null)
        {
            Building bd = hitO.GetComponent<Building>();
            if (bd != null)
            {
                CtrlZer.instance.checkPoint();
                bd.height = bd.height + offcc * 2;
                bd.scatterThis();

            }

        }
    }
}
public class SideTool
{
    public MapItem mi = null;
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

    public void update(Vector2 offset,Vector2 Pos)
    {
        if (mi == null) return;
        if (state == 1)//g
        {

            MeRect mr = mi as MeRect;
            if (mr != null)
            {
                Vector2 tof = new Vector2(offset.x, -1f * offset.y);
                mr.grab(tof);
            }

        }
        if (state == 2)//r
        {
            MeRect mr = mi as MeRect;
            if (mr != null)
            {
                mr.rotate(  (offset.x * (Pos - new Vector2(0.5f, 0.5f)).y - offset.y * (Pos - new Vector2(0.5f, 0.5f)).x)/ (Pos - new Vector2(0.5f, 0.5f)).magnitude);
            }
        }
        if (state == 3)
        {
            MeRect mr = mi as MeRect;
            if (mr != null)
            {
                mr.scale(offset.x);
            }

        }
        mi.scatterThis();
    }
}
public class WallDrawer : Tool
{
    public bool drawing = false;
    public List<Vector3> PointSelected;
    public List<Vector2> PathDrawed;
    int lid = 1;
    public WallDrawer(string name) : base(name)
    {
        drawing = false;
        PathDrawed = new List<Vector2>();
        PointSelected = new List<Vector3>();
    }
    public override void startUse(Vector3 Position, GameObject hitO)
    {
        if(drawing == false)
        {
            ToolController.inste.dragVisualizer.enabled = true;
            lid = 1;
            if(hitO.GetComponent<MapItem>() != null)
            {
                lid = hitO.GetComponent<MapItem>().layerIndex + 1;
            }
        }
        drawing =true;
        PointSelected.Add(Position);
        PathDrawed.Add(MathOfRwrme.U3dPosToSvgPos(Position));
        UpdateVisualizer();
    }
    public override void escape()
    {
            ToolController.inste.dragVisualizer.enabled = false;
        drawing = false;
        PathDrawed.Clear();
        PointSelected.Clear();
    }

    public override void space()
    {
        ToolController.inste.dragVisualizer.enabled = false;
        if (drawing)
        {
            drawing = false;
            GameObject go = ToolController.inste.InsOnePref(MapImporter.instate.WallPref);
            Wall wl = go.GetComponent<Wall>();
            wl.positionLine = new List<Vector2>(PathDrawed) ;
            wl.material = "GardenWall1";
            wl.id = MetaMap.instance.getNewItemId("wall");
            wl.layerIndex = lid;
            PathDrawed.Clear();
            PointSelected.Clear();
            MetaMap.instance.defaultLayer.mapItems.Add(wl);
            wl.scatterThis();
            
        }
    }

    private void UpdateVisualizer()
    {

        if (ToolController.inste.dragVisualizer == null) return;
        ToolController.inste.dragVisualizer.positionCount = 0;
        for (int i = 0; i < PointSelected.Count; i++)
        {
            ToolController.inste.dragVisualizer.positionCount++;
            ToolController.inste.dragVisualizer.SetPosition(i, PointSelected[i]+Vector3.up*5);
        }

    }

}
public class PlatformDrawer : Tool
{
    int drawing = 0;
    int lid;
    List<Vector3> startLine;
    List<Vector2> startLineDrawed;
    List<Vector3> endLine;
    List<Vector2> endLineDrawed;
    //0 stop,1,drawingstart,2:drawing end;
    public PlatformDrawer(string name) : base(name)
    {
        drawing = 0;
        startLine = new List<Vector3>();
        endLine = new List<Vector3>();
        startLineDrawed =new List<Vector2>();
        endLineDrawed =new List<Vector2>();
    }
    public override void startUse(Vector3 Position, GameObject hitO)
    {
        if (drawing == 0)
        {
            ToolController.inste.dragVisualizer.enabled = true;
            lid = 1;
            if (hitO.GetComponent<MapItem>() != null)
            {
                lid = hitO.GetComponent<MapItem>().layerIndex + 1;
            }
            drawing = 1;
        }
        if (drawing == 1)
        {
            startLine.Add(Position);
            startLineDrawed.Add(MathOfRwrme.U3dPosToSvgPos(Position));
            UpdateVisualizer();
        }
        if(drawing==2)
        {
            endLine.Add(Position);
            endLineDrawed.Add(MathOfRwrme.U3dPosToSvgPos(Position));
            UpdateVisualizer();
        }
    }
    public override void escape()
    {
        ToolController.inste.dragVisualizer.enabled = false;
        drawing = 0;
        startLineDrawed.Clear();
        endLineDrawed.Clear();
        startLine.Clear();
        endLine.Clear();
    }

    public override void space()
    {
        
        if (drawing == 2)
        {
            GameObject go = ToolController.inste.InsOnePref(MapImporter.instate.PlatformPref);
            Platform plt = go.GetComponent<Platform>();
            plt.id = MetaMap.instance.getNewItemId("platform");
            plt.layerIndex = lid;
            while(startLineDrawed.Count!=endLineDrawed.Count)
            {
                if(startLineDrawed.Count>endLineDrawed.Count)
                {
                    endLineDrawed.Add(endLineDrawed[endLineDrawed.Count - 1]);
                }
                else
                {
                    startLineDrawed.Add(startLineDrawed[startLineDrawed.Count - 1]);
                }
            }
            ToolController.inste.dragVisualizer.enabled = false;
            plt.positinLineR = new List<Vector2>(startLineDrawed);
            plt.positinLineL = new List<Vector2>(endLineDrawed);
            plt.top_material = "terrain";
            plt.base_wall_template = "CliffWall2";
            plt.wall_template = "StoneWall1";
            plt.wall_height = -1;
            startLineDrawed.Clear();
            endLineDrawed.Clear();
            startLine.Clear();
            endLine.Clear();
            MetaMap.instance.defaultLayer.mapItems.Add(plt);
            plt.scatterThis();
            drawing = 0;
        }
        if (drawing == 1)
        {
            drawing = 2;
        }

    }

    private void UpdateVisualizer()
    {

        if (ToolController.inste.dragVisualizer == null) return;
        ToolController.inste.dragVisualizer.positionCount = 0;
        for (int i = startLine.Count-1; i>=0; i--)
        {
            ToolController.inste.dragVisualizer.positionCount++;
            ToolController.inste.dragVisualizer.SetPosition(startLine.Count-1-i, startLine[i] + Vector3.up * 5);
        }
        for (int i=0;i<endLine.Count;i++)
        {
            ToolController.inste.dragVisualizer.positionCount++;
            ToolController.inste.dragVisualizer.SetPosition(startLine.Count+i, endLine[i] + Vector3.up * 5);
        }

    }

}
public class PlatformTypeChanger : Tool
{
    private ToolController controller;
    public int offcc = 1;
    public PlatformTypeChanger(string name, ToolController toolController) : base(name)
    {
        controller = toolController;
    }

    public override void startUse(Vector3 position, GameObject hitO)
    {
        if (hitO != null)
        {
            Platform bd = hitO.GetComponent<Platform>();
            if (bd != null)
            {
                CtrlZer.instance.checkPoint();
                if(bd.isBridge)
                {
                    bd.isBridge = false;
                    bd.isDeck = true;

                }
                else if(bd.isDeck)
                {
                    bd.isBridge = false;
                    bd.isDeck = false;
                }
                else
                {
                    bd.isBridge = true;
                    bd.isDeck = false;
                }
                bd.scatterThis();

            }

        }
    }
}
public class PlatformBasewallChanger : Tool
{
    public WallType wtp;
    public PlatformBasewallChanger(string name) : base(name)
    {
    }

    public override void startUse(Vector3 position, GameObject hitO)
    {
        if (hitO != null)
        {
            Platform bd = hitO.GetComponent<Platform>();
            if (bd != null)
            {
                CtrlZer.instance.checkPoint();
                bd.base_wall_template = wtp.name;
                bd.scatterThis();

            }

        }
    }
}
public class PlatformHeightSetter : Tool
{
    public float height;
    public PlatformHeightSetter(string name) : base(name)
    {
    }

    public override void startUse(Vector3 position, GameObject hitO)
    {
        if (hitO != null)
        {
            Platform bd = hitO.GetComponent<Platform>();
            if (bd != null)
            {
                CtrlZer.instance.checkPoint();
                bd.height = height;
                bd.scatterThis();

            }

        }
    }
}
public class ItemScatter : Tool
{
    private System.Type itemType;
    public ItemScatter(string name) : base(name)
    {

    }
    public override void startUse(Vector3 position, GameObject hitO)
    {
        if(itemType == typeof(SpawnPoint))
        {
            SpawnPoint sp = ToolController.inste.InsOnePref(MapImporter.instate.SpawnPointPref).GetComponent<SpawnPoint>();

            sp.id = MetaMap.instance.getNewItemId("#spawnrect");

            sp.position = MathOfRwrme.U3dPosToSvgPos(position);
            sp.size = new Vector2(5, 5);
            sp.layerIndex = 1;

            MetaMap.instance.defaultLayer.mapItems.Add(sp);
            sp.scatterThis();
            ToolController.inste.miSelected = sp;
        }
        if(itemType == typeof(Rock))
        {
            Rock rc = ToolController.inste.InsOnePref(MapImporter.instate.RockPref).GetComponent<Rock>();
            rc.id = MetaMap.instance.getNewItemId("#rock");
            rc.layerIndex = 1;
            if (hitO.GetComponent<MapItem>() != null)
            {
                rc.layerIndex = hitO.GetComponent<MapItem>().layerIndex + 1;
            }
            rc.position = MathOfRwrme.U3dPosToSvgPos(position);
            MetaMap.instance.defaultLayer.mapItems.Add(rc);
            rc.scatterThis();
            ToolController.inste.miSelected = rc;

        }
        if(itemType == typeof(Ladder))
        {
            Ladder ld = ToolController.inste.InsOnePref(MapImporter.instate.LadderPref).GetComponent<Ladder>();
            ld.id = MetaMap.instance.getNewItemId("#ladder");
            ld.layerIndex = 1;
            if (hitO.GetComponent<MapItem>() != null)
            {
                ld.layerIndex = hitO.GetComponent<MapItem>().layerIndex + 1;
            }
            ld.position = MathOfRwrme.U3dPosToSvgPos(position);
            MetaMap.instance.defaultLayer.mapItems.Add(ld);
            ld.scatterThis();
            ToolController.inste.miSelected = ld;

        }
        if (itemType == typeof(Vehicle))
        {
            Vehicle sp = ToolController.inste.InsOnePref(MapImporter.instate.VehiclePref).GetComponent<Vehicle>();

            sp.id = MetaMap.instance.getNewItemId("spawn_vehicle");

            sp.position = MathOfRwrme.U3dPosToSvgPos(position)- new Vector2(1.445f*2,1.667f*2);
            sp.layerIndex = 1;
            sp.taged = true;
            sp.key = "jeep";

            MetaMap.instance.defaultLayer.mapItems.Add(sp);
            sp.scatterThis();
            ToolController.inste.miSelected = sp;
        }
        if (itemType == typeof(ItemSupply))
        {
            ItemSupply ld = ToolController.inste.InsOnePref(MapImporter.instate.ItemSupplyPref).GetComponent<ItemSupply>();
            ld.id = MetaMap.instance.getNewItemId("item_supply");
            ld.layerIndex = 1;
            if (hitO.GetComponent<MapItem>() != null)
            {
                ld.layerIndex = hitO.GetComponent<MapItem>().layerIndex + 1;
            }
            ld.position = MathOfRwrme.U3dPosToSvgPos(position);
            ld.size = new Vector2(4, 4);
            MetaMap.instance.defaultLayer.mapItems.Add(ld);
            ld.scatterThis();
            ToolController.inste.miSelected = ld;

        }
        if (itemType == typeof(Crate))
        {
            Crate ld = ToolController.inste.InsOnePref(MapImporter.instate.CratePref).GetComponent<Crate>();
            ld.id = MetaMap.instance.getNewItemId("crate");
            ld.layerIndex = 1;
            if (hitO.GetComponent<MapItem>() != null)
            {
                ld.layerIndex = hitO.GetComponent<MapItem>().layerIndex + 1;
            }
            ld.position = MathOfRwrme.U3dPosToSvgPos(position);
            ld.size = new Vector2(4.9588485f, 4.4664636f);
            MetaMap.instance.defaultLayer.mapItems.Add(ld);
            ld.scatterThis();
            ToolController.inste.miSelected = ld;

        }

    }

    public void setType(System.Type type)
    {
        itemType = type;
    }

}
public class MeshScatter : Tool
{
    string templateName = null;
    public MeshScatter(string name) : base(name)
    {

    }
    public override void startUse(Vector3 position, GameObject hitO)
    {
        MeMesh ms = ToolController.inste.InsOnePref(MapImporter.instate.MeshPref).GetComponent<MeMesh>();
        
        ms.id = MetaMap.instance.getNewItemId("#mesh");
        ms.layerIndex = 1;
        if (hitO.GetComponent<MapItem>() != null)
        {
        ms.layerIndex = hitO.GetComponent<MapItem>().layerIndex + 1;
        }

        ms.position = MathOfRwrme.U3dPosToSvgPos(position);
        ms.templated = true;
        ms.size = MetaMap.instance.meshTemplates.Find(x => x.name == templateName).size;
        ms.template_ref = templateName;

        MetaMap.instance.defaultLayer.mapItems.Add(ms);
        ms.scatterThis();
    }

    public void ChooseThis(string name)
    {
        templateName = name;
        ToolController.inste.setToolWithIndex(14);
    }

}
public class Eraser : Tool
{
    private ToolController controller;
    private LineRenderer visualizer;
    private Vector3 startPosition;
    private Vector3 currentPosition;
    private bool isDragging = false;
    
    private System.Type itemType;

    public Eraser(string name, ToolController toolController) : base(name)
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

        isDragging = false;

        if (visualizer != null)
        {
            visualizer.enabled = false;
        }
        Erase();
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

        visualizer.startWidth = 1.5f;
        visualizer.endWidth = 1.5f;
    }

    private void Erase()
    {
        for (int i = 0; i < MetaMap.instance.defaultLayer.mapItems.Count; i++)
        {
            if(MetaMap.instance.defaultLayer.mapItems[i].GetType()!=itemType)continue;
            if(MetaMap.instance.defaultLayer.mapItems[i] is MeRect sp)
            {
                if(MathOfRwrme.SvgPosToU3dPos(sp.position).x>startPosition.x && MathOfRwrme.SvgPosToU3dPos(sp.position).x < currentPosition.x && MathOfRwrme.SvgPosToU3dPos(sp.position).y < startPosition.z&& MathOfRwrme.SvgPosToU3dPos(sp.position).y > currentPosition.z)
                {
                    MetaMap.instance.defaultLayer.mapItems.RemoveAt(i);
                    i--;
                    GameObject.Destroy(sp.gameObject);
                }
            }
        }
    }

    public void setType(System.Type type)
    {
        itemType = type;
    }
}

public class TerrainMaterialPainter : Tool
{
    public int tarind=3;
    public bool underUse = false;
    private Vector2 curPos;
    private Color tarCol;
    public float radius = 20;
    private Texture2D tex;

    public TerrainMaterialPainter(string name) : base(name)
    {
    }

    public void DrawCircleOnTexture(Texture2D texture,Vector2 pos ,float width,float height, Color color)
    {
        pos = new Vector2(pos.x/2, (1024 - pos.y)/2+512);
        float scaleX = texture.width / width;
        float scaleY = texture.height / height;

        int pixelCenterX = Mathf.RoundToInt(pos.x * scaleX);
        int pixelCenterY = Mathf.RoundToInt(pos.y * scaleY);
        int pixelRadius = Mathf.RoundToInt(radius * Mathf.Min(scaleX, scaleY));

        // 2. 计算边界
        int left = Mathf.Max(0, pixelCenterX - pixelRadius);
        int right = Mathf.Min(texture.width - 1, pixelCenterX + pixelRadius);
        int top = Mathf.Max(0, pixelCenterY - pixelRadius);
        int bottom = Mathf.Min(texture.height - 1, pixelCenterY + pixelRadius);

        int regionWidth = right - left + 1;
        int regionHeight = bottom - top + 1;

        int radiusSquared = pixelRadius * pixelRadius;

        for (int y = top; y <= bottom; y++)
        {
            int dy = y - pixelCenterY;
            int dySquared = dy * dy;

            for (int x = left; x <= right; x++)
            {
                int dx = x - pixelCenterX;
                if (dx * dx + dySquared <= radiusSquared)
                {
                    texture.SetPixel(x, y, color);
                }
            }
        }

        texture.Apply();
    }

    public override void startUse(Vector3 position, GameObject hitO)
    {
        if (tarind == 0) tarCol = new Color(0, 0, 0, 1);
        if (tarind == 1) tarCol = new Color(1, 0, 0, 1);
        if (tarind == 2) tarCol = new Color(1, 1, 0, 1);
        if (tarind == 3) tarCol = new Color(1, 1, 1, 1);
        if (tarind == 4) tarCol = new Color(1, 1, 1, 0);
        Terrain terrain = Terrain.activeTerrain;
        tex = terrain.materialTemplate.GetTexture("_Mask") as Texture2D;
        underUse = true;
        curPos = MathOfRwrme.U3dPosToSvgPos(position);
        DrawCircleOnTexture(tex, curPos, 1024, 1024, tarCol);
    }

    public override void OnDragging(Vector3 position)
    {
        if (!underUse) return;
        curPos = MathOfRwrme.U3dPosToSvgPos(position);
        DrawCircleOnTexture(tex, curPos, 1024, 1024, tarCol);
    }

    public override void EndUse()
    {
        underUse = false;
    }
}


public class HeightBush : Tool
{
    public float range = 10.0f; 
    public float height = 0.5f; 
    public float hardness = 1.0f; 

    private Terrain currentTerrain;

    public HeightBush(string name) : base(name)
    {
    }
    public override void startUse(Vector3 position, GameObject hitObject)
    {
        currentTerrain = Terrain.activeTerrain;
        if (currentTerrain == null) return;
        ApplyHeightBrush(position, true);
    }


    public override void OnDragging(Vector3 currentPosition)
    {
        if (currentTerrain == null) return;
        ApplyHeightBrush(currentPosition, false);
    }

    private void ApplyHeightBrush(Vector3 worldPos, bool isStart)
    {
        TerrainData terrainData = currentTerrain.terrainData;

        Vector2Int centerCoord =Vector2Int.FloorToInt(new Vector2( worldPos.x,worldPos.z)/2) ;
        int radiusInPixels = Mathf.CeilToInt(range / 2);

        float[,] heights = terrainData.GetHeights(0, 0,
            terrainData.heightmapResolution,
            terrainData.heightmapResolution);


        int startX = Mathf.Max(0, centerCoord.x - radiusInPixels);
        int endX = Mathf.Min(terrainData.heightmapResolution, centerCoord.x + radiusInPixels);
        int startY = Mathf.Max(0, centerCoord.y - radiusInPixels);
        int endY = Mathf.Min(terrainData.heightmapResolution, centerCoord.y + radiusInPixels);
        Debug.Log("are = " + startX.ToString()+" " + endX.ToString() + " " + startY.ToString() + " " + endY.ToString() + " ");

        for (int y = startY; y < endY; y++)
        {
            for (int x = startX; x < endX; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y),
                    new Vector2(centerCoord.x, centerCoord.y)) / radiusInPixels;

                if (distance <= 1.0f)
                {
                    float falloff = Mathf.Pow(1 - distance, hardness);

                    float targetHeight = heights[y, x]*(1-falloff) + height * falloff;
                    heights[y, x] = Mathf.Clamp(targetHeight, 0, 1);
                }
            }
        }

        terrainData.SetHeights(0, 0, heights);
        terrainData.SyncHeightmap();
    }
}
public class HeightSmudge : Tool
{
    public float range = 10.0f;
    public float strength = 0.3f;

    private Terrain currentTerrain;
    private Vector3 lastPos;

    public HeightSmudge(string name) : base(name) { }

    public override void startUse(Vector3 position, GameObject hitObject)
    {
        currentTerrain = Terrain.activeTerrain;
        lastPos = position;
    }

    public override void OnDragging(Vector3 currentPos)
    {
        if (currentTerrain == null) return;

        TerrainData data = currentTerrain.terrainData;
        Vector3 size = data.size;
        int res = data.heightmapResolution;

        Vector3 localPos = currentPos - currentTerrain.transform.position;
        Vector3 lastLocalPos = lastPos - currentTerrain.transform.position;

        int cx = (int)(localPos.x / size.x * res);
        int cy = (int)(localPos.z / size.z * res);
        int lcx = (int)(lastLocalPos.x / size.x * res);
        int lcy = (int)(lastLocalPos.z / size.z * res);

        int radius = Mathf.CeilToInt(range / 2);
        float[,] heights = data.GetHeights(0, 0, res, res);

        for (int y = Mathf.Max(0, cy - radius); y < Mathf.Min(res, cy + radius); y++)
            for (int x = Mathf.Max(0, cx - radius); x < Mathf.Min(res, cx + radius); x++)
            {
                int srcY = lcy + (y - cy);
                int srcX = lcx + (x - cx);

                if (srcY >= 0 && srcY < res && srcX >= 0 && srcX < res)
                    heights[y, x] = Mathf.Lerp(heights[y, x], heights[srcY, srcX], strength);
            }

        data.SetHeights(0, 0, heights);
        lastPos = currentPos;
    }
}
