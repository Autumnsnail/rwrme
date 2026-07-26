using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CtrlZer : MonoBehaviour
{
    public static CtrlZer instance;

    private const int MaxHistorySize = 50;

    private struct ItemTransformData
    {
        public MapItem item;
        public Vector2 position;
        public float rotation;
        public Vector2 size;
    }

    private struct PathData
    {
        public MapItem item;
        public List<Vector2> positionLine;       // for MePath (Wall)
        public List<Vector2> positinLineL;       // for PathPair (Platform)
        public List<Vector2> positinLineR;       // for PathPair (Platform)
    }

    private struct Snapshot
    {
        public bool transformOnly;
        public List<MapItem> defaultItems;
        public List<MapItem> baseItems;
        public List<MapItem> offroadItems;
        public List<MapItem> heightPathItems;
        public List<MapItem> materialPathItems;
        public List<ItemTransformData> transformData;
        public List<PathData> pathData;
        public float[,] heightmapData;
        public Color[] maskPixels;
        public int maskWidth;
        public int maskHeight;
    }

    private List<Snapshot> undoStack = new List<Snapshot>();
    private List<Snapshot> redoStack = new List<Snapshot>();

    void Start()
    {
        instance = this;
    }

    void Update()
    {
        if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl))
        {
            if (Input.GetKeyDown(KeyCode.Z))
            {
                if (ToolController.inste != null)
                {
                    var tool = ToolController.inste.currentTool;
                    if (tool is WallDrawer wd && wd.drawing)
                    {
                        wd.RemoveLastVertex();
                        return;
                    }
                    if (tool is PlatformDrawer pd && pd.IsDrawing)
                    {
                        pd.RemoveLastVertex();
                        return;
                    }
                    if (tool is OffraodDrawer od && od.drawing)
                    {
                        od.RemoveLastVertex();
                        return;
                    }
                    if (tool is PostDrawer postDrawer && postDrawer.drawing)
                    {
                        postDrawer.RemoveLastVertex();
                        return;
                    }
                    if (tool is HeightPathDrawer hpd && hpd.drawing)
                    {
                        hpd.RemoveLastVertex();
                        return;
                    }
                    if (tool is MaterialPathDrawer mpd && mpd.drawing)
                    {
                        mpd.RemoveLastVertex();
                        return;
                    }
                }
                Undo();
            }
            if (Input.GetKeyDown(KeyCode.Y))
            {
                Redo();
            }
        }
    }

    private Snapshot CaptureSnapshot()
    {
        var defItems = new List<MapItem>(MetaMap.instance.defaultLayer.mapItems);
        var bsItems = new List<MapItem>(MetaMap.instance.baseLayer.mapItems);
        var orItems = MetaMap.instance.offroadLayer != null
            ? new List<MapItem>(MetaMap.instance.offroadLayer.mapItems)
            : new List<MapItem>();
        var hpItems = MetaMap.instance.heightPathLayer != null
            ? new List<MapItem>(MetaMap.instance.heightPathLayer.mapItems)
            : new List<MapItem>();
        var mpItems = MetaMap.instance.materialPathLayer != null
            ? new List<MapItem>(MetaMap.instance.materialPathLayer.mapItems)
            : new List<MapItem>();
        var transforms = new List<ItemTransformData>();
        var paths = new List<PathData>();

        CaptureItemData(defItems, transforms, paths);
        CaptureItemData(bsItems, transforms, paths);
        CaptureItemData(orItems, transforms, paths);
        CaptureItemData(hpItems, transforms, paths);
        CaptureItemData(mpItems, transforms, paths);

        return new Snapshot
        {
            defaultItems = defItems,
            baseItems = bsItems,
            offroadItems = orItems,
            heightPathItems = hpItems,
            materialPathItems = mpItems,
            transformData = transforms,
            pathData = paths
        };
    }

    private void CaptureItemData(List<MapItem> items, List<ItemTransformData> transforms, List<PathData> paths)
    {
        foreach (var item in items)
        {
            if (item is MeRect mr)
            {
                transforms.Add(new ItemTransformData
                {
                    item = mr,
                    position = mr.position,
                    rotation = mr.rotation,
                    size = mr.size
                });
            }

            if (item is MePath mp)
            {
                paths.Add(new PathData
                {
                    item = mp,
                    positionLine = new List<Vector2>(mp.positionLine)
                });
            }
            else if (item is PathPair pp)
            {
                paths.Add(new PathData
                {
                    item = pp,
                    positinLineL = new List<Vector2>(pp.positinLineL),
                    positinLineR = new List<Vector2>(pp.positinLineR)
                });
            }
        }
    }

    private void PushUndo(Snapshot snap)
    {
        undoStack.Add(snap);
        if (undoStack.Count > MaxHistorySize)
            undoStack.RemoveAt(0);

        DestroyRedoOrphans();
        redoStack.Clear();

        Debug.Log("CtrlZer: Checkpoint saved (undo depth: " + undoStack.Count + ")");
    }

    /// <summary>
    /// Save mapItems state before a non-terrain operation.
    /// </summary>
    public void checkPoint()
    {
        PushUndo(CaptureSnapshot());
    }

    /// <summary>
    /// Save only transform data before a move/rotate/scale operation.
    /// Undo will restore transforms without touching item lists or visibility.
    /// </summary>
    public void checkPointTransformOnly()
    {
        Snapshot snap = CaptureSnapshot();
        snap.transformOnly = true;
        PushUndo(snap);
    }

    /// <summary>
    /// Save mapItems + terrain heightmap before a height-modifying operation.
    /// </summary>
    public void checkPointWithHeightmap()
    {
        Snapshot snap = CaptureSnapshot();
        MetaMap mm = MetaMap.instance;
        mm.EnsurePreTerrain();
        if (mm.m_preTerrain?.data != null)
            snap.heightmapData = mm.m_preTerrain.data.CopyDataArray();
        PushUndo(snap);
    }

    /// <summary>
    /// Save mapItems + pre terrain mask before a terrain-paint operation.
    /// </summary>
    public void checkPointWithTerrainMask()
    {
        Snapshot snap = CaptureSnapshot();
        MetaMap mm = MetaMap.instance;
        mm.EnsurePreCombinedAlpha();
        Texture2D tex = mm.preCombinedAlpha;
        if (tex != null)
        {
            snap.maskPixels = tex.GetPixels();
            snap.maskWidth = tex.width;
            snap.maskHeight = tex.height;
        }
        PushUndo(snap);
    }

    public void Undo()
    {
        if (undoStack.Count == 0)
        {
            Debug.Log("CtrlZer: Nothing to undo");
            return;
        }

        Snapshot target = undoStack[undoStack.Count - 1];
        undoStack.RemoveAt(undoStack.Count - 1);

        Snapshot current;
        if (target.transformOnly)
        {
            current = CaptureSnapshot();
            current.transformOnly = true;
        }
        else
        {
            current = CaptureCurrentFullSnapshot();
        }
        redoStack.Add(current);

        RestoreSnapshot(target);
        Debug.Log("CtrlZer: Undo (remaining: " + undoStack.Count + ")");
    }

    public void Redo()
    {
        if (redoStack.Count == 0)
        {
            Debug.Log("CtrlZer: Nothing to redo");
            return;
        }

        Snapshot target = redoStack[redoStack.Count - 1];
        redoStack.RemoveAt(redoStack.Count - 1);

        Snapshot current;
        if (target.transformOnly)
        {
            current = CaptureSnapshot();
            current.transformOnly = true;
        }
        else
        {
            current = CaptureCurrentFullSnapshot();
        }
        undoStack.Add(current);

        RestoreSnapshot(target);
        Debug.Log("CtrlZer: Redo (remaining: " + redoStack.Count + ")");
    }

    /// <summary>
    /// Capture a snapshot that mirrors whatever data types the target snapshot
    /// might contain, so that undo/redo round-trips correctly for terrain data.
    /// Always captures both heightmap and mask to ensure nothing is lost.
    /// </summary>
    private Snapshot CaptureCurrentFullSnapshot()
    {
        Snapshot snap = CaptureSnapshot();

        Terrain terrain = Terrain.activeTerrain;
        MetaMap mm = MetaMap.instance;
        if (terrain != null && mm != null)
        {
            mm.EnsurePreTerrain();
            if (mm.m_preTerrain?.data != null)
                snap.heightmapData = mm.m_preTerrain.data.CopyDataArray();

            mm.EnsurePreCombinedAlpha();
            if (mm.preCombinedAlpha != null)
            {
                snap.maskPixels = mm.preCombinedAlpha.GetPixels();
                snap.maskWidth = mm.preCombinedAlpha.width;
                snap.maskHeight = mm.preCombinedAlpha.height;
            }
        }

        return snap;
    }

    private void RestoreSnapshot(Snapshot snapshot)
    {
        if (!snapshot.transformOnly)
        {
            HashSet<MapItem> previousItems = new HashSet<MapItem>(MetaMap.instance.defaultLayer.mapItems);
            foreach (var item in MetaMap.instance.baseLayer.mapItems)
                previousItems.Add(item);
            if (MetaMap.instance.offroadLayer != null)
            {
                foreach (var item in MetaMap.instance.offroadLayer.mapItems)
                    previousItems.Add(item);
            }
            if (MetaMap.instance.heightPathLayer != null)
            {
                foreach (var item in MetaMap.instance.heightPathLayer.mapItems)
                    previousItems.Add(item);
            }
            if (MetaMap.instance.materialPathLayer != null)
            {
                foreach (var item in MetaMap.instance.materialPathLayer.mapItems)
                    previousItems.Add(item);
            }

            MetaMap.instance.defaultLayer.mapItems = new List<MapItem>(snapshot.defaultItems);
            MetaMap.instance.baseLayer.mapItems = new List<MapItem>(snapshot.baseItems);
            if (MetaMap.instance.offroadLayer != null)
                MetaMap.instance.offroadLayer.mapItems = snapshot.offroadItems != null
                    ? new List<MapItem>(snapshot.offroadItems)
                    : new List<MapItem>();
            if (MetaMap.instance.heightPathLayer != null)
                MetaMap.instance.heightPathLayer.mapItems = snapshot.heightPathItems != null
                    ? new List<MapItem>(snapshot.heightPathItems)
                    : new List<MapItem>();
            if (MetaMap.instance.materialPathLayer != null)
                MetaMap.instance.materialPathLayer.mapItems = snapshot.materialPathItems != null
                    ? new List<MapItem>(snapshot.materialPathItems)
                    : new List<MapItem>();

            HashSet<MapItem> restoredItems = new HashSet<MapItem>(snapshot.defaultItems);
            foreach (var item in snapshot.baseItems)
                restoredItems.Add(item);
            if (snapshot.offroadItems != null)
            {
                foreach (var item in snapshot.offroadItems)
                    restoredItems.Add(item);
            }
            if (snapshot.heightPathItems != null)
            {
                foreach (var item in snapshot.heightPathItems)
                    restoredItems.Add(item);
            }
            if (snapshot.materialPathItems != null)
            {
                foreach (var item in snapshot.materialPathItems)
                    restoredItems.Add(item);
            }

            MapItem[] allItems = FindObjectsOfType<MapItem>(true);
            foreach (MapItem item in allItems)
            {
                if (item == null) continue;
                bool wasInList = previousItems.Contains(item);
                bool nowInList = restoredItems.Contains(item);

                if (!wasInList && nowInList)
                    item.gameObject.SetActive(true);
                else if (wasInList && !nowInList)
                    item.gameObject.SetActive(false);
            }
        }

        if (snapshot.transformData != null)
        {
            foreach (var td in snapshot.transformData)
            {
                if (td.item is MeRect mr && mr != null)
                {
                    mr.position = td.position;
                    mr.rotation = td.rotation;
                    mr.size = td.size;
                }
            }
        }

        if (snapshot.pathData != null)
        {
            foreach (var pd in snapshot.pathData)
            {
                if (pd.item == null) continue;
                if (pd.item is MePath mp && pd.positionLine != null)
                {
                    mp.positionLine = new List<Vector2>(pd.positionLine);
                }
                else if (pd.item is PathPair pp)
                {
                    if (pd.positinLineL != null) pp.positinLineL = new List<Vector2>(pd.positinLineL);
                    if (pd.positinLineR != null) pp.positinLineR = new List<Vector2>(pd.positinLineR);
                }
            }
        }

        StartCoroutine(Syncer.instence.ScatterMapItems());

        if (!snapshot.transformOnly)
        {
            if (snapshot.heightmapData != null && MetaMap.instance != null)
            {
                MetaMap.instance.EnsurePreTerrain();
                MetaMap.instance.m_preTerrain.data.CopyFromArray(snapshot.heightmapData);
                if (Syncer.instence != null) Syncer.instence.ApplyPreHeightToTerrain();
            }

            if (snapshot.maskPixels != null && MetaMap.instance != null)
            {
                MetaMap.instance.EnsurePreCombinedAlpha();
                Texture2D tex = MetaMap.instance.preCombinedAlpha;
                if (tex != null && tex.width == snapshot.maskWidth && tex.height == snapshot.maskHeight)
                {
                    tex.SetPixels(snapshot.maskPixels);
                    tex.Apply();
                    if (Syncer.instence != null) Syncer.instence.ApplyPreAlphaToTerrain();
                }
            }

            if (Syncer.instence != null)
                Syncer.instence.ApplyPreviewTerrain();
        }
    }

    private void DestroyRedoOrphans()
    {
        if (redoStack.Count == 0) return;

        HashSet<MapItem> keepAlive = new HashSet<MapItem>(MetaMap.instance.defaultLayer.mapItems);
        foreach (var item in MetaMap.instance.baseLayer.mapItems)
            keepAlive.Add(item);
        if (MetaMap.instance.offroadLayer != null)
        {
            foreach (var item in MetaMap.instance.offroadLayer.mapItems)
                keepAlive.Add(item);
        }
        if (MetaMap.instance.heightPathLayer != null)
        {
            foreach (var item in MetaMap.instance.heightPathLayer.mapItems)
                keepAlive.Add(item);
        }
        if (MetaMap.instance.materialPathLayer != null)
        {
            foreach (var item in MetaMap.instance.materialPathLayer.mapItems)
                keepAlive.Add(item);
        }
        foreach (var snap in undoStack)
        {
            foreach (var item in snap.defaultItems) keepAlive.Add(item);
            foreach (var item in snap.baseItems) keepAlive.Add(item);
            if (snap.offroadItems != null)
            {
                foreach (var item in snap.offroadItems) keepAlive.Add(item);
            }
            if (snap.heightPathItems != null)
            {
                foreach (var item in snap.heightPathItems) keepAlive.Add(item);
            }
            if (snap.materialPathItems != null)
            {
                foreach (var item in snap.materialPathItems) keepAlive.Add(item);
            }
        }

        HashSet<MapItem> alreadyDestroyed = new HashSet<MapItem>();
        foreach (var snap in redoStack)
        {
            foreach (var item in snap.defaultItems)
            {
                if (item != null && !keepAlive.Contains(item) && alreadyDestroyed.Add(item))
                    Destroy(item.gameObject);
            }
            foreach (var item in snap.baseItems)
            {
                if (item != null && !keepAlive.Contains(item) && alreadyDestroyed.Add(item))
                    Destroy(item.gameObject);
            }
            if (snap.offroadItems != null)
            {
                foreach (var item in snap.offroadItems)
                {
                    if (item != null && !keepAlive.Contains(item) && alreadyDestroyed.Add(item))
                        Destroy(item.gameObject);
                }
            }
            if (snap.heightPathItems != null)
            {
                foreach (var item in snap.heightPathItems)
                {
                    if (item != null && !keepAlive.Contains(item) && alreadyDestroyed.Add(item))
                        Destroy(item.gameObject);
                }
            }
            if (snap.materialPathItems != null)
            {
                foreach (var item in snap.materialPathItems)
                {
                    if (item != null && !keepAlive.Contains(item) && alreadyDestroyed.Add(item))
                        Destroy(item.gameObject);
                }
            }
        }
    }
}
