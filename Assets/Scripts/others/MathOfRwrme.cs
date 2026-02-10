using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;


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
    
    //svg pos save in metadata
    //u3d pos is where it locate in unity ipos
    //game pos is not used
    //svg pos/2 = game pos = u3d post reverse y
    public static Vector2 SvgPosToU3dPos(Vector2 svgV2)
    {
        Vector2 u3dV2 = new Vector2(2048.0f, 2048.0f);
        u3dV2.x = svgV2.x;
        u3dV2.y = u3dV2.y - svgV2.y;
        u3dV2 = u3dV2 / 2;
        return u3dV2;
    }
    public static Color StringToColor(string str)
    {
        uint hash = 0;
        foreach (char c in str) hash = hash * 31 + c;

        float r = (hash & 0xFF) / 255f;
        float g = ((hash >> 8) & 0xFF) / 255f;
        float b = ((hash >> 16) & 0xFF) / 255f;

        return new Color(r * 0.7f + 0.3f, g * 0.7f + 0.3f, b * 0.7f + 0.3f);
    }
    public static Vector2 U3dPosToSvgPos(Vector2 u3dV2)
    {
        u3dV2 *= 2;
        u3dV2.y = 2048 - u3dV2.y;
        return u3dV2;
    }

    public static Vector2 U3dPosToSvgPos(Vector3 u3dV3)
    {
        return U3dPosToSvgPos(new Vector2(u3dV3.x, u3dV3.z));
    }

    public static string angleToTransform(float angle,Vector2 posi)
    {
        float angleRad = -angle * Mathf.Deg2Rad;
        float cos = Mathf.Cos(angleRad);
        float sin = Mathf.Sin(angleRad);
        float a = cos;
        float b = sin;
        float c = -sin;
        float d = cos;
        return $"matrix({a},{b},{c},{d},{posi.x},{posi.y})";
    }


}

public class SvgTransform
{
    Matrix4x4 data;
    SvgTransform(float a,float b,float c,float d,float ox,float oy)
    {
    }
}

public class Matrix2x2
{
    public float m00, m01;
    public float m10, m11;

    public Matrix2x2(float m00, float m01, float m10, float m11)
    {
        this.m00 = m00;
        this.m01 = m01;
        this.m10 = m10;
        this.m11 = m11;
    }

    /// <summary>
    /// 矩阵与向量乘法
    /// </summary>
    public Vector2 Multiply(Vector2 vector)
    {
        return new Vector2(
            m00 * vector.x + m01 * vector.y,
            m10 * vector.x + m11 * vector.y
        );
    }

    /// <summary>
    /// 重载 * 操作符
    /// </summary>
    public static Vector2 operator *(Matrix2x2 matrix, Vector2 vector)
    {
        return matrix.Multiply(vector);
    }

    /// <summary>
    /// 创建旋转矩阵
    /// </summary>
    /// <param name="angleDegrees">旋转角度（度）</param>
    public static Matrix2x2 CreateRotation(float angleDegrees)
    {
        float radians = angleDegrees * Mathf.Deg2Rad;
        float cos = Mathf.Cos(radians);
        float sin = Mathf.Sin(radians);

        return new Matrix2x2(
            cos, -sin,
            sin, cos
        );
    }

    /// <summary>
    /// 创建缩放矩阵
    /// </summary>
    public static Matrix2x2 CreateScale(Vector2 scale)
    {
        return new Matrix2x2(
            scale.x, 0,
            0, scale.y
        );
    }

    /// <summary>
    /// 从矩阵字符串创建（兼容你之前的格式）
    /// </summary>
    public static Matrix2x2 FromMatrixString(string matrixString)
    {
        string cleanString = matrixString.Replace("matrix(", "").Replace(")", "");
        string[] parts = cleanString.Split(',');

        if (parts.Length >= 4)
        {
            return new Matrix2x2(
                float.Parse(parts[0]), // m00
                float.Parse(parts[1]), // m01
                float.Parse(parts[2]), // m10
                float.Parse(parts[3])  // m11
            );
        }

        throw new System.ArgumentException("无效的矩阵字符串格式");
    }

    public override string ToString()
    {
        return $"[{m00:F4}, {m01:F4}]\n[{m10:F4}, {m11:F4}]";
    }
}