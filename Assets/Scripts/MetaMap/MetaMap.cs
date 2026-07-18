using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

public class MetaMap : MonoBehaviour
{
    public static MetaMap instance; 
    public int mapSizeX;
    public int mapSizeY;
    public MetaTerrain m_metaTerrain; // terrain5 标准高度图（预览基准，只读）
    public Layer defaultLayer;//default: L1,L2,L3,L4...
    public Layer baseLayer;//base.default
    public Layer offroadLayer;
    public Layer heightPathLayer;
    public Layer materialPathLayer;
    /// <summary>老工具编辑的 pre_terrain5_heightmap 数据。</summary>
    public MetaTerrain m_preTerrain;
    /// <summary>导入的 terrain5_combined_alpha 标准（预览基准）。</summary>
    public Texture2D referenceCombinedAlpha;
    /// <summary>老工具编辑的 pre_terrain5_combined_alpha。</summary>
    public Texture2D preCombinedAlpha;
    public const string PreHeightmapFileName = "pre_terrain5_heightmap.png";
    public const string PreCombinedAlphaFileName = "pre_terrain5_combined_alpha.png";
    public const string FinalHeightmapFileName = "terrain5_heightmap.png";
    public const string FinalCombinedAlphaFileName = "terrain5_combined_alpha.png";
    /// <summary>越野路径取高、VpMetaToucher 等使用的逻辑层号（原 Offroad.heightSampleLayer）。</summary>
    public int offroadHeightSampleLayer = 4;
    public MetaMapConfig m_metaMapConfig;
    public string m_settings;

    public Texture2D CombinedAlpha;

    public List<BuildingType> buildingTypes;
    public List<WallType> wallTypes;
    public List<MeshTemplate> meshTemplates;

    public List<DecalTemplate> DecalTemplates;
    public List<PostTemplate> PostTemplates;
    public List<string> allowedExtensions = new List<string> { "default"};//import later

    public int terrainLayerCount = 4;
    public List<string> terrainAlphaFileName = new List<string> { "null", "terrain5_alpha_sand.png", "terrain5_alpha_grass.png", "terrain5_alpha_asphalt.png", "terrain5_alpha_road.png" };

    // Start is called before the first frame update
    void Start()
    {
        instance = this;
        m_metaTerrain = new MetaTerrain();
        defaultLayer = new Layer();
        baseLayer = new Layer();
        offroadLayer = new Layer();
        heightPathLayer = new Layer();
        materialPathLayer = new Layer();
        m_preTerrain = new MetaTerrain();

        m_metaMapConfig = new MetaMapConfig();

        buildingTypes = new List<BuildingType>();
        wallTypes = new List<WallType>();

        CombinedAlpha = new Texture2D(2, 2);
        meshTemplates = new List<MeshTemplate>();
        DecalTemplates = new List<DecalTemplate>();
        PostTemplates = new List<PostTemplate>();
        //setBts();
        //setWts();

        Debug.Log("MetaMapInit");
    }

    public string getNewItemId(string startWith)
    {
        int n = defaultLayer.mapItems.Count + baseLayer.mapItems.Count;
        if (offroadLayer != null && offroadLayer.mapItems != null)
            n += offroadLayer.mapItems.Count;
        if (heightPathLayer != null && heightPathLayer.mapItems != null)
            n += heightPathLayer.mapItems.Count;
        if (materialPathLayer != null && materialPathLayer.mapItems != null)
            n += materialPathLayer.mapItems.Count;
        return startWith + n.ToString() + "rwrme";
    }

    public IEnumerable<MapItem> EnumerateAllMapItems()
    {
        if (defaultLayer?.mapItems != null)
            foreach (MapItem mi in defaultLayer.mapItems)
                if (mi != null) yield return mi;
        if (baseLayer?.mapItems != null)
            foreach (MapItem mi in baseLayer.mapItems)
                if (mi != null) yield return mi;
        if (offroadLayer?.mapItems != null)
            foreach (MapItem mi in offroadLayer.mapItems)
                if (mi != null) yield return mi;
        if (heightPathLayer?.mapItems != null)
            foreach (MapItem mi in heightPathLayer.mapItems)
                if (mi != null) yield return mi;
        if (materialPathLayer?.mapItems != null)
            foreach (MapItem mi in materialPathLayer.mapItems)
                if (mi != null) yield return mi;
    }

    /// <summary>按 id 查找：完全匹配 &gt; 唯一前缀 &gt; 唯一子串。</summary>
    public MapItem FindMapItemById(string query)
    {
        query = query?.Trim();
        if (string.IsNullOrEmpty(query)) return null;

        MapItem uniquePrefix = null;
        int prefixCount = 0;
        MapItem uniqueContains = null;
        int containsCount = 0;

        foreach (MapItem mi in EnumerateAllMapItems())
        {
            if (mi == null || string.IsNullOrEmpty(mi.id)) continue;

            if (string.Equals(mi.id, query, StringComparison.OrdinalIgnoreCase))
                return mi;

            if (mi.id.StartsWith(query, StringComparison.OrdinalIgnoreCase))
            {
                prefixCount++;
                uniquePrefix = mi;
            }

            if (mi.id.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                containsCount++;
                uniqueContains = mi;
            }
        }

        if (prefixCount == 1) return uniquePrefix;
        if (containsCount == 1) return uniqueContains;
        return null;
    }

    public int CountMapItemIdMatches(string query)
    {
        query = query?.Trim();
        if (string.IsNullOrEmpty(query)) return 0;

        int count = 0;
        foreach (MapItem mi in EnumerateAllMapItems())
        {
            if (mi == null || string.IsNullOrEmpty(mi.id)) continue;
            if (mi.id.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
                count++;
        }
        return count;
    }

    public void InitPreTerrainFromReference()
    {
        if (m_metaTerrain?.data == null || m_metaTerrain.data.Width <= 0)
            return;
        m_preTerrain.maxHeight = m_metaTerrain.maxHeight;
        m_preTerrain.fileName = PreHeightmapFileName;
        m_preTerrain.setData(TerrainPathRasterizer.Clone(m_metaTerrain.data));
    }

    public void EnsurePreTerrain()
    {
        if (m_preTerrain?.data == null || m_preTerrain.data.Width <= 0)
            InitPreTerrainFromReference();
    }

    public void EnsurePreCombinedAlpha()
    {
        if (preCombinedAlpha != null) return;
        if (referenceCombinedAlpha != null)
            preCombinedAlpha = TerrainPathRasterizer.CloneTexture(referenceCombinedAlpha);
    }

    public IEnumerable<HeightPath> GetHeightPaths()
    {
        if (heightPathLayer?.mapItems == null) yield break;
        foreach (MapItem mi in heightPathLayer.mapItems)
            if (mi is HeightPath hp) yield return hp;
    }

    public IEnumerable<MaterialPath> GetMaterialPaths()
    {
        if (materialPathLayer?.mapItems == null) yield break;
        foreach (MapItem mi in materialPathLayer.mapItems)
            if (mi is MaterialPath mp) yield return mp;
    }

    /// <summary>预览/导出成品：pre_ 位图 + 路径覆盖。</summary>
    public GrayScaleImage BakeFinalHeightmap()
    {
        EnsurePreTerrain();
        return TerrainPathRasterizer.BakeHeightmap(m_preTerrain.data, GetHeightPaths());
    }

    public Texture2D BakeFinalCombinedAlpha()
    {
        EnsurePreCombinedAlpha();
        return TerrainPathRasterizer.BakeAlpha(preCombinedAlpha, GetMaterialPaths());
    }

    /// <summary>预览基准高度：terrain5 标准 + 路径（不读 pre_ 刷痕）。</summary>
    public GrayScaleImage BakePreviewHeightmap()
    {
        if (m_metaTerrain?.data == null || m_metaTerrain.data.Width <= 0)
            return BakeFinalHeightmap();
        return TerrainPathRasterizer.BakeHeightmap(m_metaTerrain.data, GetHeightPaths());
    }

    /// <summary>预览基准材质：terrain5 标准 + 路径。</summary>
    public Texture2D BakePreviewCombinedAlpha()
    {
        if (referenceCombinedAlpha == null)
            return BakeFinalCombinedAlpha();
        return TerrainPathRasterizer.BakeAlpha(referenceCombinedAlpha, GetMaterialPaths());
    }

    // manual add is abborded now
    private void setBts()
    {
        //for tests until we get way import this
        buildingTypes.Add(new BuildingType("BuildingWhite1", Color.gray, Color.white));
        buildingTypes.Add(new BuildingType("BuildingRoofStory1", Color.gray, Color.white));
        buildingTypes.Add(new BuildingType("BuildingWhite1Empty1stFloor", Color.gray, Color.white));
        buildingTypes.Add(new BuildingType("BuildingWhite1Busy", Color.gray, Color.white));
        buildingTypes.Add(new BuildingType("BuildingWhite1Brick", Color.gray, Color.white));
        buildingTypes.Add(new BuildingType("BuildingShop1", Color.gray, Color.white));
        buildingTypes.Add(new BuildingType("BuildingShop2", Color.gray, Color.white));
        buildingTypes.Add(new BuildingType("BuildingShop1End", Color.gray, Color.white));
        buildingTypes.Add(new BuildingType("BuildingWhite2", new Color(0.29f, 0.314f, 0.267f, 1f), Color.white));
        buildingTypes.Add(new BuildingType("BuildingWhite2Busy", new Color(0.29f, 0.314f, 0.267f, 1f), Color.white));
        buildingTypes.Add(new BuildingType("BuildingWhite2RoofStory1", Color.white, Color.gray));
        buildingTypes.Add(new BuildingType("BuildingWhite3", new Color(0.29f, 0.314f, 0.267f, 1f), Color.white));
        buildingTypes.Add(new BuildingType("BuildingWhite3RoofStory1", new Color(0.29f, 0.314f, 0.267f, 1f), Color.white));
        buildingTypes.Add(new BuildingType("BuildingWhite3Busy", new Color(0.29f,0.314f,0.267f,1f), Color.white));
        buildingTypes.Add(new BuildingType("BuildingWhite4", Color.green, Color.white));
        buildingTypes.Add(new BuildingType("BuildingHouse", Color.gray, Color.white));
        buildingTypes.Add(new BuildingType("BuildingVilla", Color.red, Color.white));

    }

    private void setWts()
    {
        wallTypes.Add(new WallType("SandbagWall1", Color.gray, 0.6f, 1.325f));
        wallTypes.Add(new WallType("TrenchWall1", Color.gray, 0.5f, 1.35f));
        wallTypes.Add(new WallType("StoneWall1", Color.black, 0.6f, 1.3f));
        wallTypes.Add(new WallType("StoneWallCastle1", Color.black, 0.6f, 1.3f));
        wallTypes.Add(new WallType("PoolWall", Color.blue, 0.6f, 1.1f));
        wallTypes.Add(new WallType("BrickWall1", Color.gray, 0.6f, 2.5f));
        wallTypes.Add(new WallType("GardenWall1", Color.green, 0.7f, 1.2f));
        wallTypes.Add(new WallType("DummyWall1", Color.gray, 0.4f, 0.0f));
        wallTypes.Add(new WallType("CliffWall1", Color.gray, 0.4f, 1.3f));
        wallTypes.Add(new WallType("CliffWall2", Color.green, 0.4f, 1.3f));
        wallTypes.Add(new WallType("FarmFence1", Color.gray, -1.0f, 1.2f));
        wallTypes.Add(new WallType("FarmFence2", Color.gray, -1.0f, 1.2f));
        wallTypes.Add(new WallType("SecurityFence1", Color.gray, -1.0f, 2.5f));
        wallTypes.Add(new WallType("PlatformFence1", Color.black, -1.0f, 1.2f));
        wallTypes.Add(new WallType("PlatformFence2", Color.gray, -1.0f, 1.2f));
        //wallTypes.Add(new WallType("FarmFence2", Color.gray, -1.0f, 1.2f)); // �ظ���
        wallTypes.Add(new WallType("InvisibleWall1", Color.cyan, 1.2f, 2.5f));
        wallTypes.Add(new WallType("ChurchWall1", Color.gray, 0.2f, 2.5f));
        wallTypes.Add(new WallType("RuinWall1", Color.gray, 0.8f, 3.0f));
    }

    void Update()
    {
        
    }
}


public class GrayScaleImage
{
    [SerializeField] private float[,] data;

    public int Width => data?.GetLength(1) ?? 0;
    public int Height => data?.GetLength(0) ?? 0;

    public GrayScaleImage(int width, int height)
    {
        data = new float[height, width];
    }

    public GrayScaleImage(float[,] sourceData)
    {
        if (sourceData == null) return;

        data = new float[sourceData.GetLength(0), sourceData.GetLength(1)];
        System.Array.Copy(sourceData, data, sourceData.Length);
    }

    public float this[int y, int x]
    {
        get => IsValidCoordinate(y, x) ? data[y, x] : 0f;
        set { if (IsValidCoordinate(y, x)) data[y, x] = value; }
    }

    private bool IsValidCoordinate(int y, int x)
    {
        return data != null && y >= 0 && y < Height && x >= 0 && x < Width;
    }

    public void Resize(int newWidth, int newHeight)
    {
        var newData = new float[newHeight, newWidth];

        int copyWidth = Mathf.Min(Width, newWidth);
        int copyHeight = Mathf.Min(Height, newHeight);

        for (int y = 0; y < copyHeight; y++)
            for (int x = 0; x < copyWidth; x++)
                newData[y, x] = data[y, x];

        data = newData;
    }

    public float[,] CopyDataArray()
    {
        if (data == null) return null;
        var copy = new float[Height, Width];
        System.Array.Copy(data, copy, data.Length);
        return copy;
    }

    public void CopyFromArray(float[,] src)
    {
        if (src == null) return;
        data = new float[src.GetLength(0), src.GetLength(1)];
        System.Array.Copy(src, data, src.Length);
    }

    public byte[] convToPng()
    {
        Debug.Log("convGSItoPngBytes");
        byte[] pngInfo = null;
        Texture2D texture = new Texture2D(Width, Height, GraphicsFormat.R8_UNorm, TextureCreationFlags.None);
        for(int y = 0; y < Height;y++)
        {
            for(int x = 0;x < Width;x++)
            {
                Color color = new Color(this[y,x], this[y, x], this[y, x], 1f);
                texture.SetPixel(x, y, color);
            }
        }
        pngInfo = texture.EncodeToPNG();
        return pngInfo;
    }
}

/// <summary>
/// 与 <c>map/map_config.xml</c> 根节点 <c>map_config</c> 对应：根属性 + 子节点 <c>file</c> 路径。
/// </summary>
[Serializable]
public class MetaMapConfig
{
    /// <summary>map_config/@min_factions</summary>
    public int minFactions;

    /// <summary>map_config/@max_factions</summary>
    public int maxFactions;

    /// <summary>map_config/@add_neutral_last（XML 中通常为 0/1）</summary>
    public int addNeutralLast;

    /// <summary>各 faction/@file</summary>
    public List<string> factionFiles = new List<string>();

    /// <summary>weapon/@file</summary>
    public string weaponFile;

    /// <summary>projectile/@file</summary>
    public string projectileFile;

    /// <summary>call/@file</summary>
    public string callFile;

    public List<string> includeLayers = new List<string>();

    /// <summary>carry_item/@file</summary>
    public string carryItemFile;

    /// <summary>vehicle/@file</summary>
    public string vehicleFile;
}
public class MetaTerrain
{
    public GrayScaleImage data;
    public int resolutionX;
    public int resolutionY;
    public float waterHeight=2.0f;
    public float maxHeight=25.0f;
    public string fileName = "terrain5_heightmap.png";
    public MetaTerrain()
    {
        data = new GrayScaleImage(0, 0);
    }
    public void setData(GrayScaleImage igsi)
    {
        resolutionY = igsi.Height;
        resolutionX = igsi.Width;
        data = igsi;
    }
}
public class Layer
{
    public List<MapItem> mapItems;
    public Layer()
    {
        mapItems = new List<MapItem>();
    }

    public void sortByIndex()
    {
        mapItems = mapItems
            .OrderBy(item => item.layerIndex)
            .ThenBy(item => item is Platform ? 0 : 1)
            .ThenBy(item => item is Building ? 0 : 1)
            .ToList();
    }
}
public class MapItem:MonoBehaviour
{
    public string id;
    public int layerIndex;
    public string material;
    //can pick by selector

    public virtual float Rank => 0;
    //rank is used to sort the items with same layerIndex
    public MapItem()
    {
        id = string.Empty;
        layerIndex = 0;
    }

    public virtual void scatterThis()
    {
        Debug.Log("MetaMap:WrongScatte");
    }

    public virtual string getInfoText()
    {
        return this.GetType().Name+"\n" + "empty info";
    }

    public virtual void rotate(float angle)
    {

    }
    public virtual void grab(Vector2 offset)
    {

    }
    public virtual void scale(float scaler)
    {

    }

    /// <summary>用于"以鼠标点为新锚点"翻译；MeRect 用 position，路径类用顶点均值。</summary>
    public virtual Vector2 GetAnchor() { return Vector2.zero; }
    /// <summary>getNewItemId 的前缀；与现有 import / 工具创建保持一致。</summary>
    public virtual string IdPrefix { get { return "item"; } }
    /// <summary>实例化对应 prefab 并深拷贝本对象数据字段；不分配 id / layerIndex / 入层。null 表示该子类不支持复制。</summary>
    public virtual MapItem Duplicate() { return null; }

}
public class MeRect :MapItem//this class won,t use directly
{
    public Vector2 position;
    public float rotation;//angle
    public Vector2 size;//width x and height y

    public Vector3 offset;

    // 性能：每个 MeRect 实例在 prefab 里都挂了一个 Canvas + 3 个 TMP_InputField，常驻渲染会拖垮帧率。
    // 默认隐藏，仅在该实例被选中（miSelected == this）时由 ToolController.Update 切回可见。
    [System.NonSerialized] GameObject _offsetUiRoot;
    [System.NonSerialized] bool _offsetUiCached;

    void Awake()
    {
        CacheOffsetUi();
        if (_offsetUiRoot != null) _offsetUiRoot.SetActive(false);
    }

    void CacheOffsetUi()
    {
        if (_offsetUiCached) return;
        _offsetUiCached = true;
        Transform t = transform.Find("Canvas");
        _offsetUiRoot = (t != null) ? t.gameObject : null;
        if (t != null) MapItemParamUiLayout.Ensure(t);
    }

    public void SetOffsetUiActive(bool on)
    {
        CacheOffsetUi();
        if (_offsetUiRoot != null) _offsetUiRoot.SetActive(on);
        if (on) updateOffsetShow();
    }

    public void appOffset()
    {
        GameObject go = this.gameObject;
        if (go != null)
        {
            //offset :x 1 y 1 z -1
            go.transform.localPosition = go.transform.localPosition + new Vector3(offset.x, offset.y, -offset.z);
        }
    }

    public void updateOffsetShow()
    {
        GameObject go = this.gameObject;
        if (go == null) return;

        // 与 ToolController 一致：偏移 UI 挂在当前物体子节点 Canvas 上；不要用 GameObject.Find，否则会命中场景里别的 Canvas 且没有 x/y/z 子节点。
        Transform canvasTf = go.transform.Find("Canvas");
        if (canvasTf == null) return;

        Canvas canvas = canvasTf.GetComponent<Canvas>();
        if (canvas == null) return;

        MapItemParamUiLayout.Ensure(canvasTf);
        TMP_InputField xText = MapItemParamUiLayout.Find(canvasTf, "x")?.GetComponent<TMP_InputField>();
        TMP_InputField yText = MapItemParamUiLayout.Find(canvasTf, "y")?.GetComponent<TMP_InputField>();
        TMP_InputField zText = MapItemParamUiLayout.Find(canvasTf, "z")?.GetComponent<TMP_InputField>();
        // SetTextWithoutNotify 切断 setter → onValueChanged → setOffset* → scatterThis 的回环
        if (xText != null) xText.SetTextWithoutNotify(offset.x.ToString(System.Globalization.CultureInfo.InvariantCulture));
        if (yText != null) yText.SetTextWithoutNotify(offset.y.ToString(System.Globalization.CultureInfo.InvariantCulture));
        if (zText != null) zText.SetTextWithoutNotify(offset.z.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    public void setOffsetx(string value)
    {
        if (float.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float f))
        {
            offset.x = f;
            scatterThis();
        }
    }

    public void setOffsety(string value)
    {
        if (float.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float f))
        {
            offset.y = f;
            scatterThis();
        }
    }

    public void setOffsetz(string value)
    {
        if (float.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float f))
        {
            offset.z = f;
            scatterThis();
        }
    }

    public MeRect(Vector2 pos,float r,Vector2 s,string key,int lI)
    {
        position = pos;
        rotation = r;
        size = s;
        id = key;
        layerIndex = lI;
        offset  = new Vector3(0,0,0);
    }
    public override void scale(float scaler)
    {
        size = size * (1 + 0.03f * scaler);
    }
    public override void grab(Vector2 vector2)
    {
        position = position + vector2;
    }
    public override void rotate(float scaler)
    {
        rotation = rotation + scaler * -2;
    }
    public override Vector2 GetAnchor() { return position; }

    protected void CopyMeRectFieldsTo(MeRect dst)
    {
        dst.position = position;
        dst.rotation = rotation;
        dst.size = size;
        dst.layerIndex = layerIndex;
        dst.material = material;
    }
}
public class PathPair:MapItem
{
    public List<Vector2> positinLineL;
    public List<Vector2> positinLineR;

    public override Vector2 GetAnchor()
    {
        int total = (positinLineL != null ? positinLineL.Count : 0)
                  + (positinLineR != null ? positinLineR.Count : 0);
        if (total == 0) return Vector2.zero;
        Vector2 sum = Vector2.zero;
        if (positinLineL != null) foreach (var p in positinLineL) sum += p;
        if (positinLineR != null) foreach (var p in positinLineR) sum += p;
        return sum / total;
    }

    protected void CopyPathPairFieldsTo(PathPair dst)
    {
        dst.layerIndex = layerIndex;
        dst.material = material;
        dst.positinLineL = positinLineL != null ? new List<Vector2>(positinLineL) : new List<Vector2>();
        dst.positinLineR = positinLineR != null ? new List<Vector2>(positinLineR) : new List<Vector2>();
    }
}
public class MePath:MapItem
{
    public List<Vector2> positionLine;
    public List<bool> curve;
    public List<Vector2> controlPoints;

    public override Vector2 GetAnchor()
    {
        if (positionLine == null || positionLine.Count == 0) return Vector2.zero;
        Vector2 sum = Vector2.zero;
        foreach (var p in positionLine) sum += p;
        return sum / positionLine.Count;
    }

    public override void grab(Vector2 offset)
    {
        if (positionLine != null)
            for (int i = 0; i < positionLine.Count; i++)
                positionLine[i] += offset;
        if (controlPoints != null)
            for (int i = 0; i < controlPoints.Count; i++)
                controlPoints[i] += offset;
    }

    protected void CopyMePathFieldsTo(MePath dst)
    {
        dst.layerIndex = layerIndex;
        dst.material = material;
        dst.positionLine = positionLine != null ? new List<Vector2>(positionLine) : new List<Vector2>();
        dst.curve = curve != null ? new List<bool>(curve) : new List<bool>();
        dst.controlPoints = controlPoints != null ? new List<Vector2>(controlPoints) : new List<Vector2>();
    }
}

/// <summary>与 Offroad 相同的三次贝塞尔约定：curve[i] 表示段 (i-1)→i 为曲线。</summary>
public static class MePathCurve
{
    public const int DefaultSamplesPerCubicSegment = 24;

    public static Vector2 CubicBezier2(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t)
    {
        Vector2 a = Vector2.Lerp(p0, p1, t);
        Vector2 b = Vector2.Lerp(p1, p2, t);
        Vector2 c = Vector2.Lerp(p2, p3, t);
        Vector2 d = Vector2.Lerp(a, b, t);
        Vector2 e = Vector2.Lerp(b, c, t);
        return Vector2.Lerp(d, e, t);
    }

    public static void ApplyAutoBezierCurveAnnotations(List<Vector2> anchors, List<bool> curve, List<Vector2> controlPoints)
    {
        curve.Clear();
        controlPoints.Clear();
        int n = anchors != null ? anchors.Count : 0;
        if (n < 2)
        {
            if (n == 1)
                curve.Add(false);
            return;
        }

        curve.Add(false);
        for (int i = 1; i < n; i++)
            curve.Add(true);

        for (int i = 1; i < n; i++)
        {
            Vector2 p0 = i >= 2 ? anchors[i - 2] : anchors[0] + (anchors[0] - anchors[1]);
            Vector2 p1 = anchors[i - 1];
            Vector2 p2 = anchors[i];
            Vector2 p3 = i + 1 < n ? anchors[i + 1] : anchors[n - 1] + (anchors[n - 1] - anchors[n - 2]);

            controlPoints.Add(p1 + (p2 - p0) * (1f / 6f));
            controlPoints.Add(p2 - (p3 - p1) * (1f / 6f));
        }
    }

    public static void CollectSvgCurvePoints(MePath path, int samplesPerSegment, List<Vector2> output)
    {
        output.Clear();
        if (path?.positionLine == null || path.positionLine.Count < 2) return;

        List<Vector2> anchors = path.positionLine;
        bool hasCurve = path.curve != null && path.curve.Count == anchors.Count;
        bool hasCp = path.controlPoints != null && path.controlPoints.Count >= 2;
        int cpIndex = 0;

        void Append(Vector2 p)
        {
            if (output.Count > 0 && (output[output.Count - 1] - p).sqrMagnitude < 1e-8f)
                return;
            output.Add(p);
        }

        Append(anchors[0]);
        for (int i = 1; i < anchors.Count; i++)
        {
            bool cubic = hasCurve && path.curve[i] && hasCp && cpIndex + 1 < path.controlPoints.Count;
            if (cubic)
            {
                Vector2 p0 = anchors[i - 1];
                Vector2 c1 = path.controlPoints[cpIndex++];
                Vector2 c2 = path.controlPoints[cpIndex++];
                Vector2 p3 = anchors[i];
                int steps = Mathf.Max(2, samplesPerSegment);
                for (int s = 1; s <= steps; s++)
                    Append(CubicBezier2(p0, c1, c2, p3, s / (float)steps));
            }
            else
                Append(anchors[i]);
        }
    }

    public static void WalkCpLayout(MePath path, int anchorCount, List<int> cpIdxList, List<(int iEnd, int cp0, int cp1)> tangSpecs)
    {
        cpIdxList.Clear();
        tangSpecs.Clear();
        if (anchorCount < 2 || path == null) return;

        bool hasCurve = path.curve != null && path.curve.Count == anchorCount;
        bool hasCp = path.controlPoints != null && path.controlPoints.Count >= 2;
        int cpIndex = 0;
        for (int i = 1; i < anchorCount; i++)
        {
            bool cubic = hasCurve && path.curve[i] && hasCp && cpIndex + 1 < path.controlPoints.Count;
            if (cubic)
            {
                cpIdxList.Add(cpIndex);
                cpIdxList.Add(cpIndex + 1);
                tangSpecs.Add((i, cpIndex, cpIndex + 1));
                cpIndex += 2;
            }
        }
    }
}

public class mapItemType
{
    public string name="";
}
public class BuildingType:mapItemType
{
    public const string EdgeDarkenShaderName = "RWRME/Color Edge Darken";

    public Material materialTop;
    public Material materialSide;

    static Material CreateEdgeDarkenMaterial(Color c)
    {
        Shader shader = Shader.Find(EdgeDarkenShaderName);
        if (shader == null)
        {
            Debug.LogWarning($"BuildingType: 未找到 {EdgeDarkenShaderName}，回退 Standard");
            shader = Shader.Find("Standard");
        }
        var mat = new Material(shader);
        mat.color = c;
        if (shader.name == EdgeDarkenShaderName)
        {
            mat.SetFloat("_EdgeWidth", 0.04f);
            mat.SetFloat("_EdgeDarken", 0.45f);
            mat.SetFloat("_Alpha", 1f);
        }
        return mat;
    }

    public BuildingType(string n,Color c,Color c1)
    {
        materialTop = CreateEdgeDarkenMaterial(c);
        materialSide = CreateEdgeDarkenMaterial(c1);
        name = n;
    }
}
public class WallType:mapItemType
{
    public Material material;
    public float depth;
    public float height;

    public WallType(string name,Color c, float depth, float height)
    {
        this.name = name;
        material = new Material(Shader.Find("Standard"));
        material.color = c;
        this.depth = depth;
        this.height = height;
    }
}

/// <summary>
/// 四元整型（RWR texture0_atlas_cell）：x=块列, y=块行, z=图集列数, w=图集行数（均 0 起）。
/// 勿用 <see cref="Vector4"/> 存索引，避免 Inspector 与 float 运算产生小数。
/// </summary>
[System.Serializable]
public struct IntVector4
{
    public int x, y, z, w;

    public IntVector4(int x, int y, int z, int w)
    {
        this.x = x;
        this.y = y;
        this.z = z;
        this.w = w;
    }

    public Vector4 ToVector4() => new Vector4(x, y, z, w);

    public static bool TryParse(string s, out IntVector4 v)
    {
        v = default;
        if (string.IsNullOrWhiteSpace(s)) return false;
        var parts = s.Split((char[])null, System.StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 4) return false;
        if (!int.TryParse(parts[0], out int a)) return false;
        if (!int.TryParse(parts[1], out int b)) return false;
        if (!int.TryParse(parts[2], out int c)) return false;
        if (!int.TryParse(parts[3], out int d)) return false;
        v = new IntVector4(a, b, c, d);
        return true;
    }

    /// <summary>当前图集块在纹理上的像素宽高（整图时 z,w 为 1）。</summary>
    public Vector2 GetAtlasCellPixelSize(Texture2D texture)
    {
        if (texture == null)
            return Vector2.zero;

        int cols = z > 0 ? z : 1;
        int rows = w > 0 ? w : 1;
        return new Vector2(texture.width / (float)cols, texture.height / (float)rows);
    }

    /// <summary>RWR 图集单元在 0–1 UV 中的矩形（Unity 底向上 V）。</summary>
    public bool TryGetRwrAtlasUvRect(out Rect uvRect)
    {
        uvRect = default;
        int col = x, row = y, cols = z, rows = w;
        if (cols <= 0 || rows <= 0 || col < 0 || row < 0 || col >= cols || row >= rows)
            return false;

        float du = 1f / cols;
        float dv = 1f / rows;
        float u0 = col * du;
        float u1 = (col + 1) * du;
        float v1 = 1f - row * dv;
        float v0 = 1f - (row + 1) * dv;
        uvRect = Rect.MinMaxRect(u0, v0, u1, v1);
        return true;
    }

    /// <summary>
    /// 为 Cube 贴花设置图集 UV：仅处理朝 +Y 的顶面，按顶点 XZ 映射到图集块（侧面保持原 UV）。
    /// </summary>
    public void ApplyAtlasUvToMesh(Mesh mesh)
    {
        if (mesh == null || !TryGetRwrAtlasUvRect(out Rect atlas))
            return;

        Vector3[] verts = mesh.vertices;
        Vector3[] normals = mesh.normals;
        Vector2[] uvs = mesh.uv;
        if (verts == null || uvs == null || verts.Length != uvs.Length)
            return;

        Bounds b = mesh.bounds;
        float topY = b.max.y;
        const float planeEps = 1e-4f;

        for (int i = 0; i < verts.Length; i++)
        {
            bool onTopPlane = verts[i].y >= topY - planeEps;
            bool facesUp = normals == null || normals.Length != verts.Length || normals[i].y > 0.5f;
            if (!onTopPlane || !facesUp)
                continue;

            float u01 = Mathf.InverseLerp(b.min.x, b.max.x, verts[i].x);
            float v01 = Mathf.InverseLerp(b.min.z, b.max.z, verts[i].z);
            uvs[i] = new Vector2(
                Mathf.Lerp(atlas.xMin, atlas.xMax, u01),
                Mathf.Lerp(atlas.yMin, atlas.yMax, v01));
        }

        mesh.uv = uvs;
    }
}

public class DecalTemplate
{
    public string name =  "empty";

    public Color color=Color.white;

    public float length = 1.0f;

    public string textureName = "40mm.png";

    /// <summary>图集切块（块列 x, 块行 y, 列数 z, 行数 w），对应 texture0_atlas_cell。</summary>
    public IntVector4 textureCut = new IntVector4(0, 0, 1, 1);


    public DecalTemplate(string m_name)
    {
        name = m_name;
        color = MathOfRwrme.StringToColor(name);
    }
}

public class PostTemplate
{
    public string name = "empty";

    /// <summary>指向 <see cref="MeshTemplate.name"/>（SVG 字段 mesh_template），非 OGRE 文件名。</summary>
    public string meshName = "";

    public Color color = Color.white;

    public PostTemplate(string m_name, string meshTemplateRef)
    {
        name = m_name;
        meshName = meshTemplateRef;
        color = MathOfRwrme.StringToColor(name);
    }

    public MeshTemplate ResolveMeshTemplate()
    {
        if (MetaMap.instance == null || string.IsNullOrEmpty(meshName))
            return null;
        return MetaMap.instance.meshTemplates.FirstOrDefault(m => m.name == meshName);
    }
}
public class MeshTemplate
{
    public string name="empty";
    public Color color=Color.white;
    public Vector3 extend = Vector3.one;
    public Vector2 size= Vector2.one;

    public string meshName = "";
    public string textureName = "";
    public Vector4 textureAC = new Vector4(0,0,0,0);
}
public class PlatformSerfaceType: mapItemType
{
    public Material material;
    public PlatformSerfaceType(string name, Color c)
    {
        this.name = name;
        material = new Material(Shader.Find("Standard"));
        material.color = c;
    }
}

public enum HeightPathMode
{
    Raise = 0,
    Lower = 1,
    /// <summary>定量：将路径区域设为绝对归一化高度 [0,1]。</summary>
    Set = 2,
    /// <summary>偏移量：相对基准 heightmap 每像素叠加 heightDelta（可正可负）。</summary>
    Offset = 3,
}

static class TerrainPathPickUtil
{
    const string PickChildPrefix = "_path_pick_";
    const float PickWidthScale = 4f;
    const float PickColliderHeight = 10f;

    static int s_pinAbleLayer = -2;

    public static int PinAbleLayer
    {
        get
        {
            if (s_pinAbleLayer == -2)
            {
                s_pinAbleLayer = LayerMask.NameToLayer("PinAble");
                if (s_pinAbleLayer < 0) s_pinAbleLayer = 6;
            }
            return s_pinAbleLayer;
        }
    }

    public static void BuildPickColliders(Transform lineParent, List<Vector3> worldPts, float pathWidth)
    {
        if (worldPts == null || worldPts.Count < 2) return;

        int pin = PinAbleLayer;
        float halfW = Mathf.Max(pathWidth * PickWidthScale * 0.5f, 0.35f);
        for (int i = 0; i < worldPts.Count - 1; i++)
        {
            Vector3 a = worldPts[i];
            Vector3 b = worldPts[i + 1];
            Vector3 delta = b - a;
            Vector3 flat = new Vector3(delta.x, 0f, delta.z);
            float len = flat.magnitude;
            if (len < 1e-4f)
                continue;

            GameObject pick = new GameObject(PickChildPrefix + i);
            pick.transform.SetParent(lineParent, true);
            pick.layer = pin;

            pick.transform.position = (a + b) * 0.5f;
            flat /= len;
            pick.transform.rotation = Quaternion.LookRotation(flat, Vector3.up);

            BoxCollider box = pick.AddComponent<BoxCollider>();
            box.isTrigger = false;
            box.center = Vector3.zero;
            box.size = new Vector3(halfW * 2f, PickColliderHeight, len);
        }
    }
}

public class HeightPath : MePath
{
    public float width = 20f;
    /// <summary>Raise/Lower/Offset 为 delta；Set 为绝对高度。</summary>
    public float heightDelta = 0.03f;
    public HeightPathMode mode = HeightPathMode.Raise;
    public int samplesPerCubicSegment = MePathCurve.DefaultSamplesPerCubicSegment;

    public override float Rank => 0.04f;

    const string LineChildName = "_height_path_line";

    public override string IdPrefix => "height_path";

    public string GetHeightParamLabel()
    {
        if (mode == HeightPathMode.Set) return "height";
        if (mode == HeightPathMode.Offset) return "height_offset";
        return "height_delta";
    }

    public override string getInfoText()
    {
        return "HeightPath\nid = " + id + "\nwidth = " + width + "\n"
            + GetHeightParamLabel() + " = " + heightDelta + "\nmode = " + mode;
    }

    public override MapItem Duplicate()
    {
        HeightPath c = new GameObject("HeightPath").AddComponent<HeightPath>();
        CopyMePathFieldsTo(c);
        c.width = width;
        c.heightDelta = heightDelta;
        c.mode = mode;
        return c;
    }

    void OnEnable()
    {
        scatterThis();
    }

    List<Vector3> BuildWorldCurvePoints()
    {
        var svgPts = new List<Vector2>();
        MePathCurve.CollectSvgCurvePoints(this, samplesPerCubicSegment, svgPts);
        var list = new List<Vector3>(svgPts.Count);
        foreach (Vector2 svgPt in svgPts)
        {
            Vector2 u3d = MathOfRwrme.SvgPosToU3dPos(svgPt);
            float y = 0f;
            if (Terrain.activeTerrain != null)
                y = Terrain.activeTerrain.SampleHeight(new Vector3(u3d.x, 0f, u3d.y)) + 2f;
            Vector3 w = new Vector3(u3d.x, y, u3d.y);
            if (list.Count > 0 && (list[list.Count - 1] - w).sqrMagnitude < 1e-8f)
                continue;
            list.Add(w);
        }
        return list;
    }

    void EnsureCurveData()
    {
        if (positionLine == null || positionLine.Count < 2) return;
        if (curve != null && curve.Count == positionLine.Count
            && controlPoints != null && controlPoints.Count >= 2)
            return;
        curve = new List<bool>();
        controlPoints = new List<Vector2>();
        MePathCurve.ApplyAutoBezierCurveAnnotations(positionLine, curve, controlPoints);
    }

    public override void scatterThis()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
            Destroy(transform.GetChild(i).gameObject);

        if (positionLine == null || positionLine.Count < 2) return;

        EnsureCurveData();

        List<Vector3> worldPts = BuildWorldCurvePoints();
        if (worldPts.Count < 2) return;

        GameObject lineGo = new GameObject(LineChildName);
        lineGo.transform.SetParent(transform, false);
        lineGo.layer = TerrainPathPickUtil.PinAbleLayer;

        LineRenderer lr = lineGo.AddComponent<LineRenderer>();
        lr.useWorldSpace = true;
        lr.loop = false;
        lr.startWidth = width * 0.05f;
        lr.endWidth = width * 0.05f;
        if (mode == HeightPathMode.Set)
        {
            lr.startColor = new Color(1f, 0.35f, 0.2f, 0.9f);
            lr.endColor = new Color(0.85f, 0.2f, 0.1f, 0.9f);
        }
        else if (mode == HeightPathMode.Offset)
        {
            lr.startColor = new Color(0.95f, 0.85f, 0.15f, 0.9f);
            lr.endColor = new Color(0.75f, 0.6f, 0.05f, 0.9f);
        }
        else
        {
            lr.startColor = new Color(0.2f, 0.85f, 1f, 0.9f);
            lr.endColor = new Color(0.1f, 0.5f, 1f, 0.9f);
        }
        lr.positionCount = worldPts.Count;
        for (int i = 0; i < worldPts.Count; i++)
            lr.SetPosition(i, worldPts[i]);

        Shader shader = Shader.Find("Hidden/Internal-Colored") ?? Shader.Find("Sprites/Default");
        lr.material = new Material(shader);
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;

        TerrainPathPickUtil.BuildPickColliders(lineGo.transform, worldPts, width);
    }
}

public class MaterialPath : MePath
{
    public float width = 20f;
    /// <summary>1=sand, 2=grass, 3=asphalt, 4=road</summary>
    public int materialIndex = 2;
    public int samplesPerCubicSegment = MePathCurve.DefaultSamplesPerCubicSegment;

    public override float Rank => 0.04f;

    const string LineChildName = "_material_path_line";

    public override string IdPrefix => "material_path";

    static readonly Color[] MaterialColors =
    {
        Color.black,
        new Color(1f, 0.6f, 0.2f),
        new Color(0.2f, 0.85f, 0.2f),
        new Color(0.5f, 0.5f, 0.55f),
        new Color(0.35f, 0.25f, 0.15f),
    };

    public override string getInfoText()
    {
        return "MaterialPath\nid = " + id + "\nwidth = " + width + "\nmaterial = " + materialIndex;
    }

    public override MapItem Duplicate()
    {
        MaterialPath c = new GameObject("MaterialPath").AddComponent<MaterialPath>();
        CopyMePathFieldsTo(c);
        c.width = width;
        c.materialIndex = materialIndex;
        return c;
    }

    void OnEnable()
    {
        scatterThis();
    }

    List<Vector3> BuildWorldCurvePoints()
    {
        var svgPts = new List<Vector2>();
        MePathCurve.CollectSvgCurvePoints(this, samplesPerCubicSegment, svgPts);
        var list = new List<Vector3>(svgPts.Count);
        foreach (Vector2 svgPt in svgPts)
        {
            Vector2 u3d = MathOfRwrme.SvgPosToU3dPos(svgPt);
            float y = 0f;
            if (Terrain.activeTerrain != null)
                y = Terrain.activeTerrain.SampleHeight(new Vector3(u3d.x, 0f, u3d.y)) + 1.5f;
            Vector3 w = new Vector3(u3d.x, y, u3d.y);
            if (list.Count > 0 && (list[list.Count - 1] - w).sqrMagnitude < 1e-8f)
                continue;
            list.Add(w);
        }
        return list;
    }

    void EnsureCurveData()
    {
        if (positionLine == null || positionLine.Count < 2) return;
        if (curve != null && curve.Count == positionLine.Count
            && controlPoints != null && controlPoints.Count >= 2)
            return;
        curve = new List<bool>();
        controlPoints = new List<Vector2>();
        MePathCurve.ApplyAutoBezierCurveAnnotations(positionLine, curve, controlPoints);
    }

    public override void scatterThis()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
            Destroy(transform.GetChild(i).gameObject);

        if (positionLine == null || positionLine.Count < 2) return;

        EnsureCurveData();

        List<Vector3> worldPts = BuildWorldCurvePoints();
        if (worldPts.Count < 2) return;

        int idx = Mathf.Clamp(materialIndex, 0, MaterialColors.Length - 1);
        Color col = MaterialColors[idx];

        GameObject lineGo = new GameObject(LineChildName);
        lineGo.transform.SetParent(transform, false);
        lineGo.layer = TerrainPathPickUtil.PinAbleLayer;

        LineRenderer lr = lineGo.AddComponent<LineRenderer>();
        lr.useWorldSpace = true;
        lr.loop = false;
        lr.startWidth = width * 0.05f;
        lr.endWidth = width * 0.05f;
        lr.startColor = col;
        lr.endColor = col;
        lr.positionCount = worldPts.Count;
        for (int i = 0; i < worldPts.Count; i++)
            lr.SetPosition(i, worldPts[i]);

        Shader shader = Shader.Find("Hidden/Internal-Colored") ?? Shader.Find("Sprites/Default");
        lr.material = new Material(shader);
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;

        TerrainPathPickUtil.BuildPickColliders(lineGo.transform, worldPts, width);
    }
}

/// <summary>
/// 将高度/材质路径栅格化到位图。坐标与 TerrainMaterialPainter、MathOfRwrme 一致。
/// </summary>
public static class TerrainPathRasterizer
{
    public const int SvgCanvasSize = 1024;

    public static GrayScaleImage Clone(GrayScaleImage src)
    {
        if (src == null) return new GrayScaleImage(0, 0);
        var copy = new GrayScaleImage(src.Width, src.Height);
        for (int y = 0; y < src.Height; y++)
            for (int x = 0; x < src.Width; x++)
                copy[y, x] = src[y, x];
        return copy;
    }

    public static Texture2D CloneTexture(Texture2D src)
    {
        if (src == null) return null;
        var tex = new Texture2D(src.width, src.height, src.format, false);
        tex.SetPixels(src.GetPixels());
        tex.Apply();
        return tex;
    }

    public static GrayScaleImage BakeHeightmap(GrayScaleImage baseline, IEnumerable<HeightPath> paths)
    {
        if (baseline == null || baseline.Width <= 0 || baseline.Height <= 0)
            return baseline ?? new GrayScaleImage(0, 0);

        GrayScaleImage result = Clone(baseline);
        GrayScaleImage offsetReference = Clone(baseline);
        if (paths == null) return result;

        float worldX = MapImporter.instate != null ? MapImporter.instate.pageWorldX : 1536f;
        float worldZ = MapImporter.instate != null ? MapImporter.instate.pageWorldZ : 1536f;
        int res = result.Width;

        foreach (HeightPath path in paths)
        {
            if (path?.positionLine == null || path.positionLine.Count < 2) continue;
            ApplyHeightPath(result, offsetReference, path, res, worldX, worldZ);
        }

        return result;
    }

    public static Texture2D BakeAlpha(Texture2D baseline, IEnumerable<MaterialPath> paths)
    {
        if (baseline == null) return null;

        Texture2D result = CloneTexture(baseline);
        if (paths == null) return result;

        foreach (MaterialPath path in paths)
        {
            if (path?.positionLine == null || path.positionLine.Count < 2) continue;
            ApplyMaterialPath(result, path);
        }

        return result;
    }

    static List<Vector2> BuildStrokePolygonForMePath(MePath path, float halfWidthSvg, int samplesPerSegment)
    {
        var sampled = new List<Vector2>();
        MePathCurve.CollectSvgCurvePoints(path, samplesPerSegment, sampled);
        return BuildHardStrokePolygon(sampled, halfWidthSvg);
    }

    static void ApplyHeightPath(GrayScaleImage img, GrayScaleImage offsetReference, HeightPath path, int res, float worldX, float worldZ)
    {
        float halfWidthSvg = path.width * 0.5f;
        List<Vector2> strokePoly = BuildStrokePolygonForMePath(path, halfWidthSvg, path.samplesPerCubicSegment);
        if (strokePoly == null || strokePoly.Count < 3) return;

        GetPolygonBoundsSvg(strokePoly, out float minX, out float minY, out float maxX, out float maxY);
        SvgToHeightmapIndex(new Vector2(minX, minY), res, worldX, worldZ, out int x0, out int y0);
        SvgToHeightmapIndex(new Vector2(maxX, maxY), res, worldX, worldZ, out int x1, out int y1);
        int left = Mathf.Max(0, Mathf.Min(x0, x1));
        int right = Mathf.Min(res - 1, Mathf.Max(x0, x1));
        int bottom = Mathf.Max(0, Mathf.Min(y0, y1));
        int top = Mathf.Min(res - 1, Mathf.Max(y0, y1));

        for (int y = bottom; y <= top; y++)
        {
            for (int x = left; x <= right; x++)
            {
                Vector2 u3d = HeightmapIndexToU3d(x, y, res, worldX, worldZ);
                Vector2 svg = U3dToSvg(u3d);
                if (!PointInPolygon(svg, strokePoly)) continue;

                float current = img[y, x];
                float next = current;
                switch (path.mode)
                {
                    case HeightPathMode.Set:
                        next = path.heightDelta;
                        break;
                    case HeightPathMode.Offset:
                    {
                        float refH = offsetReference != null ? offsetReference[y, x] : current;
                        next = refH + path.heightDelta;
                        break;
                    }
                    case HeightPathMode.Lower:
                        next = current - path.heightDelta;
                        break;
                    case HeightPathMode.Raise:
                        next = current + path.heightDelta;
                        break;
                }
                img[y, x] = Mathf.Clamp01(next);
            }
        }
    }

    static void ApplyMaterialPath(Texture2D tex, MaterialPath path)
    {
        float halfWidthSvg = path.width * 0.5f;
        List<Vector2> strokePoly = BuildStrokePolygonForMePath(path, halfWidthSvg, path.samplesPerCubicSegment);
        if (strokePoly == null || strokePoly.Count < 3) return;

        GetPolygonBoundsSvg(strokePoly, out float minX, out float minY, out float maxX, out float maxY);

        Vector2 pMin = SvgToTexturePixel(new Vector2(minX, minY), tex.width, tex.height);
        Vector2 pMax = SvgToTexturePixel(new Vector2(maxX, maxY), tex.width, tex.height);
        int left = Mathf.Max(0, Mathf.Min((int)pMin.x, (int)pMax.x));
        int right = Mathf.Min(tex.width - 1, Mathf.Max((int)pMin.x, (int)pMax.x));
        int bottom = Mathf.Max(0, Mathf.Min((int)pMin.y, (int)pMax.y));
        int top = Mathf.Min(tex.height - 1, Mathf.Max((int)pMin.y, (int)pMax.y));

        Color paint = MaterialIndexToColor(path.materialIndex);

        for (int py = bottom; py <= top; py++)
        {
            for (int px = left; px <= right; px++)
            {
                Vector2 svg = TexturePixelToSvg(new Vector2(px, py), tex.width, tex.height);
                if (!PointInPolygon(svg, strokePoly)) continue;
                tex.SetPixel(px, py, paint);
            }
        }

        tex.Apply();
    }

    static Color MaterialIndexToColor(int materialIndex)
    {
        switch (materialIndex)
        {
            case 1: return new Color(1, 0, 0, 1);
            case 2: return new Color(0, 1, 0, 1);
            case 3: return new Color(0, 0, 1, 1);
            case 4: return new Color(0, 0, 0, 0);
            default: return new Color(0, 0, 0, 1);
        }
    }

    public static Vector2 SvgToTexturePixel(Vector2 svgPos, int texWidth, int texHeight)
    {
        Vector2 pos = new Vector2(svgPos.x / 2f, (SvgCanvasSize - svgPos.y) / 2f + 512f);
        float scaleX = texWidth / (float)SvgCanvasSize;
        float scaleY = texHeight / (float)SvgCanvasSize;
        return new Vector2(pos.x * scaleX, pos.y * scaleY);
    }

    static Vector2 TexturePixelToSvg(Vector2 pixel, int texWidth, int texHeight)
    {
        float scaleX = texWidth / (float)SvgCanvasSize;
        float scaleY = texHeight / (float)SvgCanvasSize;
        float px = pixel.x / scaleX;
        float py = SvgCanvasSize - (pixel.y / scaleY - 512f) * 2f;
        return new Vector2(px * 2f, py);
    }

    static void SvgToHeightmapIndex(Vector2 svg, int res, float worldX, float worldZ, out int hx, out int hy)
    {
        Vector2 u3d = MathOfRwrme.SvgPosToU3dPos(svg);
        hx = Mathf.Clamp((int)(u3d.x / worldX * res), 0, res - 1);
        hy = Mathf.Clamp((int)(u3d.y / worldZ * res), 0, res - 1);
    }

    static Vector2 HeightmapIndexToU3d(int x, int y, int res, float worldX, float worldZ)
    {
        return new Vector2((x + 0.5f) / res * worldX, (y + 0.5f) / res * worldZ);
    }

    static Vector2 U3dToSvg(Vector2 u3d)
    {
        return MathOfRwrme.U3dPosToSvgPos(u3d);
    }

    static void GetPathBoundsSvg(List<Vector2> pts, float padding, out float minX, out float minY, out float maxX, out float maxY)
    {
        minX = minY = float.MaxValue;
        maxX = maxY = float.MinValue;
        foreach (Vector2 p in pts)
        {
            if (p.x < minX) minX = p.x;
            if (p.y < minY) minY = p.y;
            if (p.x > maxX) maxX = p.x;
            if (p.y > maxY) maxY = p.y;
        }
        minX -= padding;
        minY -= padding;
        maxX += padding;
        maxY += padding;
    }

    static void GetPolygonBoundsSvg(List<Vector2> poly, out float minX, out float minY, out float maxX, out float maxY)
    {
        minX = minY = float.MaxValue;
        maxX = maxY = float.MinValue;
        foreach (Vector2 p in poly)
        {
            if (p.x < minX) minX = p.x;
            if (p.y < minY) minY = p.y;
            if (p.x > maxX) maxX = p.x;
            if (p.y > maxY) maxY = p.y;
        }
    }

    /// <summary>开放折线硬描边：方头端帽 + miter 拐角，无距离场渐变。</summary>
    static List<Vector2> BuildHardStrokePolygon(List<Vector2> pts, float halfWidth)
    {
        int n = pts?.Count ?? 0;
        if (n < 2 || halfWidth <= 0f) return null;

        var left = new List<Vector2>(n);
        var right = new List<Vector2>(n);

        for (int i = 0; i < n; i++)
        {
            if (i == 0)
            {
                Vector2 dir = (pts[1] - pts[0]).normalized;
                Vector2 nrm = PerpLeft(dir);
                left.Add(pts[0] + nrm * halfWidth);
                right.Add(pts[0] - nrm * halfWidth);
            }
            else if (i == n - 1)
            {
                Vector2 dir = (pts[n - 1] - pts[n - 2]).normalized;
                Vector2 nrm = PerpLeft(dir);
                left.Add(pts[n - 1] + nrm * halfWidth);
                right.Add(pts[n - 1] - nrm * halfWidth);
            }
            else
            {
                ComputeMiterOffsets(pts[i - 1], pts[i], pts[i + 1], halfWidth, out Vector2 ml, out Vector2 mr);
                left.Add(ml);
                right.Add(mr);
            }
        }

        var poly = new List<Vector2>(n * 2);
        poly.AddRange(left);
        for (int i = right.Count - 1; i >= 0; i--)
            poly.Add(right[i]);
        return poly;
    }

    static Vector2 PerpLeft(Vector2 dir) => new Vector2(-dir.y, dir.x);

    static void ComputeMiterOffsets(Vector2 a, Vector2 b, Vector2 c, float halfWidth, out Vector2 left, out Vector2 right)
    {
        Vector2 d1 = (b - a).normalized;
        Vector2 d2 = (c - b).normalized;
        Vector2 n1 = PerpLeft(d1);
        Vector2 n2 = PerpLeft(d2);

        left = MiterPoint(b, n1, d1, n2, d2, halfWidth);
        right = MiterPoint(b, -n1, d1, -n2, d2, halfWidth);
    }

    static Vector2 MiterPoint(Vector2 corner, Vector2 n1, Vector2 d1, Vector2 n2, Vector2 d2, float halfWidth)
    {
        Vector2 p1 = corner + n1 * halfWidth;
        Vector2 p2 = corner + n2 * halfWidth;
        if (LineLineIntersect(p1, d1, p2, d2, out Vector2 hit))
        {
            float maxMiter = halfWidth * 8f;
            if ((hit - corner).sqrMagnitude <= maxMiter * maxMiter)
                return hit;
        }

        Vector2 bis = n1 + n2;
        if (bis.sqrMagnitude < 1e-10f)
            return corner + n1 * halfWidth;
        bis = bis.normalized;
        return corner + bis * halfWidth;
    }

    static bool LineLineIntersect(Vector2 p1, Vector2 d1, Vector2 p2, Vector2 d2, out Vector2 hit)
    {
        float cross = d1.x * d2.y - d1.y * d2.x;
        if (Mathf.Abs(cross) < 1e-8f)
        {
            hit = (p1 + p2) * 0.5f;
            return false;
        }

        Vector2 delta = p2 - p1;
        float t = (delta.x * d2.y - delta.y * d2.x) / cross;
        hit = p1 + d1 * t;
        return true;
    }

    static bool PointInPolygon(Vector2 p, List<Vector2> poly)
    {
        bool inside = false;
        int count = poly.Count;
        for (int i = 0, j = count - 1; i < count; j = i++)
        {
            Vector2 pi = poly[i];
            Vector2 pj = poly[j];
            if (((pi.y > p.y) != (pj.y > p.y))
                && (p.x < (pj.x - pi.x) * (p.y - pi.y) / (pj.y - pi.y + 1e-10f) + pi.x))
                inside = !inside;
        }
        return inside;
    }

    static float DistanceToPolyline(Vector2 p, List<Vector2> pts)
    {
        float best = float.MaxValue;
        for (int i = 0; i < pts.Count - 1; i++)
        {
            float d = DistancePointSegment(p, pts[i], pts[i + 1]);
            if (d < best) best = d;
        }
        return best;
    }

    static float DistancePointSegment(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float lenSq = ab.sqrMagnitude;
        if (lenSq < 1e-6f) return Vector2.Distance(p, a);
        float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / lenSq);
        Vector2 proj = a + ab * t;
        return Vector2.Distance(p, proj);
    }
}