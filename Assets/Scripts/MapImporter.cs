using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using static MapImporter;

public class MapImporter : MonoBehaviour
{
    public enum MapType
    {
        alpha_sand
    }

    [Header("导入设置")]
    public string basePath = "map"; // 基础路径
    public string filePrefix = "terrain5_"; // 文件前缀

    private Dictionary<MapType, GrayScaleImage> loadedMaps = new Dictionary<MapType, GrayScaleImage>();

    void Start()
    {
        ImportAllMaps();
    }

    public void importTerrain()
    {
        TerrainConfigReader tcr = new TerrainConfigReader();
        tcr.LoadTerrainConfig();
        tcr.PrintConfigValues();
        string mapName = "terrain5_heightmap.png";
        int maxHeight = 25;
        /*
        PageWorldX = 1536
        PageWorldZ = 1536
        MaxHeight = 25
        */
        if (tcr.GetValue("Heightmap.image")!=null)
        {
            mapName = tcr.GetValue("Heightmap.image");
            Debug.Log("get heightmap name as "+mapName);
        }
        if(tcr.GetValue("MaxHeight")!=null)
        {
            maxHeight = tcr.GetInt("MaxHeight");
        }
        
        GrayScaleImage grayImage = LoadGrayScaleImage(Path.Combine(Application.persistentDataPath, basePath, mapName));
        gameObject.GetComponent<MetaMap>().m_metaTerrain.setData(grayImage);
        gameObject.GetComponent<MetaMap>().m_metaTerrain.maxHeight = maxHeight;
    }

    public void ImportAllMaps()
    {
        importTerrain();
        foreach (MapType mapType in System.Enum.GetValues(typeof(MapType)))
        {
            string fileName = filePrefix + mapType.ToString().ToLower() + ".png";
            string filePath = Path.Combine(Application.persistentDataPath, basePath, fileName);

            if (File.Exists(filePath))
            {
                GrayScaleImage grayImage = LoadGrayScaleImage(filePath);
                if (grayImage != null)
                {
                    loadedMaps[mapType] = grayImage;
                    Debug.Log($"已加载: {fileName} ({grayImage.Width}x{grayImage.Height})");
                }
            }
            else
            {
                Debug.LogWarning($"文件不存在: {filePath}");
            }
        }

        Debug.Log($"导入完成，共加载 {loadedMaps.Count} 张灰度图");
    }

    private GrayScaleImage LoadGrayScaleImage(string filePath)
    {
        try
        {
            byte[] fileData = File.ReadAllBytes(filePath);
            Texture2D texture = new Texture2D(2, 2);

            if (texture.LoadImage(fileData))
            {
                return ConvertTextureToGrayScaleImage(texture);
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"加载失败: {e.Message}");
        }

        return null;
    }

    private GrayScaleImage ConvertTextureToGrayScaleImage(Texture2D texture)
    {
        int width = texture.width;
        int height = texture.height;
        GrayScaleImage grayImage = new GrayScaleImage(width, height);

        Color[] pixels = texture.GetPixels();

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int index = y * width + x;
                float grayValue = pixels[index].grayscale; // 转换为灰度值
                grayImage[y, x] = grayValue;
            }
        }

        // 清理纹理
        DestroyImmediate(texture);

        return grayImage;
    }

    // 获取指定类型的灰度图
    public GrayScaleImage GetGrayScaleImage(MapType mapType)
    {
        loadedMaps.TryGetValue(mapType, out GrayScaleImage image);
        return image;
    }

    // 检查是否已加载某灰度图
    public bool HasMap(MapType mapType)
    {
        return loadedMaps.ContainsKey(mapType);
    }

    // 获取所有已加载的灰度图
    public Dictionary<MapType, GrayScaleImage> GetAllMaps()
    {
        return new Dictionary<MapType, GrayScaleImage>(loadedMaps);
    }

    // 打印统计信息
    public void PrintStats()
    {
        foreach (var pair in loadedMaps)
        {
            Debug.Log($"{pair.Key}: {pair.Value.Width}x{pair.Value.Height}");
        }
    }
}


public class TerrainConfigReader : MonoBehaviour
{
    public string configFilePath = "map/terrain.cfg";

    private Dictionary<string, string> configData = new Dictionary<string, string>();

    void Start()
    {
        //LoadTerrainConfig();
    }

    public void LoadTerrainConfig()
    {
        string fullPath = Path.Combine(Application.dataPath, configFilePath);

        if (!File.Exists(fullPath))
        {
            Debug.LogError("配置文件不存在: " + fullPath);
            return;
        }

        try
        {
            string[] lines = File.ReadAllLines(fullPath);

            foreach (string line in lines)
            {
                if (string.IsNullOrEmpty(line) || line.StartsWith("//") || !line.Contains("="))
                    continue;

                string[] parts = line.Split('=');
                if (parts.Length == 2)
                {
                    string key = parts[0].Trim();
                    string value = parts[1].Trim();
                    configData[key] = value;
                }
            }

            Debug.Log("地形配置加载完成，共 " + configData.Count + " 个参数");
        }
        catch (System.Exception e)
        {
            Debug.LogError("读取配置文件失败: " + e.Message);
        }
    }

    // 获取配置值的方法
    public string GetValue(string key)
    {
        return configData.ContainsKey(key) ? configData[key] : null;
    }

    public int GetInt(string key, int defaultValue = 0)
    {
        return configData.ContainsKey(key) && int.TryParse(configData[key], out int result) ? result : defaultValue;
    }

    public float GetFloat(string key, float defaultValue = 0f)
    {
        return configData.ContainsKey(key) && float.TryParse(configData[key], out float result) ? result : defaultValue;
    }

    public bool GetBool(string key, bool defaultValue = false)
    {
        if (!configData.ContainsKey(key)) return defaultValue;

        string value = configData[key].ToLower();
        return value == "yes" || value == "true" || value == "1";
    }

    // 使用示例
   public void PrintConfigValues()
    {
        Debug.Log($"DetailTile: {GetInt("DetailTile")}");
        Debug.Log($"PageSource: {GetValue("PageSource")}");
        Debug.Log($"Heightmap: {GetValue("Heightmap.image")}");
        Debug.Log($"PageSize: {GetInt("PageSize")}");
        Debug.Log($"MaxHeight: {GetFloat("MaxHeight")}");
        Debug.Log($"VertexNormals: {GetBool("VertexNormals")}");
    }
}