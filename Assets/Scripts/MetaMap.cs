using JetBrains.Annotations;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

public class MetaMap : MonoBehaviour
{

    public int mapSizeX;
    public int mapSizeY;
    public MetaTerrain m_metaTerrain;

    // Start is called before the first frame update
    void Start()
    {
        m_metaTerrain = new MetaTerrain();
        Debug.Log("MetaMapInit");
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

    public byte[] convToPng()
    {
        Debug.Log("convGSItoPngBytes");
        byte[] pngInfo = null;
        Texture2D texture = new Texture2D(Width, Height, GraphicsFormat.R8_UNorm, TextureCreationFlags.None);
        for(int y = 0; y < Height;y++)
        {
            for(int x = 0;x < Width;x++)
            {
                Color color = new Color(this[y,x], this[y, x], this[y, x], 1f);
                texture.SetPixel(x, y, color);
            }
        }
        pngInfo = texture.EncodeToPNG();
        return pngInfo;
    }
}
public class MetaTerrain
{
    public GrayScaleImage data;
    public int resolutionX;
    public int resolutionY;
    public float waterHeight=2.0f;
    public float maxHeight=25.0f;
    public string fileName = "terrain5_heightmap.png";
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