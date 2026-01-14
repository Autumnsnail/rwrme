using JetBrains.Annotations;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using Unity.VisualScripting;
using Unity.VisualScripting.Antlr3.Runtime.Misc;
using UnityEditor;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using static UnityEngine.Terrain;

public class MetaMap : MonoBehaviour
{
    public static MetaMap instance; 
    public int mapSizeX;
    public int mapSizeY;
    public MetaTerrain m_metaTerrain;
    public Layer defaultLayer;//default: L1,L2,L3,L4...

    public List<BuildingType> buildingTypes;
    public List<WallType> wallTypes;

    public List<PlatformSerfaceType> PST;

    public List<string> allowedExtensions = new List<string> { "default"};//import later

    // Start is called before the first frame update
    void Start()
    {
        instance = this;
        m_metaTerrain = new MetaTerrain();
        defaultLayer = new Layer();

        buildingTypes = new List<BuildingType>();
        wallTypes = new List<WallType>();

        PST = new List<PlatformSerfaceType>();
        setBts();
        setWts();

        setPsts();
        Debug.Log("MetaMapInit");
    }

    public string getNewItemId(string startWith)
    {
        return startWith + defaultLayer.mapItems.Count.ToString() + "rwrme";
    }

    private void setBts()
    {
        //for tests until we get way import this
        buildingTypes.Add(new BuildingType("BuildingWhite1", Color.gray, Color.white));
        buildingTypes.Add(new BuildingType("BuildingRoofStory1", Color.gray, Color.white));
        buildingTypes.Add(new BuildingType("BuildingWhite1Empty1stFloor", Color.gray, Color.white));
        buildingTypes.Add(new BuildingType("BuildingWhite1Busy", Color.gray, Color.white));
        buildingTypes.Add(new BuildingType("BuildingWhite1Brick", Color.gray, Color.white));
        buildingTypes.Add(new BuildingType("BuildingShop1", Color.gray, Color.white));
        buildingTypes.Add(new BuildingType("BuildingShop2", Color.gray, Color.white));
        buildingTypes.Add(new BuildingType("BuildingShop1End", Color.gray, Color.white));
        buildingTypes.Add(new BuildingType("BuildingWhite2", new Color(0.29f, 0.314f, 0.267f, 1f), Color.white));
        buildingTypes.Add(new BuildingType("BuildingWhite2Busy", new Color(0.29f, 0.314f, 0.267f, 1f), Color.white));
        buildingTypes.Add(new BuildingType("BuildingWhite2RoofStory1", Color.white, Color.gray));
        buildingTypes.Add(new BuildingType("BuildingWhite3", new Color(0.29f, 0.314f, 0.267f, 1f), Color.white));
        buildingTypes.Add(new BuildingType("BuildingWhite3RoofStory1", new Color(0.29f, 0.314f, 0.267f, 1f), Color.white));
        buildingTypes.Add(new BuildingType("BuildingWhite3Busy", new Color(0.29f,0.314f,0.267f,1f), Color.white));
        buildingTypes.Add(new BuildingType("BuildingWhite4", Color.green, Color.white));
        buildingTypes.Add(new BuildingType("BuildingHouse", Color.gray, Color.white));
        buildingTypes.Add(new BuildingType("BuildingVilla", Color.red, Color.white));

    }

    private void setWts()
    {
        wallTypes.Add(new WallType("SandbagWall1", Color.gray, 0.6f, 1.325f));
        wallTypes.Add(new WallType("TrenchWall1", Color.gray, 0.5f, 1.35f));
        wallTypes.Add(new WallType("StoneWall1", Color.black, 0.6f, 1.3f));
        wallTypes.Add(new WallType("StoneWallCastle1", Color.black, 0.6f, 1.3f));
        wallTypes.Add(new WallType("PoolWall", Color.blue, 0.6f, 1.1f));
        wallTypes.Add(new WallType("BrickWall1", Color.gray, 0.6f, 2.5f));
        wallTypes.Add(new WallType("GardenWall1", Color.green, 0.7f, 1.2f));
        wallTypes.Add(new WallType("DummyWall1", Color.gray, 0.4f, 0.0f));
        wallTypes.Add(new WallType("CliffWall1", Color.gray, 0.4f, 1.3f));
        wallTypes.Add(new WallType("CliffWall2", Color.green, 0.4f, 1.3f));
        wallTypes.Add(new WallType("FarmFence1", Color.gray, -1.0f, 1.2f));
        wallTypes.Add(new WallType("FarmFence2", Color.gray, -1.0f, 1.2f));
        wallTypes.Add(new WallType("SecurityFence1", Color.gray, -1.0f, 2.5f));
        wallTypes.Add(new WallType("PlatformFence1", Color.black, -1.0f, 1.2f));
        wallTypes.Add(new WallType("PlatformFence2", Color.gray, -1.0f, 1.2f));
        //wallTypes.Add(new WallType("FarmFence2", Color.gray, -1.0f, 1.2f)); // ÖØ¸´Ïî
        wallTypes.Add(new WallType("InvisibleWall1", Color.cyan, 1.2f, 2.5f));
        wallTypes.Add(new WallType("ChurchWall1", Color.gray, 0.2f, 2.5f));
        wallTypes.Add(new WallType("RuinWall1", Color.gray, 0.8f, 3.0f));
    }

    private void setPsts()
    {
        PST.Add(new PlatformSerfaceType("terrain", Color.grey * 0.6f));
        PST.Add(new PlatformSerfaceType("pavement", Color.yellow * 0.6f));
        PST.Add(new PlatformSerfaceType("wood", Color.yellow * 0.8f+Color.red*0.2f));
        PST.Add(new PlatformSerfaceType("grass", Color.green * 0.6f));
        PST.Add(new PlatformSerfaceType("none", Color.cyan * 0.6f));

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
public class Layer
{
    public List<MapItem> mapItems;
    public Layer()
    {
        mapItems = new List<MapItem>();
    }

    public void sortByIndex()
    {
        mapItems = mapItems.OrderBy(item => item.layerIndex).ToList();
    }
}
public class MapItem:MonoBehaviour
{
    public string id;
    public int layerIndex;
    public string material;
    //can pick by selector
    public MapItem()
    {
        id = string.Empty;
        layerIndex = 0;
    }

    public virtual void scatterThis()
    {
        Debug.Log("MetaMap:WrongScatte");
    }

    public virtual string getInfoText()
    {
        return this.GetType().Name+"\n" + "empty info";
    }


}
public class MeRect :MapItem//this class won,t use directly
{
    public Vector2 position;
    public float rotation;//angle
    public Vector2 size;//width x and height y

    public MeRect(Vector2 pos,float r,Vector2 s,string key,int lI)
    {
        position = pos;
        rotation = r;
        size = s;
        id = key;
        layerIndex = lI;
    }
}
public class PathPair:MapItem
{
    public List<Vector2> positinLineL;
    public List<Vector2> positinLineR;
}
public class MePath:MapItem
{
    public List<Vector2> positionLine;
}

public class mapItemType
{
    public string name="";
}
public class BuildingType:mapItemType
{
    public Material materialTop;
    public Material materialSide;

    public BuildingType(string n,Color c,Color c1)
    {
        materialTop = new Material(Shader.Find("Standard"));
        materialTop.color = c;
        materialSide = new Material(Shader.Find("Standard"));
        materialSide.color = c1;
        name = n;
    }
}
public class WallType:mapItemType
{
    public Material material;
    public float depth;
    public float height;

    public WallType(string name,Color c, float depth, float height)
    {
        this.name = name;
        material = new Material(Shader.Find("Standard"));
        material.color = c;
        this.depth = depth;
        this.height = height;
    }
}

public class PlatformSerfaceType
{
    public Material material;
    public string name;
    public PlatformSerfaceType(string name, Color c)
    {
        this.name = name;
        material = new Material(Shader.Find("Standard"));
        material.color = c;
    }
}