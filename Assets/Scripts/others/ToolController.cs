
using System.Collections.Generic;
using TMPro;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;

public class ToolController : MonoBehaviour
{
    // Start is called before the first frame update
    public List<Tool> tools = new List<Tool>();
    public Tool currentTool;
    public Camera orthographicCamera;
    public static ToolController inste;

    // 用于拖选可视化的 LineRenderer
    public LineRenderer dragVisualizer;

    private SideTool sdt = new SideTool();

    private Dictionary<KeyCode, int> toolShortcutMap;

    public MapItem miSelected;
    private MapItem lastMiS;

    public List<MapItem> misSelected = new List<MapItem>();
    public bool MultiSelectMode => misSelected.Count > 0;
    private int lastMisCount = 0;

    private HashSet<MapItem> highlightedItems = new HashSet<MapItem>();

    private BuildingScaleHandle activeScaleHandle;
    private Building lastHandleTarget;

    private PlatformPathHandle activePlatformHandle;
    private Platform lastPlatformHandleTarget;

    private WallPathHandle activeWallHandle;
    private Wall lastWallHandleTarget;

    private OffroadPathHandle activeOffroadHandle;
    private Offroad lastOffroadHandleTarget;

    public int axisLock = 0; // 0=none, 1=lock X (free X, fix Z), 2=lock Z (free Z, fix X)
    private Vector3 lockOrigin;

    // 用于存储复制的建筑信息
    private Building copiedBuilding = null;

    public GameObject heightChangerVisPart;
    public GameObject ToolVisPartInstance;

    /// <summary>全局高度平滑：3×3 盒式滤波重复次数（越大越平）。</summary>
    public int heightMapGlobalSmoothPasses = 4;
    /// <summary>地形 _Mask 四通道全局平滑：与 <see cref="heightMapGlobalSmoothPasses"/> 相同含义；设为 0 时仍至少做 1 次。</summary>
    public int terrainMaskGlobalSmoothPasses = 4;
    /// <summary>全局噪点：归一化高度上的最大偏移幅度（约 0.01～0.03）。</summary>
    public float heightMapGlobalNoiseAmplitude = 0.018f;
    /// <summary>全局噪点：Perlin 采样密度（越大纹理越细）。</summary>
    public float heightMapGlobalNoiseFrequency = 0.06f;

    void Start()
    {
        inste = this;
        orthographicCamera = Camera.main;

        // 允许射线命中 MeshCollider 的背面，否则法线/绕序反了的平台会“点不到”
#if UNITY_2020_1_OR_NEWER
        Physics.queriesHitBackfaces = true;
#endif

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
        tools.Add(new BaseTool("BaseBuilder",this)); //tool 11 = base drawer
        tools.Add(new ItemScatter("Scatter"));//tool 12 SpawnPosition pointer
        tools.Add(new Eraser("Eraser", this)); //tool 13 = SpawnerEraser
        tools.Add(new MeshScatter("Mesh Scatter")); //tool 14 = MeshScatter
        tools.Add(new TerrainMaterialPainter("Terrain painter")); //tool 15 = terrainPainter
        tools.Add(new HeightBush("HBS")); //tool 16 = terrainPainter
        tools.Add(new HeightSmudge("HS")); //tool 17 = terrainSmudge
        tools.Add(new OffraodDrawer("offraodDrawer")); //tool 18 = offroad path drawer

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
            { KeyCode.F9,     18 },  // OffraodDrawer
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

    /// <summary>与鼠标按下一致：可打工具射线的屏幕区域（排除右侧 UI 与左下角一块）。</summary>
    private static bool IsPointerInMainToolScreenArea()
    {
        if (Screen.width <= 0 || Screen.height <= 0) return false;
        float nx = Input.mousePosition.x / Screen.width;
        float ny = Input.mousePosition.y / Screen.height;
        return nx < UIManager.RightPanelAnchorMinX && (nx > 0.21f || ny > 0.23f);
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

        string modeLabel = sdt.GetModeLabel();
        if (modeLabel != null)
        {
            GUIStyle style = new GUIStyle(GUI.skin.label);
            style.fontSize = 18;
            style.fontStyle = FontStyle.Bold;
            style.normal.textColor = Color.yellow;
            style.alignment = TextAnchor.MiddleCenter;
            Rect modeRect = new Rect(Screen.width * 0.5f - 80, 10, 160, 30);
            GUI.Label(modeRect, modeLabel, style);
        }

        if (axisLock != 0)
        {
            GUIStyle lockStyle = new GUIStyle(GUI.skin.label);
            lockStyle.fontSize = 16;
            lockStyle.fontStyle = FontStyle.Bold;
            lockStyle.normal.textColor = axisLock == 1 ? Color.red : Color.cyan;
            lockStyle.alignment = TextAnchor.MiddleCenter;
            string lockLabel = axisLock == 1 ? "Lock: X" : "Lock: Z(Y)";
            Rect lockRect = new Rect(Screen.width * 0.5f - 60, 40, 120, 25);
            GUI.Label(lockRect, lockLabel, lockStyle);
        }
    }

        // Update is called once per frame
    void Update()
    {
        // F5：刷新整张地图。放在输入框 early-return 之前，避免编辑搜索框/坐标框时按 F5 失效。
        if (Input.GetKeyDown(KeyCode.F5)
            && !Input.GetKey(KeyCode.LeftShift) && !Input.GetKey(KeyCode.RightShift)
            && !Input.GetKey(KeyCode.LeftControl) && !Input.GetKey(KeyCode.RightControl))
        {
            bool overUi = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
            if (!overUi && Syncer.instence != null)
                Syncer.instence.updateMap();
        }

        if (EventSystem.current != null &&
            EventSystem.current.currentSelectedGameObject != null &&
            EventSystem.current.currentSelectedGameObject.GetComponent<TMP_InputField>() != null)
        {
            if (Input.GetMouseButtonDown(0))
            {
                float nx = Input.mousePosition.x / Screen.width;
                if (nx < UIManager.RightPanelAnchorMinX)
                    EventSystem.current.SetSelectedGameObject(null);
            }
            return;
        }

        if(currentTool.visPart==null)
        {
            if(ToolVisPartInstance!=null)Destroy(ToolVisPartInstance);
        }
        else
        {
            if(ToolVisPartInstance==null)
            {
                ToolVisPartInstance=Instantiate(currentTool.visPart,transform);
            }
            else
            {
                if(ToolVisPartInstance.GetType()!=currentTool.visPart.GetType())
                {
                    Destroy(ToolVisPartInstance);
                    ToolVisPartInstance=Instantiate(currentTool.visPart);
                }
            }
            //update the position of the tool vis part
            currentTool.updateVisPart();
        }

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
            if (lastMiS != miSelected || lastMisCount != misSelected.Count)
        {
            if (MultiSelectMode)
            {
                UIManager.instance.changeShowingCanvas(null);
                UIManager.instance.RefreshMultiSelectPanel(misSelected);
            }
            else
            {
                UIManager.instance.RefreshMultiSelectPanel(null);
                if (miSelected != null)
                {
                    Transform can = miSelected.transform.Find("Canvas");
                    if (can != null)
                        UIManager.instance.changeShowingCanvas(can.gameObject.GetComponent<Canvas>());
                    else
                        UIManager.instance.changeShowingCanvas(null);
                }
                UIManager.instance.RefreshVertexPanel(miSelected);
            }
        }
        lastMiS = miSelected;
        lastMisCount = misSelected.Count;
        UpdateHighlights();
        UpdateScaleHandle();
        UpdatePlatformHandle();
        UpdateWallHandle();
        UpdateOffroadHandle();

        bool handleBusy = (activeScaleHandle != null && activeScaleHandle.IsDragging)
            || (activePlatformHandle != null && activePlatformHandle.IsDragging)
            || (activeWallHandle != null && activeWallHandle.IsDragging)
            || (activeOffroadHandle != null && activeOffroadHandle.IsDragging);

        if (Input.GetKeyDown(KeyCode.X))
        {
            if (axisLock == 1) axisLock = 0;
            else { axisLock = 1; lockOrigin = GetLastToolPosition(); }
        }
        if (Input.GetKeyDown(KeyCode.Y))
        {
            if (axisLock == 2) axisLock = 0;
            else { axisLock = 2; lockOrigin = GetLastToolPosition(); }
        }

        if (Input.GetMouseButtonDown(0) && !handleBusy)
        {
            if (sdt.state != 0)
            {
                sdt.state = 0;
            }
            else
            {

                if (IsPointerInMainToolScreenArea())
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
                        Vector3 hitPosition = ApplyAxisLock(hit.point);
                        lockOrigin = hitPosition;

                        GameObject hitObject = hit.collider.gameObject.transform.root.gameObject;
                        Debug.Log("ToolManager:hit at" + hitObject.ToSafeString());
                        currentTool.startUse(hitPosition, hitObject);
                        
                    }

                }
            }
        }
        if (Input.GetMouseButton(0) && !handleBusy)
        {
            if (IsPointerInMainToolScreenArea())
            {
                Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
                RaycastHit hit;
                // 与 MouseDown 一致：选择工具需命中 Base（Layer7），否则拖动时射线会穿透 Base 落到 Layer6，误判为大范围框选
                int layerMask = 1 << 6;
                if (currentTool is SelecterTool)
                    layerMask = (1 << 6) | (1 << 7);
                if (Physics.Raycast(ray, out hit, Mathf.Infinity, layerMask))
                {
                    Vector3 currentPosition = ApplyAxisLock(hit.point);
                    currentTool.OnDragging(currentPosition);
                }
            }
        }

        if (Input.GetMouseButtonUp(0) && !handleBusy)
        {
            currentTool.EndUse();
        }

        if (Input.GetKeyDown(KeyCode.G))
        {
            if (sdt.mi != null || MultiSelectMode)
            {
                if (sdt.state != 1)
                    CtrlZer.instance.checkPointTransformOnly();
                sdt.tChangeMode(1);
            }
        }

        if (Input.GetKeyDown(KeyCode.R))
        {
            if (sdt.mi != null || MultiSelectMode)
            {
                if (sdt.state != 2)
                    CtrlZer.instance.checkPointTransformOnly();
                sdt.tChangeMode(2);
            }
        }

        bool altHeld = Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);
        if (altHeld && Input.GetKeyDown(KeyCode.S))
        {
            if (sdt.mi != null || MultiSelectMode)
            {
                if (sdt.state != 3)
                    CtrlZer.instance.checkPointTransformOnly();
                sdt.tChangeMode(3);
            }
        }

        if (Input.GetKeyDown(KeyCode.Delete))
        {
            DeleteSelected();
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
            axisLock = 0;
        }
        if(Input.GetKeyDown(KeyCode.Escape))
        {
            currentTool.escape();
            axisLock = 0;
            if (miSelected != null || misSelected.Count > 0)
            {
                miSelected = null;
                misSelected.Clear();
                UIManager.instance.changeShowingCanvas(null);
                UIManager.instance.RefreshMultiSelectPanel(null);
                UIManager.instance.RefreshVertexPanel(null);
            }
        }

        HandleToolShortcuts();

        sdt.mi = miSelected;
        sdt.mis = misSelected.Count > 0 ? misSelected : null;

        Vector2 ofst = new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y"));

        Vector3 pixelPos = Input.mousePosition;
        float normalizedX = pixelPos.x / Screen.width;
        float normalizedY = pixelPos.y / Screen.height;
        Vector2 gv2 = new Vector2(normalizedX, normalizedY);
        sdt.update(ofst, gv2);
    }
    private void HandleToolShortcuts()
    {
        // 需按住 Shift 才响应数字/F 键切工具，避免误触
        if (!Input.GetKey(KeyCode.LeftShift) && !Input.GetKey(KeyCode.RightShift)) return;
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

    public void FunctionHeightMapSmooth()
    {
        Terrain terrain = Terrain.activeTerrain;
        if (terrain == null || terrain.terrainData == null)
            return;

        CtrlZer.instance.checkPointWithHeightmap();

        TerrainData td = terrain.terrainData;
        int res = td.heightmapResolution;
        float[,] h = td.GetHeights(0, 0, res, res);
        float[,] scratch = new float[res, res];
        int passes = Mathf.Max(1, heightMapGlobalSmoothPasses);

        for (int p = 0; p < passes; p++)
        {
            for (int y = 0; y < res; y++)
            {
                for (int x = 0; x < res; x++)
                {
                    float sum = 0f;
                    int cnt = 0;
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        int yy = y + dy;
                        if (yy < 0 || yy >= res)
                            continue;
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            int xx = x + dx;
                            if (xx < 0 || xx >= res)
                                continue;
                            sum += h[yy, xx];
                            cnt++;
                        }
                    }
                    scratch[y, x] = sum / cnt;
                }
            }
            float[,] tmp = h;
            h = scratch;
            scratch = tmp;
        }

        td.SetHeights(0, 0, h);
        td.SyncHeightmap();
    }

    public void FunctionHeightMapNoiser()
    {
        Terrain terrain = Terrain.activeTerrain;
        if (terrain == null || terrain.terrainData == null)
            return;

        CtrlZer.instance.checkPointWithHeightmap();

        TerrainData td = terrain.terrainData;
        int res = td.heightmapResolution;
        float[,] heights = td.GetHeights(0, 0, res, res);
        float amp = Mathf.Max(0f, heightMapGlobalNoiseAmplitude);
        float freq = Mathf.Max(0.0001f, heightMapGlobalNoiseFrequency);
        float ox = Random.Range(0f, 8192f);
        float oy = Random.Range(0f, 8192f);

        for (int y = 0; y < res; y++)
        {
            for (int x = 0; x < res; x++)
            {
                float n = Mathf.PerlinNoise(ox + x * freq, oy + y * freq);
                float delta = (n - 0.5f) * 2f * amp;
                heights[y, x] = Mathf.Clamp01(heights[y, x] + delta);
            }
        }

        td.SetHeights(0, 0, heights);
        td.SyncHeightmap();
    }

    public void FunctionTerrainMaterialSmooth()
    {
        Terrain terrain = Terrain.activeTerrain;
        if (terrain == null || terrain.materialTemplate == null)
            return;

        Texture2D mask = terrain.materialTemplate.GetTexture("_Mask") as Texture2D;
        if (mask == null)
            return;

        if (!mask.isReadable)
        {
            Debug.LogError("ToolController: 地形 _Mask 需在 Inspector 中勾选 Read/Write Enabled，否则无法平滑。");
            return;
        }

        CtrlZer.instance.checkPointWithTerrainMask();

        int w = mask.width;
        int h = mask.height;
        Color[] src = mask.GetPixels();
        Color[] scratch = new Color[src.Length];
        int passes = Mathf.Max(1, terrainMaskGlobalSmoothPasses);

        for (int p = 0; p < passes; p++)
        {
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    Vector4 sum = Vector4.zero;
                    int cnt = 0;
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        int yy = y + dy;
                        if (yy < 0 || yy >= h)
                            continue;
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            int xx = x + dx;
                            if (xx < 0 || xx >= w)
                                continue;
                            Color c = src[yy * w + xx];
                            sum.x += c.r;
                            sum.y += c.g;
                            sum.z += c.b;
                            sum.w += c.a;
                            cnt++;
                        }
                    }
                    float inv = 1f / cnt;
                    scratch[y * w + x] = new Color(sum.x * inv, sum.y * inv, sum.z * inv, sum.w * inv);
                }
            }
            Color[] tmp = src;
            src = scratch;
            scratch = tmp;
        }

        mask.SetPixels(src);
        mask.Apply();
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
        axisLock = 0;
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
        if (ms == null) return;
        string name = UIManager.instance != null ? UIManager.instance.GetMeshDropdownOptionText(index) : null;
        if (string.IsNullOrEmpty(name))
        {
            if (index < 0 || index >= MetaMap.instance.meshTemplates.Count) return;
            name = MetaMap.instance.meshTemplates[index].name;
        }
        ms.ChooseThis(name);
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

    private Vector3 lastToolHitPos;

    private Vector3 ApplyAxisLock(Vector3 pos)
    {
        lastToolHitPos = pos;
        if (axisLock == 1) pos.z = lockOrigin.z;
        else if (axisLock == 2) pos.x = lockOrigin.x;
        return pos;
    }

    private Vector3 GetLastToolPosition()
    {
        return lastToolHitPos;
    }

    public string GetSelectionInfoText()
    {
        if (misSelected.Count > 0)
        {
            string result = "选中 " + misSelected.Count + " 个对象:\n";
            foreach (var mi in misSelected)
                result += mi.id + "\n";
            return result;
        }
        if (miSelected != null)
            return miSelected.getInfoText();
        return "";
    }

    public void DeleteSelected()
    {
        CtrlZer.instance.checkPoint();
        if (misSelected.Count > 0)
        {
            foreach (var mi in new List<MapItem>(misSelected))
            {
                if (!MetaMap.instance.defaultLayer.mapItems.Remove(mi))
                {
                    if (!MetaMap.instance.baseLayer.mapItems.Remove(mi) && MetaMap.instance.offroadLayer != null)
                        MetaMap.instance.offroadLayer.mapItems.Remove(mi);
                }
                mi.gameObject.SetActive(false);
            }
            misSelected.Clear();
        }
        else if (miSelected != null)
        {
            if (!MetaMap.instance.defaultLayer.mapItems.Remove(miSelected))
            {
                if (!MetaMap.instance.baseLayer.mapItems.Remove(miSelected) && MetaMap.instance.offroadLayer != null)
                    MetaMap.instance.offroadLayer.mapItems.Remove(miSelected);
            }
            miSelected.gameObject.SetActive(false);
        }
    }

    public void ClearMultiSelect()
    {
        misSelected.Clear();
    }

    private void UpdateHighlights()
    {
        highlightedItems.RemoveWhere(item =>
        {
            if (item == null || !misSelected.Contains(item))
            {
                if (item != null) RemoveHighlightVisual(item);
                return true;
            }
            return false;
        });

        foreach (var item in misSelected)
        {
            if (item != null && !highlightedItems.Contains(item))
            {
                ApplyHighlightVisual(item);
                highlightedItems.Add(item);
            }
        }
    }

    private void ApplyHighlightVisual(MapItem item)
    {
        if (item.transform.Find("_MSHighlight") != null) return;

        GameObject hl = new GameObject("_MSHighlight");
        hl.transform.SetParent(item.transform, false);
        hl.AddComponent<SelectionTint>();
    }

    private void RemoveHighlightVisual(MapItem item)
    {
        Transform hl = item.transform.Find("_MSHighlight");
        if (hl != null) Destroy(hl.gameObject);

        Renderer[] renderers = item.GetComponentsInChildren<Renderer>();
        MaterialPropertyBlock clear = new MaterialPropertyBlock();
        foreach (var r in renderers)
        {
            if (r != null) r.SetPropertyBlock(clear);
        }
    }

    private void UpdateScaleHandle()
    {
        Building target = (!MultiSelectMode && miSelected is Building b) ? b : null;

        if (target == lastHandleTarget) return;
        lastHandleTarget = target;

        if (activeScaleHandle != null)
        {
            Destroy(activeScaleHandle.gameObject);
            activeScaleHandle = null;
        }

        if (target != null)
        {
            GameObject handleGO = new GameObject("_BuildingScaleHandle");
            activeScaleHandle = handleGO.AddComponent<BuildingScaleHandle>();
            activeScaleHandle.Init(target);
        }
    }

    private void UpdatePlatformHandle()
    {
        Platform target = (!MultiSelectMode && miSelected is Platform p) ? p : null;

        if (target == lastPlatformHandleTarget) return;
        lastPlatformHandleTarget = target;

        if (activePlatformHandle != null)
        {
            Destroy(activePlatformHandle.gameObject);
            activePlatformHandle = null;
        }

        if (target != null)
        {
            GameObject go = new GameObject("_PlatformPathHandle");
            activePlatformHandle = go.AddComponent<PlatformPathHandle>();
            activePlatformHandle.Init(target);
        }
    }

    private void UpdateWallHandle()
    {
        Wall target = (!MultiSelectMode && miSelected is Wall w) ? w : null;

        if (target == lastWallHandleTarget) return;
        lastWallHandleTarget = target;

        if (activeWallHandle != null)
        {
            Destroy(activeWallHandle.gameObject);
            activeWallHandle = null;
        }

        if (target != null)
        {
            GameObject go = new GameObject("_WallPathHandle");
            activeWallHandle = go.AddComponent<WallPathHandle>();
            activeWallHandle.Init(target);
        }
    }

    private void UpdateOffroadHandle()
    {
        Offroad target = (!MultiSelectMode && miSelected is Offroad o) ? o : null;

        if (target == lastOffroadHandleTarget) return;
        lastOffroadHandleTarget = target;

        if (activeOffroadHandle != null)
        {
            Destroy(activeOffroadHandle.gameObject);
            activeOffroadHandle = null;
        }

        if (target != null)
        {
            GameObject go = new GameObject("_OffroadPathHandle");
            activeOffroadHandle = go.AddComponent<OffroadPathHandle>();
            activeOffroadHandle.Init(target);
        }
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

            // 记录撤销点（必须在修改 mapItems 之前）
            CtrlZer.instance.checkPoint();

            // 添加到地图
            MetaMap.instance.defaultLayer.mapItems.Add(newBuilding);
            
            // 刷新显示
            newBuilding.scatterThis();
            
            // 选中新建筑
            miSelected = newBuilding;

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
    public GameObject visPart=null;
    
    public Tool(string name)
    {
        m_name = name;
    }

    public virtual void updateVisPart()
    {

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
    private Vector3 startPosition;
    private Vector3 lastDragPosition;
    private GameObject hitObject;
    private LineRenderer visualizer;
    private bool isDragging = false;
    private bool hasDragged = false;
    private bool ctrlHeld = false;
    private const float DRAG_THRESHOLD = 3f;

    public SelecterTool(string name) : base(name) { }

    public override void startUse(Vector3 position, GameObject hitO)
    {
        startPosition = position;
        lastDragPosition = position;
        hitObject = hitO;
        isDragging = true;
        hasDragged = false;
        ctrlHeld = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
        visualizer = ToolController.inste.GetDragVisualizer();
    }

    public override void OnDragging(Vector3 currentPosition)
    {
        if (!isDragging) return;
        lastDragPosition = currentPosition;

        float dist = Vector3.Distance(startPosition, currentPosition);
        if (dist > DRAG_THRESHOLD)
        {
            hasDragged = true;
            if (visualizer != null)
            {
                visualizer.enabled = true;
                UpdateBoxVisualizer(currentPosition);
            }
        }
    }

    public override void EndUse()
    {
        if (!isDragging) return;
        isDragging = false;

        if (visualizer != null)
            visualizer.enabled = false;

        if (hasDragged)
            PerformBoxSelection();
        else
            PerformClickSelection();
    }

    private void PerformClickSelection()
    {
        if (hitObject == null) return;
        MapItem clickedItem = hitObject.GetComponent<MapItem>();

        if (clickedItem == null)
        {
            if (!ctrlHeld)
            {
                ToolController.inste.misSelected.Clear();
            }
            return;
        }

        if (clickedItem is Platform)
        {
            ToolController.inste.misSelected.Clear();
            ToolController.inste.miSelected = clickedItem;
            return;
        }

        if (ctrlHeld)
        {
            if (ToolController.inste.misSelected.Count == 0
                && ToolController.inste.miSelected != null
                && !(ToolController.inste.miSelected is Platform))
            {
                ToolController.inste.misSelected.Add(ToolController.inste.miSelected);
            }

            if (ToolController.inste.misSelected.Contains(clickedItem))
                ToolController.inste.misSelected.Remove(clickedItem);
            else
                ToolController.inste.misSelected.Add(clickedItem);

            if (ToolController.inste.misSelected.Count == 1)
                ToolController.inste.miSelected = ToolController.inste.misSelected[0];
            else if (ToolController.inste.misSelected.Count > 1)
                ToolController.inste.miSelected = clickedItem;
        }
        else
        {
            ToolController.inste.misSelected.Clear();
            ToolController.inste.miSelected = clickedItem;
        }
    }

    private void PerformBoxSelection()
    {
        float minX = Mathf.Min(startPosition.x, lastDragPosition.x);
        float maxX = Mathf.Max(startPosition.x, lastDragPosition.x);
        float minZ = Mathf.Min(startPosition.z, lastDragPosition.z);
        float maxZ = Mathf.Max(startPosition.z, lastDragPosition.z);

        Collider[] colliders = Physics.OverlapBox(
            new Vector3((minX + maxX) / 2, startPosition.y, (minZ + maxZ) / 2),
            new Vector3((maxX - minX) / 2, 50f, (maxZ - minZ) / 2),
            Quaternion.identity,
            (1 << 6) | (1 << 7)
        );

        if (!ctrlHeld)
            ToolController.inste.misSelected.Clear();

        foreach (Collider col in colliders)
        {
            GameObject root = col.gameObject.transform.root.gameObject;
            MapItem mi = root.GetComponent<MapItem>();
            if (mi == null) continue;
            if (mi is Platform) continue;
            if (!ToolController.inste.misSelected.Contains(mi))
                ToolController.inste.misSelected.Add(mi);
        }

        if (ToolController.inste.misSelected.Count > 0)
            ToolController.inste.miSelected = ToolController.inste.misSelected[0];

        Debug.Log("框选完成，选中 " + ToolController.inste.misSelected.Count + " 个对象");
    }

    private void UpdateBoxVisualizer(Vector3 currentPosition)
    {
        if (visualizer == null) return;

        float y = Mathf.Max(startPosition.y, currentPosition.y) + 10f;
        Vector3 p1 = new Vector3(startPosition.x, y, startPosition.z);
        Vector3 p2 = new Vector3(currentPosition.x, y, startPosition.z);
        Vector3 p3 = new Vector3(currentPosition.x, y, currentPosition.z);
        Vector3 p4 = new Vector3(startPosition.x, y, currentPosition.z);

        visualizer.positionCount = 5;
        visualizer.SetPosition(0, p1);
        visualizer.SetPosition(1, p2);
        visualizer.SetPosition(2, p3);
        visualizer.SetPosition(3, p4);
        visualizer.SetPosition(4, p1);

        float pulse = 1f + 0.2f * Mathf.Sin(Time.time * 5f);
        visualizer.startWidth = 1.5f * pulse;
        visualizer.endWidth = 1.5f * pulse;
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
        Vector2 dis = MathOfRwrme.U3dPosToSvgPos(new Vector2(currentPosition.x, currentPosition.z)) - MathOfRwrme.U3dPosToSvgPos(new Vector2(startPosition.x, startPosition.z));
        if (Mathf.Abs(dis.x) < 5f && Mathf.Abs(dis.y) < 5f)
        {
            Debug.Log("Base too small, cancelled");
            return;
        }
        GameObject go = ToolController.inste.InsOnePref(MapImporter.instate.BasePref);
        Base bs = go.GetComponent<Base>();
        bs.position = MathOfRwrme.U3dPosToSvgPos(new Vector2(startPosition.x, startPosition.z));
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

            CtrlZer.instance.checkPoint();

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
    public List<MapItem> mis = null;
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

    public string GetModeLabel()
    {
        if (state == 0) return null;
        if (state == 1) return "Grab (Alt+G)";
        if (state == 2) return "Rotate (Alt+R)";
        return "Scale (Alt+S)";
    }

    public void update(Vector2 offset, Vector2 Pos)
    {
        if (state == 0) return;

        if (mis != null && mis.Count > 0)
        {
            foreach (var item in mis)
                ApplyTransform(item, offset, Pos);
        }
        else if (mi != null)
        {
            ApplyTransform(mi, offset, Pos);
        }
    }

    private void ApplyTransform(MapItem item, Vector2 offset, Vector2 Pos)
    {
        if (state == 1)
        {
            Vector2 grabOff = new Vector2(offset.x, -1f * offset.y);
            int al = ToolController.inste.axisLock;
            if (al == 1) grabOff.y = 0;
            else if (al == 2) grabOff.x = 0;
            item.grab(grabOff);
        }
        if (state == 2)
        {
            item.rotate((offset.x * (Pos - new Vector2(0.5f, 0.5f)).y - offset.y * (Pos - new Vector2(0.5f, 0.5f)).x) / (Pos - new Vector2(0.5f, 0.5f)).magnitude);
        }
        if (state == 3)
        {
            item.scale(offset.x);
        }
        item.scatterThis();
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
            CtrlZer.instance.checkPoint();
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

    public void RemoveLastVertex()
    {
        if (PointSelected.Count > 0)
        {
            PointSelected.RemoveAt(PointSelected.Count - 1);
            PathDrawed.RemoveAt(PathDrawed.Count - 1);
            UpdateVisualizer();
        }
        if (PointSelected.Count == 0)
        {
            drawing = false;
            ToolController.inste.dragVisualizer.enabled = false;
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

public class OffraodDrawer : Tool
{
    public bool drawing;
    public List<Vector3> PointSelected;
    public List<Vector2> PathDrawed;

    public OffraodDrawer(string name) : base(name)
    {
        drawing = false;
        PathDrawed = new List<Vector2>();
        PointSelected = new List<Vector3>();
    }

    public override void startUse(Vector3 Position, GameObject hitO)
    {
        if (!drawing)
        {
            ToolController.inste.dragVisualizer.enabled = true;
        }
        drawing = true;
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
        if (!drawing || PathDrawed.Count < 2)
            return;

        ToolController.inste.dragVisualizer.enabled = false;
        CtrlZer.instance.checkPoint();
        drawing = false;

        GameObject go;
        Offroad ofr;
        if (MapImporter.instate.OffroadPref != null)
        {
            go = ToolController.inste.InsOnePref(MapImporter.instate.OffroadPref);
            ofr = go.GetComponent<Offroad>();
            if (ofr == null) ofr = go.AddComponent<Offroad>();
        }
        else
        {
            go = new GameObject("Offroad");
            ofr = go.AddComponent<Offroad>();
        }

        ofr.positionLine = new List<Vector2>(PathDrawed);
        ofr.curve = new List<bool>();
        ofr.controlPoints = new List<Vector2>();
        Offroad.ApplyAutoBezierCurveAnnotations(ofr.positionLine, ofr.curve, ofr.controlPoints);
        ofr.id = MetaMap.instance.getNewItemId("offroad");
        ofr.layerIndex = MetaMap.instance.offroadHeightSampleLayer;

        PathDrawed.Clear();
        PointSelected.Clear();

        MetaMap.instance.offroadLayer.mapItems.Add(ofr);
        ofr.scatterThis();
    }

    public void RemoveLastVertex()
    {
        if (PointSelected.Count > 0)
        {
            PointSelected.RemoveAt(PointSelected.Count - 1);
            PathDrawed.RemoveAt(PathDrawed.Count - 1);
            UpdateVisualizer();
        }
        if (PointSelected.Count == 0)
        {
            drawing = false;
            ToolController.inste.dragVisualizer.enabled = false;
        }
    }

    private void UpdateVisualizer()
    {
        if (ToolController.inste.dragVisualizer == null) return;
        ToolController.inste.dragVisualizer.positionCount = 0;
        for (int i = 0; i < PointSelected.Count; i++)
        {
            ToolController.inste.dragVisualizer.positionCount++;
            ToolController.inste.dragVisualizer.SetPosition(i, PointSelected[i] + Vector3.up * 5f);
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
    public bool IsDrawing => drawing > 0;
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
            CtrlZer.instance.checkPoint();
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

    public void RemoveLastVertex()
    {
        if (drawing == 2 && endLine.Count > 0)
        {
            endLine.RemoveAt(endLine.Count - 1);
            endLineDrawed.RemoveAt(endLineDrawed.Count - 1);
        }
        else if (drawing == 1 && startLine.Count > 0)
        {
            startLine.RemoveAt(startLine.Count - 1);
            startLineDrawed.RemoveAt(startLineDrawed.Count - 1);
        }

        if (drawing == 1 && startLine.Count == 0)
        {
            drawing = 0;
            ToolController.inste.dragVisualizer.enabled = false;
        }

        UpdateVisualizer();
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
        CtrlZer.instance.checkPoint();
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
        CtrlZer.instance.checkPoint();
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
        CtrlZer.instance.checkPoint();
        for (int i = 0; i < MetaMap.instance.defaultLayer.mapItems.Count; i++)
        {
            if(MetaMap.instance.defaultLayer.mapItems[i].GetType()!=itemType)continue;
            if(MetaMap.instance.defaultLayer.mapItems[i] is MeRect sp)
            {
                if(MathOfRwrme.SvgPosToU3dPos(sp.position).x>startPosition.x && MathOfRwrme.SvgPosToU3dPos(sp.position).x < currentPosition.x && MathOfRwrme.SvgPosToU3dPos(sp.position).y < startPosition.z&& MathOfRwrme.SvgPosToU3dPos(sp.position).y > currentPosition.z)
                {
                    MetaMap.instance.defaultLayer.mapItems.RemoveAt(i);
                    i--;
                    sp.gameObject.SetActive(false);
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
        CtrlZer.instance.checkPointWithTerrainMask();
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
        visPart = ToolController.inste.heightChangerVisPart;
    }

    public override void updateVisPart()
    {
        if (ToolController.inste.ToolVisPartInstance == null)
            return;

        // 显示尺寸：按高度图分辨率换算为世界单位；位置见下方射线与地形交点
        Terrain terrain = Terrain.activeTerrain;
        if (terrain == null || terrain.terrainData == null)
            return;

        // range 与 ApplyHeightBrush 一致：高度图上的“直径”（采样意义下跨度约 2*ceil(range/2)）
        // 预制体为直径 1 的球体，localScale 为世界空间直径（X/Z）；世界步长随 heightmapResolution 变化
        TerrainData td = terrain.terrainData;
        Vector3 tsz = td.size;
        int hres = td.heightmapResolution;
        float worldPerSampleX = tsz.x / Mathf.Max(1, hres - 1);
        float worldPerSampleZ = tsz.z / Mathf.Max(1, hres - 1);
        float diameterInSamples = 2f * Mathf.CeilToInt(range * 0.5f);
        ToolController.inste.ToolVisPartInstance.transform.localScale = new Vector3(
            diameterInSamples * worldPerSampleX,
            1f,
            diameterInSamples * worldPerSampleZ);

        Camera cam = ToolController.inste.orthographicCamera != null
            ? ToolController.inste.orthographicCamera
            : Camera.main;
        if (cam == null)
            return;

        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        var hits = Physics.RaycastAll(ray, Mathf.Infinity);
        if (hits == null || hits.Length == 0)
            return;

        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        foreach (var h in hits)
        {
            if (h.collider == null)
                continue;
            if (h.collider.GetComponent<Terrain>() == null && !(h.collider is TerrainCollider))
                continue;
            ToolController.inste.ToolVisPartInstance.transform.position = h.point;
            break;
        }
    }

    public override void startUse(Vector3 position, GameObject hitObject)
    {
        CtrlZer.instance.checkPointWithHeightmap();
        currentTerrain = hitObject != null
            ? hitObject.GetComponentInChildren<Terrain>()
            : null;
        if (currentTerrain == null)
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
        Vector3 size = terrainData.size;
        int res = terrainData.heightmapResolution;

        Vector3 localPos = worldPos - currentTerrain.transform.position;
        int cx = Mathf.Clamp((int)(localPos.x / size.x * res), 0, res - 1);
        int cy = Mathf.Clamp((int)(localPos.z / size.z * res), 0, res - 1);
        Vector2Int centerCoord = new Vector2Int(cx, cy);
        int radiusInPixels = Mathf.CeilToInt(range / 2);

        float[,] heights = terrainData.GetHeights(0, 0, res, res);

        int startX = Mathf.Max(0, centerCoord.x - radiusInPixels);
        int endX = Mathf.Min(res, centerCoord.x + radiusInPixels);
        int startY = Mathf.Max(0, centerCoord.y - radiusInPixels);
        int endY = Mathf.Min(res, centerCoord.y + radiusInPixels);

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
        CtrlZer.instance.checkPointWithHeightmap();
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
public class SelectionTint : MonoBehaviour
{
    private static readonly Color TINT_TARGET = new Color(0f, 1f, 1f, 1f);
    private const float STRENGTH = 0.35f;
    private MaterialPropertyBlock mpb;

    void Awake()
    {
        mpb = new MaterialPropertyBlock();
    }

    void LateUpdate()
    {
        if (transform.parent == null) return;

        Renderer[] renderers = transform.parent.GetComponentsInChildren<Renderer>();
        foreach (var r in renderers)
        {
            if (r.gameObject == gameObject) continue;
            if (r.sharedMaterial == null || !r.sharedMaterial.HasProperty("_Color")) continue;

            Color original = r.material.color;
            Color tinted = Color.Lerp(original, TINT_TARGET, STRENGTH);

            r.GetPropertyBlock(mpb);
            mpb.SetColor("_Color", tinted);
            r.SetPropertyBlock(mpb);
        }
    }
}

[DefaultExecutionOrder(-100)]
public class BuildingScaleHandle : MonoBehaviour
{
    public bool IsDragging { get; private set; }

    private Building target;

    // 0=right(+X), 1=left(-X), 2=front(+Z), 3=back(-Z) in building local space
    private GameObject[] arrows = new GameObject[4];
    private BoxCollider[] hitZones = new BoxCollider[4];

    private int draggingEdge = -1;
    private float dragStartProj;
    private Vector2 originalSize;
    private bool undoRecorded;

    private Material matX, matZ, matHL;

    public void Init(Building b)
    {
        target = b;

        Shader sh = Shader.Find("Hidden/Internal-Colored");
        if (sh == null) sh = Shader.Find("Sprites/Default");
        if (sh == null) sh = Shader.Find("Unlit/Color");

        matX = MkMat(sh, new Color(1f, 0.15f, 0.15f));
        matZ = MkMat(sh, new Color(0.15f, 0.4f, 1f));
        matHL = MkMat(sh, new Color(1f, 1f, 0.2f));

        for (int i = 0; i < 4; i++)
        {
            arrows[i] = MkArrowVisual(i < 2 ? matX : matZ);
            arrows[i].transform.SetParent(transform, false);

            GameObject hitGO = new GameObject("H" + i);
            hitGO.transform.SetParent(transform, false);
            hitGO.layer = 2;
            hitZones[i] = hitGO.AddComponent<BoxCollider>();
        }
        Sync();
    }

    private Material MkMat(Shader sh, Color c)
    {
        Material m = new Material(sh);
        m.color = c;
        m.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.Always);
        m.SetInt("_ZWrite", 0);
        m.renderQueue = 4000;
        return m;
    }

    private GameObject MkArrowVisual(Material mat)
    {
        GameObject root = new GameObject("A");

        // Shaft: thin stretched cube
        GameObject shaft = GameObject.CreatePrimitive(PrimitiveType.Cube);
        shaft.name = "S";
        shaft.transform.SetParent(root.transform, false);
        Destroy(shaft.GetComponent<Collider>());
        shaft.GetComponent<Renderer>().material = mat;
        shaft.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        // Tip: cube rotated 45° around forward to look like a diamond/arrowhead
        GameObject tip = GameObject.CreatePrimitive(PrimitiveType.Cube);
        tip.name = "T";
        tip.transform.SetParent(root.transform, false);
        Destroy(tip.GetComponent<Collider>());
        tip.GetComponent<Renderer>().material = mat;
        tip.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        return root;
    }

    private void Sync()
    {
        if (target == null) return;
        Transform bt = target.gameObject.transform;

        // The building prefab body cube has localPosition (0.5, 0.5, -0.5)
        // So the visible cube occupies local X:[0,1], Y:[0,1], Z:[-1,0]
        // Edge centers at mid-height (Y=0.5):
        Vector3 rightEdge = bt.TransformPoint(1f, 0.5f, -0.5f);
        Vector3 leftEdge  = bt.TransformPoint(0f, 0.5f, -0.5f);
        Vector3 frontEdge = bt.TransformPoint(0.5f, 0.5f, 0f);
        Vector3 backEdge  = bt.TransformPoint(0.5f, 0.5f, -1f);

        Vector3 wRight = bt.right;
        Vector3 wFwd   = bt.forward;

        float halfX = Vector3.Distance(rightEdge, leftEdge) * 0.5f;
        float halfZ = Vector3.Distance(frontEdge, backEdge) * 0.5f;

        float slen = Mathf.Clamp(Mathf.Min(halfX, halfZ) * 0.35f, 0.4f, 2f);
        float tipSz = Mathf.Clamp(Mathf.Min(halfX, halfZ) * 0.25f, 0.3f, 1f);
        float bldH = target.height * 1.5f;
        float ah = Mathf.Clamp(bldH * 0.15f, 0.15f, 1f);

        PlaceArrow(0, rightEdge, wRight, slen, tipSz, ah);
        PlaceArrow(1, leftEdge,  -wRight, slen, tipSz, ah);
        PlaceArrow(2, frontEdge, wFwd,   slen, tipSz, ah);
        PlaceArrow(3, backEdge,  -wFwd,  slen, tipSz, ah);
    }

    private Vector3 WorldOutward(int edge)
    {
        Transform bt = target.gameObject.transform;
        switch (edge)
        {
            case 0: return bt.right;
            case 1: return -bt.right;
            case 2: return bt.forward;
            default: return -bt.forward;
        }
    }

    private void PlaceArrow(int idx, Vector3 edgePos, Vector3 outDir, float slen, float tipSz, float ah)
    {
        Quaternion look = Quaternion.LookRotation(outDir, Vector3.up);

        Transform shaft = arrows[idx].transform.GetChild(0);
        shaft.position = edgePos + outDir * (slen * 0.5f);
        shaft.rotation = look;
        shaft.localScale = new Vector3(0.12f, ah, slen);

        Transform tip = arrows[idx].transform.GetChild(1);
        tip.position = edgePos + outDir * (slen + tipSz * 0.35f);
        tip.rotation = look * Quaternion.Euler(0, 0, 45);
        tip.localScale = new Vector3(tipSz, ah, tipSz);

        float totalLen = slen + tipSz;
        hitZones[idx].transform.position = edgePos + outDir * (totalLen * 0.5f);
        hitZones[idx].transform.rotation = look;
        hitZones[idx].size = new Vector3(Mathf.Max(tipSz * 2.5f, 1.5f), Mathf.Max(ah * 4, 2f), totalLen + 0.5f);
        hitZones[idx].center = Vector3.zero;
    }

    void Update()
    {
        if (target == null) { Destroy(gameObject); return; }
        Sync();

        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);

        if (!IsDragging)
        {
            for (int i = 0; i < 4; i++)
                SetMat(i, i < 2 ? matX : matZ);

            int hovered = -1;
            float best = float.MaxValue;
            for (int i = 0; i < 4; i++)
            {
                RaycastHit hit;
                if (hitZones[i].Raycast(ray, out hit, 500f) && hit.distance < best)
                { best = hit.distance; hovered = i; }
            }
            if (hovered >= 0)
                SetMat(hovered, matHL);

            if (Input.GetMouseButtonDown(0) && hovered >= 0)
                BeginDrag(hovered, Vector3.zero);
        }
        else
        {
            ApplyDrag(Vector3.zero);
            if (Input.GetMouseButtonUp(0))
                EndDrag();
        }
    }

    private Vector3 dragStartScreen;
    private Vector2 dragScreenDir;
    private float dragPixelsPerHalf;

    private Vector3 GetVisualCenter()
    {
        return target.gameObject.transform.TransformPoint(0.5f, 0.5f, -0.5f);
    }

    private Vector2 originalPosition;

    private void BeginDrag(int edge, Vector3 worldHit)
    {
        IsDragging = true;
        draggingEdge = edge;
        originalSize = target.size;
        originalPosition = target.position;
        undoRecorded = false;
        dragStartScreen = Input.mousePosition;

        Transform bt = target.gameObject.transform;
        Vector3 outward;
        bool isX = edge < 2;
        switch (edge)
        {
            case 0: outward = bt.right; break;
            case 1: outward = -bt.right; break;
            case 2: outward = bt.forward; break;
            default: outward = -bt.forward; break;
        }
        float halfExt = isX ? originalSize.x / 4f : originalSize.y / 4f;

        Vector3 center3D = GetVisualCenter();
        Vector3 edge3D = center3D + outward * halfExt;

        Vector3 centerScr = Camera.main.WorldToScreenPoint(center3D);
        Vector3 edgeScr = Camera.main.WorldToScreenPoint(edge3D);

        dragScreenDir = new Vector2(edgeScr.x - centerScr.x, edgeScr.y - centerScr.y);
        dragPixelsPerHalf = dragScreenDir.magnitude;
        if (dragPixelsPerHalf > 0.01f)
            dragScreenDir /= dragPixelsPerHalf;
        else
            dragPixelsPerHalf = 1f;

        SetMat(edge, matHL);
    }

    private void ApplyDrag(Vector3 worldPos)
    {
        Vector2 mouseDelta = new Vector2(
            Input.mousePosition.x - dragStartScreen.x,
            Input.mousePosition.y - dragStartScreen.y
        );

        float screenProj = Vector2.Dot(mouseDelta, dragScreenDir);

        bool isX = draggingEdge < 2;
        float origDim = isX ? originalSize.x : originalSize.y;
        float delta = screenProj * (origDim * 0.5f) / dragPixelsPerHalf;

        if (!undoRecorded && Mathf.Abs(delta) > 0.5f)
        {
            CtrlZer.instance.checkPointTransformOnly();
            undoRecorded = true;
        }

        float newVal = Mathf.Max(1f, origDim + delta);
        float dimChange = newVal - origDim;

        Vector2 newSize;
        if (isX)
            newSize = new Vector2(newVal, originalSize.y);
        else
            newSize = new Vector2(originalSize.x, newVal);

        // One-sided extension: the DRAGGED edge moves, the OPPOSITE edge stays fixed.
        // Building prefab body at localPosition (0.5, 0.5, -0.5):
        //   X grows from root rightward  (local X: 0 → size.x/2)
        //   Z grows from root backward   (local Z: 0 → -size.y/2)
        //
        // Edge 0 (right):  grows right naturally, left edge at X=0 stays. No shift.
        // Edge 1 (left):   want left to extend. Shift root LEFT by dimChange/2.
        // Edge 2 (front):  Z grows backward naturally. Shift root FORWARD by dimChange/2
        //                  so the back edge stays and front extends.
        // Edge 3 (back):   grows backward naturally, front edge at Z=0 stays. No shift.
        Quaternion rot = target.gameObject.transform.rotation;
        Vector3 worldShift = Vector3.zero;

        if (draggingEdge == 1)
            worldShift = rot * new Vector3(-dimChange / 2f, 0f, 0f);
        else if (draggingEdge == 2)
            worldShift = rot * new Vector3(0f, 0f, dimChange / 2f);

        target.position = originalPosition + new Vector2(worldShift.x * 2f, -worldShift.z * 2f);
        target.size = newSize;
        target.scatterThis();
    }

    private void EndDrag()
    {
        IsDragging = false;
        draggingEdge = -1;
    }

    private void SetMat(int idx, Material mat)
    {
        foreach (Renderer r in arrows[idx].GetComponentsInChildren<Renderer>())
            r.material = mat;
    }

    void OnDestroy()
    {
        if (matX != null) Destroy(matX);
        if (matZ != null) Destroy(matZ);
        if (matHL != null) Destroy(matHL);
    }
}

/// <summary>
/// 选中 Platform 时在场景中与路径顶点交互：每对 R[i]—L[i] 的横连线、以及 L[i]—R[i+1] 的斜连线（与桥面四边形边一致），加可拖拽手柄（Layer2 + Collider.Raycast；拖拽时 handleBusy 屏蔽主工具）。
/// </summary>
[DefaultExecutionOrder(-100)]
public class PlatformPathHandle : MonoBehaviour
{
    public bool IsDragging { get; private set; }

    private Platform target;
    private Transform linksRoot;
    private Transform handlesRoot;

    private List<LineRenderer> pairLines = new List<LineRenderer>();
    /// <summary>每段 L[i] — R[i+1]，与桥面四边形对角一致。</summary>
    private List<LineRenderer> crossLines = new List<LineRenderer>();
    private List<BoxCollider> hitZones = new List<BoxCollider>();
    private List<Renderer> handleRenderers = new List<Renderer>();

    private Material matLine;
    private Material matR;
    private Material matL;
    private Material matHL;

    private int vertexCount;
    private int hoveredSlot = -1;
    private int dragSlot = -1;
    private bool undoRecorded;

    private const float LiftY = 0.35f;
    private const float HitHalf = 0.55f;

    public void Init(Platform plt)
    {
        target = plt;
        linksRoot = new GameObject("Links").transform;
        linksRoot.SetParent(transform, false);
        handlesRoot = new GameObject("Handles").transform;
        handlesRoot.SetParent(transform, false);

        Shader sh = Shader.Find("Hidden/Internal-Colored");
        if (sh == null) sh = Shader.Find("Sprites/Default");
        if (sh == null) sh = Shader.Find("Unlit/Color");

        matLine = MkMat(sh, new Color(1f, 0.92f, 0.1f, 1f));
        matR = MkMat(sh, new Color(0.15f, 0.95f, 1f, 1f));
        matL = MkMat(sh, new Color(1f, 0.2f, 0.85f, 1f));
        matHL = MkMat(sh, new Color(1f, 1f, 1f, 1f));

        RebuildIfNeeded();
    }

    private static Material MkMat(Shader sh, Color c)
    {
        Material m = new Material(sh);
        m.color = c;
        m.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.Always);
        m.SetInt("_ZWrite", 0);
        m.renderQueue = 4000;
        return m;
    }

    private static int CrossLineCount(int n) => n >= 2 ? n - 1 : 0;

    private void RebuildIfNeeded()
    {
        int n = GetPairCount();
        int expectedCross = CrossLineCount(n);
        if (n == vertexCount && n == pairLines.Count && crossLines.Count == expectedCross && hitZones.Count == n * 2)
            return;

        foreach (var lr in pairLines)
            if (lr != null) Destroy(lr.gameObject);
        pairLines.Clear();
        foreach (var lr in crossLines)
            if (lr != null) Destroy(lr.gameObject);
        crossLines.Clear();
        foreach (Transform t in handlesRoot)
            if (t != null) Destroy(t.gameObject);
        hitZones.Clear();
        handleRenderers.Clear();

        vertexCount = n;
        for (int i = 0; i < n; i++)
        {
            GameObject lrGo = new GameObject("PairLine_" + i);
            lrGo.transform.SetParent(linksRoot, false);
            LineRenderer lr = lrGo.AddComponent<LineRenderer>();
            lr.positionCount = 2;
            lr.useWorldSpace = true;
            lr.startWidth = 0.22f;
            lr.endWidth = 0.22f;
            lr.material = matLine;
            lr.startColor = matLine.color;
            lr.endColor = matLine.color;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            pairLines.Add(lr);

            MkVertexHandle(i, true);
            MkVertexHandle(i, false);
        }

        for (int i = 0; i < expectedCross; i++)
        {
            GameObject lrGo = new GameObject("Cross_LR_" + i);
            lrGo.transform.SetParent(linksRoot, false);
            LineRenderer lr = lrGo.AddComponent<LineRenderer>();
            lr.positionCount = 2;
            lr.useWorldSpace = true;
            lr.startWidth = 0.18f;
            lr.endWidth = 0.18f;
            lr.material = matLine;
            lr.startColor = new Color(matLine.color.r, matLine.color.g, matLine.color.b, 0.85f);
            lr.endColor = lr.startColor;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            crossLines.Add(lr);
        }
    }

    private void MkVertexHandle(int index, bool isRight)
    {
        GameObject root = new GameObject((isRight ? "R_" : "L_") + index);
        root.transform.SetParent(handlesRoot, false);

        GameObject vis = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        vis.name = "Vis";
        vis.transform.SetParent(root.transform, false);
        vis.transform.localScale = Vector3.one * 0.38f;
        Destroy(vis.GetComponent<Collider>());
        Renderer rend = vis.GetComponent<Renderer>();
        rend.material = isRight ? matR : matL;
        rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        GameObject hitGO = new GameObject("Hit");
        hitGO.transform.SetParent(root.transform, false);
        hitGO.layer = 2;
        BoxCollider box = hitGO.AddComponent<BoxCollider>();
        box.size = Vector3.one * (HitHalf * 2f);
        box.center = Vector3.zero;

        hitZones.Add(box);
        handleRenderers.Add(rend);
    }

    private int GetPairCount()
    {
        if (target == null) return 0;
        int rc = target.positinLineR != null ? target.positinLineR.Count : 0;
        int lc = target.positinLineL != null ? target.positinLineL.Count : 0;
        return Mathf.Min(rc, lc);
    }

    private static void BuildPathHeights(Platform p, List<float> heightsOut)
    {
        heightsOut.Clear();
        if (p.positinLineR == null) return;
        for (int j = 0; j < p.positinLineR.Count; j++)
        {
            Vector3 pot = Vector3.zero;
            VpMetaToucher.getXYHeightWithLayer(MathOfRwrme.SvgPosToU3dPos(p.positinLineR[j]), p.layerIndex, ref pot);
            heightsOut.Add(pot.y);
        }
        if (p.isDeck)
        {
            for (int j = 0; j < heightsOut.Count; j++)
                heightsOut[j] += p.height;
        }
    }

    private static Vector3 VertexWorld(Platform p, int i, bool useRight, List<float> pathHeight)
    {
        Vector2 svg = useRight ? p.positinLineR[i] : p.positinLineL[i];
        Vector2 u3d = MathOfRwrme.SvgPosToU3dPos(svg);
        float y = pathHeight[i];
        return new Vector3(u3d.x, y + LiftY, u3d.y);
    }

    private void SyncTransforms(List<float> pathHeight, int n)
    {
        for (int i = 0; i < n; i++)
        {
            Vector3 wR = VertexWorld(target, i, true, pathHeight);
            Vector3 wL = VertexWorld(target, i, false, pathHeight);
            pairLines[i].SetPosition(0, wR);
            pairLines[i].SetPosition(1, wL);

            int slotR = i * 2;
            int slotL = i * 2 + 1;
            handlesRoot.GetChild(slotR).position = wR;
            handlesRoot.GetChild(slotL).position = wL;
        }

        for (int i = 0; i < n - 1; i++)
        {
            Vector3 wLi = VertexWorld(target, i, false, pathHeight);
            Vector3 wRNext = VertexWorld(target, i + 1, true, pathHeight);
            crossLines[i].SetPosition(0, wLi);
            crossLines[i].SetPosition(1, wRNext);
        }
    }

    void Update()
    {
        if (target == null) { Destroy(gameObject); return; }

        RebuildIfNeeded();
        int n = GetPairCount();
        if (n == 0)
        {
            linksRoot.gameObject.SetActive(false);
            handlesRoot.gameObject.SetActive(false);
            return;
        }
        linksRoot.gameObject.SetActive(true);
        handlesRoot.gameObject.SetActive(true);

        List<float> pathHeights = new List<float>();
        BuildPathHeights(target, pathHeights);
        if (pathHeights.Count < n)
            return;

        SyncTransforms(pathHeights, n);

        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);

        if (!IsDragging)
        {
            for (int s = 0; s < hitZones.Count; s++)
                handleRenderers[s].material = IsRightSlot(s) ? matR : matL;

            hoveredSlot = -1;
            float best = float.MaxValue;
            for (int s = 0; s < hitZones.Count; s++)
            {
                RaycastHit hit;
                if (hitZones[s].Raycast(ray, out hit, 800f) && hit.distance < best)
                {
                    best = hit.distance;
                    hoveredSlot = s;
                }
            }
            if (hoveredSlot >= 0)
                handleRenderers[hoveredSlot].material = matHL;

            if (Input.GetMouseButtonDown(0) && hoveredSlot >= 0)
            {
                CtrlZer.instance.checkPoint();
                IsDragging = true;
                dragSlot = hoveredSlot;
                undoRecorded = false;
            }
        }
        else
        {
            handleRenderers[dragSlot].material = matHL;
            ApplyDrag();
            if (Input.GetMouseButtonUp(0))
            {
                IsDragging = false;
                dragSlot = -1;
            }
        }
    }

    private static bool IsRightSlot(int slot) => (slot & 1) == 0;

    private void ApplyDrag()
    {
        int layerMask = 1 << 6;
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;
        if (!Physics.Raycast(ray, out hit, Mathf.Infinity, layerMask))
            return;

        int i = dragSlot / 2;
        bool right = IsRightSlot(dragSlot);
        Vector2 svg = MathOfRwrme.U3dPosToSvgPos(new Vector2(hit.point.x, hit.point.z));

        Vector2 before = right ? target.positinLineR[i] : target.positinLineL[i];
        if (!undoRecorded && (svg - before).sqrMagnitude > 0.0004f)
        {
            CtrlZer.instance.checkPoint();
            undoRecorded = true;
        }

        if (right) target.positinLineR[i] = svg;
        else target.positinLineL[i] = svg;

        target.scatterThis();
    }

    void OnDestroy()
    {
        if (matLine != null) Destroy(matLine);
        if (matR != null) Destroy(matR);
        if (matL != null) Destroy(matL);
        if (matHL != null) Destroy(matHL);
    }
}

/// <summary>
/// 选中 Wall 时在场景中显示路径折线与顶点手柄；Layer2 碰撞体 + Raycast，拖拽投影到地形（Layer6）更新 SVG 顶点（与 PlatformPathHandle 一致）。
/// </summary>
[DefaultExecutionOrder(-100)]
public class WallPathHandle : MonoBehaviour
{
    public bool IsDragging { get; private set; }

    private Wall target;
    private Transform linksRoot;
    private Transform handlesRoot;

    private List<LineRenderer> segmentLines = new List<LineRenderer>();
    private List<BoxCollider> hitZones = new List<BoxCollider>();
    private List<Renderer> handleRenderers = new List<Renderer>();

    private Material matLine;
    private Material matV;
    private Material matHL;

    private int vertexCount = -1;
    private int hoveredSlot = -1;
    private int dragSlot = -1;
    private bool undoRecorded;

    private const float LiftY = 0.35f;
    private const float HitHalf = 0.55f;

    public void Init(Wall wall)
    {
        target = wall;
        linksRoot = new GameObject("Links").transform;
        linksRoot.SetParent(transform, false);
        handlesRoot = new GameObject("Handles").transform;
        handlesRoot.SetParent(transform, false);

        Shader sh = Shader.Find("Hidden/Internal-Colored");
        if (sh == null) sh = Shader.Find("Sprites/Default");
        if (sh == null) sh = Shader.Find("Unlit/Color");

        matLine = MkMat(sh, new Color(1f, 0.88f, 0.15f, 1f));
        matV = MkMat(sh, new Color(0.25f, 0.92f, 1f, 1f));
        matHL = MkMat(sh, Color.white);

        RebuildIfNeeded();
    }

    private static Material MkMat(Shader sh, Color c)
    {
        Material m = new Material(sh);
        m.color = c;
        m.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.Always);
        m.SetInt("_ZWrite", 0);
        m.renderQueue = 4000;
        return m;
    }

    private int GetVertexCount()
    {
        if (target == null || target.positionLine == null) return 0;
        return target.positionLine.Count;
    }

    private void RebuildIfNeeded()
    {
        int n = GetVertexCount();
        int expectedSeg = n >= 2 ? n - 1 : 0;
        if (n == vertexCount && segmentLines.Count == expectedSeg && hitZones.Count == n)
            return;

        foreach (var lr in segmentLines)
            if (lr != null) Destroy(lr.gameObject);
        segmentLines.Clear();
        foreach (Transform t in handlesRoot)
            if (t != null) Destroy(t.gameObject);
        hitZones.Clear();
        handleRenderers.Clear();

        vertexCount = n;

        for (int i = 0; i < expectedSeg; i++)
        {
            GameObject lrGo = new GameObject("Seg_" + i);
            lrGo.transform.SetParent(linksRoot, false);
            LineRenderer lr = lrGo.AddComponent<LineRenderer>();
            lr.positionCount = 2;
            lr.useWorldSpace = true;
            lr.startWidth = 0.2f;
            lr.endWidth = 0.2f;
            lr.material = matLine;
            lr.startColor = matLine.color;
            lr.endColor = matLine.color;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            segmentLines.Add(lr);
        }

        for (int i = 0; i < n; i++)
            MkVertexHandle(i);
    }

    private void MkVertexHandle(int index)
    {
        GameObject root = new GameObject("V_" + index);
        root.transform.SetParent(handlesRoot, false);

        GameObject vis = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        vis.name = "Vis";
        vis.transform.SetParent(root.transform, false);
        vis.transform.localScale = Vector3.one * 0.36f;
        Destroy(vis.GetComponent<Collider>());
        Renderer rend = vis.GetComponent<Renderer>();
        rend.material = matV;
        rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        GameObject hitGO = new GameObject("Hit");
        hitGO.transform.SetParent(root.transform, false);
        hitGO.layer = 2;
        BoxCollider box = hitGO.AddComponent<BoxCollider>();
        box.size = Vector3.one * (HitHalf * 2f);
        box.center = Vector3.zero;

        hitZones.Add(box);
        handleRenderers.Add(rend);
    }

    private Vector3 VertexWorld(int i)
    {
        Vector2 svg = target.positionLine[i];
        Vector3 pot = Vector3.zero;
        VpMetaToucher.getXYHeightWithLayer(MathOfRwrme.SvgPosToU3dPos(svg), target.layerIndex, ref pot, target.Rank);
        return new Vector3(pot.x, pot.y + LiftY, pot.z);
    }

    private void SyncTransforms(int n)
    {
        for (int i = 0; i < n - 1; i++)
        {
            segmentLines[i].SetPosition(0, VertexWorld(i));
            segmentLines[i].SetPosition(1, VertexWorld(i + 1));
        }

        for (int i = 0; i < n; i++)
            handlesRoot.GetChild(i).position = VertexWorld(i);
    }

    void Update()
    {
        if (target == null) { Destroy(gameObject); return; }

        RebuildIfNeeded();
        int n = GetVertexCount();
        if (n == 0)
        {
            linksRoot.gameObject.SetActive(false);
            handlesRoot.gameObject.SetActive(false);
            return;
        }
        linksRoot.gameObject.SetActive(true);
        handlesRoot.gameObject.SetActive(true);

        SyncTransforms(n);

        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);

        if (!IsDragging)
        {
            for (int s = 0; s < handleRenderers.Count; s++)
                handleRenderers[s].material = matV;

            hoveredSlot = -1;
            float best = float.MaxValue;
            for (int s = 0; s < hitZones.Count; s++)
            {
                RaycastHit hit;
                if (hitZones[s].Raycast(ray, out hit, 800f) && hit.distance < best)
                {
                    best = hit.distance;
                    hoveredSlot = s;
                }
            }
            if (hoveredSlot >= 0)
                handleRenderers[hoveredSlot].material = matHL;

            if (Input.GetMouseButtonDown(0) && hoveredSlot >= 0)
            {
                CtrlZer.instance.checkPoint();
                IsDragging = true;
                dragSlot = hoveredSlot;
                undoRecorded = false;
            }
        }
        else
        {
            handleRenderers[dragSlot].material = matHL;
            ApplyDrag();
            if (Input.GetMouseButtonUp(0))
            {
                IsDragging = false;
                dragSlot = -1;
            }
        }
    }

    private void ApplyDrag()
    {
        int layerMask = 1 << 6;
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;
        if (!Physics.Raycast(ray, out hit, Mathf.Infinity, layerMask))
            return;

        int i = dragSlot;
        Vector2 svg = MathOfRwrme.U3dPosToSvgPos(new Vector2(hit.point.x, hit.point.z));
        Vector2 before = target.positionLine[i];
        if (!undoRecorded && (svg - before).sqrMagnitude > 0.0004f)
        {
            CtrlZer.instance.checkPoint();
            undoRecorded = true;
        }

        target.positionLine[i] = svg;
        target.scatterThis();
    }

    void OnDestroy()
    {
        if (matLine != null) Destroy(matLine);
        if (matV != null) Destroy(matV);
        if (matHL != null) Destroy(matHL);
    }
}

/// <summary>
/// 选中 Offroad 时：锚点折线 + 顶点手柄；对每条三次段（curve[i] 且 controlPoints 足够）显示两个贝塞尔控制点手柄及 p0—c1、c2—p3 辅助线。取高与 Offroad 一致（offroadHeightSampleLayer + heightOffset）。
/// </summary>
[DefaultExecutionOrder(-100)]
public class OffroadPathHandle : MonoBehaviour
{
    public bool IsDragging { get; private set; }

    private Offroad target;
    private Transform linksRoot;
    private Transform tangentsRoot;
    private Transform handlesRoot;

    private List<LineRenderer> segmentLines = new List<LineRenderer>();
    private List<LineRenderer> tangentLines = new List<LineRenderer>();
    private List<BoxCollider> hitZones = new List<BoxCollider>();
    private List<Renderer> handleRenderers = new List<Renderer>();
    /// <summary>与 handlesRoot 子顺序一致：先 n 个锚点，再控制点；本列表仅控制点槽位有效，长度 = hitZones.Count - anchorCount。</summary>
    private readonly List<int> cpHandleToListIndex = new List<int>();
    private readonly List<(int iEnd, int cp0, int cp1)> cubicTangentSpecs = new List<(int, int, int)>();

    private Material matLine;
    private Material matTangent;
    private Material matV;
    private Material matCp;
    private Material matHL;

    private int layoutAnchorCount = -1;
    private int layoutCpHandleCount = -1;
    private int layoutTangentLineCount = -1;
    private int hoveredSlot = -1;
    private int dragSlot = -1;
    private bool undoRecorded;

    private const float LiftY = 0.35f;
    private const float HitHalf = 0.55f;
    private const float HitHalfCp = 0.48f;

    public void Init(Offroad offroad)
    {
        target = offroad;
        linksRoot = new GameObject("Links").transform;
        linksRoot.SetParent(transform, false);
        tangentsRoot = new GameObject("Tangents").transform;
        tangentsRoot.SetParent(transform, false);
        handlesRoot = new GameObject("Handles").transform;
        handlesRoot.SetParent(transform, false);

        Shader sh = Shader.Find("Hidden/Internal-Colored");
        if (sh == null) sh = Shader.Find("Sprites/Default");
        if (sh == null) sh = Shader.Find("Unlit/Color");

        matLine = MkMat(sh, new Color(0.35f, 1f, 0.55f, 1f));
        matTangent = MkMat(sh, new Color(0.95f, 0.55f, 1f, 0.55f));
        matV = MkMat(sh, new Color(0.25f, 0.92f, 1f, 1f));
        matCp = MkMat(sh, new Color(1f, 0.62f, 0.18f, 1f));
        matHL = MkMat(sh, Color.white);

        RebuildIfNeeded();
    }

    private static Material MkMat(Shader sh, Color c)
    {
        Material m = new Material(sh);
        m.color = c;
        m.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.Always);
        m.SetInt("_ZWrite", 0);
        m.renderQueue = 4000;
        return m;
    }

    private int GetVertexCount()
    {
        if (target == null || target.positionLine == null) return 0;
        return target.positionLine.Count;
    }

    static void WalkOffroadCpLayout(Offroad t, int n, System.Collections.Generic.List<int> cpIdxList, System.Collections.Generic.List<(int iEnd, int cp0, int cp1)> tangSpecs)
    {
        cpIdxList.Clear();
        tangSpecs.Clear();
        if (n < 2 || t == null) return;
        bool hasCurve = t.curve != null && t.curve.Count == n;
        bool hasCp = t.controlPoints != null && t.controlPoints.Count >= 2;
        int cpIndex = 0;
        for (int i = 1; i < n; i++)
        {
            bool cubic = hasCurve && t.curve[i] && hasCp && cpIndex + 1 < t.controlPoints.Count;
            if (cubic)
            {
                cpIdxList.Add(cpIndex);
                cpIdxList.Add(cpIndex + 1);
                tangSpecs.Add((i, cpIndex, cpIndex + 1));
                cpIndex += 2;
            }
        }
    }

    private void RebuildIfNeeded()
    {
        int n = GetVertexCount();
        int expectedSeg = n >= 2 ? n - 1 : 0;
        WalkOffroadCpLayout(target, n, cpHandleToListIndex, cubicTangentSpecs);
        int cpH = cpHandleToListIndex.Count;
        int tangL = cubicTangentSpecs.Count * 2;
        int totalHandles = n + cpH;

        if (n == layoutAnchorCount && cpH == layoutCpHandleCount && tangL == layoutTangentLineCount
            && segmentLines.Count == expectedSeg && hitZones.Count == totalHandles)
            return;

        foreach (var lr in segmentLines)
            if (lr != null) Destroy(lr.gameObject);
        segmentLines.Clear();
        foreach (var lr in tangentLines)
            if (lr != null) Destroy(lr.gameObject);
        tangentLines.Clear();
        foreach (Transform t in handlesRoot)
            if (t != null) Destroy(t.gameObject);
        hitZones.Clear();
        handleRenderers.Clear();

        layoutAnchorCount = n;
        layoutCpHandleCount = cpH;
        layoutTangentLineCount = tangL;

        for (int i = 0; i < expectedSeg; i++)
        {
            GameObject lrGo = new GameObject("Seg_" + i);
            lrGo.transform.SetParent(linksRoot, false);
            LineRenderer lr = lrGo.AddComponent<LineRenderer>();
            lr.positionCount = 2;
            lr.useWorldSpace = true;
            lr.startWidth = 0.2f;
            lr.endWidth = 0.2f;
            lr.material = matLine;
            lr.startColor = matLine.color;
            lr.endColor = matLine.color;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            segmentLines.Add(lr);
        }

        for (int t = 0; t < tangL; t++)
        {
            GameObject lrGo = new GameObject("Tan_" + t);
            lrGo.transform.SetParent(tangentsRoot, false);
            LineRenderer lr = lrGo.AddComponent<LineRenderer>();
            lr.positionCount = 2;
            lr.useWorldSpace = true;
            lr.startWidth = 0.1f;
            lr.endWidth = 0.1f;
            lr.material = matTangent;
            lr.startColor = matTangent.color;
            lr.endColor = matTangent.color;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            tangentLines.Add(lr);
        }

        for (int i = 0; i < n; i++)
            MkVertexHandle(i, false);
        for (int j = 0; j < cpH; j++)
            MkVertexHandle(j, true);
    }

    private void MkVertexHandle(int index, bool isControlPoint)
    {
        string nm = isControlPoint ? "C_" + index : "V_" + index;
        GameObject root = new GameObject(nm);
        root.transform.SetParent(handlesRoot, false);

        GameObject vis = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        vis.name = "Vis";
        vis.transform.SetParent(root.transform, false);
        vis.transform.localScale = Vector3.one * (isControlPoint ? 0.3f : 0.36f);
        Destroy(vis.GetComponent<Collider>());
        Renderer rend = vis.GetComponent<Renderer>();
        rend.material = isControlPoint ? matCp : matV;
        rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        GameObject hitGO = new GameObject("Hit");
        hitGO.transform.SetParent(root.transform, false);
        hitGO.layer = 2;
        BoxCollider box = hitGO.AddComponent<BoxCollider>();
        float h = isControlPoint ? HitHalfCp : HitHalf;
        box.size = Vector3.one * (h * 2f);
        box.center = Vector3.zero;

        hitZones.Add(box);
        handleRenderers.Add(rend);
    }

    private Vector3 SvgPointWorld(Vector2 svg)
    {
        Vector3 pot = Vector3.zero;
        int hLayer = MetaMap.instance != null ? MetaMap.instance.offroadHeightSampleLayer : 4;
        VpMetaToucher.getXYHeightWithLayer(MathOfRwrme.SvgPosToU3dPos(svg), hLayer, ref pot, target.Rank);
        return new Vector3(pot.x, pot.y + LiftY + target.heightOffset, pot.z);
    }

    private Vector3 VertexWorld(int i)
    {
        return SvgPointWorld(target.positionLine[i]);
    }

    private Vector3 CpWorld(int cpListIndex)
    {
        return SvgPointWorld(target.controlPoints[cpListIndex]);
    }

    private void SyncTransforms(int n)
    {
        for (int i = 0; i < n - 1; i++)
        {
            segmentLines[i].SetPosition(0, VertexWorld(i));
            segmentLines[i].SetPosition(1, VertexWorld(i + 1));
        }

        for (int k = 0; k < cubicTangentSpecs.Count; k++)
        {
            var spec = cubicTangentSpecs[k];
            int iEnd = spec.iEnd;
            int a = spec.cp0;
            int b = spec.cp1;
            tangentLines[k * 2].SetPosition(0, VertexWorld(iEnd - 1));
            tangentLines[k * 2].SetPosition(1, CpWorld(a));
            tangentLines[k * 2 + 1].SetPosition(0, CpWorld(b));
            tangentLines[k * 2 + 1].SetPosition(1, VertexWorld(iEnd));
        }

        for (int i = 0; i < n; i++)
            handlesRoot.GetChild(i).position = VertexWorld(i);
        for (int j = 0; j < cpHandleToListIndex.Count; j++)
            handlesRoot.GetChild(n + j).position = CpWorld(cpHandleToListIndex[j]);
    }

    void Update()
    {
        if (target == null) { Destroy(gameObject); return; }

        RebuildIfNeeded();
        int n = GetVertexCount();
        if (n == 0)
        {
            linksRoot.gameObject.SetActive(false);
            tangentsRoot.gameObject.SetActive(false);
            handlesRoot.gameObject.SetActive(false);
            return;
        }
        linksRoot.gameObject.SetActive(true);
        tangentsRoot.gameObject.SetActive(cubicTangentSpecs.Count > 0);
        handlesRoot.gameObject.SetActive(true);

        SyncTransforms(n);

        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);

        if (!IsDragging)
        {
            for (int s = 0; s < n; s++)
                handleRenderers[s].material = matV;
            for (int s = n; s < handleRenderers.Count; s++)
                handleRenderers[s].material = matCp;

            hoveredSlot = -1;
            float best = float.MaxValue;
            for (int s = 0; s < hitZones.Count; s++)
            {
                RaycastHit hit;
                if (hitZones[s].Raycast(ray, out hit, 800f) && hit.distance < best)
                {
                    best = hit.distance;
                    hoveredSlot = s;
                }
            }
            if (hoveredSlot >= 0)
                handleRenderers[hoveredSlot].material = matHL;

            if (Input.GetMouseButtonDown(0) && hoveredSlot >= 0)
            {
                CtrlZer.instance.checkPoint();
                IsDragging = true;
                dragSlot = hoveredSlot;
                undoRecorded = false;
            }
        }
        else
        {
            handleRenderers[dragSlot].material = matHL;
            ApplyDrag(n);
            if (Input.GetMouseButtonUp(0))
            {
                IsDragging = false;
                dragSlot = -1;
            }
        }
    }

    private void ApplyDrag(int n)
    {
        int layerMask = 1 << 6;
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;
        if (!Physics.Raycast(ray, out hit, Mathf.Infinity, layerMask))
            return;

        Vector2 svg = MathOfRwrme.U3dPosToSvgPos(new Vector2(hit.point.x, hit.point.z));

        if (dragSlot < n)
        {
            int i = dragSlot;
            Vector2 before = target.positionLine[i];
            if (!undoRecorded && (svg - before).sqrMagnitude > 0.0004f)
            {
                CtrlZer.instance.checkPoint();
                undoRecorded = true;
            }
            target.positionLine[i] = svg;
        }
        else
        {
            int cpList = cpHandleToListIndex[dragSlot - n];
            Vector2 before = target.controlPoints[cpList];
            if (!undoRecorded && (svg - before).sqrMagnitude > 0.0004f)
            {
                CtrlZer.instance.checkPoint();
                undoRecorded = true;
            }
            target.controlPoints[cpList] = svg;
        }

        target.scatterThis();
    }

    void OnDestroy()
    {
        if (matLine != null) Destroy(matLine);
        if (matTangent != null) Destroy(matTangent);
        if (matV != null) Destroy(matV);
        if (matCp != null) Destroy(matCp);
        if (matHL != null) Destroy(matHL);
    }
}
