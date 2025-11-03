using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;

public class MapImporter : MonoBehaviour
{
    public enum MapType
    {
        heightmap,
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

    public void ImportAllMaps()
    {
        foreach (MapType mapType in System.Enum.GetValues(typeof(MapType)))
        {
            string fileName = filePrefix + mapType.ToString().ToLower() + ".png";
            string filePath = Path.Combine(Application.persistentDataPath, basePath, fileName);

            if (File.Exists(filePath))
            {
                GrayScaleImage grayImage = LoadGrayScaleImage(filePath, mapType);
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

    private GrayScaleImage LoadGrayScaleImage(string filePath, MapType mapType)
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
            Debug.LogError($"加载失败 {filePath}: {e.Message}");
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