using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Net;
using UnityEngine;
using UnityEngine.UIElements;

public class Wall : MePath
{
    // Start is called before the first frame update
    public List<Vector2> ParsePathData(string pathData)
    {
        List<Vector2> points = new List<Vector2>();

        // 清理数据
        pathData = pathData.Trim().ToLower();

        // 分割命令和参数
        string[] parts = pathData.Split(new char[] { ' ', ',', '\t', '\n' },
                                        StringSplitOptions.RemoveEmptyEntries);

        Vector2 currentPoint = Vector2.zero;
        bool isRelative = false;

        for (int i = 0; i < parts.Length; i++)
        {
            string token = parts[i];

            // 检查是否是命令
            if (char.IsLetter(token[0]))
            {
                char command = token[0];

                switch (command)
                {
                    case 'm': // 移动命令（相对）
                        isRelative = true;
                        if (i + 2 < parts.Length)
                        {
                            float x = float.Parse(parts[++i]);
                            float y = float.Parse(parts[++i]);

                            if (points.Count == 0) // 第一个点
                            {
                                currentPoint = new Vector2(x, y);
                                points.Add(currentPoint);
                            }
                            else // 后续点
                            {
                                currentPoint += new Vector2(x, y);
                                points.Add(currentPoint);
                            }
                        }
                        break;

                    case 'l': // 直线命令（相对）
                        isRelative = true;
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

                    case 'c': // 贝塞尔曲线（这里简化为直线）
                    case 's':
                    case 'q':
                    case 't':
                    case 'a':
                        // 跳过曲线参数
                        //int paramCount = GetParameterCount(command);
                        //i += paramCount;
                        break;
                }
            }
            else // 是坐标值（隐式重复上一个命令）
            {
                // 假设是 l 命令的延续
                float x = float.Parse(token);
                if (i + 1 < parts.Length)
                {
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
            }
        }

        return points;
    }

    public GameObject SubWallPref;

    public string template;
    

    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
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
            Vector3 start = new Vector3(start2.x, 0, start2.y);
            Vector2 end2 = MathOfRwrme.SvgPosToU3dPos(positionLine[i+1]);
            Vector3 end = new Vector3(end2.x, 0, end2.y);
            float segmentLength = Vector3.Distance(start, end);
            Vector3 direction = (end - start).normalized;
            float angle = Mathf.Atan2(direction.z, direction.x) * Mathf.Rad2Deg;
            GameObject wall = Instantiate(SubWallPref, this.transform);
            wall.transform.localPosition = start;
            wall.transform.localRotation = Quaternion.Euler(0, -angle, 0);
            wall.transform.localScale = new Vector3(segmentLength, 1, 1);
        }
        Vector2 hp2 = MathOfRwrme.SvgPosToU3dPos(positionLine[0]);
        Vector3 Position = Vector3.zero;
        int ind = 0;
        VpMetaToucher.getXYHeight(hp2,ref Position, ref ind);
        gameObject.transform.localPosition = new Vector3(0, Position.y, 0);
    }


}
