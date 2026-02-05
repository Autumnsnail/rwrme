using System;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public class Platform : PathPair
{
    // Start is called before the first frame update
    public List<Vector2> ParsePathData(string pathData)
    {
        List<Vector2> points = new List<Vector2>();

        string[] parts = pathData.Split(new char[] { ' ', ',', '\t', '\n' },
                                        StringSplitOptions.RemoveEmptyEntries);

        Vector2 currentPoint = Vector2.zero;

        int cmdType = 0;//0 for m,1 for M

        for(int i=0;i<parts.Length; i++)
        {
            string token = parts[i];
            if (char.IsLetter(token[0]))
            {
                switch (token[0])
                {
                    case 'm':
                        cmdType = 0;
                        break;
                    case 'M':
                        cmdType = 1;
                        break;
                }
            }
            else
            {
                if (i + 1 < parts.Length)
                {
                    float x = float.Parse(parts[i]);
                    float y = float.Parse(parts[++i]);
                    if(cmdType==0) currentPoint += new Vector2(x, y);
                    if (cmdType == 1) currentPoint = new Vector2(x, y);
                    points.Add(currentPoint);
                }
            }
            
        }
        /*
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
                    case 'M':
                        isRelative = false;
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
                                currentPoint = new Vector2(x, y);
                                points.Add(currentPoint);
                            }
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
        */
        return points;
    }

    public string base_wall_template;
    public string wall_template;
    public string top_material;
    public float wall_height = -1f;
    public bool isBridge = false;
    public bool isDeck = false;//upHeight
    public float height = 0.0f;

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
        info += "platform\n";
        info += "id = " + id + "\n";
        info += "layer = " + layerIndex.ToString() + "\n";
        info += "top_material = " + top_material + "\n";
        info += "base_wall_template = " + base_wall_template + "\n";
        info += "wall_template = " + wall_template + "\n";
        info += "wall_height = " + wall_height + "\n";
        if (isBridge) info += "bridge\n";
        if (isDeck) info += "deck\n";
        return info;
    }

    public override void scatterThis()
    {
        Vector3 pot = new Vector3();
        VpMetaToucher.getXYHeightWithLayer(MathOfRwrme.SvgPosToU3dPos(positinLineR[0]), this.layerIndex, ref pot);
        List<Vector2> u3dpll = positinLineL.Select(pos => MathOfRwrme.SvgPosToU3dPos(pos)).ToList();
        List<Vector2> u3dplr = positinLineR.Select(pos => MathOfRwrme.SvgPosToU3dPos(pos)).ToList();
        this.GetComponent<Renderer>().material.color = MathOfRwrme.StringToColor(top_material);
        WallType wtp = MetaMap.instance.wallTypes.FirstOrDefault(type => type.name.Equals(wall_template));
        float wallHeight = 1;
        if (wtp != null)
        {
            wallHeight = wtp.height;
            this.transform.GetChild(0).gameObject.GetComponent<Renderer>().material = wtp.material;
        }
        if (wall_height != -1)
        {
            wallHeight = wall_height;
        }

        WallType bwtp = MetaMap.instance.wallTypes.FirstOrDefault(type => type.name.Equals(base_wall_template));
        if (bwtp != null)
        {
            this.transform.GetChild(1).gameObject.GetComponent<Renderer>().material = bwtp.material;
        }

        GeneratePathGeometry(u3dpll, u3dplr, pot.y, wallHeight);

    }

    public void setSurface(string input)
    {
        top_material = input;
    }
    void GeneratePathGeometry(List<Vector2> leftPath, List<Vector2> rightPath, float pathHeight, float wallHei)
    {
        List<Vector3> vertices = new List<Vector3>();
        List<Vector3> sides = new List<Vector3>();
        List<Vector3> baseWall = new List<Vector3>();

        if (isDeck)
        {
            pathHeight += height;
        }

        for (int i = 0; i < leftPath.Count; i++)
        {
            Vector3 p1 = new Vector3(leftPath[i].x, pathHeight, leftPath[i].y);
            Vector3 p2 = new Vector3(rightPath[i].x, pathHeight, rightPath[i].y);
            vertices.Add(p1);
            vertices.Add(p2);
        }

        GenerateQuadFloor(vertices);


        if (!string.IsNullOrEmpty(wall_template))
        {
            if (isDeck)
            {
                for (int i = 0; i < leftPath.Count; i++)
                {
                    Vector3 p1 = new Vector3(leftPath[i].x, pathHeight + wallHei, leftPath[i].y);
                    Vector3 p2 = new Vector3(leftPath[i].x, pathHeight, leftPath[i].y);
                    sides.Add(p1);
                    sides.Add(p2);
                }
                for (int i = leftPath.Count - 1; i >= 0; i--)
                {
                    Vector3 p1 = new Vector3(rightPath[i].x, pathHeight + wallHei, rightPath[i].y);
                    Vector3 p2 = new Vector3(rightPath[i].x, pathHeight, rightPath[i].y);
                    sides.Add(p1);
                    sides.Add(p2);
                }
                sides.Add(new Vector3(leftPath[0].x, pathHeight + wallHei, leftPath[0].y));
                sides.Add(new Vector3(leftPath[0].x, pathHeight, leftPath[0].y));
            }
            else if (isBridge)
            {

                for (int i = 0; i < leftPath.Count; i++)
                {
                    Vector3 p1 = new Vector3(leftPath[i].x, pathHeight + wallHei, leftPath[i].y);
                    Vector3 p2 = new Vector3(leftPath[i].x, pathHeight, leftPath[i].y);
                    sides.Add(p1);
                    sides.Add(p2);
                }
                sides.Add(new Vector3(leftPath[leftPath.Count - 1].x, pathHeight + wallHei, leftPath[leftPath.Count - 1].y));
                sides.Add(new Vector3(leftPath[leftPath.Count - 1].x, pathHeight + wallHei, leftPath[leftPath.Count - 1].y));
                sides.Add(new Vector3(rightPath[0].x, pathHeight + wallHei, rightPath[0].y));
                sides.Add(new Vector3(rightPath[0].x, pathHeight + wallHei, rightPath[0].y));

                for (int i = 0; i < leftPath.Count; i++)
                {
                    Vector3 p1 = new Vector3(rightPath[i].x, pathHeight + wallHei, rightPath[i].y);
                    Vector3 p2 = new Vector3(rightPath[i].x, pathHeight, rightPath[i].y);
                    sides.Add(p1);
                    sides.Add(p2);
                }
            }
            else
            {
                sides.Add(new Vector3(rightPath[0].x, pathHeight + wallHei, rightPath[0].y));
                sides.Add(new Vector3(rightPath[0].x, pathHeight, rightPath[0].y));
                for (int i = 0; i < leftPath.Count; i++)
                {
                    Vector3 p1 = new Vector3(leftPath[i].x, pathHeight + wallHei, leftPath[i].y);
                    Vector3 p2 = new Vector3(leftPath[i].x, pathHeight, leftPath[i].y);
                    sides.Add(p1);
                    sides.Add(p2);
                }
                sides.Add(new Vector3(rightPath[rightPath.Count - 1].x, pathHeight + wallHei, rightPath[rightPath.Count - 1].y));
                sides.Add(new Vector3(rightPath[rightPath.Count - 1].x, pathHeight, rightPath[rightPath.Count - 1].y));
            }
            GenerateQuadSide(sides);
        }
        for (int i = 0; i < leftPath.Count; i++)
        {
            Vector3 p1 = new Vector3(leftPath[i].x, pathHeight, leftPath[i].y);
            Vector3 p2 = new Vector3(leftPath[i].x, 0, leftPath[i].y);
            baseWall.Add(p1);
            baseWall.Add(p2);
        }
        for (int i = leftPath.Count - 1; i >= 0; i--)
        {
            Vector3 p1 = new Vector3(rightPath[i].x, pathHeight, rightPath[i].y);
            Vector3 p2 = new Vector3(rightPath[i].x, 0, rightPath[i].y);
            baseWall.Add(p1);
            baseWall.Add(p2);
        }
        GenerateQuadBaseWall(baseWall);




    }

    void GenerateQuadFloor(List<Vector3> vps)
    {

        // 创建网格
        Mesh mesh = new Mesh();

        List<int> triangles = new List<int>();
        for (int i = 0; i < vps.Count / 2 - 1; i++)
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
    void GenerateQuadSide(List<Vector3> vps)
    {
        Mesh mesh = new Mesh();
        List<Vector3> allVertices = new List<Vector3>();
        allVertices.AddRange(vps);

        List<int> triangles = new List<int>();

        // 正面三角形
        for (int i = 0; i < vps.Count - 3; i = i + 2)
        {
            // 第一个三角形
            triangles.Add(i);         // 左下 i
            triangles.Add(i + 1);     // 左上 i
            triangles.Add(i + 2);     // 右下 i+1

            // 第二个三角形
            triangles.Add(i + 1);     // 左上 i
            triangles.Add(i + 3);     // 右上 i+1
            triangles.Add(i + 2);     // 右下 i+1
        }
        for (int i = vps.Count - 1; i >= 3; i = i - 2)
        {
            triangles.Add(i);         // 左下 i
            triangles.Add(i - 2);     // 左上 i
            triangles.Add(i - 1);     // 右下 i+1

            // 第二个三角形
            triangles.Add(i - 1);     // 左上 i
            triangles.Add(i - 2);     // 右上 i+1
            triangles.Add(i - 3);     // 右下 i+1
        }


        // 3. 设置网格数据
        mesh.vertices = allVertices.ToArray();
        mesh.triangles = triangles.ToArray();
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        // 4. 应用网格
        gameObject.transform.GetChild(0).gameObject.GetComponent<MeshFilter>().mesh = mesh;
        //gameObject.transform.GetChild(0).gameObject.GetComponent<MeshCollider>().sharedMesh = mesh;
    }

    void GenerateQuadBaseWall(List<Vector3> vps)
    {

        // 创建网格
        Mesh mesh = new Mesh();

        List<int> triangles = new List<int>();
        for (int i = 0; i < vps.Count / 2 - 1; i++)
        {
            int baseIndex = i * 2;

            triangles.Add(baseIndex + 2);
            triangles.Add(baseIndex + 1);
            triangles.Add(baseIndex);

            triangles.Add(baseIndex + 2);
            triangles.Add(baseIndex + 3);
            triangles.Add(baseIndex + 1);
        }

        mesh.vertices = vps.ToArray();
        mesh.triangles = triangles.ToArray();
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        gameObject.transform.GetChild(1).gameObject.GetComponent<MeshFilter>().mesh = mesh;
        //gameObject.transform.GetChild(1).gameObject.GetComponent<MeshCollider>().sharedMesh = mesh;

    }
}
