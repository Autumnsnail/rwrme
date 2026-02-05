
using UnityEngine;

public class Rock : MeRect
{
    public Rock(Vector2 pos, float r, Vector2 s, string key, int lI) : base(pos, r, s, key, lI)
    {
    }

    public override string getInfoText()
    {
        string info = "";
        info += "Rock\n";
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
            go.transform.localScale = Vector3.one;
        }
    }
}