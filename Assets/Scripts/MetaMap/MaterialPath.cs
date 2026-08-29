using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MaterialPath : MePath
{
    public float width = 20f;
    /// <summary>1=sand, 2=grass, 3=asphalt, 4=road</summary>
    public int materialIndex = 2;
    /// <summary>
    /// 半宽内中心→边缘权重曲线的幂指数：w = (1 - dist/R)^hardness。
    /// 1 为线性；大于 1 中心更满、近缘更陡；小于 1 中心衰减更快。不改变描边范围（范围由 width 决定）。
    /// SVG 无 hardness 时用此默认 1.0。
    /// </summary>
    public float hardness = 1.0f;
    public int samplesPerCubicSegment = MePathCurve.DefaultSamplesPerCubicSegment;

    public override float Rank => 0.04f;

    const string LineChildName = "_material_path_line";
    const string CanvasName = "Canvas";

    [System.NonSerialized] GameObject _paramUiRoot;
    [System.NonSerialized] bool _paramUiCached;
    [System.NonSerialized] bool _listenersWired;

    public override string IdPrefix => "material_path";

    static readonly Color[] MaterialColors =
    {
        Color.black,
        new Color(1f, 0.6f, 0.2f),
        new Color(0.2f, 0.85f, 0.2f),
        new Color(0.5f, 0.5f, 0.55f),
        new Color(0.35f, 0.25f, 0.15f),
    };

    public static string MaterialName(int index)
    {
        switch (index)
        {
            case 1: return "sand";
            case 3: return "asphalt";
            case 4: return "road";
            default: return "grass";
        }
    }

    public static MaterialPath CreateInstance()
    {
        if (MapImporter.instate != null && MapImporter.instate.MaterialPathPref != null)
        {
            GameObject go = Instantiate(MapImporter.instate.MaterialPathPref);
            MaterialPath mp = go.GetComponent<MaterialPath>();
            if (mp == null) mp = go.AddComponent<MaterialPath>();
            return mp;
        }

        return new GameObject("MaterialPath").AddComponent<MaterialPath>();
    }

    public override string getInfoText()
    {
        return "MaterialPath\nid = " + id + "\nwidth = " + width
            + "\nmaterial = " + materialIndex + " (" + MaterialName(materialIndex) + ")"
            + "\nhardness = " + hardness;
    }

    public override MapItem Duplicate()
    {
        MaterialPath c = CreateInstance();
        CopyMePathFieldsTo(c);
        c.width = width;
        c.materialIndex = materialIndex;
        c.hardness = hardness;
        c.samplesPerCubicSegment = samplesPerCubicSegment;
        return c;
    }

    void Awake()
    {
        CacheParamUi();
        if (_paramUiRoot != null) _paramUiRoot.SetActive(false);
    }

    void OnEnable()
    {
        scatterThis();
    }

    void CacheParamUi()
    {
        if (_paramUiCached) return;
        _paramUiCached = true;
        Transform t = transform.Find(CanvasName);
        _paramUiRoot = t != null ? t.gameObject : null;
        if (t != null) MapItemParamUiLayout.Ensure(t);
    }

    public void SetParamUiActive(bool on)
    {
        CacheParamUi();
        if (_paramUiRoot != null) _paramUiRoot.SetActive(on);
        if (on) UpdateParamUi();
    }

    public void SetWidth(string value)
    {
        if (!float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float f))
            return;
        width = Mathf.Clamp(f, 1f, 200f);
        scatterThis();
        if (Syncer.instence != null) Syncer.instence.ApplyPreviewTerrain();
    }

    public void SetHardness(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return;
        // 允许 "0.5" / "0,5"；输入过程中的尾随小数点等非法片段直接忽略
        string normalized = value.Trim().Replace(',', '.');
        if (!float.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out float f))
            return;
        hardness = Mathf.Clamp(f, 0f, 50f);
        scatterThis();
        if (Syncer.instence != null) Syncer.instence.ApplyPreviewTerrain();
    }

    public void CycleMaterial()
    {
        materialIndex = materialIndex >= 4 ? 1 : materialIndex + 1;
        scatterThis();
        if (Syncer.instence != null) Syncer.instence.ApplyPreviewTerrain();
    }

    public void UpdateParamUi()
    {
        Transform canvas = transform.Find(CanvasName);
        if (canvas == null) return;

        MapItemParamUiLayout.Ensure(canvas);
        WireListeners(canvas);

        TMP_InputField widthInput = MapItemParamUiLayout.Find(canvas, "width")?.GetComponent<TMP_InputField>();
        if (widthInput != null && !widthInput.isFocused)
            widthInput.SetTextWithoutNotify(width.ToString(CultureInfo.InvariantCulture));

        TMP_InputField hardnessInput = MapItemParamUiLayout.Find(canvas, "hardness")?.GetComponent<TMP_InputField>();
        if (hardnessInput != null)
        {
            // 强制小数输入（避免被当成整数框）
            if (hardnessInput.contentType != TMP_InputField.ContentType.DecimalNumber)
                hardnessInput.contentType = TMP_InputField.ContentType.DecimalNumber;
            if (!hardnessInput.isFocused)
                hardnessInput.SetTextWithoutNotify(hardness.ToString("G", CultureInfo.InvariantCulture));
        }

        Transform cycleTf = MapItemParamUiLayout.Find(canvas, "CycleMaterial");
        if (cycleTf != null)
        {
            string labelText = MaterialName(materialIndex) + " (" + materialIndex + ")";
            TextMeshProUGUI tmp = cycleTf.GetComponentInChildren<TextMeshProUGUI>(true);
            if (tmp != null)
                tmp.text = labelText;
            else
            {
                Text uiText = cycleTf.GetComponentInChildren<Text>(true);
                if (uiText != null)
                    uiText.text = labelText;
            }
        }
    }

    void WireListeners(Transform canvas)
    {
        if (_listenersWired) return;

        // 用 onEndEdit：避免 onValueChanged 在输入 "1." 时被回写打断小数
        TMP_InputField widthInput = MapItemParamUiLayout.Find(canvas, "width")?.GetComponent<TMP_InputField>();
        if (widthInput != null && widthInput.onEndEdit.GetPersistentEventCount() == 0)
            widthInput.onEndEdit.AddListener(SetWidth);

        TMP_InputField hardnessInput = MapItemParamUiLayout.Find(canvas, "hardness")?.GetComponent<TMP_InputField>();
        if (hardnessInput != null)
        {
            hardnessInput.contentType = TMP_InputField.ContentType.DecimalNumber;
            if (hardnessInput.onEndEdit.GetPersistentEventCount() == 0)
                hardnessInput.onEndEdit.AddListener(SetHardness);
        }

        Button cycleBtn = MapItemParamUiLayout.Find(canvas, "CycleMaterial")?.GetComponent<Button>();
        if (cycleBtn != null && cycleBtn.onClick.GetPersistentEventCount() == 0)
            cycleBtn.onClick.AddListener(CycleMaterial);

        _listenersWired = true;
    }

    List<Vector3> BuildWorldCurvePoints()
    {
        var svgPts = new List<Vector2>();
        MePathCurve.CollectSvgCurvePoints(this, samplesPerCubicSegment, svgPts);
        var list = new List<Vector3>(svgPts.Count);
        foreach (Vector2 svgPt in svgPts)
        {
            Vector2 u3d = MathOfRwrme.SvgPosToU3dPos(svgPt);
            float y = 0f;
            if (Terrain.activeTerrain != null)
                y = Terrain.activeTerrain.SampleHeight(new Vector3(u3d.x, 0f, u3d.y)) + 1.5f;
            Vector3 w = new Vector3(u3d.x, y, u3d.y);
            if (list.Count > 0 && (list[list.Count - 1] - w).sqrMagnitude < 1e-8f)
                continue;
            list.Add(w);
        }
        return list;
    }

    void EnsureCurveData()
    {
        if (positionLine == null || positionLine.Count < 2) return;
        if (curve != null && curve.Count == positionLine.Count
            && controlPoints != null && controlPoints.Count >= 2)
            return;
        curve = new List<bool>();
        controlPoints = new List<Vector2>();
        MePathCurve.ApplyAutoBezierCurveAnnotations(positionLine, curve, controlPoints);
    }

    public override void scatterThis()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform child = transform.GetChild(i);
            if (child.name == CanvasName)
                continue;
            Destroy(child.gameObject);
        }

        UpdateParamUi();

        if (positionLine == null || positionLine.Count < 2) return;

        EnsureCurveData();

        List<Vector3> worldPts = BuildWorldCurvePoints();
        if (worldPts.Count < 2) return;

        int idx = Mathf.Clamp(materialIndex, 0, MaterialColors.Length - 1);
        Color col = MaterialColors[idx];

        GameObject lineGo = new GameObject(LineChildName);
        lineGo.transform.SetParent(transform, false);
        lineGo.layer = TerrainPathPickUtil.PinAbleLayer;

        LineRenderer lr = lineGo.AddComponent<LineRenderer>();
        lr.useWorldSpace = true;
        lr.loop = false;
        lr.startWidth = width * 0.05f;
        lr.endWidth = width * 0.05f;
        lr.startColor = col;
        lr.endColor = col;
        lr.positionCount = worldPts.Count;
        for (int i = 0; i < worldPts.Count; i++)
            lr.SetPosition(i, worldPts[i]);

        Shader shader = Shader.Find("Hidden/Internal-Colored") ?? Shader.Find("Sprites/Default");
        lr.material = new Material(shader);
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;

        TerrainPathPickUtil.BuildPickColliders(lineGo.transform, worldPts, width);
    }
}
