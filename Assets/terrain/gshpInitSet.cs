using UnityEngine;
using System.Collections;
using System.IO;

public class TerrainHeightmapFromTexture : MonoBehaviour
{
    [Header("地形设置")]
    public Terrain targetTerrain;

    [Header("灰度图设置")]
    public Texture2D heightmapTexture;
    public bool flipVertically = false;
    public bool flipHorizontally = false;

    [Header("高度调整")]
    [Range(0f, 1f)]
    public float maxHeight = 1f;
    [Range(0f, 1f)]
    public float minHeight = 0f;

    [Header("调试设置")]
    public bool enableLogging = true;
    public bool previewInEditor = false;

    private void Start()
    {
        if (targetTerrain == null)
        {
            targetTerrain = GetComponent<Terrain>();
            if (targetTerrain == null)
            {
                Debug.LogError("未找到地形组件！请将脚本挂载到地形对象上或指定目标地形。");
                return;
            }
        }

        if (heightmapTexture != null)
        {
            ApplyHeightmap();
        }
        else
        {
            Debug.LogWarning("未指定灰度图纹理！");
        }
    }

    [ContextMenu("应用高度图")]
    public void ApplyHeightmap()
    {
        if (targetTerrain == null || heightmapTexture == null)
        {
            Debug.LogError("地形或灰度图为空！");
            return;
        }

        Log("开始应用灰度图到地形...");
        Log($"灰度图尺寸: {heightmapTexture.width} x {heightmapTexture.height}");

        // 获取地形数据
        TerrainData terrainData = targetTerrain.terrainData;
        int resolution = terrainData.heightmapResolution;
        Log($"地形高度图分辨率: {resolution}");

        // 创建高度数组
        float[,] heights = new float[resolution, resolution];

        // 处理灰度图数据
        ProcessHeightmapTexture(heights, resolution);

        // 应用高度数据到地形
        Log("正在设置地形高度...");
        terrainData.SetHeights(0, 0, heights);

        Log("地形高度设置完成！");
        Log($"最终高度范围: {minHeight} 到 {maxHeight}");

        if (previewInEditor)
        {
            Debug.Log("预览模式已启用 - 在编辑器中查看地形变化");
        }
    }

    private void ProcessHeightmapTexture(float[,] heights, int resolution)
    {
        Log("开始处理灰度图数据...");

        int textureWidth = heightmapTexture.width;
        int textureHeight = heightmapTexture.height;

        // 确保纹理可读
        if (!heightmapTexture.isReadable)
        {
            Debug.LogError("灰度图纹理不可读！请在导入设置中启用 'Read/Write Enabled'");
            return;
        }

        // 获取所有像素颜色
        Color[] pixels = heightmapTexture.GetPixels();
        Log($"已获取 {pixels.Length} 个像素数据");

        float minFound = 1f;
        float maxFound = 0f;

        for (int y = 0; y < resolution; y++)
        {
            for (int x = 0; x < resolution; x++)
            {
                // 计算纹理坐标
                int texX = (int)((float)x / resolution * textureWidth);
                int texY = (int)((float)y / resolution * textureHeight);

                // 处理翻转
                if (flipHorizontally) texX = textureWidth - 1 - texX;
                if (flipVertically) texY = textureHeight - 1 - texY;

                // 确保坐标在有效范围内
                texX = Mathf.Clamp(texX, 0, textureWidth - 1);
                texY = Mathf.Clamp(texY, 0, textureHeight - 1);

                // 获取像素索引
                int pixelIndex = texY * textureWidth + texX;

                if (pixelIndex >= 0 && pixelIndex < pixels.Length)
                {
                    // 获取灰度值（使用RGB的平均值或单独通道）
                    Color pixel = pixels[pixelIndex];
                    float grayValue = (pixel.r + pixel.g + pixel.b) / 3f;

                    // 记录找到的最小和最大值
                    if (grayValue < minFound) minFound = grayValue;
                    if (grayValue > maxFound) maxFound = grayValue;

                    // 应用高度范围调整
                    float adjustedHeight = Mathf.Lerp(minHeight, maxHeight, grayValue);

                    heights[y, x] = adjustedHeight;
                }
                else
                {
                    heights[y, x] = 0f;
                    if (enableLogging && (x == 0 || y == 0))
                    {
                        Debug.LogWarning($"像素索引超出范围: {pixelIndex}, 坐标: ({x},{y})");
                    }
                }
            }

            // 每处理10%的行输出一次进度
            if (enableLogging && y % (resolution / 10) == 0)
            {
                float progress = (float)y / resolution * 100f;
                Log($"处理进度: {progress:F1}%");
            }
        }

        Log($"灰度图数据范围: {minFound:F3} 到 {maxFound:F3}");
        Log("灰度图数据处理完成");
    }

    [ContextMenu("重置地形高度")]
    public void ResetTerrainHeight()
    {
        if (targetTerrain != null)
        {
            TerrainData terrainData = targetTerrain.terrainData;
            int resolution = terrainData.heightmapResolution;
            float[,] heights = new float[resolution, resolution];
            terrainData.SetHeights(0, 0, heights);
            Log("地形高度已重置为平面");
        }
    }

    [ContextMenu("打印地形信息")]
    public void PrintTerrainInfo()
    {
        if (targetTerrain != null)
        {
            TerrainData data = targetTerrain.terrainData;
            Log("=== 地形信息 ===");
            Log($"尺寸: {data.size}");
            Log($"高度图分辨率: {data.heightmapResolution}");
            Log($"地形位置: {targetTerrain.transform.position}");
            Log($"地形旋转: {targetTerrain.transform.rotation}");
            Log($"地形缩放: {targetTerrain.transform.localScale}");
        }
    }

    private void Log(string message)
    {
        if (enableLogging)
        {
            Debug.Log($"[TerrainHeightmap] {message}");
        }
    }

    // 在Inspector中验证输入
    private void OnValidate()
    {
        if (maxHeight < minHeight)
        {
            maxHeight = minHeight + 0.01f;
        }

        if (minHeight < 0f) minHeight = 0f;
        if (maxHeight > 1f) maxHeight = 1f;
    }
}