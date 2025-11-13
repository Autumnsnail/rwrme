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
    // Update is called once per frame
    void Update()
    {
        
    }
}
