
using System.Linq;
using UnityEngine;

public class MeMesh : MeRect
{
    public bool templated=false;
    public string template_ref;
    public MeMesh(Vector2 pos, float r, Vector2 s, string key, int lI) : base(pos, r, s, key, lI)
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
            go.transform.localScale = Vector3.one;
            go.transform.rotation = Quaternion.Euler(0f, -1 * rotation, 0f);
            if (templated)
            {
                MeshTemplate foundTemplate = MetaMap.instance.meshTemplates.FirstOrDefault(template => template.name == template_ref);
                go.transform.localScale = new Vector3 (size.x/2,foundTemplate.extend.y,size.y/2);
                go.transform.GetChild(0).gameObject.GetComponent<Renderer>().material.color = new Color(foundTemplate.color.r, foundTemplate.color.g, foundTemplate.color.b,0.5f);
                go.transform.GetChild(1).gameObject.GetComponent<Renderer>().material.color = foundTemplate.color;
                go.transform.GetChild(1).localScale = new Vector3 (foundTemplate.extend.x / go.transform.localScale.x, foundTemplate.extend.y / go.transform.localScale.y, foundTemplate.extend.z / go.transform.localScale.z);
            }
        }

    }

    public override void scale(float scaler)
    {
        //disable
    }
    public override string getInfoText()
    {
        string ou="Mesh\n";
        ou += ("id:" + id);
        if (templated)
        {
            ou+=("\ntemplate:"+template_ref);
        }
        return ou; 
    }

    void Update()
    {
        
    }
}
