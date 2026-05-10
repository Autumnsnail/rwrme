
using UnityEngine;

public class ItemSupply : MeRect
{
    public int type = 0;// 1 for weapon_rack,0 for stash,

    public ItemSupply(Vector2 pos, float r, Vector2 s, string key, int lI) : base(pos, r, s, key, lI)
    {
    }

    public override string IdPrefix { get { return "item_supply"; } }
    public override MapItem Duplicate()
    {
        ItemSupply c = Instantiate(MapImporter.instate.ItemSupplyPref).GetComponent<ItemSupply>();
        CopyMeRectFieldsTo(c);
        c.type = type;
        return c;
    }

    void Start()
    {
    }

    public void changeType()
    {
        type = 1 - type;
    }

    public override void scatterThis()
    {
        GameObject go = this.gameObject;
        if (go != null)
        {
            Vector3 troPos = new Vector3(0, 0, 0);
            VpMetaToucher.getXYHeightWithLayer(MathOfRwrme.SvgPosToU3dPos(position), layerIndex, ref troPos);
            go.transform.localPosition = troPos;
            go.transform.localScale = new Vector3(size.x/2,1,size.y/2);
            go.transform.rotation = Quaternion.Euler(0f, -1 * rotation, 0f);
            go.transform.GetChild(0).gameObject.GetComponent<Renderer>().material.color = Color.red * type + Color.green * (1 - type);
        }

    }

    public override void scale(float scaler)
    {
        //disable
    }
    public override string getInfoText()
    {
        string ou="Item_Supply\n";
        ou += ("id:" + id)+"\n";
        if(type == 1)
        {
            ou += ("type = weapon_rack\n");
        }
        if(type == 0)
        {
            ou += ("type = stash\n");
        }
        return ou; 
    }



    void Update()
    {
        
    }
}
