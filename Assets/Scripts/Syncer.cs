using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class Syncer : MonoBehaviour
{

    MetaMap m_mm;
    Terrain m_terrain;
    static public Syncer instence;

    public GameObject toggleConstructions;
    public GameObject toggleSpawnPoints;
    public GameObject toggleMeshs;
    public GameObject toggleDecals;

    Transform mapRangeRoot;

    GrayScaleImage _heightPreviewCache;
    Texture2D _alphaPreviewCache;
    float[,] _heightsUploadBuffer;
    bool _heightCacheReady;
    bool _alphaCacheReady;

    // Start is called before the first frame update
    void Start()
    {
        instence = this;
        // ����Э��
        //StartCoroutine(StartupRoutine());
        runToInit();
        EnsureMapRangeOverlays();
        updateMap();
        Debug.Log("SyncerInit");
    }
    IEnumerator StartupRoutine()
    {
        // �ȴ�һ֡��������Start������ִ��
        yield return null;

        // ִ�е�һ����ʼ������
        runToInit();


        // ����ʹ����ʵʱ�䣨����Time.timeScaleӰ�죩
        yield return new WaitForSecondsRealtime(2.0f);

        // ִ�и��µ�ͼ����
        updateMap();

        // �����Ҫ���������������Ӹ���ȴ��Ͳ���

    }
    private void runToInit()
    {
        // ���û��ָ�����Σ����Ի�ȡ��ǰ����
        if (m_terrain == null)
        {
            m_terrain = Terrain.activeTerrain;
            Debug.Log($"[���α༭��] δָ�����Σ�ʹ�õ�ǰ�������: {m_terrain?.name ?? "δ�ҵ�"}");
        }
        if (m_terrain == null)
        {
            Debug.LogError("[���α༭��] ����δ�ҵ����õĵ��Σ�");
            return;
        }

        if (m_mm == null)
        {
            m_mm = gameObject.GetComponent<MetaMap>();
            Debug.Log("get MM");
        }
        if (m_mm == null)
        {
            Debug.LogError("mmmNull��");
            return;
        }

    }
    // Update is called once per frame
    void Update()
    {
        
    }

    public void updateMap()
    {
        PurgeInvalidWallAndOffroad();
        PurgeInvalidTerrainPaths();
        ApplyPreviewTerrain();
        StartCoroutine(ScatterMapItems());
    }

    public void ApplyPreviewTerrain()
    {
        ApplyPreviewHeightmap();
        ApplyPreviewCombinedAlpha();
    }

    void PurgeInvalidTerrainPaths()
    {
        if (m_mm == null) return;

        if (m_mm.heightPathLayer != null)
        {
            for (int i = m_mm.heightPathLayer.mapItems.Count - 1; i >= 0; i--)
            {
                MapItem mi = m_mm.heightPathLayer.mapItems[i];
                if (mi is HeightPath hp && IsPathTooShort(hp.positionLine))
                {
                    m_mm.heightPathLayer.mapItems.RemoveAt(i);
                    if (hp.gameObject != null) Destroy(hp.gameObject);
                }
            }
        }

        if (m_mm.materialPathLayer != null)
        {
            for (int i = m_mm.materialPathLayer.mapItems.Count - 1; i >= 0; i--)
            {
                MapItem mi = m_mm.materialPathLayer.mapItems[i];
                if (mi is MaterialPath mp && IsPathTooShort(mp.positionLine))
                {
                    m_mm.materialPathLayer.mapItems.RemoveAt(i);
                    if (mp.gameObject != null) Destroy(mp.gameObject);
                }
            }
        }
    }

    void ApplyPreviewHeightmap()
    {
        if (m_mm == null || m_terrain == null) return;
        m_mm.EnsurePreTerrain();
        GrayScaleImage pre = m_mm.m_preTerrain.data;
        if (pre == null || pre.Width <= 0) return;

        if (_heightPreviewCache == null) _heightPreviewCache = new GrayScaleImage(pre.Width, pre.Height);
        TerrainPathRasterizer.BakeHeightmapInto(_heightPreviewCache, pre, m_mm.GetHeightPaths());
        _heightCacheReady = true;
        UploadHeightCacheFull();
    }

    void ApplyPreviewCombinedAlpha()
    {
        if (m_mm == null || m_terrain == null || MapImporter.instate == null) return;
        m_mm.EnsurePreCombinedAlpha();
        Texture2D pre = m_mm.preCombinedAlpha;
        if (pre == null) return;

        EnsureAlphaPreviewCache(pre);
        TerrainPathRasterizer.BakeAlphaInto(_alphaPreviewCache, pre, m_mm.GetMaterialPaths());
        _alphaCacheReady = true;
        BindAlphaPreviewMask();
    }

    /// <summary>笔刷即时预览：与成品一致，pre_ + HeightPath。</summary>
    public void ApplyPreHeightToTerrain()
    {
        ApplyPreviewHeightmap();
    }

    /// <summary>笔刷即时预览：与成品一致，pre_ + MaterialPath。</summary>
    public void ApplyPreAlphaToTerrain()
    {
        ApplyPreviewCombinedAlpha();
    }

    /// <summary>笔刷脏矩形局部预览（含路径叠层）。坐标为高度图索引，含端点。</summary>
    public void ApplyBrushHeightPreview(int x0, int y0, int x1, int y1)
    {
        if (m_mm == null || m_terrain == null) return;
        m_mm.EnsurePreTerrain();
        GrayScaleImage pre = m_mm.m_preTerrain.data;
        if (pre == null || pre.Width <= 0) return;

        if (!_heightCacheReady || _heightPreviewCache == null
            || _heightPreviewCache.Width != pre.Width || _heightPreviewCache.Height != pre.Height)
        {
            ApplyPreviewHeightmap();
            return;
        }

        int res = pre.Width;
        x0 = Mathf.Clamp(x0, 0, res - 1);
        x1 = Mathf.Clamp(x1, 0, res - 1);
        y0 = Mathf.Clamp(y0, 0, res - 1);
        y1 = Mathf.Clamp(y1, 0, res - 1);
        if (x1 < x0 || y1 < y0) return;

        TerrainPathRasterizer.RebakeHeightRect(
            _heightPreviewCache, pre, m_mm.GetHeightPaths(), x0, y0, x1, y1);
        UploadHeightCacheRect(x0, y0, x1, y1);
    }

    /// <summary>材质笔刷脏矩形局部预览（含路径叠层）。</summary>
    public void ApplyBrushAlphaPreview(int x0, int y0, int x1, int y1)
    {
        if (m_mm == null || MapImporter.instate == null) return;
        m_mm.EnsurePreCombinedAlpha();
        Texture2D pre = m_mm.preCombinedAlpha;
        if (pre == null) return;

        if (!_alphaCacheReady || _alphaPreviewCache == null
            || _alphaPreviewCache.width != pre.width || _alphaPreviewCache.height != pre.height)
        {
            ApplyPreviewCombinedAlpha();
            return;
        }

        x0 = Mathf.Clamp(x0, 0, pre.width - 1);
        x1 = Mathf.Clamp(x1, 0, pre.width - 1);
        y0 = Mathf.Clamp(y0, 0, pre.height - 1);
        y1 = Mathf.Clamp(y1, 0, pre.height - 1);
        if (x1 < x0 || y1 < y0) return;

        TerrainPathRasterizer.RebakeAlphaRect(
            _alphaPreviewCache, pre, m_mm.GetMaterialPaths(), x0, y0, x1, y1);
        BindAlphaPreviewMask();
    }

    void EnsureAlphaPreviewCache(Texture2D pre)
    {
        if (_alphaPreviewCache != null
            && _alphaPreviewCache.width == pre.width
            && _alphaPreviewCache.height == pre.height)
            return;

        if (_alphaPreviewCache != null)
            Destroy(_alphaPreviewCache);

        _alphaPreviewCache = new Texture2D(pre.width, pre.height, TextureFormat.RGBA32, false);
        _alphaPreviewCache.wrapMode = TextureWrapMode.Clamp;
        _alphaPreviewCache.filterMode = FilterMode.Bilinear;
        _alphaCacheReady = false;
    }

    void UploadHeightCacheFull()
    {
        if (_heightPreviewCache == null || m_terrain == null) return;
        int resolution = _heightPreviewCache.Width;
        TerrainData terrainData = m_terrain.terrainData;
        terrainData.heightmapResolution = resolution;
        EnsureHeightsBuffer(resolution, resolution);
        for (int y = 0; y < resolution; y++)
            for (int x = 0; x < resolution; x++)
                _heightsUploadBuffer[y, x] = _heightPreviewCache[y, x];

        float worldX = MapImporter.instate != null ? MapImporter.instate.pageWorldX : terrainData.size.x;
        float worldZ = MapImporter.instate != null ? MapImporter.instate.pageWorldZ : terrainData.size.z;
        terrainData.size = new Vector3(worldX, m_mm.m_metaTerrain.maxHeight, worldZ);
        terrainData.SetHeights(0, 0, _heightsUploadBuffer);
    }

    void UploadHeightCacheRect(int x0, int y0, int x1, int y1)
    {
        if (_heightPreviewCache == null || m_terrain == null) return;
        int w = x1 - x0 + 1;
        int h = y1 - y0 + 1;
        EnsureHeightsBuffer(h, w);
        _heightPreviewCache.FillHeightsPatch(_heightsUploadBuffer, x0, y0, x1, y1);
        m_terrain.terrainData.SetHeights(x0, y0, _heightsUploadBuffer);
    }

    void EnsureHeightsBuffer(int rows, int cols)
    {
        if (_heightsUploadBuffer != null
            && _heightsUploadBuffer.GetLength(0) == rows
            && _heightsUploadBuffer.GetLength(1) == cols)
            return;
        _heightsUploadBuffer = new float[rows, cols];
    }

    void BindAlphaPreviewMask()
    {
        if (_alphaPreviewCache == null || MapImporter.instate == null) return;
        Material mat = m_terrain != null ? m_terrain.materialTemplate : null;
        if (mat == null) mat = MapImporter.instate.cbdTl;
        if (mat == null) return;

        Texture old = mat.GetTexture("_Mask");
        mat.SetTexture("_Mask", _alphaPreviewCache);
        if (old != null && old != _alphaPreviewCache
            && old != m_mm.preCombinedAlpha
            && old != m_mm.referenceCombinedAlpha
            && old != m_mm.CombinedAlpha)
            Destroy(old);
    }

    /// <summary>从地图数据中移除非法路径：锚点数小于 2 的 <see cref="Wall"/>、<see cref="Offroad"/>。</summary>
    void PurgeInvalidWallAndOffroad()
    {
        if (m_mm == null)
            return;

        for (int i = m_mm.defaultLayer.mapItems.Count - 1; i >= 0; i--)
        {
            MapItem mi = m_mm.defaultLayer.mapItems[i];
            if (mi is Wall w && IsPathTooShort(w.positionLine))
            {
                m_mm.defaultLayer.mapItems.RemoveAt(i);
                if (w != null && w.gameObject != null)
                    Destroy(w.gameObject);
            }
        }

        if (m_mm.offroadLayer != null)
        {
            for (int i = m_mm.offroadLayer.mapItems.Count - 1; i >= 0; i--)
            {
                MapItem mi = m_mm.offroadLayer.mapItems[i];
                if (mi is Offroad o && IsPathTooShort(o.positionLine))
                {
                    m_mm.offroadLayer.mapItems.RemoveAt(i);
                    if (o != null && o.gameObject != null)
                        Destroy(o.gameObject);
                }
            }
        }
    }

    static bool IsPathTooShort(List<Vector2> positionLine)
    {
        return positionLine == null || positionLine.Count < 2;
    }

    public void setHeightFromMeta()
    {
        ApplyPreviewHeightmap();
    }

    /*//deuse on 2025 11 27
    public void scatterBuildings()
    {
        foreach(MapItem mapItem in m_mm.defaultLayer.mapItems)
        {
            if(mapItem is Building bld)
            {
                //Debug.Log("setBuilding as ");
                GameObject newInstance = Instantiate(buildingPrefeb);
                newInstance.transform.localScale = new Vector3(bld.size.x, bld.height * 3.0f, bld.size.y);
                newInstance.transform.position = new Vector3(bld.position.x, m_terrain.SampleHeight(new Vector3(bld.position.x, 0, bld.position.y)),bld.position.y);
                newInstance.transform.rotation = Quaternion.Euler(0f,-1*bld.rotation, 0f);
                newInstance.GetComponent<ObjectContainer>().pointerToMapItem = bld;
            }
        }
        
    }
    */

    public IEnumerator ScatterMapItems()
    {
        m_mm.defaultLayer.sortByIndex();
        int index = 0; 
        string name = "";
        foreach (MapItem mapItem in m_mm.defaultLayer.mapItems)
        {
            mapItem.scatterThis();
            if (index != mapItem.layerIndex)
            {
                index = mapItem.layerIndex;
                yield return null;
            }
            else if (name != mapItem.GetType().Name)
            {
                name = mapItem.GetType().Name;
                yield return null;
            }
        }
        foreach (MapItem mapItem in m_mm.baseLayer.mapItems)
        {
            mapItem.scatterThis();
        }
        if (m_mm.offroadLayer != null)
        {
            m_mm.offroadLayer.sortByIndex();
            index = 0;
            name = "";
            foreach (MapItem mapItem in m_mm.offroadLayer.mapItems)
            {
                mapItem.scatterThis();
                if (index != mapItem.layerIndex)
                {
                    index = mapItem.layerIndex;
                    yield return null;
                }
                else if (name != mapItem.GetType().Name)
                {
                    name = mapItem.GetType().Name;
                    yield return null;
                }
            }
        }
        if (m_mm.heightPathLayer != null)
        {
            foreach (MapItem mapItem in m_mm.heightPathLayer.mapItems)
                mapItem.scatterThis();
        }
        if (m_mm.materialPathLayer != null)
        {
            foreach (MapItem mapItem in m_mm.materialPathLayer.mapItems)
                mapItem.scatterThis();
        }
    }
    public void destroyAllOutMapitems()
    {
        MapItem[] allItems = FindObjectsOfType<MapItem>(true);
        foreach (MapItem item in allItems)
        {
            // �ؼ������ gameObject �Ƿ��ڳ�����
            if (!MetaMap.instance.defaultLayer.mapItems.Contains(item)
                && !MetaMap.instance.baseLayer.mapItems.Contains(item)
                && (MetaMap.instance.offroadLayer == null || !MetaMap.instance.offroadLayer.mapItems.Contains(item)))
            {
                Destroy(item.gameObject);
            }
        }
    }
    public void changeConstructionVisState(bool stat)
    {
        toggleConstructions.GetComponent<Toggle>().isOn = stat;
        toggleSpawnPoints.GetComponent<Toggle>().SetIsOnWithoutNotify(stat);
        toggleMeshs.GetComponent<Toggle>().SetIsOnWithoutNotify(stat);
        toggleDecals.GetComponent<Toggle>().SetIsOnWithoutNotify(stat);
        foreach (MapItem mi in MetaMap.instance.defaultLayer.mapItems)
        {
            mi.gameObject.SetActive(stat);
        }
    }
    public void SyncGeneralSettingInfo()
    {

    }
    public void changeBaseVisState(bool stat)
    {

        foreach (MapItem mi in MetaMap.instance.baseLayer.mapItems)
        {
            mi.gameObject.SetActive(stat);
        }
    }
    public void changeOffroadVisState(bool stat)
    {
        if (MetaMap.instance.offroadLayer == null) return;
        foreach (MapItem mi in MetaMap.instance.offroadLayer.mapItems)
        {
                mi.gameObject.SetActive(stat);
        }
    }

    public void changeSpawnPointVisState(bool stat)
    {
        foreach (MapItem mi in MetaMap.instance.defaultLayer.mapItems)
        {
            if(mi is SpawnPoint)
            {
                mi.gameObject.SetActive(stat);
            }
        }
    }
    public void changeMeshsVisState(bool stat)
    {
        foreach (MapItem mi in MetaMap.instance.defaultLayer.mapItems)
        {
            if (mi is MeMesh)
            {
                mi.gameObject.SetActive(stat);
            }
        }
    }

    public void changeDecalsVisState(bool stat)
    {
        foreach (MapItem mi in MetaMap.instance.defaultLayer.mapItems)
        {
            if (mi is Decal)
            {
                mi.gameObject.SetActive(stat);
            }
        }
    }

    public void changeMapRangeVisState(bool stat)
    {
        EnsureMapRangeOverlays();
        if (mapRangeRoot != null)
            mapRangeRoot.gameObject.SetActive(stat);
    }

    /// <summary>VisualChanger / Stuck：开启时扫描全部层路径碰撞问题并标出；关闭清除标记。</summary>
    public void changeStuckDetectState(bool stat)
    {
        if (_stuckScanCo != null)
        {
            StopCoroutine(_stuckScanCo);
            _stuckScanCo = null;
        }

        var markers = PathDetectionMarkers.Ensure();
        if (!stat)
        {
            markers.Clear();
            markers.SetEnabled(false);
            return;
        }

        markers.SetEnabled(true);
        _stuckScanCo = StartCoroutine(StuckScanRoutine(markers));
    }

    IEnumerator StuckScanRoutine(PathDetectionMarkers markers)
    {
        PathCollisionDetector.ScanResult result = null;
        yield return PathCollisionDetector.ScanCoroutine(
            new PathCollisionDetector.ScanOptions
            {
                LayerFilter = null, // 逐层全部
                MinWidthWorld = PathCollisionDetector.DefaultMinWidthWorld,
                Progress = msg => Debug.Log("[StuckDetect] " + msg),
            },
            r => result = r);

        markers.Show(result?.Issues);
        Debug.Log("[StuckDetect] " + (result != null ? result.Message : "empty"));
        _stuckScanCo = null;
    }

    Coroutine _stuckScanCo;

    void EnsureMapRangeOverlays()
    {
        if (mapRangeRoot != null) return;

        GameObject root = new GameObject("MapRangeOverlay");
        mapRangeRoot = root.transform;

        // SVG 边距条：x/y 的 0–60 与 1988–2048（满幅另一轴）
        CreateMapRangeStrip("MapRange_X0", 0f, 0f, 60f, 2048f);
        CreateMapRangeStrip("MapRange_Y0", 0f, 0f, 2048f, 60f);
        CreateMapRangeStrip("MapRange_X1", 1988f, 0f, 60f, 2048f);
        CreateMapRangeStrip("MapRange_Y1", 0f, 1988f, 2048f, 60f);
    }

    /// <summary>在 SVG 矩形 [x,y,w,h] 上铺一条水平半透明红 Quad（无碰撞）。</summary>
    void CreateMapRangeStrip(string name, float svgX, float svgY, float svgW, float svgH)
    {
        Vector2 u0 = MathOfRwrme.SvgPosToU3dPos(new Vector2(svgX, svgY));
        Vector2 u1 = MathOfRwrme.SvgPosToU3dPos(new Vector2(svgX + svgW, svgY + svgH));

        float minX = Mathf.Min(u0.x, u1.x);
        float maxX = Mathf.Max(u0.x, u1.x);
        float minZ = Mathf.Min(u0.y, u1.y);
        float maxZ = Mathf.Max(u0.y, u1.y);

        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Quad);
        go.name = name;
        go.transform.SetParent(mapRangeRoot, false);

        Collider col = go.GetComponent<Collider>();
        if (col != null) Destroy(col);

        go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        go.transform.position = new Vector3((minX + maxX) * 0.5f, 1f, (minZ + maxZ) * 0.5f);
        go.transform.localScale = new Vector3(Mathf.Max(0.01f, maxX - minX), Mathf.Max(0.01f, maxZ - minZ), 1f);

        Renderer rend = go.GetComponent<Renderer>();
        if (rend != null)
        {
            Shader sh = Shader.Find("Unlit/Color");
            if (sh == null) sh = Shader.Find("Sprites/Default");
            if (sh == null) sh = Shader.Find("Standard");
            Material mat = new Material(sh);
            Color c = new Color(1f, 0f, 0f, 0.35f);
            mat.color = c;
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", c);
            // 尽量开透明（Unlit/Color 可能不支持，颜色仍可见）
            if (sh != null && sh.name.Contains("Standard"))
            {
                mat.SetFloat("_Mode", 3f);
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite", 0);
                mat.DisableKeyword("_ALPHATEST_ON");
                mat.EnableKeyword("_ALPHABLEND_ON");
                mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                mat.renderQueue = 3000;
                mat.color = c;
            }
            rend.material = mat;
            rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            rend.receiveShadows = false;
        }
    }
}
