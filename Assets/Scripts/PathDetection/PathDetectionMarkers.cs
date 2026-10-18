using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 路径检测结果场景标记：红=三点重合，黄=过窄缝隙。
/// 顶部水平半透明遮罩（Always ZTest），俯视可见。
/// </summary>
public sealed class PathDetectionMarkers : MonoBehaviour
{
    const float OverlayY = 80f;
    /// <summary>SVG 半径下限（世界边长约 radius，因 svg/2=world）。</summary>
    const float MinSvgRadius = 8f;
    const float MinWorldEdge = 4f;

    public static PathDetectionMarkers Instance { get; private set; }

    Transform _root;
    Transform _tripleRoot;
    Transform _narrowRoot;
    bool _visible = true;

    static Material _matTriple;
    static Material _matNarrow;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        EnsureRoots();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public static PathDetectionMarkers Ensure()
    {
        if (Instance != null) return Instance;
        var go = new GameObject("PathDetectionMarkers");
        return go.AddComponent<PathDetectionMarkers>();
    }

    void EnsureRoots()
    {
        if (_root != null) return;
        _root = transform;
        _tripleRoot = new GameObject("TripleOverlap").transform;
        _tripleRoot.SetParent(_root, false);
        _narrowRoot = new GameObject("NarrowGap").transform;
        _narrowRoot.SetParent(_root, false);
    }

    public void SetEnabled(bool enabled)
    {
        _visible = enabled;
        EnsureRoots();
        _root.gameObject.SetActive(enabled);
    }

    public bool IsEnabled => _visible && _root != null && _root.gameObject.activeSelf;

    public void Clear()
    {
        EnsureRoots();
        ClearChildren(_tripleRoot);
        ClearChildren(_narrowRoot);
    }

    static void ClearChildren(Transform t)
    {
        if (t == null) return;
        for (int i = t.childCount - 1; i >= 0; i--)
        {
            var c = t.GetChild(i).gameObject;
            if (Application.isPlaying) Destroy(c);
            else DestroyImmediate(c);
        }
    }

    public void Show(IReadOnlyList<PathCollisionDetector.Issue> issues)
    {
        EnsureRoots();
        Clear();
        if (issues == null) return;

        EnsureMaterials();

        foreach (var issue in issues)
        {
            bool triple = issue.Kind == PathCollisionDetector.IssueKind.TripleOverlap;
            Transform parent = triple ? _tripleRoot : _narrowRoot;
            Material mat = triple ? _matTriple : _matNarrow;
            string name = (triple ? "T" : "N") + "_L" + issue.LayerIndex;
            SpawnOverlay(parent, name, issue.SvgCenter, issue.SvgRadius, mat);
        }

        _root.gameObject.SetActive(_visible);
    }

    static void EnsureMaterials()
    {
        if (_matTriple == null)
            _matTriple = MakeOverlayMaterial(new Color(1f, 0.12f, 0.08f, 0.4f));
        if (_matNarrow == null)
            _matNarrow = MakeOverlayMaterial(new Color(1f, 0.88f, 0.1f, 0.4f));
    }

    static Material MakeOverlayMaterial(Color color)
    {
        Shader sh = Shader.Find("Hidden/Internal-Colored");
        if (sh == null) sh = Shader.Find("Sprites/Default");
        if (sh == null) sh = Shader.Find("Unlit/Color");

        var m = new Material(sh);
        m.color = color;
        if (m.HasProperty("_Color")) m.SetColor("_Color", color);
        m.SetInt("_ZTest", (int)CompareFunction.Always);
        m.SetInt("_ZWrite", 0);
        if (m.HasProperty("_Cull"))
            m.SetInt("_Cull", (int)CullMode.Off);
        m.renderQueue = 4000;
        return m;
    }

    static void SpawnOverlay(Transform parent, string name, Vector2 svgCenter, float svgRadius, Material mat)
    {
        float rSvg = Mathf.Max(MinSvgRadius, svgRadius);
        Vector2 u0 = MathOfRwrme.SvgPosToU3dPos(svgCenter + new Vector2(-rSvg, -rSvg));
        Vector2 u1 = MathOfRwrme.SvgPosToU3dPos(svgCenter + new Vector2(rSvg, rSvg));

        float minX = Mathf.Min(u0.x, u1.x);
        float maxX = Mathf.Max(u0.x, u1.x);
        float minZ = Mathf.Min(u0.y, u1.y);
        float maxZ = Mathf.Max(u0.y, u1.y);

        float w = Mathf.Max(MinWorldEdge, maxX - minX);
        float d = Mathf.Max(MinWorldEdge, maxZ - minZ);

        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Quad);
        go.name = name;
        go.transform.SetParent(parent, false);

        Collider col = go.GetComponent<Collider>();
        if (col != null) Object.Destroy(col);

        // 顶部水平遮罩：高空俯视平面，Always ZTest 不被建筑挡住
        go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        go.transform.position = new Vector3((minX + maxX) * 0.5f, OverlayY, (minZ + maxZ) * 0.5f);
        go.transform.localScale = new Vector3(w, d, 1f);

        Renderer rend = go.GetComponent<Renderer>();
        if (rend != null)
        {
            rend.sharedMaterial = mat;
            rend.shadowCastingMode = ShadowCastingMode.Off;
            rend.receiveShadows = false;
        }
    }
}
