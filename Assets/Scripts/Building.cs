using System.Collections;
using System.Collections.Generic;
using UnityEditor.UI;
using UnityEngine;

public class Building : MeRect
{
    public int height;
    public string material;
    public Building(int h, string m, Vector2 pos, float r, Vector2 s, string k, int layerI) : base(pos, r, s, k, layerI)
    {
        //Debug.Log($"Buildingππ‘Ï: height={h}, material={m}, position={pos}, rotation={r}, scale={s}, key={k}");
        height = h;
        material = m;
    }
    public void reinit(int h, string m, Vector2 pos, float r, Vector2 s, string k, int layerI)
    {
        height = h;
        material = m;
        position = pos;
        rotation = r;
        size = s;
        material = k;
        layerIndex = layerI;
    }
    public override void scatterThis()
    {
        //Debug.Log("setBuilding as ");
        //GameObject newInstance = Instantiate(buildingPrefeb);
        //newInstance.transform.localScale = new Vector3(bld.size.x, bld.height * 3.0f, bld.size.y);
        //newInstance.transform.position = new Vector3(bld.position.x, m_terrain.SampleHeight(new Vector3(bld.position.x, 0, bld.position.y)), bld.position.y);
        //newInstance.transform.rotation = Quaternion.Euler(0f, -1 * bld.rotation, 0f);
        //newInstance.GetComponent<ObjectContainer>().pointerToMapItem = bld;
        GameObject go = this.gameObject;
        if (go != null)
        {
            go.transform.localScale = new Vector3(size.x, height * 3.0f, size.y);
            //go.transform.localPosition = new Vector3(bld.position.x, m_terrain.SampleHeight(new Vector3(bld.position.x, 0, bld.position.y)), bld.position.y);
            Vector3 troPos=new Vector3(0,0,0);
            VpMetaToucher.getXYHeightWithLayer(position, layerIndex,ref troPos);
            go.transform.localPosition = troPos;
            go.transform.rotation = Quaternion.Euler(0f, -1 * rotation, 0f);
        }
    }
}