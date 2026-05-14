using System.Collections.Generic;
using UnityEngine;

public class Offroad : MePath
{
    public List<bool> curve;
    public List<Vector2> controlPoints;

    [Header("曲线显示")]
    public float lineWidth = 3.2f;
    public int samplesPerCubicSegment = 24;
    [ColorUsage(true, true)]
    public Color lineColor = new Color(0.2f, 1f, 0.45f, 1f);
    [ColorUsage(true, true)]
    public Color emissionColor = new Color(0.35f, 1f, 0.55f, 1f);
    [Min(0f)] public float emissionIntensity = 5f;
    public float heightOffset = 1f;

    [Header("选中碰撞（PinAble）")]
    [Tooltip("竖直方向碰撞体高度，便于斜视角射线命中")]
    public float pickColliderHeight = 14f;
    [Tooltip("相对线宽的命中宽度倍率，略大于视觉线宽更易点选")]
    public float pickWidthScale = 5f;

    const string LineChildName = "_offroad_curve_line";
    const string PickChildPrefix = "_offroad_pick_";

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

    public override string getInfoText()
    {
        int h = MetaMap.instance != null ? MetaMap.instance.offroadHeightSampleLayer : 4;
        return "Offroad\nid = " + id + "\nlayerIndex = " + layerIndex + "\noffroadHeightSampleLayer = " + h + "\nanchors = " + (positionLine != null ? positionLine.Count : 0);
    }

    public override void grab(Vector2 offset)
    {
        if (positionLine != null)
            for (int i = 0; i < positionLine.Count; i++) positionLine[i] += offset;
        if (controlPoints != null)
            for (int i = 0; i < controlPoints.Count; i++) controlPoints[i] += offset;
    }

    public override string IdPrefix { get { return "offroad"; } }
    public override MapItem Duplicate()
    {
        Offroad c;
        if (MapImporter.instate.OffroadPref != null)
        {
            GameObject go = Instantiate(MapImporter.instate.OffroadPref);
            c = go.GetComponent<Offroad>();
            if (c == null) c = go.AddComponent<Offroad>();
        }
        else
        {
            GameObject go = new GameObject("Offroad");
            c = go.AddComponent<Offroad>();
        }
        CopyMePathFieldsTo(c);
        c.curve = curve != null ? new List<bool>(curve) : new List<bool>();
        c.controlPoints = controlPoints != null ? new List<Vector2>(controlPoints) : new List<Vector2>();
        return c;
    }

    public void OnEnable()
    {
        scatterThis();
    }
    public override void scatterThis()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
            Destroy(transform.GetChild(i).gameObject);

        if (positionLine == null || positionLine.Count < 2)
            return;

        List<Vector3> worldPts = BuildWorldCurvePoints();
        if (worldPts.Count < 2)
            return;

        int pin = PinAbleLayer;

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

        lr.material = CreateCurveLineMaterial();
        lr.textureMode = LineTextureMode.Stretch;
        lr.numCornerVertices = 4;
        lr.numCapVertices = 4;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;

        BuildPickColliders(lineGo.transform, worldPts, pin);
    }

    Material CreateCurveLineMaterial()
    {
        Shader sh = Shader.Find("Standard");
        if (sh == null)
            sh = Shader.Find("Sprites/Default");

        Material m = new Material(sh);
        if (sh.name.Contains("Standard"))
        {
            m.color = lineColor;
            m.SetFloat("_Metallic", 0.05f);
            m.SetFloat("_Glossiness", 0.45f);
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", emissionColor * emissionIntensity);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }
        else
        {
            m.color = lineColor * (1f + emissionIntensity * 0.25f);
        }
        return m;
    }

    void BuildPickColliders(Transform lineParent, List<Vector3> worldPts, int pinLayer)
    {
        float halfW = Mathf.Max(lineWidth * pickWidthScale * 0.5f, 0.6f);
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

            Vector3 mid = (a + b) * 0.5f;
            pick.transform.position = mid;
            flat /= len;
            pick.transform.rotation = Quaternion.LookRotation(flat, Vector3.up);

            BoxCollider box = pick.AddComponent<BoxCollider>();
            box.isTrigger = false;
            box.center = Vector3.zero;
            box.size = new Vector3(halfW * 2f, pickColliderHeight, len);
        }
    }

    List<Vector3> BuildWorldCurvePoints()
    {
        var list = new List<Vector3>(positionLine.Count * Mathf.Max(2, samplesPerCubicSegment));
        int cpIndex = 0;
        bool hasCurve = curve != null && curve.Count == positionLine.Count;
        bool hasCp = controlPoints != null && controlPoints.Count >= 2;

        void AppendWorld(Vector2 svgPt)
        {
            Vector2 xy = MathOfRwrme.SvgPosToU3dPos(svgPt);
            Vector3 w = Vector3.zero;
            int hLayer = MetaMap.instance != null ? MetaMap.instance.offroadHeightSampleLayer : 4;
            VpMetaToucher.getXYHeightWithLayer(xy, hLayer, ref w, Rank);
            w.y += heightOffset;
            if (list.Count > 0 && (list[list.Count - 1] - w).sqrMagnitude < 1e-8f)
                return;
            list.Add(w);
        }

        AppendWorld(positionLine[0]);

        for (int i = 1; i < positionLine.Count; i++)
        {
            bool cubicEnd = hasCurve && curve[i] && hasCp && cpIndex + 1 < controlPoints.Count;
            if (cubicEnd)
            {
                Vector2 p0 = positionLine[i - 1];
                Vector2 c1 = controlPoints[cpIndex++];
                Vector2 c2 = controlPoints[cpIndex++];
                Vector2 p3 = positionLine[i];
                int steps = Mathf.Max(2, samplesPerCubicSegment);
                for (int s = 1; s <= steps; s++)
                {
                    float t = s / (float)steps;
                    Vector2 p = CubicBezier2(p0, c1, c2, p3, t);
                    AppendWorld(p);
                }
            }
            else
                AppendWorld(positionLine[i]);
        }

        return list;
    }

    static Vector2 CubicBezier2(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t)
    {
        Vector2 a = Vector2.Lerp(p0, p1, t);
        Vector2 b = Vector2.Lerp(p1, p2, t);
        Vector2 c = Vector2.Lerp(p2, p3, t);
        Vector2 d = Vector2.Lerp(a, b, t);
        Vector2 e = Vector2.Lerp(b, c, t);
        return Vector2.Lerp(d, e, t);
    }

    /// <summary>
    /// 根据锚点折线填充 <paramref name="curve"/> 与 <paramref name="controlPoints"/>，与 <see cref="BuildWorldCurvePoints"/> 约定一致：
    /// <c>curve[0]=false</c>，<c>curve[1..]</c> 为 true 表示段 (i-1)→i 为三次贝塞尔；控制点为均匀 Catmull-Rom 转 Cubic（端点用虚拟邻点外推）。
    /// </summary>
    public static void ApplyAutoBezierCurveAnnotations(List<Vector2> anchors, List<bool> curve, List<Vector2> controlPoints)
    {
        curve.Clear();
        controlPoints.Clear();
        int n = anchors != null ? anchors.Count : 0;
        if (n < 2)
        {
            if (n == 1)
                curve.Add(false);
            return;
        }

        curve.Add(false);
        for (int i = 1; i < n; i++)
            curve.Add(true);

        for (int i = 1; i < n; i++)
        {
            Vector2 p0 = i >= 2 ? anchors[i - 2] : anchors[0] + (anchors[0] - anchors[1]);
            Vector2 p1 = anchors[i - 1];
            Vector2 p2 = anchors[i];
            Vector2 p3 = i + 1 < n ? anchors[i + 1] : anchors[n - 1] + (anchors[n - 1] - anchors[n - 2]);

            controlPoints.Add(p1 + (p2 - p0) * (1f / 6f));
            controlPoints.Add(p2 - (p3 - p1) * (1f / 6f));
        }
    }

    void Start()
    {
    }

    void Update()
    {
    }
}
