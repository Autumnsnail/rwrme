
using UnityEngine;

public class SpawnPoint : MeRect
{
    public SpawnPoint(Vector2 pos, float r, Vector2 s, string key, int lI) : base(pos, r, s, key, lI)
    {
    }

    public override string getInfoText()
    {
        string info = "";
        info += "SpawnPoint\n";
        info += "id = " + id + "\n";
        info += "layer = " + layerIndex.ToString() + "\n";

        return info;
    }
    public override void grab(Vector2 vector2)
    {
        position = position + vector2;
    }
    public override void scatterThis()
    {
        GameObject go = this.gameObject;
        if (go != null)
        {
            Vector3 troPos=new Vector3(0,0,0);
            VpMetaToucher.getXYHeightWithLayer(MathOfRwrme.SvgPosToU3dPos(position), 4,ref troPos);
            go.transform.localPosition = troPos+Vector3.up;
        }
    }
}