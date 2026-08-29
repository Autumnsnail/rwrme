using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;           // .NET 标准 XML
using UnityEngine;

public class MapExporter : MonoBehaviour
{
    // Start is called before the first frame update
    MetaMap m_mm;
    Terrain targetTerrain;
    public static MapExporter ins;

    public XmlDocument xmlDoc;
    
    [Header("导出路径配置")]
    public string basePath = "map"; // 基础路径

    private string FindTemplatePathLikeImporter() => MapImporter.FindFirstTemplateInTemplatesDir();

    static void TryAddTemplateName(ISet<string> set, string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return;
        set.Add(name.Trim());
    }

    static readonly (string key, string title)[] UsedTemplateLogSections =
    {
        ("mesh", "mesh (#mesh template_ref)"),
        ("decal", "decal (#decal template_ref)"),
        ("wall", "wall (template = material)"),
        ("building", "building (material)"),
        ("platform_wall", "platform (wall_template)"),
        ("platform_base_wall", "platform (base_wall_template)"),
    };

    static Dictionary<string, HashSet<string>> CreateUsedTemplateGroups()
    {
        var groups = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        foreach ((string key, _) in UsedTemplateLogSections)
            groups[key] = new HashSet<string>(StringComparer.Ordinal);
        return groups;
    }

    static Dictionary<string, HashSet<string>> CollectUsedTemplateNamesByType()
    {
        var groups = CreateUsedTemplateGroups();
        void ScanItems(IList<MapItem> items)
        {
            if (items == null) return;
            foreach (MapItem mi in items)
            {
                switch (mi)
                {
                    case MeMesh mesh:
                        TryAddTemplateName(groups["mesh"], mesh.template_ref);
                        break;
                    case Decal decal:
                        TryAddTemplateName(groups["decal"], decal.template_ref);
                        break;
                    case Wall wall:
                        TryAddTemplateName(groups["wall"], wall.material);
                        break;
                    case Building building:
                        TryAddTemplateName(groups["building"], building.material);
                        break;
                    case Platform platform:
                        TryAddTemplateName(groups["platform_wall"], platform.wall_template);
                        TryAddTemplateName(groups["platform_base_wall"], platform.base_wall_template);
                        break;
                }
            }
        }

        if (MetaMap.instance != null)
        {
            ScanItems(MetaMap.instance.defaultLayer?.mapItems);
            ScanItems(MetaMap.instance.baseLayer?.mapItems);
            ScanItems(MetaMap.instance.offroadLayer?.mapItems);
        }
        return groups;
    }

    /// <summary>
    /// 编辑器：工程根（Assets 父目录）；独立构建：与 .exe 同级（*_Data 的父目录）。
    /// </summary>
    static string GetExecutableNearbyDirectory()
    {
        string dir = Path.GetDirectoryName(Application.dataPath);
        return string.IsNullOrEmpty(dir) ? Application.dataPath : dir;
    }

    void WriteUsedTemplatesLog(Dictionary<string, HashSet<string>> groups)
    {
        string logPath = Path.Combine(GetExecutableNearbyDirectory(), "used_templates.log");

        int total = groups.Values.Sum(s => s.Count);
        var sb = new StringBuilder();
        sb.AppendLine("# 本图导出时引用的 template 名称（按类型分组，组内按字母序）");
        sb.AppendLine("# " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
        sb.AppendLine("# path: " + logPath);
        sb.AppendLine("# total: " + total.ToString(CultureInfo.InvariantCulture));

        foreach ((string key, string title) in UsedTemplateLogSections)
        {
            if (!groups.TryGetValue(key, out HashSet<string> set) || set.Count == 0)
                continue;
            sb.AppendLine();
            sb.AppendLine("[" + title + "]");
            sb.AppendLine("# count: " + set.Count.ToString(CultureInfo.InvariantCulture));
            foreach (string n in set.OrderBy(x => x, StringComparer.Ordinal))
                sb.AppendLine(n);
        }

        File.WriteAllText(logPath, sb.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        Debug.Log("MapExporter: 已写入 " + total + " 个 template 名称（" + UsedTemplateLogSections.Length + " 类）到 " + logPath);
    }

    /// <summary>
    /// 从模板文档取出 inkscape:label="materials" 的 &lt;g&gt; 深拷贝，并去掉其中 inkscape:label 以 #general 开头的 &lt;rect&gt;。
    /// </summary>
    static XmlElement CloneMaterialsLayerWithoutGeneralRect(XmlDocument templateDoc)
    {
        XmlElement materials = MapImporter.FindMaterialsLayer(templateDoc?.DocumentElement);
        if (materials == null) return null;

        XmlElement clone = (XmlElement)materials.CloneNode(true);
        var remove = new List<XmlNode>();
        foreach (XmlNode child in clone.ChildNodes)
        {
            if (child is XmlElement rect && rect.Name == "rect"
                && rect.GetAttribute("inkscape:label").StartsWith("#general", StringComparison.Ordinal))
                remove.Add(child);
        }
        foreach (XmlNode n in remove)
            clone.RemoveChild(n);
        return clone;
    }

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

    /// <summary>
    /// 首点相对 <c>m</c>；直线段用显式相对 <c>l</c>（不可在 <c>c</c> 后用隐式直线：Unity VectorGraphics 会把隐式数字仍当作 <c>，需 6 个数而报错）。
    /// 曲线为相对 <c>，与 <see cref="SvgPathParser.ParsePathDataToOps"/> / MapImporter 一致。
    /// </summary>
    static string BuildOffroadSvgPathD(Offroad ofr)
    {
        var pts = ofr.positionLine;
        if (pts == null || pts.Count < 2) return string.Empty;

        var sb = new StringBuilder();
        Vector2 penAbs = Vector2.zero;
        bool hasCurve = ofr.curve != null && ofr.curve.Count == pts.Count;
        bool hasCp = ofr.controlPoints != null && ofr.controlPoints.Count >= 2;
        int cpIdx = 0;

        sb.Append('m').Append(' ');
        AppendSvgCoordPair(sb, pts[0].x - penAbs.x, pts[0].y - penAbs.y);
        penAbs = pts[0];

        for (int i = 1; i < pts.Count; i++)
        {
            bool cubic = hasCurve && hasCp && ofr.curve[i] && cpIdx + 1 < ofr.controlPoints.Count;
            if (cubic)
            {
                Vector2 p0 = pts[i - 1];
                Vector2 c1 = ofr.controlPoints[cpIdx++];
                Vector2 c2 = ofr.controlPoints[cpIdx++];
                Vector2 p3 = pts[i];
                sb.Append(" c ");
                AppendSvgCoordPair(sb, c1.x - p0.x, c1.y - p0.y);
                sb.Append(' ');
                AppendSvgCoordPair(sb, c2.x - p0.x, c2.y - p0.y);
                sb.Append(' ');
                AppendSvgCoordPair(sb, p3.x - p0.x, p3.y - p0.y);
                penAbs = p3;
            }
            else
            {
                Vector2 d = pts[i] - penAbs;
                sb.Append(" l ");
                AppendSvgCoordPair(sb, d.x, d.y);
                penAbs = pts[i];
            }
        }

        return sb.ToString();
    }

    static string BuildCurvedMePathSvgPathD(MePath path)
    {
        var pts = path.positionLine;
        if (pts == null || pts.Count < 2) return string.Empty;

        var sb = new StringBuilder();
        Vector2 penAbs = Vector2.zero;
        bool hasCurve = path.curve != null && path.curve.Count == pts.Count;
        bool hasCp = path.controlPoints != null && path.controlPoints.Count >= 2;
        int cpIdx = 0;

        sb.Append('m').Append(' ');
        AppendSvgCoordPair(sb, pts[0].x - penAbs.x, pts[0].y - penAbs.y);
        penAbs = pts[0];

        for (int i = 1; i < pts.Count; i++)
        {
            bool cubic = hasCurve && hasCp && path.curve[i] && cpIdx + 1 < path.controlPoints.Count;
            if (cubic)
            {
                Vector2 p0 = pts[i - 1];
                Vector2 c1 = path.controlPoints[cpIdx++];
                Vector2 c2 = path.controlPoints[cpIdx++];
                Vector2 p3 = pts[i];
                sb.Append(" c ");
                AppendSvgCoordPair(sb, c1.x - p0.x, c1.y - p0.y);
                sb.Append(' ');
                AppendSvgCoordPair(sb, c2.x - p0.x, c2.y - p0.y);
                sb.Append(' ');
                AppendSvgCoordPair(sb, p3.x - p0.x, p3.y - p0.y);
                penAbs = p3;
            }
            else
            {
                Vector2 d = pts[i] - penAbs;
                sb.Append(" l ");
                AppendSvgCoordPair(sb, d.x, d.y);
                penAbs = pts[i];
            }
        }

        return sb.ToString();
    }

    static string BuildPolylineSvgPathD(List<Vector2> pts)
    {
        if (pts == null || pts.Count < 2) return string.Empty;
        var sb = new StringBuilder();
        Vector2 penAbs = Vector2.zero;
        sb.Append('m').Append(' ');
        AppendSvgCoordPair(sb, pts[0].x - penAbs.x, pts[0].y - penAbs.y);
        penAbs = pts[0];
        for (int i = 1; i < pts.Count; i++)
        {
            Vector2 d = pts[i] - penAbs;
            sb.Append(" l ");
            AppendSvgCoordPair(sb, d.x, d.y);
            penAbs = pts[i];
        }
        return sb.ToString();
    }

    void ExportTerrainPathLayer(XmlElement root, string inkscapeNs, string sodipodiNs, string layerLabel, Layer layer, bool isHeight)
    {
        if (layer?.mapItems == null || layer.mapItems.Count == 0) return;

        XmlElement group = xmlDoc.CreateElement("g");
        group.SetAttribute("groupmode", inkscapeNs, "layer");
        group.SetAttribute("id", "layer_" + layerLabel);
        group.SetAttribute("label", inkscapeNs, layerLabel);
        group.SetAttribute("insensitive", sodipodiNs, "true");

        int idx = 0;
        foreach (MapItem mi in layer.mapItems)
        {
            MePath path = mi as MePath;
            if (path?.positionLine == null || path.positionLine.Count < 2) continue;

            string pcd = BuildCurvedMePathSvgPathD(path);
            if (string.IsNullOrWhiteSpace(pcd)) continue;

            XmlElement pathEl = xmlDoc.CreateElement("path");
            pathEl.SetAttribute("label", inkscapeNs, isHeight ? "height_path" : "material_path");
            pathEl.SetAttribute("id", (isHeight ? "height_path_" : "material_path_") + idx);
            pathEl.SetAttribute("d", pcd);
            pathEl.SetAttribute("style", isHeight
                ? "fill:none;stroke:#33ccff;stroke-width:1px;stroke-opacity:1"
                : "fill:none;stroke:#66cc33;stroke-width:1px;stroke-opacity:1");

            XmlElement desc = xmlDoc.CreateElement("desc");
            if (isHeight && mi is HeightPath hp)
            {
                var sb = new StringBuilder();
                sb.Append("width=").Append(hp.width.ToString(CultureInfo.InvariantCulture)).Append(';');
                switch (hp.mode)
                {
                    case HeightPathMode.Offset:
                        sb.Append("height_offset=").Append(hp.heightDelta.ToString(CultureInfo.InvariantCulture)).Append(';');
                        sb.Append("mode=offset;");
                        break;
                    case HeightPathMode.Set:
                        sb.Append("height=").Append(hp.heightDelta.ToString(CultureInfo.InvariantCulture)).Append(';');
                        sb.Append("mode=set;");
                        break;
                    case HeightPathMode.Lower:
                        sb.Append("height_delta=").Append(hp.heightDelta.ToString(CultureInfo.InvariantCulture)).Append(';');
                        sb.Append("mode=lower;");
                        break;
                    default:
                        sb.Append("height_delta=").Append(hp.heightDelta.ToString(CultureInfo.InvariantCulture)).Append(';');
                        sb.Append("mode=raise;");
                        break;
                }
                desc.InnerText = sb.ToString();
            }
            else if (!isHeight && mi is MaterialPath mp)
            {
                desc.InnerText = "width=" + mp.width.ToString(CultureInfo.InvariantCulture) + ";"
                    + "material_index=" + mp.materialIndex + ";"
                    + "hardness=" + mp.hardness.ToString(CultureInfo.InvariantCulture) + ";";
            }
            pathEl.AppendChild(desc);
            group.AppendChild(pathEl);
            idx++;
        }

        if (idx > 0) root.AppendChild(group);
    }

    public void exportPreTerrainHeightmap()
    {
        m_mm.EnsurePreTerrain();
        string filePath = Path.Combine(Application.dataPath, basePath, MetaMap.PreHeightmapFileName);
        File.WriteAllBytes(filePath, m_mm.m_preTerrain.data.convToPng());
        Debug.Log("MapExporter: exported " + MetaMap.PreHeightmapFileName);
    }

    public void exportPreTerrainAlphamap()
    {
        m_mm.EnsurePreCombinedAlpha();
        if (m_mm.preCombinedAlpha == null) return;

        string filePath = Path.Combine(Application.dataPath, basePath, MetaMap.PreCombinedAlphaFileName);
        File.WriteAllBytes(filePath, m_mm.preCombinedAlpha.EncodeToPNG());
        Debug.Log("MapExporter: exported " + MetaMap.PreCombinedAlphaFileName);
    }

    static void AppendSvgCoordPair(StringBuilder sb, float x, float y)
    {
        sb.Append(x.ToString(CultureInfo.InvariantCulture));
        sb.Append(',');
        sb.Append(y.ToString(CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// Unity VectorGraphics 要求 stroke-miterlimit &gt;= 1；Inkscape 模板里常见 0.69999999。
    /// </summary>
    static string SanitizeSvgForUnityImport(string svg)
    {
        if (string.IsNullOrEmpty(svg)) return svg;

        svg = Regex.Replace(
            svg,
            @"stroke-miterlimit:([0-9]*\.?[0-9]+(?:[eE][+-]?[0-9]+)?)",
            m =>
            {
                if (float.TryParse(m.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out float v)
                    && v < 1f)
                    return "stroke-miterlimit:1";
                return m.Value;
            });

        svg = Regex.Replace(
            svg,
            @"stroke-miterlimit=""([0-9]*\.?[0-9]+(?:[eE][+-]?[0-9]+)?)""",
            m =>
            {
                if (float.TryParse(m.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out float v)
                    && v < 1f)
                    return "stroke-miterlimit=\"1\"";
                return m.Value;
            });

        return svg;
    }

    public void exportMap() 
    {
        if (Syncer.instence != null)
            Syncer.instence.updateMap();

        string xmlFilePath = Path.Combine(Application.dataPath, basePath, "objects.svg");
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

        XmlDocument templateDoc = new XmlDocument(); 
        string templatePath = FindTemplatePathLikeImporter();
        if (string.IsNullOrEmpty(templatePath) || !File.Exists(templatePath))
        {
            Debug.LogError("MapExporter: 找不到模板文件（*.xml / *.svg），目录: " + Path.Combine(Application.dataPath, "templates"));
            return;
        }
        XmlReaderSettings settings = new XmlReaderSettings();
        settings.DtdProcessing = DtdProcessing.Ignore;
        settings.ValidationType = ValidationType.None; 
        using (XmlReader reader = XmlReader.Create(templatePath, settings))
        {
            templateDoc.Load(reader);
        }

        XmlElement template = CloneMaterialsLayerWithoutGeneralRect(templateDoc);
        if (template == null)
        {
            Debug.LogError("MapExporter: 模板中未找到 inkscape:label=\"materials\" 的图层");
            return;
        }

        XmlNode importedNode = xmlDoc.ImportNode(template, true);

        XmlElement generalRect = xmlDoc.CreateElement("rect");
        XmlElement grdesc = xmlDoc.CreateElement("desc");
        grdesc.InnerText = MetaMap.instance.m_settings;
        grdesc.SetAttribute("id", "descGeneralSettings");
        generalRect.AppendChild(grdesc);
        generalRect.SetAttribute("label",inkscapeNs, "#general");
        generalRect.SetAttribute("x", "-2030.0349");
        generalRect.SetAttribute("y", "-1.3721894");
        generalRect.SetAttribute("height", "50.507629");
        generalRect.SetAttribute("width", "77.781746");
        generalRect.SetAttribute("id", "rectGeneralSettings");
        generalRect.SetAttribute("style", "fill:#ffd6b0;fill-opacity:1;fill-rule:nonzero;stroke:#000000;stroke-width:5;stroke-linecap:square;stroke-linejoin:round;stroke-miterlimit:4;stroke-opacity:1;stroke-dasharray:none;stroke-dashoffset:0;display:inline;enable-background:new");
        importedNode.AppendChild(generalRect);

        rootElement.AppendChild(importedNode);

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


            //add platforms here
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

            //add posts here
            XmlElement postLayer = xmlDoc.CreateElement("g");
            postLayer.SetAttribute("groupmode", inkscapeNs, "layer");
            postLayer.SetAttribute("id", "layer" + i.ToString() + "posts");
            postLayer.SetAttribute("label", inkscapeNs, "posts");
            postLayer.SetAttribute("style", "display:inline");
            bool hasPosts = false;
            for (int j = 0; j < MetaMap.instance.defaultLayer.mapItems.Count; j++)
            {
                MapItem mi = MetaMap.instance.defaultLayer.mapItems[j];
                if (mi.layerIndex != i) continue;
                Post pt = mi as Post;
                if (pt == null || pt.positionLine == null || pt.positionLine.Count < 2) continue;
                hasPosts = true;

                XmlElement ptE = xmlDoc.CreateElement("path");

                string cmds = new string('c', pt.positionLine.Count);
                ptE.SetAttribute("nodetypes", sodipodiNs, cmds);
                ptE.SetAttribute("label", inkscapeNs, "post");
                ptE.SetAttribute("connector-curvature", inkscapeNs, "0");
                ptE.SetAttribute("id", pt.id);

                string pcd = "m";
                Vector2 pos = Vector2.zero;
                for (int step = 0; step < pt.positionLine.Count; step++)
                {
                    Vector2 shownPos = pt.positionLine[step] - pos;
                    pos = pt.positionLine[step];
                    pcd += " " + shownPos.x.ToString(CultureInfo.InvariantCulture)
                        + "," + shownPos.y.ToString(CultureInfo.InvariantCulture);
                }
                ptE.SetAttribute("d", pcd);
                ptE.SetAttribute("style", "fill:none;stroke:#9d7b00;stroke-width:1px;stroke-linecap:butt;stroke-linejoin:miter;stroke-opacity:1;display:inline;enable-background:new");

                XmlElement ptEDesc = xmlDoc.CreateElement("desc");
                ptEDesc.SetAttribute("id", "desc_" + pt.id);
                ptEDesc.InnerText = "post_template = " + pt.template_ref + ";";
                ptE.AppendChild(ptEDesc);

                postLayer.AppendChild(ptE);
            }
            if (hasPosts) layer.AppendChild(postLayer);

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
                    if(bd.offset != Vector3.zero)
                    {
                        baseDescStr = baseDescStr + "offset = " + bd.offset.x.ToString() + " " + bd.offset.y.ToString() + " " + bd.offset.z.ToString() + ";";
                    }
                    buiEDesc.InnerText = baseDescStr;
                    buiE.AppendChild(buiEDesc);
                    buildingLayer.AppendChild(buiE);
                }

            }
            layer.AppendChild(buildingLayer);

            //add walls here
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
                    if (wl.reHighed) wlEDesc.InnerText += "height = " + wl.reHighedHeight.ToString() + ";";
                    if (wl.merged) wlEDesc.InnerText += "merge = 1;";
                    wlE.AppendChild(wlEDesc);
                    WallLayer.AppendChild(wlE);

                }
            }
            layer.AppendChild(WallLayer);

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
                ekE.SetAttribute("label", inkscapeNs, "rock");
                ekE.SetAttribute("x", 0.ToString());
                ekE.SetAttribute("y", 0.ToString());
                ekE.SetAttribute("transform", MathOfRwrme.angleToTransform(rk.rotation, rk.position));
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

            //add trees here
            XmlElement TreeLayer = xmlDoc.CreateElement("g");
            TreeLayer.SetAttribute("groupmode", inkscapeNs, "layer");
            TreeLayer.SetAttribute("id", "layer" + i.ToString() + "trees");
            TreeLayer.SetAttribute("label", inkscapeNs, "trees");
            TreeLayer.SetAttribute("style", "display:inline");
            has = false;
            for (int j = 0; j < MetaMap.instance.defaultLayer.mapItems.Count; j++)
            {
                MapItem mi = MetaMap.instance.defaultLayer.mapItems[j];
                if (mi.layerIndex != i) continue;
                MeTree tree = mi as MeTree;
                if (tree == null) continue;
                has = true;
                XmlElement ekE = xmlDoc.CreateElement("rect");
                ekE.SetAttribute("label", inkscapeNs, "tree");
                ekE.SetAttribute("x", "0");
                ekE.SetAttribute("y", "0");
                ekE.SetAttribute("height", tree.size.y.ToString());
                ekE.SetAttribute("width", tree.size.x.ToString());
                ekE.SetAttribute("transform", MathOfRwrme.angleToTransform(tree.rotation, tree.position));
                ekE.SetAttribute("id", tree.id);
                ekE.SetAttribute("style", "fill:#009700;fill-opacity:1;stroke:none;display:inline;enable-background:new");

                TreeLayer.AppendChild(ekE);
            }
            if (has) layer.AppendChild(TreeLayer);

            //add templated mesh here
            XmlElement meshLayer = xmlDoc.CreateElement("g");
            meshLayer.SetAttribute("groupmode", inkscapeNs, "layer");
            meshLayer.SetAttribute("id", "layer" + i.ToString() + "meshs");
            meshLayer.SetAttribute("label", inkscapeNs, "meshs");
            meshLayer.SetAttribute("style", "display:inline");
            has = false;
            for (int j = 0; j < MetaMap.instance.defaultLayer.mapItems.Count; j++)
            {
                MapItem mi = MetaMap.instance.defaultLayer.mapItems[j];
                if (mi.layerIndex != i) continue;
                MeMesh ms = mi as MeMesh;
                if (ms == null) continue;
                has = true;
                XmlElement ekE = xmlDoc.CreateElement("rect");
                ekE.SetAttribute("label", inkscapeNs, "#mesh");
                ekE.SetAttribute("x", "0");
                ekE.SetAttribute("y", "0");
                ekE.SetAttribute("height", ms.size.y.ToString());
                ekE.SetAttribute("width", ms.size.x.ToString());
                ekE.SetAttribute("transform", MathOfRwrme.angleToTransform(ms.rotation, ms.position));
                ekE.SetAttribute("id", ms.id);
                ekE.SetAttribute("style", "fill:#ffff00;fill-opacity:1;stroke:#000000;stroke-width:0;stroke-linecap:butt;stroke-linejoin:miter;stroke-miterlimit:4;stroke-opacity:1;stroke-dasharray:none;stroke-dashoffset:0;display:inline;enable-background:new");

                XmlElement stpDesc = xmlDoc.CreateElement("desc");
                stpDesc.SetAttribute("id", "desc_" + ms.id);
                var inv = CultureInfo.InvariantCulture;
                stpDesc.InnerText = "template = "+ms.template_ref+";";
                if(ms.offset != Vector3.zero)
                {
                    stpDesc.InnerText += "offset = " + ms.offset.x.ToString(inv) + " " + ms.offset.y.ToString(inv) + " " + ms.offset.z.ToString(inv) + ";";
                }
                if (ms.reCollision)
                {
                    stpDesc.InnerText += "collision_model_type = 1;";

                    stpDesc.InnerText += "collision_model_size = " + ms.collisionSize.x.ToString(inv)
                        + " " + ms.collisionSize.y.ToString(inv) + " " + ms.collisionSize.z.ToString(inv) + ";";
                }
                ekE.AppendChild(stpDesc);


                meshLayer.AppendChild(ekE);
            }
            if (has) layer.AppendChild(meshLayer);

            //add templated decals here
            XmlElement decalLayer = xmlDoc.CreateElement("g");
            decalLayer.SetAttribute("groupmode", inkscapeNs, "layer");
            decalLayer.SetAttribute("id", "layer" + i.ToString() + "decals");
            decalLayer.SetAttribute("label", inkscapeNs, "decals");
            decalLayer.SetAttribute("style", "display:inline");
            has = false;
            for (int j = 0; j < MetaMap.instance.defaultLayer.mapItems.Count; j++)
            {
                MapItem mi = MetaMap.instance.defaultLayer.mapItems[j];
                if (mi.layerIndex != i) continue;
                Decal decal = mi as Decal;
                if (decal == null) continue;
                has = true;
                XmlElement ekE = xmlDoc.CreateElement("rect");
                ekE.SetAttribute("label", inkscapeNs, "#decal");
                ekE.SetAttribute("x", 0.ToString());

                ekE.SetAttribute("y", 0.ToString());
                Vector2 decalFootprint = decal.GetSvgExportSize();
                ekE.SetAttribute("height", decalFootprint.y.ToString(CultureInfo.InvariantCulture));
                ekE.SetAttribute("width", decalFootprint.x.ToString(CultureInfo.InvariantCulture));
                ekE.SetAttribute("transform", MathOfRwrme.angleToTransform(decal.rotation, decal.position));
                ekE.SetAttribute("id", decal.id);
                ekE.SetAttribute("style", "fill:#ff00ff;fill-opacity:1;stroke:none;display:inline;enable-background:new");
                
                XmlElement stpDesc = xmlDoc.CreateElement("desc");
                stpDesc.SetAttribute("id", "desc_" + decal.id);
                stpDesc.InnerText = "template = "+decal.template_ref+";";
                ekE.AppendChild(stpDesc);

                decalLayer.AppendChild(ekE);
            }
            if (has) layer.AppendChild(decalLayer);

            //add ladders here
            XmlElement LadderLayer = xmlDoc.CreateElement("g");
            LadderLayer.SetAttribute("groupmode", inkscapeNs, "layer");
            LadderLayer.SetAttribute("id", "layer" + i.ToString() + "ladders");
            LadderLayer.SetAttribute("label", inkscapeNs, "ladders");
            LadderLayer.SetAttribute("style", "display:inline");
            has = false;
            for (int j = 0; j < MetaMap.instance.defaultLayer.mapItems.Count; j++)
            {
                MapItem mi = MetaMap.instance.defaultLayer.mapItems[j];
                if (mi.layerIndex != i) continue;
                Ladder ld = mi as Ladder;
                if (ld == null) continue;
                has = true;
                XmlElement ekE = xmlDoc.CreateElement("rect");
                ekE.SetAttribute("label", inkscapeNs, "#ladder");
                ekE.SetAttribute("x", 0.ToString());
                ekE.SetAttribute("y", 0.ToString());
                ekE.SetAttribute("height", "2.25");
                ekE.SetAttribute("width", "6");
                ekE.SetAttribute("transform", MathOfRwrme.angleToTransform(ld.rotation, ld.position));
                ekE.SetAttribute("id", ld.id);
                ekE.SetAttribute("style", "opacity:0.79775277;fill:#2cffe7;fill-opacity:1;fill-rule:nonzero;stroke:none;display:inline;enable-background:new");

                LadderLayer.AppendChild(ekE);
            }
            if (has) layer.AppendChild(LadderLayer);

            //add function items here
            XmlElement folayer = xmlDoc.CreateElement("g");
            folayer.SetAttribute("groupmode", inkscapeNs, "layer");
            folayer.SetAttribute("id", "layer" + i.ToString() + "itemSupply");
            folayer.SetAttribute("label", inkscapeNs, "itemSupply");
            folayer.SetAttribute("style", "display:inline");
            has = false;
            for (int j = 0; j < MetaMap.instance.defaultLayer.mapItems.Count; j++)
            {
                MapItem mi = MetaMap.instance.defaultLayer.mapItems[j];
                if (mi.layerIndex != i) continue;
                ItemSupply ms = mi as ItemSupply;
                if (ms == null) continue;
                has = true;
                XmlElement ekE = xmlDoc.CreateElement("rect");
                ekE.SetAttribute("label", inkscapeNs, "");
                ekE.SetAttribute("x", "0");
                ekE.SetAttribute("y", "0");
                ekE.SetAttribute("height", ms.size.y.ToString());
                ekE.SetAttribute("width", ms.size.x.ToString());
                ekE.SetAttribute("transform", MathOfRwrme.angleToTransform(ms.rotation, ms.position));
                ekE.SetAttribute("id", ms.id);
                ekE.SetAttribute("style", "fill:#de8787;fill-opacity:1;display:inline;enable-background:new");

                XmlElement stpDesc = xmlDoc.CreateElement("desc");
                stpDesc.SetAttribute("id", "desc_" + ms.id);
                stpDesc.InnerText = "type = ";
                if(ms.type == 0)
                {
                    stpDesc.InnerText += "stash;";
                }
                if (ms.type == 1)
                {
                    stpDesc.InnerText += "weapon_rack;";
                }
                ekE.AppendChild(stpDesc);
                folayer.AppendChild(ekE);
            }
            if (has) layer.AppendChild(folayer);

            //add crates here
            XmlElement crateLayer = xmlDoc.CreateElement("g");
            crateLayer.SetAttribute("groupmode", inkscapeNs, "layer");
            crateLayer.SetAttribute("id", "layer" + i.ToString() + "crate");
            crateLayer.SetAttribute("label", inkscapeNs, "crate");
            crateLayer.SetAttribute("style", "display:inline");
            has = false;
            for (int j = 0; j < MetaMap.instance.defaultLayer.mapItems.Count; j++)
            {
                MapItem mi = MetaMap.instance.defaultLayer.mapItems[j];
                if (mi.layerIndex != i) continue;
                Crate ct = mi as Crate;
                if (ct == null) continue;
                has = true;
                XmlElement ekE = xmlDoc.CreateElement("rect");
                ekE.SetAttribute("label", inkscapeNs, "");
                ekE.SetAttribute("x", "0");
                ekE.SetAttribute("y", "0");
                ekE.SetAttribute("height", ct.size.y.ToString());
                ekE.SetAttribute("width", ct.size.x.ToString());
                ekE.SetAttribute("transform", MathOfRwrme.angleToTransform(ct.rotation, ct.position));
                ekE.SetAttribute("id", ct.id);
                ekE.SetAttribute("style", "fill:#ff9955;fill-opacity:1;stroke:none;display:inline;enable-background:new");

                crateLayer.AppendChild(ekE);
            }
            if (has) layer.AppendChild(crateLayer);

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
                        SpawnPointLayer.AppendChild(spE);
                    }
                }
                layer.AppendChild(SpawnPointLayer);

                //add vehicles here
                XmlElement VehicleLayer = xmlDoc.CreateElement("g");
                VehicleLayer.SetAttribute("groupmode", inkscapeNs, "layer");
                VehicleLayer.SetAttribute("id", "layer" + i.ToString() + "vehicles");
                VehicleLayer.SetAttribute("label", inkscapeNs, "vehicles");
                VehicleLayer.SetAttribute("style", "display:none");

                for (int j = 0; j < MetaMap.instance.defaultLayer.mapItems.Count; j++)
                {
                    Vehicle vc = MetaMap.instance.defaultLayer.mapItems[j] as Vehicle;
                    if (vc != null)
                    {
                        XmlElement spE = xmlDoc.CreateElement("rect");
                        spE.SetAttribute("style", "fill:#0000ff;fill-opacity:1;stroke:none;display:inline;enable-background:new");
                        spE.SetAttribute("id", vc.id);
                        spE.SetAttribute("width", 5.7888808.ToString());
                        spE.SetAttribute("height", 6.6684999.ToString());

                        spE.SetAttribute("x", 0.ToString());
                        spE.SetAttribute("y", 0.ToString());
                        spE.SetAttribute("label", "");
                        spE.SetAttribute("transform", MathOfRwrme.angleToTransform(vc.rotation, vc.position));
                        XmlElement vehiDesc = xmlDoc.CreateElement("desc");
                        vehiDesc.SetAttribute("id", "desc_vehicle" + j.ToString());
                        if(vc.taged)
                        {
                            vehiDesc.InnerText = "type = vehicle;tag = " +vc.key+ ";";
                        }
                        else
                        {
                            vehiDesc.InnerText = "type = vehicle;key = " + vc.key + ";";
                        }
                        spE.AppendChild(vehiDesc);
                        VehicleLayer.AppendChild(spE);
                    }
                }
                layer.AppendChild(VehicleLayer);

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

        // export offroad（与 Platform 一致：相对 m + 隐式 l；曲线段为相对 c，与 MapImporter / SvgPathParser 解析一致）
        if (MetaMap.instance.offroadLayer != null && MetaMap.instance.offroadLayer.mapItems != null
            && MetaMap.instance.offroadLayer.mapItems.Count > 0)
        {
            XmlElement offroadGroup = xmlDoc.CreateElement("g");
            offroadGroup.SetAttribute("groupmode", inkscapeNs, "layer");
            offroadGroup.SetAttribute("id", "layer774");//Offroad
            offroadGroup.SetAttribute("label", inkscapeNs, "offroad");
            offroadGroup.SetAttribute("insensitive", sodipodiNs, "true");
            for (int i = 0; i < MetaMap.instance.offroadLayer.mapItems.Count; i++)
            {
                Offroad ofr = MetaMap.instance.offroadLayer.mapItems[i] as Offroad;
                if (ofr == null || ofr.positionLine == null || ofr.positionLine.Count < 2) continue;

                string pcd = BuildOffroadSvgPathD(ofr);
                if (string.IsNullOrWhiteSpace(pcd)) continue;

                XmlElement ofrE = xmlDoc.CreateElement("path");
                string nodetypes = new string('c', ofr.positionLine.Count);
                ofrE.SetAttribute("nodetypes", sodipodiNs, nodetypes);
                ofrE.SetAttribute("label", inkscapeNs, ofr.id + "_offroad");
                ofrE.SetAttribute("connector-curvature", inkscapeNs, "0");
                ofrE.SetAttribute("id", "path"+(i+1)+"_navigation");
                ofrE.SetAttribute("d", pcd);
                ofrE.SetAttribute("style", "fill:none;stroke:#00aa44;stroke-width:1px;stroke-linecap:butt;stroke-linejoin:miter;stroke-opacity:1;display:inline;enable-background:new");
                offroadGroup.AppendChild(ofrE);
            }
            rootElement.AppendChild(offroadGroup);
        }

        ExportTerrainPathLayer(rootElement, inkscapeNs, sodipodiNs, "height_paths", MetaMap.instance.heightPathLayer, true);
        ExportTerrainPathLayer(rootElement, inkscapeNs, sodipodiNs, "material_paths", MetaMap.instance.materialPathLayer, false);

        Debug.Log("MapExport");
        xmlDoc.Save(xmlFilePath);

        string xmlContent = File.ReadAllText(xmlFilePath);

        xmlContent = xmlContent.Replace("inkscape:label=\"#general\"", "\ninkscape:label=\"#general\"");
        xmlContent = SanitizeSvgForUnityImport(xmlContent);

        File.WriteAllText(xmlFilePath, xmlContent);

        WriteUsedTemplatesLog(CollectUsedTemplateNamesByType());

        exportPreTerrainHeightmap();
        exportPreTerrainAlphamap();
        exportTerrainHeightmap();
        exportTerrainAlphamap();
        exportMapConfig();

        AudioSource audioSource = GetComponent<AudioSource>();
        if (audioSource != null)
        {
            audioSource.PlayOneShot(audioSource.clip);
        }

//        exportMapViewPic(); it takes times
    }

    /// <summary>
    /// 导出地形高度图
    /// </summary>
    public void exportTerrainHeightmap()
    {
        Debug.Log("=== 开始导出成品地形高度图 (pre_ + height_paths) ===");

        m_mm.EnsurePreTerrain();
        GrayScaleImage baked = m_mm.BakeFinalHeightmap();
        if (baked == null || baked.Width <= 0)
        {
            Debug.LogError("无法导出地形高度图：烘焙结果为空");
            return;
        }

        string filePath = Path.Combine(Application.dataPath, basePath, MetaMap.FinalHeightmapFileName);
        File.WriteAllBytes(filePath, baked.convToPng());
        Debug.Log("MapExporter: exported " + MetaMap.FinalHeightmapFileName + " (pre + height paths)");
#if UNITY_EDITOR
        UnityEditor.AssetDatabase.Refresh();
#endif
    }

    public void exportTerrainAlphamap()
    {
        m_mm.EnsurePreCombinedAlpha();
        Texture2D baked = m_mm.BakeFinalCombinedAlpha();
        if (baked == null)
        {
            Debug.LogError("无法导出 combined alpha：烘焙结果为空");
            return;
        }

        int width = baked.width;
        int height = baked.height;
        Color32[] srcPixels = baked.GetPixels32();

        string filePath = Path.Combine(Application.dataPath, basePath, MetaMap.FinalCombinedAlphaFileName);
        File.WriteAllBytes(filePath, baked.EncodeToPNG());

        for (int i = 1; i < 5; i++)
        {
            string fileName = MetaMap.instance.terrainAlphaFileName[i];
            Texture2D channelTex = new Texture2D(width, height, TextureFormat.R8, false);
            Color32[] channelPixels = new Color32[srcPixels.Length];
            for (int p = 0; p < srcPixels.Length; p++)
            {
                byte value = 0;
                switch (i)
                {
                    case 1: value = srcPixels[p].r; break;
                    case 2: value = srcPixels[p].g; break;
                    case 3: value = srcPixels[p].b; break;
                    case 4: value = srcPixels[p].a; break;
                }
                channelPixels[p] = new Color32(value, value, value, 255);
            }
            channelTex.SetPixels32(channelPixels);
            channelTex.Apply();
            filePath = Path.Combine(Application.dataPath, basePath, fileName);
            File.WriteAllBytes(filePath, channelTex.EncodeToPNG());
        }

        Debug.Log("MapExporter: exported " + MetaMap.FinalCombinedAlphaFileName + " (pre + material paths)");
#if UNITY_EDITOR
        UnityEditor.AssetDatabase.Refresh();
#endif
    }

    public void exportMapConfig()
    {
        string filePath = Path.Combine(Application.dataPath, basePath, "map_config.xml");
        if(m_mm.m_metaMapConfig!=null)
        {
            if(m_mm.m_metaMapConfig.includeLayers.Find(x=>x=="bases.default")==null)
            {
                m_mm.m_metaMapConfig.includeLayers.Add("bases.default");
            }
        }
        //use xml method to export map config
        XmlDocument xmlDoc = new XmlDocument();
        XmlElement root = xmlDoc.CreateElement("map_config");
        root.SetAttribute("min_factions", m_mm.m_metaMapConfig.minFactions.ToString());
        root.SetAttribute("max_factions", m_mm.m_metaMapConfig.maxFactions.ToString());
        root.SetAttribute("add_neutral_last", m_mm.m_metaMapConfig.addNeutralLast.ToString());
        foreach(string layer in m_mm.m_metaMapConfig.includeLayers)
        {
            XmlElement includeLayer = xmlDoc.CreateElement("include_layer");
            includeLayer.SetAttribute("name", layer);
            root.AppendChild(includeLayer);
        }
        xmlDoc.AppendChild(root);
        foreach(string faction in m_mm.m_metaMapConfig.factionFiles)
        {
            XmlElement factionElement = xmlDoc.CreateElement("faction");
            factionElement.SetAttribute("file", faction);
            root.AppendChild(factionElement);
        }
        //weapon 
        XmlElement weaponsElement = xmlDoc.CreateElement("weapon");
        weaponsElement.SetAttribute("file", m_mm.m_metaMapConfig.weaponFile);
        root.AppendChild(weaponsElement);
        //projectiles
        XmlElement projectilesElement = xmlDoc.CreateElement("projectile");
        projectilesElement.SetAttribute("file", m_mm.m_metaMapConfig.projectileFile);
        root.AppendChild(projectilesElement);
        //calls
        XmlElement callsElement = xmlDoc.CreateElement("call");
        callsElement.SetAttribute("file", m_mm.m_metaMapConfig.callFile);
        root.AppendChild(callsElement);
        //carry_items
        XmlElement carryItemsElement = xmlDoc.CreateElement("carry_item");
        carryItemsElement.SetAttribute("file", m_mm.m_metaMapConfig.carryItemFile);
        root.AppendChild(carryItemsElement);
        //vehicles
        XmlElement vehiclesElement = xmlDoc.CreateElement("vehicle");
        vehiclesElement.SetAttribute("file", m_mm.m_metaMapConfig.vehicleFile);
        root.AppendChild(vehiclesElement);
        xmlDoc.Save(filePath);
    }

    public void exportMapViewPic()
    {
        const int outputSize = 2048;
        const int mapViewSuperSample = 2;
        // 下采样后对副本做可分离模糊，再与锐图 lerp，越大过渡越柔和（0 关闭混合）。
        const float mapViewPostSoftenBlend = 0.42f;
        // 模糊副本时的 5 抽头二项式模糊次数（每次含横+竖一遍）。
        const int mapViewPostBlurPasses = 2;
        int drawSize = outputSize * mapViewSuperSample;
        const float contourIntervalM = 10f;
        string filePath = Path.Combine(Application.dataPath, basePath, "map_view_ls.png");

        string mapDir = Path.Combine(Application.dataPath, basePath);
        if (!Directory.Exists(mapDir))
            Directory.CreateDirectory(mapDir);

        if (targetTerrain == null)
        {
            targetTerrain = Terrain.activeTerrain;
            if (targetTerrain == null)
                targetTerrain = FindObjectOfType<Terrain>();
        }

        float maxHeight = m_mm.m_metaTerrain.maxHeight;
        float waterLevel = m_mm.m_metaTerrain.waterHeight;
        float[,] srcHeights = null;
        int hres = 0;

        if (targetTerrain != null)
        {
            TerrainData terrainData = targetTerrain.terrainData;
            hres = terrainData.heightmapResolution;
            maxHeight = terrainData.size.y;
            srcHeights = terrainData.GetHeights(0, 0, hres, hres);
        }
        else if (m_mm.m_metaTerrain.data.Width > 0)
        {
            hres = m_mm.m_metaTerrain.resolutionX;
            srcHeights = new float[hres, hres];
            for (int y = 0; y < hres; y++)
                for (int x = 0; x < hres; x++)
                    srcHeights[y, x] = m_mm.m_metaTerrain.data[y, x];
        }
        else
        {
            Debug.LogError("MapExporter: 无法导出 map_view_ls.png，未找到地形高度数据");
            return;
        }

        float terrainSpan = targetTerrain != null ? targetTerrain.terrainData.size.x : 1024f;

        var waterColor = new Color32(0x77, 0xAE, 0xD5, 255);
        var landColor = new Color32(255, 255, 255, 255);
        var bMaskOverlayColor = new Color32(0xD5, 0xD5, 0xD5, 255);
        var aMaskOverlayColor = new Color32(0x48, 0x48, 0x48, 255);
        var lineColor = new Color32(0, 0, 0, 255);

        float[] worldHeights = new float[drawSize * drawSize];
        Color32[] pixels = new Color32[drawSize * drawSize];

        for (int y = 0; y < drawSize; y++)
        {
            float ny = y / (float)(drawSize - 1);
            for (int x = 0; x < drawSize; x++)
            {
                float nx = x / (float)(drawSize - 1);
                float heightM = SampleWorldHeightMeters(srcHeights, hres, maxHeight, nx, ny);
                int idx = y * drawSize + x;
                worldHeights[idx] = heightM;
                pixels[idx] = landColor;
            }
        }

        DrawMapViewTreeDensityYStripes(pixels, drawSize, terrainSpan);

        Texture2D maskTex = null;
        Color32[] maskPixels = null;
        int maskW = 0, maskH = 0;
        Terrain activeTerrain = Terrain.activeTerrain;
        if (activeTerrain != null && activeTerrain.materialTemplate != null)
            maskTex = activeTerrain.materialTemplate.GetTexture("_Mask") as Texture2D;
        if (maskTex != null)
        {
            maskW = maskTex.width;
            maskH = maskTex.height;
            maskPixels = maskTex.GetPixels32();
        }
        else
            Debug.LogWarning("MapExporter: 未找到地形 _Mask，map_view_ls 跳过 B/A 通道遮罩");

        if (maskPixels != null)
        {
            const byte maskThreshold = 128;
            for (int y = 0; y < drawSize; y++)
            {
                float ny = y / (float)(drawSize - 1);
                for (int x = 0; x < drawSize; x++)
                {
                    float nx = x / (float)(drawSize - 1);
                    int idx = y * drawSize + x;

                    byte rawB = SampleTextureBlueChannel(maskPixels, maskW, maskH, nx, ny);
                    if (rawB >= maskThreshold)
                        pixels[idx] = bMaskOverlayColor;

                    byte rawA = SampleTextureAlphaChannel(maskPixels, maskW, maskH, nx, ny);
                    if (rawA < maskThreshold)
                        pixels[idx] = aMaskOverlayColor;
                }
            }
        }

        DrawMapViewBases(pixels, drawSize, terrainSpan);

        for (int y = 0; y < drawSize; y++)
        {
            float ny = y / (float)(drawSize - 1);
            for (int x = 0; x < drawSize; x++)
            {
                float nx = x / (float)(drawSize - 1);
                int idx = y * drawSize + x;
                if (worldHeights[idx] < waterLevel)
                    pixels[idx] = waterColor;
            }
        }

        for (int y = 0; y < drawSize; y++)
        {
            for (int x = 0; x < drawSize; x++)
            {
                int idx = y * drawSize + x;
                float h = worldHeights[idx];

                if (x + 1 < drawSize)
                {
                    float hRight = worldHeights[idx + 1];
                    if (MapViewCrossesWaterLine(h, hRight, waterLevel) ||
                        MapViewCrossesLandContour(h, hRight, waterLevel, contourIntervalM))
                        pixels[idx] = lineColor;
                }

                if (y + 1 < drawSize)
                {
                    float hUp = worldHeights[idx + drawSize];
                    if (MapViewCrossesWaterLine(h, hUp, waterLevel) ||
                        MapViewCrossesLandContour(h, hUp, waterLevel, contourIntervalM))
                        pixels[idx] = lineColor;
                }
            }
        }

        DrawMapViewPlatforms(pixels, drawSize, terrainSpan, lineColor);
        DrawMapViewBuildings(pixels, drawSize, terrainSpan, lineColor);
        DrawMapViewWalls(pixels, drawSize, terrainSpan, lineColor);
        DrawMapViewRockMeshes(pixels, drawSize, terrainSpan);

        Color32[] outputPixels = MapViewDownsampleBoxAverage(pixels, drawSize, outputSize);
        if (mapViewPostSoftenBlend > 0f && mapViewPostBlurPasses > 0)
        {
            var blurred = new Color32[outputPixels.Length];
            System.Array.Copy(outputPixels, blurred, outputPixels.Length);
            MapViewApplySeparableBinomialBlur5Passes(blurred, outputSize, outputSize, mapViewPostBlurPasses);
            MapViewLerpRgbInPlace(outputPixels, blurred, mapViewPostSoftenBlend);
        }

        Texture2D exportTexture = new Texture2D(outputSize, outputSize, TextureFormat.RGBA32, false);
        exportTexture.SetPixels32(outputPixels);
        exportTexture.Apply();

        byte[] pngData = exportTexture.EncodeToPNG();
        File.WriteAllBytes(filePath, pngData);
        DestroyImmediate(exportTexture);

        Debug.Log($"MapExporter: map_view_ls.png 已导出至 {filePath} ({outputSize}x{outputSize}, 超采样 x{mapViewSuperSample} 抗锯齿, 柔和后处理 blend={mapViewPostSoftenBlend} blurPasses={mapViewPostBlurPasses}, 水位={waterLevel}m, 等高线间隔={contourIntervalM}m)");

#if UNITY_EDITOR
        UnityEditor.AssetDatabase.Refresh();
#endif
    }

    static Color32[] MapViewDownsampleBoxAverage(Color32[] src, int srcSize, int dstSize)
    {
        if (srcSize % dstSize != 0)
        {
            Debug.LogError($"MapViewDownsampleBoxAverage: srcSize {srcSize} 不能被 dstSize {dstSize} 整除");
            return src;
        }
        int f = srcSize / dstSize;
        int area = f * f;
        var dst = new Color32[dstSize * dstSize];
        for (int oy = 0; oy < dstSize; oy++)
        {
            for (int ox = 0; ox < dstSize; ox++)
            {
                int r = 0, g = 0, b = 0, a = 0;
                for (int dy = 0; dy < f; dy++)
                {
                    int iy = oy * f + dy;
                    for (int dx = 0; dx < f; dx++)
                    {
                        int ix = ox * f + dx;
                        Color32 c = src[iy * srcSize + ix];
                        r += c.r;
                        g += c.g;
                        b += c.b;
                        a += c.a;
                    }
                }
                dst[oy * dstSize + ox] = new Color32((byte)(r / area), (byte)(g / area), (byte)(b / area), (byte)(a / area));
            }
        }
        return dst;
    }

    static void MapViewApplySeparableBinomialBlur5Passes(Color32[] buf, int w, int h, int passes)
    {
        if (passes <= 0) return;
        var scratch = new Color32[w * h];
        for (int p = 0; p < passes; p++)
        {
            MapViewBlurHorizontalBinomial5(buf, scratch, w, h);
            MapViewBlurVerticalBinomial5(scratch, buf, w, h);
        }
    }

    static void MapViewBlurHorizontalBinomial5(Color32[] src, Color32[] dst, int w, int h)
    {
        for (int y = 0; y < h; y++)
        {
            int row = y * w;
            for (int x = 0; x < w; x++)
            {
                int r = 0, g = 0, b = 0, a = 0;
                for (int k = -2; k <= 2; k++)
                {
                    int wgt = (k == -2 || k == 2) ? 1 : (k == -1 || k == 1) ? 4 : 6;
                    int xi = Mathf.Clamp(x + k, 0, w - 1);
                    Color32 c = src[row + xi];
                    r += c.r * wgt;
                    g += c.g * wgt;
                    b += c.b * wgt;
                    a += c.a * wgt;
                }
                dst[row + x] = new Color32((byte)(r >> 4), (byte)(g >> 4), (byte)(b >> 4), (byte)(a >> 4));
            }
        }
    }

    static void MapViewBlurVerticalBinomial5(Color32[] src, Color32[] dst, int w, int h)
    {
        for (int y = 0; y < h; y++)
        {
            int row = y * w;
            for (int x = 0; x < w; x++)
            {
                int r = 0, g = 0, b = 0, a = 0;
                for (int k = -2; k <= 2; k++)
                {
                    int wgt = (k == -2 || k == 2) ? 1 : (k == -1 || k == 1) ? 4 : 6;
                    int yi = Mathf.Clamp(y + k, 0, h - 1);
                    Color32 c = src[yi * w + x];
                    r += c.r * wgt;
                    g += c.g * wgt;
                    b += c.b * wgt;
                    a += c.a * wgt;
                }
                dst[row + x] = new Color32((byte)(r >> 4), (byte)(g >> 4), (byte)(b >> 4), (byte)(a >> 4));
            }
        }
    }

    static void MapViewLerpRgbInPlace(Color32[] dst, Color32[] other, float t)
    {
        if (t <= 0f) return;
        t = Mathf.Clamp01(t);
        int tm = Mathf.RoundToInt(255f * t);
        int om = 255 - tm;
        for (int i = 0; i < dst.Length; i++)
        {
            Color32 a = dst[i];
            Color32 b = other[i];
            dst[i] = new Color32(
                (byte)((a.r * om + b.r * tm + 127) / 255),
                (byte)((a.g * om + b.g * tm + 127) / 255),
                (byte)((a.b * om + b.b * tm + 127) / 255),
                a.a);
        }
    }

    static float SampleWorldHeightMeters(float[,] heights, int hres, float maxHeight, float nx, float ny)
    {
        float fx = Mathf.Clamp01(nx) * (hres - 1);
        float fy = Mathf.Clamp01(ny) * (hres - 1);
        int x0 = Mathf.FloorToInt(fx);
        int y0 = Mathf.FloorToInt(fy);
        int x1 = Mathf.Min(x0 + 1, hres - 1);
        int y1 = Mathf.Min(y0 + 1, hres - 1);
        float tx = fx - x0;
        float ty = fy - y0;

        float h00 = heights[y0, x0];
        float h10 = heights[y0, x1];
        float h01 = heights[y1, x0];
        float h11 = heights[y1, x1];
        float normalized = Mathf.Lerp(Mathf.Lerp(h00, h10, tx), Mathf.Lerp(h01, h11, tx), ty);
        return normalized * maxHeight;
    }

    static bool MapViewCrossesWaterLine(float h0, float h1, float waterLevel)
    {
        return (h0 < waterLevel) != (h1 < waterLevel);
    }

    static bool MapViewCrossesLandContour(float h0, float h1, float waterLevel, float intervalM)
    {
        if (h0 < waterLevel || h1 < waterLevel)
            return false;

        return Mathf.FloorToInt(h0 / intervalM) != Mathf.FloorToInt(h1 / intervalM);
    }

    static byte SampleTextureBlueChannel(Color32[] pixels, int w, int h, float nx, float ny)
    {
        return SampleTextureChannel(pixels, w, h, nx, ny, c => c.b);
    }

    static byte SampleTextureAlphaChannel(Color32[] pixels, int w, int h, float nx, float ny)
    {
        return SampleTextureChannel(pixels, w, h, nx, ny, c => c.a);
    }

    static byte SampleTextureChannel(Color32[] pixels, int w, int h, float nx, float ny, System.Func<Color32, byte> channel)
    {
        int x = Mathf.Clamp(Mathf.RoundToInt(Mathf.Clamp01(nx) * (w - 1)), 0, w - 1);
        int y = Mathf.Clamp(Mathf.RoundToInt(Mathf.Clamp01(ny) * (h - 1)), 0, h - 1);
        return channel(pixels[y * w + x]);
    }

    void DrawMapViewPlatforms(Color32[] pixels, int size, float terrainSpan, Color32 strokeColor)
    {
        if (m_mm?.defaultLayer?.mapItems == null) return;

        var fillColor = new Color32(0xE6, 0xE6, 0xE6, 255);

        foreach (MapItem mi in m_mm.defaultLayer.mapItems)
        {
            if (mi is not Platform plt) continue;
            if (plt.positinLineL == null || plt.positinLineR == null) continue;

            int n = Mathf.Min(plt.positinLineL.Count, plt.positinLineR.Count);
            if (n < 2) continue;

            var left = new List<Vector2>(n);
            var right = new List<Vector2>(n);
            for (int i = 0; i < n; i++)
            {
                left.Add(SvgPosToMapPixel(plt.positinLineL[i], size, terrainSpan));
                right.Add(SvgPosToMapPixel(plt.positinLineR[i], size, terrainSpan));
            }

            if (!string.Equals(plt.top_material, "terrain", System.StringComparison.OrdinalIgnoreCase))
            {
                for (int i = 0; i < n - 1; i++)
                    FillMapViewQuad(pixels, size, left[i], right[i],  right[i + 1],left[i + 1], fillColor);
            }

            if (plt.isBridge)
            {
                DrawMapViewSegment2px(pixels, size, left[0], right[0], strokeColor);
                DrawMapViewSegment2px(pixels, size, left[n - 1], right[n - 1], strokeColor);
            }
            else if (plt.isDeck)
            {
                DrawMapViewPolyline2px(pixels, size, left, strokeColor);
                DrawMapViewPolyline2px(pixels, size, right, strokeColor);
                DrawMapViewSegment2px(pixels, size, left[0], right[0], strokeColor);
                DrawMapViewSegment2px(pixels, size, left[n - 1], right[n - 1], strokeColor);
            }
            else
            {
                DrawMapViewPolyline2px(pixels, size, left, strokeColor);
                DrawMapViewSegment2px(pixels, size, left[0], right[0], strokeColor);
                DrawMapViewSegment2px(pixels, size, left[n - 1], right[n - 1], strokeColor);
            }
        }
    }

    static Vector2 SvgPosToMapPixel(Vector2 svg, int size, float terrainSpan)
    {
        return U3dPosToMapPixel(MathOfRwrme.SvgPosToU3dPos(svg), size, terrainSpan);
    }

    static Vector2 U3dPosToMapPixel(Vector2 u3d, int size, float terrainSpan)
    {
        return new Vector2(
            u3d.x / terrainSpan * (size - 1),
            u3d.y / terrainSpan * (size - 1));
    }

    void DrawMapViewBuildings(Color32[] pixels, int size, float terrainSpan, Color32 strokeColor)
    {
        if (m_mm?.defaultLayer?.mapItems == null) return;

        var buildings = new List<Building>();
        foreach (MapItem mi in m_mm.defaultLayer.mapItems)
        {
            if (mi is Building bld)
                buildings.Add(bld);
        }
        if (buildings.Count == 0) return;

        var fillColor = new Color32(0x80, 0x80, 0x80, 255);
        // 绘制顺序：layerIndex 更小先画，同层则 height 更小先画；更大 layerIndex 视为更“高”，叠在上层。
        var byLayerHeight = new Dictionary<(int layerIndex, int height), List<Building>>();

        foreach (Building bld in buildings)
        {
            var key = (bld.layerIndex, bld.height);
            if (!byLayerHeight.TryGetValue(key, out List<Building> group))
            {
                group = new List<Building>();
                byLayerHeight[key] = group;
            }
            group.Add(bld);
        }

        var sortedKeys = new List<(int layerIndex, int height)>(byLayerHeight.Keys);
        sortedKeys.Sort((a, b) =>
        {
            int c = a.layerIndex.CompareTo(b.layerIndex);
            return c != 0 ? c : a.height.CompareTo(b.height);
        });

        var heightMask = new bool[size * size];
        foreach (var key in sortedKeys)
        {
            foreach (Building bld in byLayerHeight[key])
            {
                Vector2[] corners = GetBuildingMapCorners(bld, size, terrainSpan);
                FillMapViewQuad(pixels, size, corners[0], corners[1], corners[2], corners[3], fillColor);
            }

            System.Array.Clear(heightMask, 0, heightMask.Length);
            foreach (Building bld in byLayerHeight[key])
            {
                Vector2[] corners = GetBuildingMapCorners(bld, size, terrainSpan);
                FillMapViewQuadToMask(heightMask, size, corners[0], corners[1], corners[2], corners[3]);
            }
            OutlineMapViewMask1px(pixels, size, heightMask, strokeColor);
        }
    }

    static Vector2[] GetBuildingMapCorners(Building bld, int size, float terrainSpan)
    {
        return GetMeRectMapFootprintCorners(bld, size, terrainSpan);
    }

    /// <summary>与 Building.scatterThis 一致的 MeRect 俯视 footprint 四角（U3D 平面映射到贴图像素）。</summary>
    static Vector2[] GetMeRectMapFootprintCorners(MeRect r, int size, float terrainSpan)
    {
        Vector2 root = MathOfRwrme.SvgPosToU3dPos(r.position);
        float halfW = r.size.x * 0.5f;
        float halfD = r.size.y * 0.5f;
        Vector3[] localCorners =
        {
            new Vector3(0f, 0f, 0f),
            new Vector3(halfW, 0f, 0f),
            new Vector3(halfW, 0f, -halfD),
            new Vector3(0f, 0f, -halfD)
        };

        Quaternion rot = Quaternion.Euler(0f, -r.rotation, 0f);
        var corners = new Vector2[4];
        for (int i = 0; i < 4; i++)
        {
            Vector3 offset = rot * localCorners[i];
            corners[i] = U3dPosToMapPixel(root + new Vector2(offset.x, offset.z), size, terrainSpan);
        }
        return corners;
    }

    void DrawMapViewBases(Color32[] pixels, int size, float terrainSpan)
    {
        if (m_mm?.baseLayer?.mapItems == null) return;

        var dashColor = new Color32(0x7F, 0x7F, 0x7F, 255);
        const int dashOn = 4;
        const int dashOff = 4;

        foreach (MapItem mi in m_mm.baseLayer.mapItems)
        {
            if (mi is not Base bs) continue;
            Vector2[] corners = GetMeRectMapFootprintCorners(bs, size, terrainSpan);
            float arcS = 0f;
            for (int e = 0; e < 4; e++)
                MapViewBresenhamDashedStroke4px(pixels, size, corners[e], corners[(e + 1) % 4], ref arcS, dashOn, dashOff, dashColor);
        }
    }

    static void MapViewBresenhamDashedStroke4px(Color32[] pixels, int size, Vector2 from, Vector2 to, ref float arcS, int dashOn, int dashOff, Color32 col)
    {
        int x0 = Mathf.RoundToInt(from.x);
        int y0 = Mathf.RoundToInt(from.y);
        int x1 = Mathf.RoundToInt(to.x);
        int y1 = Mathf.RoundToInt(to.y);

        float edx = to.x - from.x;
        float edy = to.y - from.y;
        int period = dashOn + dashOff;
        if (period <= 0) return;

        int dx = Mathf.Abs(x1 - x0);
        int dy = Mathf.Abs(y1 - y0);
        int sx = x0 < x1 ? 1 : -1;
        int sy = y0 < y1 ? 1 : -1;
        int err = dx - dy;

        while (true)
        {
            int sMod = Mathf.FloorToInt(arcS);
            if ((sMod % period) < dashOn)
                StampMapViewStroke4pxPerp(pixels, size, x0, y0, edx, edy, col);

            if (x0 == x1 && y0 == y1) break;
            int e2 = 2 * err;
            if (e2 > -dy)
            {
                err -= dy;
                x0 += sx;
            }
            if (e2 < dx)
            {
                err += dx;
                y0 += sy;
            }
            arcS += 1f;
        }
    }

    static void StampMapViewStroke4pxPerp(Color32[] pixels, int size, int ix, int iy, float segDx, float segDy, Color32 col)
    {
        float el = Mathf.Sqrt(segDx * segDx + segDy * segDy);
        if (el < 1e-4f) return;
        float px = -segDy / el;
        float py = segDx / el;
        for (int w = -2; w <= 1; w++)
        {
            int ox = Mathf.RoundToInt(ix + px * w);
            int oy = Mathf.RoundToInt(iy + py * w);
            SetMapViewPixel(pixels, size, ox, oy, col);
        }
    }

    void DrawMapViewWalls(Color32[] pixels, int size, float terrainSpan, Color32 strokeColor)
    {
        if (m_mm?.defaultLayer?.mapItems == null) return;

        foreach (MapItem mi in m_mm.defaultLayer.mapItems)
        {
            if (mi is not Wall wl) continue;
            if (wl.positionLine == null || wl.positionLine.Count < 2) continue;

            for (int i = 0; i < wl.positionLine.Count - 1; i++)
            {
                Vector2 p0 = SvgPosToMapPixel(wl.positionLine[i], size, terrainSpan);
                Vector2 p1 = SvgPosToMapPixel(wl.positionLine[i + 1], size, terrainSpan);
                DrawMapViewSegment2px(pixels, size, p0, p1, strokeColor);
            }
        }
    }

    void DrawMapViewRockMeshes(Color32[] pixels, int size, float terrainSpan)
    {
        if (m_mm?.defaultLayer?.mapItems == null) return;

        const int radiusPx = 3;
        var fillColor = new Color32(0x91, 0x91, 0x91, 255);

        foreach (MapItem mi in m_mm.defaultLayer.mapItems)
        {
            MeRect rect = null;
            if (mi is Rock rock)
                rect = rock;
            else if (mi is MeMesh msm
                     && !string.IsNullOrEmpty(msm.template_ref)
                     && msm.template_ref.StartsWith("rock", System.StringComparison.OrdinalIgnoreCase))
                rect = msm;

            if (rect == null) continue;

            Vector2 centerSvg = MeRectRectCenterSvg(rect);
            Vector2 centerPx = SvgPosToMapPixel(centerSvg, size, terrainSpan);
            FillMapViewCircle(pixels, size, centerPx, radiusPx, fillColor);
        }
    }

    /// <summary>
    /// 统计 defaultLayer 中 MeTree，以及 template_ref 含 “tree” 子串的 MeMesh，按粗网格密度分三档；
    /// 很少不画，适中与浓密沿图像 Y 画 1 像素竖线（X 间隔 7px）。密度经盒式模糊 + 像素双线性采样。
    /// 在水泥(B)/路面(A)遮罩之前绘制。
    /// </summary>
    void DrawMapViewTreeDensityYStripes(Color32[] pixels, int size, float terrainSpan)
    {
        if (m_mm?.defaultLayer?.mapItems == null) return;

        const int gridN = 64;
        int cell = Mathf.Max(1, (size + gridN - 1) / gridN);
        int[,] cnt = new int[gridN, gridN];

        foreach (MapItem mi in m_mm.defaultLayer.mapItems)
        {
            MeRect rect = null;
            if (mi is MeTree tr)
                rect = tr;
            else if (mi is MeMesh msm
                     && !string.IsNullOrEmpty(msm.template_ref)
                     && msm.template_ref.IndexOf("tree", System.StringComparison.OrdinalIgnoreCase) >= 0)
                rect = msm;

            if (rect == null) continue;

            Vector2 px = SvgPosToMapPixel(MeRectRectCenterSvg(rect), size, terrainSpan);
            int gx = Mathf.Clamp((int)(px.x / cell), 0, gridN - 1);
            int gy = Mathf.Clamp((int)(px.y / cell), 0, gridN - 1);
            cnt[gy, gx]++;
        }

        float[,] smooth = new float[gridN, gridN];
        float[,] work = new float[gridN, gridN];
        for (int gy = 0; gy < gridN; gy++)
        {
            for (int gx = 0; gx < gridN; gx++)
                smooth[gy, gx] = cnt[gy, gx];
        }

        for (int pass = 0; pass < 2; pass++)
        {
            BoxBlurGridFloat(smooth, work, gridN);
            var tmp = smooth;
            smooth = work;
            work = tmp;
        }

        var flat = new float[gridN * gridN];
        int k = 0;
        for (int gy = 0; gy < gridN; gy++)
        {
            for (int gx = 0; gx < gridN; gx++)
                flat[k++] = smooth[gy, gx];
        }
        System.Array.Sort(flat);

        if (flat[flat.Length - 1] <= 0f)
            return;

        float v33;
        float v66;
        if (flat[0] >= flat[flat.Length - 1] - 1e-6f && flat[0] <= flat[flat.Length - 1] + 1e-6f)
        {
            v33 = flat[0] - 1f;
            v66 = flat[0];
        }
        else
        {
            int i33 = (int)((flat.Length - 1) * 0.33f);
            int i66 = Mathf.Max(i33 + 1, (int)((flat.Length - 1) * 0.66f));
            i66 = Mathf.Min(i66, flat.Length - 1);
            v33 = flat[i33];
            v66 = flat[i66];
            if (v66 <= v33)
                v66 = Mathf.Min(flat[flat.Length - 1], v33 + 1f);
        }

        var moderateColor = new Color32(0xE1, 0xE1, 0xE1, 255);
        var denseColor = new Color32(0x98, 0x98, 0x98, 255);
        const int stripeStepX = 7;

        for (int y = 0; y < size; y++)
        {
            float py = y + 0.5f;
            for (int x = 0; x < size; x += stripeStepX)
            {
                float px = x + 0.5f;
                float d = SampleTreeDensityBilinear(smooth, gridN, cell, px, py);
                if (d <= v33)
                    continue;

                Color32 col = d <= v66 ? moderateColor : denseColor;
                pixels[y * size + x] = col;
            }
        }
    }

    static void BoxBlurGridFloat(float[,] src, float[,] dst, int gn)
    {
        for (int gy = 0; gy < gn; gy++)
        {
            for (int gx = 0; gx < gn; gx++)
            {
                float sum = 0f;
                int w = 0;
                for (int dy = -1; dy <= 1; dy++)
                {
                    int ny = Mathf.Clamp(gy + dy, 0, gn - 1);
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        int nx = Mathf.Clamp(gx + dx, 0, gn - 1);
                        sum += src[ny, nx];
                        w++;
                    }
                }
                dst[gy, gx] = sum / w;
            }
        }
    }

    static float SampleTreeDensityBilinear(float[,] grid, int gn, int cell, float px, float py)
    {
        float gxF = px / cell - 0.5f;
        float gyF = py / cell - 0.5f;
        gxF = Mathf.Clamp(gxF, 0f, gn - 1 - 1e-4f);
        gyF = Mathf.Clamp(gyF, 0f, gn - 1 - 1e-4f);

        int x0 = Mathf.FloorToInt(gxF);
        int y0 = Mathf.FloorToInt(gyF);
        int x1 = Mathf.Min(x0 + 1, gn - 1);
        int y1 = Mathf.Min(y0 + 1, gn - 1);
        float tx = gxF - x0;
        float ty = gyF - y0;

        float v00 = grid[y0, x0];
        float v10 = grid[y0, x1];
        float v01 = grid[y1, x0];
        float v11 = grid[y1, x1];
        return Mathf.Lerp(Mathf.Lerp(v00, v10, tx), Mathf.Lerp(v01, v11, tx), ty);
    }

    static Vector2 MeRectRectCenterSvg(MeRect m)
    {
        float angleRad = -m.rotation * Mathf.Deg2Rad;
        float cos = Mathf.Cos(angleRad);
        float sin = Mathf.Sin(angleRad);
        float lx = m.size.x * 0.5f;
        float ly = m.size.y * 0.5f;
        float x = cos * lx - sin * ly + m.position.x;
        float y = sin * lx + cos * ly + m.position.y;
        return new Vector2(x, y);
    }

    static void FillMapViewCircle(Color32[] pixels, int size, Vector2 center, int radius, Color32 color)
    {
        int cx = Mathf.RoundToInt(center.x);
        int cy = Mathf.RoundToInt(center.y);
        int r2 = radius * radius;

        int minX = Mathf.Max(0, cx - radius);
        int maxX = Mathf.Min(size - 1, cx + radius);
        int minY = Mathf.Max(0, cy - radius);
        int maxY = Mathf.Min(size - 1, cy + radius);

        for (int y = minY; y <= maxY; y++)
        {
            int dy = y - cy;
            for (int x = minX; x <= maxX; x++)
            {
                int dx = x - cx;
                if (dx * dx + dy * dy <= r2)
                    pixels[y * size + x] = color;
            }
        }
    }

    static void FillMapViewQuad(Color32[] pixels, int size, Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color32 color)
    {
        FillMapViewTriangle(pixels, size, a, b, c, color);
        FillMapViewTriangle(pixels, size, a, d, c, color);
    }

    static void FillMapViewTriangle(Color32[] pixels, int size, Vector2 a, Vector2 b, Vector2 c, Color32 color)
    {
        int minX = Mathf.Clamp(Mathf.FloorToInt(Mathf.Min(a.x, Mathf.Min(b.x, c.x))), 0, size - 1);
        int maxX = Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(a.x, Mathf.Max(b.x, c.x))), 0, size - 1);
        int minY = Mathf.Clamp(Mathf.FloorToInt(Mathf.Min(a.y, Mathf.Min(b.y, c.y))), 0, size - 1);
        int maxY = Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(a.y, Mathf.Max(b.y, c.y))), 0, size - 1);

        float area = MapViewEdgeFunc(a, b, c);
        if (Mathf.Abs(area) < 0.001f) return;

        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                Vector2 p = new Vector2(x + 0.5f, y + 0.5f);
                float w0 = MapViewEdgeFunc(b, c, p);
                float w1 = MapViewEdgeFunc(c, a, p);
                float w2 = MapViewEdgeFunc(a, b, p);
                if (w0 * area >= 0f && w1 * area >= 0f && w2 * area >= 0f)
                    pixels[y * size + x] = color;
            }
        }
    }

    static float MapViewEdgeFunc(Vector2 a, Vector2 b, Vector2 p)
    {
        return (p.x - a.x) * (b.y - a.y) - (p.y - a.y) * (b.x - a.x);
    }

    static void FillMapViewQuadToMask(bool[] mask, int size, Vector2 a, Vector2 b, Vector2 c, Vector2 d)
    {
        FillMapViewTriangleToMask(mask, size, a, b, c);
        FillMapViewTriangleToMask(mask, size, a, d, c);
    }

    static void FillMapViewTriangleToMask(bool[] mask, int size, Vector2 a, Vector2 b, Vector2 c)
    {
        int minX = Mathf.Clamp(Mathf.FloorToInt(Mathf.Min(a.x, Mathf.Min(b.x, c.x))), 0, size - 1);
        int maxX = Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(a.x, Mathf.Max(b.x, c.x))), 0, size - 1);
        int minY = Mathf.Clamp(Mathf.FloorToInt(Mathf.Min(a.y, Mathf.Min(b.y, c.y))), 0, size - 1);
        int maxY = Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(a.y, Mathf.Max(b.y, c.y))), 0, size - 1);

        float area = MapViewEdgeFunc(a, b, c);
        if (Mathf.Abs(area) < 0.001f) return;

        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                Vector2 p = new Vector2(x + 0.5f, y + 0.5f);
                float w0 = MapViewEdgeFunc(b, c, p);
                float w1 = MapViewEdgeFunc(c, a, p);
                float w2 = MapViewEdgeFunc(a, b, p);
                if (w0 * area >= 0f && w1 * area >= 0f && w2 * area >= 0f)
                    mask[y * size + x] = true;
            }
        }
    }

    static void OutlineMapViewMask1px(Color32[] pixels, int size, bool[] mask, Color32 color)
    {
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                if (!mask[y * size + x]) continue;
                if (x == 0 || !mask[y * size + x - 1] ||
                    x == size - 1 || !mask[y * size + x + 1] ||
                    y == 0 || !mask[(y - 1) * size + x] ||
                    y == size - 1 || !mask[(y + 1) * size + x])
                    SetMapViewPixel(pixels, size, x, y, color);
            }
        }
    }

    static void DrawMapViewPolyline2px(Color32[] pixels, int size, IList<Vector2> points, Color32 color)
    {
        if (points == null || points.Count < 2) return;
        for (int i = 0; i < points.Count - 1; i++)
            DrawMapViewSegment2px(pixels, size, points[i], points[i + 1], color);
    }

    static void DrawMapViewSegment2px(Color32[] pixels, int size, Vector2 from, Vector2 to, Color32 color)
    {
        int x0 = Mathf.RoundToInt(from.x);
        int y0 = Mathf.RoundToInt(from.y);
        int x1 = Mathf.RoundToInt(to.x);
        int y1 = Mathf.RoundToInt(to.y);

        int dx = Mathf.Abs(x1 - x0);
        int dy = Mathf.Abs(y1 - y0);
        int sx = x0 < x1 ? 1 : -1;
        int sy = y0 < y1 ? 1 : -1;
        int err = dx - dy;

        while (true)
        {
            StampMapViewPixel2px(pixels, size, x0, y0, color);
            if (x0 == x1 && y0 == y1) break;
            int e2 = 2 * err;
            if (e2 > -dy)
            {
                err -= dy;
                x0 += sx;
            }
            if (e2 < dx)
            {
                err += dx;
                y0 += sy;
            }
        }
    }

    static void StampMapViewPixel2px(Color32[] pixels, int size, int cx, int cy, Color32 color)
    {
        for (int oy = 0; oy < 2; oy++)
        for (int ox = 0; ox < 2; ox++)
            SetMapViewPixel(pixels, size, cx + ox, cy + oy, color);
    }

    static void SetMapViewPixel(Color32[] pixels, int size, int x, int y, Color32 color)
    {
        if (x < 0 || x >= size || y < 0 || y >= size) return;
        pixels[y * size + x] = color;
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
