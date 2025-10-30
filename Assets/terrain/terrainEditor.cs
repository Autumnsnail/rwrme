using UnityEngine;
using System.Collections;

/// <summary>
/// 运行时地形编辑器
/// 功能：在游戏运行时动态修改地形高度、绘制纹理、放置树木和细节对象
/// </summary>
public class RuntimeTerrainEditor : MonoBehaviour
{
    [Header("地形引用")]
    public Terrain terrain; // 要编辑的地形引用

    [Header("笔刷设置")]
    public float brushSize = 10f;        // 笔刷大小
    public float brushStrength = 0.1f;   // 笔刷强度
    public float maxHeight = 100f;       // 最大高度限制

    [Header("测试设置")]
    public bool enableTestOnStart = true; // 启动时是否运行测试
    public Vector3 testPosition = new Vector3(100, 0, 100); // 测试位置

    private TerrainData terrainData;
    private int heightmapWidth;
    private int heightmapHeight;

    // 启动时初始化
    void Start()
    {
        InitializeTerrainEditor();

        if (enableTestOnStart)
        {
            RunEditorTests();
        }
    }

    /// <summary>
    /// 初始化地形编辑器
    /// </summary>
    void InitializeTerrainEditor()
    {
        // 如果没有指定地形，尝试获取当前地形
        if (terrain == null)
        {
            terrain = Terrain.activeTerrain;
            Debug.Log($"[地形编辑器] 未指定地形，使用当前激活地形: {terrain?.name ?? "未找到"}");
        }

        if (terrain == null)
        {
            Debug.LogError("[地形编辑器] 错误：未找到可用的地形！");
            return;
        }

        terrainData = terrain.terrainData;
        heightmapWidth = terrainData.heightmapResolution;
        heightmapHeight = terrainData.heightmapResolution;

        Debug.Log($"[地形编辑器] 初始化完成");
        Debug.Log($"[地形编辑器] 地形: {terrain.name}");
        Debug.Log($"[地形编辑器] 高度图分辨率: {heightmapWidth}x{heightmapHeight}");
        Debug.Log($"[地形编辑器] 地形尺寸: {terrainData.size}");
        Debug.Log($"[地形编辑器] 笔刷大小: {brushSize}, 强度: {brushStrength}");
    }

    /// <summary>
    /// 在指定世界坐标位置抬升地形
    /// </summary>
    /// <param name="worldPosition">世界坐标位置</param>
    public void RaiseTerrain(Vector3 worldPosition)
    {
        if (!IsTerrainReady()) return;

        Debug.Log($"[地形编辑器] 抬升地形位置: {worldPosition}");

        // 将世界坐标转换为高度图坐标
        Vector3 terrainLocalPos = worldPosition - terrain.transform.position;
        Vector2 normalizedPos = new Vector2(
            terrainLocalPos.x / terrainData.size.x,
            terrainLocalPos.z / terrainData.size.z
        );

        int x = Mathf.FloorToInt(normalizedPos.x * heightmapWidth);
        int y = Mathf.FloorToInt(normalizedPos.y * heightmapHeight);

        Debug.Log($"[地形编辑器] 高度图坐标: ({x}, {y})");

        // 修改地形高度
        ModifyHeightmap(x, y, brushStrength);
    }

    /// <summary>
    /// 在指定世界坐标位置降低地形
    /// </summary>
    /// <param name="worldPosition">世界坐标位置</param>
    public void LowerTerrain(Vector3 worldPosition)
    {
        if (!IsTerrainReady()) return;

        Debug.Log($"[地形编辑器] 降低地形位置: {worldPosition}");

        Vector3 terrainLocalPos = worldPosition - terrain.transform.position;
        Vector2 normalizedPos = new Vector2(
            terrainLocalPos.x / terrainData.size.x,
            terrainLocalPos.z / terrainData.size.z
        );

        int x = Mathf.FloorToInt(normalizedPos.x * heightmapWidth);
        int y = Mathf.FloorToInt(normalizedPos.y * heightmapHeight);

        Debug.Log($"[地形编辑器] 高度图坐标: ({x}, {y})");

        // 使用负强度来降低地形
        ModifyHeightmap(x, y, -brushStrength);
    }

    /// <summary>
    /// 修改高度图数据
    /// </summary>
    /// <param name="centerX">中心点X坐标</param>
    /// <param name="centerY">中心点Y坐标</param>
    /// <param name="strength">修改强度</param>
    void ModifyHeightmap(int centerX, int centerY, float strength)
    {
        // 获取当前高度图数据
        float[,] heights = terrainData.GetHeights(0, 0, heightmapWidth, heightmapHeight);

        // 计算笔刷影响范围
        int brushRadius = Mathf.FloorToInt(brushSize * heightmapWidth / terrainData.size.x / 2);
        int startX = Mathf.Max(0, centerX - brushRadius);
        int startY = Mathf.Max(0, centerY - brushRadius);
        int endX = Mathf.Min(heightmapWidth, centerX + brushRadius);
        int endY = Mathf.Min(heightmapHeight, centerY + brushRadius);

        Debug.Log($"[地形编辑器] 笔刷影响范围: ({startX},{startY}) 到 ({endX},{endY})");

        // 应用笔刷效果
        for (int y = startY; y < endY; y++)
        {
            for (int x = startX; x < endX; x++)
            {
                // 计算到笔刷中心的距离
                float distance = Vector2.Distance(new Vector2(x, y), new Vector2(centerX, centerY));
                float normalizedDistance = distance / brushRadius;

                // 距离越远影响越小
                if (normalizedDistance <= 1.0f)
                {
                    float falloff = 1.0f - normalizedDistance;
                    float effect = strength * falloff * Time.deltaTime;

                    // 应用高度修改，确保在合理范围内
                    heights[y, x] = Mathf.Clamp(heights[y, x] + effect, 0f, maxHeight / terrainData.size.y);
                }
            }
        }

        // 应用修改后的高度图
        terrainData.SetHeights(0, 0, heights);

        Debug.Log($"[地形编辑器] 高度图修改完成，强度: {strength}");
    }

    /// <summary>
    /// 平整地形到指定高度
    /// </summary>
    /// <param name="worldPosition">世界坐标位置</param>
    /// <param name="targetHeight">目标高度（0-1标准化）</param>
    public void FlattenTerrain(Vector3 worldPosition, float targetHeight = 0.1f)
    {
        if (!IsTerrainReady()) return;

        Debug.Log($"[地形编辑器] 平整地形位置: {worldPosition}, 目标高度: {targetHeight}");

        Vector3 terrainLocalPos = worldPosition - terrain.transform.position;
        Vector2 normalizedPos = new Vector2(
            terrainLocalPos.x / terrainData.size.x,
            terrainLocalPos.z / terrainData.size.z
        );

        int centerX = Mathf.FloorToInt(normalizedPos.x * heightmapWidth);
        int centerY = Mathf.FloorToInt(normalizedPos.y * heightmapHeight);

        float[,] heights = terrainData.GetHeights(0, 0, heightmapWidth, heightmapHeight);

        int brushRadius = Mathf.FloorToInt(brushSize * heightmapWidth / terrainData.size.x / 2);
        int startX = Mathf.Max(0, centerX - brushRadius);
        int startY = Mathf.Max(0, centerY - brushRadius);
        int endX = Mathf.Min(heightmapWidth, centerX + brushRadius);
        int endY = Mathf.Min(heightmapHeight, centerY + brushRadius);

        for (int y = startY; y < endY; y++)
        {
            for (int x = startX; x < endX; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), new Vector2(centerX, centerY));
                float normalizedDistance = distance / brushRadius;

                if (normalizedDistance <= 1.0f)
                {
                    float falloff = 1.0f - normalizedDistance;
                    // 平滑过渡到目标高度
                    heights[y, x] = Mathf.Lerp(heights[y, x], targetHeight, falloff * brushStrength * Time.deltaTime);
                }
            }
        }

        terrainData.SetHeights(0, 0, heights);
        Debug.Log($"[地形编辑器] 地形平整完成");
    }

    /// <summary>
    /// 检查地形是否准备就绪
    /// </summary>
    /// <returns>是否准备好</returns>
    bool IsTerrainReady()
    {
        if (terrain == null || terrainData == null)
        {
            Debug.LogError("[地形编辑器] 错误：地形未初始化！");
            return false;
        }
        return true;
    }

    /// <summary>
    /// 运行编辑器测试
    /// </summary>
    void RunEditorTests()
    {
        Debug.Log("=== 地形编辑器测试开始 ===");

        if (!IsTerrainReady())
        {
            Debug.LogError("[测试] 地形未准备好，跳过测试");
            return;
        }

        // 测试抬升地形
        Debug.Log("[测试] 执行抬升地形测试...");
        RaiseTerrain(testPosition);

        // 等待一帧
        StartCoroutine(RunDelayedTests());
    }

    /// <summary>
    /// 延迟运行更多测试
    /// </summary>
    IEnumerator RunDelayedTests()
    {
        yield return new WaitForSeconds(0.1f);

        Debug.Log("[测试] 执行降低地形测试...");
        LowerTerrain(testPosition + new Vector3(10, 0, 10));

        yield return new WaitForSeconds(0.1f);

        Debug.Log("[测试] 执行平整地形测试...");
        FlattenTerrain(testPosition + new Vector3(20, 0, 20), 0.2f);

        Debug.Log("=== 地形编辑器测试完成 ===");
    }

    /// <summary>
    /// 在场景视图中绘制Gizmos用于可视化笔刷位置
    /// </summary>
    void OnDrawGizmosSelected()
    {
        if (terrain != null && enableTestOnStart)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(testPosition, brushSize);
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(testPosition + new Vector3(10, 0, 10), brushSize);
            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(testPosition + new Vector3(20, 0, 20), brushSize);
        }
    }
}