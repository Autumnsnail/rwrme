using System.Collections;
using System.Collections.Generic;
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
                go.transform.localScale = foundTemplate.extend;
                go.transform.GetChild(0).gameObject.GetComponent<Renderer>().material.color = foundTemplate.color;
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
