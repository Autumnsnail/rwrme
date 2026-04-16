
using UnityEngine;

public class VpMetaToucher
{

    public static bool getXYHeight(Vector2 xyp, ref Vector3 worldPosition, ref int layerIndex)
    {
        layerIndex = 0;

        // ��2D����ת��Ϊ3D����
        Vector3 rayStart = new Vector3(xyp.x, 100f, xyp.y);

        // ֻ��� PinAble ��
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

        // û���ҵ��ɲ�̤������
        worldPosition = new Vector3(xyp.x, 0, xyp.y);
        return false;
    }

    public static bool getXYHeightWithLayer(Vector2 xyp, int objectLayer, ref Vector3 placementPosition,int rank)
    {
        //this is not actually objectLayer,it has a float offset for different types of objects
        // �Ӹߴ����·�������
        Vector3 rayStart = new Vector3(xyp.x, 100f, xyp.y);

        // ��ȡ���пɲ�̤�����ײ
        RaycastHit[] hits = Physics.RaycastAll(rayStart, Vector3.down, Mathf.Infinity);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        //string result = string.Join(" | ", hits.Select((hit, index) => $"#{index}:{hit.collider.name}({hit.distance:F1})")); Debug.Log("��ײ���: " + result);
        // ����ߵ㿪ʼ��飬�ҵ���һ���㼶��������Ŀɲ�̤����
        for (int i=0;i<hits.Length;i++)
        //for (int i = hits.Length-1; i>=0; i--)
        {
            RaycastHit hit = hits[i];
            int hitLayer = hit.collider.gameObject.layer;
            MapItem mi = hit.collider.gameObject.transform.root.GetComponent<MapItem>();
            //Debug.Log(hit.collider.gameObject.name);
            // ����Ƿ��� PinAble ��
            if (hitLayer == 6)
            {
                //Debug.Log(hit.collider.gameObject.name);
                if(mi != null)
                {
                    //Debug.Log("fundAPlacenot ground");
                    //we put this here but is not actually correct
                    if(mi.layerIndex  < objectLayer)
                    {
                        placementPosition = hit.point;
                        return true;
                    }
                    else if(mi.layerIndex == objectLayer)
                    {
                        if(mi.Rank < rank)
                        {
                            placementPosition = hit.point;
                            return true;
                        }
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


}
