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
        setHeightFromMeta();
        StartCoroutine(ScatterMapItems());
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

        teerainData.size = new Vector3(teerainData.size.x,m_mm.m_metaTerrain.maxHeight, teerainData.size.z);
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
        foreach (MapItem mapItem in m_mm.defaultLayer.mapItems)
        {
            
            mapItem.scatterThis();
            if (index != mapItem.layerIndex)
            {
                index = mapItem.layerIndex;
                yield return null;
            }
        }
        foreach (MapItem mapItem in m_mm.baseLayer.mapItems)
        {
            mapItem.scatterThis();
        }
    }
    public void destroyAllOutMapitems()
    {
        MapItem[] allItems = FindObjectsOfType<MapItem>(true);
        foreach (MapItem item in allItems)
        {
            // �ؼ������ gameObject �Ƿ��ڳ�����
            if (!MetaMap.instance.defaultLayer.mapItems.Contains(item))
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
}
