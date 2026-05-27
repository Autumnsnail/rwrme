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

    void Start()
    {
        
    }

    public Decal(Vector2 pos, float r, Vector2 s, string key, int lI) : base(pos, r, s, key, lI)
    {

    }

    public override string getInfoText()
    {
        return "Decal\n" + "id = " + id + "\n" + "layer = " + layerIndex.ToString() + "\n" + "template = " + template_ref + "\n";
    }
    // Update is called once per frame
    void Update()
    {
        
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

            Vector2 footprint = foundTemplate.size;
            Transform core = go.transform.GetChild(0);
            Renderer renderer = core.GetComponent<Renderer>();
            if (renderer == null) return;

            Material mat = renderer.material;
            ApplyFadeBlend(mat);
            const float decalAlpha = 0.5f;
            const float metersPer100Pixels = 4f;

            if (!string.IsNullOrEmpty(foundTemplate.textureName)
                && OgreRuntimeImporter.TryGetTextureFromLibrary(foundTemplate.textureName, out Texture2D tex))
            {
                Vector2 cellPixels = foundTemplate.textureCut.GetAtlasCellPixelSize(tex);
                footprint = cellPixels * (metersPer100Pixels / 100f);

                mat.mainTexture = tex;
                mat.color = new Color(1f, 1f, 1f, decalAlpha);
            }
            else
            {
                mat.mainTexture = null;
                mat.color = new Color(foundTemplate.color.r, foundTemplate.color.g, foundTemplate.color.b, decalAlpha);
            }

            size = footprint;
            go.transform.localScale = new Vector3(footprint.x / 2f, 1f, footprint.y / 2f);

            MeshFilter meshFilter = core.GetComponent<MeshFilter>();
            if (meshFilter != null)
                foundTemplate.textureCut.ApplyAtlasUvToMesh(meshFilter.mesh);
        }
    }

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
