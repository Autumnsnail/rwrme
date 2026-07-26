
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;
using UnityEngine;


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

    // 防御性解析 "x y z"：用空白分隔（含 tab/换行），跳过空串，区域不变（Invariant）；不抛异常。
    static readonly char[] s_vec3Separators = new[] { ' ', '\t', '\n', '\r' };
    static bool TryParseVector3(string s, out Vector3 v)
    {
        v = Vector3.zero;
        if (string.IsNullOrWhiteSpace(s)) return false;
        string[] parts = s.Split(s_vec3Separators, System.StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 3) return false;
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        var st = System.Globalization.NumberStyles.Float;
        if (!float.TryParse(parts[0], st, inv, out float x)) return false;
        if (!float.TryParse(parts[1], st, inv, out float y)) return false;
        if (!float.TryParse(parts[2], st, inv, out float z)) return false;
        v = new Vector3(x, y, z);
        return true;
    }

    [HideInInspector] public float pageWorldX = 1024f;
    [HideInInspector] public float pageWorldZ = 1024f;
    [HideInInspector] public float svgCanvasSize = 2048f;

    public GameObject BuildingPref;
    public GameObject PlatformPref;
    public GameObject WallPref;
    public GameObject SubWallPref;
    public GameObject BasePref;
    public GameObject SpawnPointPref;
    public GameObject RockPref;
    public GameObject MeshPref;
    public GameObject DecalPref;
    public GameObject LadderPref;
    public GameObject VehiclePref;
    public GameObject ItemSupplyPref;
    public GameObject CratePref;
    public GameObject OffroadPref;
    public GameObject PostPref;
    public GameObject TreePref;

    public Material cbdTl;


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
        if (tcr.GetValue("PageWorldX") != null)
            pageWorldX = tcr.GetFloat("PageWorldX");
        if (tcr.GetValue("PageWorldZ") != null)
            pageWorldZ = tcr.GetFloat("PageWorldZ");

        // Read SVG canvas size for coordinate mapping
        string svgPath = Path.Combine(Application.dataPath, basePath, "objects.svg");
        if (File.Exists(svgPath))
        {
            XmlDocument svgDoc = new XmlDocument();
            svgDoc.Load(svgPath);
            XmlElement svgRoot = svgDoc.DocumentElement;
            if (svgRoot.HasAttribute("width"))
            {
                float.TryParse(svgRoot.GetAttribute("width"), out svgCanvasSize);
            }
        }

        GrayScaleImage grayImage = LoadGrayScaleImage(Path.Combine(Application.dataPath, basePath, mapName));
        gameObject.GetComponent<MetaMap>().m_metaTerrain.setData(grayImage);
        gameObject.GetComponent<MetaMap>().m_metaTerrain.maxHeight = maxHeight;
        Debug.Log($"set Height {maxHeight}");
        gameObject.GetComponent<MetaMap>().m_metaTerrain.fileName = mapName;

    }

    /// <summary>
    /// 读取 <c>map_config.xml</c>，写入 <see cref="MetaMap.m_metaMapConfig"/>（与 <see cref="importTerrain"/> 相同从 <c>basePath</c> 取路径）。
    /// </summary>
    public void importMapConfig()
    {
        string xmlPath = Path.Combine(Application.dataPath, basePath, "map_config.xml");
        if (!File.Exists(xmlPath))
        {
            Debug.LogWarning("MapImporter: map_config.xml 不存在，跳过: " + xmlPath);
            return;
        }

        MetaMap mm = gameObject.GetComponent<MetaMap>();
        if (mm == null)
        {
            Debug.LogError("MapImporter: 未找到 MetaMap 组件");
            return;
        }
        if (mm.m_metaMapConfig == null)
            mm.m_metaMapConfig = new MetaMapConfig();

        MetaMapConfig cfg = mm.m_metaMapConfig;
        cfg.factionFiles.Clear();

        try
        {
            XmlDocument doc = new XmlDocument();
            doc.Load(xmlPath);
            XmlElement root = doc.DocumentElement;
            if (root == null || root.LocalName != "map_config")
            {
                Debug.LogError("MapImporter: map_config.xml 根节点应为 map_config");
                return;
            }

            if (root.HasAttribute("min_factions"))
                int.TryParse(root.GetAttribute("min_factions"), out cfg.minFactions);
            if (root.HasAttribute("max_factions"))
                int.TryParse(root.GetAttribute("max_factions"), out cfg.maxFactions);
            if (root.HasAttribute("add_neutral_last"))
                int.TryParse(root.GetAttribute("add_neutral_last"), out cfg.addNeutralLast);

            foreach (XmlNode node in root.ChildNodes)
            {
                if (node.NodeType != XmlNodeType.Element) continue;
                if (!(node is XmlElement el)) continue;
                string fileAttr = el.GetAttribute("file");
                switch (el.LocalName)
                {
                    case "faction":
                        if (!string.IsNullOrEmpty(fileAttr))
                            cfg.factionFiles.Add(fileAttr);
                        break;
                    case "include_layer":
                        if (!string.IsNullOrEmpty(fileAttr))
                            cfg.includeLayers.Add(fileAttr);
                        break;
                    case "weapon":
                        cfg.weaponFile = fileAttr;
                        break;
                    case "projectile":
                        cfg.projectileFile = fileAttr;
                        break;
                    case "call":
                        cfg.callFile = fileAttr;
                        break;
                    case "carry_item":
                        cfg.carryItemFile = fileAttr;
                        break;
                    case "vehicle":
                        cfg.vehicleFile = fileAttr;
                        break;
                }
            }

            Debug.Log($"MapImporter: importMapConfig 完成 factions={cfg.factionFiles.Count} weapon={cfg.weaponFile}");
        }
        catch (Exception ex)
        {
            Debug.LogError("MapImporter: importMapConfig 失败: " + ex.Message);
        }
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
                    if (ele.GetAttribute("inkscape:label").StartsWith("bases.default") ||ele.GetAttribute("inkscape:label")=="bases")//default bases but bases.default used more frequently
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
                                    position = position*scale;
                                    position = position + offV;
                                    
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
                                    gc.rotation = angle;
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
                                    float _raGroup = 0;
                                    Matrix2x2 _rmGroup = Matrix2x2.CreateRotation(0);
                                    Vector2 _ovGroup = Vector2.zero;
                                    Vector2 _scGroup = Vector2.one;

                                    if (sele.HasAttribute("transform"))
                                    {
                                        Debug.Log("MapImporter : group has transform :" + sele.GetAttribute("transform"));
                                        dealWithTransform(sele.GetAttribute("transform"), ref _rmGroup, ref _raGroup, ref _ovGroup,ref _scGroup);
                                    }

                                    foreach (XmlNode r in snode.ChildNodes)
                                    {
                                        if (r.Name == "rect")
                                        {
                                            if (r is XmlElement bRect)
                                            {
                                                float cX = float.Parse(bRect.GetAttribute("x"));
                                                float cY = float.Parse(bRect.GetAttribute("y"));
                                                Vector2 position = new Vector2(cX, cY);
                                                string trans = bRect.GetAttribute("transform");
                                                float angle = 0;
                                                Matrix2x2 rotM = Matrix2x2.CreateRotation(0);
                                                Vector2 offV = Vector2.zero;
                                                Vector2 scale = Vector2.one;
                                                dealWithTransform(trans, ref rotM, ref angle, ref offV, ref scale);
                                                position = position * scale;
                                                position = rotM * position;
                                                position = position + offV;
                                                angle += _raGroup;
                                                position = position * _scGroup;
                                                position = _rmGroup * position;
                                                position += _ovGroup;
                                                angle += raLayer;
                                                position = rmLayer * position;
                                                position += ovLayer;


                                                if (bRect.GetAttribute("id").StartsWith("building"))
                                                {
                                                    float cWidth = float.Parse(bRect.GetAttribute("width"));
                                                    float cHeight = float.Parse(bRect.GetAttribute("height"));


                                                    int BheightF = 0;
                                                    string bmaterial = "";
                                                    bool roof = false;
                                                    Vector3 offset = Vector3.zero;
                                                    foreach (XmlNode de in r.ChildNodes)
                                                    {
                                                        //Debug.Log("MapImporter : " + de.InnerText);
                                                        // 仅保留 #offset 这类已知"带 # 数据条目"；其它 # 开头一律视为注释
                                                        var properties = de.InnerText.Split(';')
                                                            .Select(p => p.Trim())
                                                            .Where(p => p.Length > 0 && (!p.StartsWith("#") || p.StartsWith("#offset")))
                                                            .Where(p => p.Contains('='))
                                                            .Select(p => p.StartsWith("#offset") ? p.Substring(1) : p)
                                                            .Select(p => p.Split('=', 2))
                                                            .Where(kv => kv.Length == 2 && kv[0].Trim().Length > 0)
                                                            .GroupBy(kv => kv[0].Trim())
                                                            .ToDictionary(g => g.Key, g => g.Last()[1].Trim());
                                                        if (properties.ContainsKey("height"))
                                                        {
                                                            BheightF = Mathf.RoundToInt(float.Parse(properties["height"]));
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
                                                        if(properties.ContainsKey("offset"))
                                                        {
                                                            TryParseVector3(properties["offset"], out offset);
                                                        }

                                                    }
                                                    GameObject go = Instantiate(BuildingPref);
                                                    Building gc = go.GetComponent<Building>();
                                                    gc.offset = offset;
                                                    //if (number == 1) Debug.Log("i got this ");
                                                    gc.reinit(BheightF, bmaterial, position, angle, new Vector2(cWidth, cHeight), MetaMap.instance.getNewItemId("building"), number);
                                                    gc.roof = roof;
                                                    MetaMap.instance.defaultLayer.mapItems.Add(gc);
                                                    continue;
                                                }
                                                if (bRect.GetAttribute("id").StartsWith("item_supply"))
                                                {
                                                    float cWidth = float.Parse(bRect.GetAttribute("width"));
                                                    float cHeight = float.Parse(bRect.GetAttribute("height"));

                                                    foreach (XmlNode de in r.ChildNodes)
                                                    {
                                                        if (de.Name != "desc") continue;
                                                        var properties = de.InnerText.Split(';')
                                                            .Select(p => p.Trim())
                                                            .Where(p => p.Length > 0 && !p.StartsWith("#"))
                                                            .Where(p => p.Contains('='))
                                                            .Select(p => p.Split('=', 2))
                                                            .Where(kv => kv.Length == 2 && kv[0].Trim().Length > 0)
                                                            .ToDictionary(kv => kv[0].Trim(), kv => kv[1].Trim());
                                                        if(properties.ContainsKey("type"))
                                                        {
                                                            ItemSupply stspl = Instantiate(ItemSupplyPref).GetComponent<ItemSupply>();
                                                            if (properties["type"]=="weapon_rack")
                                                            {
                                                                stspl.type = 1;
                                                            }
                                                            if (properties["type"] == "stash")
                                                            {
                                                                stspl.type = 0;
                                                            }
                                                            stspl.position = position;
                                                            stspl.id = MetaMap.instance.getNewItemId("item_supply");
                                                            stspl.size = new Vector2(cWidth, cHeight);
                                                            stspl.rotation = angle;
                                                            stspl.layerIndex = number;
                                                            MetaMap.instance.defaultLayer.mapItems.Add(stspl);
                                                        }

                                                    }
                                                    continue;
                                                }
                                                if (bRect.GetAttribute("id").StartsWith("crate"))
                                                {
                                                    float cWidth = float.Parse(bRect.GetAttribute("width"));
                                                    float cHeight = float.Parse(bRect.GetAttribute("height"));

                                                    
                                                            Crate cT = Instantiate(CratePref).GetComponent<Crate>();
                                                        cT.position = position;
                                                        cT.id = MetaMap.instance.getNewItemId("crate");
                                                        cT.size = new Vector2(cWidth, cHeight);
                                                        cT.rotation = angle;
                                                        cT.layerIndex = number;
                                                            MetaMap.instance.defaultLayer.mapItems.Add(cT);
                                                    
                                                    continue;
                                                }
                                                if (bRect.GetAttribute("id").StartsWith("spawn"))
                                                {
                                                    XmlElement rdsc = bRect.FirstChild as XmlElement;
                                                    if (rdsc == null) continue;
//                                                    Debug.Log("MapImporter : spawn has properties :" + rdsc.InnerText);
                                                    var properties = rdsc.InnerText.Split(';')
                                                        .Where(p => p.Contains('='))
                                                        .Select(p => p.Split('=', 2))
                                                        .GroupBy(kv => kv[0].Trim())
                                                        .ToDictionary(
                                                            g => g.Key,
                                                            g => g.Last()[1].Trim()
                                                        );
                                                    if(properties.ContainsKey("type"))
                                                    {
                                                        if (properties["type"]=="vehicle")
                                                        {
                                                            //get a vehicle
                                                            Vehicle vc = Instantiate(VehiclePref).GetComponent<Vehicle>();
                                                            if(properties.ContainsKey("tag"))
                                                            {
                                                                vc.taged = true;
                                                                vc.key = properties["tag"];
                                                            }
                                                            else
                                                            {
                                                                if(!properties.ContainsKey("key"))continue;
                                                                vc.key = properties["key"];
                                                            }
                                                            vc.id = MetaMap.instance.getNewItemId("spawn_vehicle");

                                                            vc.position = position;
                                                            vc.layerIndex = 4;
                                                            vc.rotation = angle;
                                                            
                                                            MetaMap.instance.defaultLayer.mapItems.Add(vc);

                                                        }
                                                    }
                                                    continue;
                                                }
                                                if (bRect.GetAttribute("id").StartsWith("decal"))
                                                {
                                                    
                                                    float cWidth = float.Parse(bRect.GetAttribute("width"));
                                                    float cHeight = float.Parse(bRect.GetAttribute("height"));
                                                    XmlElement rdsc = bRect.FirstChild as XmlElement;
                                                    if (rdsc == null) continue;
                                                    var properties = rdsc.InnerText.Split(';')
                                                        .Where(p => p.Contains('='))
                                                        .Select(p => p.Split('=', 2))
                                                        .GroupBy(kv => kv[0].Trim())
                                                        .ToDictionary(
                                                            g => g.Key,
                                                            g => g.Last()[1].Trim()
                                                        );
                                                    if(properties.ContainsKey("template"))
                                                    {
                                                        Decal decal = Instantiate(DecalPref).GetComponent<Decal>();
                                                        decal.size = new Vector2(cWidth, cHeight);
                                                        decal.length = Mathf.Max(cWidth, cHeight);
                                                        decal.template_ref = properties["template"];
                                                        decal.id = MetaMap.instance.getNewItemId("decal");
                                                        decal.position = position;
                                                        decal.layerIndex = number;
                                                        decal.rotation = angle;
                                                        MetaMap.instance.defaultLayer.mapItems.Add(decal);
                                                        decal.scatterThis();
                                                    }
                                                    continue;
                                                }
                                                
                                                if (bRect.GetAttribute("inkscape:label").StartsWith("#spawnrect"))
                                                {
                                                    if(r.ChildNodes.Count==0)
                                                    {
                                                        //solider SpawnPoint
                                                        SpawnPoint sp = Instantiate(SpawnPointPref).GetComponent<SpawnPoint>();

                                                        sp.id = MetaMap.instance.getNewItemId("#spawnrect");


                                                        sp.position = position;
                                                        sp.size = new Vector2(5, 5);
                                                        sp.layerIndex = 1;

                                                        MetaMap.instance.defaultLayer.mapItems.Add(sp);
                                                    }
                                                    continue;
                                                }
                                                if (bRect.GetAttribute("inkscape:label").StartsWith("rock"))
                                                {
                                                    GameObject go = Instantiate(RockPref);
                                                    Rock rc = go.GetComponent<Rock>();
                                                    rc.rotation = angle;
                                                    rc.position = position;
                                                    rc.id = MetaMap.instance.getNewItemId("#rock");
                                                    rc.layerIndex = number;
                                                    MetaMap.instance.defaultLayer.mapItems.Add(rc);
                                                    rc.scatterThis();
                                                    continue;
                                                }
                                                if (bRect.GetAttribute("inkscape:label").StartsWith("#ladder"))
                                                {

                                                    GameObject ld = Instantiate(LadderPref);
                                                    Ladder ldr = ld.GetComponent<Ladder>();
                                                    ldr.position = position;
                                                    ldr.rotation  = angle;
                                                    ldr.id = MetaMap.instance.getNewItemId("#ladder");
                                                    ldr.layerIndex = number;
                                                    MetaMap.instance.defaultLayer.mapItems.Add(ldr);
                                                    continue;
                                                }
                                                if (bRect.GetAttribute("inkscape:label").StartsWith("#mesh"))
                                                {
                                                    float cWidth = float.Parse(bRect.GetAttribute("width"));
                                                    float cHeight = float.Parse(bRect.GetAttribute("height"));
                                                    XmlNode descnode = null;
                                                    foreach(XmlNode lsnd in bRect.ChildNodes)
                                                    {
                                                        if(lsnd.Name == "desc")
                                                        {
                                                            descnode = lsnd;
                                                            break;
                                                        }
                                                    }
                                                    XmlElement thdesc = descnode as XmlElement;
                                                    if (thdesc == null) continue;
                                                   // Debug.Log("MapImporter : " + thdesc.InnerText);
                                                    // 仅保留 #offset 这类已知"带 # 数据条目"；其它 # 开头一律视为注释
                                                    var properties = thdesc.InnerText.Split(';')
                                                        .Select(p => p.Trim())
                                                        .Where(p => p.Length > 0 && (!p.StartsWith("#") || p.StartsWith("#offset")))
                                                        .Where(p => p.Contains('='))
                                                        .Select(p => p.StartsWith("#offset") ? p.Substring(1) : p)
                                                        .Select(p => p.Split('=', 2))
                                                        .Where(kv => kv.Length == 2 && kv[0].Trim().Length > 0)
                                                        .GroupBy(kv => kv[0].Trim())
                                                        .ToDictionary(
                                                            g => g.Key,
                                                            g => g.Last()[1].Trim()
                                                        );
                                                    if (!properties.ContainsKey("template"))
                                                    {
                                                        continue;
                                                    }
                                                    else
                                                    {
                                                        if (!MetaMap.instance.meshTemplates.Any(template => template.name == properties["template"])) continue;
                                                        GameObject go = Instantiate(MeshPref);
                                                        MeMesh ms = go.GetComponent<MeMesh>();


                                                        if(properties.ContainsKey("offset"))
                                                        {
                                                            if (TryParseVector3(properties["offset"], out Vector3 mo))
                                                                ms.offset = mo;
                                                        }
                                                        if (properties.ContainsKey("collision_model_size")
                                                            && TryParseVector3(properties["collision_model_size"], out Vector3 cs))
                                                        {
                                                            ms.reCollision = true;
                                                            ms.collisionSize = cs;
                                                        }

                                                        ms.position = position;
                                                        ms.rotation = angle;
                                                        ms.id = MetaMap.instance.getNewItemId("#mesh");
                                                        ms.layerIndex = number;
                                                        ms.template_ref = properties["template"];
                                                        ms.size = new Vector2(cWidth, cHeight);
                                                        ms.templated = true;
                                                        MetaMap.instance.defaultLayer.mapItems.Add(ms);
                                                    }
                                                    continue;
                                                }
                                                //if the rect dosent have a child node name desc,it may have other child node,may not, then it is a tree
                                                if (bRect.ChildNodes.Count > 0)
                                                {
                                                    foreach (XmlNode child in bRect.ChildNodes)
                                                    {
                                                        if (child.Name == "desc")
                                                        {
                                                            continue;
                                                        }
                                                    }
                                                }
                                                //finally we got tree:
                                                GameObject tro = Instantiate(TreePref);
                                                MeTree tree = tro.GetComponent<MeTree>();
                                                tree.position = position;
                                                tree.rotation = angle;
                                                tree.id = MetaMap.instance.getNewItemId("tree");
                                                tree.layerIndex = number;
                                                MetaMap.instance.defaultLayer.mapItems.Add(tree);
                                                tree.scatterThis();
                                                continue;


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
                                                pf.positinLineL = SvgPathParser.Parse(pathData1);
                                                for (int i = 0; i < pf.positinLineL.Count; i++)
                                                {
                                                    pf.positinLineL[i] = rmPair * pf.positinLineL[i];
                                                    pf.positinLineL[i] += ovPair;

                                                    pf.positinLineL[i] = _scGroup * pf.positinLineL[i];
                                                    pf.positinLineL[i] = _rmGroup * pf.positinLineL[i];
                                                    pf.positinLineL[i] += _ovGroup;
                                                    pf.positinLineL[i] = rmLayer * pf.positinLineL[i];
                                                    pf.positinLineL[i] += ovLayer;
                                                }

                                                string pathData2 = pnl[1].Attributes["d"].Value;
                                                pf.positinLineR = SvgPathParser.Parse(pathData2);
                                                for (int i = 0; i < pf.positinLineR.Count; i++)
                                                {
                                                    pf.positinLineR[i] = rmPair * pf.positinLineR[i];
                                                    pf.positinLineR[i] += ovPair;

                                                    pf.positinLineR[i] = _scGroup * pf.positinLineR[i];
                                                    pf.positinLineR[i] = _rmGroup * pf.positinLineR[i];
                                                    pf.positinLineR[i] += _ovGroup;
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
                                                        pf.isBridge = true;

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
                                                float _raPath = 0;
                                                Matrix2x2 _rmPath = Matrix2x2.CreateRotation(0);
                                                Vector2 _ovPath = Vector2.zero;
                                                Vector2 _scPath = Vector2.one;
                                                if (bPath.HasAttribute("transform"))
                                                {
                                                    dealWithTransform(bPath.GetAttribute("transform"), ref _rmPath, ref _raPath, ref _ovPath,ref _scPath);
                                                }
                                                if (bPath.GetAttribute("id").StartsWith("wall"))
                                                {
                                                    string pathData1 = bPath.Attributes["d"].Value;
                                                    List<List<Vector2>> wallSegments = SvgPathParser.ParseSegments(pathData1);

                                                    XmlNode descNode = bPath.FirstChild;
                                                    var properties = descNode.InnerText.Split(';')
                                                        .Where(p => p.Contains('='))
                                                        .Select(p => p.Split('=', 2))
                                                        .GroupBy(k => k[0].Trim(), v => v[1].Trim())
                                                        .ToDictionary(g => g.Key, g => g.First());

                                                    foreach (var segment in wallSegments)
                                                    {
                                                        GameObject go = Instantiate(WallPref);
                                                        Wall gs = go.GetComponent<Wall>();
                                                        gs.positionLine = segment;

                                                        for (int i = 0; i < gs.positionLine.Count; i++)
                                                        {
                                                            gs.positionLine[i] = gs.positionLine[i] * _scPath;
                                                            gs.positionLine[i] = _rmPath * gs.positionLine[i];
                                                            gs.positionLine[i] += _ovPath;

                                                            gs.positionLine[i] = gs.positionLine[i] * _scGroup;
                                                            gs.positionLine[i] = _rmGroup * gs.positionLine[i];
                                                            gs.positionLine[i] += _ovGroup;
                                                            gs.positionLine[i] = rmLayer * gs.positionLine[i];
                                                            gs.positionLine[i] += ovLayer;
                                                        }

                                                        if (properties.ContainsKey("template")) gs.material = properties["template"];
                                                        if (properties.ContainsKey("height"))
                                                        {
                                                            gs.reHighed = true;
                                                            gs.reHighedHeight = float.Parse(properties["height"]);
                                                        } 
                                                        if (properties.ContainsKey("merge"))
                                                        {
                                                            gs.merged = true;
                                                        }

                                                        MetaMap.instance.defaultLayer.mapItems.Add(gs);
                                                        gs.id = MetaMap.instance.getNewItemId("wall");
                                                        gs.layerIndex = number;
                                                    }
                                                }
                                                if (bPath.HasAttribute("inkscape:label")
                                                    && bPath.GetAttribute("inkscape:label").StartsWith("post"))
                                                {
                                                    string pathDataPost = bPath.GetAttribute("d");
                                                    if (string.IsNullOrWhiteSpace(pathDataPost)) continue;

                                                    List<List<Vector2>> postSegments = SvgPathParser.ParseSegments(pathDataPost);

                                                    var properties = new Dictionary<string, string>();
                                                    if (bPath.FirstChild is XmlElement descPost)
                                                    {
                                                        properties = descPost.InnerText.Split(';')
                                                            .Where(p => p.Contains('='))
                                                            .Select(p => p.Split('=', 2))
                                                            .GroupBy(k => k[0].Trim(), v => v[1].Trim())
                                                            .ToDictionary(g => g.Key, g => g.First());
                                                    }

                                                    string templateRef = null;
                                                    if (properties.ContainsKey("post_template"))
                                                        templateRef = properties["post_template"];
                                                    else
                                                    {
                                                        continue;
                                                    }
                                                    foreach (var segment in postSegments)
                                                    {
                                                        if (segment == null || segment.Count < 2) continue;

                                                        GameObject go;
                                                        if (PostPref != null)
                                                            go = Instantiate(PostPref);
                                                        else
                                                            go = new GameObject("Post");

                                                        Post ps = go.GetComponent<Post>();
                                                        if (ps == null) ps = go.AddComponent<Post>();
                                                        ps.positionLine = segment;

                                                        for (int i = 0; i < ps.positionLine.Count; i++)
                                                        {
                                                            ps.positionLine[i] = ps.positionLine[i] * _scPath;
                                                            ps.positionLine[i] = _rmPath * ps.positionLine[i];
                                                            ps.positionLine[i] += _ovPath;

                                                            ps.positionLine[i] = ps.positionLine[i] * _scGroup;
                                                            ps.positionLine[i] = _rmGroup * ps.positionLine[i];
                                                            ps.positionLine[i] += _ovGroup;
                                                            ps.positionLine[i] = rmLayer * ps.positionLine[i];
                                                            ps.positionLine[i] += ovLayer;
                                                        }

                                                        if (!string.IsNullOrEmpty(templateRef))
                                                            ps.template_ref = templateRef;

                                                        ps.id = MetaMap.instance.getNewItemId("post");
                                                        ps.layerIndex = number;
                                                        MetaMap.instance.defaultLayer.mapItems.Add(ps);
                                                    }
                                                }
                                            }
                                        }
                                    }

                                }

                            }
                        }
                    }
                    if (ele.GetAttribute("inkscape:label").StartsWith("offroad"))
                    {
                        Debug.Log("import offroad");
                        ImportOffroadFromGroup(ele);
                    }
                    if (ele.GetAttribute("inkscape:label").StartsWith("height_paths"))
                    {
                        ImportHeightPathsFromGroup(ele);
                    }
                    if (ele.GetAttribute("inkscape:label").StartsWith("material_paths"))
                    {
                        ImportMaterialPathsFromGroup(ele);
                    }
                }
            }
        }

        //CtrlZer.instance.checkPoint();
        if (Syncer.instence != null) Syncer.instence.ScatterMapItems();
    }
    public void importGeneralSetting()
    {
        Debug.Log("start to Import GeneralSettings");
        XmlDocument xmlDoc = new XmlDocument();
        string xmlPath = Path.Combine(Application.dataPath, basePath, "objects.svg");
        Debug.Log("LoadSvg at " + xmlPath);
        xmlDoc.Load(xmlPath);
        XmlElement root = xmlDoc.DocumentElement;
        foreach (XmlNode node in root.ChildNodes )
        {
            if(node.Name =="g" && node is XmlElement end)
            {
                if(end.GetAttribute("inkscape:label").StartsWith("materials"))
                {
                    foreach (XmlNode recs in end.ChildNodes)
                    {
                        if(recs.Name == "rect"&& recs is XmlElement rece)
                        {
                            if(rece.GetAttribute("inkscape:label").StartsWith("#general"))
                            {
                                Debug.Log("find gs :" + rece.ToString());
                                XmlNode descNode = null;
                                foreach (XmlNode child in rece.ChildNodes)
                                {
                                    if (child.Name == "desc") { descNode = child; break; }
                                }
                                if (descNode == null) continue;
                                MetaMap.instance.m_settings = descNode.InnerText;
                                UIManager.instance.mms[7].transform.GetChild(0).gameObject.GetComponent<TMPro.TMP_InputField>().SetTextWithoutNotify(descNode.InnerText);
                                return;
                            }
                        }
                    }
                }
            }
        }

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
    /// <summary>
    /// <c>Assets/templates</c> 下按文件名排序取第一个 *.xml 或 *.svg（Inkscape 模板与纯 xml 均可）。
    /// </summary>
    public static string FindFirstTemplateInTemplatesDir()
    {
        string templatesDir = Path.Combine(Application.dataPath, "templates");
        if (!Directory.Exists(templatesDir)) return null;

        return Directory.GetFiles(templatesDir, "*.xml")
            .Concat(Directory.GetFiles(templatesDir, "*.svg"))
            .OrderBy(p => Path.GetFileName(p), StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
    }

    /// <summary>模板 / objects.svg 根下 inkscape:label="materials" 的图层。</summary>
    public static XmlElement FindMaterialsLayer(XmlElement root)
    {
        if (root == null) return null;
        foreach (XmlNode node in root.ChildNodes)
        {
            if (node is XmlElement g && g.Name == "g"
                && g.GetAttribute("inkscape:label") == "materials")
                return g;
        }
        return null;
    }

    public void importTemplates()
    {
        Debug.Log("start to Import GeneralSettings");
        XmlDocument xmlDoc = new XmlDocument();
        string templatesDir = Path.Combine(Application.dataPath, "templates");
        string templatePath = FindFirstTemplateInTemplatesDir();

        if (string.IsNullOrEmpty(templatePath) || !File.Exists(templatePath))
        {
            Debug.LogError("MapImporter: 找不到模板文件（*.xml / *.svg），目录: " + templatesDir);
            return;
        }
        xmlDoc.Load(templatePath);
        XmlElement eg = FindMaterialsLayer(xmlDoc.DocumentElement);
        if (eg == null)
        {
            Debug.LogError("MapImporter: 模板中未找到 inkscape:label=\"materials\" 的图层: " + templatePath);
            return;
        }
        foreach (XmlNode node in eg.ChildNodes)
        {
            if (node.Name != "rect") continue;
            XmlElement rect = node as XmlElement;
            if(rect == null) continue;
            if (rect.GetAttribute("inkscape:label").StartsWith("#general", StringComparison.Ordinal))
                continue;
            if(rect.GetAttribute("inkscape:label").StartsWith("#mesh"))
            {
//                Debug.Log(rect.GetAttribute("id"));
                float width = float.Parse( rect.GetAttribute("width"));
                float height = float.Parse(rect.GetAttribute("height"));
                foreach (XmlNode node2 in rect.ChildNodes)
                { 
                var properties = node2.InnerText.Split(';')
                    .Where(p => p.Contains('='))
                    .Select(p => p.Split('=', 2))
                    .GroupBy(kv => kv[0].Trim())
                    .ToDictionary(
                        g => g.Key,
                        g => g.Last()[1].Trim()
                    );
                if (!properties.ContainsKey("name")) continue;
                MeshTemplate template = new MeshTemplate(); 
                template.size = new Vector2(width, height);
                template.name = properties["name"];
                if(properties.ContainsKey("base_template"))
                {
                    //check if the base template is in list by name
                    MeshTemplate baseTemplate = MetaMap.instance.meshTemplates.FirstOrDefault(template => template.name == properties["base_template"]);
                    if(baseTemplate != null)
                    {
                        template.meshName = baseTemplate.meshName;
                        template.textureName = baseTemplate.textureName;
                        template.textureAC = baseTemplate.textureAC;
                        template.extend = baseTemplate.extend;
                        template.color = baseTemplate.color;
                        template.size = baseTemplate.size;
                    }
                }
                if (properties.ContainsKey("mesh_name"))
                {template.meshName = properties["mesh_name"];}
                if(properties.ContainsKey("texture0"))
                {
                    template.textureName = properties["texture0"];
                }
                if (properties.ContainsKey("texture0_atlas_cell"))
                {
                    string[] parts = properties["texture0_atlas_cell"].Split(' ');
                    template.textureAC = new Vector4(
                        float.Parse(parts[0]),
                        float.Parse(parts[1]),
                        float.Parse(parts[2]),
                        float.Parse(parts[3])
                    );
                }
                if (properties.ContainsKey("collision_model_size"))
                {
//                    Debug.Log("MapImporter get collision_model_size: " + properties["collision_model_size"]);
                    string inputS = Regex.Replace(properties["collision_model_size"], @"\s+", " ").Trim();
                    if (TryParseVector3(inputS, out Vector3 vector))
                        template.extend = vector;
                }
                template.color = MathOfRwrme.StringToColor(template.name);
                MetaMap.instance.meshTemplates.Add(template);
                }
            }
            if(rect.GetAttribute("inkscape:label").StartsWith("#material"))
            {
                foreach (XmlNode node2 in rect.ChildNodes)
                {
                    var properties = node2.InnerText.Split(';')
                        .Where(p => p.Contains('='))
                        .Select(p => p.Split('=', 2))
                        .GroupBy(kv => kv[0].Trim())
                        .ToDictionary(
                            g => g.Key,
                            g => g.Last()[1].Trim()
                        );
                    if (properties.ContainsKey("type")) continue;
                    if (!properties.ContainsKey("name")) continue;
                    string name = properties["name"];
                    Color c1 = MathOfRwrme.StringToColor(name);
                    Color c2 = MathOfRwrme.StringToColor("side"+name);
                    BuildingType bt = new BuildingType(name,c1,c2);
                    MetaMap.instance.buildingTypes.Add(bt);
                }
            }
            if(rect.GetAttribute("inkscape:label").StartsWith("#wall_template"))
            {
                Debug.Log("get wall template");
                foreach (XmlNode node2 in rect.ChildNodes)
                {
                    var properties = node2.InnerText.Split(';')
                        .Where(p => p.Contains('='))
                        .Select(p => p.Split('=', 2))
                        .GroupBy(kv => kv[0].Trim())
                        .ToDictionary(
                            g => g.Key,
                            g => g.Last()[1].Trim()
                        );
                    if (!properties.ContainsKey("name")) continue;
                    string name = properties["name"];
                    Color c1 = MathOfRwrme.StringToColor(name);
                    float dep = 1.0f;
                    float hei = 1.0f;
                    if (properties.ContainsKey("depth")) dep = float.Parse( properties["depth"] );
                    if (properties.ContainsKey("height")) hei = float.Parse(properties["height"]);
                    WallType wt = new WallType(name, c1, dep,hei);
                    MetaMap.instance.wallTypes.Add(wt);
                }
            }
            if (rect.GetAttribute("inkscape:label").StartsWith("#terrain_layer"))
            {
                Debug.Log("terrain_layer");
                foreach (XmlNode node2 in rect.ChildNodes)
                {
                    var properties = node2.InnerText.Split(';')
                        .Where(p => p.Contains('='))
                        .Select(p => p.Split('=', 2))
                        .GroupBy(kv => kv[0].Trim())
                        .ToDictionary(
                            g => g.Key,
                            g => g.Last()[1].Trim()
                        );
                    if (!properties.ContainsKey("alpha")) continue;
                    int ind = int.Parse( properties["index"]);
                    while(ind > MetaMap.instance.terrainAlphaFileName.Count-1)
                    {
                        MetaMap.instance.terrainAlphaFileName.Add("null");
                    }
                    if (ind+1 >MetaMap.instance.terrainLayerCount) MetaMap.instance.terrainLayerCount = ind+1;
                    MetaMap.instance.terrainAlphaFileName[ind] = properties["alpha"];
                }
            }
            if(rect.GetAttribute("id").StartsWith("decal_template"))
            {
                foreach (XmlNode node2 in rect.ChildNodes)
                {
                    var properties = node2.InnerText.Split(';')
                        .Where(p => p.Contains('='))
                        .Select(p => p.Split('=', 2))
                        .GroupBy(kv => kv[0].Trim())
                        .ToDictionary(
                            g => g.Key,
                            g => g.Last()[1].Trim()
                        );
                    if (!properties.ContainsKey("name")) continue;
                    string name = properties["name"];
                    DecalTemplate decalTemplate = new DecalTemplate(name);
                    if (properties.ContainsKey("texture0"))
                        decalTemplate.textureName = properties["texture0"];
                    if (properties.ContainsKey("texture0_atlas_cell")
                        && IntVector4.TryParse(properties["texture0_atlas_cell"], out IntVector4 cut))
                        decalTemplate.textureCut = cut;
                        
                    float cWidth = float.Parse(rect.GetAttribute("width"));
                    float cHeight = float.Parse(rect.GetAttribute("height"));
                    decalTemplate.length = Mathf.Max( cWidth, cHeight ); 
                    MetaMap.instance.DecalTemplates.Add(decalTemplate);
                }
            }
            if(rect.GetAttribute("id").StartsWith("post_template"))
            {
                foreach (XmlNode node2 in rect.ChildNodes)
                {
                    var properties = node2.InnerText.Split(';')
                        .Where(p => p.Contains('='))
                        .Select(p => p.Split('=', 2))
                        .GroupBy(kv => kv[0].Trim())
                        .ToDictionary(g => g.Key, g => g.Last()[1].Trim());
                    if (!properties.ContainsKey("name")) continue;
                    string name = properties["name"];
                    if (!properties.ContainsKey("mesh_template")) continue;
                    string meshName = properties["mesh_template"];
                    PostTemplate postTemplate = new PostTemplate(name, meshName);
                    MetaMap.instance.PostTemplates.Add(postTemplate);
                }
            }
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

        if (trs == string.Empty) return;

        //translate matrix rotate scale
        if (trs.StartsWith("translate"))
        {
            string cleanString = trs.Replace("translate(", "").Replace(")", "").Trim();
            string[] parts = cleanString.Split(new char[] { ',', ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
            float x = float.Parse(parts[0]);
            float y = parts.Length > 1 ? float.Parse(parts[1]) : 0f;
            offset = new Vector2(x, y);
        }
        else if (trs.StartsWith("matrix"))
        {
            string cleanString = trs.Replace("matrix(", "").Replace(")", "").Trim();
            string[] parts = cleanString.Split(new char[] { ',', ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
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
            string cleanString = trs.Replace("rotate(", "").Replace(")", "").Trim();
            string[] parts = cleanString.Split(new char[] { ',', ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
            float svgAngle = float.Parse(parts[0]);
            angle = -svgAngle;
            rotate = Matrix2x2.CreateRotation(svgAngle);
            if (parts.Length >= 3)
            {
                float cx = float.Parse(parts[1]);
                float cy = float.Parse(parts[2]);
                offset = new Vector2(
                    cx - cx * Mathf.Cos(svgAngle * Mathf.Deg2Rad) + cy * Mathf.Sin(svgAngle * Mathf.Deg2Rad),
                    cy - cx * Mathf.Sin(svgAngle * Mathf.Deg2Rad) - cy * Mathf.Cos(svgAngle * Mathf.Deg2Rad)
                );
            }
        }
        else if(trs.StartsWith("scale"))
        {
            string cleanString = trs.Replace("scale(", "").Replace(")", "").Trim();
            string[] parts = cleanString.Split(new char[] { ',', ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
            float x = float.Parse(parts[0]);
            float y = parts.Length > 1 ? float.Parse(parts[1]) : x;
            scale = new Vector2(x, y);
        }
        else
        {
            Debug.Log("com not found:" + trs);
        }

    }
    public void dealWithTransform(string trs, ref Matrix2x2 rotate, ref float angle, ref Vector2 offset)
    {
        Vector2 scale=Vector2.one;
        dealWithTransform(trs, ref rotate, ref angle, ref offset, ref scale);
    }
    public void ImportAllMaps()
    {
        importTemplates();
        importGeneralSetting();
        importMapConfig();
        importObjects();
        importTerrain();
        importPreTerrain();
        importTerrainCombinAplha();
        importPreCombinedAlpha();
    }

    public void importPreTerrain()
    {
        MetaMap mm = gameObject.GetComponent<MetaMap>();
        if (mm == null) return;

        string prePath = Path.Combine(Application.dataPath, basePath, MetaMap.PreHeightmapFileName);
        if (File.Exists(prePath))
        {
            GrayScaleImage pre = LoadGrayScaleImage(prePath);
            mm.m_preTerrain.maxHeight = mm.m_metaTerrain.maxHeight;
            mm.m_preTerrain.fileName = MetaMap.PreHeightmapFileName;
            mm.m_preTerrain.setData(pre);
            Debug.Log("MapImporter: loaded " + MetaMap.PreHeightmapFileName);
        }
        else
        {
            mm.InitPreTerrainFromReference();
            Debug.Log("MapImporter: initialized pre heightmap from terrain5 reference");
        }
    }

    public void importPreCombinedAlpha()
    {
        MetaMap mm = gameObject.GetComponent<MetaMap>();
        if (mm == null || cbdTl == null) return;

        string refPath = Path.Combine(Application.dataPath, basePath, MetaMap.FinalCombinedAlphaFileName);
        if (File.Exists(refPath))
        {
            byte[] refData = File.ReadAllBytes(refPath);
            Texture2D refTex = new Texture2D(2, 2);
            refTex.LoadImage(refData);
            mm.referenceCombinedAlpha = refTex;
        }

        string prePath = Path.Combine(Application.dataPath, basePath, MetaMap.PreCombinedAlphaFileName);
        if (File.Exists(prePath))
        {
            byte[] preData = File.ReadAllBytes(prePath);
            Texture2D preTex = new Texture2D(2, 2);
            preTex.LoadImage(preData);
            mm.preCombinedAlpha = preTex;
            Debug.Log("MapImporter: loaded " + MetaMap.PreCombinedAlphaFileName);
        }
        else if (mm.referenceCombinedAlpha != null)
        {
            mm.preCombinedAlpha = TerrainPathRasterizer.CloneTexture(mm.referenceCombinedAlpha);
            Debug.Log("MapImporter: initialized pre combined alpha from terrain5 reference");
        }
    }

    public void importTerrainCombinAplha()
    {
        string fileName = "terrain5_combined_alpha.png";
        string path = Path.Combine(Application.dataPath,basePath,fileName);
        if (File.Exists(path))
        {
            byte[] fileData = File.ReadAllBytes(path);
            Texture2D texture = new Texture2D(2, 2);
            texture.LoadImage(fileData);
            cbdTl.SetTexture("_Mask",texture);
            Debug.Log("MapImporter set cbdmtl");
        }

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

    public void PrintStats()
    {
        foreach (var pair in loadedMaps)
        {
            Debug.Log($"{pair.Key}: {pair.Value.Width}x{pair.Value.Height}");
        }
    }

    static void ApplyOffroadTransformStep(ref Vector2 p, Matrix2x2 rm, Vector2 ov, Vector2 sc)
    {
        p *= sc;
        p = rm * p;
        p += ov;
    }

    /// <summary>用 <see cref="SvgPathParser.ParsePathDataToOps"/> 结果构造锚点、曲线标记与控制点（每段 Cubic 两个控制点）。</summary>
    static void BuildOffroadFromPathOps(List<SvgPathOp> ops, out List<Vector2> anchors, out List<bool> curveFlags, out List<Vector2> controlPoints)
    {
        anchors = new List<Vector2>();
        curveFlags = new List<bool>();
        controlPoints = new List<Vector2>();
        Vector2 subpathStart = Vector2.zero;
        const float eps = 1e-4f;

        foreach (SvgPathOp op in ops)
        {
            switch (op.Kind)
            {
                case SvgPathOpKind.MoveTo:
                    anchors.Add(op.P);
                    curveFlags.Add(false);
                    subpathStart = op.P;
                    break;
                case SvgPathOpKind.LineTo:
                    if (anchors.Count > 0 && (anchors[anchors.Count - 1] - op.P).sqrMagnitude < eps * eps)
                        break;
                    anchors.Add(op.P);
                    curveFlags.Add(false);
                    break;
                case SvgPathOpKind.CubicTo:
                    Vector2 p0 = op.P;
                    if (anchors.Count == 0)
                    {
                        anchors.Add(p0);
                        curveFlags.Add(false);
                    }
                    else if ((anchors[anchors.Count - 1] - p0).sqrMagnitude > eps * eps)
                    {
                        anchors.Add(p0);
                        curveFlags.Add(false);
                    }
                    anchors.Add(op.CubicP3);
                    curveFlags.Add(true);
                    controlPoints.Add(op.CubicC1);
                    controlPoints.Add(op.CubicC2);
                    break;
                case SvgPathOpKind.ClosePath:
                    if (anchors.Count > 0 && (anchors[anchors.Count - 1] - subpathStart).sqrMagnitude > eps * eps)
                    {
                        anchors.Add(subpathStart);
                        curveFlags.Add(false);
                    }
                    break;
            }
        }
    }

    static void TransformOffroadPointList(List<Vector2> pts, Matrix2x2 _rmPath, Vector2 _ovPath, Vector2 _scPath,
        List<(Matrix2x2 rm, Vector2 ov, Vector2 sc)> ancestorChain, Matrix2x2 rmOff, Vector2 ovOff, Vector2 scOff)
    {
        for (int i = 0; i < pts.Count; i++)
        {
            Vector2 p = pts[i];
            ApplyOffroadTransformStep(ref p, _rmPath, _ovPath, _scPath);
            for (int gi = ancestorChain.Count - 1; gi >= 0; gi--)
            {
                var g = ancestorChain[gi];
                ApplyOffroadTransformStep(ref p, g.rm, g.ov, g.sc);
            }
            ApplyOffroadTransformStep(ref p, rmOff, ovOff, scOff);
            pts[i] = p;
        }
    }

    void ImportOffroadFromGroup(XmlElement offroadGroup)
    {
        float raOff = 0f;
        Matrix2x2 rmOff = Matrix2x2.CreateRotation(0f);
        Vector2 ovOff = Vector2.zero;
        Vector2 scOff = Vector2.one;
        if (offroadGroup.HasAttribute("transform"))
            dealWithTransform(offroadGroup.GetAttribute("transform"), ref rmOff, ref raOff, ref ovOff, ref scOff);

        var emptyChain = new List<(Matrix2x2 rm, Vector2 ov, Vector2 sc)>();
        ImportOffroadRecursive(offroadGroup, emptyChain, rmOff, ovOff, scOff);
    }

    void ImportOffroadRecursive(XmlElement parent, List<(Matrix2x2 rm, Vector2 ov, Vector2 sc)> ancestorChain,
        Matrix2x2 rmOff, Vector2 ovOff, Vector2 scOff)
    {
        foreach (XmlNode child in parent.ChildNodes)
        {
            if (child is not XmlElement xe) continue;

            if (xe.Name == "path")
            {
                string pathData = xe.GetAttribute("d");
                if (string.IsNullOrWhiteSpace(pathData)) continue;

                float _raPath = 0f;
                Matrix2x2 _rmPath = Matrix2x2.CreateRotation(0f);
                Vector2 _ovPath = Vector2.zero;
                Vector2 _scPath = Vector2.one;
                if (xe.HasAttribute("transform"))
                    dealWithTransform(xe.GetAttribute("transform"), ref _rmPath, ref _raPath, ref _ovPath, ref _scPath);

                List<SvgPathOp> pathOps = SvgPathParser.ParsePathDataToOps(pathData);
                BuildOffroadFromPathOps(pathOps, out List<Vector2> anchors, out List<bool> curveFlags, out List<Vector2> cps);
                if (anchors == null || anchors.Count == 0) continue;

                TransformOffroadPointList(anchors, _rmPath, _ovPath, _scPath, ancestorChain, rmOff, ovOff, scOff);
                TransformOffroadPointList(cps, _rmPath, _ovPath, _scPath, ancestorChain, rmOff, ovOff, scOff);

                GameObject go;
                if (OffroadPref != null)
                {
                    go = Instantiate(OffroadPref);
                }
                else
                {
                    go = new GameObject("Offroad");
                }
                Offroad ofr = go.GetComponent<Offroad>();
                if (ofr == null) ofr = go.AddComponent<Offroad>();

                ofr.positionLine = anchors;
                ofr.id = MetaMap.instance.getNewItemId("offroad");
                ofr.layerIndex = MetaMap.instance.offroadHeightSampleLayer;
                ofr.curve = curveFlags;
                ofr.controlPoints = cps;

                MetaMap.instance.offroadLayer.mapItems.Add(ofr);
            }
            /*
            else if (xe.Name == "g")
            {
                float gra = 0f;
                Matrix2x2 grm = Matrix2x2.CreateRotation(0f);
                Vector2 gov = Vector2.zero;
                Vector2 gsc = Vector2.one;
                if (xe.HasAttribute("transform"))
                    dealWithTransform(xe.GetAttribute("transform"), ref grm, ref gra, ref gov, ref gsc);

                var nextChain = new List<(Matrix2x2 rm, Vector2 ov, Vector2 sc)>(ancestorChain.Count + 1);
                nextChain.AddRange(ancestorChain);
                nextChain.Add((grm, gov, gsc));
                ImportOffroadRecursive(xe, nextChain, rmOff, ovOff, scOff);
            }*/
        }
    }

    static Dictionary<string, string> ParsePathDesc(XmlElement pathEl)
    {
        if (pathEl.FirstChild is not XmlElement descEl) return new Dictionary<string, string>();
        return descEl.InnerText.Split(';')
            .Where(p => p.Contains('='))
            .Select(p => p.Split('=', 2))
            .GroupBy(k => k[0].Trim(), v => v[1].Trim())
            .ToDictionary(g => g.Key, g => g.First());
    }

    void ImportHeightPathsFromGroup(XmlElement group)
    {
        float ra = 0f;
        Matrix2x2 rm = Matrix2x2.CreateRotation(0f);
        Vector2 ov = Vector2.zero;
        Vector2 sc = Vector2.one;
        if (group.HasAttribute("transform"))
            dealWithTransform(group.GetAttribute("transform"), ref rm, ref ra, ref ov, ref sc);

        foreach (XmlNode child in group.ChildNodes)
        {
            if (child is not XmlElement xe || xe.Name != "path") continue;
            string pathData = xe.GetAttribute("d");
            if (string.IsNullOrWhiteSpace(pathData)) continue;

            var props = ParsePathDesc(xe);
            List<SvgPathOp> pathOps = SvgPathParser.ParsePathDataToOps(pathData);
            BuildOffroadFromPathOps(pathOps, out List<Vector2> anchors, out List<bool> curveFlags, out List<Vector2> cps);
            if (anchors == null || anchors.Count < 2) continue;

            for (int i = 0; i < anchors.Count; i++)
                anchors[i] = rm * (anchors[i] * sc) + ov;
            for (int i = 0; i < cps.Count; i++)
                cps[i] = rm * (cps[i] * sc) + ov;

            HeightPath hp = new GameObject("HeightPath").AddComponent<HeightPath>();
            hp.positionLine = anchors;
            hp.curve = curveFlags;
            hp.controlPoints = cps;

            if (props.TryGetValue("width", out string wStr) && float.TryParse(wStr, out float w))
                hp.width = w;
            if (props.TryGetValue("height_offset", out string hoStr) && float.TryParse(hoStr, out float ho))
            {
                hp.heightDelta = ho;
                hp.mode = HeightPathMode.Offset;
            }
            else if (props.TryGetValue("height", out string hAbsStr) && float.TryParse(hAbsStr, out float hAbs))
            {
                hp.heightDelta = hAbs;
                hp.mode = HeightPathMode.Set;
            }
            else if (props.TryGetValue("height_delta", out string hStr) && float.TryParse(hStr, out float hd))
                hp.heightDelta = hd;
            if (props.TryGetValue("mode", out string modeStr))
            {
                if (modeStr == "lower") hp.mode = HeightPathMode.Lower;
                else if (modeStr == "set") hp.mode = HeightPathMode.Set;
                else if (modeStr == "offset") hp.mode = HeightPathMode.Offset;
                else if (modeStr == "raise") hp.mode = HeightPathMode.Raise;
            }

            hp.id = MetaMap.instance.getNewItemId("height_path");
            MetaMap.instance.heightPathLayer.mapItems.Add(hp);
        }
    }

    void ImportMaterialPathsFromGroup(XmlElement group)
    {
        float ra = 0f;
        Matrix2x2 rm = Matrix2x2.CreateRotation(0f);
        Vector2 ov = Vector2.zero;
        Vector2 sc = Vector2.one;
        if (group.HasAttribute("transform"))
            dealWithTransform(group.GetAttribute("transform"), ref rm, ref ra, ref ov, ref sc);

        foreach (XmlNode child in group.ChildNodes)
        {
            if (child is not XmlElement xe || xe.Name != "path") continue;
            string pathData = xe.GetAttribute("d");
            if (string.IsNullOrWhiteSpace(pathData)) continue;

            var props = ParsePathDesc(xe);
            List<SvgPathOp> pathOps = SvgPathParser.ParsePathDataToOps(pathData);
            BuildOffroadFromPathOps(pathOps, out List<Vector2> anchors, out List<bool> curveFlags, out List<Vector2> cps);
            if (anchors == null || anchors.Count < 2) continue;

            for (int i = 0; i < anchors.Count; i++)
                anchors[i] = rm * (anchors[i] * sc) + ov;
            for (int i = 0; i < cps.Count; i++)
                cps[i] = rm * (cps[i] * sc) + ov;

            MaterialPath mp = new GameObject("MaterialPath").AddComponent<MaterialPath>();
            mp.positionLine = anchors;
            mp.curve = curveFlags;
            mp.controlPoints = cps;

            if (props.TryGetValue("width", out string wStr) && float.TryParse(wStr, out float w))
                mp.width = w;
            if (props.TryGetValue("material_index", out string miStr) && int.TryParse(miStr, out int mi))
                mp.materialIndex = mi;
            else if (props.TryGetValue("material", out string matStr))
            {
                switch (matStr.Trim().ToLowerInvariant())
                {
                    case "sand": mp.materialIndex = 1; break;
                    case "grass": mp.materialIndex = 2; break;
                    case "asphalt": mp.materialIndex = 3; break;
                    case "road": mp.materialIndex = 4; break;
                }
            }

            mp.id = MetaMap.instance.getNewItemId("material_path");
            MetaMap.instance.materialPathLayer.mapItems.Add(mp);
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
        catch (System.Exception)
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