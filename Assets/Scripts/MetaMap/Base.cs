using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class Base : MeRect
{
    public int factionIndex = -1;
    public string _name = "";
    public Base(Vector2 pos, float r, Vector2 s, string key, int lI) : base(pos, r, s, key, lI)
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
            go.transform.localScale = new Vector3(size.x / 2, 50, size.y / 2);
            Vector3 troPos = new Vector3(MathOfRwrme.SvgPosToU3dPos(position).x,0, MathOfRwrme.SvgPosToU3dPos(position).y);
            go.transform.localPosition = troPos;
            go.transform.rotation = Quaternion.Euler(0f, -1 * rotation, 0f);
        }
    }

    public override string getInfoText()
    {
        string ou ="name = "+ _name + "\n";
        ou = ou +"id = "+id+"\n";
        if (factionIndex != -1) { ou = ou + "faction_index = " + factionIndex + "\n"; }
        return ou; 
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
