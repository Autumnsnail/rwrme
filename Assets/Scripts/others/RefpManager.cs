using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
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
        if (refpp == null)
        {
            Debug.LogWarning("RefpManager: refpi not found.");
            return;
        }

        var path = TryBrowseTextureFile("Select reference texture");
        if (string.IsNullOrEmpty(path))
            return;

        filePath = path;

        if (!File.Exists(filePath))
        {
            Debug.Log("NoFile");
            return;
        }

        var loadedTexture = new Texture2D(2, 2);
        var fileData = File.ReadAllBytes(filePath);
        if (loadedTexture.LoadImage(fileData))
        {
            refpp.transform.GetChild(0).GetComponent<Renderer>().material.mainTexture = loadedTexture;
            refpp.transform.position = new Vector3(0, 0, 0);
        }
    }

    private static string TryBrowseTextureFile(string title)
    {
        const string filter = "Image files|*.png;*.jpg;*.jpeg;*.tga;*.tif;*.tiff;*.bmp;*.gif|All files|*.*";
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        try
        {
            return RuntimeWindowsDialogs.ShowOpenFile(title, filter);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"Browse texture failed.\n{e.Message}");
            return null;
        }
#else
        try
        {
            var t = Type.GetType("System.Windows.Forms.OpenFileDialog, System.Windows.Forms");
            if (t == null)
            {
                Debug.LogWarning("System.Windows.Forms not available; cannot browse for texture.");
                return null;
            }

            var dlg = Activator.CreateInstance(t);
            t.GetProperty("Title")?.SetValue(dlg, title);
            t.GetProperty("Filter")?.SetValue(dlg, filter);
            var show = t.GetMethod("ShowDialog", Type.EmptyTypes);
            var result = show?.Invoke(dlg, null);
            var fileName = t.GetProperty("FileName")?.GetValue(dlg) as string;
            if (result != null && Convert.ToInt32(result) == 1 && !string.IsNullOrEmpty(fileName))
                return fileName;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"Browse texture failed.\n{e.Message}");
        }

        return null;
#endif
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
