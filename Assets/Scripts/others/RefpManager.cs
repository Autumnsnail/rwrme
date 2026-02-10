using System.Collections;
using System.Collections.Generic;
using System.IO;
using Unity.VisualScripting;
using UnityEngine;

public class RefpManager : MonoBehaviour
{
    // refpicsManager
    // Start is called before the first frame update
    string filePath;

    GameObject refpp;
    void Start()
    {
        refpp = GameObject.Find("refpi");
        setRefpInvisable();
        filePath = null;
    }

    public void setRefpInvisable()
    {

        refpp.transform.position = new Vector3(0, -100, 0);
    }

    public void importTexture()
    {
        if (File.Exists(filePath))
        {

            Texture2D loadedTexture = new Texture2D(2, 2);
            byte[] fileData = File.ReadAllBytes(filePath);
            if (loadedTexture.LoadImage(fileData))
            {
                refpp.transform.GetChild(0).GetComponent<Renderer>().material.mainTexture = loadedTexture;
                refpp.transform.position = new Vector3(0, 0, 0);
            }
        }
        else
        {
            Debug.Log("NoFile");
        }
    }

    public void setPathName(string path)
    {
        filePath = path;
    }
    public void setAlpha(float alp)
    {
        Debug.Log($"{alp}");
        //refpp.transform.GetChild(0).GetComponent<Renderer>().material.SetColor("_BaseColor", new Color(1, 1, 1, alp));
        refpp.transform.GetChild(0).GetComponent<Renderer>().material.color = new Color(1, 1, 1, alp);
    }

    public void setScaleX(string sx)
    {
        float f1 = 1.0f;
        if (float.TryParse(sx, out float result))
        {
            f1 = result;
        }
        Vector3 ve = refpp.transform.localScale;
        ve.x = f1;
        refpp.transform.localScale = ve;
    }
    public void setScaleY(string sx)
    {
        float f1 = 1.0f;
        if (float.TryParse(sx, out float result))
        {
            f1 = result;
        }
        Vector3 ve = refpp.transform.localScale;
        ve.z = f1;
        refpp.transform.localScale = ve;
    }
    public void setOffsetX(string sx)
    {
        float f1 = 1.0f;
        if (float.TryParse(sx, out float result))
        {
            f1 = result;
        }
        Vector3 ve = refpp.transform.position;
        ve.x = f1;
        refpp.transform.position = ve;
    }
    public void setOffsetY(string sx)
    {
        float f1 = 1.0f;
        if (float.TryParse(sx, out float result))
        {
            f1 = result;
        }
        Vector3 ve = refpp.transform.position;
        ve.z = f1;
        refpp.transform.position = ve;
    }
    // Update is called once per frame
    void Update()
    {
        
    }
}
