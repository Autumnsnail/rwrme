using System;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;

public class Decal : MeRect
{
    // Start is called before the first frame update

    public string template_ref;

    public override float Rank => 0.3f;

    void Start()
    {
        
    }

    public Decal(Vector2 pos, float r, Vector2 s, string key, int lI) : base(pos, r, s, key, lI)
    {

    }

    public override string getInfoText()
    {
        return "Decal\n" + "id = " + id + "\n" + "layer = " + layerIndex.ToString() + "\n" + "template = " + template_ref + "\n";
    }
    // Update is called once per frame
    void Update()
    {
        
    }

    public override void scatterThis()
    {
        GameObject go = this.gameObject;
        if (go != null)
        {
            Vector3 troPos = new Vector3(0, 0, 0);
            VpMetaToucher.getXYHeightWithLayer(MathOfRwrme.SvgPosToU3dPos(position), layerIndex, ref troPos,Rank);
            go.transform.localPosition = troPos;
            go.transform.localScale = Vector3.one;
            go.transform.rotation = Quaternion.Euler(0f, -1 * rotation, 0f);
            DecalTemplate foundTemplate = MetaMap.instance.DecalTemplates.FirstOrDefault(template => template.name == template_ref);
            if(foundTemplate==null)return;
            size = foundTemplate.size;
            go.transform.localScale = new Vector3(foundTemplate.size.x/2,1,foundTemplate.size.y/2);
            go.transform.GetChild(0).gameObject.GetComponent<Renderer>().material.color = new Color(foundTemplate.color.r, foundTemplate.color.g, foundTemplate.color.b,0.5f);
        }
    }

}
