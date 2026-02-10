
using UnityEngine;

public class Crate : MeRect
{

    public Crate(Vector2 pos, float r, Vector2 s, string key, int lI) : base(pos, r, s, key, lI)
    {
    }

    void Start()
    {
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
        }

    }

    public override void scale(float scaler)
    {
        //disable
    }
    public override string getInfoText()
    {
        string ou="Crate\n";
        ou += ("id:" + id)+"\n";
        return ou; 
    }



    void Update()
    {
        
    }
}
