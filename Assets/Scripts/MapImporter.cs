using Palmmedia.ReportGenerator.Core.Reporting.Builders.Rendering;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel.Design.Serialization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.LowLevel;
using static MapImporter;
using static UnityEditor.PlayerSettings;


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

    public GameObject BuildingPref;
    public GameObject PlatformPref;

    void Start()
    {
        ImportAllMaps();
        Debug.Log("MapInporter Init");
    }

    public void importTerrain()
    {
        TerrainConfigReader tcr = new TerrainConfigReader();
        tcr.LoadTerrainConfig();
        tcr.PrintConfigValues();
        string mapName = "terrain5_heightmap.png";
        float maxHeight = 25.0f;
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
            maxHeight = tcr.GetFloat("MaxHeight");
        }
   
        GrayScaleImage grayImage = LoadGrayScaleImage(Path.Combine(Application.dataPath, basePath, mapName));
        gameObject.GetComponent<MetaMap>().m_metaTerrain.setData(grayImage);
        gameObject.GetComponent<MetaMap>().m_metaTerrain.maxHeight = maxHeight;
        Debug.Log($"set Height {maxHeight}");
        gameObject.GetComponent<MetaMap>().m_metaTerrain.fileName = mapName;

    }

    public void importObjects()
    {
        Debug.Log("start to Import Objects");
        XmlDocument xmlDoc = new XmlDocument();
        string xmlPath = Application.dataPath + "/map/"+ "objects.svg";
        Debug.Log("LoadSvg at "+xmlPath);
        xmlDoc.Load(xmlPath);
        
        XmlElement root = xmlDoc.DocumentElement;
        foreach (XmlNode node in root.ChildNodes)
        {
            if(node.Name=="g")
            {
                if (node is XmlElement ele)
                {
                    Debug.Log(ele.GetAttribute("inkscape:label"));
                    if(ele.GetAttribute("inkscape:label").StartsWith("layer"))
                    {
                        string lnm = ele.GetAttribute("inkscape:label");


                        int number = 0;

                        // 使用正则表达式匹配 "layer" 后面跟着纯数字的情况
                        Regex regex = new Regex(@"^layer(\d+)$");
                        Match match = regex.Match(lnm);

                        if (match.Success)
                        {
                            string numberString = match.Groups[1].Value;
                            int.TryParse(numberString, out number);
                        }
                        else
                        {
                            Debug.Log("MapImporter: 混合图层：" + lnm);
                            continue;
                        }
                        Debug.Log("MapImporter: 标准图层：" + lnm);


                        foreach (XmlNode snode in node.ChildNodes)
                        {
                            if (snode.Name == "g")
                            {
                                if (snode is XmlElement sele)
                                {
                                    if (true)//TODO
                                    {
                                        foreach (XmlNode r in snode.ChildNodes)
                                        {
                                            if (r.Name == "rect")
                                            {
                                                if (r is XmlElement bRect)
                                                {
                                                    if (bRect.GetAttribute("id").StartsWith("buildingrect"))
                                                    {
                                                        float cWidth = float.Parse(bRect.GetAttribute("width"));
                                                        float cHeight = float.Parse(bRect.GetAttribute("height"));
                                                        float cX = float.Parse(bRect.GetAttribute("x"));
                                                        float cY = float.Parse(bRect.GetAttribute("y"));
                                                        string trans = bRect.GetAttribute("transform");
                                                        string cleanString = trans.Replace("matrix(", "").Replace(")", "");
                                                        string[] parts = cleanString.Split(',');
                                                        float a = float.Parse(parts[0]);
                                                        float b = float.Parse(parts[1]);
                                                        float c = float.Parse(parts[2]);
                                                        float d = float.Parse(parts[3]);
                                                        float tx = float.Parse(parts[4]);
                                                        float ty = float.Parse(parts[5]);
                                                        float radians = Mathf.Atan2(c, a);
                                                        float angle = radians * Mathf.Rad2Deg;
                                                        Matrix2x2 ms = new Matrix2x2(a, c, b, d);
                                                        Vector2 position = new Vector2(cX, cY);
                                                        position = ms * position;
                                                        position = position + new Vector2(tx, ty);
                                                        int BheightF = 0;
                                                        string bmaterial = "";
                                                        foreach (XmlNode de in r.ChildNodes)
                                                        {
                                                            var properties = de.InnerText.Split(';')
                                                                .Where(p => p.Contains('='))
                                                                .Select(p => p.Split('=', 2))
                                                                .ToDictionary(k => k[0].Trim(), v => v[1].Trim());
                                                            if (properties.ContainsKey("height"))
                                                            {
                                                                BheightF = int.Parse(properties["height"]);
                                                            }
                                                            else
                                                            {
                                                                //Debug.Log(bRect.OuterXml);
                                                            }
                                                            if (properties.ContainsKey("material"))
                                                            {
                                                                bmaterial = properties["material"];
                                                            }
                                                            else
                                                            {

                                                            }

                                                        }
                                                        GameObject go = Instantiate(BuildingPref);
                                                        Building gc = go.GetComponent<Building>();
                                                        //Building gc = new Building(BheightF, bmaterial, MathOfRwrme.SvgPosToU3dPos(position), angle, new Vector2(cWidth / 2, cHeight / 2), MetaMap.instance.getNewItemId("buildingrect"), number);
                                                        gc.reinit(BheightF, bmaterial, MathOfRwrme.SvgPosToU3dPos(position), angle, new Vector2(cWidth / 2, cHeight / 2), MetaMap.instance.getNewItemId("buildingrect"), number);
                                                        MetaMap.instance.defaultLayer.mapItems.Add(gc);
                                                    }

                                                }
                                            }
                                            if (r.Name == "g")
                                            {
                                                //List<XmlNode> pnl = r.ChildNodes;
                                                List<XmlNode> pnl = new List<XmlNode>();
                                                XmlNodeList pnlls = r.ChildNodes;
                                                foreach (XmlNode pnlli in pnlls)
                                                {
                                                    pnl.Add(pnlli);
                                                }



                                                if (pnl.Count == 2)
                                                {
                                                    string id2 = pnl[1].Attributes["id"].Value;
                                                    if(id2.StartsWith("platform"))
                                                    {
                                                        pnl.Insert(0, pnl[1]);
                                                        pnl.RemoveAt(2);
                                                    }
                                                    string id1 = pnl[0].Attributes["id"].Value;
                                                    if (id1.StartsWith("platform"))
                                                    {
                                                        //Debug.Log("MapImporter:getA platform");

                                                        GameObject go = Instantiate(PlatformPref);
                                                        Platform pf = go.GetComponent<Platform>();
                                                        MetaMap.instance.defaultLayer.mapItems.Add(pf);


                                                        pf.id = MetaMap.instance.getNewItemId("platform");
                                                        
                                                        string pathData1 = pnl[0].Attributes["d"].Value;
                                                        pf.positinLineL = pf.ParsePathData(pathData1);
                                                        for(int i=0;i<pf.positinLineL.Count;i++)
                                                        {
                                                            pf.positinLineL[i] = MathOfRwrme.SvgPosToU3dPos(pf.positinLineL[i]); 
                                                        }

                                                        string pathData2 = pnl[1].Attributes["d"].Value;
                                                        pf.positinLineR = pf.ParsePathData(pathData2);
                                                        for (int i = 0; i < pf.positinLineR.Count; i++)
                                                        {
                                                            pf.positinLineR[i] = MathOfRwrme.SvgPosToU3dPos(pf.positinLineR[i]);
                                                        }

                                                        pf.layerIndex = number;

                                                        XmlNode descNode = pnl[0].FirstChild;
                                                        Debug.Log("MapImporter: " + descNode.InnerText);
                                                        var properties = descNode.InnerText.Split(';')
                                                            .Where(p => p.Contains('='))
                                                            .Select(p => p.Split('=', 2))
                                                            .GroupBy(k => k[0].Trim(), v => v[1].Trim())
                                                            .ToDictionary(g => g.Key, g => g.First());

                                                        if (properties.ContainsKey("base_wall_template")) pf.base_wall_template = properties["base_wall_template"];
                                                        if (properties.ContainsKey("top_material")) pf.top_material = properties["top_material"];
                                                        if (properties.ContainsKey("wall_height")) pf.wall_height =  float.Parse(properties["wall_height"]);

                                                    }
                                                }
                                            }
                                        }
                                    }
                                }

                            }
                        }
                    }
                }
            }
        }
    }

    public void ImportAllMaps()
    {
        importObjects();
        importTerrain();
        foreach (MapType mapType in System.Enum.GetValues(typeof(MapType)))
        {
            string fileName = filePrefix + mapType.ToString().ToLower() + ".png";
            string filePath = Path.Combine(Application.dataPath, basePath, fileName);

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


public class TerrainConfigReader
{
    public string configFilePath = "map/terrain.cfg";

    private Dictionary<string, string> configData = new Dictionary<string, string>();

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