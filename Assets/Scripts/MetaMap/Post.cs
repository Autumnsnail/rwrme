using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class Post : MePath
{
    public string template_ref;

    public override float Rank => 0.05f;

    [Header("折线显示")]
    public float lineWidth = 0.85f;
    public float heightOffset = 0.15f;

    [Header("选中碰撞（PinAble）")]
    public float pickColliderHeight = 10f;
    public float pickWidthScale = 4f;

    const string LineChildName = "_post_polyline";
    const string PickChildPrefix = "_post_pick_";
    const string MeshNodePrefix = "_post_mesh_";

    static int s_pinAbleLayer = -2;

    static int PinAbleLayer
    {
        get
        {
            if (s_pinAbleLayer == -2)
            {
                s_pinAbleLayer = LayerMask.NameToLayer("PinAble");
                if (s_pinAbleLayer < 0) s_pinAbleLayer = 6;
            }
            return s_pinAbleLayer;
        }
    }

    void Start()
    {
    }

    void Update()
    {
    }

    void OnEnable()
    {
        scatterThis();
    }

    public override string getInfoText()
    {
        return "Post\n" + "id = " + id + "\n" + "layer = " + layerIndex.ToString() + "\n" + "template = " + template_ref;
    }

    public override void grab(Vector2 offset)
    {
        if (positionLine == null) return;
        for (int i = 0; i < positionLine.Count; i++)
            positionLine[i] += offset;
    }

    public override string IdPrefix => "post";

    public override MapItem Duplicate()
    {
        Post c;
        if (MapImporter.instate != null && MapImporter.instate.PostPref != null)
        {
            GameObject go = Instantiate(MapImporter.instate.PostPref);
            c = go.GetComponent<Post>();
            if (c == null) c = go.AddComponent<Post>();
        }
        else
        {
            c = new GameObject("Post").AddComponent<Post>();
        }
        CopyMePathFieldsTo(c);
        c.template_ref = template_ref;
        return c;
    }

    public override void scatterThis()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
            Destroy(transform.GetChild(i).gameObject);

        if (positionLine == null || positionLine.Count == 0)
            return;

        List<Vector3> worldPts = BuildWorldPolylinePoints();
        if (worldPts.Count == 0)
            return;

        PostTemplate template = ResolveTemplate();
        Color lineColor = template != null ? template.color : new Color(0.62f, 0.48f, 0f, 1f);
        int pin = PinAbleLayer;

        if (TryResolveLibraryMesh(template, out Mesh libraryMesh, out Color meshColor))
        {
            for (int i = 0; i < worldPts.Count; i++)
                SpawnMeshNode(i, worldPts, libraryMesh, meshColor, pin);
        }

        if (worldPts.Count < 2)
            return;

        GameObject lineGo = new GameObject(LineChildName);
        lineGo.transform.SetParent(transform, false);
        lineGo.layer = pin;

        LineRenderer lr = lineGo.AddComponent<LineRenderer>();
        lr.useWorldSpace = true;
        lr.loop = false;
        lr.widthMultiplier = 1f;
        lr.startWidth = lineWidth;
        lr.endWidth = lineWidth;
        lr.startColor = lineColor;
        lr.endColor = lineColor;
        lr.positionCount = worldPts.Count;
        for (int i = 0; i < worldPts.Count; i++)
            lr.SetPosition(i, worldPts[i]);

        lr.material = CreateLineMaterial(lineColor);
        lr.textureMode = LineTextureMode.Stretch;
        lr.numCornerVertices = 2;
        lr.numCapVertices = 2;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;

        BuildPickColliders(lineGo.transform, worldPts, pin);
    }

    PostTemplate ResolveTemplate()
    {
        if (MetaMap.instance == null || string.IsNullOrEmpty(template_ref))
            return null;
        return MetaMap.instance.PostTemplates.FirstOrDefault(t => t.name == template_ref);
    }

    static bool TryResolveLibraryMesh(PostTemplate postTemplate, out Mesh mesh, out Color meshColor)
    {
        mesh = null;
        meshColor = default;
        if (postTemplate == null)
            return false;

        MeshTemplate meshTemplate = postTemplate.ResolveMeshTemplate();
        if (meshTemplate == null || string.IsNullOrEmpty(meshTemplate.meshName))
            return false;

        if (!OgreRuntimeImporter.TryGetFromLibrary(meshTemplate.meshName, out List<MeshLoader.Result> submeshes)
            || submeshes == null
            || submeshes.Count == 0
            || submeshes[0].Mesh == null)
            return false;

        mesh = submeshes[0].Mesh;
        meshColor = new Color(meshTemplate.color.r, meshTemplate.color.g, meshTemplate.color.b, 0.5f);
        return true;
    }

    void SpawnMeshNode(int index, List<Vector3> worldPts, Mesh mesh, Color meshColor, int layer)
    {
        GameObject nodeGo = new GameObject(MeshNodePrefix + index);
        nodeGo.transform.SetParent(transform, false);
        nodeGo.layer = layer;
        nodeGo.transform.position = worldPts[index];
        nodeGo.transform.rotation = GetNodeRotation(worldPts, index);

        MeshFilter mf = nodeGo.AddComponent<MeshFilter>();
        mf.sharedMesh = mesh;

        MeshRenderer mr = nodeGo.AddComponent<MeshRenderer>();
        mr.material = CreateMeshMaterial(meshColor);
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        mr.receiveShadows = true;
    }

    static Quaternion GetNodeRotation(List<Vector3> worldPts, int index)
    {
        Vector3 forward = Vector3.zero;
        if (index > 0)
        {
            Vector3 prev = worldPts[index] - worldPts[index - 1];
            prev.y = 0f;
            if (prev.sqrMagnitude > 1e-6f)
                forward += prev.normalized;
        }
        if (index < worldPts.Count - 1)
        {
            Vector3 next = worldPts[index + 1] - worldPts[index];
            next.y = 0f;
            if (next.sqrMagnitude > 1e-6f)
                forward += next.normalized;
        }

        forward.y = 0f;
        if (forward.sqrMagnitude < 1e-6f)
            return Quaternion.identity;
        return Quaternion.LookRotation(forward.normalized, Vector3.up);
    }

    static Material CreateMeshMaterial(Color color)
    {
        Shader sh = Shader.Find("Standard");
        if (sh == null)
            sh = Shader.Find("Sprites/Default");

        Material m = new Material(sh);
        m.color = color;
        if (sh.name.Contains("Standard"))
        {
            m.SetFloat("_Metallic", 0f);
            m.SetFloat("_Glossiness", 0.35f);
        }
        return m;
    }

    static Material CreateLineMaterial(Color color)
    {
        Shader sh = Shader.Find("Sprites/Default");
        if (sh == null)
            sh = Shader.Find("Standard");

        Material m = new Material(sh);
        m.color = color;
        if (sh.name.Contains("Standard"))
        {
            m.SetFloat("_Metallic", 0f);
            m.SetFloat("_Glossiness", 0.2f);
        }
        return m;
    }

    List<Vector3> BuildWorldPolylinePoints()
    {
        var list = new List<Vector3>(positionLine.Count);
        foreach (Vector2 svgPt in positionLine)
        {
            Vector2 xy = MathOfRwrme.SvgPosToU3dPos(svgPt);
            Vector3 w = Vector3.zero;
            VpMetaToucher.getXYHeightWithLayer(xy, layerIndex, ref w, Rank);
            w.y += heightOffset;
            if (list.Count > 0 && (list[list.Count - 1] - w).sqrMagnitude < 1e-8f)
                continue;
            list.Add(w);
        }
        return list;
    }

    void BuildPickColliders(Transform lineParent, List<Vector3> worldPts, int pinLayer)
    {
        float halfW = Mathf.Max(lineWidth * pickWidthScale * 0.5f, 0.35f);
        for (int i = 0; i < worldPts.Count - 1; i++)
        {
            Vector3 a = worldPts[i];
            Vector3 b = worldPts[i + 1];
            Vector3 delta = b - a;
            Vector3 flat = new Vector3(delta.x, 0f, delta.z);
            float len = flat.magnitude;
            if (len < 1e-4f)
                continue;

            GameObject pick = new GameObject(PickChildPrefix + i);
            pick.transform.SetParent(lineParent, true);
            pick.layer = pinLayer;

            pick.transform.position = (a + b) * 0.5f;
            flat /= len;
            pick.transform.rotation = Quaternion.LookRotation(flat, Vector3.up);

            BoxCollider box = pick.AddComponent<BoxCollider>();
            box.isTrigger = false;
            box.center = Vector3.zero;
            box.size = new Vector3(halfW * 2f, pickColliderHeight, len);
        }
    }
}
