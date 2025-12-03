using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Syncer : MonoBehaviour
{

    MetaMap m_mm;
    Terrain m_terrain;

    // Start is called before the first frame update
    void Start()
    {
        // 启动协程
        //StartCoroutine(StartupRoutine());
        runToInit();
        updateMap();
        Debug.Log("SyncerInit");
    }

    IEnumerator StartupRoutine()
    {
        // 等待一帧，让其他Start方法先执行
        yield return null;

        // 执行第一个初始化函数
        runToInit();


        // 或者使用真实时间（不受Time.timeScale影响）
        yield return new WaitForSecondsRealtime(2.0f);

        // 执行更新地图函数
        updateMap();

        // 如果需要，可以在这里添加更多等待和操作

    }

    private void runToInit()
    {
        // 如果没有指定地形，尝试获取当前地形
        if (m_terrain == null)
        {
            m_terrain = Terrain.activeTerrain;
            Debug.Log($"[地形编辑器] 未指定地形，使用当前激活地形: {m_terrain?.name ?? "未找到"}");
        }
        if (m_terrain == null)
        {
            Debug.LogError("[地形编辑器] 错误：未找到可用的地形！");
            return;
        }

        if (m_mm == null)
        {
            m_mm = gameObject.GetComponent<MetaMap>();
            Debug.Log("get MM");
        }
        if (m_mm == null)
        {
            Debug.LogError("mmmNull！");
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
        //scatterBuildings();
        StartCoroutine(ScatterMapItems());
    }

    public void setHeightFromMeta()
    {
        Debug.Log("开始应用灰度图到地形...");
        
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
        foreach(MapItem mapItem in m_mm.defaultLayer.mapItems)
        {
            mapItem.scatterThis();
            yield return null;
        }
    }
}
