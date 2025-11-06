using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class MetaMap : MonoBehaviour
{

    public int mapSizeX;
    public int mapSizeY;
    public MetaTerrain m_metaTerrain;

    // Start is called before the first frame update
    void Start()
    {
        m_metaTerrain = new MetaTerrain();
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}

public class GrayScaleImage
{
    [SerializeField] private float[,] data;

    public int Width => data?.GetLength(1) ?? 0;
    public int Height => data?.GetLength(0) ?? 0;

    public GrayScaleImage(int width, int height)
    {
        data = new float[height, width];
    }

    public GrayScaleImage(float[,] sourceData)
    {
        if (sourceData == null) return;

        data = new float[sourceData.GetLength(0), sourceData.GetLength(1)];
        System.Array.Copy(sourceData, data, sourceData.Length);
    }

    public float this[int y, int x]
    {
        get => IsValidCoordinate(y, x) ? data[y, x] : 0f;
        set { if (IsValidCoordinate(y, x)) data[y, x] = value; }
    }

    private bool IsValidCoordinate(int y, int x)
    {
        return data != null && y >= 0 && y < Height && x >= 0 && x < Width;
    }

    public void Resize(int newWidth, int newHeight)
    {
        var newData = new float[newHeight, newWidth];

        int copyWidth = Mathf.Min(Width, newWidth);
        int copyHeight = Mathf.Min(Height, newHeight);

        for (int y = 0; y < copyHeight; y++)
            for (int x = 0; x < copyWidth; x++)
                newData[y, x] = data[y, x];

        data = newData;
    }
}
public class MetaTerrain : MonoBehaviour
{
    public GrayScaleImage data;
    public int resolutionX;
    public int resolutionY;
    int mapHeight;
    int waterHeight;
    public int maxHeight;
    public MetaTerrain()
    {
        data = new GrayScaleImage(0, 0);
    }
    public void setData(GrayScaleImage igsi)
    {
        resolutionY = igsi.Height;
        resolutionX = igsi.Width;
        data = igsi;
    }
}