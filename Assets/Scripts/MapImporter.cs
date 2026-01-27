using Palmmedia.ReportGenerator.Core.Reporting.Builders.Rendering;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel.Design.Serialization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;
using Unity.VisualScripting;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.LowLevel;
using UnityEngine.UIElements;
using static MapImporter;
using static UnityEditor.Experimental.GraphView.GraphView;
using static UnityEditor.PlayerSettings;


public class MapImporter : MonoBehaviour
{
    public enum MapType
    {
        alpha_sand
    }

    [Header("��������")]
    public string basePath = "map";
    public string filePrefix = "terrain5_";

    private Dictionary<MapType, GrayScaleImage> loadedMaps = new Dictionary<MapType, GrayScaleImage>();

    public static MapImporter instate;

    public GameObject BuildingPref;
    public GameObject PlatformPref;
    public GameObject WallPref;
    public GameObject SubWallPref;
    public GameObject BasePref;

    void Start()
    {
        instate = this;
        ImportAllMaps();
        Debug.Log("MapInporter Init");
    }

    public void importTerrain()
    {
        TerrainConfigReader tcr = new TerrainConfigReader();
        tcr.configFilePath = Path.Combine(basePath, "terrain.cfg");
        tcr.LoadTerrainConfig();
        tcr.PrintConfigValues();
        string mapName = "terrain5_heightmap.png";
        float maxHeight = 25.0f;
        /*
        PageWorldX = 1536
        PageWorldZ = 1536
        MaxHeight = 25
        */
        if (tcr.GetValue("Heightmap.image") != null)
        {
            mapName = tcr.GetValue("Heightmap.image");
            Debug.Log("get heightmap name as " + mapName);
        }
        if (tcr.GetValue("MaxHeight") != null)
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
        string xmlPath = Path.Combine(Application.dataPath, basePath, "objects.svg");
        Debug.Log("LoadSvg at " + xmlPath);
        xmlDoc.Load(xmlPath);

        XmlElement root = xmlDoc.DocumentElement;
        foreach (XmlNode node in root.ChildNodes)
        {
            if (node.Name == "g")
            {
                if (node is XmlElement ele)
                {
                    if (ele.GetAttribute("inkscape:label").StartsWith("bases.default"))
                    {
                        Debug.Log("import bases");
                        foreach (XmlNode baseNode in ele.ChildNodes)
                        {
                            if (baseNode.Name == "rect")
                            {
                                if (baseNode is XmlElement baseXml)
                                {
                                    float cWidth = float.Parse(baseXml.GetAttribute("width"));
                                    float cHeight = float.Parse(baseXml.GetAttribute("height"));
                                    float cX = float.Parse(baseXml.GetAttribute("x"));
                                    float cY = float.Parse(baseXml.GetAttribute("y"));
                                    Vector2 position = new Vector2(cX, cY);

                                    string trans = baseXml.GetAttribute("transform");
                                    float angle = 0;
                                    Matrix2x2 rotM = Matrix2x2.CreateRotation(0);
                                    Vector2 offV = Vector2.zero;
                                    Vector2 scale = Vector2.one;
                                    dealWithTransform(trans, ref rotM, ref angle, ref offV,ref scale);

                                    position = rotM * position;
                                    position = position + offV;
                                    position.x = position.x*scale.x;
                                    position.y = position.y*scale.y;
                                    cWidth = cWidth * scale.x;
                                    cHeight = cHeight * scale.y;
                                    if(cWidth<0)
                                    {
                                        position.x += cWidth;
                                        cWidth *= -1;
                                    }
                                    if (cHeight < 0)
                                    {
                                        position.y += cHeight;
                                        cHeight *= -1;
                                    }
                                       

                                    //x+a(k-x)
                                    //(1-a)x+ak

                                    string name="";
                                    int factionIndex=-1;
                                    foreach (XmlNode de in baseNode.ChildNodes)
                                    {
                                        var properties = de.InnerText.Split(';')
                                            .Where(p => p.Contains('='))
                                            .Select(p => p.Split('=', 2))
                                            .ToDictionary(k => k[0].Trim(), v => v[1].Trim());
                                        if (properties.ContainsKey("name"))
                                        {
                                            name = properties["name"];
                                        }
                                        else
                                        {
                                        }
                                        if (properties.ContainsKey("faction_index"))
                                        {
                                            factionIndex = int.Parse( properties["faction_index"]);
                                        }
                                        else
                                        {
                                            factionIndex = -1;
                                        }
                                    }
                                    GameObject go = Instantiate(BasePref);
                                    Base gc = go.GetComponent<Base>();
                                    gc._name = name;
                                    gc.factionIndex = factionIndex;
                                    gc.id = MetaMap.instance.getNewItemId("base");
                                    gc.position = position;
                                    gc.size = new Vector2(cWidth, cHeight);
                                    MetaMap.instance.baseLayer.mapItems.Add(gc);
                                }
                            }
                        }
                    }
                    if (ele.GetAttribute("inkscape:label").StartsWith("layer"))
                    {
                        string lnm = ele.GetAttribute("inkscape:label");
                        int number = 0;

                        number = dealWithLayerLabel(lnm);
                        if (number == -1) continue;

                        Debug.Log("mapImporter start import items at layer " + number.ToString());

                        float raLayer = 0;
                        Matrix2x2 rmLayer = Matrix2x2.CreateRotation(0);
                        Vector2 ovLayer = Vector2.zero;

                        if (ele.HasAttribute("transform"))
                        {
                            dealWithTransform(ele.GetAttribute("transform"), ref rmLayer, ref raLayer, ref ovLayer);
                        }

                        foreach (XmlNode snode in node.ChildNodes)
                        {
                            if (snode.Name == "g")
                            {
                                if (snode is XmlElement sele)
                                {
                                    float raGroup = 0;
                                    Matrix2x2 rmGroup = Matrix2x2.CreateRotation(0);
                                    Vector2 ovGroup = Vector2.zero;

                                    if (sele.HasAttribute("transform"))
                                    {
                                        Debug.Log("MapImporter : group has transform :" + sele.GetAttribute("transform"));
                                        dealWithTransform(sele.GetAttribute("transform"), ref rmGroup, ref raGroup, ref ovGroup);
                                    }

                                    foreach (XmlNode r in snode.ChildNodes)
                                    {
                                        if (r.Name == "rect")
                                        {
                                            if (r is XmlElement bRect)
                                            {
                                                if (bRect.GetAttribute("id").StartsWith("building"))
                                                {
                                                    float cWidth = float.Parse(bRect.GetAttribute("width"));
                                                    float cHeight = float.Parse(bRect.GetAttribute("height"));
                                                    float cX = float.Parse(bRect.GetAttribute("x"));
                                                    float cY = float.Parse(bRect.GetAttribute("y"));
                                                    Vector2 position = new Vector2(cX, cY);

                                                    string trans = bRect.GetAttribute("transform");
                                                    float angle = 0;
                                                    Matrix2x2 rotM = Matrix2x2.CreateRotation(0);
                                                    Vector2 offV = Vector2.zero;

                                                    dealWithTransform(trans, ref rotM, ref angle, ref offV);

                                                    position = rotM * position;
                                                    position = position + offV;

                                                    angle += raGroup;
                                                    position = rmGroup * position;
                                                    position += ovGroup;

                                                    angle += raLayer;
                                                    position = rmLayer * position;
                                                    position += ovLayer;

                                                    int BheightF = 0;
                                                    string bmaterial = "";
                                                    bool roof = false;
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
                                                        if (properties.ContainsKey("roof_type"))
                                                        {

                                                            roof = (properties["roof_type"] == "elevated");
                                                        }
                                                        else if (BheightF == 2)
                                                        {
                                                            roof = true;
                                                        }

                                                    }
                                                    GameObject go = Instantiate(BuildingPref);
                                                    Building gc = go.GetComponent<Building>();
                                                    //if (number == 1) Debug.Log("i got this ");
                                                    gc.reinit(BheightF, bmaterial, position, angle, new Vector2(cWidth, cHeight), MetaMap.instance.getNewItemId("building"), number);
                                                    gc.roof = roof;
                                                    MetaMap.instance.defaultLayer.mapItems.Add(gc);
                                                }

                                            }
                                        }
                                        if (r.Name == "g")
                                        {

                                            float raPair = 0;
                                            Matrix2x2 rmPair = Matrix2x2.CreateRotation(0);
                                            Vector2 ovPair = Vector2.zero;
                                            if (r is XmlElement pairEle)
                                            {
                                                if (pairEle.HasAttribute("transform"))
                                                {
                                                    dealWithTransform(pairEle.GetAttribute("transform"), ref rmPair, ref raPair, ref ovPair);
                                                }
                                            }
                                            //import platform
                                            //List<XmlNode> pnl = r.ChildNodes;
                                            List<XmlNode> pnl = new List<XmlNode>();
                                            XmlNodeList pnlls = r.ChildNodes;
                                            foreach (XmlNode pnlli in pnlls)
                                            {
                                                pnl.Add(pnlli);
                                            }



                                            if (pnl.Count == 2)
                                            {

                                                XmlNode descNode = pnl[0].FirstChild;
                                                var properties = descNode.InnerText.Split(';')
                                                    .Where(p => p.Contains('='))
                                                    .Select(p => p.Split('=', 2))
                                                    .GroupBy(k => k[0].Trim(), v => v[1].Trim())
                                                    .ToDictionary(g => g.Key, g => g.First());
                                                if (properties.ContainsKey("type"))
                                                {
                                                    if (properties["type"] == "start" || properties["type"] == "deck_start")
                                                    {
                                                        pnl.Insert(0, pnl[1]);
                                                        pnl.RemoveAt(2);
                                                        descNode = pnl[0].FirstChild;
                                                        properties = descNode.InnerText.Split(';')
                                                            .Where(p => p.Contains('='))
                                                            .Select(p => p.Split('=', 2))
                                                            .GroupBy(k => k[0].Trim(), v => v[1].Trim())
                                                            .ToDictionary(g => g.Key, g => g.First());
                                                    }
                                                }

                                                GameObject go = Instantiate(PlatformPref);
                                                Platform pf = go.GetComponent<Platform>();
                                                MetaMap.instance.defaultLayer.mapItems.Add(pf);

                                                pf.id = MetaMap.instance.getNewItemId("platform");

                                                string pathData1 = pnl[0].Attributes["d"].Value;
                                                pf.positinLineL = pf.ParsePathData(pathData1);
                                                for (int i = 0; i < pf.positinLineL.Count; i++)
                                                {
                                                    pf.positinLineL[i] = rmPair * pf.positinLineL[i];
                                                    pf.positinLineL[i] += ovPair;
                                                    pf.positinLineL[i] = rmGroup * pf.positinLineL[i];
                                                    pf.positinLineL[i] += ovGroup;
                                                    pf.positinLineL[i] = rmLayer * pf.positinLineL[i];
                                                    pf.positinLineL[i] += ovLayer;
                                                }

                                                string pathData2 = pnl[1].Attributes["d"].Value;
                                                pf.positinLineR = pf.ParsePathData(pathData2);
                                                for (int i = 0; i < pf.positinLineR.Count; i++)
                                                {
                                                    pf.positinLineR[i] = rmPair * pf.positinLineR[i];
                                                    pf.positinLineR[i] += ovPair;
                                                    pf.positinLineR[i] = rmGroup * pf.positinLineR[i];
                                                    pf.positinLineR[i] += ovGroup;
                                                    pf.positinLineR[i] = rmLayer * pf.positinLineR[i];
                                                    pf.positinLineR[i] += ovLayer;

                                                }

                                                pf.layerIndex = number;

                                                if (properties.ContainsKey("type"))
                                                {
                                                    if (properties["type"].StartsWith("deck"))
                                                    {
                                                        pf.isDeck = true;
                                                    }
                                                }
                                                if (properties.ContainsKey("mode"))
                                                {
                                                    if (properties["mode"].StartsWith("bridge"))
                                                    {
                                                        pf.isDeck = true;

                                                    }
                                                }
                                                if (properties.ContainsKey("base_wall_template"))
                                                { pf.base_wall_template = properties["base_wall_template"]; }
                                                else
                                                { pf.base_wall_template = "StoneWall1"; }
                                                if (properties.ContainsKey("top_material")) pf.top_material = properties["top_material"];
                                                if (properties.ContainsKey("wall_height")) { pf.wall_height = float.Parse(properties["wall_height"]); }
                                                else
                                                {
                                                    pf.wall_height = -1f;
                                                }
                                                if (properties.ContainsKey("height")) pf.height = float.Parse(properties["height"]);
                                                if (properties.ContainsKey("wall_template")) pf.wall_template = properties["wall_template"];

                                            }
                                        }
                                        if (r.Name == "path")
                                        {
                                            if (r is XmlElement bPath)
                                            {
                                                float raPath = 0;
                                                Matrix2x2 rmPath = Matrix2x2.CreateRotation(0);
                                                Vector2 ovPath = Vector2.zero;
                                                if (bPath.HasAttribute("transform"))
                                                {
                                                    dealWithTransform(bPath.GetAttribute("transform"), ref rmPath, ref raPath, ref ovPath);
                                                }
                                                if (bPath.GetAttribute("id").StartsWith("wall"))
                                                {

                                                    GameObject go = Instantiate(WallPref);
                                                    Wall gs = go.GetComponent<Wall>();
                                                    //gs.SubWallPref = SubWallPref;//DONE!
                                                    string pathData1 = bPath.Attributes["d"].Value;
                                                    gs.positionLine = gs.ParsePathData(pathData1);

                                                    for (int i = 0; i < gs.positionLine.Count; i++)
                                                    {
                                                        gs.positionLine[i] = rmPath * gs.positionLine[i];
                                                        gs.positionLine[i] += ovPath;
                                                        gs.positionLine[i] = rmGroup * gs.positionLine[i];
                                                        gs.positionLine[i] += ovGroup;
                                                        gs.positionLine[i] = rmLayer * gs.positionLine[i];
                                                        gs.positionLine[i] += ovLayer;

                                                    }

                                                    XmlNode descNode = bPath.FirstChild;
                                                    var properties = descNode.InnerText.Split(';')
                                                        .Where(p => p.Contains('='))
                                                        .Select(p => p.Split('=', 2))
                                                        .GroupBy(k => k[0].Trim(), v => v[1].Trim())
                                                        .ToDictionary(g => g.Key, g => g.First());
                                                    if (properties.ContainsKey("template")) gs.material = properties["template"];
                                                    MetaMap.instance.defaultLayer.mapItems.Add(gs);
                                                    gs.id = MetaMap.instance.getNewItemId("wall");
                                                    gs.layerIndex = number;
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

        //CtrlZer.instance.checkPoint();
    }

    public int dealWithLayerLabel(string label)
    {
        if (!label.StartsWith("layer")) return -1;
        if (label == "layer") return 1;
        string lnm = label;
        int number = -1;
        Regex regex = new Regex(@"^layer(\d+)(?:\.([a-zA-Z]+))?$");
        Match match = regex.Match(lnm);
        if (match.Success)
        {
            string numberString = match.Groups[1].Value;
            string extension = match.Groups[2].Success ? match.Groups[2].Value : "default";

            if (!MetaMap.instance.allowedExtensions.Contains(extension))
            {
                return -1;
            }

            if (!int.TryParse(numberString, out number))
            {
                return -1;
            }

            return number;
        }
        else
        {
            return -1;
        }
    }
    public void dealWithTransform(string trs, ref Matrix2x2 rotate, ref float angle, ref Vector2 offset,ref Vector2 scale)
    {
        rotate = Matrix2x2.CreateRotation(0);
        //identitilyeze
        offset = Vector2.zero;
        //zero offset
        angle = 0;
        //zero rotate
        scale = Vector2.one;

        //translate matrix rotate
        if (trs.StartsWith("translate"))
        {
            string cleanString = trs.Replace("translate(", "").Replace(")", "");
            string[] parts = cleanString.Split(',');
            float x = float.Parse(parts[0]);
            float y = float.Parse(parts[1]);
            offset = new Vector2(x, y);
        }
        else if (trs.StartsWith("matrix"))
        {
            string cleanString = trs.Replace("matrix(", "").Replace(")", "");
            //Debug.Log("MapImporter.cs" + cleanString);
            string[] parts = cleanString.Split(',');
            float a = float.Parse(parts[0]);
            float b = float.Parse(parts[1]);
            float c = float.Parse(parts[2]);
            float d = float.Parse(parts[3]);
            float tx = float.Parse(parts[4]);
            float ty = float.Parse(parts[5]);
            float radians = Mathf.Atan2(c, a);
            angle = radians * Mathf.Rad2Deg;
            rotate = new Matrix2x2(a, c, b, d);
            offset = new Vector2(tx, ty);
        }
        else if (trs.StartsWith("rotate"))
        {
            string cleanString = trs.Replace("rotate(", "").Replace(")", "");
            angle = float.Parse(cleanString);
            rotate = Matrix2x2.CreateRotation(angle);
        }
        else if(trs.StartsWith("scale"))
        {
            string cleanString = trs.Replace("scale(", "").Replace(")", "");
            string[] parts = cleanString.Split(',');
            float x = float.Parse(parts[0]);
            float y = float.Parse(parts[1]);
            scale = new Vector2(x, y);
        }
        else
        {
            Debug.LogError("com not found:" + trs);
        }

    }
    public void dealWithTransform(string trs, ref Matrix2x2 rotate, ref float angle, ref Vector2 offset)
    {
        Vector2 scale=Vector2.one;
        dealWithTransform(trs, ref rotate, ref angle, ref offset, ref scale);
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
                    Debug.Log($"�Ѽ���: {fileName} ({grayImage.Width}x{grayImage.Height})");
                }
            }
            else
            {
                Debug.LogWarning($"�ļ�������: {filePath}");
            }
        }

        Debug.Log($"������ɣ������� {loadedMaps.Count} �ŻҶ�ͼ");
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
            Debug.LogError($"����ʧ��: {e.Message}");
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
                float grayValue = pixels[index].grayscale;
                grayImage[y, x] = grayValue;
            }
        }

        DestroyImmediate(texture);

        return grayImage;
    }

    public GrayScaleImage GetGrayScaleImage(MapType mapType)
    {
        loadedMaps.TryGetValue(mapType, out GrayScaleImage image);
        return image;
    }

    public bool HasMap(MapType mapType)
    {
        return loadedMaps.ContainsKey(mapType);
    }

    public Dictionary<MapType, GrayScaleImage> GetAllMaps()
    {
        return new Dictionary<MapType, GrayScaleImage>(loadedMaps);
    }

    // ��ӡͳ����Ϣ
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

        }
        catch (System.Exception e)
        {
        }
    }

    // ��ȡ����ֵ�ķ���
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

    // ʹ��ʾ��
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