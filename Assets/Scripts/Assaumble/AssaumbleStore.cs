using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

[Serializable]
public class AssaumbleFile
{
    public string name;
    public string templateName;
    public AssaumbleMember[] members;
}

[Serializable]
public class AssaumbleMember
{
    public string type;
    public int relLayer;

    public float px, py, rot, sx, sy;
    public string material;

    public int height;
    public bool roof;
    public bool merged;
    public bool reHighed;
    public float reHighedHeight;

    public string template_ref;
    public bool templated;
    public bool reCollision;
    public float cx, cy, cz;
    public float length;

    public string base_wall_template;
    public string wall_template;
    public string top_material;
    public float wall_height;
    public bool isBridge;
    public bool isDeck;
    public float platformHeight;

    public bool taged;
    public string vehicleKey;
    public int factionIndex;
    public string baseName;
    public int supplyType;

    public float width;
    public float heightDelta;
    public int heightMode;
    public int materialIndex;
    public float hardness;
    public int samplesPerCubicSegment;

    public float[] path;
    public float[] pathL;
    public float[] pathR;
    public bool[] curve;
    public float[] controls;
}

/// <summary>Assaumble (.asmb) 加载、保存与放置。</summary>
public static class AssaumbleStore
{
    public static readonly List<AssaumbleFile> Loaded = new List<AssaumbleFile>();
    public static AssaumbleFile Current;

    public static string DirPath => Path.Combine(Application.dataPath, "assaumbles");

    public static string CurrentTemplateStem()
    {
        string path = MapImporter.FindFirstTemplateInTemplatesDir();
        if (string.IsNullOrEmpty(path)) return "template";
        return Path.GetFileNameWithoutExtension(path);
    }

    public static List<string> GetDisplayNames()
    {
        return Loaded.Select(a => a.name + "_" + a.templateName).ToList();
    }

    public static void SetCurrentByIndex(int index)
    {
        if (index < 0 || index >= Loaded.Count)
        {
            Current = null;
            return;
        }
        Current = Loaded[index];
    }

    public static void LoadAll()
    {
        Loaded.Clear();
        Current = null;
        if (!Directory.Exists(DirPath))
            Directory.CreateDirectory(DirPath);

        foreach (string file in Directory.GetFiles(DirPath, "*.asmb").OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                string json = File.ReadAllText(file, Encoding.UTF8);
                AssaumbleFile data = JsonUtility.FromJson<AssaumbleFile>(json);
                if (data == null || data.members == null) continue;
                if (string.IsNullOrEmpty(data.name))
                    data.name = Path.GetFileNameWithoutExtension(file);
                if (string.IsNullOrEmpty(data.templateName))
                    data.templateName = CurrentTemplateStem();
                Loaded.Add(data);
            }
            catch (Exception e)
            {
                Debug.LogWarning("AssaumbleStore: failed to load " + file + ": " + e.Message);
            }
        }
        Debug.Log("AssaumbleStore: loaded " + Loaded.Count + " assemblies from " + DirPath);
    }

    public static bool SaveFromSelection(string nameInput, List<MapItem> selection)
    {
        if (string.IsNullOrWhiteSpace(nameInput))
        {
            Debug.LogWarning("AssaumbleStore: nameInput is empty");
            return false;
        }
        if (selection == null || selection.Count == 0)
        {
            Debug.LogWarning("AssaumbleStore: selection is empty");
            return false;
        }

        var usable = new List<MapItem>();
        foreach (var mi in selection)
        {
            if (mi == null) continue;
            if (!CanSerialize(mi))
            {
                Debug.LogWarning("AssaumbleStore: skip unsupported type " + mi.GetType().Name);
                continue;
            }
            usable.Add(mi);
        }
        if (usable.Count == 0)
        {
            Debug.LogWarning("AssaumbleStore: no serializable items in selection");
            return false;
        }

        Vector2 origin = Vector2.zero;
        int minLayer = int.MaxValue;
        foreach (var mi in usable)
        {
            origin += mi.GetAnchor();
            if (mi.layerIndex < minLayer) minLayer = mi.layerIndex;
        }
        origin /= usable.Count;

        string stem = CurrentTemplateStem();
        string safeName = SanitizeFileName(nameInput.Trim());
        var file = new AssaumbleFile
        {
            name = safeName,
            templateName = stem,
            members = usable.Select(mi => ToMember(mi, origin, minLayer)).ToArray()
        };

        if (!Directory.Exists(DirPath))
            Directory.CreateDirectory(DirPath);

        string path = Path.Combine(DirPath, safeName + "_" + stem + ".asmb");
        File.WriteAllText(path, JsonUtility.ToJson(file, true), Encoding.UTF8);
        Debug.Log("AssaumbleStore: saved " + path);

        LoadAll();
        int idx = Loaded.FindIndex(a => a.name == file.name && a.templateName == file.templateName);
        if (idx >= 0) Current = Loaded[idx];
        return true;
    }

    public static void PlaceAt(Vector2 worldOrigin, int baseLayer)
    {
        if (Current == null || Current.members == null || Current.members.Length == 0)
        {
            Debug.LogWarning("AssaumbleStore: no current assembly to place");
            return;
        }

        var pending = new List<MapItem>();
        bool terrainPaths = false;
        foreach (var member in Current.members.OrderBy(m => m.relLayer))
        {
            MapItem mi = FromMember(member, worldOrigin, baseLayer);
            if (mi == null) continue;
            MetaMap.instance.defaultLayer.mapItems.Add(mi);
            pending.Add(mi);
            if (mi is HeightPath || mi is MaterialPath)
                terrainPaths = true;
        }

        pending.Sort((a, b) =>
        {
            int c = a.layerIndex.CompareTo(b.layerIndex);
            if (c != 0) return c;
            return a.Rank.CompareTo(b.Rank);
        });

        foreach (MapItem mi in pending)
        {
            mi.scatterThis();
            Physics.SyncTransforms();
        }

        if (terrainPaths && Syncer.instence != null)
            Syncer.instence.ApplyPreviewTerrain();

        if (ToolController.inste == null || pending.Count == 0)
            return;

        if (pending.Count == 1)
        {
            ToolController.inste.miSelected = pending[0];
            ToolController.inste.misSelected.Clear();
        }
        else
        {
            ToolController.inste.miSelected = null;
            ToolController.inste.misSelected.Clear();
            ToolController.inste.misSelected.AddRange(pending);
            if (UIManager.instance != null)
                UIManager.instance.RefreshMultiSelectPanel(ToolController.inste.misSelected);
        }
    }

    static bool CanSerialize(MapItem mi)
    {
        return mi is Building || mi is Wall || mi is MeMesh || mi is Decal
            || mi is Platform || mi is Post || mi is Offroad
            || mi is Vehicle || mi is Rock || mi is Crate || mi is Ladder
            || mi is Base || mi is ItemSupply || mi is SpawnPoint
            || mi is HeightPath || mi is MaterialPath;
    }

    static AssaumbleMember ToMember(MapItem mi, Vector2 origin, int minLayer)
    {
        var m = new AssaumbleMember
        {
            type = mi.GetType().Name,
            relLayer = mi.layerIndex - minLayer,
            material = mi.material
        };

        if (mi is MeRect rect)
        {
            Vector2 p = rect.position - origin;
            m.px = p.x; m.py = p.y;
            m.rot = rect.rotation;
            m.sx = rect.size.x; m.sy = rect.size.y;
        }
        if (mi is MePath path)
        {
            m.path = Pack(path.positionLine, origin);
            m.curve = path.curve != null ? path.curve.ToArray() : null;
            m.controls = Pack(path.controlPoints, origin);
        }
        if (mi is PathPair pair)
        {
            m.pathL = Pack(pair.positinLineL, origin);
            m.pathR = Pack(pair.positinLineR, origin);
        }

        switch (mi)
        {
            case Building b:
                m.height = b.height; m.roof = b.roof;
                break;
            case Wall w:
                m.merged = w.merged; m.reHighed = w.reHighed; m.reHighedHeight = w.reHighedHeight;
                break;
            case MeMesh mesh:
                m.templated = mesh.templated; m.template_ref = mesh.template_ref;
                m.reCollision = mesh.reCollision;
                m.cx = mesh.collisionSize.x; m.cy = mesh.collisionSize.y; m.cz = mesh.collisionSize.z;
                break;
            case Decal d:
                m.template_ref = d.template_ref; m.length = d.length;
                break;
            case Platform p:
                m.base_wall_template = p.base_wall_template;
                m.wall_template = p.wall_template;
                m.top_material = p.top_material;
                m.wall_height = p.wall_height;
                m.isBridge = p.isBridge; m.isDeck = p.isDeck;
                m.platformHeight = p.height;
                break;
            case Post post:
                m.template_ref = post.template_ref;
                break;
            case Vehicle v:
                m.taged = v.taged; m.vehicleKey = v.key;
                break;
            case Base bas:
                m.factionIndex = bas.factionIndex; m.baseName = bas._name;
                break;
            case ItemSupply supply:
                m.supplyType = supply.type;
                break;
            case HeightPath hp:
                m.width = hp.width; m.heightDelta = hp.heightDelta;
                m.heightMode = (int)hp.mode;
                m.samplesPerCubicSegment = hp.samplesPerCubicSegment;
                break;
            case MaterialPath mp:
                m.width = mp.width; m.materialIndex = mp.materialIndex;
                m.hardness = mp.hardness;
                m.samplesPerCubicSegment = mp.samplesPerCubicSegment;
                break;
        }
        return m;
    }

    static MapItem FromMember(AssaumbleMember m, Vector2 worldOrigin, int baseLayer)
    {
        if (m == null || string.IsNullOrEmpty(m.type)) return null;
        MapImporter imp = MapImporter.instate;
        if (imp == null) return null;

        MapItem mi = null;
        switch (m.type)
        {
            case "Building":
                mi = Ins(imp.BuildingPref).GetComponent<Building>();
                ApplyMeRect(mi as MeRect, m, worldOrigin);
                var b = (Building)mi; b.height = m.height; b.roof = m.roof;
                break;
            case "Wall":
                mi = Ins(imp.WallPref).GetComponent<Wall>();
                ApplyMePath(mi as MePath, m, worldOrigin);
                var w = (Wall)mi; w.merged = m.merged; w.reHighed = m.reHighed; w.reHighedHeight = m.reHighedHeight;
                break;
            case "MeMesh":
                mi = Ins(imp.MeshPref).GetComponent<MeMesh>();
                ApplyMeRect(mi as MeRect, m, worldOrigin);
                var mesh = (MeMesh)mi;
                mesh.templated = m.templated; mesh.template_ref = m.template_ref;
                mesh.reCollision = m.reCollision;
                mesh.collisionSize = new Vector3(m.cx, m.cy, m.cz);
                break;
            case "Decal":
                mi = Ins(imp.DecalPref).GetComponent<Decal>();
                ApplyMeRect(mi as MeRect, m, worldOrigin);
                var d = (Decal)mi; d.template_ref = m.template_ref; d.length = m.length;
                break;
            case "Platform":
                mi = Ins(imp.PlatformPref).GetComponent<Platform>();
                ApplyPathPair(mi as PathPair, m, worldOrigin);
                var p = (Platform)mi;
                p.base_wall_template = m.base_wall_template;
                p.wall_template = m.wall_template;
                p.top_material = m.top_material;
                p.wall_height = m.wall_height;
                p.isBridge = m.isBridge; p.isDeck = m.isDeck;
                p.height = m.platformHeight;
                break;
            case "Post":
                mi = Ins(imp.PostPref).GetComponent<Post>();
                ApplyMePath(mi as MePath, m, worldOrigin);
                ((Post)mi).template_ref = m.template_ref;
                break;
            case "Offroad":
                mi = Ins(imp.OffroadPref).GetComponent<Offroad>();
                ApplyMePath(mi as MePath, m, worldOrigin);
                break;
            case "Vehicle":
                mi = Ins(imp.VehiclePref).GetComponent<Vehicle>();
                ApplyMeRect(mi as MeRect, m, worldOrigin);
                var v = (Vehicle)mi; v.taged = m.taged; v.key = m.vehicleKey ?? "";
                break;
            case "Rock":
                mi = Ins(imp.RockPref).GetComponent<Rock>();
                ApplyMeRect(mi as MeRect, m, worldOrigin);
                break;
            case "Crate":
                mi = Ins(imp.CratePref).GetComponent<Crate>();
                ApplyMeRect(mi as MeRect, m, worldOrigin);
                break;
            case "Ladder":
                mi = Ins(imp.LadderPref).GetComponent<Ladder>();
                ApplyMeRect(mi as MeRect, m, worldOrigin);
                break;
            case "Base":
                mi = Ins(imp.BasePref).GetComponent<Base>();
                ApplyMeRect(mi as MeRect, m, worldOrigin);
                var bas = (Base)mi; bas.factionIndex = m.factionIndex; bas._name = m.baseName ?? "";
                break;
            case "ItemSupply":
                mi = Ins(imp.ItemSupplyPref).GetComponent<ItemSupply>();
                ApplyMeRect(mi as MeRect, m, worldOrigin);
                ((ItemSupply)mi).type = m.supplyType;
                break;
            case "SpawnPoint":
                mi = Ins(imp.SpawnPointPref).GetComponent<SpawnPoint>();
                ApplyMeRect(mi as MeRect, m, worldOrigin);
                break;
            case "HeightPath":
                mi = new GameObject("HeightPath").AddComponent<HeightPath>();
                ApplyMePath(mi as MePath, m, worldOrigin);
                var hp = (HeightPath)mi;
                hp.width = m.width; hp.heightDelta = m.heightDelta;
                hp.mode = (HeightPathMode)m.heightMode;
                if (m.samplesPerCubicSegment > 0) hp.samplesPerCubicSegment = m.samplesPerCubicSegment;
                break;
            case "MaterialPath":
                mi = MaterialPath.CreateInstance();
                ApplyMePath(mi as MePath, m, worldOrigin);
                var mp = (MaterialPath)mi;
                mp.width = m.width; mp.materialIndex = m.materialIndex;
                mp.hardness = m.hardness;
                if (m.samplesPerCubicSegment > 0) mp.samplesPerCubicSegment = m.samplesPerCubicSegment;
                break;
            default:
                Debug.LogWarning("AssaumbleStore: unknown type " + m.type);
                return null;
        }

        mi.layerIndex = baseLayer + m.relLayer;
        mi.material = m.material;
        mi.id = MetaMap.instance.getNewItemId(mi.IdPrefix);
        return mi;
    }

    static GameObject Ins(GameObject pref)
    {
        return ToolController.inste != null
            ? ToolController.inste.InsOnePref(pref)
            : UnityEngine.Object.Instantiate(pref);
    }

    static void ApplyMeRect(MeRect rect, AssaumbleMember m, Vector2 worldOrigin)
    {
        if (rect == null) return;
        rect.position = new Vector2(m.px, m.py) + worldOrigin;
        rect.rotation = m.rot;
        rect.size = new Vector2(m.sx, m.sy);
    }

    static void ApplyMePath(MePath path, AssaumbleMember m, Vector2 worldOrigin)
    {
        if (path == null) return;
        path.positionLine = Unpack(m.path, worldOrigin);
        path.curve = m.curve != null ? new List<bool>(m.curve) : new List<bool>();
        path.controlPoints = Unpack(m.controls, worldOrigin);
        path.material = m.material;
    }

    static void ApplyPathPair(PathPair pair, AssaumbleMember m, Vector2 worldOrigin)
    {
        if (pair == null) return;
        pair.positinLineL = Unpack(m.pathL, worldOrigin);
        pair.positinLineR = Unpack(m.pathR, worldOrigin);
        pair.material = m.material;
    }

    static float[] Pack(List<Vector2> pts, Vector2 origin)
    {
        if (pts == null || pts.Count == 0) return Array.Empty<float>();
        var a = new float[pts.Count * 2];
        for (int i = 0; i < pts.Count; i++)
        {
            Vector2 p = pts[i] - origin;
            a[i * 2] = p.x;
            a[i * 2 + 1] = p.y;
        }
        return a;
    }

    static List<Vector2> Unpack(float[] a, Vector2 worldOrigin)
    {
        var list = new List<Vector2>();
        if (a == null) return list;
        for (int i = 0; i + 1 < a.Length; i += 2)
            list.Add(new Vector2(a[i], a[i + 1]) + worldOrigin);
        return list;
    }

    static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sb = new StringBuilder(name.Length);
        foreach (char c in name)
            sb.Append(Array.IndexOf(invalid, c) >= 0 ? '_' : c);
        string s = sb.ToString().Trim();
        return string.IsNullOrEmpty(s) ? "assaumble" : s;
    }
}
