using UnityEngine;
using System.Collections;
using System.IO;

public class TerrainHeightmapFromTexture : MonoBehaviour
{
    [Header("��������")]
    public Terrain targetTerrain;

    [Header("�Ҷ�ͼ����")]
    public Texture2D heightmapTexture;
    public bool flipVertically = false;
    public bool flipHorizontally = false;

    [Header("�߶ȵ���")]
    [Range(0f, 1f)]
    public float maxHeight = 1f;
    [Range(0f, 1f)]
    public float minHeight = 0f;

    [Header("��������")]
    public bool enableLogging = true;
    public bool previewInEditor = false;

    private void Start()
    {
        if (targetTerrain == null)
        {
            targetTerrain = GetComponent<Terrain>();
            if (targetTerrain == null)
            {
                Debug.LogError("δ�ҵ�����������뽫�ű����ص����ζ����ϻ�ָ��Ŀ����Ρ�");
                return;
            }
        }

        if (heightmapTexture != null)
        {
            ApplyHeightmap();
        }
        else
        {
            Debug.LogWarning("δָ���Ҷ�ͼ������");
        }
    }

    [ContextMenu("Ӧ�ø߶�ͼ")]
    public void ApplyHeightmap()
    {
        if (targetTerrain == null || heightmapTexture == null)
        {
            Debug.LogError("���λ�Ҷ�ͼΪ�գ�");
            return;
        }

        Log("��ʼӦ�ûҶ�ͼ������...");
        Log($"�Ҷ�ͼ�ߴ�: {heightmapTexture.width} x {heightmapTexture.height}");

        // ��ȡ��������
        TerrainData terrainData = targetTerrain.terrainData;
        int resolution = terrainData.heightmapResolution;
        Log($"���θ߶�ͼ�ֱ���: {resolution}");

        // �����߶�����
        float[,] heights = new float[resolution, resolution];

        // �����Ҷ�ͼ����
        ProcessHeightmapTexture(heights, resolution);

        // Ӧ�ø߶����ݵ�����
        Log("�������õ��θ߶�...");
        terrainData.SetHeights(0, 0, heights);

        Log("���θ߶�������ɣ�");
        Log($"���ո߶ȷ�Χ: {minHeight} �� {maxHeight}");

        if (previewInEditor)
        {
            Debug.Log("Ԥ��ģʽ������ - �ڱ༭���в鿴���α仯");
        }
    }

    private void ProcessHeightmapTexture(float[,] heights, int resolution)
    {
        Log("��ʼ�����Ҷ�ͼ����...");

        int textureWidth = heightmapTexture.width;
        int textureHeight = heightmapTexture.height;

        // ȷ�������ɶ�
        if (!heightmapTexture.isReadable)
        {
            Debug.LogError("�Ҷ�ͼ�������ɶ������ڵ������������� 'Read/Write Enabled'");
            return;
        }

        // ��ȡ����������ɫ
        Color[] pixels = heightmapTexture.GetPixels();
        Log($"�ѻ�ȡ {pixels.Length} ����������");

        float minFound = 1f;
        float maxFound = 0f;

        for (int y = 0; y < resolution; y++)
        {
            for (int x = 0; x < resolution; x++)
            {
                // ������������
                int texX = (int)((float)x / resolution * textureWidth);
                int texY = (int)((float)y / resolution * textureHeight);

                // ������ת
                if (flipHorizontally) texX = textureWidth - 1 - texX;
                if (flipVertically) texY = textureHeight - 1 - texY;

                // ȷ����������Ч��Χ��
                texX = Mathf.Clamp(texX, 0, textureWidth - 1);
                texY = Mathf.Clamp(texY, 0, textureHeight - 1);

                // ��ȡ��������
                int pixelIndex = texY * textureWidth + texX;

                if (pixelIndex >= 0 && pixelIndex < pixels.Length)
                {
                    // ��ȡ�Ҷ�ֵ��ʹ��RGB��ƽ��ֵ�򵥶�ͨ����
                    Color pixel = pixels[pixelIndex];
                    float grayValue = (pixel.r + pixel.g + pixel.b) / 3f;

                    // ��¼�ҵ�����С�����ֵ
                    if (grayValue < minFound) minFound = grayValue;
                    if (grayValue > maxFound) maxFound = grayValue;

                    // Ӧ�ø߶ȷ�Χ����
                    float adjustedHeight = Mathf.Lerp(minHeight, maxHeight, grayValue);

                    heights[y, x] = adjustedHeight;
                }
                else
                {
                    heights[y, x] = 0f;
                    if (enableLogging && (x == 0 || y == 0))
                    {
                        Debug.LogWarning($"��������������Χ: {pixelIndex}, ����: ({x},{y})");
                    }
                }
            }

            // ÿ����10%�������һ�ν���
            if (enableLogging && y % (resolution / 10) == 0)
            {
                float progress = (float)y / resolution * 100f;
                Log($"��������: {progress:F1}%");
            }
        }

        Log($"�Ҷ�ͼ���ݷ�Χ: {minFound:F3} �� {maxFound:F3}");
        Log("�Ҷ�ͼ���ݴ������");
    }

    [ContextMenu("���õ��θ߶�")]
    public void ResetTerrainHeight()
    {
        if (targetTerrain != null)
        {
            TerrainData terrainData = targetTerrain.terrainData;
            int resolution = terrainData.heightmapResolution;
            float[,] heights = new float[resolution, resolution];
            terrainData.SetHeights(0, 0, heights);
            Log("���θ߶�������Ϊƽ��");
        }
    }

    [ContextMenu("��ӡ������Ϣ")]
    public void PrintTerrainInfo()
    {
        if (targetTerrain != null)
        {
            TerrainData data = targetTerrain.terrainData;
            Log("=== ������Ϣ ===");
            Log($"�ߴ�: {data.size}");
            Log($"�߶�ͼ�ֱ���: {data.heightmapResolution}");
            Log($"����λ��: {targetTerrain.transform.position}");
            Log($"������ת: {targetTerrain.transform.rotation}");
            Log($"��������: {targetTerrain.transform.localScale}");
        }
    }

    [ContextMenu("�����θ߶�ͼ")]
    public void ExportHeightmap()
    {
        if (targetTerrain == null)
        {
            Debug.LogError("Ŀ����Ϊ�գ��޷����");
            return;
        }

        Log("��ʼ�����θ߶�ͼ...");

        // ��ȡ��������
        TerrainData terrainData = targetTerrain.terrainData;
        int resolution = terrainData.heightmapResolution;
        Log($"���θ߶�ͼ�ֱ���: {resolution}");

        // ��ȡ��ǰ���θ߶�����
        float[,] heights = terrainData.GetHeights(0, 0, resolution, resolution);

        // ��������
        Texture2D exportTexture = new Texture2D(resolution, resolution, TextureFormat.RGB24, false);

        // �ҵ�ʵ�ʵĸ߶ȷ�Χ���������һ����
        float actualMin = float.MaxValue;
        float actualMax = float.MinValue;

        // ��ȼɨ��һ���ҵ�ʵ�ʸ߶ȷ�Χ
        for (int y = 0; y < resolution; y++)
        {
            for (int x = 0; x < resolution; x++)
            {
                float height = heights[y, x];
                if (height < actualMin) actualMin = height;
                if (height > actualMax) actualMax = height;
            }
        }

        Log($"ʵ�ʸ߶ȷ�Χ: {actualMin:F3} �� {actualMax:F3}");

        // ת���߶����ݴ�����
        Color[] pixels = new Color[resolution * resolution];

        for (int y = 0; y < resolution; y++)
        {
            for (int x = 0; x < resolution; x++)
            {
                // ��ȡ�߶�ֵ
                float height = heights[y, x];

                // �����߶�ֵ��һ����0-1��Χ
                float normalizedHeight;
                if (actualMax > actualMin)
                {
                    normalizedHeight = (height - actualMin) / (actualMax - actualMin);
                }
                else
                {
                    normalizedHeight = 0.5f; // ��������������ƽ̹��ʹ���м�ֵ
                }

                // Ӧ����ת�����������Ļ����뵼��ʱ����һ�£�
                int texX = flipHorizontally ? (resolution - 1 - x) : x;
                int texY = flipVertically ? (resolution - 1 - y) : y;

                // ����������
                int pixelIndex = texY * resolution + texX;

                // �����Ҷ���ɫ
                Color grayColor = new Color(normalizedHeight, normalizedHeight, normalizedHeight, 1f);
                pixels[pixelIndex] = grayColor;
            }

            // ÿ����10%����һ�ν���
            if (enableLogging && y % (resolution / 10) == 0)
            {
                float progress = (float)y / resolution * 100f;
                Log($"������: {progress:F1}%");
            }
        }

        // ���������ص�����
        exportTexture.SetPixels(pixels);
        exportTexture.Apply();

        // ����ΪPNG�ļ�
        string fileName = $"TerrainHeightmap_{System.DateTime.Now:yyyyMMdd_HHmmss}.png";
        string filePath = Path.Combine(Application.dataPath, fileName);

        byte[] pngData = exportTexture.EncodeToPNG();
        File.WriteAllBytes(filePath, pngData);

        // �������ʱ����
        DestroyImmediate(exportTexture);

        Log($"���θ߶�ͼ�ѵ�����: {filePath}");
        Log($"�����ĸ߶ȷ�Χ: {actualMin:F3} �� {actualMax:F3}");
        Log("���������");

#if UNITY_EDITOR
        // �ڱ༭���ˢ����ʻ����
        UnityEditor.AssetDatabase.Refresh();
        Log("���ˢ��Unity��ʻ����");
#endif
    }

    private void Log(string message)
    {
        if (enableLogging)
        {
            Debug.Log($"[TerrainHeightmap] {message}");
        }
    }

    // ��Inspector����֤����
    private void OnValidate()
    {
        if (maxHeight < minHeight)
        {
            maxHeight = minHeight + 0.01f;
        }

        if (minHeight < 0f) minHeight = 0f;
        if (maxHeight > 1f) maxHeight = 1f;
    }
}