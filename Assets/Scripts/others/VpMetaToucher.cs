using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting.FullSerializer;
using UnityEngine;

public class VpMetaToucher
{

    public static bool getXYHeight(Vector2 xyp, ref Vector3 worldPosition, ref int layerIndex)
    {
        layerIndex = 0;

        // 将2D坐标转换为3D坐标
        Vector3 rayStart = new Vector3(xyp.x, 100f, xyp.y);

        // 只检测 PinAble 层
        int pinAbleLayer = 6;//PinAble

        int layerMask = 1 << pinAbleLayer;

        RaycastHit hit;
        if (Physics.Raycast(rayStart, Vector3.down, out hit, 101, layerMask))
        {
            worldPosition = hit.point;
            MapItem oc = hit.collider.gameObject.GetComponent<MapItem>();
            if (oc != null)
            {
                layerIndex = oc.layerIndex;
            }
            return true;
        }

        // 没有找到可踩踏的物体
        worldPosition = new Vector3(xyp.x, 0, xyp.y);
        return false;
    }

    public static bool getXYHeightWithLayer(Vector2 xyp, int objectLayer, ref Vector3 placementPosition)
    {
        // 从高处向下发射射线
        Vector3 rayStart = new Vector3(xyp.x, 100f, xyp.y);

        // 获取所有可踩踏层的碰撞
        RaycastHit[] hits = Physics.RaycastAll(rayStart, Vector3.down, Mathf.Infinity);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        //string result = string.Join(" | ", hits.Select((hit, index) => $"#{index}:{hit.collider.name}({hit.distance:F1})")); Debug.Log("碰撞结果: " + result);
        // 从最高点开始检查，找到第一个层级低于物体的可踩踏表面
        for (int i=0;i<hits.Length;i++)
        //for (int i = hits.Length-1; i>=0; i--)
        {
            RaycastHit hit = hits[i];
            int hitLayer = hit.collider.gameObject.layer;
            MapItem mi = hit.collider.gameObject.transform.root.GetComponent<MapItem>();
            //Debug.Log(hit.collider.gameObject.name);
            // 检查是否是 PinAble 层
            if (hitLayer == 6)
            {
                //Debug.Log(hit.collider.gameObject.name);
                if(mi != null)
                {
                    //Debug.Log("fundAPlacenot ground");
                    if(mi.layerIndex  < objectLayer)
                    {
                        placementPosition = hit.point;
                        return true;
                    }
                }
                else
                {
                    //MayBeGround
                    //or Water
                    //but water is no good
                    //no water now
                    placementPosition = hit.point;
                    return true;

                }
            }
        }

        Debug.Log("VpMetaToucher:Not PlaceAble");
        placementPosition = new Vector3(xyp.x, 0, xyp.y);
        return false;
    }

    public static MapItem GetMapItemUnderPosition(Vector2 xyp)
    {
        // 将2D坐标转换为3D坐标
        Vector3 rayStart = new Vector3(xyp.x, 100f, xyp.y);
        RaycastHit[] hits = Physics.RaycastAll(rayStart, Vector3.down, Mathf.Infinity);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        if (hits[0].collider.transform.root.GetComponent<MapItem>() != null)
        {
            return hits[0].collider.transform.root.GetComponent<MapItem>();
        }
        return null;
    }

}
