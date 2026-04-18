
using System.IO;
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
        string templatePath = Path.Combine(Application.dataPath, "templates", "vn_no_bti.xml");
        XmlReaderSettings settings = new XmlReaderSettings();
        settings.DtdProcessing = DtdProcessing.Ignore;
        settings.ValidationType = ValidationType.None; 
        using (XmlReader reader = XmlReader.Create(templatePath, settings))
        {
            templateDoc.Load(reader);
        }

        XmlElement template = templateDoc.DocumentElement.ChildNodes[0] as XmlElement;
        
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
                stpDesc.InnerText = "template = "+ms.template_ref+";";
                ekE.AppendChild(stpDesc);


                meshLayer.AppendChild(ekE);
            }
            if (has) layer.AppendChild(meshLayer);

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

        Debug.Log("MapExport");
        string fullPath = System.IO.Path.Combine(Application.dataPath, basePath, m_mm.m_metaTerrain.fileName);
        System.IO.File.WriteAllBytes(fullPath, m_mm.m_metaTerrain.data.convToPng());


        Debug.Log("MapExporter:exportSVG!");
        xmlDoc.Save(xmlFilePath);

        string xmlContent = File.ReadAllText(xmlFilePath);
        
        xmlContent = xmlContent.Replace("inkscape:label=\"#general\"", "\ninkscape:label=\"#general\"");

        File.WriteAllText(xmlFilePath, xmlContent);


        exportTerrainHeightmap();
        exportTerrainAlphamap();
        exportMapConfig();
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

        // 与 MapImporter.ConvertTextureToGrayScaleImage / Syncer 一致：PNG 灰度 = Unity 高度 [0,1] 线性映射到 0..255，
        // 不使用 min-max 拉伸，否则重导入会破坏绝对标高。
        float actualMin = float.MaxValue;
        float actualMax = float.MinValue;
        byte[] grayPixels = new byte[resolution * resolution];

        for (int y = 0; y < resolution; y++)
        {
            for (int x = 0; x < resolution; x++)
            {
                float height = heights[y, x];
                if (height < actualMin) actualMin = height;
                if (height > actualMax) actualMax = height;

                int pixelIndex = y * resolution + x;
                grayPixels[pixelIndex] = (byte)Mathf.Clamp(Mathf.RoundToInt(height * 255f), 0, 255);
            }

            // 每处理10%报告一次进度
            if (resolution >= 10 && y % (resolution / 10) == 0)
            {
                float progress = (float)y / resolution * 100f;
                Debug.Log($"导出进度: {progress:F1}%");
            }
        }

        // 使用 LoadRawTextureData 加载灰度数据
        exportTexture.LoadRawTextureData(grayPixels);
        exportTexture.Apply();

        // 保存为PNG文件
        string fileName = $"terrain5_heightmap.png";
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
        Debug.Log($"✓ 归一化高度采样范围: {actualMin:F3} ~ {actualMax:F3}（世界竖直尺度 terrain size.y = {terrainData.size.y:F1}m）");
        Debug.Log("=== 地形高度图导出完成！ ===");

#if UNITY_EDITOR
        // 在编辑器中刷新资产数据库
        UnityEditor.AssetDatabase.Refresh();
        Debug.Log("已刷新Unity资产数据库");
#endif
    }

    public void exportTerrainAlphamap()
    {
        Texture2D tex = Terrain.activeTerrain.materialTemplate.GetTexture("_Mask") as Texture2D; 
        byte[] pngData = tex.EncodeToPNG();
        int width = tex.width;
        int height = tex.height;
        Color32[] srcPixels = tex.GetPixels32();

        string filePath = Path.Combine(Application.dataPath, basePath,"terrain5_combined_alpha.png");
        File.WriteAllBytes(filePath, pngData);

        for(int i=1;i<5;i++)
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
                    case 4: value = srcPixels[p].a;
                        value = (byte)(255 - value);
                        break;
                }

                channelPixels[p] = new Color32(value, value, value, 255);
            }
            channelTex.SetPixels32(channelPixels);
            channelTex.Apply();
            filePath = Path.Combine(
                Application.dataPath,
                basePath,
                fileName
            );

            File.WriteAllBytes(filePath, channelTex.EncodeToPNG());
        }

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

    // Update is called once per frame
    void Update()
    {
        
    }
}
