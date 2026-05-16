
using UnityEngine;

public class Ladder : MeRect
{
    public Ladder(Vector2 pos, float r, Vector2 s, string key, int lI) : base(pos, r, s, key, lI)
    {
    }

    public override string IdPrefix { get { return "#ladder"; } }
    public override MapItem Duplicate()
    {
        Ladder c = Instantiate(MapImporter.instate.LadderPref).GetComponent<Ladder>();
        CopyMeRectFieldsTo(c);
        return c;
    }

    public override string getInfoText()
    {
        string info = "";
        info += "Ladder\n";
        info += "id = " + id + "\n";

        return info;
    }
    public override void scatterThis()
    {
        GameObject go = this.gameObject;
        if (go != null)
        {
            Vector3 troPos=new Vector3(0,0,0);
            VpMetaToucher.getXYHeightWithLayer(MathOfRwrme.SvgPosToU3dPos(position), layerIndex,ref troPos);
            go.transform.localPosition = troPos;
            go.transform.localScale = new Vector3(1,2.5f,1);
            go.transform.rotation = Quaternion.Euler(0f, -1 * rotation, 0f);
        }
    }
}