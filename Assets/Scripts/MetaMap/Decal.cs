using System;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Rendering;

public class Decal : MeRect
{
    // Start is called before the first frame update

    public string template_ref;

    public override float Rank => 0.4f;

    public float length = 1.0f;

    void Start()
    {
        
    }

    public Decal(Vector2 pos, float r, Vector2 s, string key, int lI) : base(pos, r, s, key, lI)
    {

    }

    public override string getInfoText()
    {
        return "Decal\n" + "id = " + id + "\n" + "layer = " + layerIndex.ToString() + "\n" + "template = " + template_ref + "\n" + "length = "+length;
    }
    // Update is called once per frame
    void Update()
    {
        
    }

    public void setLength(string value)
    {
        if (float.TryParse(value, out float length))
        {
            this.length = length;
            scatterThis();
        }
    }

    public override void scatterThis()
    {
        GameObject go = this.gameObject;
        if (go != null)
        {
            
            Vector3 troPos = new Vector3(0, 0, 0);
            VpMetaToucher.getXYHeightWithLayer(MathOfRwrme.SvgPosToU3dPos(position), layerIndex, ref troPos,Rank);
            go.transform.localPosition = troPos;
            go.transform.localScale = Vector3.one;
            go.transform.rotation = Quaternion.Euler(0f, -1 * rotation, 0f);
            DecalTemplate foundTemplate = MetaMap.instance.DecalTemplates.FirstOrDefault(template => template.name == template_ref);
            if(foundTemplate==null)return;

            Transform core = go.transform.GetChild(0);
            Renderer renderer = core.GetComponent<Renderer>();
            if (renderer == null) return;

            Material mat = renderer.material;
            ApplyFadeBlend(mat);
            const float decalAlpha = 0.5f;
            Texture2D tex = null;
            bool hasTexture = !string.IsNullOrEmpty(foundTemplate.textureName)
                && OgreRuntimeImporter.TryGetTextureFromLibrary(foundTemplate.textureName, out tex);

            if (hasTexture)
            {
                mat.mainTexture = tex;
                mat.color = new Color(1f, 1f, 1f, decalAlpha);
            }
            else
            {
                mat.mainTexture = null;
                mat.color = new Color(foundTemplate.color.r, foundTemplate.color.g, foundTemplate.color.b, decalAlpha);
            }

            Vector2 footprint = ComputeFootprintMeters(foundTemplate, hasTexture, tex);
            length = Mathf.Max(footprint.x, footprint.y);
            size = footprint;
            go.transform.localScale = new Vector3(footprint.x / 2f, 1f, footprint.y / 2f);

            MeshFilter meshFilter = core.GetComponent<MeshFilter>();
            if (meshFilter != null)
                foundTemplate.textureCut.ApplyAtlasUvToMesh(meshFilter.mesh);
        }
    }

    /// <summary>与 scatter 一致：较长边为 length，短边仅由图集切分或 size 宽高比决定。</summary>
    public Vector2 ComputeFootprintMeters()
    {
        if (MetaMap.instance == null)
            return FallbackFootprint();

        DecalTemplate foundTemplate = MetaMap.instance.DecalTemplates
            .FirstOrDefault(template => template.name == template_ref);
        if (foundTemplate == null)
            return FallbackFootprint();

        Texture2D tex = null;
        bool hasTexture = !string.IsNullOrEmpty(foundTemplate.textureName)
            && OgreRuntimeImporter.TryGetTextureFromLibrary(foundTemplate.textureName, out tex);
        return ComputeFootprintMeters(foundTemplate, hasTexture, tex);
    }

    Vector2 FallbackFootprint()
    {
        float longM = length > 0f ? length : Mathf.Max(Mathf.Max(size.x, size.y), 2f);
        if (size.x > 0f && size.y > 0f)
            return ComputeFootprintFromAspect(longM, size.x, size.y);
        return new Vector2(longM, longM);
    }

    Vector2 ComputeFootprintMeters(DecalTemplate foundTemplate, bool hasTexture, Texture2D tex)
    {
        float longM = length > 0f ? length : Mathf.Max(Mathf.Max(size.x, size.y), 2f);

        if (hasTexture && tex != null)
        {
            Vector2 cellPx = foundTemplate.textureCut.GetAtlasCellPixelSize(tex);
            return ComputeFootprintFromAspect(longM, cellPx.x, cellPx.y);
        }

        if (size.x > 0f && size.y > 0f)
            return ComputeFootprintFromAspect(longM, size.x, size.y);

        return new Vector2(longM, longM);
    }

    static Vector2 ComputeFootprintFromAspect(float longM, float axisX, float axisY)
    {
        float maxA = Mathf.Max(axisX, axisY);
        float minA = Mathf.Min(axisX, axisY);
        float shortM = maxA > 0f ? longM * (minA / maxA) : longM;
        if (axisX >= axisY)
            return new Vector2(longM, shortM);
        return new Vector2(shortM, longM);
    }

    /// <summary>导出 SVG rect 的 width/height（与 MapImporter 读取字段一致）。</summary>
    public Vector2 GetSvgExportSize() => ComputeFootprintMeters();

    static void ApplyFadeBlend(Material material)
    {
        if (material == null || !material.HasProperty("_Mode"))
            return;

        material.SetFloat("_Mode", 2f);
        material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
        material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
        material.SetInt("_ZWrite", 0);
        material.DisableKeyword("_ALPHATEST_ON");
        material.EnableKeyword("_ALPHABLEND_ON");
        material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        material.renderQueue = 3000;
    }

}
