
using UnityEngine;
using UnityEngine.UI;

public class Vehicle : MeRect
{
    public Vehicle(Vector2 pos, float r, Vector2 s, string key, int lI) : base(pos, r, s, key, lI)
    {
    }

    public bool taged=false;
    public string key = string.Empty;//if(taged) this is tag instead of key

    public override string IdPrefix { get { return "spawn_vehicle"; } }
    public override MapItem Duplicate()
    {
        Vehicle c = Instantiate(MapImporter.instate.VehiclePref).GetComponent<Vehicle>();
        CopyMeRectFieldsTo(c);
        c.taged = taged;
        c.key = key;
        return c;
    }

    public GameObject toggle;
    public GameObject nameText; 
    
    /*
    public override void rotate(float scaler)
    {
        float len = Mathf.Sqrt(Mathf.Pow(6.6684999f / 2, 2) + Mathf.Pow(5.7888808f / 2, 2));
        Mathf.Sin((Mathf.PI/180) * scaler * -2f)
        rotation = rotation + scaler * -2;
    }
    */
    public void setTaged(bool chg)
    {
        taged = chg;
    }
    public void setKey(string chg)
    {
        key = chg;
    }
    public override string getInfoText()
    {
        string info = "";
        info += "Vehicle\n";
        info += "id:"+id+"\n";
        if (taged )
        {
            info +="tag = "+ key +"\n";
        }
        else
        {
            info += "key = " + key + "\n";
        }
        return info;
    }
    public override void scatterThis()
    {
        //nameText.GetComponent<TMP_InputField>().SetTextWithoutNotify(key);
        toggle.GetComponent<Toggle>().SetIsOnWithoutNotify(taged);
        GameObject go = this.gameObject;
        if (go != null)
        {
            Vector3 troPos = new Vector3(0, 0, 0);
            VpMetaToucher.getXYHeightWithLayer(MathOfRwrme.SvgPosToU3dPos(position), 4, ref troPos);
            go.transform.localPosition = troPos+Vector3.up;
            go.transform.GetChild(0).gameObject.GetComponent<Renderer>().material.color = MathOfRwrme.StringToColor(key);
            go.transform.rotation = Quaternion.Euler(0f, -1 * rotation, 0f);
        }
    }
}