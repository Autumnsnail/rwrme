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


    // Start is called before the first frame update
    void Start()
    {
        instence = this;
        // ����Э��
        //StartCoroutine(StartupRoutine());
        runToInit();
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
        setHeightFromMeta();
        StartCoroutine(ScatterMapItems());
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
        Debug.Log("��ʼӦ�ûҶ�ͼ������...");
        
        TerrainData teerainData = m_terrain.terrainData;
        
        teerainData.heightmapResolution = m_mm.m_metaTerrain.resolutionX;

        int resolution = teerainData.heightmapResolution;

        Debug.Log($"resolutionX={resolution}");
        float[,] heights = new float[resolution, resolution];

        for(int y=0; y<resolution; y++)
        {
            for(int x=0; x<resolution; x++)
            {
                //Debug.Log($"{m_mm.m_metaTerrain.data[y, x]*m_mm.m_metaTerrain.maxHeight}");
                heights[y, x] = m_mm.m_metaTerrain.data[y,x];
            }
        }

        float worldX = MapImporter.instate != null ? MapImporter.instate.pageWorldX : teerainData.size.x;
        float worldZ = MapImporter.instate != null ? MapImporter.instate.pageWorldZ : teerainData.size.z;
        teerainData.size = new Vector3(worldX, m_mm.m_metaTerrain.maxHeight, worldZ);
        teerainData.SetHeights(0, 0, heights);

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
}
