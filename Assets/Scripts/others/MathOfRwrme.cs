using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public class MathOfRwrme
{
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
    
    public static Vector3 MetaPosToU3dPos(float x,float y)
    {
        return new Vector3(x/2.0f,0,(y/2.0f)+1024);
    }
    public static Vector3 MetaPosToU3dPos(Vector2 pos)
    {
        return new Vector3(pos.x / 2.0f, 0, (pos.y / 2.0f) + 1024);
    }
    public static Vector2 U3dPosToMetaPos(Vector3 worldPos)
    {
        return new Vector2(worldPos.x * 2, (worldPos.z - 1024) * 2);
    }

}

public class SvgTransform
{
    Matrix4x4 data;
    SvgTransform(float a,float b,float c,float d,float ox,float oy)
    {
    }
}
