using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// 俯视栅格检测：同 layerIndex 内碰撞足迹。
/// Triple：仅 Building×Building、MeMesh×MeMesh（同类型、不含 Wall、不跨类型）。
/// Narrow：Building/Wall/MeMesh 作为障碍。
/// </summary>
public static class PathCollisionDetector
{
    public const float DefaultMinWidthWorld = 1f;
    public const float DefaultCellSizeSvg = 2f;
    public const float SvgMapSize = 2048f;

    public enum IssueKind
    {
        TripleOverlap = 0,
        NarrowGap = 1,
    }

    enum FootprintKind
    {
        Building = 0,
        Wall = 1,
        MeMesh = 2,
    }

    public sealed class Issue
    {
        public IssueKind Kind;
        public int LayerIndex;
        public Vector2 SvgCenter;
        public float SvgRadius;
    }

    public sealed class ScanResult
    {
        public readonly List<Issue> Issues = new List<Issue>();
        public int TripleClusters;
        public int NarrowClusters;
        public string Message = "";
    }

    public sealed class ScanOptions
    {
        /// <summary>null 或空 = 逐层；否则只扫列出的 layerIndex。</summary>
        public int[] LayerFilter;
        public float MinWidthWorld = DefaultMinWidthWorld;
        public float CellSizeSvg = DefaultCellSizeSvg;
        public Action<string> Progress;
    }

    struct Footprint
    {
        public int OwnerKey;
        public FootprintKind Kind;
        public Vector2[] SvgCorners; // 凸四边形（OBB）
    }

    /// <summary>同步扫描（小图）；大图请用 <see cref="ScanCoroutine"/>。</summary>
    public static ScanResult Scan(ScanOptions options = null)
    {
        ScanResult result = null;
        var e = ScanCoroutine(options, r => result = r);
        while (e.MoveNext()) { }
        return result ?? new ScanResult { Message = "empty" };
    }

    public static IEnumerator ScanCoroutine(ScanOptions options, Action<ScanResult> onDone)
    {
        options ??= new ScanOptions();
        var result = new ScanResult();
        if (MetaMap.instance == null || MetaMap.instance.defaultLayer == null)
        {
            result.Message = "no MetaMap";
            onDone?.Invoke(result);
            yield break;
        }

        float cell = Mathf.Max(0.5f, options.CellSizeSvg);
        float minWidthSvg = Mathf.Max(0.1f, options.MinWidthWorld * 2f); // world*2 = svg

        var layers = CollectLayers(options.LayerFilter);
        if (layers.Count == 0)
        {
            result.Message = "no layers";
            onDone?.Invoke(result);
            yield break;
        }

        for (int li = 0; li < layers.Count; li++)
        {
            int layer = layers[li];
            options.Progress?.Invoke($"layer {layer} ({li + 1}/{layers.Count}) collect");
            // 严格同层足迹；绝不与其它 layerIndex 混合
            var footprints = CollectFootprints(layer);
            yield return null;

            if (footprints.Count == 0)
                continue;

            options.Progress?.Invoke($"layer {layer} rasterize ({footprints.Count} boxes)");
            if (!TryBuildGrid(footprints, cell, out int gw, out int gh, out float originX, out float originY,
                    out byte[] occupied, out _, out _, out _))
            {
                continue;
            }
            yield return null;

            // --- Rule A: triple 仅同类型（Building 一组、MeMesh 一组；不含 Wall）---
            result.TripleClusters += DetectSameTypeTriples(
                footprints, FootprintKind.Building, cell, layer, result.Issues, options.Progress);
            yield return null;
            result.TripleClusters += DetectSameTypeTriples(
                footprints, FootprintKind.MeMesh, cell, layer, result.Issues, options.Progress);
            yield return null;

            // --- Rule B: narrow throats（本层全部障碍含 Wall）---
            options.Progress?.Invoke($"layer {layer} narrow");
            bool[] free = new bool[gw * gh];
            for (int i = 0; i < free.Length; i++)
                free[i] = occupied[i] == 0;

            float[] clearance = ChamferDistance(free, gw, gh, cell);
            float narrowLim = minWidthSvg * 0.5f;
            float openLim = minWidthSvg;

            int[] openLabel = LabelOpenComponents(free, clearance, openLim, gw, gh, minCells: 8);
            bool[] throat = MarkThroats(free, clearance, narrowLim, openLabel, gw, gh);
            int narrows = ClusterAndEmit(throat, gw, gh, originX, originY, cell, layer,
                IssueKind.NarrowGap, result.Issues);
            result.NarrowClusters += narrows;
            yield return null;
        }

        result.Message = $"triple {result.TripleClusters}, narrow {result.NarrowClusters}";
        onDone?.Invoke(result);
    }

    static List<int> CollectLayers(int[] filter)
    {
        var set = new SortedSet<int>();
        if (filter != null && filter.Length > 0)
        {
            foreach (int f in filter)
            {
                if (f >= 0) set.Add(f);
            }
            return set.ToList();
        }

        foreach (MapItem mi in MetaMap.instance.EnumerateAllMapItems())
        {
            if (mi is Building || mi is Wall || mi is MeMesh)
                set.Add(mi.layerIndex);
        }
        return set.ToList();
    }

    static List<Footprint> CollectFootprints(int layer)
    {
        var list = new List<Footprint>();
        // owner key = "L{layer}|{id}"，杜绝跨层 id 碰撞；本函数只收集该 layer
        var ownerIds = new Dictionary<string, int>(StringComparer.Ordinal);
        int nextOwner = 1;

        int KeyOf(string id)
        {
            if (string.IsNullOrEmpty(id)) id = "_anon_" + nextOwner;
            string key = "L" + layer + "|" + id;
            if (!ownerIds.TryGetValue(key, out int k))
            {
                k = nextOwner++;
                if (nextOwner > 65534) nextOwner = 65534;
                ownerIds[key] = k;
            }
            return k;
        }

        foreach (MapItem mi in MetaMap.instance.EnumerateAllMapItems())
        {
            if (mi == null || mi.layerIndex != layer) continue;

            if (mi is Building b)
            {
                list.Add(new Footprint
                {
                    OwnerKey = KeyOf(b.id),
                    Kind = FootprintKind.Building,
                    SvgCorners = MeRectSvgCorners(b),
                });
            }
            else if (mi is MeMesh mesh)
            {
                Vector2[] corners = MeMeshSvgCorners(mesh);
                if (corners != null)
                {
                    list.Add(new Footprint
                    {
                        OwnerKey = KeyOf(mesh.id),
                        Kind = FootprintKind.MeMesh,
                        SvgCorners = corners,
                    });
                }
            }
            else if (mi is Wall wall)
            {
                int owner = KeyOf(wall.id);
                float depthWorld = 1f;
                if (MetaMap.instance.wallTypes != null)
                {
                    WallType wt = MetaMap.instance.wallTypes.FirstOrDefault(t => t.name == wall.material);
                    if (wt != null) depthWorld = Mathf.Max(0.5f, wt.depth);
                }
                float halfThickSvg = depthWorld;

                if (wall.positionLine == null || wall.positionLine.Count < 2) continue;
                for (int i = 0; i < wall.positionLine.Count - 1; i++)
                {
                    Vector2 a = wall.positionLine[i];
                    Vector2 bpt = wall.positionLine[i + 1];
                    list.Add(new Footprint
                    {
                        OwnerKey = owner,
                        Kind = FootprintKind.Wall,
                        SvgCorners = SegmentRectCorners(a, bpt, halfThickSvg),
                    });
                }
            }
        }
        return list;
    }

    static int CountDistinctOwners(List<Footprint> footprints)
    {
        var set = new HashSet<int>();
        for (int i = 0; i < footprints.Count; i++)
            set.Add(footprints[i].OwnerKey);
        return set.Count;
    }

    /// <summary>仅在同 Kind 足迹内做 triple；Building / MeMesh 分开调用。</summary>
    static int DetectSameTypeTriples(
        List<Footprint> all, FootprintKind kind, float cell, int layer,
        List<Issue> issues, Action<string> progress)
    {
        var typed = new List<Footprint>();
        for (int i = 0; i < all.Count; i++)
        {
            if (all[i].Kind == kind)
                typed.Add(all[i]);
        }

        int owners = CountDistinctOwners(typed);
        string tag = kind == FootprintKind.Building ? "building" : "mesh";
        if (owners < 3)
        {
            progress?.Invoke($"layer {layer} triple/{tag} skip (owners={owners}<3)");
            return 0;
        }

        progress?.Invoke($"layer {layer} triple/{tag} (owners={owners})");
        if (!TryBuildGrid(typed, cell, out int gw, out int gh, out float ox, out float oy,
                out _, out _, out _, out bool[] tripleMask))
            return 0;

        return ClusterAndEmit(tripleMask, gw, gh, ox, oy, cell, layer, IssueKind.TripleOverlap, issues);
    }

    /// <summary>与 MapExporter.GetMeRectMapFootprintCorners 一致，输出 SVG 四角。</summary>
    static Vector2[] MeRectSvgCorners(MeRect r)
    {
        Vector2 root = MathOfRwrme.SvgPosToU3dPos(r.position);
        float halfW = r.size.x * 0.5f;
        float halfD = r.size.y * 0.5f;
        Vector3[] local =
        {
            new Vector3(0f, 0f, 0f),
            new Vector3(halfW, 0f, 0f),
            new Vector3(halfW, 0f, -halfD),
            new Vector3(0f, 0f, -halfD),
        };
        Quaternion rot = Quaternion.Euler(0f, -r.rotation, 0f);
        var corners = new Vector2[4];
        for (int i = 0; i < 4; i++)
        {
            Vector3 o = rot * local[i];
            corners[i] = MathOfRwrme.U3dPosToSvgPos(root + new Vector2(o.x, o.z));
        }
        return corners;
    }

    static Vector2[] MeMeshSvgCorners(MeMesh mesh)
    {
        Vector3 extend = Vector3.one;
        if (mesh.reCollision)
            extend = mesh.collisionSize;
        else if (MetaMap.instance != null && mesh.templated && !string.IsNullOrEmpty(mesh.template_ref))
        {
            MeshTemplate t = MetaMap.instance.meshTemplates?
                .FirstOrDefault(x => x.name == mesh.template_ref);
            if (t != null) extend = t.extend;
            else return null;
        }
        else if (!mesh.templated)
            return null;

        // 碰撞世界尺寸 = extend.xz；SVG 全宽 = 2 * world
        float halfSvgX = extend.x;
        float halfSvgZ = extend.z;
        Vector2 center = mesh.position;
        float rad = -mesh.rotation * Mathf.Deg2Rad;
        float cos = Mathf.Cos(rad);
        float sin = Mathf.Sin(rad);

        Vector2[] local =
        {
            new Vector2(-halfSvgX, -halfSvgZ),
            new Vector2(halfSvgX, -halfSvgZ),
            new Vector2(halfSvgX, halfSvgZ),
            new Vector2(-halfSvgX, halfSvgZ),
        };
        var corners = new Vector2[4];
        for (int i = 0; i < 4; i++)
        {
            float x = cos * local[i].x - sin * local[i].y;
            float y = sin * local[i].x + cos * local[i].y;
            corners[i] = center + new Vector2(x, y);
        }
        return corners;
    }

    static Vector2[] SegmentRectCorners(Vector2 a, Vector2 b, float halfThickSvg)
    {
        Vector2 d = b - a;
        float len = d.magnitude;
        if (len < 1e-4f)
        {
            return new[]
            {
                a + new Vector2(-halfThickSvg, -halfThickSvg),
                a + new Vector2(halfThickSvg, -halfThickSvg),
                a + new Vector2(halfThickSvg, halfThickSvg),
                a + new Vector2(-halfThickSvg, halfThickSvg),
            };
        }
        Vector2 n = new Vector2(-d.y, d.x) / len * halfThickSvg;
        return new[]
        {
            a + n,
            a - n,
            b - n,
            b + n,
        };
    }

    static bool TryBuildGrid(List<Footprint> footprints, float cell,
        out int gw, out int gh, out float originX, out float originY,
        out byte[] occupied, out ushort[] ownerA, out ushort[] ownerB, out bool[] tripleMask)
    {
        float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
        foreach (var fp in footprints)
        {
            foreach (var c in fp.SvgCorners)
            {
                minX = Mathf.Min(minX, c.x);
                minY = Mathf.Min(minY, c.y);
                maxX = Mathf.Max(maxX, c.x);
                maxY = Mathf.Max(maxY, c.y);
            }
        }
        minX = Mathf.Clamp(minX - cell, 0f, SvgMapSize);
        minY = Mathf.Clamp(minY - cell, 0f, SvgMapSize);
        maxX = Mathf.Clamp(maxX + cell, 0f, SvgMapSize);
        maxY = Mathf.Clamp(maxY + cell, 0f, SvgMapSize);

        gw = Mathf.Clamp(Mathf.CeilToInt((maxX - minX) / cell), 1, 1024);
        gh = Mathf.Clamp(Mathf.CeilToInt((maxY - minY) / cell), 1, 1024);
        originX = minX;
        originY = minY;

        int n = gw * gh;
        occupied = new byte[n];
        ownerA = new ushort[n];
        ownerB = new ushort[n];
        tripleMask = new bool[n];

        foreach (var fp in footprints)
        {
            RasterizeQuad(fp.SvgCorners, fp.OwnerKey, originX, originY, cell, gw, gh,
                occupied, ownerA, ownerB, tripleMask);
        }
        return true;
    }

    static void RasterizeQuad(Vector2[] corners, int owner, float ox, float oy, float cell, int gw, int gh,
        byte[] occupied, ushort[] ownerA, ushort[] ownerB, bool[] triple)
    {
        float minX = corners[0].x, maxX = corners[0].x, minY = corners[0].y, maxY = corners[0].y;
        for (int i = 1; i < 4; i++)
        {
            minX = Mathf.Min(minX, corners[i].x);
            maxX = Mathf.Max(maxX, corners[i].x);
            minY = Mathf.Min(minY, corners[i].y);
            maxY = Mathf.Max(maxY, corners[i].y);
        }

        int x0 = Mathf.Clamp(Mathf.FloorToInt((minX - ox) / cell), 0, gw - 1);
        int x1 = Mathf.Clamp(Mathf.FloorToInt((maxX - ox) / cell), 0, gw - 1);
        int y0 = Mathf.Clamp(Mathf.FloorToInt((minY - oy) / cell), 0, gh - 1);
        int y1 = Mathf.Clamp(Mathf.FloorToInt((maxY - oy) / cell), 0, gh - 1);

        ushort oid = (ushort)Mathf.Clamp(owner, 0, 65535);
        for (int gy = y0; gy <= y1; gy++)
        {
            for (int gx = x0; gx <= x1; gx++)
            {
                float cx = ox + (gx + 0.5f) * cell;
                float cy = oy + (gy + 0.5f) * cell;
                if (!PointInConvexQuad(corners, new Vector2(cx, cy))) continue;

                int idx = gy * gw + gx;
                occupied[idx] = 1;
                if (ownerA[idx] == 0)
                    ownerA[idx] = oid;
                else if (ownerA[idx] != oid && ownerB[idx] == 0)
                    ownerB[idx] = oid;
                else if (ownerA[idx] != oid && ownerB[idx] != oid)
                    triple[idx] = true;
            }
        }
    }

    static bool PointInConvexQuad(Vector2[] q, Vector2 p)
    {
        bool sign = false;
        for (int i = 0; i < 4; i++)
        {
            Vector2 a = q[i];
            Vector2 b = q[(i + 1) % 4];
            float cross = (b.x - a.x) * (p.y - a.y) - (b.y - a.y) * (p.x - a.x);
            if (i == 0) sign = cross >= 0f;
            else if ((cross >= 0f) != sign && Mathf.Abs(cross) > 1e-5f)
                return false;
        }
        return true;
    }

    static int ClusterAndEmit(bool[] mask, int gw, int gh, float ox, float oy, float cell, int layer,
        IssueKind kind, List<Issue> issues)
    {
        int clusters = 0;
        var visited = new bool[mask.Length];
        var queue = new Queue<int>();

        for (int i = 0; i < mask.Length; i++)
        {
            if (!mask[i] || visited[i]) continue;
            queue.Clear();
            queue.Enqueue(i);
            visited[i] = true;
            float sumX = 0f, sumY = 0f;
            int count = 0;
            float minX = float.MaxValue, maxX = float.MinValue, minY = float.MaxValue, maxY = float.MinValue;

            while (queue.Count > 0)
            {
                int cur = queue.Dequeue();
                int cx = cur % gw;
                int cy = cur / gw;
                float px = ox + (cx + 0.5f) * cell;
                float py = oy + (cy + 0.5f) * cell;
                sumX += px;
                sumY += py;
                count++;
                minX = Mathf.Min(minX, px);
                maxX = Mathf.Max(maxX, px);
                minY = Mathf.Min(minY, py);
                maxY = Mathf.Max(maxY, py);

                for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dy == 0) continue;
                    int nx = cx + dx;
                    int ny = cy + dy;
                    if (nx < 0 || ny < 0 || nx >= gw || ny >= gh) continue;
                    int ni = ny * gw + nx;
                    if (!mask[ni] || visited[ni]) continue;
                    visited[ni] = true;
                    queue.Enqueue(ni);
                }
            }

            if (count == 0) continue;
            clusters++;
            float radius = 0.5f * Mathf.Max(maxX - minX, maxY - minY) + cell;
            issues.Add(new Issue
            {
                Kind = kind,
                LayerIndex = layer,
                SvgCenter = new Vector2(sumX / count, sumY / count),
                SvgRadius = Mathf.Max(cell, radius),
            });
        }
        return clusters;
    }

    /// <summary>3-4 倒角距离变换，返回 SVG 单位净空（到障碍）。</summary>
    static float[] ChamferDistance(bool[] free, int gw, int gh, float cell)
    {
        const float INF = 1e8f;
        float[] d = new float[gw * gh];
        for (int i = 0; i < d.Length; i++)
            d[i] = free[i] ? INF : 0f;

        // forward
        for (int y = 0; y < gh; y++)
        for (int x = 0; x < gw; x++)
        {
            int i = y * gw + x;
            if (!free[i]) continue;
            float v = d[i];
            if (x > 0) v = Mathf.Min(v, d[i - 1] + cell);
            if (y > 0) v = Mathf.Min(v, d[i - gw] + cell);
            if (x > 0 && y > 0) v = Mathf.Min(v, d[i - gw - 1] + cell * 1.414f);
            if (x + 1 < gw && y > 0) v = Mathf.Min(v, d[i - gw + 1] + cell * 1.414f);
            d[i] = v;
        }
        // backward
        for (int y = gh - 1; y >= 0; y--)
        for (int x = gw - 1; x >= 0; x--)
        {
            int i = y * gw + x;
            if (!free[i]) continue;
            float v = d[i];
            if (x + 1 < gw) v = Mathf.Min(v, d[i + 1] + cell);
            if (y + 1 < gh) v = Mathf.Min(v, d[i + gw] + cell);
            if (x + 1 < gw && y + 1 < gh) v = Mathf.Min(v, d[i + gw + 1] + cell * 1.414f);
            if (x > 0 && y + 1 < gh) v = Mathf.Min(v, d[i + gw - 1] + cell * 1.414f);
            d[i] = v;
        }
        return d;
    }

    static int[] LabelOpenComponents(bool[] free, float[] clearance, float openLim, int gw, int gh, int minCells)
    {
        int[] label = new int[gw * gh];
        int current = 0;
        var queue = new Queue<int>();
        var componentSizes = new List<int> { 0 };

        for (int i = 0; i < label.Length; i++)
        {
            if (!free[i] || clearance[i] < openLim || label[i] != 0) continue;
            current++;
            int size = 0;
            queue.Clear();
            queue.Enqueue(i);
            label[i] = current;
            while (queue.Count > 0)
            {
                int cur = queue.Dequeue();
                size++;
                int cx = cur % gw;
                int cy = cur / gw;
                for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dy == 0) continue;
                    int nx = cx + dx, ny = cy + dy;
                    if (nx < 0 || ny < 0 || nx >= gw || ny >= gh) continue;
                    int ni = ny * gw + nx;
                    if (!free[ni] || clearance[ni] < openLim || label[ni] != 0) continue;
                    label[ni] = current;
                    queue.Enqueue(ni);
                }
            }
            while (componentSizes.Count <= current) componentSizes.Add(0);
            componentSizes[current] = size;
        }

        // 抹掉过小开阔区
        for (int i = 0; i < label.Length; i++)
        {
            int L = label[i];
            if (L > 0 && componentSizes[L] < minCells)
                label[i] = 0;
        }
        return label;
    }

    static bool[] MarkThroats(bool[] free, float[] clearance, float narrowLim, int[] openLabel, int gw, int gh)
    {
        var throat = new bool[gw * gh];
        for (int y = 0; y < gh; y++)
        for (int x = 0; x < gw; x++)
        {
            int i = y * gw + x;
            if (!free[i]) continue;
            if (clearance[i] >= narrowLim) continue; // 不够窄
            // 邻域是否碰到两个不同开阔连通块
            int seenA = 0, seenB = 0;
            for (int dy = -2; dy <= 2; dy++)
            for (int dx = -2; dx <= 2; dx++)
            {
                int nx = x + dx, ny = y + dy;
                if (nx < 0 || ny < 0 || nx >= gw || ny >= gh) continue;
                int L = openLabel[ny * gw + nx];
                if (L == 0) continue;
                if (seenA == 0) seenA = L;
                else if (L != seenA && seenB == 0) seenB = L;
            }
            if (seenA != 0 && seenB != 0)
                throat[i] = true;
        }
        return throat;
    }
}
