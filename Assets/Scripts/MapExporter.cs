using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Xml;           // .NET 标准 XML
using System.Xml.Linq;
using UnityEditor.Experimental.GraphView;      // LINQ to XML（更现代）
using UnityEngine;
using static UnityEditor.Experimental.GraphView.GraphView;

public class MapExporter : MonoBehaviour
{
    // Start is called before the first frame update
    MetaMap m_mm;
    Terrain targetTerrain;
    public static MapExporter ins;

    public XmlDocument xmlDoc;
    
    [Header("导出路径配置")]
    public string basePath = "map"; // 基础路径

    void Start()
    {
        ins = this;
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
        string xmlFilePath = Path.Combine(Application.dataPath, basePath, "OUTobjects.svg");
        xmlDoc = new XmlDocument();
        XmlDeclaration xd=  xmlDoc.CreateXmlDeclaration("1.0", "UTF-8","no");
        xmlDoc.AppendChild( xd );
        XmlElement rootElement = xmlDoc.CreateElement("svg");

        rootElement.SetAttribute("xmlns:dc", "http://purl.org/dc/elements/1.1/");
        rootElement.SetAttribute("xmlns:cc", "http://creativecommons.org/ns#");
        rootElement.SetAttribute("xmlns:rdf", "http://www.w3.org/1999/02/22-rdf-syntax-ns#");
        rootElement.SetAttribute("xmlns:svg", "http://www.w3.org/2000/svg");
        rootElement.SetAttribute("xmlns", "http://www.w3.org/2000/svg");
        rootElement.SetAttribute("xmlns:xlink", "http://www.w3.org/1999/xlink");
        rootElement.SetAttribute("xmlns:sodipodi", "http://sodipodi.sourceforge.net/DTD/sodipodi-0.dtd");
        rootElement.SetAttribute("xmlns:inkscape", "http://www.inkscape.org/namespaces/inkscape");
        rootElement.SetAttribute("width", "2048");
        rootElement.SetAttribute("height", "2048");
        rootElement.SetAttribute("id", "svg2");
        string inkscapeNs = "http://www.inkscape.org/namespaces/inkscape";
        string sodipodiNs = "http://sodipodi.sourceforge.net/DTD/sodipodi-0.dtd";
        rootElement.SetAttribute("version", inkscapeNs, "0.48.5 r10040");
        rootElement.SetAttribute("sodipodi:docname", "objects.svg");
        rootElement.SetAttribute("export-xdpi", inkscapeNs, "90");
        rootElement.SetAttribute("export-ydpi", inkscapeNs, "90");
        rootElement.SetAttribute("style", "display:inline;enable-background:new");
        rootElement.SetAttribute("export-filename", inkscapeNs, "G:\\PROGRAMMING\\cplusplus\\runningwithrifles_svn\\simplified3d\\Release\\media\\packages\\vanilla\\maps\\map2\\_rwr_height.png");



        xmlDoc.AppendChild(rootElement);

        //export map items(constructions)
        MetaMap.instance.defaultLayer.sortByIndex();
        int layC =  MetaMap.instance.defaultLayer.mapItems[MetaMap.instance.defaultLayer.mapItems.Count - 1].layerIndex;
        int mic = MetaMap.instance.defaultLayer.mapItems.Count;
        for (int i =1;i<=layC;i++)
        {
            XmlElement layer = xmlDoc.CreateElement("g");
            layer.SetAttribute("groupmode", inkscapeNs, "layer");
            layer.SetAttribute("id", "layer"+i.ToString()+"defaultlayer");
            layer.SetAttribute("label", inkscapeNs, "layer" +i.ToString());
            layer.SetAttribute("style", "display:inline");

            //add Building here

            XmlElement buildingLayer = xmlDoc.CreateElement("g");
            buildingLayer.SetAttribute("groupmode", inkscapeNs, "layer");
            buildingLayer.SetAttribute("id", "layer" + i.ToString() + "buildings");
            buildingLayer.SetAttribute("label", inkscapeNs, "buildings");
            buildingLayer.SetAttribute("style", "display:inline");
            buildingLayer.SetAttribute("sodipodi:insensitive", "true");

            for (int j =0;j<MetaMap.instance.defaultLayer.mapItems.Count;j++)
            {
                MapItem mi = MetaMap.instance.defaultLayer.mapItems[j];
                if (mi.layerIndex != i) continue;
                Building bd = mi as Building;
                if (bd != null)
                {
                    XmlElement buiE = xmlDoc.CreateElement("rect");
                    buiE.SetAttribute("style", "fill:#ff0000;fill-opacity:1;stroke:#000000;stroke-width:1.0000006;stroke-opacity:1;display:inline;enable-background:new");
                    buiE.SetAttribute("id", bd.id);
                    buiE.SetAttribute("width", ( bd.size.x).ToString());
                    buiE.SetAttribute("height", ( bd.size.y).ToString());

                    buiE.SetAttribute("x", "0");
                    buiE.SetAttribute("y", "0");
                    buiE.SetAttribute("label", inkscapeNs, "#rect6406" + j.ToString());
                    buiE.SetAttribute("transform", MathOfRwrme.angleToTransform(bd.rotation, bd.position));
                    XmlElement buiEDesc = xmlDoc.CreateElement("desc");
                    buiEDesc.SetAttribute("id", "desc" + j.ToString());
                    string baseDescStr = $"height={bd.height};material={bd.material};";
                    if (bd.roof)
                    {
                        baseDescStr = baseDescStr + "roof_type = elevated;";
                    }
                    else
                    {
                        baseDescStr = baseDescStr + "roof_type = flat;";

                    }
                    buiEDesc.InnerText = baseDescStr;
                    buiE.AppendChild(buiEDesc);
                    buildingLayer.AppendChild(buiE);
                }

            }
            layer.AppendChild(buildingLayer);

            XmlElement WallLayer = xmlDoc.CreateElement("g");
            WallLayer.SetAttribute("groupmode", inkscapeNs, "layer");
            WallLayer.SetAttribute("id", "layer" + i.ToString() + "walls");
            WallLayer.SetAttribute("label", inkscapeNs, "walls");
            WallLayer.SetAttribute("style", "display:inline");

            for(int j = 0;j<MetaMap.instance.defaultLayer.mapItems.Count;j++)
            {
                MapItem mi = MetaMap.instance.defaultLayer.mapItems[j];
                if (mi.layerIndex != i) continue;
                Wall wl = mi as Wall;
                if(wl != null)
                {
                    XmlElement wlE = xmlDoc.CreateElement("path");

                    string cmds = new string('c', wl.positionLine.Count);
                    wlE.SetAttribute("nodetypes", sodipodiNs, cmds);
                    wlE.SetAttribute("label", inkscapeNs, "");
                    wlE.SetAttribute("connector-curvature", inkscapeNs, "0");
                    wlE.SetAttribute("id", wl.id);

                    string pcd = "m";
                    Vector2 pos = Vector2.zero;
                    for(int step=0; step < wl.positionLine.Count;step++)
                    {
                        Vector2 shownPos = wl.positionLine[step] - pos;
                        pos = wl.positionLine[step];
                        pcd += " " + shownPos.x.ToString() + "," + shownPos.y.ToString();
                        //pcd += (" " + shownPos.ToString().Trim('(', ')'));
                    }
                    wlE.SetAttribute("d", pcd);
                    wlE.SetAttribute("style", "fill:none;stroke:#008000;stroke-width:2;stroke-linecap:butt;stroke-linejoin:miter;stroke-miterlimit:4;stroke-opacity:1;stroke-dasharray:none;display:inline;enable-background:new");

                    XmlElement wlEDesc = xmlDoc.CreateElement("desc");
                    wlEDesc.SetAttribute("id", "desc" + (mic+j).ToString());
                    wlEDesc.InnerText = "template = "+ wl.material+";";
                    wlE.AppendChild(wlEDesc);
                    WallLayer.AppendChild(wlE);

                }
            }
            layer.AppendChild(WallLayer);

            XmlElement platformLayer = xmlDoc.CreateElement("g");
            platformLayer.SetAttribute("groupmode", inkscapeNs, "layer");
            platformLayer.SetAttribute("id", "layer" + i.ToString() + "platforms");
            platformLayer.SetAttribute("label", inkscapeNs, "platforms");
            platformLayer.SetAttribute("style", "display:inline");
            for (int j = 0; j < MetaMap.instance.defaultLayer.mapItems.Count; j++)
            {
                MapItem mi = MetaMap.instance.defaultLayer.mapItems[j];
                if (mi.layerIndex != i) continue;
                Platform plt = mi as Platform;
                if (plt != null)
                {
                    XmlElement pair = xmlDoc.CreateElement("g");
                    pair.SetAttribute("id", "gp" + (j + mic * 2).ToString());

                    XmlElement stp = xmlDoc.CreateElement("path");
                    string cmdStart = new string('c', plt.positinLineR.Count);
                    stp.SetAttribute("nodetypes", sodipodiNs, cmdStart);
                    stp.SetAttribute("label", inkscapeNs, plt.id+"_platform");
                    stp.SetAttribute("connector-curvature", inkscapeNs, "0");
                    stp.SetAttribute("id", plt.id+"_s");
                    string pcd = "m";
                    Vector2 pos = Vector2.zero;
                    for (int step = 0; step < plt.positinLineR.Count; step++)
                    {
                        Vector2 shownPos = plt.positinLineR[step] - pos;
                        pos = plt.positinLineR[step];
                        pcd += " " + shownPos.x.ToString() + "," + shownPos.y.ToString();
                    }
                    stp.SetAttribute("d", pcd);
                    stp.SetAttribute("style", "fill:none;stroke:#0000ff;stroke-width:1px;stroke-linecap:butt;stroke-linejoin:miter;stroke-opacity:1;display:inline;enable-background:new");

                    XmlElement stpDesc = xmlDoc.CreateElement("desc");
                    stpDesc.SetAttribute("id", "desc" + (mic*2 + j).ToString());
                    stpDesc.InnerText = "type = start;";
                    if(plt.isDeck)stpDesc.InnerText = "type = deck_start;";
                    stp.AppendChild(stpDesc);

                    pair.AppendChild(stp);
                    
                    
                    
                    XmlElement end = xmlDoc.CreateElement("path");

                    string cmdEnd = new string('c', plt.positinLineL.Count);
                    end.SetAttribute("nodetypes", sodipodiNs, cmdStart);
                    end.SetAttribute("label", "");
                    end.SetAttribute("connector-curvature", inkscapeNs, "0");
                    end.SetAttribute("id", plt.id );
                    pcd = "m";
                    pos = Vector2.zero;
                    for (int step = 0; step < plt.positinLineL.Count; step++)
                    {
                        Vector2 shownPos = plt.positinLineL[step] - pos;
                        pos = plt.positinLineL[step];
                        pcd += " " + shownPos.x.ToString() + "," + shownPos.y.ToString();
                    }
                    end.SetAttribute("d", pcd);
                    end.SetAttribute("style", "fill:none;stroke:#ff0000;stroke-width:1px;stroke-linecap:butt;stroke-linejoin:miter;stroke-opacity:1;display:inline;enable-background:new");

                    XmlElement endDesc = xmlDoc.CreateElement("desc");
                    endDesc.SetAttribute("id", "desc" + (mic * 2 + j).ToString());
                    endDesc.InnerText = "type = end;";
                    if (plt.isDeck) { 
                        endDesc.InnerText = "type = deck_end;";
                        endDesc.InnerText += "height = "+plt.height.ToString()+";";
                    }
                    endDesc.InnerText += "top_material = " + plt.top_material + ";";
                    if (plt.wall_template != "") endDesc.InnerText += "wall_template = " + plt.wall_template + ";";
                    if (plt.base_wall_template != "") endDesc.InnerText += "base_wall_template = " + plt.base_wall_template + ";";
                    if (plt.wall_height != -1) endDesc.InnerText += "wall_height = " + plt.wall_height + ";";
                    if (plt.isBridge) endDesc.InnerText += "mode = bridge; ";

                    end.AppendChild(endDesc);

                    pair.AppendChild(end);


                    platformLayer.AppendChild(pair);
                }
            }
            layer.AppendChild(platformLayer);

            //add rocks here
            XmlElement RockLayer = xmlDoc.CreateElement("g");
            RockLayer.SetAttribute("groupmode", inkscapeNs, "layer");
            RockLayer.SetAttribute("id", "layer" + i.ToString() + "rocks");
            RockLayer.SetAttribute("label", inkscapeNs, "rocks");
            RockLayer.SetAttribute("style", "display:inline");
            bool has = false;
            for (int j = 0; j < MetaMap.instance.defaultLayer.mapItems.Count; j++)
            {
                MapItem mi = MetaMap.instance.defaultLayer.mapItems[j];
                if (mi.layerIndex != i) continue;
                Rock rk = mi as Rock;
                if(rk == null) continue;
                has = true;
                XmlElement ekE = xmlDoc.CreateElement("rect");
                ekE.SetAttribute("ry", "10");
                ekE.SetAttribute("rx", "10");
                ekE.SetAttribute("label", inkscapeNs, rk.id);
                ekE.SetAttribute("x", rk.position.x.ToString());
                ekE.SetAttribute("y", rk.position.y.ToString());
                ekE.SetAttribute("height", "8.5977106");
                ekE.SetAttribute("width", "10.030663");
                ekE.SetAttribute("id", rk.id);
                ekE.SetAttribute("style", "opacity:1;fill:#999999;fill-opacity:1;display:inline;enable-background:new");

                XmlElement stpDesc = xmlDoc.CreateElement("desc");
                stpDesc.SetAttribute("id", "desc_" + rk.id);
                stpDesc.InnerText = "rock";
                ekE.AppendChild( stpDesc );

                XmlElement stpTitle = xmlDoc.CreateElement("title");
                stpTitle.SetAttribute("id", "title_" + rk.id);
                stpTitle.InnerText = "rock";
                ekE.AppendChild(stpTitle);

                RockLayer.AppendChild(ekE);
            }
            if (has) layer.AppendChild(RockLayer);


                if (i==1)
            {
                //add spawnpoints here
                XmlElement SpawnPointLayer = xmlDoc.CreateElement("g");
                SpawnPointLayer.SetAttribute("groupmode", inkscapeNs, "layer");
                SpawnPointLayer.SetAttribute("id", "layer" + i.ToString() + "spawnpoints");
                SpawnPointLayer.SetAttribute("label", inkscapeNs, "spawnpoints");
                SpawnPointLayer.SetAttribute("style", "display:none");

                for (int j = 0; j < MetaMap.instance.defaultLayer.mapItems.Count; j++)
                {
                    SpawnPoint sp = MetaMap.instance.defaultLayer.mapItems[j] as SpawnPoint;
                    if (sp != null)
                    {
                        XmlElement spE = xmlDoc.CreateElement("rect");
                        spE.SetAttribute("style", "fill:#0000ff;fill-opacity:1;stroke:none;display:inline;enable-background:new");
                        spE.SetAttribute("id", sp.id);
                        spE.SetAttribute("width", 4.9588485.ToString());
                        spE.SetAttribute("height", 4.4664636.ToString());

                        spE.SetAttribute("x", sp.position.x.ToString());
                        spE.SetAttribute("y", sp.position.y.ToString());
                        spE.SetAttribute("label", inkscapeNs, sp.id);
                        XmlElement buiEDesc = xmlDoc.CreateElement("desc");
                        buiEDesc.SetAttribute("id", "desc" + j.ToString());
                        SpawnPointLayer.AppendChild(spE);
                    }

                }
                layer.AppendChild(SpawnPointLayer);

            }

            rootElement.AppendChild(layer);
        }

        //export bases
        XmlElement basesLayer = xmlDoc.CreateElement("g");
        basesLayer.SetAttribute("groupmode", inkscapeNs, "layer");
        basesLayer.SetAttribute("id", "layerBases");
        basesLayer.SetAttribute("label", inkscapeNs, "bases.default");
        basesLayer.SetAttribute("insensitive",sodipodiNs, "true");
        for (int i = 0; i < MetaMap.instance.baseLayer.mapItems.Count; i++)
        {
            Base cbsi = MetaMap.instance.baseLayer.mapItems[i] as Base;
            XmlElement cbs = xmlDoc.CreateElement("rect");
            cbs.SetAttribute("style", "opacity:0.26966289;fill:#ffff00;fill-opacity:1;stroke:none;display:inline");
            cbs.SetAttribute("id", cbsi.id);
            cbs.SetAttribute("width", (cbsi.size.x).ToString());
            cbs.SetAttribute("height", (cbsi.size.y).ToString());

            cbs.SetAttribute("x", "0");
            cbs.SetAttribute("y", "0");
            cbs.SetAttribute("label", inkscapeNs, "#rect"+ i.ToString() + "_base"  );
            cbs.SetAttribute("transform", MathOfRwrme.angleToTransform(cbsi.rotation, cbsi.position));
            XmlElement cbsEDesc = xmlDoc.CreateElement("desc");
            cbsEDesc.SetAttribute("id", "desc_base" + i.ToString());
            string baseDescStr = "name=" + cbsi._name + ";";
            if(cbsi.factionIndex!=-1)
            {
                baseDescStr = baseDescStr + "faction_index=" + cbsi.factionIndex.ToString() + ";";
            }
            cbsEDesc.InnerText = baseDescStr;
            cbs.AppendChild(cbsEDesc);
            basesLayer.AppendChild(cbs);
        }
        rootElement.AppendChild(basesLayer);


        Debug.Log("MapExport");
        string fullPath = System.IO.Path.Combine(Application.dataPath, basePath, m_mm.m_metaTerrain.fileName);
        System.IO.File.WriteAllBytes(fullPath, m_mm.m_metaTerrain.data.convToPng());


        Debug.Log("MapExporter:exportSVG!");
        xmlDoc.Save(xmlFilePath);
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
        string filePath = Path.Combine(Application.dataPath, basePath, fileName);
        
        // 确保map目录存在
        string mapDir = Path.Combine(Application.dataPath, basePath);
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

    public void exportBuildings()
    {

    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
