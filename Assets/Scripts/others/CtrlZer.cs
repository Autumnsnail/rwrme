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

    private struct Snapshot
    {
        public List<MapItem> defaultItems;
        public List<MapItem> baseItems;
        public List<ItemTransformData> transformData;
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
        var transforms = new List<ItemTransformData>();

        CaptureTransforms(defItems, transforms);
        CaptureTransforms(bsItems, transforms);

        return new Snapshot
        {
            defaultItems = defItems,
            baseItems = bsItems,
            transformData = transforms
        };
    }

    private void CaptureTransforms(List<MapItem> items, List<ItemTransformData> output)
    {
        foreach (var item in items)
        {
            if (item is MeRect mr)
            {
                output.Add(new ItemTransformData
                {
                    item = mr,
                    position = mr.position,
                    rotation = mr.rotation,
                    size = mr.size
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
    /// Save mapItems + terrain heightmap before a height-modifying operation.
    /// </summary>
    public void checkPointWithHeightmap()
    {
        Snapshot snap = CaptureSnapshot();

        Terrain terrain = Terrain.activeTerrain;
        if (terrain != null)
        {
            TerrainData td = terrain.terrainData;
            int res = td.heightmapResolution;
            float[,] src = td.GetHeights(0, 0, res, res);
            float[,] copy = new float[res, res];
            System.Buffer.BlockCopy(src, 0, copy, 0, res * res * sizeof(float));
            snap.heightmapData = copy;
        }

        PushUndo(snap);
    }

    /// <summary>
    /// Save mapItems + terrain mask texture before a terrain-paint operation.
    /// </summary>
    public void checkPointWithTerrainMask()
    {
        Snapshot snap = CaptureSnapshot();

        Terrain terrain = Terrain.activeTerrain;
        if (terrain != null)
        {
            Texture2D tex = terrain.materialTemplate.GetTexture("_Mask") as Texture2D;
            if (tex != null)
            {
                snap.maskPixels = tex.GetPixels();
                snap.maskWidth = tex.width;
                snap.maskHeight = tex.height;
            }
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

        redoStack.Add(CaptureCurrentFullSnapshot());

        Snapshot snapshot = undoStack[undoStack.Count - 1];
        undoStack.RemoveAt(undoStack.Count - 1);

        RestoreSnapshot(snapshot);
        Debug.Log("CtrlZer: Undo (remaining: " + undoStack.Count + ")");
    }

    public void Redo()
    {
        if (redoStack.Count == 0)
        {
            Debug.Log("CtrlZer: Nothing to redo");
            return;
        }

        undoStack.Add(CaptureCurrentFullSnapshot());

        Snapshot snapshot = redoStack[redoStack.Count - 1];
        redoStack.RemoveAt(redoStack.Count - 1);

        RestoreSnapshot(snapshot);
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
        if (terrain != null)
        {
            TerrainData td = terrain.terrainData;
            int res = td.heightmapResolution;
            float[,] src = td.GetHeights(0, 0, res, res);
            float[,] copy = new float[res, res];
            System.Buffer.BlockCopy(src, 0, copy, 0, res * res * sizeof(float));
            snap.heightmapData = copy;

            Texture2D tex = terrain.materialTemplate.GetTexture("_Mask") as Texture2D;
            if (tex != null)
            {
                snap.maskPixels = tex.GetPixels();
                snap.maskWidth = tex.width;
                snap.maskHeight = tex.height;
            }
        }

        return snap;
    }

    private void RestoreSnapshot(Snapshot snapshot)
    {
        MetaMap.instance.defaultLayer.mapItems = new List<MapItem>(snapshot.defaultItems);
        MetaMap.instance.baseLayer.mapItems = new List<MapItem>(snapshot.baseItems);

        HashSet<MapItem> activeItems = new HashSet<MapItem>(snapshot.defaultItems);
        foreach (var item in snapshot.baseItems)
            activeItems.Add(item);

        MapItem[] allItems = FindObjectsOfType<MapItem>(true);
        foreach (MapItem item in allItems)
        {
            if (item == null) continue;
            item.gameObject.SetActive(activeItems.Contains(item));
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

        StartCoroutine(Syncer.instence.ScatterMapItems());

        if (snapshot.heightmapData != null)
        {
            Terrain terrain = Terrain.activeTerrain;
            if (terrain != null)
            {
                terrain.terrainData.SetHeights(0, 0, snapshot.heightmapData);
                terrain.terrainData.SyncHeightmap();
            }
        }

        if (snapshot.maskPixels != null)
        {
            Terrain terrain = Terrain.activeTerrain;
            if (terrain != null)
            {
                Texture2D tex = terrain.materialTemplate.GetTexture("_Mask") as Texture2D;
                if (tex != null)
                {
                    tex.SetPixels(snapshot.maskPixels);
                    tex.Apply();
                }
            }
        }
    }

    private void DestroyRedoOrphans()
    {
        if (redoStack.Count == 0) return;

        HashSet<MapItem> keepAlive = new HashSet<MapItem>(MetaMap.instance.defaultLayer.mapItems);
        foreach (var item in MetaMap.instance.baseLayer.mapItems)
            keepAlive.Add(item);
        foreach (var snap in undoStack)
        {
            foreach (var item in snap.defaultItems) keepAlive.Add(item);
            foreach (var item in snap.baseItems) keepAlive.Add(item);
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
        }
    }
}
