using UnityEngine;
using System.Linq;

public class Building : MeRect
{
    public int height;
    public bool roof=false;

    public override float Rank => 0.1f;

    public override string getInfoText()
    {
        string info = "";
        info += "building\n";
        info += "id = " + id + "\n";
        info += "layer = " + layerIndex.ToString() + "\n";
        info += "roof = " + roof.ToString() + "\n";
        info += "template = " + material + "\n";
        info += "height = " + height+" ("+height*3.0f+")" ;

        return info;
    }
    
    public Building(int h, string m, Vector2 pos, float r, Vector2 s, string k, int layerI) : base(pos, r, s, k, layerI)
    {
        //Debug.Log($"Building����: height={h}, material={m}, position={pos}, rotation={r}, scale={s}, key={k}");
        height = h;
        material = m;
    }
    public void scaleAxis(float scaler, int axis)
    {
        if (axis == 0)
            size = new Vector2(size.x + scaler, size.y);
        else
            size = new Vector2(size.x, size.y + scaler);
    }

    public void reinit(int h, string m, Vector2 pos, float r, Vector2 s, string k, int layerI)
    {
        height = h;
        material = m;
        position = pos;
        rotation = r;
        size = s;
        id = k;
        layerIndex = layerI;
    }
    public override void scatterThis()
    {
        //Debug.Log("setBuilding as ");
        //GameObject newInstance = Instantiate(buildingPrefeb);
        //newInstance.transform.localScale = new Vector3(bld.size.x, bld.height * 3.0f, bld.size.y);
        //newInstance.transform.position = new Vector3(bld.position.x, m_terrain.SampleHeight(new Vector3(bld.position.x, 0, bld.position.y)), bld.position.y);
        //newInstance.transform.rotation = Quaternion.Euler(0f, -1 * bld.rotation, 0f);
        //newInstance.GetComponent<ObjectContainer>().pointerToMapItem = bld;
        GameObject go = this.gameObject;
        if (go != null)
        {
            go.transform.localScale = new Vector3(size.x/2, height * 1.5f, size.y/2);
            //go.transform.localPosition = new Vector3(bld.position.x, m_terrain.SampleHeight(new Vector3(bld.position.x, 0, bld.position.y)), bld.position.y);
            Vector3 troPos=new Vector3(0,0,0);
            VpMetaToucher.getXYHeightWithLayer(MathOfRwrme.SvgPosToU3dPos(position), layerIndex,ref troPos,Rank);
            go.transform.localPosition = troPos;
            go.transform.rotation = Quaternion.Euler(0f, -1 * rotation, 0f);
            GameObject rf = go.transform.GetChild(1).gameObject;
            if(rf != null)
            {
                if (roof) rf.transform.localScale = Vector3.one;
                if (!roof) rf.transform.localScale = Vector3.zero;
            }
            BuildingType bt = MetaMap.instance.buildingTypes.FirstOrDefault(type => type.name.Equals(material));
            if(bt!= null)
            {
                if (rf != null)
                {
                    GameObject rfv = rf.transform.GetChild(0).gameObject;
                    Renderer renderer = rfv.GetComponent<Renderer>();
                    if(renderer != null)
                    {
                        renderer.material = bt.materialTop;
                    }   
                }
                GameObject body = go.transform.GetChild(0).gameObject;
                Renderer bdr = body.GetComponent<Renderer>();
                if (bdr != null)
                {
                    bdr.material = bt.materialSide;
                }

            }
        }
    }
}