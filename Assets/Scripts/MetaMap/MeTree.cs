
using UnityEngine;

public class MeTree : MeRect
{
    // you son of...
    // i mean that this only use in 
    // map7
    // but I need to make a whole workflow for this
    // pasik you!
    // ok i was wrong
    // they are every where
    // instead of only in map7
    // but in a ridiculous way
    // so pasik you!
    public override float Rank => 0.2f;
    public MeTree(Vector2 pos, float r, Vector2 s, string key, int lI) : base(pos, r, s, key, lI)
    {
    }

    public override string getInfoText()
    {
        string info = "";
        info += "Tree\n";
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
            go.transform.rotation = Quaternion.Euler(0f, -1 * rotation, 0f);
        }
    }
}