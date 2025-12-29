using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;

public class MapExporter : MonoBehaviour
{
    // Start is called before the first frame update
    MetaMap m_mm;
    Terrain targetTerrain;
    
    void Start()
    {
        if (m_mm == null)
        {
            m_mm = gameObject.GetComponent<MetaMap>();
        }
        
        // 获取地形对象
        if (targetTerrain == null)
        {
            targetTerrain = Terrain.activeTerrain;
            if (targetTerrain == null)
            {
                targetTerrain = FindObjectOfType<Terrain>();
            }
            
            if (targetTerrain != null)
            {
                Debug.Log($"MapExporter: 找到地形 {targetTerrain.name}");
            }
            else
            {
                Debug.LogWarning("MapExporter: 未找到地形对象，地形导出功能将不可用");
            }
        }
        
        Debug.Log("MapExporter Init");
    }

    public void exportMap()
    {
        Debug.Log("MapExport");
        string fullPath = System.IO.Path.Combine(Application.dataPath+"/map/", m_mm.m_metaTerrain.fileName);
        System.IO.File.WriteAllBytes(fullPath, m_mm.m_metaTerrain.data.convToPng());
        
        // 同时导出地形高度图
        exportTerrainHeightmap();
    }

    /// <summary>
    /// 导出地形高度图
    /// </summary>
    public void exportTerrainHeightmap()
    {
        Debug.Log("=== 开始导出地形高度图 ===");
        
        // 检查地形对象
        if (targetTerrain == null)
        {
            targetTerrain = Terrain.activeTerrain;
            if (targetTerrain == null)
            {
                targetTerrain = FindObjectOfType<Terrain>();
            }
        }
        
        if (targetTerrain == null)
        {
            Debug.LogError("无法导出地形高度图：未找到地形对象！");
            return;
        }

        Debug.Log($"正在导出地形: {targetTerrain.name}");

        // 获取地形数据
        TerrainData terrainData = targetTerrain.terrainData;
        int resolution = terrainData.heightmapResolution;
        Debug.Log($"地形高度图分辨率: {resolution} x {resolution}");

        // 获取当前地形高度数据
        float[,] heights = terrainData.GetHeights(0, 0, resolution, resolution);

        // 创建灰度纹理（使用 R8 单通道格式）
        Texture2D exportTexture = new Texture2D(resolution, resolution, TextureFormat.R8, false);

        // 找到实际的高度范围（用于归一化）
        float actualMin = float.MaxValue;
        float actualMax = float.MinValue;

        // 首先扫描一遍找到实际高度范围
        for (int y = 0; y < resolution; y++)
        {
            for (int x = 0; x < resolution; x++)
            {
                float height = heights[y, x];
                if (height < actualMin) actualMin = height;
                if (height > actualMax) actualMax = height;
            }
        }

        Debug.Log($"实际高度范围: {actualMin:F3} 到 {actualMax:F3}");

        // 转换高度数据为灰度字节数组
        byte[] grayPixels = new byte[resolution * resolution];

        for (int y = 0; y < resolution; y++)
        {
            for (int x = 0; x < resolution; x++)
            {
                // 获取高度值
                float height = heights[y, x];

                // 将高度值归一化到0-1范围
                float normalizedHeight;
                if (actualMax > actualMin)
                {
                    normalizedHeight = (height - actualMin) / (actualMax - actualMin);
                }
                else
                {
                    normalizedHeight = 0.5f; // 如果地形完全平坦，使用中间值
                }

                // 计算像素索引
                int pixelIndex = y * resolution + x;

                // 转换为0-255的灰度值
                grayPixels[pixelIndex] = (byte)(normalizedHeight * 255);
            }

            // 每处理10%报告一次进度
            if (y % (resolution / 10) == 0)
            {
                float progress = (float)y / resolution * 100f;
                Debug.Log($"导出进度: {progress:F1}%");
            }
        }

        // 使用 LoadRawTextureData 加载灰度数据
        exportTexture.LoadRawTextureData(grayPixels);
        exportTexture.Apply();

        // 保存为PNG文件
        string fileName = $"TerrainHeightmap_{System.DateTime.Now:yyyyMMdd_HHmmss}.png";
        string filePath = Path.Combine(Application.dataPath, "map", fileName);
        
        // 确保map目录存在
        string mapDir = Path.Combine(Application.dataPath, "map");
        if (!Directory.Exists(mapDir))
        {
            Directory.CreateDirectory(mapDir);
            Debug.Log($"创建目录: {mapDir}");
        }

        byte[] pngData = exportTexture.EncodeToPNG();
        File.WriteAllBytes(filePath, pngData);

        // 清理临时纹理
        DestroyImmediate(exportTexture);

        Debug.Log($"✓ 地形高度图已导出至: {filePath}");
        Debug.Log($"✓ 文件大小: {pngData.Length / 1024} KB");
        Debug.Log($"✓ 图片格式: 灰度PNG (单通道 8 位)");
        Debug.Log($"✓ 导出的高度范围: {actualMin:F3} 到 {actualMax:F3}");
        Debug.Log("=== 地形高度图导出完成！ ===");

#if UNITY_EDITOR
        // 在编辑器中刷新资产数据库
        UnityEditor.AssetDatabase.Refresh();
        Debug.Log("已刷新Unity资产数据库");
#endif
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
