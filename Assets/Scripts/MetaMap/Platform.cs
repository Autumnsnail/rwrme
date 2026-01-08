using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public class Platform : PathPair
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



    public string base_wall_template;
    public string top_material;
    public float wall_height;
    public bool isBridge=false;

    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public override void scatterThis()
    {   
        Vector3 pot = new Vector3();
        VpMetaToucher.getXYHeightWithLayer(MathOfRwrme.SvgPosToU3dPos(positinLineR[0]) , this.layerIndex,ref pot);
        List<Vector2> u3dpll = positinLineL.Select(pos => MathOfRwrme.SvgPosToU3dPos(pos)).ToList();
        List<Vector2> u3dplr = positinLineR.Select(pos => MathOfRwrme.SvgPosToU3dPos(pos)).ToList();
        GeneratePathGeometry(u3dpll, u3dplr, pot.y+this.wall_height);
    }


    void GeneratePathGeometry(List<Vector2> leftPath, List<Vector2> rightPath,float pathHeight)
    {
        List<Vector3> vertices = new List<Vector3>();

        // 生成地板/平台
        for (int i = 0; i < leftPath.Count; i++)
        {
            Vector3 p1 = new Vector3(leftPath[i].x, pathHeight, leftPath[i].y);
            Vector3 p2 = new Vector3(rightPath[i].x, pathHeight, rightPath[i].y);
            vertices.Add(p1);
            vertices.Add(p2);
        }
        GenerateQuadFloor(vertices);
    }

    void GenerateQuadFloor(List<Vector3> vps)
    {

        // 创建网格
        Mesh mesh = new Mesh();

        List<int> triangles = new List<int>();
        for (int i = 0; i < vps.Count/2-1; i++)
        {
            int baseIndex = i * 2;

            // 第一个三角形（左下、、右下左上）
            triangles.Add(baseIndex);         // column1[i]
            triangles.Add(baseIndex + 1);     // column2[i]
            triangles.Add(baseIndex + 2);     // column1[i+1]

            // 第二个三角形（左上、、右下右上）
            triangles.Add(baseIndex + 1);     // column2[i]
            triangles.Add(baseIndex + 3);     // column2[i+1]
            triangles.Add(baseIndex + 2);     // column1[i+1]
        }

        mesh.vertices = vps.ToArray();
        mesh.triangles = triangles.ToArray();
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        GetComponent<MeshFilter>().mesh = mesh;
        GetComponent<MeshCollider>().sharedMesh = mesh;

    }

}
