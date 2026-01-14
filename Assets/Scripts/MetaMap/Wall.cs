using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Net;
using Unity.VisualScripting.Antlr3.Runtime;
using UnityEngine;
using UnityEngine.UIElements;
using System.Xml;           // .NET 标准 XML

public class Wall : MePath
{
    // Start is called before the first frame update
    public List<Vector2> ParsePathData(string pathData)
    {

        // 清理数据
        pathData = pathData.Trim().ToLower();

        // 分割命令和参数
        string[] parts = pathData.Split(new char[] { ' ', ',', '\t', '\n' },
                                        StringSplitOptions.RemoveEmptyEntries);

        //Debug.Log("Wall.cs " + pathData);

        bool isRelative = false;
        Vector2 currentPoint = Vector2.zero;
        List<Vector2> points = new List<Vector2>();

        for (int i = 0; i < parts.Length; i++)
        {
            string token = parts[i];

            // 检查是否是命令
            if (char.IsLetter(token[0]))
            {
                char command = token[0];
                isRelative = char.IsLower(command); // 小写是相对坐标
                command = char.ToLower(command); // 统一转换为小写处理

                switch (command)
                {
                    case 'm': // 移动命令
                        if (i + 2 < parts.Length)
                        {
                            float x = float.Parse(parts[++i]);
                            float y = float.Parse(parts[++i]);

                            if (isRelative)
                            {
                                currentPoint += new Vector2(x, y);
                            }
                            else
                            {
                                currentPoint = new Vector2(x, y);
                            }

                            // 如果是第一个点或者移动命令，添加到点列表
                            if (points.Count == 0)
                            {
                                points.Add(currentPoint);
                            }
                            // 注意：SVG规范中，移动命令后的第一个点实际上是隐式的直线命令
                        }
                        break;

                    case 'l': // 直线命令
                        if (i + 2 < parts.Length)
                        {
                            float x = float.Parse(parts[++i]);
                            float y = float.Parse(parts[++i]);

                            if (isRelative)
                            {
                                currentPoint += new Vector2(x, y);
                            }
                            else
                            {
                                currentPoint = new Vector2(x, y);
                            }
                            points.Add(currentPoint);
                        }
                        break;

                    case 'v': // 垂直直线命令（只改变Y坐标）
                        if (i + 1 < parts.Length)
                        {
                            float dy = float.Parse(parts[++i]);

                            if (isRelative)
                            {
                                currentPoint += new Vector2(0, dy);
                            }
                            else
                            {
                                currentPoint = new Vector2(currentPoint.x, dy);
                            }
                            points.Add(currentPoint);
                        }
                        break;

                    case 'h': // 水平直线命令（只改变X坐标）
                        if (i + 1 < parts.Length)
                        {
                            float dx = float.Parse(parts[++i]);

                            if (isRelative)
                            {
                                currentPoint += new Vector2(dx, 0);
                            }
                            else
                            {
                                currentPoint = new Vector2(dx, currentPoint.y);
                            }
                            points.Add(currentPoint);
                        }
                        break;

                    case 'c': // 贝塞尔曲线
                    case 's':
                    case 'q':
                    case 't':
                    case 'a':
                        // 跳过曲线参数
                        int paramCount = GetParameterCount(command);
                        i += paramCount;
                        break;

                        // 添加其他命令的处理...
                }
            }
            else // 隐式命令延续
            {
                // 需要知道当前最后一个命令是什么
                // 这里假设是直线命令的延续
                try
                {
                    float x = float.Parse(token);
                    float y = 0;

                    // 尝试获取Y坐标
                    if (i + 1 < parts.Length && !char.IsLetter(parts[i + 1][0]))
                    {
                        y = float.Parse(parts[++i]);

                        // 处理为相对坐标（这是最常见的情况）
                        currentPoint += new Vector2(x, y);
                        points.Add(currentPoint);
                    }
                }
                catch (FormatException)
                {
                    // 解析失败，跳过
                    continue;
                }
            }
        }

        return points;
    }
    private int GetParameterCount(char command)
    {
        switch (command)
        {
            case 'c': return 6; // 三次贝塞尔：x1 y1 x2 y2 x y
            case 's': return 4; // 平滑三次贝塞尔：x2 y2 x y
            case 'q': return 4; // 二次贝塞尔：x1 y1 x y
            case 't': return 2; // 平滑二次贝塞尔：x y
            case 'a': return 7; // 椭圆弧：rx ry x-axis-rotation large-arc-flag sweep-flag x y
            default: return 0;
        }
    }

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
