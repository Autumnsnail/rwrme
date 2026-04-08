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
            Vector3 start=Vector3.zero;int ind = 0;
            VpMetaToucher.getXYHeight(start2, ref start, ref ind);
            Vector2 end2 = MathOfRwrme.SvgPosToU3dPos(positionLine[i+1]);
            Vector3 end = Vector3.zero;
            VpMetaToucher.getXYHeight(end2, ref end, ref ind);

            float segmentLength = Vector3.Distance(start, end);
            Vector3 direction = (end - start).normalized;

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

            Vector3 horizontalProjection = new Vector3(direction.x, 0, direction.z).normalized;
            float yawAngle = Mathf.Atan2(horizontalProjection.z, horizontalProjection.x) * Mathf.Rad2Deg;
            wall.transform.localRotation = Quaternion.AngleAxis(-yawAngle, Vector3.up);
            Vector3 currentRight = wall.transform.right; // 应用Y轴旋转后的X轴
            float rollAngle = Vector3.SignedAngle(currentRight, direction, wall.transform.forward);
            wall.transform.localRotation *= Quaternion.AngleAxis(rollAngle, Vector3.forward);



            //wall.transform.localRotation = Quaternion.FromToRotation(Vector3.right, direction);
            //wall.transform.localRotation = Quaternion.LookRotation(Vector3.Cross(direction, Vector3.up) , Vector3.forward);
            wall.transform.GetChild(0).gameObject.GetComponent<Renderer>().material = mtl;
        }
    }

}
