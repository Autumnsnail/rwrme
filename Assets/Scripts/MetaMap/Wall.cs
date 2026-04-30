using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class Wall : MePath
{
    public GameObject SubWallPref;


    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {

    }
    public override string getInfoText()
    {
        string info = "";
        info += "wall\n";
        info += "id = "+id +"\n";
        info += "layer = " + layerIndex.ToString() + "\n";
        info += "template = " + material;
        return info;
    }
    public override void grab(Vector2 offset)
    {
        for (int i = 0; i < positionLine.Count; i++)
            positionLine[i] += offset;
    }

    public override void rotate(float scaler)
    {
        if (positionLine.Count == 0) return;
        Vector2 center = Vector2.zero;
        foreach (var p in positionLine) center += p;
        center /= positionLine.Count;

        float rad = scaler * -2f * Mathf.Deg2Rad;
        float cos = Mathf.Cos(rad);
        float sin = Mathf.Sin(rad);
        for (int i = 0; i < positionLine.Count; i++)
        {
            Vector2 d = positionLine[i] - center;
            positionLine[i] = center + new Vector2(d.x * cos - d.y * sin, d.x * sin + d.y * cos);
        }
    }

    public override void scatterThis()
    {
        GameObject go = this.gameObject;
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Destroy(transform.GetChild(i).gameObject);
        }
        for (int i = 0;i<positionLine.Count-1;i++)
        {
            Vector2 start2 = MathOfRwrme.SvgPosToU3dPos(positionLine[i]);
            Vector3 start=Vector3.zero;
            VpMetaToucher.getXYHeightWithLayer(start2, layerIndex,ref start,Rank);
            Vector2 end2 = MathOfRwrme.SvgPosToU3dPos(positionLine[i+1]);
            Vector3 end = Vector3.zero;
            VpMetaToucher.getXYHeightWithLayer(end2, layerIndex,ref end,Rank);

            // 墙立在 XZ 平面上沿水平延伸：长度必须用 Δx、Δz，不能把 Vector3 隐式转成 Vector2（会丢掉 Z，只剩 x 与高度 y）
            Vector3 delta = end - start;
            float segmentLength = new Vector2(delta.x, delta.z).magnitude;

            Vector3 direction = delta.normalized;

            float dep = 1;
            float hei = 1;
            Material mtl = new Material(Shader.Find("Standard"));
            WallType wt = MetaMap.instance.wallTypes.FirstOrDefault(type => type.name.Equals(material));
            if (wt != null)
            {
                dep = wt.depth;
                if(wt.depth == -1f)
                {
                    dep = 0.1f;
                }
                hei = wt.height;
                mtl = wt.material;
            }


            GameObject wall = Instantiate(SubWallPref, this.transform);
            wall.transform.localPosition = start;
            wall.transform.localScale = new Vector3(segmentLength, hei, dep);

            // 仅绕本地 Y 轴对齐水平朝向（XZ），不随线段俯仰做 roll
            Vector3 flat = new Vector3(direction.x, 0f, direction.z);
            if (flat.sqrMagnitude < 1e-8f)
                wall.transform.localRotation = Quaternion.identity;
            else
            {
                flat.Normalize();
                float yawAngle = Mathf.Atan2(flat.z, flat.x) * Mathf.Rad2Deg;
                wall.transform.localRotation = Quaternion.AngleAxis(-yawAngle, Vector3.up);
            }



            //wall.transform.localRotation = Quaternion.FromToRotation(Vector3.right, direction);
            //wall.transform.localRotation = Quaternion.LookRotation(Vector3.Cross(direction, Vector3.up) , Vector3.forward);
            wall.transform.GetChild(0).gameObject.GetComponent<Renderer>().material = mtl;
        }
    }

}
