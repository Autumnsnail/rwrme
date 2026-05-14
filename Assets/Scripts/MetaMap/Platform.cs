using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class Platform : PathPair
{
    public string base_wall_template;
    public string wall_template;
    public string top_material;
    public float wall_height = -1f;
    public bool isBridge = false;
    public bool isDeck = false;//upHeight
    public float height = 0.0f;
    
    public override float Rank => 0.0f;

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
        if (isBridge) info += "bridge\n";
        if (isDeck) info += "deck\n";
        info += "id = " + id + "\n";
        info += "layer = " + layerIndex.ToString() + "\n";
        info += "top_material = " + top_material + "\n";
        info += "base_wall_template = " + base_wall_template + "\n";
        info += "wall_template = " + wall_template + "\n";
        info += "wall_height = " + wall_height + "\n";
        return info;
    }

    public override void grab(Vector2 offset)
    {
        for (int i = 0; i < positinLineR.Count; i++) positinLineR[i] += offset;
        for (int i = 0; i < positinLineL.Count; i++) positinLineL[i] += offset;
    }

    public override string IdPrefix { get { return "platform"; } }
    public override MapItem Duplicate()
    {
        Platform c = Instantiate(MapImporter.instate.PlatformPref).GetComponent<Platform>();
        CopyPathPairFieldsTo(c);
        c.base_wall_template = base_wall_template;
        c.wall_template = wall_template;
        c.top_material = top_material;
        c.wall_height = wall_height;
        c.isBridge = isBridge;
        c.isDeck = isDeck;
        c.height = height;
        return c;
    }

    public override void rotate(float scaler)
    {
        Vector2 center = Vector2.zero;
        int total = positinLineR.Count + positinLineL.Count;
        if (total == 0) return;
        foreach (var p in positinLineR) center += p;
        foreach (var p in positinLineL) center += p;
        center /= total;

        float rad = scaler * -2f * Mathf.Deg2Rad;
        float cos = Mathf.Cos(rad);
        float sin = Mathf.Sin(rad);

        for (int i = 0; i < positinLineR.Count; i++)
        {
            Vector2 d = positinLineR[i] - center;
            positinLineR[i] = center + new Vector2(d.x * cos - d.y * sin, d.x * sin + d.y * cos);
        }
        for (int i = 0; i < positinLineL.Count; i++)
        {
            Vector2 d = positinLineL[i] - center;
            positinLineL[i] = center + new Vector2(d.x * cos - d.y * sin, d.x * sin + d.y * cos);
        }
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

        GeneratePathGeometry(u3dpll, u3dplr, wallHeight);
    }

    public void setSurface(string input)
    {
        top_material = input;
    }
    void GeneratePathGeometry(List<Vector2> leftPath, List<Vector2> rightPath,  float wallHei)
    {
        List<Vector3> vertices = new List<Vector3>();
        List<Vector3> sides = new List<Vector3>();
        List<Vector3> baseWall = new List<Vector3>();

        List<float> pathHeight = new List<float>();
        for (int i=0;i<rightPath.Count;i++)
        {
            Vector3 pot = Vector3.zero;
            VpMetaToucher.getXYHeightWithLayer(MathOfRwrme.SvgPosToU3dPos(positinLineR[i]), this.layerIndex, ref  pot);
            pathHeight.Add(pot.y);
        }
        if (isDeck)
        {
            for (int i = 0; i < rightPath.Count; i++) pathHeight[i] += height;
        }

        for (int i = 0; i < leftPath.Count; i++)
        {
            Vector3 p1 = new Vector3(leftPath[i].x, pathHeight[i], leftPath[i].y);
            Vector3 p2 = new Vector3(rightPath[i].x, pathHeight[i], rightPath[i].y);
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
                    Vector3 p1 = new Vector3(leftPath[i].x, pathHeight[i] + wallHei, leftPath[i].y);
                    Vector3 p2 = new Vector3(leftPath[i].x, pathHeight[i], leftPath[i].y);
                    sides.Add(p1);
                    sides.Add(p2);
                }
                for (int i = leftPath.Count - 1; i >= 0; i--)
                {
                    Vector3 p1 = new Vector3(rightPath[i].x, pathHeight[i] + wallHei, rightPath[i].y);
                    Vector3 p2 = new Vector3(rightPath[i].x, pathHeight[i], rightPath[i].y);
                    sides.Add(p1);
                    sides.Add(p2);
                }
                sides.Add(new Vector3(leftPath[0].x, pathHeight[0]   + wallHei, leftPath[0].y));
                sides.Add(new Vector3(leftPath[0].x, pathHeight[0], leftPath[0].y));
            }
            else if (isBridge)
            {
                sides.Add(new Vector3(leftPath[0].x, pathHeight[0] + wallHei, leftPath[0].y));
                sides.Add(new Vector3(leftPath[0].x, pathHeight[0], leftPath[0].y));
                sides.Add(new Vector3(rightPath[0].x, pathHeight[0] + wallHei, rightPath[0].y));
                sides.Add(new Vector3(rightPath[0].x, pathHeight[0], rightPath[0].y));
                /*for (int i = 0; i < leftPath.Count; i++)
                {
                    Vector3 p1 = new Vector3(leftPath[i].x, pathHeight[i] + wallHei, leftPath[i].y);
                    Vector3 p2 = new Vector3(leftPath[i].x, pathHeight[i], leftPath[i].y);
                    sides.Add(p1);
                    sides.Add(p2);
                }*/
                sides.Add(new Vector3(rightPath[0].x, pathHeight[0] + wallHei, rightPath[0].y));
                sides.Add(new Vector3(rightPath[0].x, pathHeight[0] + wallHei, rightPath[0].y));
                sides.Add(new Vector3(leftPath[leftPath.Count - 1].x, pathHeight[leftPath.Count - 1] + wallHei, leftPath[leftPath.Count - 1].y));
                sides.Add(new Vector3(leftPath[leftPath.Count - 1].x, pathHeight[leftPath.Count - 1] + wallHei, leftPath[leftPath.Count - 1].y));

                sides.Add(new Vector3(leftPath[leftPath.Count - 1].x, pathHeight[leftPath.Count - 1] + wallHei, leftPath[leftPath.Count - 1].y));
                sides.Add(new Vector3(leftPath[leftPath.Count - 1].x, pathHeight[leftPath.Count - 1], leftPath[leftPath.Count - 1].y));
                sides.Add(new Vector3(rightPath[leftPath.Count - 1].x, pathHeight[leftPath.Count - 1] + wallHei, rightPath[leftPath.Count - 1].y));
                sides.Add(new Vector3(rightPath[leftPath.Count - 1].x, pathHeight[leftPath.Count - 1], rightPath[leftPath.Count - 1].y));

                /*for (int i = 0; i < leftPath.Count; i++)
                {
                    Vector3 p1 = new Vector3(rightPath[i].x, pathHeight[i] + wallHei, rightPath[i].y);
                    Vector3 p2 = new Vector3(rightPath[i].x, pathHeight[i], rightPath[i].y);
                    sides.Add(p1);
                    sides.Add(p2);
                }*/
            }
            else
            {
                sides.Add(new Vector3(rightPath[0].x, pathHeight[0] + wallHei, rightPath[0].y));
                sides.Add(new Vector3(rightPath[0].x, pathHeight[0], rightPath[0].y));
                for (int i = 0; i < leftPath.Count; i++)
                {
                    Vector3 p1 = new Vector3(leftPath[i].x, pathHeight[i] + wallHei, leftPath[i].y);
                    Vector3 p2 = new Vector3(leftPath[i].x, pathHeight[i], leftPath[i].y);
                    sides.Add(p1);
                    sides.Add(p2);
                }
                sides.Add(new Vector3(rightPath[rightPath.Count - 1].x, pathHeight[rightPath.Count - 1] + wallHei, rightPath[rightPath.Count - 1].y));
                sides.Add(new Vector3(rightPath[rightPath.Count - 1].x, pathHeight[rightPath.Count - 1], rightPath[rightPath.Count - 1].y));
            }
            GenerateQuadSide(sides);
        }
        for (int i = 0; i < leftPath.Count; i++)
        {
            Vector3 p1 = new Vector3(leftPath[i].x, pathHeight[i], leftPath[i].y);
            Vector3 p2 = new Vector3(leftPath[i].x, 0, leftPath[i].y);
            baseWall.Add(p1);
            baseWall.Add(p2);
        }
        for (int i = leftPath.Count - 1; i >= 0; i--)
        {
            Vector3 p1 = new Vector3(rightPath[i].x, pathHeight[i], rightPath[i].y);
            Vector3 p2 = new Vector3(rightPath[i].x, 0, rightPath[i].y);
            baseWall.Add(p1);
            baseWall.Add(p2);
        }
        baseWall.Add(new Vector3(leftPath[0].x, pathHeight[0], leftPath[0].y));
        baseWall.Add(new Vector3(leftPath[0].x, 0, leftPath[0].y));
        GenerateQuadBaseWall(baseWall);




    }

    void GenerateQuadFloor(List<Vector3> vps)
    {

        // ��������
        Mesh mesh = new Mesh();

        List<int> triangles = new List<int>();
        for (int i = 0; i < vps.Count / 2 - 1; i++)
        {
            int baseIndex = i * 2;

            // ��һ�������Σ����¡����������ϣ�
            triangles.Add(baseIndex);         // column1[i]
            triangles.Add(baseIndex + 1);     // column2[i]
            triangles.Add(baseIndex + 2);     // column1[i+1]

            // �ڶ��������Σ����ϡ����������ϣ�
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

        // ����������
        for (int i = 0; i < vps.Count - 3; i = i + 2)
        {
            // ��һ��������
            triangles.Add(i);         // ���� i
            triangles.Add(i + 1);     // ���� i
            triangles.Add(i + 2);     // ���� i+1

            // �ڶ���������
            triangles.Add(i + 1);     // ���� i
            triangles.Add(i + 3);     // ���� i+1
            triangles.Add(i + 2);     // ���� i+1
        }
        for (int i = vps.Count - 1; i >= 3; i = i - 2)
        {
            triangles.Add(i);         // ���� i
            triangles.Add(i - 2);     // ���� i
            triangles.Add(i - 1);     // ���� i+1

            // �ڶ���������
            triangles.Add(i - 1);     // ���� i
            triangles.Add(i - 2);     // ���� i+1
            triangles.Add(i - 3);     // ���� i+1
        }


        // 3. ������������
        mesh.vertices = allVertices.ToArray();
        mesh.triangles = triangles.ToArray();
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        // 4. Ӧ������
        gameObject.transform.GetChild(0).gameObject.GetComponent<MeshFilter>().mesh = mesh;
        //gameObject.transform.GetChild(0).gameObject.GetComponent<MeshCollider>().sharedMesh = mesh;
    }

    void GenerateQuadBaseWall(List<Vector3> vps)
    {

        // ��������
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
