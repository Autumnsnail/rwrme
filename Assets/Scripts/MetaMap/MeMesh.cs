using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MeMesh : MeRect
{
    public bool templated=false;
    public string template_ref;

    public bool reCollision = false;
    public Vector3 collisionSize = Vector3.one;

    public override float Rank => 0.2f;
    public MeMesh(Vector2 pos, float r, Vector2 s, string key, int lI) : base(pos, r, s, key, lI)
    {
    }

    void Start()
    {
    }

    public void SetReCollision(bool on)
    {
        reCollision = on;
        scatterThis();
    }

    public void SetCollisionSizeX(string value)
    {
        if (TryParseCollisionComponent(value, out float x))
        {
            collisionSize.x = x;
            scatterThis();
        }
    }

    public void SetCollisionSizeY(string value)
    {
        if (TryParseCollisionComponent(value, out float y))
        {
            collisionSize.y = y;
            scatterThis();
        }
    }

    public void SetCollisionSizeZ(string value)
    {
        if (TryParseCollisionComponent(value, out float z))
        {
            collisionSize.z = z;
            scatterThis();
        }
    }

    static bool TryParseCollisionComponent(string value, out float component)
    {
        return float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out component);
    }

    void UpdateCollisionUi()
    {
        Transform canvas = transform.Find("Canvas");
        if (canvas == null) return;
        MapItemParamUiLayout.Ensure(canvas);

        Toggle toggle = MapItemParamUiLayout.Find(canvas, "ToggleReCollision")?.GetComponent<Toggle>();
        if (toggle != null)
            toggle.SetIsOnWithoutNotify(reCollision);

        var inv = CultureInfo.InvariantCulture;
        SetCollisionFieldActive(canvas, "x_Collision", reCollision);
        SetCollisionFieldActive(canvas, "y_Collision", reCollision);
        SetCollisionFieldActive(canvas, "z_Collision", reCollision);
        if (!reCollision) return;

        MapItemParamUiLayout.Find(canvas, "x_Collision")?.GetComponent<TMP_InputField>()
            ?.SetTextWithoutNotify(collisionSize.x.ToString(inv));
        MapItemParamUiLayout.Find(canvas, "y_Collision")?.GetComponent<TMP_InputField>()
            ?.SetTextWithoutNotify(collisionSize.y.ToString(inv));
        MapItemParamUiLayout.Find(canvas, "z_Collision")?.GetComponent<TMP_InputField>()
            ?.SetTextWithoutNotify(collisionSize.z.ToString(inv));
    }

    static void SetCollisionFieldActive(Transform canvas, string childName, bool active)
    {
        Transform t = MapItemParamUiLayout.Find(canvas, childName);
        if (t != null)
            t.gameObject.SetActive(active);
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
            if (templated)
            {
                MeshTemplate foundTemplate = MetaMap.instance.meshTemplates.FirstOrDefault(template => template.name == template_ref);
                if (foundTemplate == null) return;

                go.transform.localScale = new Vector3 (size.x/2,foundTemplate.extend.y,size.y/2);
                go.transform.GetChild(0).gameObject.GetComponent<Renderer>().material.color = new Color(foundTemplate.color.r, foundTemplate.color.g, foundTemplate.color.b,0.5f);
                go.transform.GetChild(1).gameObject.GetComponent<Renderer>().material.color = new Color(foundTemplate.color.r, foundTemplate.color.g, foundTemplate.color.b,0.5f);

                Vector3 collisionExtend = reCollision ? collisionSize : foundTemplate.extend;
                go.transform.GetChild(1).localScale = new Vector3 (
                    collisionExtend.x / go.transform.localScale.x,
                    collisionExtend.y / go.transform.localScale.y,
                    collisionExtend.z / go.transform.localScale.z);

                if (OgreRuntimeImporter.TryGetFromLibrary(foundTemplate.meshName, out List<MeshLoader.Result> submeshes))
                {
                    go.transform.GetChild(0).gameObject.GetComponent<MeshFilter>().mesh = submeshes[0].Mesh;
                    go.transform.GetChild(0).gameObject.transform.localScale = new Vector3(1/go.transform.localScale.x,1/go.transform.localScale.y,1/go.transform.localScale.z);
                }
            }
            if (offset != Vector3.zero)
            {
                appOffset();
                updateOffsetShow();
            }
        }
        UpdateCollisionUi();
    }

    public override void scale(float scaler)
    {
        //disable
    }

    public override string IdPrefix { get { return "#mesh"; } }
    public override MapItem Duplicate()
    {
        MeMesh c = Instantiate(MapImporter.instate.MeshPref).GetComponent<MeMesh>();
        CopyMeRectFieldsTo(c);
        c.templated = templated;
        c.template_ref = template_ref;
        c.reCollision = reCollision;
        c.collisionSize = collisionSize;
        return c;
    }

    public override string getInfoText()
    {
        string ou="Mesh\n";
        ou += ("id:" + id);
        if (templated)
        {
            ou+=("\ntemplate:"+template_ref);
        }
        if (reCollision)
        {
            ou += "\ncollision_model_size = " + collisionSize.x.ToString(CultureInfo.InvariantCulture)
                + " " + collisionSize.y.ToString(CultureInfo.InvariantCulture)
                + " " + collisionSize.z.ToString(CultureInfo.InvariantCulture);
        }
        return ou; 
    }

    void Update()
    {
        
    }
}

